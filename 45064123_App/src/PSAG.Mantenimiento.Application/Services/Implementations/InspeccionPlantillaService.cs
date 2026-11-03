using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Inspeccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Maestro de planes de inspección (Maestros &gt; Planes de inspección). Un plan = un equipo de la línea con
/// los puntos de su planilla. Así se suman otros equipos más adelante sin tocar código ni scripts.
/// </summary>
public class InspeccionPlantillaService : IInspeccionPlantillaService
{
    private const int LargoCodigoPlan = 20;
    private const int LargoNombrePlan = 100;
    private const int LargoDescripcionPlan = 500;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InspeccionPlantillaService> _logger;

    public InspeccionPlantillaService(IUnitOfWork unitOfWork, ILogger<InspeccionPlantillaService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<PlanInspeccionConItemsDto>>> ObtenerPlantillaAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        var linea = await _unitOfWork.Lineas.GetByIdAsync(lineaId, cancellationToken);
        if (linea == null)
            return Result<IReadOnlyList<PlanInspeccionConItemsDto>>.Failure("NOT_FOUND", "Línea no encontrada.");

        var planes = await _unitOfWork.PlanesInspeccion.GetPlantillaLineaAsync(lineaId, incluirInactivos: true, cancellationToken);
        var dtos = planes.Select(p => InspeccionMapeo.APlanConItems(p, p.Items)).ToList();
        return Result<IReadOnlyList<PlanInspeccionConItemsDto>>.Success(dtos);
    }

    public async Task<Result<int>> CrearPlanAsync(GuardarPlanInspeccionRequest request, string usuarioId, CancellationToken cancellationToken = default)
    {
        var linea = await _unitOfWork.Lineas.GetByIdAsync(request.LineaId, cancellationToken);
        if (linea == null)
            return Result<int>.Failure("NOT_FOUND", "Línea no encontrada.");

        var (error, puesto) = await ValidarPlanAsync(request, request.LineaId, null, cancellationToken);
        if (error != null)
            return Result<int>.Failure("VALIDATION", error);

        var codigo = Recortar(request.Codigo);
        if (codigo == null)
        {
            var n = await _unitOfWork.PlanesInspeccion.CountByLineaAsync(request.LineaId, cancellationToken) + 1;
            do { codigo = $"INS-L{request.LineaId}-{n++:00}"; }
            while (await _unitOfWork.PlanesInspeccion.ExisteCodigoAsync(codigo, null, cancellationToken));
        }

        var plan = new PlanInspeccion { LineaId = request.LineaId, Codigo = codigo };
        Aplicar(plan, request, puesto);

        await _unitOfWork.PlanesInspeccion.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Plan de inspección {PlanId} ({Codigo}) creado en línea {LineaId} por {UsuarioId}",
            plan.PlanInspeccionId, plan.Codigo, plan.LineaId, usuarioId);
        return Result<int>.Success(plan.PlanInspeccionId);
    }

    public async Task<Result> ActualizarPlanAsync(int planInspeccionId, GuardarPlanInspeccionRequest request, string usuarioId, CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.PlanesInspeccion.GetByIdAsync(planInspeccionId, cancellationToken);
        if (plan == null)
            return Result.Failure("NOT_FOUND", "Plan de inspección no encontrado.");

        var (error, puesto) = await ValidarPlanAsync(request, plan.LineaId, planInspeccionId, cancellationToken);
        if (error != null)
            return Result.Failure("VALIDATION", error);

        plan.Codigo = Recortar(request.Codigo) ?? plan.Codigo;
        Aplicar(plan, request, puesto);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<int>> CrearItemAsync(GuardarItemInspeccionRequest request, string usuarioId, CancellationToken cancellationToken = default)
    {
        var plan = await _unitOfWork.PlanesInspeccion.GetByIdAsync(request.PlanInspeccionId, cancellationToken);
        if (plan == null)
            return Result<int>.Failure("NOT_FOUND", "Plan de inspección no encontrado.");

        var items = await _unitOfWork.PlanesInspeccion.GetItemsAsync(plan.PlanInspeccionId, cancellationToken);
        var (error, quien) = ValidarItem(request, items, null);
        if (error != null)
            return Result<int>.Failure("VALIDATION", error);

        var numero = request.NumeroItem > 0 ? request.NumeroItem : (items.Count == 0 ? 1 : items.Max(i => i.NumeroItem) + 1);
        var codigo = Recortar(request.Codigo);
        if (codigo == null)
        {
            var n = numero;
            do { codigo = $"{plan.Codigo}-{n++:00}"; }
            while (items.Any(i => i.Codigo == codigo));
        }

        var item = new ItemPlanInspeccion { PlanInspeccionId = plan.PlanInspeccionId, Codigo = codigo };
        Aplicar(item, request, numero, quien);

        await _unitOfWork.PlanesInspeccion.AddItemAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Punto de inspección {ItemId} creado en plan {PlanId} por {UsuarioId}",
            item.ItemPlanInspeccionId, plan.PlanInspeccionId, usuarioId);
        return Result<int>.Success(item.ItemPlanInspeccionId);
    }

    public async Task<Result> ActualizarItemAsync(int itemPlanInspeccionId, GuardarItemInspeccionRequest request, string usuarioId, CancellationToken cancellationToken = default)
    {
        var item = await _unitOfWork.PlanesInspeccion.GetItemByIdAsync(itemPlanInspeccionId, cancellationToken);
        if (item == null)
            return Result.Failure("NOT_FOUND", "Punto de inspección no encontrado.");

        // Las respuestas quedan atadas al plan (equipo) en que se dieron: un punto respondido no se mueve.
        if (request.PlanInspeccionId != 0 && request.PlanInspeccionId != item.PlanInspeccionId
            && await _unitOfWork.PlanesInspeccion.ItemTieneRespuestasAsync(itemPlanInspeccionId, cancellationToken))
            return Result.Failure("VALIDATION", "El punto ya tiene respuestas: no se puede pasar a otro plan. Desactivalo y creá uno nuevo.");

        var planId = request.PlanInspeccionId != 0 ? request.PlanInspeccionId : item.PlanInspeccionId;
        var plan = await _unitOfWork.PlanesInspeccion.GetByIdAsync(planId, cancellationToken);
        if (plan == null)
            return Result.Failure("NOT_FOUND", "Plan de inspección no encontrado.");

        var items = await _unitOfWork.PlanesInspeccion.GetItemsAsync(planId, cancellationToken);
        var (error, quien) = ValidarItem(request, items, itemPlanInspeccionId);
        if (error != null)
            return Result.Failure("VALIDATION", error);

        item.PlanInspeccionId = planId;
        item.Codigo = Recortar(request.Codigo) ?? item.Codigo;
        Aplicar(item, request, request.NumeroItem > 0 ? request.NumeroItem : item.NumeroItem, quien);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<(string? Error, TipoPuesto Puesto)> ValidarPlanAsync(
        GuardarPlanInspeccionRequest request, int lineaId, int? planId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return ("El nombre del plan es obligatorio.", default);
        if (request.Nombre.Trim().Length > LargoNombrePlan)
            return ($"El nombre no puede superar {LargoNombrePlan} caracteres.", default);
        if (request.Descripcion?.Trim().Length > LargoDescripcionPlan)
            return ($"La descripción no puede superar {LargoDescripcionPlan} caracteres.", default);
        if (!InspeccionReglas.TryLeerPuesto(request.PuestoResponsable, out var puesto) || puesto == null)
            return ("El responsable no es válido.", default);
        if (request.FrecuenciaDias < 1 || request.FrecuenciaDias > InspeccionReglas.FrecuenciaMaximaDias)
            return ($"La frecuencia tiene que ser entre 1 y {InspeccionReglas.FrecuenciaMaximaDias} días.", default);

        var codigo = Recortar(request.Codigo);
        if (codigo != null)
        {
            if (codigo.Length > LargoCodigoPlan)
                return ($"El código no puede superar {LargoCodigoPlan} caracteres.", default);
            if (await _unitOfWork.PlanesInspeccion.ExisteCodigoAsync(codigo, planId, cancellationToken))
                return ($"Ya hay un plan con el código {codigo}.", default);
        }

        if (request.EquipoId.HasValue)
        {
            if (!await _unitOfWork.Equipos.PerteneceALineaAsync(request.EquipoId.Value, lineaId, cancellationToken))
                return ("El equipo tiene que ser de la misma línea.", default);
        }

        return (null, puesto.Value);
    }

    private static (string? Error, TipoPuesto Quien) ValidarItem(GuardarItemInspeccionRequest request, IReadOnlyList<ItemPlanInspeccion> items, int? itemId)
    {
        if (!InspeccionReglas.TryLeerPuesto(request.QuienRealiza, out var quien) || quien == null)
            return ("Quién realiza no es válido.", default);

        var error = InspeccionReglas.MotivoDefinicionInvalida(
            request.ParteEquipo, request.Tareas, request.Estandar, request.Metodo, request.Codigo, quien.Value, request.FrecuenciaDias);
        if (error != null)
            return (error, default);

        var codigo = Recortar(request.Codigo);
        if (codigo != null && items.Any(i => i.Codigo == codigo && i.ItemPlanInspeccionId != itemId))
            return ($"Ya hay un punto con el código {codigo} en este plan.", default);

        return (null, quien.Value);
    }

    private static void Aplicar(PlanInspeccion plan, GuardarPlanInspeccionRequest request, TipoPuesto puesto)
    {
        plan.EquipoId = request.EquipoId;
        plan.Nombre = request.Nombre.Trim();
        plan.Descripcion = Recortar(request.Descripcion);
        plan.PuestoResponsable = puesto;
        plan.FrecuenciaDias = request.FrecuenciaDias;
        plan.Orden = request.Orden;
        plan.Activo = request.Activo;
    }

    private static void Aplicar(ItemPlanInspeccion item, GuardarItemInspeccionRequest request, int numero, TipoPuesto quien)
    {
        item.NumeroItem = numero;
        item.ParteEquipo = request.ParteEquipo.Trim();
        item.Descripcion = request.Tareas.Trim();
        item.CondicionEstandar = request.Estandar.Trim();
        item.MetodoDetalle = Recortar(request.Metodo);
        item.QuienRealiza = quien;
        item.FrecuenciaDias = request.FrecuenciaDias;
        item.Orden = request.Orden > 0 ? request.Orden : numero;
        item.Activo = request.Activo;
    }

    private static string? Recortar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

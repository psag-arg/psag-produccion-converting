using PSAG.Mantenimiento.Domain.Permisos;
using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Maestros de producción (T-798 etapa 6, diseño 2.5). Valida lo mismo que los CHECK de la base, con mensajes claros.
/// La parametrización se edita sin miedo: cada tramo de producto del turno guarda su snapshot del objetivo.
/// </summary>
public class MaestrosProduccionService : IMaestrosProduccionService
{
    public const string MensajeSinPermiso = "Los productos y la parametrización línea + producto los administra el administrador.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly ILogger<MaestrosProduccionService> _logger;

    /// <summary>Permisos configurables del usuario de la request (T-802); null fuera de una request (tests, jobs): regla por rol.</summary>
    private readonly IPermisosUsuarioActual? _permisos;

    public MaestrosProduccionService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, ILogger<MaestrosProduccionService> logger, IPermisosUsuarioActual? permisos = null)
    {
        _permisos = permisos;
        _unitOfWork = unitOfWork;
        _zonaPlanta = zonaPlanta;
        _logger = logger;
    }

    // ===================== Productos maestro =====================

    public async Task<IReadOnlyList<ProductoMaestroDto>> ObtenerProductosAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        var productos = (incluirInactivos
                ? await _unitOfWork.ProductosMaestro.GetAllAsync(cancellationToken)
                : await _unitOfWork.ProductosMaestro.GetActivosAsync(cancellationToken))
            .OrderBy(p => p.Orden).ThenBy(p => p.Nombre).ToList();
        var skus = await _unitOfWork.ProductosSku.GetPorMaestrosAsync(productos.Select(p => p.ProductoMaestroId).ToList(), cancellationToken);
        return productos.Select(p => ADto(p, skus.Where(s => s.ProductoMaestroId == p.ProductoMaestroId))).ToList();
    }

    public async Task<ProductoMaestroDto?> ObtenerProductoAsync(int productoMaestroId, CancellationToken cancellationToken = default)
    {
        var p = await _unitOfWork.ProductosMaestro.GetByIdAsync(productoMaestroId, cancellationToken);
        if (p == null)
            return null;
        return ADto(p, await _unitOfWork.ProductosSku.GetPorMaestrosAsync([productoMaestroId], cancellationToken));
    }

    public async Task<Result<ProductoMaestroDto>> CrearProductoAsync(
        GuardarProductoMaestroRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!await _permisos.PermiteAsync(Opciones.MaestrosProductos, AccionPermiso.Alta, () => CalidadReglas.PuedeAdministrarMaestros(roles), cancellationToken))
            return Result<ProductoMaestroDto>.Failure("FORBIDDEN", MensajeSinPermiso);
        var motivo = await MotivoProductoInvalidoAsync(request, null, cancellationToken);
        if (motivo != null)
            return Result<ProductoMaestroDto>.Failure("VALIDATION", motivo);

        var producto = new ProductoMaestro { CreadoPorUsuarioId = usuarioId, FechaHoraCreacion = DateTimeOffset.UtcNow };
        Aplicar(producto, request, usuarioId);
        await _unitOfWork.ProductosMaestro.AddAsync(producto, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Producto maestro {Codigo} creado por usuario {UsuarioId}", producto.Codigo, usuarioId);
        return Result<ProductoMaestroDto>.Success((await ObtenerProductoAsync(producto.ProductoMaestroId, cancellationToken))!);
    }

    public async Task<Result<ProductoMaestroDto>> ActualizarProductoAsync(
        int productoMaestroId, GuardarProductoMaestroRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!await _permisos.PermiteAsync(Opciones.MaestrosProductos, AccionPermiso.Modificar, () => CalidadReglas.PuedeAdministrarMaestros(roles), cancellationToken))
            return Result<ProductoMaestroDto>.Failure("FORBIDDEN", MensajeSinPermiso);
        var producto = await _unitOfWork.ProductosMaestro.GetByIdAsync(productoMaestroId, cancellationToken);
        if (producto == null)
            return Result<ProductoMaestroDto>.Failure("NOT_FOUND", "Producto no encontrado.");
        var motivo = await MotivoProductoInvalidoAsync(request, productoMaestroId, cancellationToken);
        if (motivo != null)
            return Result<ProductoMaestroDto>.Failure("VALIDATION", motivo);

        Aplicar(producto, request, usuarioId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Producto maestro {Codigo} actualizado por usuario {UsuarioId} (activo: {Activo})", producto.Codigo, usuarioId, producto.Activo);
        return Result<ProductoMaestroDto>.Success((await ObtenerProductoAsync(productoMaestroId, cancellationToken))!);
    }

    private async Task<string?> MotivoProductoInvalidoAsync(GuardarProductoMaestroRequest r, int? id, CancellationToken ct)
    {
        var excluir = id ?? 0;   // fuera de la expresión: EF no traduce (int? ?? 0) en la comparación
        var codigo = r.Codigo?.Trim() ?? string.Empty;
        if (codigo.Length == 0) return "Completá el código.";
        if (codigo.Length > 50) return "El código admite hasta 50 caracteres.";
        if (string.IsNullOrWhiteSpace(r.Nombre)) return "Completá el nombre.";
        if (r.Nombre.Trim().Length > 100) return "El nombre admite hasta 100 caracteres.";
        if (r.Descripcion?.Length > 500) return "La descripción admite hasta 500 caracteres.";
        if (r.TipoProducto?.Trim().Length > 30) return "El tipo de producto admite hasta 30 caracteres.";
        if (r.MetrosPorManga is <= 0) return "Los metros por manga tienen que ser mayores que 0 (o vacío si no se sabe).";
        if (r.RollosPorManga is <= 0) return "Los rollos por manga tienen que ser mayores que 0 (o vacío si no se sabe).";
        if (r.RollosPorPaquete is <= 0) return "Los rollos por paquete tienen que ser mayores que 0 (o vacío si no se sabe).";
        if (await _unitOfWork.ProductosMaestro.ExistsAsync(p => p.Codigo == codigo && p.ProductoMaestroId != excluir, ct))
            return $"Ya existe un producto con el código {codigo}.";
        return null;
    }

    private static void Aplicar(ProductoMaestro p, GuardarProductoMaestroRequest r, string usuarioId)
    {
        p.Codigo = r.Codigo.Trim();
        p.Nombre = r.Nombre.Trim();
        p.Descripcion = string.IsNullOrWhiteSpace(r.Descripcion) ? null : r.Descripcion.Trim();
        p.Orden = r.Orden;
        p.TipoProducto = string.IsNullOrWhiteSpace(r.TipoProducto) ? null : r.TipoProducto.Trim();
        p.MetrosPorManga = r.MetrosPorManga;
        p.RollosPorManga = r.RollosPorManga;
        p.RollosPorPaquete = r.RollosPorPaquete;
        p.Activo = r.Activo;
        p.ModificadoPorUsuarioId = usuarioId;
        p.FechaHoraModificacion = DateTimeOffset.UtcNow;
    }

    private static ProductoMaestroDto ADto(ProductoMaestro p, IEnumerable<ProductoSku> skus) => new()
    {
        ProductoMaestroId = p.ProductoMaestroId,
        Codigo = p.Codigo,
        Nombre = p.Nombre,
        Descripcion = p.Descripcion,
        Orden = p.Orden,
        Activo = p.Activo,
        TipoProducto = p.TipoProducto,
        MetrosPorManga = p.MetrosPorManga,
        RollosPorManga = p.RollosPorManga,
        RollosPorPaquete = p.RollosPorPaquete,
        Skus = skus.Select(ADto).ToList()
    };

    // ===================== SKU =====================

    public async Task<Result<ProductoSkuDto>> CrearSkuAsync(GuardarProductoSkuRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!await _permisos.PermiteAsync(Opciones.MaestrosProductos, AccionPermiso.Alta, () => CalidadReglas.PuedeAdministrarMaestros(roles), cancellationToken))
            return Result<ProductoSkuDto>.Failure("FORBIDDEN", MensajeSinPermiso);
        var motivo = await MotivoSkuInvalidoAsync(request, null, cancellationToken);
        if (motivo != null)
            return Result<ProductoSkuDto>.Failure("VALIDATION", motivo);

        var sku = new ProductoSku();
        Aplicar(sku, request);
        await _unitOfWork.ProductosSku.AddAsync(sku, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ProductoSkuDto>.Success(await SkuDtoAsync(sku.ProductoSkuId, cancellationToken));
    }

    public async Task<Result<ProductoSkuDto>> ActualizarSkuAsync(int productoSkuId, GuardarProductoSkuRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!await _permisos.PermiteAsync(Opciones.MaestrosProductos, AccionPermiso.Modificar, () => CalidadReglas.PuedeAdministrarMaestros(roles), cancellationToken))
            return Result<ProductoSkuDto>.Failure("FORBIDDEN", MensajeSinPermiso);
        var sku = await _unitOfWork.ProductosSku.GetByIdAsync(productoSkuId, cancellationToken);
        if (sku == null)
            return Result<ProductoSkuDto>.Failure("NOT_FOUND", "SKU no encontrado.");
        var motivo = await MotivoSkuInvalidoAsync(request, productoSkuId, cancellationToken);
        if (motivo == null && (sku.Activo && !request.Activo || request.TipoSku?.Trim().ToUpperInvariant() != ProductoSku.TipoBolson)
            && await _unitOfWork.ProductosSku.ExistsAsync(s => s.SkuContenidoId == productoSkuId && s.Activo, cancellationToken))
            motivo = "Lo usa un SKU pallet activo. Cambiale el bolsón o desactivalo primero.";
        if (motivo != null)
            return Result<ProductoSkuDto>.Failure("VALIDATION", motivo);

        Aplicar(sku, request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ProductoSkuDto>.Success(await SkuDtoAsync(productoSkuId, cancellationToken));
    }

    private async Task<string?> MotivoSkuInvalidoAsync(GuardarProductoSkuRequest r, int? id, CancellationToken ct)
    {
        var excluir = id ?? 0;   // fuera de la expresión: EF no traduce (int? ?? 0) en la comparación
        var codigo = r.CodigoSku?.Trim() ?? string.Empty;
        var tipo = r.TipoSku?.Trim().ToUpperInvariant();
        if (await _unitOfWork.ProductosMaestro.GetByIdAsync(r.ProductoMaestroId, ct) == null)
            return "Elegí el producto maestro del SKU.";
        if (codigo.Length == 0) return "Completá el código del SKU.";
        if (codigo.Length > 50) return "El código del SKU admite hasta 50 caracteres.";
        if (r.Descripcion?.Length > 200) return "La descripción admite hasta 200 caracteres.";
        if (tipo is not (ProductoSku.TipoBolson or ProductoSku.TipoPallet)) return "El tipo de SKU es BOLSON o PALLET.";
        if (tipo == ProductoSku.TipoBolson && r.PaquetesPorBolson is <= 0)
            return "Los paquetes por bolsón tienen que ser mayores que 0 (o vacío si no se sabe).";
        if (tipo == ProductoSku.TipoPallet)
        {
            if (r.BolsonesPorPallet is <= 0)
                return "Los bolsones por pallet tienen que ser mayores que 0 (o vacío si no se sabe).";
            if (r.SkuContenidoId is { } contenidoId)
            {
                var contenido = await _unitOfWork.ProductosSku.GetByIdAsync(contenidoId, ct);
                if (contenido == null || contenido.TipoSku != ProductoSku.TipoBolson || contenido.ProductoMaestroId != r.ProductoMaestroId)
                    return "El pallet tiene que llevar un SKU bolsón del mismo producto.";
                if (!contenido.Activo)
                    return "El SKU bolsón elegido está inactivo.";
            }
        }
        if (await _unitOfWork.ProductosSku.ExistsAsync(s => s.CodigoSku == codigo && s.ProductoSkuId != excluir, ct))
            return $"Ya existe un SKU con el código {codigo}.";
        return null;
    }

    private static void Aplicar(ProductoSku s, GuardarProductoSkuRequest r)
    {
        var tipo = r.TipoSku.Trim().ToUpperInvariant();
        s.ProductoMaestroId = r.ProductoMaestroId;
        s.CodigoSku = r.CodigoSku.Trim();
        s.Descripcion = string.IsNullOrWhiteSpace(r.Descripcion) ? null : r.Descripcion.Trim();
        s.TipoSku = tipo;
        // Cada dato vive en un solo tipo (mismos CHECK que la base).
        s.PaquetesPorBolson = tipo == ProductoSku.TipoBolson ? r.PaquetesPorBolson : null;
        s.SkuContenidoId = tipo == ProductoSku.TipoPallet ? r.SkuContenidoId : null;
        s.BolsonesPorPallet = tipo == ProductoSku.TipoPallet ? r.BolsonesPorPallet : null;
        s.Activo = r.Activo;
    }

    private async Task<ProductoSkuDto> SkuDtoAsync(int productoSkuId, CancellationToken ct)
    {
        var sku = await _unitOfWork.ProductosSku.GetByIdAsync(productoSkuId, ct);
        var todos = await _unitOfWork.ProductosSku.GetPorMaestrosAsync([sku!.ProductoMaestroId], ct);
        return ADto(todos.Single(s => s.ProductoSkuId == productoSkuId));
    }

    private static ProductoSkuDto ADto(ProductoSku s) => new()
    {
        ProductoSkuId = s.ProductoSkuId,
        ProductoMaestroId = s.ProductoMaestroId,
        CodigoSku = s.CodigoSku,
        Descripcion = s.Descripcion,
        TipoSku = s.TipoSku,
        PaquetesPorBolson = s.PaquetesPorBolson,
        SkuContenidoId = s.SkuContenidoId,
        SkuContenidoCodigo = s.SkuContenido?.CodigoSku,
        BolsonesPorPallet = s.BolsonesPorPallet,
        Activo = s.Activo
    };

    // ===================== Parametrización línea + producto =====================

    public async Task<IReadOnlyList<ParametroLineaProductoDto>> ObtenerParametrosAsync(int? lineaId, CancellationToken cancellationToken = default)
    {
        var todos = await _unitOfWork.ParametrosLineaProducto.GetListadoAsync(lineaId, cancellationToken);
        var hoy = HoraPlanta.Hoy(_zonaPlanta);
        var vigentes = ParametroLineaProductoVigencia.Vigentes(todos, hoy).Select(p => p.ParametroLineaProductoId).ToHashSet();
        return todos
            .OrderBy(p => p.Linea.Orden).ThenBy(p => p.Linea.Nombre)
            .ThenBy(p => p.ProductoMaestro.Orden).ThenBy(p => p.ProductoMaestro.Nombre)
            .ThenByDescending(p => p.VigenteDesde)
            .Select(p => new ParametroLineaProductoDto
            {
                ParametroLineaProductoId = p.ParametroLineaProductoId,
                LineaId = p.LineaId,
                LineaNombre = p.Linea.Nombre,
                ProductoMaestroId = p.ProductoMaestroId,
                ProductoCodigo = p.ProductoMaestro.Codigo,
                ProductoNombre = p.ProductoMaestro.Nombre,
                ProductoActivo = p.ProductoMaestro.Activo,
                VigenteDesde = p.VigenteDesde,
                Habilitado = p.Habilitado,
                MangasObjetivoTurno = p.MangasObjetivoTurno,
                BolsonesObjetivoTurno = p.BolsonesObjetivoTurno,
                MinutosTurnoReferencia = p.MinutosTurnoReferencia,
                EstadoVigencia = vigentes.Contains(p.ParametroLineaProductoId) ? EstadoVigenciaParametro.Vigente
                    : p.VigenteDesde > hoy ? EstadoVigenciaParametro.Futura
                    : EstadoVigenciaParametro.Reemplazada
            }).ToList();
    }

    public async Task<Result<ParametroLineaProductoDto>> CrearParametroAsync(GuardarParametroLineaProductoRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!await _permisos.PermiteAsync(Opciones.MaestrosParametros, AccionPermiso.Alta, () => CalidadReglas.PuedeAdministrarMaestros(roles), cancellationToken))
            return Result<ParametroLineaProductoDto>.Failure("FORBIDDEN", MensajeSinPermiso);
        var motivo = await MotivoParametroInvalidoAsync(request, null, cancellationToken);
        if (motivo != null)
            return Result<ParametroLineaProductoDto>.Failure("VALIDATION", motivo);

        var parametro = new ParametroLineaProducto();
        Aplicar(parametro, request);
        await _unitOfWork.ParametrosLineaProducto.AddAsync(parametro, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ParametroLineaProductoDto>.Success(await ParametroDtoAsync(parametro, cancellationToken));
    }

    public async Task<Result<ParametroLineaProductoDto>> ActualizarParametroAsync(int parametroLineaProductoId, GuardarParametroLineaProductoRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (!await _permisos.PermiteAsync(Opciones.MaestrosParametros, AccionPermiso.Modificar, () => CalidadReglas.PuedeAdministrarMaestros(roles), cancellationToken))
            return Result<ParametroLineaProductoDto>.Failure("FORBIDDEN", MensajeSinPermiso);
        var parametro = await _unitOfWork.ParametrosLineaProducto.GetByIdAsync(parametroLineaProductoId, cancellationToken);
        if (parametro == null)
            return Result<ParametroLineaProductoDto>.Failure("NOT_FOUND", "Parámetro no encontrado.");
        var motivo = await MotivoParametroInvalidoAsync(request, parametroLineaProductoId, cancellationToken);
        if (motivo != null)
            return Result<ParametroLineaProductoDto>.Failure("VALIDATION", motivo);

        Aplicar(parametro, request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ParametroLineaProductoDto>.Success(await ParametroDtoAsync(parametro, cancellationToken));
    }

    private async Task<string?> MotivoParametroInvalidoAsync(GuardarParametroLineaProductoRequest r, int? id, CancellationToken ct)
    {
        var excluir = id ?? 0;   // fuera de la expresión: EF no traduce (int? ?? 0) en la comparación
        if (await _unitOfWork.Lineas.GetByIdAsync(r.LineaId, ct) == null) return "Elegí la línea.";
        if (await _unitOfWork.ProductosMaestro.GetByIdAsync(r.ProductoMaestroId, ct) == null) return "Elegí el producto.";
        if (r.VigenteDesde == default) return "Indicá desde qué fecha rige.";
        if (r.MangasObjetivoTurno is < 0) return "El objetivo de mangas no puede ser negativo (vacío = sin objetivo).";
        if (r.BolsonesObjetivoTurno is < 0) return "El objetivo de bolsones no puede ser negativo (vacío = no se mide).";
        if (r.MinutosTurnoReferencia <= 0) return "Los minutos del turno de referencia tienen que ser mayores que 0 (normal: 480).";
        if (await _unitOfWork.ParametrosLineaProducto.ExistsAsync(p => p.LineaId == r.LineaId && p.ProductoMaestroId == r.ProductoMaestroId
                && p.VigenteDesde == r.VigenteDesde && p.ParametroLineaProductoId != excluir, ct))
            return $"Ya hay una vigencia de ese producto en esa línea desde el {r.VigenteDesde:dd/MM/yyyy}. Editala o elegí otra fecha.";
        return null;
    }

    private static void Aplicar(ParametroLineaProducto p, GuardarParametroLineaProductoRequest r)
    {
        p.LineaId = r.LineaId;
        p.ProductoMaestroId = r.ProductoMaestroId;
        p.VigenteDesde = r.VigenteDesde;
        p.Habilitado = r.Habilitado;
        p.MangasObjetivoTurno = r.MangasObjetivoTurno;
        p.BolsonesObjetivoTurno = r.BolsonesObjetivoTurno;
        p.MinutosTurnoReferencia = r.MinutosTurnoReferencia;
    }

    private async Task<ParametroLineaProductoDto> ParametroDtoAsync(ParametroLineaProducto p, CancellationToken ct) =>
        (await ObtenerParametrosAsync(p.LineaId, ct)).Single(x => x.ParametroLineaProductoId == p.ParametroLineaProductoId);
}

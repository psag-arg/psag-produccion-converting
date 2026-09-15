using FluentValidation;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;

namespace PSAG.Mantenimiento.Application.Validators;

public class CrearDetencionValidator : AbstractValidator<CrearDetencionRequest>
{
    public CrearDetencionValidator()
    {
        RuleFor(x => x.LineaId)
            .GreaterThan(0)
            .WithMessage("Debe seleccionar una línea válida.");

        RuleFor(x => x.Fecha)
            .NotEmpty()
            .WithMessage("La fecha es obligatoria.")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("La fecha no puede ser futura.");

        RuleFor(x => x.Descripcion)
            .NotEmpty()
            .WithMessage("La descripción es obligatoria.")
            .MaximumLength(2000)
            .WithMessage("La descripción no puede exceder 2000 caracteres.");

        RuleFor(x => x.OrigenTiempo)
            .NotEmpty()
            .WithMessage("El origen del tiempo es obligatorio.")
            .Must(o => o == "DECLARADO" || o == "CALCULADO")
            .WithMessage("El origen del tiempo debe ser DECLARADO o CALCULADO.");

        RuleFor(x => x.MinutosDeclarados)
            .GreaterThan(0)
            .When(x => x.OrigenTiempo == "DECLARADO")
            .WithMessage("Los minutos declarados son obligatorios cuando el origen es DECLARADO.");

        RuleFor(x => x.FechaHoraParada)
            .NotNull()
            .When(x => x.OrigenTiempo == "CALCULADO")
            .WithMessage("La fecha/hora de parada es obligatoria cuando el origen es CALCULADO.");

        RuleFor(x => x.FechaHoraArranque)
            .NotNull()
            .When(x => x.OrigenTiempo == "CALCULADO")
            .WithMessage("La fecha/hora de arranque es obligatoria cuando el origen es CALCULADO.")
            .GreaterThan(x => x.FechaHoraParada)
            .When(x => x.OrigenTiempo == "CALCULADO" && x.FechaHoraParada.HasValue)
            .WithMessage("La fecha/hora de arranque debe ser posterior a la parada.");
    }
}

public class ActualizarDetencionValidator : AbstractValidator<ActualizarDetencionRequest>
{
    public ActualizarDetencionValidator()
    {
        RuleFor(x => x.Descripcion)
            .NotEmpty()
            .WithMessage("La descripción es obligatoria.")
            .MaximumLength(2000)
            .WithMessage("La descripción no puede exceder 2000 caracteres.");

        RuleFor(x => x.AccionesRealizadas)
            .MaximumLength(4000)
            .WithMessage("Las acciones realizadas no pueden exceder 4000 caracteres.");

        RuleFor(x => x.ResolucionDescripcion)
            .MaximumLength(4000)
            .WithMessage("La descripción de resolución no puede exceder 4000 caracteres.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("Se requiere información de versión para actualizar.");
    }
}

public class CambiarEstadoDetencionValidator : AbstractValidator<CambiarEstadoDetencionRequest>
{
    private readonly string[] _estadosValidos = { "REGISTRADA", "EN_SEGUIMIENTO", "CERRADA" };

    public CambiarEstadoDetencionValidator()
    {
        RuleFor(x => x.NuevoEstado)
            .NotEmpty()
            .WithMessage("Debe especificar el nuevo estado.")
            .Must(e => _estadosValidos.Contains(e.ToUpperInvariant()))
            .WithMessage("Estado no válido. Los estados permitidos son: REGISTRADA, EN_SEGUIMIENTO, CERRADA.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("Se requiere información de versión para actualizar.");
    }
}

public class CerrarDetencionValidator : AbstractValidator<CerrarDetencionRequest>
{
    public CerrarDetencionValidator()
    {
        RuleFor(x => x.ResolucionDescripcion)
            .NotEmpty()
            .WithMessage("La descripción de resolución es obligatoria para cerrar la detención.")
            .MaximumLength(4000)
            .WithMessage("La descripción de resolución no puede exceder 4000 caracteres.");

        RuleFor(x => x.AccionesRealizadas)
            .MaximumLength(4000)
            .WithMessage("Las acciones realizadas no pueden exceder 4000 caracteres.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("Se requiere información de versión para actualizar.");
    }
}

public class AgregarNotaDetencionValidator : AbstractValidator<AgregarNotaDetencionRequest>
{
    private readonly string[] _tiposNotaValidos = { "Operativa", "Tecnica" };

    public AgregarNotaDetencionValidator()
    {
        RuleFor(x => x.Texto)
            .NotEmpty()
            .WithMessage("El texto de la nota es obligatorio.")
            .MaximumLength(4000)
            .WithMessage("El texto de la nota no puede exceder 4000 caracteres.");

        RuleFor(x => x.TipoNota)
            .Must(t => _tiposNotaValidos.Contains(t))
            .WithMessage("Tipo de nota no válido. Los tipos permitidos son: Operativa, Tecnica.");
    }
}

public class IniciarSeguimientoValidator : AbstractValidator<IniciarSeguimientoRequest>
{
    public IniciarSeguimientoValidator()
    {
        RuleFor(x => x.Observacion)
            .MaximumLength(2000)
            .WithMessage("La observación no puede exceder 2000 caracteres.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("Se requiere información de versión para actualizar.");
    }
}

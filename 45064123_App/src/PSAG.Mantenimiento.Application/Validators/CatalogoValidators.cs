using FluentValidation;
using PSAG.Mantenimiento.Application.DTOs.Catalogos;

namespace PSAG.Mantenimiento.Application.Validators;

public class CrearLineaValidator : AbstractValidator<CrearLineaRequest>
{
    public CrearLineaValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(100)
            .WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class ActualizarLineaValidator : AbstractValidator<ActualizarLineaRequest>
{
    public ActualizarLineaValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(100)
            .WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class CrearEquipoValidator : AbstractValidator<CrearEquipoRequest>
{
    public CrearEquipoValidator()
    {
        RuleFor(x => x.LineaId)
            .GreaterThan(0)
            .WithMessage("Debe seleccionar una línea válida.");

        RuleFor(x => x.Codigo)
            .NotEmpty()
            .WithMessage("El código es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El código no puede exceder 50 caracteres.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(100)
            .WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class ActualizarEquipoValidator : AbstractValidator<ActualizarEquipoRequest>
{
    public ActualizarEquipoValidator()
    {
        RuleFor(x => x.LineaId)
            .GreaterThan(0)
            .WithMessage("Debe seleccionar una línea válida.");

        RuleFor(x => x.Codigo)
            .NotEmpty()
            .WithMessage("El código es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El código no puede exceder 50 caracteres.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(100)
            .WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class CrearTipoFallaValidator : AbstractValidator<CrearTipoFallaRequest>
{
    public CrearTipoFallaValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .WithMessage("El código es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El código no puede exceder 50 caracteres.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(100)
            .WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class ActualizarTipoFallaValidator : AbstractValidator<ActualizarTipoFallaRequest>
{
    public ActualizarTipoFallaValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .WithMessage("El código es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El código no puede exceder 50 caracteres.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(100)
            .WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class CrearDetalleFallaValidator : AbstractValidator<CrearDetalleFallaRequest>
{
    public CrearDetalleFallaValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .WithMessage("El código es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El código no puede exceder 50 caracteres.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(100)
            .WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class ActualizarDetalleFallaValidator : AbstractValidator<ActualizarDetalleFallaRequest>
{
    public ActualizarDetalleFallaValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .WithMessage("El código es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El código no puede exceder 50 caracteres.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(100)
            .WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class CrearTurnoValidator : AbstractValidator<CrearTurnoRequest>
{
    public CrearTurnoValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .WithMessage("El código es obligatorio.")
            .MaximumLength(20)
            .WithMessage("El código no puede exceder 20 caracteres.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El nombre no puede exceder 50 caracteres.");
    }
}

public class ActualizarTurnoValidator : AbstractValidator<ActualizarTurnoRequest>
{
    public ActualizarTurnoValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .WithMessage("El código es obligatorio.")
            .MaximumLength(20)
            .WithMessage("El código no puede exceder 20 caracteres.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre es obligatorio.")
            .MaximumLength(50)
            .WithMessage("El nombre no puede exceder 50 caracteres.");
    }
}

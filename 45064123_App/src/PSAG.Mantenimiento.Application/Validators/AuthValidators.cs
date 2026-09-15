using FluentValidation;
using PSAG.Mantenimiento.Application.DTOs.Auth;

namespace PSAG.Mantenimiento.Application.Validators;

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Usuario)
            .NotEmpty()
            .WithMessage("El usuario es obligatorio.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("La contraseña es obligatoria.");
    }
}

public class CambiarPasswordValidator : AbstractValidator<CambiarPasswordRequest>
{
    public CambiarPasswordValidator()
    {
        RuleFor(x => x.PasswordActual)
            .NotEmpty()
            .WithMessage("La contraseña actual es obligatoria.");

        RuleFor(x => x.PasswordNuevo)
            .NotEmpty()
            .WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .Matches("[A-Z]")
            .WithMessage("La contraseña debe contener al menos una letra mayúscula.")
            .Matches("[a-z]")
            .WithMessage("La contraseña debe contener al menos una letra minúscula.")
            .Matches("[0-9]")
            .WithMessage("La contraseña debe contener al menos un número.");

        RuleFor(x => x.ConfirmarPasswordNuevo)
            .Equal(x => x.PasswordNuevo)
            .WithMessage("La confirmación de contraseña no coincide.");
    }
}

public class CrearUsuarioValidator : AbstractValidator<CrearUsuarioRequest>
{
    public CrearUsuarioValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("El nombre de usuario es obligatorio.")
            .MinimumLength(3)
            .WithMessage("El nombre de usuario debe tener al menos 3 caracteres.")
            .MaximumLength(50)
            .WithMessage("El nombre de usuario no puede exceder 50 caracteres.")
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("El nombre de usuario solo puede contener letras, números, puntos, guiones y guiones bajos.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress()
            .WithMessage("El correo electrónico no es válido.");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty()
            .WithMessage("El nombre completo es obligatorio.")
            .MaximumLength(200)
            .WithMessage("El nombre completo no puede exceder 200 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres.");

        RuleFor(x => x.Roles)
            .NotEmpty()
            .WithMessage("Debe asignar al menos un rol.");
    }
}

public class ActualizarUsuarioValidator : AbstractValidator<ActualizarUsuarioRequest>
{
    public ActualizarUsuarioValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress()
            .WithMessage("El correo electrónico no es válido.");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty()
            .WithMessage("El nombre completo es obligatorio.")
            .MaximumLength(200)
            .WithMessage("El nombre completo no puede exceder 200 caracteres.");

        RuleFor(x => x.Roles)
            .NotEmpty()
            .WithMessage("Debe asignar al menos un rol.");
    }
}

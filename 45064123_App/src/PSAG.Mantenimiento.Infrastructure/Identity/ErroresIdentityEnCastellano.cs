using Microsoft.AspNetCore.Identity;

namespace PSAG.Mantenimiento.Infrastructure.Identity;

/// <summary>
/// Mensajes de Identity en castellano para mostrarlos tal cual en pantalla.
/// </summary>
public class ErroresIdentityEnCastellano : IdentityErrorDescriber
{
    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };

    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Ocurrió un error inesperado.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Alguien cambió este usuario mientras lo editabas. Recargá la página y volvé a intentar.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "La contraseña es incorrecta.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "El enlace o código no es válido o ya venció.");
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated), "Ya hay un usuario asociado a este inicio de sesión.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), $"El nombre de usuario '{userName}' no es válido: usá sólo letras sin tilde, números y - . _ @ +.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), $"El email '{email}' no es válido.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), $"Ya existe un usuario '{userName}'.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), $"Ya existe un usuario con el email '{email}'.");
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName), $"El rol '{role}' no es válido.");
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), $"El rol '{role}' ya existe.");
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), "El usuario ya tiene contraseña.");
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), "El bloqueo no está habilitado para este usuario.");
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), $"El usuario ya tiene el rol '{role}'.");
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), $"El usuario no tiene el rol '{role}'.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"La contraseña debe tener al menos {uniqueChars} caracteres distintos.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe tener al menos un símbolo (por ejemplo ! o #).");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "La contraseña debe tener al menos un número.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "La contraseña debe tener al menos una minúscula.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "La contraseña debe tener al menos una mayúscula.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed), "El código de recuperación no es válido.");
}

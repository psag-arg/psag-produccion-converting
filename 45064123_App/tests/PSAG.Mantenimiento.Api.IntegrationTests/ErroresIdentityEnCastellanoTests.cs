using Microsoft.AspNetCore.Identity;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

public class ErroresIdentityEnCastellanoTests
{
    private readonly IdentityErrorDescriber _errores = new ErroresIdentityEnCastellano();

    [Fact]
    public void ErroresDeContrasenia_EstanEnCastellano()
    {
        Assert.Equal("La contraseña debe tener al menos 8 caracteres.", _errores.PasswordTooShort(8).Description);
        Assert.Equal("La contraseña debe tener al menos un símbolo (por ejemplo ! o #).", _errores.PasswordRequiresNonAlphanumeric().Description);
    }

    [Fact]
    public void TodosLosMensajesSobrescritos_NoQuedanEnIngles()
    {
        var descripciones = new[]
        {
            _errores.DefaultError(), _errores.PasswordRequiresDigit(), _errores.PasswordRequiresUpper(),
            _errores.PasswordRequiresLower(), _errores.PasswordRequiresUniqueChars(4), _errores.DuplicateUserName("x"),
            _errores.DuplicateEmail("x@x"), _errores.InvalidEmail("x"), _errores.ConcurrencyFailure()
        }.Select(e => e.Description);

        Assert.All(descripciones, d => Assert.DoesNotContain("must", d));
    }
}

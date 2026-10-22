using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Api.Controllers;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Nombres de usuario de los DTO: una consulta por lote, nombre completo o, si falta, el usuario.
/// </summary>
public class NombresUsuarioTests : IDisposable
{
    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NombresUsuarioTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();
        _userManager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(_context), null!, null!, null!, null!, null!, null!, null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    public void Dispose()
    {
        _userManager.Dispose();
        _context.Dispose();
        _conexion.Dispose();
    }

    [Fact]
    public async Task NombresVisibles_NombreCompletoOUsuario_IgnoraVaciosEInexistentes()
    {
        _context.Users.AddRange(
            new ApplicationUser { Id = "u1", UserName = "jperez", NombreCompleto = "Juan Pérez" },
            new ApplicationUser { Id = "u2", UserName = "mgomez", NombreCompleto = " " });
        await _context.SaveChangesAsync();

        var nombres = await _userManager.NombresVisiblesAsync(["u1", "u2", "u1", null, "", "no-existe"]);

        Assert.Equal(2, nombres.Count);
        Assert.Equal("Juan Pérez", nombres.NombreDe("u1"));
        Assert.Equal("mgomez", nombres.NombreDe("u2"));
        Assert.Null(nombres.NombreDe("no-existe"));
        Assert.Null(nombres.NombreDe(null));
    }
}

using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Exceptions;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class TareaMantenimientoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 22, 10, 0, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(EstadoTareaMantenimiento.PENDIENTE, EstadoTareaMantenimiento.PROGRAMADA, true)]
    [InlineData(EstadoTareaMantenimiento.PROGRAMADA, EstadoTareaMantenimiento.CERRADA, true)]
    [InlineData(EstadoTareaMantenimiento.PENDIENTE, EstadoTareaMantenimiento.CERRADA, false)]
    [InlineData(EstadoTareaMantenimiento.PROGRAMADA, EstadoTareaMantenimiento.PENDIENTE, false)]
    [InlineData(EstadoTareaMantenimiento.CERRADA, EstadoTareaMantenimiento.PENDIENTE, false)]
    [InlineData(EstadoTareaMantenimiento.CERRADA, EstadoTareaMantenimiento.PROGRAMADA, false)]
    [InlineData(EstadoTareaMantenimiento.PROGRAMADA, EstadoTareaMantenimiento.PROGRAMADA, false)]
    public void Transiciones_SoloPendienteProgramadaCerrada(EstadoTareaMantenimiento desde, EstadoTareaMantenimiento hacia, bool esperado)
    {
        Assert.Equal(esperado, desde.PuedeTransicionarA(hacia));
    }

    [Fact]
    public void NuevaTarea_EmpiezaPendiente()
    {
        Assert.Equal(EstadoTareaMantenimiento.PENDIENTE, new TareaMantenimiento().Estado);
    }

    [Fact]
    public void Programar_GuardaUsuarioYFecha()
    {
        var tarea = new TareaMantenimiento();

        tarea.Programar("mant-1", Ahora);

        Assert.Equal(EstadoTareaMantenimiento.PROGRAMADA, tarea.Estado);
        Assert.Equal("mant-1", tarea.ProgramadaPorUsuarioId);
        Assert.Equal(Ahora, tarea.FechaHoraProgramacion);
        Assert.Null(tarea.FechaHoraCierre);
    }

    [Fact]
    public void Cerrar_GuardaUsuarioYFechaDeCierre()
    {
        var tarea = new TareaMantenimiento();
        tarea.Programar("mant-1", Ahora);

        tarea.Cerrar("sup-1", Ahora.AddHours(2));

        Assert.True(tarea.EstaCerrada);
        Assert.Equal("sup-1", tarea.CerradaPorUsuarioId);
        Assert.Equal(Ahora.AddHours(2), tarea.FechaHoraCierre);
        Assert.Equal("mant-1", tarea.ProgramadaPorUsuarioId);
    }

    [Fact]
    public void Cerrar_SinProgramar_NoSePermite()
    {
        var tarea = new TareaMantenimiento();

        Assert.Throws<TransicionEstadoInvalidaException>(() => tarea.Cerrar("mant-1", Ahora));
        Assert.Equal(EstadoTareaMantenimiento.PENDIENTE, tarea.Estado);
        Assert.Null(tarea.CerradaPorUsuarioId);
    }

    [Fact]
    public void Programar_DosVeces_NoSePermite()
    {
        var tarea = new TareaMantenimiento();
        tarea.Programar("mant-1", Ahora);

        Assert.Throws<TransicionEstadoInvalidaException>(() => tarea.Programar("mant-2", Ahora.AddHours(1)));
        Assert.Equal("mant-1", tarea.ProgramadaPorUsuarioId);
    }

    [Fact]
    public void Cerrada_NoVuelveAProgramarseNiCerrarse()
    {
        var tarea = new TareaMantenimiento();
        tarea.Programar("mant-1", Ahora);
        tarea.Cerrar("mant-1", Ahora);

        Assert.Throws<TransicionEstadoInvalidaException>(() => tarea.Programar("mant-1", Ahora));
        Assert.Throws<TransicionEstadoInvalidaException>(() => tarea.Cerrar("mant-2", Ahora));
        Assert.Equal("mant-1", tarea.CerradaPorUsuarioId);
    }

    [Theory]
    [InlineData(new[] { "OPERARIO" }, true)]
    [InlineData(new[] { "SUPERVISOR" }, true)]
    [InlineData(new[] { "ADMINISTRADOR" }, true)]
    [InlineData(new[] { "MANTENIMIENTO" }, false)]
    [InlineData(new[] { "GERENCIA" }, false)]
    [InlineData(new string[0], false)]
    public void PuedeCargar_OperarioSupervisionYAdmin(string[] roles, bool esperado)
    {
        Assert.Equal(esperado, TareaMantenimientoReglas.PuedeCargar(roles));
    }

    [Theory]
    [InlineData(new[] { "MANTENIMIENTO" }, true)]
    [InlineData(new[] { "SUPERVISOR" }, true)]
    [InlineData(new[] { "ADMINISTRADOR" }, true)]
    [InlineData(new[] { "OPERARIO" }, false)]
    [InlineData(new[] { "GERENCIA" }, false)]
    [InlineData(new[] { "OPERARIO", "MANTENIMIENTO" }, true)]
    public void PuedeProgramarYCerrar_MantenimientoSupervisionYAdmin(string[] roles, bool esperado)
    {
        Assert.Equal(esperado, TareaMantenimientoReglas.PuedeProgramarYCerrar(roles));
    }

    [Theory]
    [InlineData(new[] { "OPERARIO" }, false, false)]
    [InlineData(new[] { "OPERARIO" }, true, true)]
    [InlineData(new[] { "SUPERVISOR" }, false, true)]
    [InlineData(new[] { "ADMINISTRADOR" }, false, true)]
    public void PuedeCargarEnLinea_OperarioSoloEnSusLineas(string[] roles, bool lineaAsignada, bool esperado)
    {
        Assert.Equal(esperado, TareaMantenimientoReglas.PuedeCargarEnLinea(roles, lineaAsignada));
    }

    [Fact]
    public void Criticidad_OrdenNumericoDeMasAMenosCritica()
    {
        Assert.True(CriticidadTarea.ALTA > CriticidadTarea.MEDIA);
        Assert.True(CriticidadTarea.MEDIA > CriticidadTarea.BAJA);
    }
}

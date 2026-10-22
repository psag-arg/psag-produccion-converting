namespace PSAG.Mantenimiento.Domain.Interfaces;

/// <summary>
/// Unidad de trabajo para coordinar transacciones.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // Eje 1: Detenciones
    IDetencionRepository Detenciones { get; }
    ILineaRepository Lineas { get; }
    IEquipoRepository Equipos { get; }
    ITipoFallaRepository TiposFalla { get; }
    IDetalleFallaRepository DetallesFalla { get; }
    ITurnoRepository Turnos { get; }
    IUsuarioLineaRepository UsuariosLineas { get; }
    IAuditoriaRepository Auditoria { get; }
    IReglaAnalisisRepository ReglasAnalisis { get; }
    ITiempoProgramadoDiaRepository TiemposProgramadosDia { get; }
    ICalendarioLineaRepository CalendariosLinea { get; }

    // Eje 2: Variables de proceso
    IProductoRepository Productos { get; }
    IGrupoParametroRepository GruposParametro { get; }
    IParametroRepository Parametros { get; }
    IEstandarParametroRepository EstandaresParametro { get; }
    IRegistroProcesoRepository RegistrosProceso { get; }

    // Eje 3: Inspecciones de máquina
    IPlanInspeccionRepository PlanesInspeccion { get; }
    IEjecucionInspeccionRepository EjecucionesInspeccion { get; }
    IHallazgoNoOkRepository HallazgosNoOk { get; }

    // Tareas de mantenimiento
    ITareaMantenimientoRepository TareasMantenimiento { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

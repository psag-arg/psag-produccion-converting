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
    IOperadorLineaRepository OperadoresLinea { get; }
    IAuditoriaRepository Auditoria { get; }
    IReglaAnalisisRepository ReglasAnalisis { get; }
    ITiempoProgramadoDiaRepository TiemposProgramadosDia { get; }
    ICalendarioLineaRepository CalendariosLinea { get; }

    // Producción: turno de producción (T-798 etapa 2)
    ITurnoProduccionRepository TurnosProduccion { get; }

    // Eje 2: Variables de proceso
    IProductoMaestroRepository ProductosMaestro { get; }
    IProductoSkuRepository ProductosSku { get; }
    IParametroLineaProductoRepository ParametrosLineaProducto { get; }
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

    // Control de calidad (T-611)
    IPuntoControlCalidadRepository PuntosControlCalidad { get; }
    ITipoDefectoCalidadRepository TiposDefectoCalidad { get; }
    IRegistroCalidadRepository RegistrosCalidad { get; }

    // Conteo de producción y paradas por cámara (T-610; maestro de cámaras T-798 etapa 4)
    ICamaraRepository Camaras { get; }
    IConteoCamaraMinutoRepository ConteosCamaraMinuto { get; }
    IParadaCamaraRepository ParadasCamara { get; }
    IAlertaCamaraRepository AlertasCamara { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

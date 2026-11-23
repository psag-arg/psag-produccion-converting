using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly MantenimientoDbContext _context;
    private IDbContextTransaction? _transaction;

    // Eje 1: Detenciones
    private IDetencionRepository? _detenciones;
    private ILineaRepository? _lineas;
    private IEquipoRepository? _equipos;
    private ITipoFallaRepository? _tiposFalla;
    private IDetalleFallaRepository? _detallesFalla;
    private ITurnoRepository? _turnos;
    private IOperadorLineaRepository? _operadoresLinea;
    private IAuditoriaRepository? _auditoria;
    private IReglaAnalisisRepository? _reglasAnalisis;
    private ITiempoProgramadoDiaRepository? _tiemposProgramadosDia;
    private ICalendarioLineaRepository? _calendariosLinea;
    private ITurnoProduccionRepository? _turnosProduccion;

    // Eje 2: Variables de proceso
    private IProductoMaestroRepository? _productosMaestro;
    private IProductoSkuRepository? _productosSku;
    private IParametroLineaProductoRepository? _parametrosLineaProducto;
    private IGrupoParametroRepository? _gruposParametro;
    private IParametroRepository? _parametros;
    private IEstandarParametroRepository? _estandaresParametro;
    private IRegistroProcesoRepository? _registrosProceso;

    // Eje 3: Inspecciones de máquina
    private IPlanInspeccionRepository? _planesInspeccion;
    private IEjecucionInspeccionRepository? _ejecucionesInspeccion;
    private IHallazgoNoOkRepository? _hallazgosNoOk;

    // Tareas de mantenimiento
    private ITareaMantenimientoRepository? _tareasMantenimiento;

    // Control de calidad
    private IPuntoControlCalidadRepository? _puntosControlCalidad;
    private ICamaraRepository? _camaras;
    private ITipoDefectoCalidadRepository? _tiposDefectoCalidad;
    private IRegistroCalidadRepository? _registrosCalidad;

    // Conteo por cámara
    private IConteoCamaraMinutoRepository? _conteosCamaraMinuto;
    private IParadaCamaraRepository? _paradasCamara;
    private IAlertaCamaraRepository? _alertasCamara;

    private readonly INotificadorTablero? _notificador;

    public UnitOfWork(MantenimientoDbContext context, INotificadorTablero? notificador = null)
    {
        _context = context;
        _notificador = notificador;
    }

    // Eje 1: Detenciones
    public IDetencionRepository Detenciones => _detenciones ??= new DetencionRepository(_context);
    public ILineaRepository Lineas => _lineas ??= new LineaRepository(_context);
    public IEquipoRepository Equipos => _equipos ??= new EquipoRepository(_context);
    public ITipoFallaRepository TiposFalla => _tiposFalla ??= new TipoFallaRepository(_context);
    public IDetalleFallaRepository DetallesFalla => _detallesFalla ??= new DetalleFallaRepository(_context);
    public ITurnoRepository Turnos => _turnos ??= new TurnoRepository(_context);
    public IOperadorLineaRepository OperadoresLinea => _operadoresLinea ??= new OperadorLineaRepository(_context);
    public IAuditoriaRepository Auditoria => _auditoria ??= new AuditoriaRepository(_context);
    public IReglaAnalisisRepository ReglasAnalisis => _reglasAnalisis ??= new ReglaAnalisisRepository(_context);
    public ITiempoProgramadoDiaRepository TiemposProgramadosDia => _tiemposProgramadosDia ??= new TiempoProgramadoDiaRepository(_context);
    public ICalendarioLineaRepository CalendariosLinea => _calendariosLinea ??= new CalendarioLineaRepository(_context);
    public ITurnoProduccionRepository TurnosProduccion => _turnosProduccion ??= new TurnoProduccionRepository(_context);

    // Eje 2: Variables de proceso
    public IProductoMaestroRepository ProductosMaestro => _productosMaestro ??= new ProductoMaestroRepository(_context);
    public IProductoSkuRepository ProductosSku => _productosSku ??= new ProductoSkuRepository(_context);
    public IParametroLineaProductoRepository ParametrosLineaProducto => _parametrosLineaProducto ??= new ParametroLineaProductoRepository(_context);
    public IGrupoParametroRepository GruposParametro => _gruposParametro ??= new GrupoParametroRepository(_context);
    public IParametroRepository Parametros => _parametros ??= new ParametroRepository(_context);
    public IEstandarParametroRepository EstandaresParametro => _estandaresParametro ??= new EstandarParametroRepository(_context);
    public IRegistroProcesoRepository RegistrosProceso => _registrosProceso ??= new RegistroProcesoRepository(_context);

    // Eje 3: Inspecciones de máquina
    public IPlanInspeccionRepository PlanesInspeccion => _planesInspeccion ??= new PlanInspeccionRepository(_context);
    public IEjecucionInspeccionRepository EjecucionesInspeccion => _ejecucionesInspeccion ??= new EjecucionInspeccionRepository(_context);
    public IHallazgoNoOkRepository HallazgosNoOk => _hallazgosNoOk ??= new HallazgoNoOkRepository(_context);

    // Tareas de mantenimiento
    public ITareaMantenimientoRepository TareasMantenimiento => _tareasMantenimiento ??= new TareaMantenimientoRepository(_context);

    // Control de calidad
    public IPuntoControlCalidadRepository PuntosControlCalidad => _puntosControlCalidad ??= new PuntoControlCalidadRepository(_context);
    public ICamaraRepository Camaras => _camaras ??= new CamaraRepository(_context);
    public ITipoDefectoCalidadRepository TiposDefectoCalidad => _tiposDefectoCalidad ??= new TipoDefectoCalidadRepository(_context);
    public IRegistroCalidadRepository RegistrosCalidad => _registrosCalidad ??= new RegistroCalidadRepository(_context);

    // Conteo por cámara
    public IConteoCamaraMinutoRepository ConteosCamaraMinuto => _conteosCamaraMinuto ??= new ConteoCamaraMinutoRepository(_context);
    public IParadaCamaraRepository ParadasCamara => _paradasCamara ??= new ParadaCamaraRepository(_context);
    public IAlertaCamaraRepository AlertasCamara => _alertasCamara ??= new AlertaCamaraRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_notificador == null)
            return await _context.SaveChangesAsync(cancellationToken);

        // T-770: qué turnos de producción tocó este guardado (los Id de lo agregado se leen después de guardar).
        var cambiados = EntidadesDelTablero();
        var filas = await _context.SaveChangesAsync(cancellationToken);
        var cambios = cambiados
            .Select(c => (Turno: c.Turno(), c.Motivo))
            .Where(c => c.Turno is > 0)
            .Select(c => new CambioTablero(c.Turno!.Value, c.Motivo))
            .Distinct()
            .ToList();
        if (cambios.Count > 0)
        {
            try
            {
                await _notificador.NotificarAsync(cambios, cancellationToken);
            }
            catch (Exception)
            {
                // El aviso en línea nunca hace fallar el guardado (el tablero tiene un refresco de respaldo).
            }
        }
        return filas;
    }

    /// <summary>
    /// T-770 · Entidades agregadas o modificadas que cambian el tablero del turno: minutos y paradas de cámara,
    /// detenciones, el turno de producción, sus tramos de producto y de operador.
    /// </summary>
    private List<(Func<long?> Turno, string Motivo)> EntidadesDelTablero()
    {
        var lista = new List<(Func<long?> Turno, string Motivo)>();
        foreach (var e in _context.ChangeTracker.Entries())
        {
            if (e.State is not (EntityState.Added or EntityState.Modified))
                continue;
            switch (e.Entity)
            {
                case ConteoCamaraMinuto m: lista.Add((() => m.TurnoProduccionId, MotivoCambioTablero.Conteo)); break;
                case ParadaCamara p: lista.Add((() => p.TurnoProduccionId, MotivoCambioTablero.Paradas)); break;
                case Detencion d: lista.Add((() => d.TurnoProduccionId, MotivoCambioTablero.Detenciones)); break;
                case TurnoProduccion t: lista.Add((() => t.TurnoProduccionId, MotivoCambioTablero.Turno)); break;
                case TurnoProduccionProducto t: lista.Add((() => t.TurnoProduccionId, MotivoCambioTablero.Turno)); break;
                case TurnoProduccionOperador t: lista.Add((() => t.TurnoProduccionId, MotivoCambioTablero.Turno)); break;
            }
        }
        return lista;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}

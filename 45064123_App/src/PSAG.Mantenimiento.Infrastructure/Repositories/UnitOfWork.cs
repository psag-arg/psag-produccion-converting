using Microsoft.EntityFrameworkCore.Storage;
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
    private IUsuarioLineaRepository? _usuariosLineas;
    private IAuditoriaRepository? _auditoria;
    private IReglaAnalisisRepository? _reglasAnalisis;
    private ITiempoProgramadoDiaRepository? _tiemposProgramadosDia;
    private ICalendarioLineaRepository? _calendariosLinea;

    // Eje 2: Variables de proceso
    private IProductoRepository? _productos;
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

    public UnitOfWork(MantenimientoDbContext context)
    {
        _context = context;
    }

    // Eje 1: Detenciones
    public IDetencionRepository Detenciones => _detenciones ??= new DetencionRepository(_context);
    public ILineaRepository Lineas => _lineas ??= new LineaRepository(_context);
    public IEquipoRepository Equipos => _equipos ??= new EquipoRepository(_context);
    public ITipoFallaRepository TiposFalla => _tiposFalla ??= new TipoFallaRepository(_context);
    public IDetalleFallaRepository DetallesFalla => _detallesFalla ??= new DetalleFallaRepository(_context);
    public ITurnoRepository Turnos => _turnos ??= new TurnoRepository(_context);
    public IUsuarioLineaRepository UsuariosLineas => _usuariosLineas ??= new UsuarioLineaRepository(_context);
    public IAuditoriaRepository Auditoria => _auditoria ??= new AuditoriaRepository(_context);
    public IReglaAnalisisRepository ReglasAnalisis => _reglasAnalisis ??= new ReglaAnalisisRepository(_context);
    public ITiempoProgramadoDiaRepository TiemposProgramadosDia => _tiemposProgramadosDia ??= new TiempoProgramadoDiaRepository(_context);
    public ICalendarioLineaRepository CalendariosLinea => _calendariosLinea ??= new CalendarioLineaRepository(_context);

    // Eje 2: Variables de proceso
    public IProductoRepository Productos => _productos ??= new ProductoRepository(_context);
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

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
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

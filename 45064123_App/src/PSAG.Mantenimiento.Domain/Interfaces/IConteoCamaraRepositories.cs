using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

/// <summary>Maestro de cámaras (CAM_Camaras, T-798 etapa 4).</summary>
public interface ICamaraRepository : IRepository<Camara>
{
    /// <summary>Con línea y equipo, ordenadas por línea, canal y código. Sólo lectura.</summary>
    Task<IReadOnlyList<Camara>> GetListadoAsync(int? lineaId = null, bool incluirInactivas = false, CancellationToken cancellationToken = default);

    /// <summary>Por código (ya normalizado en mayúsculas), con la línea. Con seguimiento.</summary>
    Task<Camara?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);

    /// <summary>Activas y habilitadas (lo que corre el servicio), con la línea. Sólo lectura.</summary>
    Task<IReadOnlyList<Camara>> GetHabilitadasAsync(CancellationToken cancellationToken = default);

    /// <summary>Cámaras activas que cuentan la producción oficial de su línea y magnitud, con la línea. Sólo lectura.</summary>
    Task<IReadOnlyList<Camara>> GetOficialesAsync(CancellationToken cancellationToken = default);

    /// <summary>Otra cámara (activa o no) que ya es la oficial de esa línea y magnitud, o null.</summary>
    Task<Camara?> GetOficialAsync(int lineaId, string magnitud, int? excluirCamaraId, CancellationToken cancellationToken = default);
}

public interface IConteoCamaraMinutoRepository : IRepository<ConteoCamaraMinuto>
{
    /// <summary>Claves (minuto, carril) que ya existen para la cámara entre esos minutos, para no duplicar en un reenvío.</summary>
    Task<HashSet<(DateTimeOffset Minuto, byte Carril)>> GetClavesExistentesAsync(
        int camaraId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken = default);

    /// <summary>Minutos de [desde, hasta) de las cámaras de la línea (o de todas), con la cámara y el turno de producción. Sólo lectura.</summary>
    Task<IReadOnlyList<ConteoCamaraMinuto>> GetRangoAsync(
        DateTimeOffset desde, DateTimeOffset hasta, int? lineaId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Minutos de [desde, hasta) de esas cámaras, livianos (sin navegación ni seguimiento): para la producción oficial
    /// de un tramo, el parcial y la velocidad (T-798 etapa 5).
    /// </summary>
    Task<IReadOnlyList<Services.MinutoConteo>> GetMinutosAsync(
        IReadOnlyCollection<int> camaraIds, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken = default);

    /// <summary>Último minuto guardado de la cámara (o null si nunca mandó).</summary>
    Task<DateTimeOffset?> GetUltimoMinutoAsync(int camaraId, CancellationToken cancellationToken = default);
}

public interface IParadaCamaraRepository : IRepository<ParadaCamara>
{
    Task<HashSet<string>> GetIdentificadoresExistentesAsync(IReadOnlyCollection<string> identificadores, CancellationToken cancellationToken = default);

    /// <summary>Paradas que empiezan en [desde, hasta) de las cámaras de la línea (o de todas), de la más reciente a la más vieja, con cámara (y su línea) y turno de producción. Sólo lectura.</summary>
    Task<IReadOnlyList<ParadaCamara>> GetRangoAsync(
        DateTimeOffset desde, DateTimeOffset hasta, int? lineaId = null, CancellationToken cancellationToken = default);
}

public interface IAlertaCamaraRepository : IRepository<AlertaCamara>
{
    /// <summary>Alertas con esos identificadores, con seguimiento (para completar el fin).</summary>
    Task<IReadOnlyList<AlertaCamara>> GetPorIdentificadoresAsync(IReadOnlyCollection<string> identificadores, CancellationToken cancellationToken = default);

    /// <summary>
    /// Alertas abiertas, más las cerradas que terminaron desde <paramref name="cerradasDesde"/>, con la cámara y su
    /// línea, de la más reciente a la más vieja. Sólo lectura.
    /// </summary>
    Task<IReadOnlyList<AlertaCamara>> GetRecientesAsync(DateTimeOffset cerradasDesde, int? lineaId = null, CancellationToken cancellationToken = default);

    /// <summary>Alertas abiertas (sin fin) de la cámara de esos tipos, con seguimiento.</summary>
    Task<IReadOnlyList<AlertaCamara>> GetAbiertasAsync(int camaraId, IReadOnlyCollection<Enums.TipoAlertaCamara> tipos, CancellationToken cancellationToken = default);
}

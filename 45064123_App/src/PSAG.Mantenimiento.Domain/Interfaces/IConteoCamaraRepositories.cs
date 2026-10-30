using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IConteoCamaraMinutoRepository : IRepository<ConteoCamaraMinuto>
{
    /// <summary>Claves (minuto, carril) que ya existen para el punto entre esos minutos, para no duplicar en un reenvío.</summary>
    Task<HashSet<(DateTimeOffset Minuto, byte Carril)>> GetClavesExistentesAsync(
        int puntoControlCalidadId, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken = default);

    /// <summary>Minutos de [desde, hasta) de la línea (o de todas). Sólo lectura.</summary>
    Task<IReadOnlyList<ConteoCamaraMinuto>> GetRangoAsync(
        DateTimeOffset desde, DateTimeOffset hasta, int? lineaId = null, CancellationToken cancellationToken = default);
}

public interface IParadaCamaraRepository : IRepository<ParadaCamara>
{
    Task<HashSet<string>> GetIdentificadoresExistentesAsync(IReadOnlyCollection<string> identificadores, CancellationToken cancellationToken = default);

    /// <summary>Paradas que empiezan en [desde, hasta), de la más reciente a la más vieja, con línea, punto y turno. Sólo lectura.</summary>
    Task<IReadOnlyList<ParadaCamara>> GetRangoAsync(
        DateTimeOffset desde, DateTimeOffset hasta, int? lineaId = null, CancellationToken cancellationToken = default);
}

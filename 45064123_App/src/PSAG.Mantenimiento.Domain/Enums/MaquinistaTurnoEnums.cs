namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>Cómo quedó registrado el maquinista de un turno (T-783).</summary>
public enum OrigenMaquinistaTurno
{
    /// <summary>El operador confirmó en el login que es el maquinista del turno de esa línea.</summary>
    LOGIN = 1,
    /// <summary>Un supervisor (o administrador) lo cargó o lo corrigió.</summary>
    SUPERVISOR = 2
}

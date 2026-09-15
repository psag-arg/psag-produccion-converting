namespace PSAG.Mantenimiento.Domain.Exceptions;

/// <summary>
/// Excepción base para errores de dominio.
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }
    public IReadOnlyList<string>? Errors { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public DomainException(string code, string message, IEnumerable<string>? errors) : base(message)
    {
        Code = code;
        Errors = errors?.ToList().AsReadOnly();
    }

    public DomainException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }
}

/// <summary>
/// Excepción cuando se intenta una transición de estado no permitida.
/// </summary>
public class TransicionEstadoInvalidaException : DomainException
{
    public TransicionEstadoInvalidaException(string estadoActual, string estadoNuevo)
        : base("TRANSICION_INVALIDA", $"No se permite la transición de estado '{estadoActual}' a '{estadoNuevo}'.")
    {
    }
}

/// <summary>
/// Excepción cuando no se encuentra una entidad.
/// </summary>
public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entidad, object id)
        : base("ENTIDAD_NO_ENCONTRADA", $"No se encontró {entidad} con identificador '{id}'.")
    {
    }
}

/// <summary>
/// Excepción cuando hay un conflicto de concurrencia.
/// </summary>
public class ConcurrencyException : DomainException
{
    public ConcurrencyException()
        : base("CONFLICTO_CONCURRENCIA", "El registro fue modificado por otro usuario. Por favor, recargue los datos e intente nuevamente.")
    {
    }
}

/// <summary>
/// Excepción cuando se viola una regla de negocio.
/// </summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string code, string message) : base(code, message)
    {
    }
}

/// <summary>
/// Excepción cuando faltan datos requeridos para cerrar un evento.
/// </summary>
public class CierreEventoIncompletoException : DomainException
{
    public IReadOnlyList<string> CamposFaltantes { get; }

    public CierreEventoIncompletoException(IEnumerable<string> camposFaltantes)
        : base("CIERRE_INCOMPLETO", $"Faltan datos requeridos para cerrar el evento: {string.Join(", ", camposFaltantes)}")
    {
        CamposFaltantes = camposFaltantes.ToList().AsReadOnly();
    }
}

/// <summary>
/// Excepción cuando una máquina no pertenece a la línea seleccionada.
/// </summary>
public class MaquinaNoPerteneceCLineaException : DomainException
{
    public MaquinaNoPerteneceCLineaException(int maquinaId, int lineaId)
        : base("MAQUINA_LINEA_INVALIDA", $"La máquina con ID {maquinaId} no pertenece a la línea con ID {lineaId}.")
    {
    }
}

/// <summary>
/// Excepción cuando el usuario no tiene permiso para operar en una línea.
/// </summary>
public class AccesoLineaNoAutorizadoException : DomainException
{
    public AccesoLineaNoAutorizadoException(int lineaId)
        : base("ACCESO_LINEA_NO_AUTORIZADO", $"No tiene permiso para operar en la línea con ID {lineaId}.")
    {
    }
}

/// <summary>
/// Excepción cuando se intenta modificar un evento cerrado.
/// </summary>
public class EventoCerradoException : DomainException
{
    public EventoCerradoException()
        : base("EVENTO_CERRADO", "No se puede modificar un evento que ya está cerrado.")
    {
    }
}

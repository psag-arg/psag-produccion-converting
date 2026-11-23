namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Habilitación de un usuario en una línea (tabla ABM_OperadoresLinea, ex UsuariosLineas). La PK sigue siendo
/// UsuarioLineaId (desde la etapa 3 de T-798 ya no la referencia DET_Detenciones: la detención guarda el usuario,
/// OperadorUsuarioId). T-802: se carga sólo desde el alta / edición del usuario (Configuración → Usuarios) y no tiene
/// turno: el turno lo declara el operario al abrirlo (el turno en curso por hora). Al abrir turno sólo puede elegir
/// líneas habilitadas; el que es sólo operario carga detenciones sólo en ellas.
/// </summary>
public class OperadorLinea
{
    public int UsuarioLineaId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public DateTimeOffset FechaAsignacion { get; set; }
    public string AsignadoPorUsuarioId { get; set; } = string.Empty;

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
}

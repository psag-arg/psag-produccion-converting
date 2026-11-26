namespace PSAG.Mantenimiento.Domain.Permisos;

/// <summary>
/// Acciones que se habilitan por rol y opción del sistema (T-802). VER es ver la opción (menú, pantalla y lecturas de la API);
/// ALTA, MODIFICACION y BAJA son cargar, editar y desactivar/eliminar (lógicamente: nada se borra físicamente).
/// Es un enum de banderas para poder expresar las acciones aplicables de una opción.
/// </summary>
[Flags]
public enum AccionPermiso
{
    Ninguna = 0,
    Ver = 1,
    Alta = 2,
    Modificar = 4,
    Baja = 8,
    Todas = Ver | Alta | Modificar | Baja
}

public static class AccionPermisoTexto
{
    /// <summary>Texto de la columna <c>CFG_Opciones.AccionesAplicables</c>: "VER,ALTA,MODIFICACION,BAJA" (en ese orden).</summary>
    public static string AColumna(AccionPermiso acciones) =>
        string.Join(',', Individuales.Where(a => acciones.HasFlag(a)).Select(Nombre));

    public static AccionPermiso DeColumna(string? texto)
    {
        var resultado = AccionPermiso.Ninguna;
        foreach (var parte in (texto ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            resultado |= parte.ToUpperInvariant() switch
            {
                "VER" => AccionPermiso.Ver,
                "ALTA" => AccionPermiso.Alta,
                "MODIFICACION" => AccionPermiso.Modificar,
                "BAJA" => AccionPermiso.Baja,
                _ => AccionPermiso.Ninguna
            };
        }
        return resultado;
    }

    public static readonly IReadOnlyList<AccionPermiso> Individuales =
        [AccionPermiso.Ver, AccionPermiso.Alta, AccionPermiso.Modificar, AccionPermiso.Baja];

    public static string Nombre(AccionPermiso accion) => accion switch
    {
        AccionPermiso.Ver => "VER",
        AccionPermiso.Alta => "ALTA",
        AccionPermiso.Modificar => "MODIFICACION",
        AccionPermiso.Baja => "BAJA",
        _ => accion.ToString().ToUpperInvariant()
    };

    /// <summary>Para los mensajes: "ver", "dar de alta", "modificar", "dar de baja".</summary>
    public static string Verbo(AccionPermiso accion) => accion switch
    {
        AccionPermiso.Ver => "ver",
        AccionPermiso.Alta => "dar de alta",
        AccionPermiso.Modificar => "modificar",
        AccionPermiso.Baja => "dar de baja",
        _ => "usar"
    };
}

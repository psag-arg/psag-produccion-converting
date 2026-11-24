namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Estado de un turno de producción (T-798, 2.6). Se guarda como texto (CHECK en la tabla).
/// </summary>
public enum EstadoTurnoProduccion
{
    /// <summary>En curso. Un solo ABIERTO por línea (índice único filtrado).</summary>
    ABIERTO = 1,
    /// <summary>Cerrado a mano (operador o supervisor).</summary>
    CERRADO = 2,
    /// <summary>Cerrado por el sistema: a la hora de fin programada (turno automático), al abrir el turno siguiente o creado ya pasado.</summary>
    CERRADO_AUTOMATICO = 3,
    /// <summary>
    /// HISTÓRICO (hasta T-798/T-806): programado, pasó su inicio más la tolerancia y nadie lo abrió. Desde el turno
    /// automático (script 44) ya no se genera: el job abre el turno de máquina ABIERTO "sin operador". Queda para leer
    /// los registros viejos (si alguien lo toma, pasa a ABIERTO como antes).
    /// </summary>
    SIN_APERTURA = 4
}

/// <summary>Quién abrió el turno de producción.</summary>
public enum OrigenAperturaTurno
{
    /// <summary>El operario, al entrar (pantalla "Inicio de turno").</summary>
    LOGIN = 1,
    /// <summary>Un supervisor (o mantenimiento / administrador).</summary>
    SUPERVISOR = 2,
    /// <summary>Llegó un dato (conteo, parada, detención) sin turno: lo abrió el sistema, sin operador ni producto.</summary>
    SISTEMA = 3
}

/// <summary>Origen de un tramo de operador (relevo).</summary>
public enum OrigenTramoOperador
{
    /// <summary>El operario lo tomó al entrar (o relevó al que estaba).</summary>
    LOGIN = 1,
    /// <summary>Corrección del supervisor (con motivo).</summary>
    SUPERVISOR = 2
}

/// <summary>Origen de un tramo de producto.</summary>
public enum OrigenTramoProducto
{
    /// <summary>Elegido al tomar el turno.</summary>
    LOGIN = 1,
    /// <summary>Cambio de producto durante el turno.</summary>
    CAMBIO = 2,
    /// <summary>Corrección del supervisor (con motivo).</summary>
    SUPERVISOR = 3,
    /// <summary>
    /// Turno automático (script 44): al abrirlo el sistema, el turno hereda el producto con que terminó el turno anterior de
    /// la línea (snapshot del parámetro vigente a la fecha del turno nuevo). Es un SUPUESTO hasta que el operario o el
    /// supervisor lo confirman (<c>ConfirmadoPorUsuarioId</c>).
    /// </summary>
    HEREDADO = 4
}

/// <summary>Origen del dato oficial de producción (PRD_ProduccionesTurno; se llena en la etapa 5).</summary>
public enum OrigenProduccionTurno
{
    CAMARA = 1,
    CAMARA_CORREGIDA = 2,
    MANUAL = 3
}

/// <summary>Estado de validación del dato oficial de producción.</summary>
public enum EstadoValidacionProduccion
{
    PENDIENTE = 1,
    VALIDADA = 2,
    SIN_DATOS = 3
}

/// <summary>Magnitudes de producción (T-798, 2.0 #7). Se guardan como texto.</summary>
public static class MagnitudProduccion
{
    public const string Mangas = "MANGAS";
    public const string Rollos = "ROLLOS";
    public const string Paquetes = "PAQUETES";
    public const string Bolsones = "BOLSONES";
    public const string Pallets = "PALLETS";

    public static readonly IReadOnlyList<string> Todas = [Mangas, Rollos, Paquetes, Bolsones, Pallets];
}

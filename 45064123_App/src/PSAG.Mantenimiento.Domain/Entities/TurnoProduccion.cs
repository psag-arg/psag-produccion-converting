using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Turno de producción (tabla PRD_TurnosProduccion, T-798 2.6): el EVENTO real de una línea en un turno de
/// <c>ABM_Turnos</c> en una fecha de producción (el día en que empieza el turno: el T3 21–05 del 19/11 sigue siendo del
/// 19/11 después de medianoche). Es transacción, no catálogo: no se borra. Su estado cambia (abrir, cerrar) con
/// control de concurrencia (<see cref="RowVersion"/>). Los relevos y los tramos de producto son tablas hijas.
/// <list type="bullet">
/// <item>Único (LineaId, TurnoId, FechaProduccion).</item>
/// <item>Un solo ABIERTO por línea (índice único filtrado).</item>
/// <item>Inicio/Fin programados y minutos programados son SNAPSHOT al crear el registro: si mañana cambian los
/// horarios o la programación, este turno no cambia.</item>
/// </list>
/// </summary>
public class TurnoProduccion : EntityBase
{
    public const int ObservacionMaxima = 500;

    public long TurnoProduccionId { get; set; }
    public int LineaId { get; set; }
    public int TurnoId { get; set; }

    /// <summary>Día en que empieza el turno (regla única de todo el sistema).</summary>
    public DateOnly FechaProduccion { get; set; }

    /// <summary>Snapshot del horario del turno (hora de planta) al crear el registro.</summary>
    public DateTimeOffset InicioProgramado { get; set; }
    public DateTimeOffset FinProgramado { get; set; }

    /// <summary>Snapshot del plan (programación + excepción del día). 0 = fuera de programación.</summary>
    public int MinutosProgramados { get; set; }

    /// <summary>Apertura real. Null en SIN_APERTURA.</summary>
    public DateTimeOffset? InicioReal { get; set; }

    /// <summary>Cierre real (manual, automático o al abrir el siguiente).</summary>
    public DateTimeOffset? FinReal { get; set; }

    public EstadoTurnoProduccion Estado { get; set; } = EstadoTurnoProduccion.ABIERTO;
    public OrigenAperturaTurno OrigenApertura { get; set; }

    /// <summary>Usuario que lo abrió (null si lo abrió el SISTEMA o está SIN_APERTURA).</summary>
    public string? AbiertoPorUsuarioId { get; set; }

    /// <summary>Usuario que lo cerró a mano (null si lo cerró el sistema).</summary>
    public string? CerradoPorUsuarioId { get; set; }

    public string? ObservacionCierre { get; set; }

    public byte[] RowVersion { get; set; } = [];

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Turno Turno { get; set; } = null!;
    public virtual ICollection<TurnoProduccionOperador> Operadores { get; set; } = new List<TurnoProduccionOperador>();
    public virtual ICollection<TurnoProduccionProducto> Productos { get; set; } = new List<TurnoProduccionProducto>();

    public bool EstaAbierto => Estado == EstadoTurnoProduccion.ABIERTO;

    public bool EstaCerrado => Estado is EstadoTurnoProduccion.CERRADO or EstadoTurnoProduccion.CERRADO_AUTOMATICO;
}

/// <summary>
/// Tramo de operador de un turno de producción (PRD_TurnosProduccionOperadores; reemplaza MaquinistasTurno de T-783).
/// El operador es el USUARIO (no la habilitación). Sin superposición entre los tramos no anulados. Corrección
/// insert-only: el tramo nuevo anula al anterior (<see cref="AnuladoPorId"/> del anterior = Id del nuevo).
/// </summary>
public class TurnoProduccionOperador
{
    public const int MotivoMaximo = 500;

    public long TurnoProduccionOperadorId { get; set; }
    public long TurnoProduccionId { get; set; }
    public string OperadorUsuarioId { get; set; } = string.Empty;
    public DateTimeOffset Desde { get; set; }

    /// <summary>Null = sigue (es el operador vigente si el turno está abierto).</summary>
    public DateTimeOffset? Hasta { get; set; }

    public OrigenTramoOperador Origen { get; set; }
    public string RegistradoPorUsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraRegistro { get; set; }

    /// <summary>Obligatorio si el origen es SUPERVISOR.</summary>
    public string? Motivo { get; set; }

    /// <summary>Tramo que lo reemplazó (corrección). Null = vigente.</summary>
    public long? AnuladoPorId { get; set; }

    // Navegación
    public virtual TurnoProduccion TurnoProduccion { get; set; } = null!;

    public bool Anulado => AnuladoPorId != null;
}

/// <summary>
/// Tramo de producto de un turno de producción (PRD_TurnosProduccionProductos), con SNAPSHOT del parámetro
/// línea-producto vigente (objetivos, minutos de referencia) y de los metros por manga del maestro al declararlo.
/// Un turno sin tramos es "a completar" (abierto por SISTEMA o convertido). Cambiar de producto cierra el tramo
/// vigente. Corrección insert-only con <see cref="AnuladoPorId"/>.
/// </summary>
public class TurnoProduccionProducto
{
    public const int MotivoMaximo = 500;

    public long TurnoProduccionProductoId { get; set; }
    public long TurnoProduccionId { get; set; }
    public int ProductoMaestroId { get; set; }

    /// <summary>SKU opcional (no se pide en esta etapa).</summary>
    public int? ProductoSkuId { get; set; }

    public DateTimeOffset Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }

    /// <summary>Versión del parámetro línea-producto que se usó (PRD_ParametrosLineaProducto).</summary>
    public int ParametroLineaProductoId { get; set; }

    public int? MangasObjetivoTurno { get; set; }
    public int? BolsonesObjetivoTurno { get; set; }
    public int MinutosTurnoReferencia { get; set; }
    public decimal? MetrosPorManga { get; set; }

    public OrigenTramoProducto Origen { get; set; }
    public string DeclaradoPorUsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraRegistro { get; set; }
    public string? Motivo { get; set; }
    public long? AnuladoPorId { get; set; }

    // Navegación
    public virtual TurnoProduccion TurnoProduccion { get; set; } = null!;
    public virtual ProductoMaestro ProductoMaestro { get; set; } = null!;
    public virtual ProductoSku? ProductoSku { get; set; }
    public virtual ParametroLineaProducto ParametroLineaProducto { get; set; } = null!;
    public virtual ICollection<ProduccionTurno> Producciones { get; set; } = new List<ProduccionTurno>();

    public bool Anulado => AnuladoPorId != null;
}

/// <summary>
/// Producción OFICIAL por tramo de producto y magnitud (PRD_ProduccionesTurno). Se crea vacía en la etapa 2 y se
/// llena en la etapa 5 (cámara + validación). "Sin datos" (SIN_DATOS, cantidad null) es distinto de cero.
/// </summary>
public class ProduccionTurno
{
    public long ProduccionTurnoId { get; set; }
    public long TurnoProduccionProductoId { get; set; }

    /// <summary>MANGAS, ROLLOS, PAQUETES, BOLSONES o PALLETS (<see cref="MagnitudProduccion"/>).</summary>
    public string Magnitud { get; set; } = string.Empty;

    public decimal? CantidadCamara { get; set; }
    public int? MinutosConDatos { get; set; }
    public decimal? CantidadOficial { get; set; }
    public OrigenProduccionTurno Origen { get; set; }
    public EstadoValidacionProduccion EstadoValidacion { get; set; } = EstadoValidacionProduccion.PENDIENTE;
    public string? ValidadoPorUsuarioId { get; set; }
    public DateTimeOffset? FechaHoraValidacion { get; set; }
    public string RegistradoPorUsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraRegistro { get; set; }
    public long? AnuladoPorId { get; set; }

    // Navegación
    public virtual TurnoProduccionProducto TurnoProduccionProducto { get; set; } = null!;
}

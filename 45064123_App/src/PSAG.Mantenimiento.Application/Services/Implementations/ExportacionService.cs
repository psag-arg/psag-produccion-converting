using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using System.Globalization;
using System.Text;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

public class ExportacionService : IExportacionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExportacionService> _logger;
    private const int MaxExportSize = 10000;

    public ExportacionService(IUnitOfWork unitOfWork, ILogger<ExportacionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<byte[]>> ExportarDetencionesCsvAsync(
        DetencionFiltroRequest filtro,
        string usuarioId,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Usuario {UsuarioId} exportando detenciones a CSV con filtros: FechaDesde={FechaDesde}, FechaHasta={FechaHasta}, Estado={Estado}",
                usuarioId, filtro.FechaDesde, filtro.FechaHasta, filtro.Estado);

            // Parsear estado si está definido
            EstadoDetencion? estado = null;
            if (!string.IsNullOrEmpty(filtro.Estado) && Enum.TryParse<EstadoDetencion>(filtro.Estado, out var estadoParsed))
            {
                estado = estadoParsed;
            }

            // Ajustar estado por flags SoloAbiertos/SoloCerrados
            if (filtro.SoloCerrados == true)
            {
                estado = EstadoDetencion.CERRADA;
            }

            // Usar el método paginado con tamaño grande para obtener todos los registros
            var (detenciones, totalCount) = await _unitOfWork.Detenciones.GetPaginadoAsync(
                page: 1,
                pageSize: MaxExportSize,
                lineaId: filtro.LineaId,
                equipoId: filtro.EquipoId,
                estado: estado,
                tipoFallaId: filtro.TipoFallaId,
                detalleFallaId: filtro.DetalleFallaId,
                turnoId: filtro.TurnoId,
                fechaDesde: filtro.FechaDesde,
                fechaHasta: filtro.FechaHasta,
                busqueda: filtro.Busqueda,
                ordenarPor: "Fecha",
                ordenDescendente: true,
                cancellationToken: cancellationToken);

            // Filtrar por líneas permitidas
            if (lineasPermitidas != null && lineasPermitidas.Any())
            {
                detenciones = detenciones.Where(d => lineasPermitidas.Contains(d.LineaId));
            }

            // Filtrar SoloAbiertos (ya que no está soportado directamente en el repositorio)
            if (filtro.SoloAbiertos == true)
            {
                detenciones = detenciones.Where(d => d.EstadoActual != EstadoDetencion.CERRADA);
            }

            // Filtrar por duración
            if (filtro.DuracionMinimaMinutos.HasValue)
            {
                detenciones = detenciones.Where(d => d.MinutosEfectivos >= filtro.DuracionMinimaMinutos.Value);
            }

            if (filtro.DuracionMaximaMinutos.HasValue)
            {
                detenciones = detenciones.Where(d => d.MinutosEfectivos <= filtro.DuracionMaximaMinutos.Value);
            }

            // Filtrar por usuarios
            if (!string.IsNullOrEmpty(filtro.CreadoPorUsuarioId))
            {
                detenciones = detenciones.Where(d => d.CreadoPorUsuarioId == filtro.CreadoPorUsuarioId);
            }

            if (!string.IsNullOrEmpty(filtro.CerradoPorUsuarioId))
            {
                detenciones = detenciones.Where(d => d.CerradoPorUsuarioId == filtro.CerradoPorUsuarioId);
            }

            var listaDetenciones = detenciones.ToList();

            // Generar CSV
            var csv = GenerarCsv(listaDetenciones);

            _logger.LogInformation(
                "Exportación completada. Total registros: {Total} de {TotalDisponible}",
                listaDetenciones.Count, totalCount);

            return Result<byte[]>.Success(csv);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al exportar detenciones a CSV");
            return Result<byte[]>.Failure($"Error al exportar detenciones: {ex.Message}");
        }
    }

    private static byte[] GenerarCsv(IEnumerable<Detencion> detenciones)
    {
        var sb = new StringBuilder();

        // Cabecera
        sb.AppendLine("Codigo,Fecha,Linea,Equipo,TipoFalla,DetalleFalla,Turno,MinutosDeclarados,FechaHoraParada,FechaHoraArranque,OrigenTiempo,MinutosEfectivos,Descripcion,Estado,AccionesRealizadas,Resolucion,FechaHoraCierre,FechaHoraCreacion");

        foreach (var d in detenciones)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(d.Codigo),
                d.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EscapeCsv(d.Linea?.Nombre ?? ""),
                EscapeCsv(d.Equipo?.Nombre ?? ""),
                EscapeCsv(d.TipoFalla?.Nombre ?? ""),
                EscapeCsv(d.DetalleFalla?.Nombre ?? ""),
                EscapeCsv(d.Turno?.Nombre ?? ""),
                d.MinutosDeclarados?.ToString(CultureInfo.InvariantCulture) ?? "",
                d.FechaHoraParada?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "",
                d.FechaHoraArranque?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "",
                d.OrigenTiempo.ToString(),
                d.MinutosEfectivos.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(d.Descripcion),
                EscapeCsv(d.EstadoActual.ObtenerDescripcion()),
                EscapeCsv(d.AccionesRealizadas ?? ""),
                EscapeCsv(d.ResolucionDescripcion ?? ""),
                d.FechaHoraCierre?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "",
                d.FechaHoraCreacion.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            ));
        }

        // Agregar BOM UTF-8 para que Excel detecte correctamente la codificación
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + content.Length];
        bom.CopyTo(result, 0);
        content.CopyTo(result, bom.Length);

        return result;
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // Si contiene comas, comillas o saltos de línea, envolver en comillas
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.Proceso;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-545 · Plantilla de Control de Proceso de TANGO 2 (planilla "Control Proceso Tango 2 (30mts)").
/// Lee los VALUES de database/24_t545_plantilla_control_proceso_tango2.sql, los carga por el maestro
/// (grupos y puntos) + estándares del producto, y verifica la carga del maquinista.
/// </summary>
public class PlantillaTango2Tests : IDisposable
{
    private const string Script = "24_t545_plantilla_control_proceso_tango2.sql";
    private const string Admin = "SCRIPT-24";
    private const string Maquinista = "maquinista-test";

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;
    private readonly ControlProcesoPlantillaService _maestro;
    private readonly RegistroProcesoService _carga;
    private readonly Linea _tango2;
    private readonly Producto _higienico;
    private readonly Dictionary<string, int> _parametroPorCodigo = new();

    public PlantillaTango2Tests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();

        var uow = new UnitOfWork(_context);
        _maestro = new ControlProcesoPlantillaService(uow, NullLogger<ControlProcesoPlantillaService>.Instance);
        _carga = new RegistroProcesoService(uow, NullLogger<RegistroProcesoService>.Instance);

        // Como en producción: una sola línea, Nombre 'TANGO 2' y Codigo vacío.
        _tango2 = new Linea { Codigo = "", Nombre = "TANGO 2", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _higienico = new Producto { Codigo = "HIGIENICO 30 MTS", Nombre = "Higiénico 30 mts", Orden = 1, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        var turno = new Turno
        {
            Codigo = "T1", Nombre = "Turno 1 (Mañana)", HoraInicio = new TimeOnly(0, 0), HoraFin = new TimeOnly(0, 0),
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        _context.AddRange(_tango2, _higienico, turno);
        _context.SaveChanges();
        _context.Add(new LineaProducto { LineaId = _tango2.LineaId, ProductoId = _higienico.ProductoId });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
    }

    [Fact]
    public void Script_TraeLosGruposPuntosYEstandaresDeLaPlanilla()
    {
        var (grupos, puntos) = LeerScript();

        Assert.Equal(6, grupos.Count);
        Assert.Equal(57, puntos.Count);
        Assert.Equal(56, puntos.Count(p => p.TipoDato == "NUMERICO"));
        Assert.Equal(54, puntos.Count(p => p.Estandar != null));
        Assert.Equal("PARÁMETROS - Producto y Máquina", grupos[0].Nombre);
        Assert.Equal("PARÁMETROS - Presiones Neumáticas (Seguridades)", grupos[^1].Nombre);
        Assert.Equal(puntos.Count, puntos.Select(p => p.Codigo).Distinct().Count());

        var roturas = puntos.Single(p => p.Codigo == "T2-ROTURAS");
        Assert.Equal(("Número de roturas", "152 - 153", 152m, 153m), (roturas.Nombre, roturas.Estandar, roturas.Minimo, roturas.Maximo));
        var rabo = puntos.Single(p => p.Codigo == "T2-RABO-LS");
        Assert.Equal(("> 10mm", 10m, (decimal?)null, "mm"), (rabo.Estandar, rabo.Minimo, rabo.Maximo, rabo.Unidad));
        var largo = puntos.Single(p => p.Codigo == "T2-LARGO-PERF");
        Assert.Equal(("202", (decimal?)null, (decimal?)null), (largo.Estandar, largo.Minimo, largo.Maximo));
        Assert.Equal("-0,7", puntos.Single(p => p.Codigo == "T2-SR7-MARCHA").Estandar);
        Assert.Null(puntos.Single(p => p.Codigo == "T2-VEL-MAQ").Estandar);
        Assert.Equal("DIÁMETRO DE MANGA", puntos.Single(p => p.Codigo == "T2-MANGA-LS").Seccion);
        Assert.Equal("Bar", puntos.Single(p => p.Codigo == "T2-GOF-PRES-TRABAJO").Unidad);
        // Valores únicos: solo texto, sin límites (no se inventan tolerancias)
        Assert.All(puntos.Where(p => p.Estandar != null && !p.Estandar.Contains('-', StringComparison.Ordinal) && !p.Estandar.StartsWith('>')),
            p => Assert.True(p.Minimo == null && p.Maximo == null, p.Codigo));
    }

    [Fact]
    public async Task Maquinista_CargaControlDeTango2ConHigienico30()
    {
        await CargarPlantillaAsync();

        var inicio = await _carga.IniciarRegistroAsync(
            new IniciarRegistroProcesoRequest { LineaId = _tango2.LineaId, ProductoId = _higienico.ProductoId }, Maquinista);
        Assert.True(inicio.IsSuccess, inicio.Error);

        var control = (await _carga.ObtenerCompletoAsync(inicio.Value!.RegistroProcesoId)).Value!;
        Assert.Equal("Higiénico 30 mts", control.ProductoNombre);
        Assert.Equal(57, control.CantidadParametros);
        Assert.Equal(
            new[] { "PARÁMETROS - Producto y Máquina", "CALIDAD DE MANGA", "PARÁMETROS - Grupo Gofrador",
                    "PARÁMETROS - Tensión del Papel", "PARÁMETROS - Rebobinadora", "PARÁMETROS - Presiones Neumáticas (Seguridades)" },
            control.Grupos.Select(g => g.Nombre));

        var d = control.Grupos.SelectMany(g => g.Detalles).ToDictionary(x => x.ParametroCodigo);
        Assert.Equal(("152 - 153", 152m, 153m), (d["T2-ROTURAS"].ValorEstandarSnapshot, d["T2-ROTURAS"].ValorMin, d["T2-ROTURAS"].ValorMax));
        Assert.Equal(("202", (decimal?)null, (decimal?)null), (d["T2-LARGO-PERF"].ValorEstandarSnapshot, d["T2-LARGO-PERF"].ValorMin, d["T2-LARGO-PERF"].ValorMax));
        Assert.Equal(("> 10mm", 10m, (decimal?)null), (d["T2-RABO-LS"].ValorEstandarSnapshot, d["T2-RABO-LS"].ValorMin, d["T2-RABO-LS"].ValorMax));
        Assert.Null(d["T2-VEL-MAQ"].ValorEstandarSnapshot);
        Assert.Equal("POR_TURNO", d["T2-ROTURAS"].Frecuencia);

        ValorParametroRequest V(string codigo, decimal valor, string? obs = null) =>
            new() { ParametroId = _parametroPorCodigo[codigo], ValorReal = valor, Observacion = obs };

        // Roturas 160 (fuera de 152 - 153) y rabo 8 mm (< 10) sin observación: no se guarda nada.
        var rechazo = await _carga.GuardarValoresAsync(new GuardarValoresRequest
        {
            RegistroProcesoId = control.RegistroProcesoId,
            RowVersion = control.RowVersion,
            Valores = { V("T2-ROTURAS", 160m), V("T2-RABO-LS", 8m), V("T2-LARGO-PERF", 202m) }
        }, Maquinista);
        Assert.False(rechazo.IsSuccess);
        Assert.Contains("Número de roturas", rechazo.Error);
        Assert.Contains("Lado Servicio", rechazo.Error);
        Assert.DoesNotContain("Largo de la Perforación", rechazo.Error);
        Assert.Equal(0, (await _carga.ObtenerCompletoAsync(control.RegistroProcesoId)).Value!.CantidadCompletados);

        // Con observación en los desvíos se guarda; los valores dentro de rango y las referencias no son desvío.
        var ok = await _carga.GuardarValoresAsync(new GuardarValoresRequest
        {
            RegistroProcesoId = control.RegistroProcesoId,
            RowVersion = control.RowVersion,
            Valores =
            {
                V("T2-ROTURAS", 160m, "Cuchilla gastada"),
                V("T2-RABO-LS", 8m, "Se ajustó el corte"),
                V("T2-RABO-C", 10m),            // borde del "> 10mm": el validador es inclusivo (>=)
                V("T2-RABO-LO", 15m),
                V("T2-DIAM-LOG", 99.5m),        // borde del rango
                V("T2-LARGO-PERF", 202m),       // valor único: referencia
                V("T2-DIAM-TAMPON", 50m),       // referencia 43: distinto, pero no se valida
                V("T2-SR7-MARCHA", -0.7m),      // negativos válidos
                V("T2-GOF-PRES-TRABAJO", 3.9m),
                V("T2-VEL-MAQ", 350m)           // "-": sin estándar
            }
        }, Maquinista);
        Assert.True(ok.IsSuccess, ok.Error);

        var guardado = (await _carga.ObtenerCompletoAsync(control.RegistroProcesoId)).Value!;
        var g = guardado.Grupos.SelectMany(x => x.Detalles).ToDictionary(x => x.ParametroCodigo);
        Assert.Equal(2, guardado.CantidadDesvios);
        Assert.True(g["T2-ROTURAS"].FueraDeEstandar);
        Assert.True(g["T2-RABO-LS"].FueraDeEstandar);
        Assert.All(new[] { "T2-RABO-C", "T2-RABO-LO", "T2-DIAM-LOG", "T2-LARGO-PERF", "T2-DIAM-TAMPON", "T2-SR7-MARCHA", "T2-GOF-PRES-TRABAJO", "T2-VEL-MAQ" },
            c => Assert.False(g[c].FueraDeEstandar, c));
        Assert.Equal("202", g["T2-LARGO-PERF"].ValorEstandarSnapshot);
        Assert.Equal("> 10mm", g["T2-RABO-LS"].ValorEstandarSnapshot);
        Assert.Equal("PARCIAL", guardado.Estado);
    }

    // ---------------------------------------------------------------------------------------------
    // Carga de la plantilla: grupos y puntos por el maestro, estándares del producto directo (no hay maestro).
    // ---------------------------------------------------------------------------------------------

    private async Task CargarPlantillaAsync()
    {
        var (grupos, puntos) = LeerScript();
        var grupoPorCodigo = new Dictionary<string, int>();
        foreach (var grupo in grupos)
        {
            var r = await _maestro.CrearGrupoAsync(new GuardarGrupoControlRequest { LineaId = _tango2.LineaId, Nombre = grupo.Nombre, Orden = grupo.Orden }, Admin);
            Assert.True(r.IsSuccess, r.Error);
            grupoPorCodigo[grupo.Codigo] = r.Value;
        }

        foreach (var p in puntos)
        {
            var r = await _maestro.CrearPuntoAsync(new GuardarPuntoControlRequest
            {
                GrupoParametroId = grupoPorCodigo[p.CodigoGrupo],
                Seccion = p.Seccion,
                Codigo = p.Codigo,
                Nombre = p.Nombre,
                TipoDato = p.TipoDato,
                UnidadMedida = p.Unidad,
                Frecuencia = "POR_TURNO",
                Orden = p.Orden
            }, Admin);
            Assert.True(r.IsSuccess, $"{p.Codigo}: {r.Error}");
            _parametroPorCodigo[p.Codigo] = r.Value;
        }

        _context.AddRange(puntos.Where(p => p.Estandar != null).Select(p => new EstandarParametro
        {
            LineaId = _tango2.LineaId,
            ProductoId = _higienico.ProductoId,
            ParametroId = _parametroPorCodigo[p.Codigo],
            ValorEstandarTexto = p.Estandar,
            ValorMin = p.Minimo,
            ValorMax = p.Maximo,
            CreadoPorUsuarioId = Admin,
            ModificadoPorUsuarioId = Admin
        }));
        await _context.SaveChangesAsync();
    }

    private sealed record GrupoScript(string Codigo, string Nombre, int Orden);

    private sealed record PuntoScript(string CodigoGrupo, string? Seccion, string Codigo, string Nombre, string TipoDato,
        string? Unidad, int Orden, string? Estandar, decimal? Minimo, decimal? Maximo);

    private static (List<GrupoScript> Grupos, List<PuntoScript> Puntos) LeerScript()
    {
        var lineas = File.ReadAllLines(RutaScript(), Encoding.UTF8);

        var grupos = FilasDe(lineas, "INSERT INTO @Grupos")
            .Select(v => new GrupoScript(v[0]!, v[1]!, int.Parse(v[2]!, CultureInfo.InvariantCulture)))
            .ToList();
        var puntos = FilasDe(lineas, "INSERT INTO @Puntos")
            .Select(v => new PuntoScript(v[0]!, v[1], v[2]!, v[3]!, v[4]!, v[5], int.Parse(v[6]!, CultureInfo.InvariantCulture), v[7],
                v[8] == null ? null : decimal.Parse(v[8]!, CultureInfo.InvariantCulture),
                v[9] == null ? null : decimal.Parse(v[9]!, CultureInfo.InvariantCulture)))
            .ToList();
        return (grupos, puntos);
    }

    /// <summary>Filas "(...)," que siguen al INSERT indicado, hasta la que termina en ";".</summary>
    private static IEnumerable<List<string?>> FilasDe(string[] lineas, string insert)
    {
        var desde = Array.FindIndex(lineas, l => l.TrimStart().StartsWith(insert, StringComparison.Ordinal));
        Assert.True(desde >= 0, $"No se encontró '{insert}' en {Script}.");

        for (var i = desde + 1; i < lineas.Length; i++)
        {
            var linea = lineas[i].Trim();
            if (linea.Length == 0)
                continue;
            Assert.StartsWith("(", linea);
            var fin = linea.EndsWith(';');
            var cuerpo = linea.TrimEnd(',', ';');
            Assert.EndsWith(")", cuerpo);
            yield return Valores(cuerpo[1..^1]);
            if (fin)
                yield break;
        }
    }

    /// <summary>
    /// Valores T-SQL separados por coma: NULL, números, y textos N'...' concatenados con NCHAR(n).
    /// </summary>
    private static List<string?> Valores(string fila)
    {
        var valores = new List<string?>();
        var i = 0;
        while (i < fila.Length)
        {
            while (i < fila.Length && fila[i] == ' ') i++;
            var texto = new StringBuilder();
            string? crudo = null;
            var esTexto = false;
            while (i < fila.Length && fila[i] != ',')
            {
                if (fila[i] == ' ' || fila[i] == '+') { i++; continue; }
                if (fila[i] == 'N' && i + 1 < fila.Length && fila[i + 1] == '\'')
                {
                    esTexto = true;
                    i += 2;
                    while (true)
                    {
                        if (fila[i] == '\'' && i + 1 < fila.Length && fila[i + 1] == '\'') { texto.Append('\''); i += 2; }
                        else if (fila[i] == '\'') { i++; break; }
                        else texto.Append(fila[i++]);
                    }
                }
                else if (fila.AsSpan(i).StartsWith("NCHAR("))
                {
                    esTexto = true;
                    var cierre = fila.IndexOf(')', i);
                    texto.Append((char)int.Parse(fila[(i + 6)..cierre], CultureInfo.InvariantCulture));
                    i = cierre + 1;
                }
                else
                {
                    var j = i;
                    while (j < fila.Length && fila[j] != ',' && fila[j] != ' ') j++;
                    crudo = fila[i..j];
                    i = j;
                }
            }
            i++; // coma
            valores.Add(esTexto ? texto.ToString() : crudo == "NULL" ? null : crudo);
        }
        return valores;
    }

    private static string RutaScript()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var ruta = Path.Combine(dir.FullName, "database", Script);
            if (File.Exists(ruta))
                return ruta;
        }
        throw new FileNotFoundException($"No se encontró database/{Script} subiendo desde {AppContext.BaseDirectory}.");
    }
}

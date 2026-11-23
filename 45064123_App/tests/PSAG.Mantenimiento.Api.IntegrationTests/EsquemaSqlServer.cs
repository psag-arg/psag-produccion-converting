using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Fixture de la colección <see cref="ColeccionBaseDeDatos"/>: en modo SQL Server arma el esquema de la base de TEST
/// UNA vez por corrida con los mismos scripts y en el mismo orden que producción (ver database/README.md, Desarrollo):
/// <c>dev/limpiar_base_dev.sql</c>, 02–08, <c>dev/seed_previo_t798_maestros.sql</c>, 17, 18, 11, 12, 19–30, T-798 31–38 y T-802 39.
/// Cada script corre en su propia conexión, por lotes separados por <c>GO</c> (como <c>sqlcmd -I -b -f 65001</c>:
/// QUOTED_IDENTIFIER ON, se corta en el primer error, UTF-8), con <c>USE [PSAG_Mantenimiento]</c> cambiado por la base de TEST.
/// Con <c>PSAG_TEST_ESQUEMA=REUSAR</c> no se rearma si la base ya tiene el esquema (para iterar rápido; para entregar, rearmar).
/// En modo SQLite no hace nada.
/// </summary>
public sealed class EsquemaSqlServer
{
    public static readonly string[] Scripts =
    [
        "dev/limpiar_base_dev.sql",
        "02_create_schema.sql", "03_create_identity.sql", "04_create_tables.sql", "05_create_constraints_indexes.sql",
        "06_create_views.sql", "07_seed_catalogs.sql", "08_seed_roles.sql",
        "dev/seed_previo_t798_maestros.sql",
        "17_t76_ajustes.sql", "18_t76_tiempo_programado_historico.sql",
        "11_create_tables_proceso.sql", "12_create_tables_inspeccion.sql",
        "19_t541_t542_horas_programadas.sql", "20_t543_t544_analisis_fallas.sql", "21_t545_control_de_proceso.sql",
        "22_t546_t547_tareas_mantenimiento.sql", "23_t611_calidad_defectos.sql", "24_t545_plantilla_control_proceso_tango2.sql",
        "25_t610_conteo_camara.sql", "26_t610_detencion_automatica_camara.sql", "27_inspecciones_empaquetadora_tmc_tango2.sql",
        "28_eficiencia_objetivo_camara.sql", "29_snapshot_rango_control_proceso.sql", "30_umbral_detencion_segundos.sql",
        "31_t798_renombre_por_modulo.sql", "32_t798_maestros_productos.sql", "33_t798_datos_tango2.sql",
        "34_t798_turnos_produccion.sql", "35_t798_detenciones_turno.sql", "36_t798_camaras.sql",
        "37_t798_indicadores.sql", "38_t798_conversion_turnos.sql",
        "39_t802_roles_permisos.sql"
    ];

    private static readonly Lazy<bool> Armado = new(Armar, LazyThreadSafetyMode.ExecutionAndPublication);

    public EsquemaSqlServer()
    {
        if (BaseDePrueba.EsSqlServer)
            Asegurar();
    }

    public static void Asegurar() => _ = Armado.Value;

    private static readonly Regex Use = new(@"^([ \t]*USE[ \t]+)\[?PSAG_Mantenimiento\]?([ \t]*;)?[ \t]*$", RegexOptions.Multiline);
    private static readonly Regex UseCualquiera = new(@"^[ \t]*USE[ \t]+(\S+?)[ \t]*;?[ \t]*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static bool Armar()
    {
        var conexion = BaseDePrueba.ConexionSqlServer;
        var baseDatos = new SqlConnectionStringBuilder(conexion).InitialCatalog;

        if (string.Equals(Environment.GetEnvironmentVariable("PSAG_TEST_ESQUEMA")?.Trim(), "REUSAR", StringComparison.OrdinalIgnoreCase)
            && TieneEsquemaT798(conexion))
        {
            Console.WriteLine($"[EsquemaSqlServer] {baseDatos}: se reusa el esquema existente (PSAG_TEST_ESQUEMA=REUSAR).");
            return true;
        }

        var reloj = Stopwatch.StartNew();
        foreach (var script in Scripts)
            Correr(conexion, baseDatos, script);
        BaseDePrueba.OlvidarOrden();
        Console.WriteLine($"[EsquemaSqlServer] {baseDatos}: {Scripts.Length} scripts en {reloj.Elapsed.TotalSeconds:0.0} s.");
        return true;
    }

    private static bool TieneEsquemaT798(string conexion)
    {
        using var c = new SqlConnection(conexion);
        c.Open();
        using var cmd = new SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(N'mnt.CAM_Camaras') IS NOT NULL AND COL_LENGTH(N'mnt.PRD_ProduccionesTurno', N'Motivo') IS NOT NULL THEN 1 ELSE 0 END", c);
        return (int)cmd.ExecuteScalar()! == 1;
    }

    /// <summary>Corre un script de database/ contra la base de TEST (sólo SQL Server), como lo hace el armado del esquema.</summary>
    internal static void CorrerScript(string script)
    {
        var conexion = BaseDePrueba.ConexionSqlServer;
        Correr(conexion, new SqlConnectionStringBuilder(conexion).InitialCatalog, script);
    }

    private static void Correr(string conexion, string baseDatos, string script)
    {
        var texto = Use.Replace(ScriptSql.Texto(script), m => $"{m.Groups[1].Value}[{baseDatos}]{m.Groups[2].Value}");
        foreach (Match m in UseCualquiera.Matches(texto))
            if (!string.Equals(m.Groups[1].Value.Trim('[', ']', ';'), baseDatos, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{script}: USE {m.Groups[1].Value} no se pudo cambiar por la base de TEST.");

        var mensajes = new StringBuilder();
        using var c = new SqlConnection(conexion);
        c.InfoMessage += (_, e) => mensajes.AppendLine(e.Message);
        c.Open();
        var numero = 0;
        foreach (var lote in Lotes(texto))
        {
            numero++;
            if (string.IsNullOrWhiteSpace(lote))
                continue;
            try
            {
                using var cmd = new SqlCommand(lote, c) { CommandTimeout = 600 };
                cmd.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(
                    $"Falló {script}, lote {numero}: {ex.Message}\n--- mensajes ---\n{Ultimas(mensajes.ToString(), 20)}", ex);
            }
        }
    }

    /// <summary>
    /// Separa los lotes por las líneas <c>GO</c> como sqlcmd: no corta dentro de comentarios <c>/* */</c> (anidados)
    /// ni de textos entre comillas (un bloque comentado con GO adentro es un solo comentario).
    /// </summary>
    internal static IEnumerable<string> Lotes(string texto)
    {
        var lote = new StringBuilder();
        var comentario = 0;      // profundidad de /* */
        var enTexto = false;     // dentro de '...'
        foreach (var linea in texto.Replace("\r\n", "\n").Split('\n'))
        {
            if (comentario == 0 && !enTexto && EsGo(linea))
            {
                yield return lote.ToString();
                lote.Clear();
                continue;
            }
            for (var i = 0; i < linea.Length; i++)
            {
                var c = linea[i];
                var sig = i + 1 < linea.Length ? linea[i + 1] : '\0';
                if (enTexto)
                {
                    if (c == '\'' && sig == '\'') i++;
                    else if (c == '\'') enTexto = false;
                }
                else if (comentario > 0)
                {
                    if (c == '/' && sig == '*') { comentario++; i++; }
                    else if (c == '*' && sig == '/') { comentario--; i++; }
                }
                else if (c == '-' && sig == '-') break;
                else if (c == '/' && sig == '*') { comentario++; i++; }
                else if (c == '\'') enTexto = true;
            }
            lote.Append(linea).Append('\n');
        }
        if (lote.ToString().Trim().Length > 0)
            yield return lote.ToString();
    }

    private static bool EsGo(string linea)
    {
        var t = linea.Trim();
        var comentario = t.IndexOf("--", StringComparison.Ordinal);
        if (comentario >= 0) t = t[..comentario].TrimEnd();
        return t.Equals("GO", StringComparison.OrdinalIgnoreCase);
    }

    private static string Ultimas(string texto, int lineas) =>
        string.Join('\n', texto.Split('\n').TakeLast(lineas));
}

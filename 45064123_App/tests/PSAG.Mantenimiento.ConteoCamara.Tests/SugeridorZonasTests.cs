using PSAG.Mantenimiento.ConteoCamara.Herramientas;

namespace PSAG.Mantenimiento.ConteoCamara.Tests;

/// <summary>El filtro de pertenencia evita que la búsqueda de zonas salte al carril vecino.</summary>
public class SugeridorZonasTests
{
    [Fact]
    public void MismoCarrilDesfasado_CorrelacionAlta_OtroCarril_Baja()
    {
        // Mismo ritmo de corte en los dos carriles, pero cada uno con sus irregularidades.
        var r = new Random(7);
        double[] Carril(int semilla)
        {
            var rr = new Random(semilla);
            double fase = 0;
            return Enumerable.Range(0, 1200).Select(i =>
            {
                fase += 0.278 + (rr.NextDouble() - 0.5) * 0.12;   // paso con jitter propio del carril
                return 120 + 40 * Math.Sin(2 * Math.PI * fase);
            }).ToArray();
        }
        var carrilA = Carril(1);
        var carrilB = Carril(2);
        var masAbajoEnA = carrilA.Skip(8).Concat(Enumerable.Repeat(120.0, 8)).Select(v => v + (r.NextDouble() - 0.5) * 4).ToArray();

        var mismo = SugeridorZonas.CorrelacionConDesfase(SugeridorZonas.PasaAlto(masAbajoEnA), SugeridorZonas.PasaAlto(carrilA), 30);
        var otro = SugeridorZonas.CorrelacionConDesfase(SugeridorZonas.PasaAlto(masAbajoEnA), SugeridorZonas.PasaAlto(carrilB), 30);

        Assert.True(mismo > 0.8, $"mismo carril {mismo:0.00}");
        Assert.True(otro < mismo - 0.3, $"otro carril {otro:0.00}");
    }

    [Fact]
    public void PasaAlto_SacaElNivel()
    {
        var s = Enumerable.Range(0, 100).Select(i => 200.0 + (i % 2 == 0 ? 10 : -10)).ToArray();
        var h = SugeridorZonas.PasaAlto(s);
        Assert.InRange(h.Skip(10).Take(80).Average(), -1.5, 1.5);
    }
}

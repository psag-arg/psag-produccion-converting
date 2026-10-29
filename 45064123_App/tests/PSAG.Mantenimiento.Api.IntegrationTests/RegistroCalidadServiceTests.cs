using System.Text;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Application.DTOs.Calidad;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-611 contra SQLite y disco reales: ingesta, carga manual, imagen, revisión con historial, permisos y filtros.
/// </summary>
public class RegistroCalidadServiceTests : CalidadTestBase
{
    private IngestaCalidadRequest Ingesta(string? fechaHora = null, string? id = null, string tipo = "TEST-RABO", string punto = "test-cam-t2") => new()
    {
        PuntoControl = punto,
        TipoDefecto = tipo,
        FechaHora = fechaHora ?? HoraPlantaTexto(1, 10),
        Confianza = "0.87",
        ValorMedido = "14.5",
        IdentificadorExterno = id
    };

    private async Task<long> IngestarAsync(string? fechaHora = null, string tipo = "TEST-RABO", string punto = "TEST-CAM-T2")
    {
        var resultado = await Service.IngestarAsync(Ingesta(fechaHora, tipo: tipo, punto: punto), ImagenDe(Png));
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        return resultado.Value!.RegistroCalidadId;
    }

    // ===================== Ingesta =====================

    [Fact]
    public async Task Ingesta_CreaRegistroAutomaticoPendiente_ConTurnoCalculadoEImagenEnDisco()
    {
        var resultado = await Service.IngestarAsync(Ingesta(HoraPlantaTexto(1, 10, 30)), ImagenDe(Png));

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.False(resultado.Value!.Duplicado);
        Assert.Equal("PENDIENTE", resultado.Value.EstadoRevision);
        Assert.Equal("Tango 2", resultado.Value.LineaNombre);

        var registro = await Context.RegistrosCalidad.AsNoTracking().SingleAsync();
        Assert.Equal(OrigenRegistroCalidad.AUTOMATICO, registro.Origen);
        Assert.Equal(RegistroCalidad.UsuarioDetector, registro.CreadoPorUsuarioId);
        Assert.Equal(TurnoManana.TurnoId, registro.TurnoId);
        Assert.Equal(Tango2.LineaId, registro.LineaId);
        Assert.Equal(0.87m, registro.Confianza);
        Assert.Equal(14.5m, registro.ValorMedido);
        Assert.Equal("mm", registro.UnidadMedida); // la del defecto, porque el detector no mandó unidad
        Assert.Equal("image/png", registro.ImagenContentType);
        Assert.True(File.Exists(Path.Combine(Carpeta, registro.ImagenNombreArchivo!)));
        Assert.Null(registro.RevisadoPorUsuarioId);
    }

    [Fact]
    public async Task Ingesta_HoraDeLaTarde_TomaElTurnoDeLaTarde()
    {
        await IngestarAsync(HoraPlantaTexto(1, 15));

        Assert.Equal(TurnoTarde.TurnoId, (await Context.RegistrosCalidad.SingleAsync()).TurnoId);
    }

    [Fact]
    public async Task Ingesta_ConOffset_SeRespetaElInstante()
    {
        // 13:30 UTC = 10:30 en la planta (UTC-3) → turno mañana.
        var dia = DiaPlanta(1).ToString("yyyy-MM-dd");
        await IngestarAsync($"{dia}T13:30:00Z");

        var registro = await Context.RegistrosCalidad.SingleAsync();
        Assert.Equal(TurnoManana.TurnoId, registro.TurnoId);
    }

    [Fact]
    public async Task Ingesta_SinTurnoParaEsaHora_NoSePierde()
    {
        await IngestarAsync(HoraPlantaTexto(1, 23));

        Assert.Null((await Context.RegistrosCalidad.SingleAsync()).TurnoId);
    }

    [Fact]
    public async Task Ingesta_MismoIdentificador_NoDuplica()
    {
        var primera = await Service.IngestarAsync(Ingesta(id: "evt-001"), ImagenDe(Png));
        var segunda = await Service.IngestarAsync(Ingesta(id: "evt-001"), ImagenDe(Png));

        Assert.True(segunda.IsSuccess);
        Assert.True(segunda.Value!.Duplicado);
        Assert.Equal(primera.Value!.RegistroCalidadId, segunda.Value.RegistroCalidadId);
        Assert.Equal(1, await Context.RegistrosCalidad.CountAsync());
        Assert.Single(Directory.GetFiles(Carpeta));
    }

    [Fact]
    public async Task Ingesta_SinImagen_SeRechaza()
    {
        var resultado = await Service.IngestarAsync(Ingesta(), null);

        Assert.Equal("VALIDATION", resultado.ErrorCode);
        Assert.Contains("imagen", resultado.ErrorMessage);
    }

    [Fact]
    public async Task Ingesta_ImagenFalsa_SeRechazaYNoQuedaNadaGuardado()
    {
        var falso = Encoding.ASCII.GetBytes("<html>no soy una imagen</html>");
        var resultado = await Service.IngestarAsync(Ingesta(), ImagenDe(falso, "frame.png"));

        Assert.Equal("VALIDATION", resultado.ErrorCode);
        Assert.Empty(Context.RegistrosCalidad);
        Assert.False(Directory.Exists(Carpeta) && Directory.EnumerateFiles(Carpeta).Any());
    }

    [Fact]
    public async Task Ingesta_PngConNombreJpg_SeGuardaConElTipoReal()
    {
        await Service.IngestarAsync(Ingesta(), ImagenDe(Png, "frame.jpg"));

        var registro = await Context.RegistrosCalidad.SingleAsync();
        Assert.Equal("image/png", registro.ImagenContentType);
        Assert.EndsWith(".png", registro.ImagenNombreArchivo);
    }

    [Theory]
    [InlineData("NO-EXISTE", "TEST-RABO")]
    [InlineData("TEST-CAM-T2", "NO-EXISTE")]
    public async Task Ingesta_CodigosDesconocidos_SeRechazan(string punto, string tipo)
    {
        var resultado = await Service.IngestarAsync(Ingesta(punto: punto, tipo: tipo), ImagenDe(Png));

        Assert.Equal("VALIDATION", resultado.ErrorCode);
        Assert.Empty(Context.RegistrosCalidad);
    }

    [Fact]
    public async Task Ingesta_PuntoInactivo_SeRechaza()
    {
        var punto = await Context.PuntosControlCalidad.SingleAsync(p => p.Codigo == "TEST-CAM-T2");
        punto.Activo = false;
        await Context.SaveChangesAsync();

        var resultado = await Service.IngestarAsync(Ingesta(), ImagenDe(Png));

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.2")]
    [InlineData("abc")]
    public async Task Ingesta_ConfianzaInvalida_SeRechaza(string confianza)
    {
        var pedido = Ingesta();
        pedido.Confianza = confianza;

        var resultado = await Service.IngestarAsync(pedido, ImagenDe(Png));

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Fact]
    public async Task Ingesta_ConfianzaConComa_SeAcepta()
    {
        var pedido = Ingesta();
        pedido.Confianza = "0,92";

        var resultado = await Service.IngestarAsync(pedido, ImagenDe(Png));

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(0.92m, (await Context.RegistrosCalidad.SingleAsync()).Confianza);
    }

    [Fact]
    public async Task Ingesta_FechaFutura_SeRechaza()
    {
        var resultado = await Service.IngestarAsync(Ingesta(HoraPlantaTexto(-2, 10)), ImagenDe(Png));

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Fact]
    public async Task Ingesta_ConProductoYDetencion_LosRelaciona()
    {
        var pedido = Ingesta();
        pedido.Producto = "SKU-TEST";
        pedido.CodigoDetencion = "det-20261020-000001";

        var resultado = await Service.IngestarAsync(pedido, ImagenDe(Png));

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var registro = await Context.RegistrosCalidad.SingleAsync();
        Assert.Equal(Producto.ProductoId, registro.ProductoId);
        Assert.Equal(DetencionTango.DetencionId, registro.DetencionId);
    }

    [Fact]
    public async Task Ingesta_DetencionDeOtraLinea_SeRechaza()
    {
        var pedido = Ingesta(punto: "TEST-CAM-F2");
        pedido.CodigoDetencion = DetencionTango.Codigo;

        var resultado = await Service.IngestarAsync(pedido, ImagenDe(Png));

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    // ===================== Carga manual =====================

    private CrearRegistroCalidadManualRequest Manual() => new()
    {
        PuntoControlCalidadId = CamaraTango.PuntoControlCalidadId,
        TipoDefectoCalidadId = Mancha.TipoDefectoCalidadId,
        FechaHora = HoraPlantaTexto(1, 7),
        Observaciones = "Visto en la salida"
    };

    [Theory]
    [InlineData("SUPERVISOR")]
    [InlineData("ADMINISTRADOR")]
    public async Task Manual_SupervisorYAdmin_CarganYNaceConfirmado(string rol)
    {
        var resultado = await Service.CrearManualAsync(Manual(), null, Supervisor, [rol]);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var dto = resultado.Value!;
        Assert.Equal("MANUAL", dto.Origen);
        Assert.Equal("CONFIRMADO", dto.EstadoRevision);
        Assert.Equal(Supervisor, dto.RevisadoPorUsuarioId);
        Assert.False(dto.TieneImagen);
        var historial = Assert.Single(dto.Revisiones);
        Assert.Equal("PENDIENTE", historial.EstadoAnterior);
        Assert.Equal("CONFIRMADO", historial.EstadoNuevo);
    }

    [Theory]
    [InlineData("OPERARIO")]
    [InlineData("MANTENIMIENTO")]
    [InlineData("GERENCIA")]
    public async Task Manual_OtrosRoles_Prohibido(string rol)
    {
        var resultado = await Service.CrearManualAsync(Manual(), null, Operario, [rol]);

        Assert.Equal("FORBIDDEN", resultado.ErrorCode);
        Assert.Empty(Context.RegistrosCalidad);
    }

    [Fact]
    public async Task Manual_ConImagenYTurnoElegido_RespetaElTurno()
    {
        var pedido = Manual();
        pedido.TurnoId = TurnoTarde.TurnoId;
        pedido.ValorMedido = "3";
        pedido.UnidadMedida = "paquetes";

        var resultado = await Service.CrearManualAsync(pedido, ImagenDe(Jpeg, "foto.jpg"), Admin, ["ADMINISTRADOR"]);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(TurnoTarde.TurnoId, resultado.Value!.TurnoId);
        Assert.Equal("paquetes", resultado.Value.UnidadMedida);
        Assert.True(resultado.Value.TieneImagen);

        var imagen = await Service.AbrirImagenAsync(resultado.Value.RegistroCalidadId);
        Assert.True(imagen.IsSuccess);
        await using var stream = imagen.Value!.Contenido;
        Assert.Equal("image/jpeg", imagen.Value.ContentType);
    }

    [Fact]
    public async Task Imagen_RegistroSinImagen_NoEncontrada()
    {
        var creado = await Service.CrearManualAsync(Manual(), null, Admin, ["ADMINISTRADOR"]);

        var imagen = await Service.AbrirImagenAsync(creado.Value!.RegistroCalidadId);

        Assert.Equal("NOT_FOUND", imagen.ErrorCode);
    }

    // ===================== Revisión =====================

    private async Task<RegistroCalidadDto> DetalleAsync(long id)
    {
        Context.ChangeTracker.Clear();
        return (await Service.ObtenerAsync(id)).Value!;
    }

    [Theory]
    [InlineData("SUPERVISOR")]
    [InlineData("MANTENIMIENTO")]
    [InlineData("ADMINISTRADOR")]
    public async Task Revision_RolesHabilitados_Confirman(string rol)
    {
        var id = await IngestarAsync();
        var detalle = await DetalleAsync(id);

        var resultado = await Service.RevisarAsync(id,
            new RevisarRegistroCalidadRequest { Estado = "CONFIRMADO", RowVersion = detalle.RowVersion }, Mantenimiento, [rol]);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal("CONFIRMADO", resultado.Value!.EstadoRevision);
        Assert.Equal(Mantenimiento, resultado.Value.RevisadoPorUsuarioId);
        Assert.NotNull(resultado.Value.FechaHoraRevision);
    }

    [Theory]
    [InlineData("OPERARIO")]
    [InlineData("GERENCIA")]
    public async Task Revision_OperarioYGerencia_SoloVen(string rol)
    {
        var id = await IngestarAsync();
        var detalle = await DetalleAsync(id);

        var resultado = await Service.RevisarAsync(id,
            new RevisarRegistroCalidadRequest { Estado = "CONFIRMADO", RowVersion = detalle.RowVersion }, Operario, [rol]);

        Assert.Equal("FORBIDDEN", resultado.ErrorCode);
        Assert.Equal("PENDIENTE", (await DetalleAsync(id)).EstadoRevision);
    }

    [Fact]
    public async Task Revision_FalsoPositivoYCorreccion_QuedanEnElHistorial()
    {
        var id = await IngestarAsync();
        var detalle = await DetalleAsync(id);

        var primera = await Service.RevisarAsync(id, new RevisarRegistroCalidadRequest
        {
            Estado = "FALSO_POSITIVO", Comentario = "Reflejo de la luz", RowVersion = detalle.RowVersion
        }, Supervisor, ["SUPERVISOR"]);
        Assert.True(primera.IsSuccess, primera.ErrorMessage);

        detalle = await DetalleAsync(id);
        var segunda = await Service.RevisarAsync(id, new RevisarRegistroCalidadRequest
        {
            Estado = "CONFIRMADO", Comentario = "Mirando de nuevo, era grasa", RowVersion = detalle.RowVersion
        }, Admin, ["ADMINISTRADOR"]);
        Assert.True(segunda.IsSuccess, segunda.ErrorMessage);

        var final = await DetalleAsync(id);
        Assert.Equal("CONFIRMADO", final.EstadoRevision);
        Assert.Equal(Admin, final.RevisadoPorUsuarioId);
        Assert.Equal(2, final.Revisiones.Count);
        // Del más reciente al más viejo
        Assert.Equal(("FALSO_POSITIVO", "CONFIRMADO", Admin), (final.Revisiones[0].EstadoAnterior, final.Revisiones[0].EstadoNuevo, final.Revisiones[0].UsuarioId));
        Assert.Equal(("PENDIENTE", "FALSO_POSITIVO", Supervisor), (final.Revisiones[1].EstadoAnterior, final.Revisiones[1].EstadoNuevo, final.Revisiones[1].UsuarioId));
        Assert.Equal("Reflejo de la luz", final.Revisiones[1].Comentario);
    }

    [Fact]
    public async Task Revision_MismoEstado_SeRechaza()
    {
        var id = await IngestarAsync();
        var detalle = await DetalleAsync(id);
        await Service.RevisarAsync(id, new RevisarRegistroCalidadRequest { Estado = "CONFIRMADO", RowVersion = detalle.RowVersion }, Supervisor, ["SUPERVISOR"]);

        detalle = await DetalleAsync(id);
        var resultado = await Service.RevisarAsync(id,
            new RevisarRegistroCalidadRequest { Estado = "CONFIRMADO", RowVersion = detalle.RowVersion }, Supervisor, ["SUPERVISOR"]);

        Assert.Equal("VALIDATION", resultado.ErrorCode);
        Assert.Single((await DetalleAsync(id)).Revisiones);
    }

    [Theory]
    [InlineData("PENDIENTE")]
    [InlineData("CUALQUIERA")]
    [InlineData("")]
    public async Task Revision_EstadoInvalido_SeRechaza(string estado)
    {
        var id = await IngestarAsync();
        var detalle = await DetalleAsync(id);

        var resultado = await Service.RevisarAsync(id,
            new RevisarRegistroCalidadRequest { Estado = estado, RowVersion = detalle.RowVersion }, Supervisor, ["SUPERVISOR"]);

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Fact]
    public async Task Revision_ConVersionVieja_Conflicto()
    {
        var id = await IngestarAsync();
        var vieja = (await DetalleAsync(id)).RowVersion;
        await Service.RevisarAsync(id, new RevisarRegistroCalidadRequest { Estado = "CONFIRMADO", RowVersion = vieja }, Supervisor, ["SUPERVISOR"]);

        Context.ChangeTracker.Clear();
        var resultado = await Service.RevisarAsync(id,
            new RevisarRegistroCalidadRequest { Estado = "FALSO_POSITIVO", RowVersion = vieja }, Admin, ["ADMINISTRADOR"]);

        Assert.Equal("CONCURRENCY", resultado.ErrorCode);
    }

    [Fact]
    public async Task VincularDetencion_YDesvincular()
    {
        var id = await IngestarAsync();

        var vinculado = await Service.VincularDetencionAsync(id,
            new VincularDetencionCalidadRequest { CodigoDetencion = DetencionTango.Codigo }, Supervisor, ["SUPERVISOR"]);
        Assert.True(vinculado.IsSuccess, vinculado.ErrorMessage);
        Assert.Equal(DetencionTango.Codigo, vinculado.Value!.DetencionCodigo);

        var operario = await Service.VincularDetencionAsync(id,
            new VincularDetencionCalidadRequest { CodigoDetencion = null }, Operario, ["OPERARIO"]);
        Assert.Equal("FORBIDDEN", operario.ErrorCode);

        var desvinculado = await Service.VincularDetencionAsync(id,
            new VincularDetencionCalidadRequest { CodigoDetencion = " " }, Supervisor, ["SUPERVISOR"]);
        Assert.True(desvinculado.IsSuccess);
        Assert.Null((await DetalleAsync(id)).DetencionId);
    }

    // ===================== Filtros =====================

    [Fact]
    public async Task Filtros_PorFechaTurnoLineaPuntoDefectoYEstado()
    {
        var ayerManana = await IngestarAsync(HoraPlantaTexto(1, 9));
        var ayerTarde = await IngestarAsync(HoraPlantaTexto(1, 16), tipo: "TEST-MANCHA");
        var haceTresDias = await IngestarAsync(HoraPlantaTexto(3, 9));
        var futura = await IngestarAsync(HoraPlantaTexto(1, 10), punto: "TEST-CAM-F2");

        var detalle = await DetalleAsync(ayerTarde);
        await Service.RevisarAsync(ayerTarde,
            new RevisarRegistroCalidadRequest { Estado = "FALSO_POSITIVO", RowVersion = detalle.RowVersion }, Supervisor, ["SUPERVISOR"]);
        Context.ChangeTracker.Clear();

        async Task<long[]> Ids(RegistrosCalidadFiltroRequest f)
        {
            var r = await Service.ObtenerPaginadoAsync(f);
            Assert.True(r.IsSuccess, r.ErrorMessage);
            return r.Value!.Items.Select(i => i.RegistroCalidadId).ToArray();
        }

        // Todos, del más reciente al más viejo
        Assert.Equal(new[] { ayerTarde, futura, ayerManana, haceTresDias }, await Ids(new RegistrosCalidadFiltroRequest()));

        // Fecha (días de planta, inclusive)
        var ayer = DiaPlanta(1);
        Assert.Equal(new[] { ayerTarde, futura, ayerManana }, await Ids(new RegistrosCalidadFiltroRequest { FechaDesde = ayer, FechaHasta = ayer }));
        Assert.Equal(new[] { haceTresDias }, await Ids(new RegistrosCalidadFiltroRequest { FechaHasta = DiaPlanta(2) }));

        Assert.Equal(new[] { ayerTarde }, await Ids(new RegistrosCalidadFiltroRequest { TurnoId = TurnoTarde.TurnoId }));
        Assert.Equal(new[] { futura }, await Ids(new RegistrosCalidadFiltroRequest { LineaId = Futura2.LineaId }));
        Assert.Equal(new[] { futura }, await Ids(new RegistrosCalidadFiltroRequest { PuntoControlCalidadId = CamaraFutura.PuntoControlCalidadId }));
        Assert.Equal(new[] { ayerTarde }, await Ids(new RegistrosCalidadFiltroRequest { TipoDefectoCalidadId = Mancha.TipoDefectoCalidadId }));
        Assert.Equal(new[] { ayerTarde }, await Ids(new RegistrosCalidadFiltroRequest { EstadoRevision = "falso_positivo" }));
        Assert.Equal(new[] { futura, ayerManana, haceTresDias }, await Ids(new RegistrosCalidadFiltroRequest { EstadoRevision = "PENDIENTE" }));
        Assert.Empty(await Ids(new RegistrosCalidadFiltroRequest { Origen = "MANUAL" }));

        // Combinados
        Assert.Equal(new[] { ayerManana }, await Ids(new RegistrosCalidadFiltroRequest
        {
            LineaId = Tango2.LineaId, TurnoId = TurnoManana.TurnoId, FechaDesde = ayer, FechaHasta = ayer
        }));
    }

    [Fact]
    public async Task Filtros_Paginado()
    {
        for (var i = 0; i < 5; i++)
            await IngestarAsync(HoraPlantaTexto(1, 8 + i));

        var pagina = await Service.ObtenerPaginadoAsync(new RegistrosCalidadFiltroRequest { Page = 2, PageSize = 2 });

        Assert.Equal(5, pagina.Value!.TotalCount);
        Assert.Equal(2, pagina.Value.Items.Count);
    }

    [Theory]
    [InlineData("NO_EXISTE", null)]
    [InlineData(null, "OTRO")]
    public async Task Filtros_ValoresInvalidos_SeRechazan(string? estado, string? origen)
    {
        var resultado = await Service.ObtenerPaginadoAsync(new RegistrosCalidadFiltroRequest { EstadoRevision = estado, Origen = origen });

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    // ===================== Maestros =====================

    [Fact]
    public async Task Maestros_SoloAdministrador_YCodigoUnico()
    {
        var pedido = new GuardarTipoDefectoCalidadRequest { Codigo = "test-nuevo", Nombre = "Nuevo", UnidadMedida = "mm" };

        Assert.Equal("FORBIDDEN", (await Maestros.CrearTipoDefectoAsync(pedido, Supervisor, ["SUPERVISOR"])).ErrorCode);

        var creado = await Maestros.CrearTipoDefectoAsync(pedido, Admin, ["ADMINISTRADOR"]);
        Assert.True(creado.IsSuccess, creado.ErrorMessage);
        Assert.Equal("TEST-NUEVO", creado.Value!.Codigo);

        var repetido = await Maestros.CrearTipoDefectoAsync(pedido, Admin, ["ADMINISTRADOR"]);
        Assert.Equal("VALIDATION", repetido.ErrorCode);
    }

    [Fact]
    public async Task Maestros_PuntoConEspaciosEnElCodigo_SeRechaza()
    {
        var resultado = await Maestros.CrearPuntoControlAsync(new GuardarPuntoControlCalidadRequest
        {
            LineaId = Tango2.LineaId, Codigo = "CAM 2", Nombre = "Cámara 2"
        }, Admin, ["ADMINISTRADOR"]);

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Fact]
    public async Task Maestros_Desactivar_NoListaSalvoQueSePida()
    {
        var dto = await Maestros.ActualizarTipoDefectoAsync(Mancha.TipoDefectoCalidadId, new GuardarTipoDefectoCalidadRequest
        {
            Codigo = Mancha.Codigo, Nombre = Mancha.Nombre, Activo = false
        }, Admin, ["ADMINISTRADOR"]);
        Assert.True(dto.IsSuccess, dto.ErrorMessage);

        Assert.DoesNotContain(await Maestros.ObtenerTiposDefectoAsync(false), t => t.TipoDefectoCalidadId == Mancha.TipoDefectoCalidadId);
        Assert.Contains(await Maestros.ObtenerTiposDefectoAsync(true), t => t.TipoDefectoCalidadId == Mancha.TipoDefectoCalidadId);
    }
}

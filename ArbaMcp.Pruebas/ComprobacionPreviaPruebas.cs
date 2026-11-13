using System.Collections.Generic;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>Cuándo una llamada lleva comprobación previa por simulación (1.10.1).</summary>
    public class ComprobacionPreviaPruebas
    {
        private static JsonElement A(string json) => JsonSerializer.Deserialize<JsonElement>(json);

        private static DescripcionHerramienta Escritura(string nombre = "crear_rasante") => new DescripcionHerramienta
        {
            Nombre = nombre,
            Parametros = new List<Parametro>
            {
                new Parametro { name = "nombre", type = "string", required = true },
                new Parametro { name = "simular", type = "boolean" },
                Catalogo.ParametroPedido()
            }
        };

        private static DescripcionHerramienta SinSimular(string nombre = "listar_lineas_muestreo") => new DescripcionHerramienta
        {
            Nombre = nombre,
            Parametros = new List<Parametro> { new Parametro { name = "fuentes", type = "boolean" }, Catalogo.ParametroPedido() }
        };

        [Fact]
        public void EscrituraRealDeHerramientaConSimular()
            => Assert.True(ComprobacionPrevia.Aplica(Escritura(), ContextoEjecucion.Documento, false, A("{\"nombre\":\"Rasante 1\"}")));

        [Theory]
        [InlineData("{\"nombre\":\"Rasante 1\",\"simular\":false}")]
        [InlineData("{\"nombre\":\"Rasante 1\",\"simular\":\"no\"}")]
        [InlineData("{}")]
        public void SimularFalsoOAusenteEsEscrituraReal(string json)
            => Assert.True(ComprobacionPrevia.Aplica(Escritura(), ContextoEjecucion.Documento, false, A(json)));

        [Fact]
        public void SinArgumentosTambienAplica()
            => Assert.True(ComprobacionPrevia.Aplica(Escritura(), ContextoEjecucion.Documento, false, default));

        [Fact]
        public void UnaSimulacionNoSeComprueba()
            => Assert.False(ComprobacionPrevia.Aplica(Escritura(), ContextoEjecucion.Documento, false, A("{\"simular\":true}")));

        [Theory]
        [InlineData(ContextoEjecucion.Aplicacion)]
        [InlineData(ContextoEjecucion.Inmediato)]
        public void SoloElContextoDocumento(ContextoEjecucion contexto)
            => Assert.False(ComprobacionPrevia.Aplica(Escritura(), contexto, false, A("{}")));

        [Fact]
        public void LasAsincronasNo()
            => Assert.False(ComprobacionPrevia.Aplica(Escritura("exportar_landxml"), ContextoEjecucion.Documento, true, A("{}")));

        [Fact]
        public void SinParametroSimularNo()
        {
            Assert.False(ComprobacionPrevia.DeclaraSimular(SinSimular()));
            Assert.False(ComprobacionPrevia.Aplica(SinSimular(), ContextoEjecucion.Documento, false, A("{\"fuentes\":true}")));
        }

        [Fact]
        public void SimularDeOtroTipoNoCuenta()
        {
            var h = new DescripcionHerramienta { Nombre = "x", Parametros = new List<Parametro> { new Parametro { name = "simular", type = "string" } } };
            Assert.False(ComprobacionPrevia.DeclaraSimular(h));
            Assert.False(ComprobacionPrevia.DeclaraSimular(null));
            Assert.False(ComprobacionPrevia.DeclaraSimular(new DescripcionHerramienta { Nombre = "y", Parametros = null }));
        }
    }
}

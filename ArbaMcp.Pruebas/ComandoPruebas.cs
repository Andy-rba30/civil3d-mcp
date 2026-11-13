using System;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>Texto que ejecutar_comando envía a la línea de comandos (1.10.1: se conservan los espacios finales).</summary>
    public class ComandoPruebas
    {
        private const string Polilinea = "_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ";

        [Fact]
        public void PrepararConservaLosEspaciosFinales()
        {
            Assert.Equal(Polilinea, Comando.Preparar(Polilinea));
            Assert.Equal("_.-LAYER _M EJE_PRUEBA\n\n", Comando.Preparar("_.-LAYER _M EJE_PRUEBA\n\n"));
        }

        [Theory]
        [InlineData("  REGEN", "REGEN")]
        [InlineData("\n\t _.ZOOM E", "_.ZOOM E")]
        [InlineData(" " + Polilinea, Polilinea)]
        public void PrepararQuitaLosEspaciosDelPrincipio(string texto, string esperado) => Assert.Equal(esperado, Comando.Preparar(texto));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(" \n\t ")]
        public void PrepararRechazaElComandoVacio(string texto)
        {
            var ex = Assert.Throws<ArgumentException>(() => Comando.Preparar(texto));
            Assert.Equal("Falta el parámetro obligatorio 'comando'.", ex.Message);
        }

        [Theory]
        [InlineData(Polilinea, "PLINE")]
        [InlineData("_.ZOOM E", "ZOOM")]
        [InlineData("REGEN", "REGEN")]
        [InlineData("regen  ", "REGEN")]
        [InlineData("_LINE\n0,0", "LINE")]
        [InlineData("_.-LAYER _M EJE_PRUEBA\n\n", "-LAYER")]
        [InlineData("_.LANDXMLOUT\nC:\\Proyectos\\prueba.xml", "LANDXMLOUT")]
        [InlineData("  _.UNDO 1", "UNDO")]
        public void NombreEsLaPrimeraPalabraSinPrefijos(string comando, string esperado) => Assert.Equal(esperado, Comando.Nombre(comando));

        [Fact]
        public void PolilineaConDosEspaciosFinalesSeEnviaTalCualSinEnterDeMas()
        {
            string enviado = Comando.TextoAEnviar(Polilinea, false);
            Assert.Equal(Polilinea, enviado);
            Assert.EndsWith("2300  ", enviado);
            Assert.DoesNotContain("\n", enviado);
        }

        [Theory]
        [InlineData("REGEN", "REGEN\n")]
        [InlineData("_.ZOOM E", "_.ZOOM E\n")]
        [InlineData("_.UNDO 1", "_.UNDO 1\n")]
        [InlineData("_.LANDXMLOUT\nC:\\Proyectos\\prueba.xml", "_.LANDXMLOUT\nC:\\Proyectos\\prueba.xml\n")]
        public void SinEspacioFinalSeAnadeUnEnterComoAntes(string comando, string esperado) => Assert.Equal(esperado, Comando.TextoAEnviar(comando, false));

        [Theory]
        [InlineData("REGEN ", "REGEN ")]
        [InlineData("REGEN\n", "REGEN\n")]
        [InlineData("REGEN\r\n", "REGEN\r\n")]
        [InlineData("_.-LAYER _M EJE_PRUEBA\n\n", "_.-LAYER _M EJE_PRUEBA\n\n")]
        public void ConEspacioOSaltoFinalNoSeAnadeNada(string comando, string esperado) => Assert.Equal(esperado, Comando.TextoAEnviar(comando, false));

        [Fact]
        public void ConUndoElGrupoVaDelante()
        {
            Assert.Equal("_.UNDO _BE\nREGEN\n", Comando.TextoAEnviar("REGEN", true));
            Assert.Equal("_.UNDO _BE\n" + Polilinea, Comando.TextoAEnviar(Polilinea, true));
        }

        [Theory]
        [InlineData("REGEN", false)]
        [InlineData("REGEN ", true)]
        [InlineData("REGEN\n", true)]
        [InlineData("REGEN\r", true)]
        [InlineData("REGEN\t", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void TerminaConEnter(string comando, bool esperado) => Assert.Equal(esperado, Comando.TerminaConEnter(comando));
    }
}

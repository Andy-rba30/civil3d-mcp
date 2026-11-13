using System;
using System.Collections.Generic;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Lógica de crear_superficies_corredor (1.12.0) que no depende de Civil 3D: nombres por defecto, nombres libres,
    /// códigos de vínculo del corredor, valores de enumerado por nombre (sin adivinar), texto de la acción, estado
    /// esperado y avisos de la relectura. Nombres genéricos (Corredor 1, Terreno); ningún dato real.
    /// </summary>
    public class SuperficiesCorredorPruebas
    {
        private static readonly string[] CodigosCorredor = { "Datum", "Daylight", "Daylight_Cut", "Daylight_Fill", "Pave", "Top" };
        private static readonly string[] ValoresVoladizo = { "None", "TopLinks", "BottomLinks" };

        private static Dictionary<string, string> Ocupados(params (string nombre, string corredor)[] l)
        {
            var d = new Dictionary<string, string>();
            foreach (var (n, c) in l) d[n] = c;
            return d;
        }

        // ---------------------------------------------------------------- nombres
        [Fact]
        public void NombresPorDefectoYPedidos()
        {
            Assert.Equal("Corredor 1 - Top", SuperficiesCorredor.NombreTop("Corredor 1", null));
            Assert.Equal("Corredor 1 - Datum", SuperficiesCorredor.NombreDatum("Corredor 1", "  "));
            Assert.Equal("Corredor 1 - Top", SuperficiesCorredor.NombreTop(" Corredor 1 ", ""));
            Assert.Equal("Rasante C1", SuperficiesCorredor.NombreTop("Corredor 1", " Rasante C1 "));
            Assert.Equal("Subrasante C1", SuperficiesCorredor.NombreDatum("Corredor 1", "Subrasante C1"));
        }

        [Fact]
        public void NombresLibresNoLanzan()
        {
            SuperficiesCorredor.ValidarNombres("Corredor 1 - Top", "Corredor 1 - Datum", Ocupados(("Terreno", null)));
            SuperficiesCorredor.ValidarNombres("Corredor 1 - Top", "Corredor 1 - Datum", null);
        }

        [Fact]
        public void NombresIgualesSinDistinguirMayusculasLanzan()
        {
            var ex = Assert.Throws<ArgumentException>(() => SuperficiesCorredor.ValidarNombres("Superficie C1", " superficie c1 ", null));
            Assert.Equal("'nombre_top' y 'nombre_datum' no pueden ser iguales ('Superficie C1'): pasa dos nombres distintos.", ex.Message);
        }

        [Fact]
        public void NombreVacioLanza()
        {
            var ex = Assert.Throws<ArgumentException>(() => SuperficiesCorredor.ValidarNombres("Corredor 1 - Top", "  ", null));
            Assert.Contains("no pueden quedar vacíos", ex.Message);
        }

        [Fact]
        public void NombreUsadoPorUnaSuperficieDelDibujoLanza()
        {
            var ex = Assert.Throws<ArgumentException>(() => SuperficiesCorredor.ValidarNombres("Terreno", "Corredor 1 - Datum", Ocupados(("Terreno", null))));
            Assert.Equal("Ya existe la superficie 'Terreno'. Usa listar_superficies o estado_corredor, o pasa otro 'nombre_top'.", ex.Message);
        }

        [Fact]
        public void NombreUsadoPorUnCorredorLanzaConElCorredor()
        {
            var ocupados = Ocupados(("Terreno", null), ("Corredor 1 - Top", "Corredor 1"), ("Corredor 1 - Datum", "Corredor 1"));
            var ex = Assert.Throws<ArgumentException>(() => SuperficiesCorredor.ValidarNombres("corredor 1 - top", "Otro", ocupados));
            Assert.Equal("Ya existe la superficie 'corredor 1 - top' (la genera el corredor 'Corredor 1'). Usa listar_superficies o estado_corredor, o pasa otro 'nombre_top'.", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => SuperficiesCorredor.ValidarNombres("Otro", "Corredor 1 - Datum", ocupados));
            Assert.EndsWith("o pasa otro 'nombre_datum'.", ex.Message);
        }

        // ---------------------------------------------------------------- códigos de vínculo
        [Fact]
        public void CodigoDelCorredorDevuelveSuGrafia()
        {
            Assert.Equal("Top", SuperficiesCorredor.CodigoDelCorredor(CodigosCorredor, "Top", "Corredor 1", "Corredor 1 - Top"));
            Assert.Equal("Datum", SuperficiesCorredor.CodigoDelCorredor(CodigosCorredor, "Datum", "Corredor 1", "Corredor 1 - Datum"));
            Assert.Equal("TOP", SuperficiesCorredor.CodigoDelCorredor(new[] { "TOP", "DATUM" }, "Top", "Corredor 1", "Corredor 1 - Top"));
        }

        [Fact]
        public void CodigoQueFaltaLanzaConLaLista()
        {
            var ex = Assert.Throws<ArgumentException>(() => SuperficiesCorredor.CodigoDelCorredor(new[] { "Top", "Pave", "", null }, "Datum", "Corredor 1", "Corredor 1 - Datum"));
            Assert.Equal("El corredor 'Corredor 1' no tiene el código de vínculo 'Datum', necesario para la superficie 'Corredor 1 - Datum'. Códigos de vínculo del corredor: Top, Pave. Revisa los subensamblajes de su ensamblaje con listar_ensamblajes (los carriles y taludes de Civil 3D traen Top y Datum).", ex.Message);
        }

        [Fact]
        public void CorredorSinCodigosPideReconstruir()
        {
            var ex = Assert.Throws<ArgumentException>(() => SuperficiesCorredor.CodigoDelCorredor(new string[0], "Top", "Corredor 1", "Corredor 1 - Top"));
            Assert.Contains("no devuelve ningún código de vínculo", ex.Message);
            Assert.Contains("reconstruir_corredor", ex.Message);
            Assert.Throws<ArgumentException>(() => SuperficiesCorredor.CodigoDelCorredor(null, "Top", "Corredor 1", "Corredor 1 - Top"));
        }

        // ---------------------------------------------------------------- enumerados
        [Fact]
        public void ValorEnumeradoPorNombreSinDistinguirMayusculas()
        {
            Assert.Equal("TopLinks", SuperficiesCorredor.ValorEnumerado(ValoresVoladizo, SuperficiesCorredor.VoladizoTop, SuperficiesCorredor.EnumeradoVoladizo, "superficie Top"));
            Assert.Equal("BottomLinks", SuperficiesCorredor.ValorEnumerado(ValoresVoladizo, SuperficiesCorredor.VoladizoDatum, SuperficiesCorredor.EnumeradoVoladizo, "superficie Datum"));
            Assert.Equal("TOPLINKS", SuperficiesCorredor.ValorEnumerado(new[] { "NONE", "TOPLINKS" }, "TopLinks", "OverhangCorrectionType", "superficie Top"));
        }

        [Fact]
        public void ValorEnumeradoQueNoExisteNoSeAdivina()
        {
            // Un nombre parecido (TopLink) no vale: la herramienta no adivina y da los valores reales
            var ex = Assert.Throws<InvalidOperationException>(() => SuperficiesCorredor.ValorEnumerado(new[] { "None", "TopLink", "BottomLink" }, "TopLinks", "OverhangCorrectionType", "corrección de voladizo de 'Corredor 1 - Top'"));
            Assert.Equal("El enumerado OverhangCorrectionType de esta instalación de Civil 3D no tiene el valor 'TopLinks' (corrección de voladizo de 'Corredor 1 - Top'). Valores: None, TopLink, BottomLink. No se crea nada: anótalo en miembros_por_verificar_civil3d.md para corregir la herramienta.", ex.Message);
            ex = Assert.Throws<InvalidOperationException>(() => SuperficiesCorredor.ValorEnumerado(null, "TopLinks", "OverhangCorrectionType", "x"));
            Assert.Contains("Valores: ninguno.", ex.Message);
        }

        // ---------------------------------------------------------------- acción
        [Fact]
        public void TextoDeLaAccion()
        {
            Assert.Equal("Crear en el corredor 'Corredor 1' las superficies 'Corredor 1 - Top' (código de vínculo Top, voladizo por los vínculos superiores) y 'Corredor 1 - Datum' (código de vínculo Datum, voladizo por los vínculos inferiores), como líneas de rotura y con el contorno de la extensión del corredor, y reconstruirlo",
                SuperficiesCorredor.Accion("Corredor 1", "Corredor 1 - Top", "Corredor 1 - Datum"));
        }

        // ---------------------------------------------------------------- estado
        [Fact]
        public void EstadoDeUnaSuperficieQueNoExiste()
        {
            var d = new Dictionary<string, object>();
            SuperficiesCorredor.AnadirEstado(d, "top", false, new[] { "Top" }, new[] { "Top" }, "TopLinks", new[] { "X" });
            Assert.Equal(false, d["top_existe"]);
            Assert.Null(d["top_codigos_enlace"]);
            Assert.Null(d["top_codigos_rotura"]);
            Assert.Null(d["top_voladizo"]);
            Assert.Null(d["top_contornos"]);
        }

        [Fact]
        public void EstadoDeUnaSuperficieQueExisteOrdenaLasListas()
        {
            var d = new Dictionary<string, object>();
            SuperficiesCorredor.AnadirEstado(d, "datum", true, new[] { "Pave", "Datum" }, new[] { "Datum" }, "BottomLinks", null);
            Assert.Equal(true, d["datum_existe"]);
            Assert.Equal(new List<string> { "Datum", "Pave" }, d["datum_codigos_enlace"]);
            Assert.Equal(new List<string> { "Datum" }, d["datum_codigos_rotura"]);
            Assert.Equal("BottomLinks", d["datum_voladizo"]);
            Assert.Null(d["datum_contornos"]);   // no se pudo leer
        }

        [Fact]
        public void EsperadoTieneLasDosSuperficiesConSusCodigosVoladizoYContorno()
        {
            var e = SuperficiesCorredor.Esperado(new[] { "Superficie previa" }, "Corredor 1 - Top", "Corredor 1 - Datum", "Top", "Datum", "TopLinks", "BottomLinks");
            Assert.Equal(new List<string> { "Corredor 1 - Datum", "Corredor 1 - Top", "Superficie previa" }, e["superficies"]);
            Assert.Equal(true, e["top_existe"]);
            Assert.Equal(new List<string> { "Top" }, e["top_codigos_enlace"]);
            Assert.Equal(new List<string> { "Top" }, e["top_codigos_rotura"]);
            Assert.Equal("TopLinks", e["top_voladizo"]);
            Assert.Equal(new List<string> { "Extensión del corredor" }, e["top_contornos"]);
            Assert.Equal(true, e["datum_existe"]);
            Assert.Equal(new List<string> { "Datum" }, e["datum_codigos_enlace"]);
            Assert.Equal(new List<string> { "Datum" }, e["datum_codigos_rotura"]);
            Assert.Equal("BottomLinks", e["datum_voladizo"]);
            Assert.Equal(new List<string> { "Extensión del corredor" }, e["datum_contornos"]);
            Assert.Equal(false, e["esta_desactualizado"]);
        }

        [Fact]
        public void EsperadoFrenteALoReleidoConVerificacion()
        {
            // Lo que leería el plugin después de crear (antes sin superficies) cuadra con lo esperado
            var esperado = SuperficiesCorredor.Esperado(new string[0], "Corredor 1 - Top", "Corredor 1 - Datum", "Top", "Datum", "TopLinks", "BottomLinks");
            var antes = new Dictionary<string, object> { ["superficies"] = new List<string>(), ["esta_desactualizado"] = false };
            SuperficiesCorredor.AnadirEstado(antes, "top", false, null, null, null, null);
            SuperficiesCorredor.AnadirEstado(antes, "datum", false, null, null, null, null);
            var despues = new Dictionary<string, object> { ["superficies"] = new List<string> { "Corredor 1 - Datum", "Corredor 1 - Top" }, ["esta_desactualizado"] = false };
            SuperficiesCorredor.AnadirEstado(despues, "top", true, new[] { "Top" }, new[] { "Top" }, "TopLinks", new[] { "Extensión del corredor" });
            SuperficiesCorredor.AnadirEstado(despues, "datum", true, new[] { "Datum" }, new[] { "Datum" }, "BottomLinks", new[] { "Extensión del corredor" });
            Verificacion.ComprobarEsperado(antes, despues, esperado, null, null);   // no lanza

            // Un voladizo distinto en la Datum es un error sin reintentar
            despues["datum_voladizo"] = "None";
            var ex = Assert.Throws<InvalidOperationException>(() => Verificacion.ComprobarEsperado(antes, despues, esperado, null, null));
            Assert.Contains("'datum_voladizo': se pidió 'BottomLinks' y después de escribir tiene 'None'", ex.Message);

            // Un código que no quedó como línea de rotura, también
            despues["datum_voladizo"] = "BottomLinks";
            despues["top_codigos_rotura"] = new List<string>();
            ex = Assert.Throws<InvalidOperationException>(() => Verificacion.ComprobarEsperado(antes, despues, esperado, null, null));
            Assert.Contains("'top_codigos_rotura'", ex.Message);
        }

        // ---------------------------------------------------------------- avisos de la relectura
        [Fact]
        public void SinAvisosSiTodoQuedoBien()
        {
            Assert.Empty(SuperficiesCorredor.AvisosRelectura("Corredor 1 - Top", "OutsideBoundary", true, true));
            Assert.Empty(SuperficiesCorredor.AvisosRelectura("Corredor 1 - Top", "outsideboundary", null, true));
        }

        [Fact]
        public void AvisosDeContornoIsBuildYSuperficieDelDibujo()
        {
            var a = SuperficiesCorredor.AvisosRelectura("Corredor 1 - Datum", "InsideBoundary", false, false);
            Assert.Equal(3, a.Count);
            Assert.Contains("quedó de tipo 'InsideBoundary', no 'OutsideBoundary'", a[0]);
            Assert.Contains("IsBuild = false", a[1]);
            Assert.Contains("la herramienta no lo cambia", a[1]);
            Assert.Contains("SurfaceId vacío después de reconstruir", a[2]);
            var b = SuperficiesCorredor.AvisosRelectura("Corredor 1 - Datum", null, true, true);
            Assert.Single(b);
            Assert.StartsWith("No se pudo leer el tipo del contorno 'Extensión del corredor'", b[0]);
        }
    
        // ---------------------------------------------------------------- contornos de estado_corredor (1.14.1)
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public void ContornoTraeElCampoNuevoYConservaElObsoleto(bool? esExtension)
        {
            var c = SuperficiesCorredor.Contorno("Extensión del corredor", "OutsideBoundary", esExtension);
            Assert.Equal("Extensión del corredor", c["nombre"]);
            Assert.Equal("OutsideBoundary", c["tipo"]);
            Assert.True(c.ContainsKey("es_extension_corredor"));
            Assert.True(c.ContainsKey("usar_como_exterior"));        // obsoleto, pero un agente puede depender de él
            Assert.Equal(esExtension, c["es_extension_corredor"]);
            Assert.Equal(c["es_extension_corredor"], c["usar_como_exterior"]);
            Assert.Equal(4, c.Count);
        }
}
}

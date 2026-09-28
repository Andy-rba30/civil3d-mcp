using System;
using System.Collections.Generic;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    public class VerificacionPruebas
    {
        [Fact]
        public void IgualConNumerosDeDistintoTipoYTolerancia()
        {
            Assert.True(Verificacion.Igual(10, 10.0));
            Assert.True(Verificacion.Igual(10.0000001, 10.0));
            Assert.True(Verificacion.Igual(2.5m, 2.5f));
            Assert.False(Verificacion.Igual(10.001, 10.0));
            Assert.False(Verificacion.Igual(10, "10"), "un número y un texto no son iguales");
        }

        [Fact]
        public void IgualConCadenasIgnoraEspaciosExtremosYMayusculas()
        {
            Assert.True(Verificacion.Igual("Terreno", "  terreno "));
            Assert.False(Verificacion.Igual("Terreno natural", "Terreno  natural"), "los espacios interiores cuentan");
            Assert.False(Verificacion.Igual("a", null));
            Assert.True(Verificacion.Igual(null, null));
        }

        [Fact]
        public void IgualConBooleanosYListas()
        {
            Assert.True(Verificacion.Igual(true, true));
            Assert.False(Verificacion.Igual(true, false));
            Assert.True(Verificacion.Igual(new List<object> { "Eje", 1 }, new object[] { " eje", 1.0 }));
            Assert.False(Verificacion.Igual(new List<string> { "a" }, new List<string> { "a", "b" }));
            Assert.False(Verificacion.Igual(new List<string> { "a", "b" }, new List<string> { "b", "a" }), "el orden importa");
            Assert.True(Verificacion.Igual(new List<string>(), new string[0]));
        }

        [Fact]
        public void CambiosYFiltrarDejanSoloLoQueCambio()
        {
            var antes = new Dictionary<string, object> { ["a"] = 1, ["b"] = "x", ["c"] = true };
            var despues = new Dictionary<string, object> { ["a"] = 1, ["b"] = "y", ["d"] = 3 };
            var cambios = Verificacion.Cambios(antes, despues);
            Assert.Equal(new List<string> { "b", "c", "d" }, cambios);
            var f = Verificacion.Filtrar(despues, cambios);
            Assert.Equal(new[] { "b", "d" }, new List<string>(f.Keys).ToArray());
            Assert.Same(antes, Verificacion.Filtrar(antes, new List<string>()));
        }

        [Fact]
        public void ResultadoLanzaSiDespuesNoCoincideConEsperadoYElMensajeLoExplica()
        {
            var antes = new Dictionary<string, object> { ["objetivos"] = new List<string> { "Terreno" } };
            var despues = new Dictionary<string, object> { ["objetivos"] = new List<string> { "Terreno" } };
            var esperado = new Dictionary<string, object> { ["objetivos"] = new List<string> { "Rasante" } };
            var ex = Assert.Throws<InvalidOperationException>(() => Verificacion.Resultado("asignar_objetivo", antes, despues, esperado, copia: "C:\\obra\\backups\\obra_x.dwg"));
            Assert.Contains("El dibujo no refleja el cambio pedido en 'objetivos'", ex.Message);
            Assert.Contains("se pidió ['Rasante'] y después de escribir tiene ['Terreno']", ex.Message);
            Assert.Contains("No se reintenta", ex.Message);
            Assert.Contains("antes={\"objetivos\":[\"Terreno\"]}", ex.Message);
            Assert.Contains("despues={\"objetivos\":[\"Terreno\"]}", ex.Message);
            Assert.Contains("copia=C:\\obra\\backups\\obra_x.dwg", ex.Message);
        }

        [Fact]
        public void ResultadoIncluyeLaRutaDeUnaCopiaEnFormaDeObjeto()
        {
            var antes = new Dictionary<string, object> { ["x"] = 1 };
            var despues = new Dictionary<string, object> { ["x"] = 1 };
            var esperado = new Dictionary<string, object> { ["x"] = 2 };
            var copia = new { ruta = "D:\\b\\copia.dwg", reutilizada = true };
            var ex = Assert.Throws<InvalidOperationException>(() => Verificacion.Resultado("h", antes, despues, esperado, copia: copia));
            Assert.Contains("copia=D:\\b\\copia.dwg", ex.Message);
            Assert.Null(Verificacion.RutaDeCopia(null));
            Assert.Equal("a", Verificacion.RutaDeCopia("a"));
        }

        [Fact]
        public void ResultadoCorrectoTieneElFormatoDeEscritura()
        {
            var antes = new Dictionary<string, object> { ["region"] = "R1", ["ensamblaje"] = "A" };
            var despues = new Dictionary<string, object> { ["region"] = "R1", ["ensamblaje"] = "B" };
            var esperado = new Dictionary<string, object> { ["ensamblaje"] = "b " };
            var r = Verificacion.Resultado("asignar_ensamblaje_region", antes, despues, esperado, "Cambiar", new { ms = 3 }, "copia.dwg", new List<string> { "aviso" });
            var json = JsonSerializer.Deserialize<JsonElement>(Json.Serializar(r));
            Assert.False(json.GetProperty("simulado").GetBoolean());
            Assert.Equal("asignar_ensamblaje_region", json.GetProperty("herramienta").GetString());
            Assert.Equal("Cambiar", json.GetProperty("mensaje").GetString());
            Assert.Equal("ensamblaje", json.GetProperty("cambios")[0].GetString());
            Assert.Equal("A", json.GetProperty("antes").GetProperty("ensamblaje").GetString());
            Assert.Equal("B", json.GetProperty("despues").GetProperty("ensamblaje").GetString());
            Assert.False(json.GetProperty("antes").TryGetProperty("region", out _), "solo los campos que cambiaron");
            Assert.Equal("copia.dwg", json.GetProperty("copia").GetString());
            Assert.Equal(3, json.GetProperty("datos").GetProperty("ms").GetInt32());
            Assert.Equal("aviso", json.GetProperty("avisos")[0].GetString());
        }

        [Fact]
        public void SimulacionAplicaLoEsperadoSobreAntes()
        {
            var antes = new Dictionary<string, object> { ["frecuencia_tangentes"] = 20.0, ["frecuencia_curvas"] = 10.0 };
            var esperado = new Dictionary<string, object> { ["frecuencia_tangentes"] = 10.0 };
            var json = JsonSerializer.Deserialize<JsonElement>(Json.Serializar(Verificacion.Simulacion("establecer_frecuencia", antes, esperado, "Cambiar frecuencias")));
            Assert.True(json.GetProperty("simulado").GetBoolean());
            Assert.Equal("Cambiar frecuencias", json.GetProperty("accion").GetString());
            Assert.Equal(1, json.GetProperty("cambios").GetArrayLength());
            Assert.Equal(20.0, json.GetProperty("antes").GetProperty("frecuencia_tangentes").GetDouble());
            Assert.Equal(10.0, json.GetProperty("despues").GetProperty("frecuencia_tangentes").GetDouble());
            Assert.Equal(JsonValueKind.Null, json.GetProperty("avisos").ValueKind);
        }

        [Fact]
        public void SimulacionSinCambiosDevuelveTodoElEstado()
        {
            var antes = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 };
            var json = JsonSerializer.Deserialize<JsonElement>(Json.Serializar(Verificacion.Simulacion("h", antes, new Dictionary<string, object> { ["a"] = 1 }, "nada")));
            Assert.Equal(0, json.GetProperty("cambios").GetArrayLength());
            Assert.Equal(2, json.GetProperty("antes").GetProperty("b").GetInt32());
        }

        [Fact]
        public void TextoDescribeValores()
        {
            Assert.Equal("null", Verificacion.Texto(null));
            Assert.Equal("'a'", Verificacion.Texto("a"));
            Assert.Equal("[1, 'b']", Verificacion.Texto(new List<object> { 1, "b" }));
            Assert.Equal("2.5", Verificacion.Texto(2.5));
            Assert.Equal("True", Verificacion.Texto(true));
        }
    }
}

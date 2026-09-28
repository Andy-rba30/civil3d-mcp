using System;
using System.Collections.Generic;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    public class LotesPruebas
    {
        private static JsonElement A(string json) => JsonSerializer.Deserialize<JsonElement>(json);

        [Fact]
        public void LeerListaAceptaTextoJsonOArreglo()
        {
            var texto = Lotes.LeerLista(A("{\"asignaciones\":\"[{\\\"region\\\":\\\"0\\\"},{\\\"region\\\":\\\"1\\\"}]\"}"), "asignaciones", false);
            Assert.Equal(2, texto.Count);
            Assert.Equal("1", texto[1].GetProperty("region").GetString());
            var directo = Lotes.LeerLista(A("{\"asignaciones\":[{\"region\":\"0\"}]}"), "asignaciones", false);
            Assert.Single(directo);
        }

        [Fact]
        public void LeerListaRechazaLoMalformadoConElIndice()
        {
            Assert.Contains("obligatorio 'asignaciones'", Assert.Throws<ArgumentException>(() => Lotes.LeerLista(A("{}"), "asignaciones", false)).Message);
            Assert.Contains("arreglo JSON", Assert.Throws<ArgumentException>(() => Lotes.LeerLista(A("{\"asignaciones\":{\"region\":\"0\"}}"), "asignaciones", false)).Message);
            Assert.Contains("asignaciones[1] debe ser un objeto", Assert.Throws<ArgumentException>(() => Lotes.LeerLista(A("{\"asignaciones\":[{\"region\":\"0\"},\"x\"]}"), "asignaciones", false)).Message);
            Assert.Contains("está vacío", Assert.Throws<ArgumentException>(() => Lotes.LeerLista(A("{\"asignaciones\":[]}"), "asignaciones", false)).Message);
            Assert.Contains("JSON válido", Assert.Throws<ArgumentException>(() => Lotes.LeerLista(A("{\"asignaciones\":\"[{\"}"), "asignaciones", false)).Message);
        }

        [Fact]
        public void LimiteDe200SalvoForzar()
        {
            string lista = "[" + string.Join(",", System.Linq.Enumerable.Repeat("{\"region\":\"0\"}", 201)) + "]";
            var args = A("{\"asignaciones\":" + lista + "}");
            var ex = Assert.Throws<ArgumentException>(() => Lotes.LeerLista(args, "asignaciones", false));
            Assert.Contains("201 elementos", ex.Message);
            Assert.Contains("forzar=true", ex.Message);
            Assert.Equal(201, Lotes.LeerLista(args, "asignaciones", true).Count);
            Assert.Equal(200, Lotes.Limite);
        }

        [Fact]
        public void ConDefectosTomaDelNivelSuperiorLoQueFalta()
        {
            var args = A("{\"corredor\":\"C1\",\"linea_base\":\"BL\",\"asignaciones\":[]}");
            var e = Lotes.ConDefectos(A("{\"region\":\"0\",\"linea_base\":\"BL2\"}"), args, "corredor", "linea_base");
            Assert.Equal("C1", e.GetProperty("corredor").GetString());
            Assert.Equal("BL2", e.GetProperty("linea_base").GetString());
            Assert.Equal("0", e.GetProperty("region").GetString());
            Assert.False(Lotes.ConDefectos(A("{}"), A("{}"), "corredor").TryGetProperty("corredor", out _));
        }

        private static List<ElementoLote> Elementos()
        {
            return new List<ElementoLote>
            {
                new ElementoLote { Indice = 0, Accion = "a0", Antes = new Dictionary<string, object> { ["objetivos"] = new List<string> { "Terreno" }, ["opcion"] = "mas_cercano" }, Esperado = new Dictionary<string, object> { ["objetivos"] = new List<string> { "Rasante" } } },
                new ElementoLote { Indice = 1, Accion = "a1", Error = "No existe la región 'X'" },
                new ElementoLote { Indice = 2, Accion = "a2", Antes = new Dictionary<string, object> { ["objetivos"] = new List<string>() }, Esperado = new Dictionary<string, object> { ["objetivos"] = new List<string> { "Eje 2" } } }
            };
        }

        [Fact]
        public void SimulacionDaPlanPorIndiceYFallidos()
        {
            var j = JsonSerializer.Deserialize<JsonElement>(Json.Serializar(Lotes.Simulacion("asignar_objetivos", Elementos(), "Asignar 3 objetivos")));
            Assert.True(j.GetProperty("simulado").GetBoolean());
            Assert.Equal(3, j.GetProperty("plan").GetArrayLength());
            Assert.Equal("Rasante", j.GetProperty("plan")[0].GetProperty("despues").GetProperty("objetivos")[0].GetString());
            Assert.Equal("Terreno", j.GetProperty("plan")[0].GetProperty("antes").GetProperty("objetivos")[0].GetString());
            Assert.False(j.GetProperty("plan")[0].GetProperty("antes").TryGetProperty("opcion", out _), "solo lo que cambia");
            Assert.Equal("No existe la región 'X'", j.GetProperty("plan")[1].GetProperty("error").GetString());
            Assert.Equal(JsonValueKind.Null, j.GetProperty("antes")[1].ValueKind);
            Assert.Equal(new[] { "0.objetivos", "2.objetivos" }, new[] { j.GetProperty("cambios")[0].GetString(), j.GetProperty("cambios")[1].GetString() });
            Assert.Equal(1, j.GetProperty("fallidos").GetArrayLength());
            Assert.Equal(1, j.GetProperty("fallidos")[0].GetProperty("indice").GetInt32());
            var datos = j.GetProperty("datos");
            Assert.Equal(3, datos.GetProperty("total").GetInt32());
            Assert.Equal(2, datos.GetProperty("aplicadas").GetInt32());
            Assert.Equal(1, datos.GetProperty("fallidas").GetInt32());
        }

        [Fact]
        public void ResultadoVerificaPorIndiceYConservaElFormatoDeEscritura()
        {
            var els = Elementos();
            els[0].Despues = new Dictionary<string, object> { ["objetivos"] = new List<string> { "Rasante" }, ["opcion"] = "mas_cercano" };
            els[2].Despues = new Dictionary<string, object> { ["objetivos"] = new List<string> { "Eje 2" } };
            var copia = new InfoCopia { Ruta = "C:\\b\\obra_x.dwg", Reutilizada = true };
            var j = JsonSerializer.Deserialize<JsonElement>(Json.Serializar(Lotes.Resultado("asignar_objetivos", els, Lotes.Resumen("asignaciones", els), copia, new List<string> { "aviso" })));
            Assert.False(j.GetProperty("simulado").GetBoolean());
            Assert.Equal("2 asignaciones aplicadas, 1 fallidas (de 3)", j.GetProperty("mensaje").GetString());
            Assert.Equal("Rasante", j.GetProperty("despues")[0].GetProperty("objetivos")[0].GetString());
            Assert.Equal(JsonValueKind.Null, j.GetProperty("despues")[1].ValueKind);
            Assert.Equal("Eje 2", j.GetProperty("despues")[2].GetProperty("objetivos")[0].GetString());
            Assert.Equal("C:\\b\\obra_x.dwg", j.GetProperty("copia").GetProperty("ruta").GetString());
            Assert.True(j.GetProperty("copia").GetProperty("reutilizada").GetBoolean());
            Assert.Equal("aviso", j.GetProperty("avisos")[0].GetString());
            Assert.Equal("No existe la región 'X'", j.GetProperty("fallidos")[0].GetProperty("motivo").GetString());
            foreach (var campo in new[] { "simulado", "herramienta", "mensaje", "cambios", "antes", "despues", "copia", "datos", "avisos", "fallidos" })
                Assert.True(j.TryGetProperty(campo, out _), "falta " + campo);
        }

        [Fact]
        public void ResultadoLanzaSiUnElementoNoReflejaElCambio()
        {
            var els = Elementos();
            els[0].Despues = new Dictionary<string, object> { ["objetivos"] = new List<string> { "Rasante" }, ["opcion"] = "mas_cercano" };
            els[2].Despues = new Dictionary<string, object> { ["objetivos"] = new List<string>() };   // no cambió
            var ex = Assert.Throws<InvalidOperationException>(() => Lotes.Resultado("asignar_objetivos", els, "x", "copia.dwg"));
            Assert.Contains("El dibujo no refleja el cambio pedido en '[2].objetivos'", ex.Message);
            Assert.Contains("se pidió ['Eje 2'] y después de escribir tiene []", ex.Message);
            Assert.Contains("copia=copia.dwg", ex.Message);
        }
    }
}

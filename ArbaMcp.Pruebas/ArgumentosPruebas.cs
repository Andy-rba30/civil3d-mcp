using System;
using System.Collections.Generic;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    public class ArgumentosPruebas
    {
        private static JsonElement A(string json) => JsonSerializer.Deserialize<JsonElement>(json);

        [Theory]
        [InlineData("{\"simular\":true}", true)]
        [InlineData("{\"simular\":\"si\"}", true)]
        [InlineData("{\"simular\":\"Sí\"}", true)]
        [InlineData("{\"simular\":1}", true)]
        [InlineData("{\"simular\":\"1\"}", true)]
        [InlineData("{\"simular\":\"true\"}", true)]
        [InlineData("{\"simular\":\"yes\"}", true)]
        [InlineData("{\"simular\":false}", false)]
        [InlineData("{\"simular\":0}", false)]
        [InlineData("{\"simular\":\"no\"}", false)]
        [InlineData("{\"simular\":null}", false)]
        [InlineData("{}", false)]
        [InlineData("[]", false)]
        public void LeerSimular(string json, bool esperado) => Assert.Equal(esperado, Argumentos.LeerSimular(A(json)));

        [Fact]
        public void StrNumBoolRequerido()
        {
            var a = A("{\"n\":\"Corredor 1\",\"v\":\"12,5\",\"w\":3,\"b\":\"si\",\"vacio\":\"  \",\"nulo\":null}");
            Assert.Equal("Corredor 1", Argumentos.Str(a, "n"));
            Assert.Equal("3", Argumentos.Str(a, "w"));
            Assert.Equal("def", Argumentos.Str(a, "nulo", "def"));
            Assert.Equal(12.5, Argumentos.Num(a, "v", 0));
            Assert.Equal(3, Argumentos.Num(a, "w", 0));
            Assert.Equal(7, Argumentos.Num(a, "falta", 7));
            Assert.Throws<ArgumentException>(() => Argumentos.Num(a, "n", 0));
            Assert.True(Argumentos.Bool(a, "b", false));
            Assert.Equal("Corredor 1", Argumentos.Requerido(a, "n"));
            var ex = Assert.Throws<ArgumentException>(() => Argumentos.Requerido(a, "vacio"));
            Assert.Contains("'vacio'", ex.Message);
            Assert.Throws<ArgumentException>(() => Argumentos.Requerido(a, "falta"));
            Assert.False(Argumentos.Tiene(A("\"texto\""), "x"), "un args que no es objeto no tiene nada");
        }

        [Fact]
        public void ListaSeparadaPorPuntoYComaOArreglo()
        {
            Assert.Equal(new List<string> { "Crown", "ETW", "Daylight" }, Argumentos.Lista(A("{\"c\":\"Crown; ETW ;;Daylight\"}"), "c"));
            Assert.Equal(new List<string> { "a", "b" }, Argumentos.Lista(A("{\"c\":[\"a\",\" b \",\"\"]}"), "c"));
            Assert.Empty(Argumentos.Lista(A("{}"), "c"));
        }

        [Fact]
        public void JsonAceptaTextoOValorEstructurado()
        {
            var texto = Argumentos.Json(A("{\"asignaciones\":\"[{\\\"region\\\":\\\"0\\\"}]\"}"), "asignaciones");
            Assert.Equal(JsonValueKind.Array, texto.ValueKind);
            Assert.Equal("0", texto[0].GetProperty("region").GetString());
            var directo = Argumentos.Json(A("{\"asignaciones\":[{\"region\":\"1\"}]}"), "asignaciones");
            Assert.Equal("1", directo[0].GetProperty("region").GetString());
            Assert.Equal(JsonValueKind.Undefined, Argumentos.Json(A("{}"), "asignaciones").ValueKind);
            Assert.Equal(JsonValueKind.Undefined, Argumentos.Json(A("{\"asignaciones\":\"  \"}"), "asignaciones").ValueKind);
            var ex = Assert.Throws<ArgumentException>(() => Argumentos.Json(A("{\"asignaciones\":\"[{region\"}"), "asignaciones"));
            Assert.Contains("'asignaciones' debe ser JSON válido", ex.Message);
        }

        [Fact]
        public void RedondearA4DecimalesYNullSiNoEsFinito()
        {
            Assert.Equal(1.2346, Argumentos.Redondear(1.23456));
            Assert.Null(Argumentos.Redondear(double.NaN));
            Assert.Null(Argumentos.Redondear(double.PositiveInfinity));
        }
    }

    public class CatalogoPruebas
    {
        [Fact]
        public void SerializaLasHerramientasParaTools()
        {
            var h = new DescripcionHerramienta
            {
                Nombre = "asignar_objetivos",
                Descripcion = "Lote",
                Parametros = { new Parametro { name = "asignaciones", type = "json", description = "Ejemplo: [{...}]", required = true }, new Parametro { name = "simular", type = "boolean" } }
            };
            string json = Catalogo.SerializarTools(new[] { h });
            var raiz = JsonSerializer.Deserialize<JsonElement>(json);
            Assert.True(raiz.GetProperty("ok").GetBoolean());
            var t = raiz.GetProperty("tools")[0];
            Assert.Equal("asignar_objetivos", t.GetProperty("name").GetString());
            Assert.Equal("Lote", t.GetProperty("description").GetString());
            Assert.Equal("json", t.GetProperty("parameters")[0].GetProperty("type").GetString());
            Assert.True(t.GetProperty("parameters")[0].GetProperty("required").GetBoolean());
            Assert.False(t.GetProperty("parameters")[1].GetProperty("required").GetBoolean());
            Assert.Equal(JsonValueKind.Null, t.GetProperty("parameters")[1].GetProperty("description").ValueKind);
        }

        [Fact]
        public void ValidaTiposDeParametro()
        {
            Catalogo.Validar(new DescripcionHerramienta { Nombre = "x", Parametros = { new Parametro { name = "a", type = "string" }, new Parametro { name = "b", type = "json" } } });
            var ex = Assert.Throws<ArgumentException>(() => Catalogo.Validar(new DescripcionHerramienta { Nombre = "x", Parametros = { new Parametro { name = "a", type = "integer" } } }));
            Assert.Contains("'integer'", ex.Message);
            Assert.Throws<ArgumentException>(() => Catalogo.Validar(new DescripcionHerramienta { Nombre = " " }));
            Assert.Throws<ArgumentException>(() => Catalogo.Validar(new DescripcionHerramienta { Nombre = "x", Parametros = { new Parametro { type = "string" } } }));
            Assert.Equal(new[] { "string", "number", "boolean", "json" }, Catalogo.TiposParametro);
        }
    }

    public class RegistroEscrituraPruebas
    {
        [Fact]
        public void LineaDeLogTieneLosCamposDelContrato()
        {
            var args = JsonSerializer.Deserialize<JsonElement>("{\"corredor\":\"Corredor 1\",\"simular\":true}");
            string linea = RegistroEscritura.LineaLog(new DateTime(2026, 9, 28, 10, 5, 3), "asignar_objetivo", args, true, 42, null);
            var j = JsonSerializer.Deserialize<JsonElement>(linea);
            Assert.Equal("2026-09-28 10:05:03", j.GetProperty("hora").GetString());
            Assert.Equal("asignar_objetivo", j.GetProperty("herramienta").GetString());
            Assert.Equal("Corredor 1", j.GetProperty("args").GetProperty("corredor").GetString());
            Assert.True(j.GetProperty("ok").GetBoolean());
            Assert.Equal(42, j.GetProperty("ms").GetInt64());
            Assert.Equal(JsonValueKind.Null, j.GetProperty("error").ValueKind);
            Assert.DoesNotContain("\n", linea);

            string sinArgs = RegistroEscritura.LineaLog(DateTime.Now, "h", default, false, 1, "falló");
            Assert.Equal(JsonValueKind.Null, JsonSerializer.Deserialize<JsonElement>(sinArgs).GetProperty("args").ValueKind);
            Assert.Equal("Escritura h ERROR: falló (1 ms)", RegistroEscritura.LineaHistorial("h", false, 1, "falló"));
            Assert.Equal("Escritura h OK (2 ms)", RegistroEscritura.LineaHistorial("h", true, 2, null));
        }
    }
}

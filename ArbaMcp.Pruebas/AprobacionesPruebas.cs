using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>Registro como dataset (1.6.0): parámetro 'pedido', id de cada línea, id_log y aprobaciones.</summary>
    public class RegistroDatasetPruebas
    {
        private static JsonElement A(string json) => JsonSerializer.Deserialize<JsonElement>(json);
        private static JsonElement J(string linea) => JsonSerializer.Deserialize<JsonElement>(linea);

        [Theory]
        [InlineData("{\"pedido\":\"lista las superficies\"}", "lista las superficies")]
        [InlineData("{\"pedido\":\"  con espacios  \"}", "con espacios")]
        [InlineData("{\"pedido\":\"\"}", null)]
        [InlineData("{\"pedido\":\"   \"}", null)]
        [InlineData("{\"pedido\":null}", null)]
        [InlineData("{\"corredor\":\"Corredor 1\"}", null)]
        [InlineData("{}", null)]
        [InlineData("[]", null)]
        public void LeerPedido(string json, string esperado) => Assert.Equal(esperado, Argumentos.LeerPedido(A(json)));

        [Fact]
        public void PedidoNoCambiaLosDemasArgumentos()
        {
            var a = A("{\"corredor\":\"Corredor 1\",\"simular\":true,\"pedido\":\"asigna el talud\"}");
            Assert.Equal("Corredor 1", Argumentos.Str(a, "corredor"));
            Assert.True(Argumentos.LeerSimular(a));
            Assert.Equal("asigna el talud", Argumentos.LeerPedido(a));
        }

        [Fact]
        public void AsegurarPedidoLoAnadeUnaSolaVezYValida()
        {
            var h = new DescripcionHerramienta { Nombre = "listar_superficies", Descripcion = "x" };
            Assert.True(Catalogo.AsegurarPedido(h));
            Assert.False(Catalogo.AsegurarPedido(h));
            Assert.Single(h.Parametros.Where(p => p.name == "pedido"));
            var p = h.Parametros.Single(p => p.name == "pedido");
            Assert.Equal("string", p.type);
            Assert.False(p.required);
            Assert.Contains("mcp_log.jsonl", p.description);
            Catalogo.Validar(h);

            // Una herramienta que ya lo declara no lo duplica, y el parámetro queda al final de las demás
            var h2 = new DescripcionHerramienta { Nombre = "h2", Parametros = { new Parametro { name = "corredor", type = "string", required = true } } };
            Catalogo.AsegurarPedido(h2);
            Assert.Equal(new[] { "corredor", "pedido" }, h2.Parametros.Select(x => x.name));
            string tools = Catalogo.SerializarTools(new[] { h2 });
            Assert.Contains("\"name\":\"pedido\"", tools);
        }

        [Fact]
        public void NuevoIdEsUnicoYOrdenable()
        {
            var hora = new DateTime(2026, 10, 31, 14, 35, 1);
            string id1 = RegistroEscritura.NuevoId(hora);
            string id2 = RegistroEscritura.NuevoId(hora);
            Assert.StartsWith("20261031-143501-", id1);
            Assert.Equal(22, id1.Length);
            Assert.NotEqual(id1, id2);
            Assert.True(string.CompareOrdinal(RegistroEscritura.NuevoId(hora.AddSeconds(1)), id1) > 0);
            Assert.DoesNotContain(" ", id1);
        }

        [Fact]
        public void LineaDeLlamadaLlevaIdPedidoYVersion()
        {
            var args = A("{\"corredor\":\"Corredor 1\",\"pedido\":\"asigna el talud al terreno\"}");
            var hora = new DateTime(2026, 10, 31, 14, 35, 1);
            string linea = RegistroEscritura.LineaLog("20261031-143501-a3f9c2", hora, "asignar_objetivo", args, true, 42, null, "1.6.0");
            var j = J(linea);
            Assert.Equal("llamada", j.GetProperty("tipo").GetString());
            Assert.Equal("20261031-143501-a3f9c2", j.GetProperty("id").GetString());
            Assert.Equal("2026-10-31 14:35:01", j.GetProperty("hora").GetString());
            Assert.Equal("asignar_objetivo", j.GetProperty("herramienta").GetString());
            Assert.Equal("asigna el talud al terreno", j.GetProperty("pedido").GetString());
            Assert.Equal("Corredor 1", j.GetProperty("args").GetProperty("corredor").GetString());
            Assert.True(j.GetProperty("ok").GetBoolean());
            Assert.Equal(42, j.GetProperty("ms").GetInt64());
            Assert.Equal(JsonValueKind.Null, j.GetProperty("error").ValueKind);
            Assert.Equal("1.6.0", j.GetProperty("version").GetString());
            Assert.DoesNotContain("\n", linea);

            // Sin pedido y sin args: pedido y args nulos, el id sigue presente
            string sin = RegistroEscritura.LineaLog("id-x", hora, "ping", default, false, 1, "falló", "1.6.0");
            var k = J(sin);
            Assert.Equal(JsonValueKind.Null, k.GetProperty("pedido").ValueKind);
            Assert.Equal(JsonValueKind.Null, k.GetProperty("args").ValueKind);
            Assert.Equal("id-x", k.GetProperty("id").GetString());
            Assert.Equal("falló", k.GetProperty("error").GetString());

            // La forma antigua sigue funcionando y ahora también genera id
            string antigua = RegistroEscritura.LineaLog(hora, "h", default, true, 2, null);
            Assert.StartsWith("20261031-143501-", J(antigua).GetProperty("id").GetString());
        }

        [Fact]
        public void LineaDeHistorialConIdYPedidoRecortado()
        {
            Assert.Equal("Registro id-1: listar_superficies OK (3 ms) pedido: \"lista las superficies\"",
                RegistroEscritura.LineaHistorial("id-1", "listar_superficies", true, 3, null, "lista las superficies"));
            Assert.Equal("Registro id-2: asignar_objetivo ERROR: no existe (5 ms)",
                RegistroEscritura.LineaHistorial("id-2", "asignar_objetivo", false, 5, "no existe", null));
            string largo = new string('a', 200);
            string linea = RegistroEscritura.LineaHistorial("id-3", "h", true, 1, null, largo + "\nsegunda línea");
            Assert.Contains("…\"", linea);
            Assert.DoesNotContain("\n", linea);
        }

        [Fact]
        public void LineaDeAprobacionTieneLosCamposDelContrato()
        {
            var hora = new DateTime(2026, 10, 31, 14, 36, 0);
            var j = J(Aprobaciones.LineaAprobacion("20261031-143600-b1c2d3", hora, "20261031-143501-a3f9c2", true, " correcto "));
            Assert.Equal("aprobacion", j.GetProperty("tipo").GetString());
            Assert.Equal("20261031-143600-b1c2d3", j.GetProperty("id").GetString());
            Assert.Equal("2026-10-31 14:36:00", j.GetProperty("hora").GetString());
            Assert.Equal("20261031-143501-a3f9c2", j.GetProperty("id_log").GetString());
            Assert.True(j.GetProperty("aprobado").GetBoolean());
            Assert.Equal("correcto", j.GetProperty("comentario").GetString());

            var k = J(Aprobaciones.LineaAprobacion("x", hora, "y", false, "  "));
            Assert.False(k.GetProperty("aprobado").GetBoolean());
            Assert.Equal(JsonValueKind.Null, k.GetProperty("comentario").ValueKind);
        }

        [Fact]
        public void BuscarLlamadaEncuentraSoloLlamadasConEseId()
        {
            var hora = new DateTime(2026, 10, 31, 14, 35, 1);
            var lineas = new List<string>
            {
                "",
                "esto no es JSON",
                "{\"hora\":\"2026-09-28 10:05:03\",\"herramienta\":\"antigua\",\"ok\":true}",   // línea anterior a la 1.6.0, sin id
                RegistroEscritura.LineaLog("id-A", hora, "listar_superficies", A("{\"pedido\":\"lista las superficies\"}"), true, 3, null, "1.6.0"),
                Aprobaciones.LineaAprobacion("id-B", hora, "id-A", true, "correcto"),
                RegistroEscritura.LineaLog("id-C", hora, "asignar_objetivo", A("{\"corredor\":\"Corredor 1\"}"), false, 9, "no existe", "1.6.0"),
            };
            var a = Aprobaciones.BuscarLlamada(lineas, "id-A");
            Assert.NotNull(a);
            Assert.Equal("listar_superficies", a.Herramienta);
            Assert.Equal("2026-10-31 14:35:01", a.Hora);
            Assert.Equal("lista las superficies", a.Pedido);
            Assert.True(a.Ok);

            var c = Aprobaciones.BuscarLlamada(lineas, " id-C ");
            Assert.NotNull(c);
            Assert.False(c.Ok);
            Assert.Null(c.Pedido);

            Assert.Null(Aprobaciones.BuscarLlamada(lineas, "id-B"));      // el id de una aprobación no es una llamada
            Assert.Null(Aprobaciones.BuscarLlamada(lineas, "id-Z"));
            Assert.Null(Aprobaciones.BuscarLlamada(lineas, ""));
            Assert.Null(Aprobaciones.BuscarLlamada(null, "id-A"));
            Assert.Null(Aprobaciones.BuscarLlamada(new List<string>(), "id-A"));
        }

        [Fact]
        public void MensajesDeErrorEHistorial()
        {
            string m = Aprobaciones.MensajeNoExiste("id-Z", "C:\\Proyectos\\mcp_log.jsonl");
            Assert.Contains("id-Z", m);
            Assert.Contains("leer_log", m);
            Assert.Equal("Aprobación id-A (listar_superficies): aprobado - correcto", Aprobaciones.LineaHistorial("id-A", "listar_superficies", true, "correcto"));
            Assert.Equal("Aprobación id-A (h): rechazado", Aprobaciones.LineaHistorial("id-A", "h", false, null));
        }
    }
}

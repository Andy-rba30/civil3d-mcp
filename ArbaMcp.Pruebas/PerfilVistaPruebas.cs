using System;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Lógica de crear_perfil_terreno_y_vista que no depende de Civil 3D: nombres por defecto, lectura de
    /// 'punto_insercion', regla del punto automático (a la derecha de lo dibujado), comprobación de rangos tras crear y
    /// bloques 'perfil' y 'vista' de la respuesta. Extensiones sintéticas en coordenadas genéricas (ningún dato real).
    /// </summary>
    public class PerfilVistaPruebas
    {
        private static JsonElement Json(string texto)
        {
            using (var d = JsonDocument.Parse(texto)) return d.RootElement.Clone();
        }

        // ---------------------------------------------------------------- nombres por defecto
        [Fact]
        public void NombresPorDefectoYPedidos()
        {
            Assert.Equal("Terreno - Terreno", PerfilVista.NombrePerfil(null, "Terreno"));
            Assert.Equal("Terreno - Terreno", PerfilVista.NombrePerfil("  ", " Terreno "));
            Assert.Equal("EG Eje 1", PerfilVista.NombrePerfil(" EG Eje 1 ", "Terreno"));
            Assert.Equal("Eje 1 - Vista", PerfilVista.NombreVista(null, "Eje 1"));
            Assert.Equal("PV-1", PerfilVista.NombreVista("PV-1", "Eje 1"));
        }

        // ---------------------------------------------------------------- punto de inserción pedido
        [Fact]
        public void LeePuntoComoObjetoOLista()
        {
            var p = PerfilVista.LeerPuntoInsercion(Json("{\"x\": 1500, \"y\": 2000}"));
            Assert.True(p.HasValue);
            Assert.Equal(1500, p.Value.X);
            Assert.Equal(2000, p.Value.Y);
            var q = PerfilVista.LeerPuntoInsercion(Json("[10.5, \"20.25\"]"));
            Assert.Equal(10.5, q.Value.X);
            Assert.Equal(20.25, q.Value.Y);
            Assert.Null(PerfilVista.LeerPuntoInsercion(default));
            Assert.Null(PerfilVista.LeerPuntoInsercion(Json("null")));
        }

        [Fact]
        public void PuntoIncompletoONoNumericoEsError()
        {
            var ex = Assert.Throws<ArgumentException>(() => PerfilVista.LeerPuntoInsercion(Json("{\"x\": 1500}")));
            Assert.Contains("faltan 'x' o 'y'", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => PerfilVista.LeerPuntoInsercion(Json("{\"x\": \"abc\", \"y\": 1}")));
            Assert.Contains("'x' no es un número", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => PerfilVista.LeerPuntoInsercion(Json("\"1500,2000\"")));
            Assert.Contains("no es un objeto ni una lista", ex.Message);
            Assert.Contains("punto_insercion", ex.Message);
        }

        // ---------------------------------------------------------------- punto automático
        [Fact]
        public void PuntoAutomaticoALaDerechaConMargenDelDiezPorCiento()
        {
            // Lo dibujado ocupa de x=1000 a x=2000 (ancho 1000) y de y=500 a y=900: x = 2000 + 100, y = 500
            var p = PerfilVista.PuntoAutomatico(new Extension2(1000, 500, 2000, 900));
            Assert.Equal(2100, p.X, 6);
            Assert.Equal(500, p.Y, 6);
        }

        [Fact]
        public void PuntoAutomaticoUneLasExtensionesEIgnoraLasNoValidas()
        {
            var eje = new Extension2(1000, 2000, 1600, 2300);          // ancho 600
            var superficie = new Extension2(900, 1900, 1700, 2500);    // ancho 800 → unión: 900..1700 (ancho 800), y mínima 1900
            var dibujoVacio = new Extension2(1e20, 1e20, -1e20, -1e20); // el "vacío" de AutoCAD: se ignora
            var p = PerfilVista.PuntoAutomatico(eje, superficie, dibujoVacio, null);
            Assert.Equal(1700 + 80, p.X, 6);
            Assert.Equal(1900, p.Y, 6);
        }

        [Fact]
        public void MargenMinimoCuandoLoDibujadoEsEstrecho()
        {
            // Ancho 100 → el 10 % serían 10, pero el margen mínimo es 50
            var p = PerfilVista.PuntoAutomatico(new Extension2(0, 0, 100, 1000));
            Assert.Equal(150, p.X, 6);
            Assert.Equal(0, p.Y, 6);
            Assert.Equal(PerfilVista.MargenMinimo, PerfilVista.Margen(0));
            Assert.Equal(300, PerfilVista.Margen(3000), 6);
        }

        [Fact]
        public void SinExtensionLegibleEsError()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => PerfilVista.PuntoAutomatico(null, new Extension2(double.NaN, 0, 1, 1)));
            Assert.Contains("punto_insercion", ex.Message);
        }

        [Fact]
        public void ExplicacionDelPuntoAutomaticoDiceLaRegla()
        {
            var total = new Extension2(1000, 500, 2000, 900);
            string texto = PerfilVista.ExplicacionPuntoAutomatico(total, PerfilVista.PuntoAutomatico(total));
            Assert.Contains("x = 2000", texto);
            Assert.Contains("+ 100 de margen", texto);
            Assert.Contains("= 2100", texto);
            Assert.Contains("y = 500", texto);
        }

        // ---------------------------------------------------------------- comprobaciones tras crear
        [Fact]
        public void PerfilValidoYMotivosDeFallo()
        {
            Assert.Null(PerfilVista.MotivoPerfilNoValido(0, 831.3274, 100.5, 112.25));
            Assert.Null(PerfilVista.MotivoPerfilNoValido(0, 10, 50, 50));   // terreno plano: cotas iguales valen
            Assert.Contains("progresiva final", PerfilVista.MotivoPerfilNoValido(0, 0, 100, 110));
            Assert.Contains("cota máxima", PerfilVista.MotivoPerfilNoValido(0, 100, 120, 110));
            Assert.Contains("no cubrir el eje", PerfilVista.MotivoPerfilNoValido(0, 100, double.NaN, 110));
        }

        [Fact]
        public void VistaValidaYMotivosDeFallo()
        {
            Assert.Null(PerfilVista.MotivoVistaNoValida("Eje 1 - Vista", 0, 831.3274));
            Assert.Contains("no tiene nombre", PerfilVista.MotivoVistaNoValida(" ", 0, 10));
            Assert.Contains("progresiva final", PerfilVista.MotivoVistaNoValida("V", 10, 10));
        }

        // ---------------------------------------------------------------- respuestas
        [Fact]
        public void RespuestaPerfilConCotasPendientesYConCotas()
        {
            var prevista = PerfilVista.RespuestaPerfil("Terreno - Terreno", "EG", 0, 831.32741, null, null, "Standard", null);
            Assert.Equal("EG", prevista["tipo"]);
            Assert.Equal(831.3274, prevista["progresiva_final"]);
            Assert.Null(prevista["cota_minima"]);
            Assert.Equal("(sin etiquetas)", prevista["conjunto_etiquetas"]);
            var real = PerfilVista.RespuestaPerfil("Terreno - Terreno", "EG", 0, 831.3274, 100.12346, 112.5, "Standard", "Complete Label Set");  // 100,12346 redondea a 100,1235
            Assert.Equal(100.1235, real["cota_minima"]);
            Assert.Equal(112.5, real["cota_maxima"]);
            Assert.Equal("Complete Label Set", real["conjunto_etiquetas"]);
        }

        [Fact]
        public void RespuestaVistaConPuntoYBandas()
        {
            var v = PerfilVista.RespuestaVista("Eje 1 - Vista", new Punto2(2100.00004, 500), "Standard", "Standard", 0, 831.3274, 2);
            var punto = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(v["punto_insercion"]);
            Assert.Equal(2100.0, punto["x"]);
            Assert.Equal(500.0, punto["y"]);
            Assert.Equal(2, v["bandas"]);
            var sinBandas = PerfilVista.RespuestaVista("V", new Punto2(0, 0), "S", null, 0, 1, null);
            Assert.Equal("(sin bandas)", sinBandas["conjunto_bandas"]);
            Assert.Null(sinBandas["bandas"]);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Bordes de calzada (crear_bordes_calzada y listar_bordes_calzada, 1.21.0) sin Civil 3D: lectura y validación de los
    /// ensanchamientos, desplazamiento total, medición de un borde con un desplazamiento sintético (trapecio) y
    /// comparación con lo pedido. Valores genéricos del eje de prueba (ningún dato real ni de la norma).
    /// </summary>
    public class BordesCalzadaPruebas
    {
        private static JsonElement J(string texto)
        {
            using (var d = JsonDocument.Parse(texto)) return d.RootElement.Clone();
        }

        /// <summary>Desplazamiento con signo de un borde: nominal fuera de la zona y una rampa lineal hasta el pleno.</summary>
        private static Func<double, double?> Trapecio(double signo, double nominal, double sobreancho, double inicioTransicion, double inicioPleno, double finPleno, double finTransicion)
        {
            return s =>
            {
                double e;
                if (s <= inicioTransicion || s >= finTransicion) e = 0;
                else if (s < inicioPleno) e = sobreancho * (s - inicioTransicion) / (inicioPleno - inicioTransicion);
                else if (s <= finPleno) e = sobreancho;
                else e = sobreancho * (finTransicion - s) / (finTransicion - finPleno);
                return signo * (nominal + e);
            };
        }

        // ------------------------------------------------------------------ nombres, desplazamiento y borde
        [Fact]
        public void NombresPorDefecto()
        {
            Assert.Equal("Eje 1 - Borde izq", BordesCalzada.NombrePorDefecto("Eje 1", BordesCalzada.Izquierdo));
            Assert.Equal("Eje 1 - Borde der", BordesCalzada.NombrePorDefecto(" Eje 1 ", BordesCalzada.Derecho));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3.6)]
        [InlineData(double.NaN)]
        public void DesplazamientoNoPositivoEsError(double d)
        {
            var ex = Assert.Throws<ArgumentException>(() => BordesCalzada.ValidarDesplazamiento(d));
            Assert.Contains("media calzada", ex.Message);
        }

        [Theory]
        [InlineData("izquierdo", "izquierdo")]
        [InlineData("Izquierda", "izquierdo")]
        [InlineData("left", "izquierdo")]
        [InlineData("der", "derecho")]
        [InlineData("DERECHO", "derecho")]
        [InlineData("arriba", null)]
        [InlineData(null, null)]
        public void NormalizaElBorde(string texto, string esperado) => Assert.Equal(esperado, BordesCalzada.NormalizarBorde(texto));

        [Fact]
        public void DistanciaEnsanchadaSinSignoEnLosDosBordes()
        {
            // 1.21.3: AddWidening rechaza offsetDistance <= 0 (validación de la ronda D, 20/11/2026)
            Assert.Equal(4.2, BordesCalzada.DistanciaEnsanchada(-3.6, 0.6), 9);
            Assert.Equal(4.2, BordesCalzada.DistanciaEnsanchada(3.6, 0.6), 9);
            Assert.True(BordesCalzada.DistanciaEnsanchada(-0.001, 0.001) > 0);
            Assert.Throws<InvalidOperationException>(() => BordesCalzada.DistanciaEnsanchada(0, 0.6));
            Assert.Throws<ArgumentException>(() => BordesCalzada.DistanciaEnsanchada(-3.6, 0));
            Assert.Throws<ArgumentException>(() => BordesCalzada.DistanciaEnsanchada(-3.6, double.NaN));
        }

        [Fact]
        public void DesplazamientoTotalConElSignoDelNominal()
        {
            Assert.Equal(-4.2, BordesCalzada.DesplazamientoTotal(-3.6, 0.6), 9);
            Assert.Equal(4.2, BordesCalzada.DesplazamientoTotal(3.6, 0.6), 9);
            Assert.Throws<InvalidOperationException>(() => BordesCalzada.DesplazamientoTotal(0, 0.6));
        }

        [Fact]
        public void PasoDeMuestreo()
        {
            Assert.Equal(1.0, BordesCalzada.PasoMuestreo(831.3274));
            Assert.Equal(5.0, BordesCalzada.PasoMuestreo(10000));
        }

        // ------------------------------------------------------------------ lectura de ensanchamientos
        [Fact]
        public void SinEnsanchamientosEsListaVacia()
        {
            Assert.Empty(BordesCalzada.Leer(default, 0, 800));
            Assert.Empty(BordesCalzada.Leer(J("null"), 0, 800));
            Assert.Empty(BordesCalzada.Leer(J("[]"), 0, 800));
        }

        [Fact]
        public void LeeEnsanchamientosValidos()
        {
            var l = BordesCalzada.Leer(J("[{\"borde\":\"izq\",\"progresiva_inicial\":220,\"progresiva_final\":345.6637,\"sobreancho\":0.6,\"longitud_transicion\":40},"
                + "{\"borde\":\"derecho\",\"progresiva_inicial\":\"485,66\",\"progresiva_final\":611.33,\"sobreancho\":\"0.75\"}]"), 0, 831.3274);
            Assert.Equal(2, l.Count);
            Assert.Equal(BordesCalzada.Izquierdo, l[0].Borde);
            Assert.Equal(1, l[0].Numero);
            Assert.Equal(40, l[0].LongitudTransicion);
            Assert.Equal(180, l[0].InicioOcupado, 9);
            Assert.Equal(385.6637, l[0].FinOcupado, 9);
            Assert.Equal(BordesCalzada.Derecho, l[1].Borde);
            Assert.Equal(485.66, l[1].ProgresivaInicial, 9);
            Assert.Null(l[1].LongitudTransicion);
            var d = l[0].ComoDiccionario(-3.6);
            Assert.Equal(4.2, (double?)d["desplazamiento_total"]);
            Assert.IsType<Dictionary<string, object>>(d["transicion_prevista"]);
            Assert.IsType<string>(l[1].ComoDiccionario(3.6)["transicion_prevista"]);
        }

        [Theory]
        [InlineData("{\"borde\":\"izquierdo\"}", "no es una lista")]
        [InlineData("[5]", "no es un objeto")]
        [InlineData("[{\"progresiva_inicial\":1,\"progresiva_final\":2,\"sobreancho\":0.5}]", "'borde' = (falta)")]
        [InlineData("[{\"borde\":\"centro\",\"progresiva_inicial\":1,\"progresiva_final\":2,\"sobreancho\":0.5}]", "'borde' = 'centro'")]
        [InlineData("[{\"borde\":\"izquierdo\",\"progresiva_final\":2,\"sobreancho\":0.5}]", "no tiene 'progresiva_inicial'")]
        [InlineData("[{\"borde\":\"izquierdo\",\"progresiva_inicial\":\"a\",\"progresiva_final\":2,\"sobreancho\":0.5}]", "no es un número")]
        [InlineData("[{\"borde\":\"izquierdo\",\"progresiva_inicial\":10,\"progresiva_final\":20,\"sobreancho\":0}]", "mayor que 0")]
        [InlineData("[{\"borde\":\"izquierdo\",\"progresiva_inicial\":10,\"progresiva_final\":20,\"sobreancho\":0.5,\"longitud_transicion\":-1}]", "negativa")]
        [InlineData("[{\"borde\":\"izquierdo\",\"progresiva_inicial\":20,\"progresiva_final\":20,\"sobreancho\":0.5}]", "mayor que la inicial")]
        [InlineData("[{\"borde\":\"izquierdo\",\"progresiva_inicial\":10,\"progresiva_final\":120,\"sobreancho\":0.5,\"longitud_transicion\":40}]", "contando la transición de 40 m")]
        [InlineData("[{\"borde\":\"izquierdo\",\"progresiva_inicial\":50,\"progresiva_final\":900,\"sobreancho\":0.5}]", "y el eje va de 0 a 800")]
        public void RechazaEnsanchamientosNoValidos(string json, string fragmento)
        {
            var ex = Assert.Throws<ArgumentException>(() => BordesCalzada.Leer(J(json.StartsWith("{") ? json : json), 0, 800));
            if (fragmento == "no es una lista") Assert.Contains("debe ser una lista JSON", ex.Message);
            else Assert.Contains(fragmento, ex.Message);
        }

        [Fact]
        public void RechazaSolapesEnElMismoBordeContandoLasTransiciones()
        {
            string json = "[{\"borde\":\"izquierdo\",\"progresiva_inicial\":100,\"progresiva_final\":200,\"sobreancho\":0.5,\"longitud_transicion\":40},"
                + "{\"borde\":\"izquierdo\",\"progresiva_inicial\":260,\"progresiva_final\":300,\"sobreancho\":0.5,\"longitud_transicion\":40}]";
            var ex = Assert.Throws<ArgumentException>(() => BordesCalzada.Leer(J(json), 0, 800));
            Assert.Contains("Los ensanchamientos 1 y 2 del borde izquierdo se solapan", ex.Message);
            // En bordes distintos no hay solape
            Assert.Equal(2, BordesCalzada.Leer(J(json.Replace("\"borde\":\"izquierdo\",\"progresiva_inicial\":260", "\"borde\":\"derecho\",\"progresiva_inicial\":260")), 0, 800).Count);
            // Sin transición pedida, tocarse no es solaparse
            Assert.Equal(2, BordesCalzada.Leer(J("[{\"borde\":\"i\",\"progresiva_inicial\":100,\"progresiva_final\":200,\"sobreancho\":0.5},{\"borde\":\"i\",\"progresiva_inicial\":200,\"progresiva_final\":300,\"sobreancho\":0.5}]"), 0, 800).Count);
        }

        [Fact]
        public void RechazaMasDeDoscientos()
        {
            var partes = Enumerable.Range(0, BordesCalzada.MaximoEnsanchamientos + 1)
                .Select(i => "{\"borde\":\"izquierdo\",\"progresiva_inicial\":" + (i * 10) + ",\"progresiva_final\":" + (i * 10 + 5) + ",\"sobreancho\":0.5}");
            var ex = Assert.Throws<ArgumentException>(() => BordesCalzada.Leer(J("[" + string.Join(",", partes) + "]"), 0, 5000));
            Assert.Contains("más de 200", ex.Message);
        }

        // ------------------------------------------------------------------ medición
        [Fact]
        public void MideUnBordeSinEnsanchamientos()
        {
            var perfil = BordesCalzada.Medir(s => -3.6, 0, 100, 1, 3.6);
            Assert.Equal(101, perfil.Muestras);
            Assert.Equal(0, perfil.MuestrasFallidas);
            Assert.Equal(-1, perfil.Signo);
            Assert.Equal(BordesCalzada.Izquierdo, perfil.Lado);
            Assert.Equal(3.6, perfil.NominalMedido.Value, 9);
            Assert.Empty(perfil.Zonas);
        }

        [Fact]
        public void MideUnaZonaConTransicionesLineales()
        {
            var perfil = BordesCalzada.Medir(Trapecio(-1, 3.6, 0.6, 180, 220, 345.6637, 385.6637), 0, 831.3274, 1, 3.6);
            Assert.Equal(-1, perfil.Signo);
            var z = Assert.Single(perfil.Zonas);
            Assert.Equal(180, z.InicioTransicion, 1);
            Assert.Equal(220, z.InicioPleno, 1);
            Assert.Equal(345.6637, z.FinPleno, 1);
            Assert.Equal(385.6637, z.FinTransicion, 1);
            Assert.Equal(40, z.LongitudEntrada, 1);
            Assert.Equal(40, z.LongitudSalida, 1);
            Assert.Equal(0.6, z.SobreanchoMaximo, 6);
            Assert.Equal(0.6, z.SobreanchoMedio, 6);
            Assert.False(z.CortadaAlInicio);
            Assert.False(z.CortadaAlFinal);
            Assert.Equal(5, z.PuntosEntrada.Count);
            Assert.Equal(0.3, z.PuntosEntrada[2][1], 2);   // a mitad de la rampa, la mitad del sobreancho
        }

        [Fact]
        public void MideUnEscalonSinTransicion()
        {
            var perfil = BordesCalzada.Medir(Trapecio(1, 3.6, 0.5, 300, 300.0001, 400, 400.0001), 0, 800, 1, 3.6);
            Assert.Equal(1, perfil.Signo);
            var z = Assert.Single(perfil.Zonas);
            Assert.Equal(300, z.InicioPleno, 1);
            Assert.Equal(400, z.FinPleno, 1);
            Assert.True(z.LongitudEntrada < 0.05);
            Assert.True(z.LongitudSalida < 0.05);
        }

        [Fact]
        public void ZonaQueTocaElInicioQuedaCortada()
        {
            var perfil = BordesCalzada.Medir(Trapecio(-1, 3.6, 0.6, -40, 0, 50, 90), 0, 300, 1, 3.6);
            var z = Assert.Single(perfil.Zonas);
            Assert.True(z.CortadaAlInicio);
            Assert.False(z.CortadaAlFinal);
            Assert.Equal(0, z.InicioTransicion, 6);
            Assert.Equal(90, z.FinTransicion, 1);
        }

        [Fact]
        public void LasMedidasFallidasSeCuentanYNoRompen()
        {
            Func<double, double?> f = s => s > 40 && s < 50 ? (double?)null : s > 60 && s < 62 ? throw new InvalidOperationException("fuera del eje") : -3.6;
            var perfil = BordesCalzada.Medir(f, 0, 100, 1, 3.6);
            Assert.Equal(101, perfil.Muestras);
            Assert.Equal(10, perfil.MuestrasFallidas);
            Assert.Empty(perfil.Zonas);
            Assert.Equal(-1, perfil.Signo);
        }

        [Fact]
        public void SinMuestrasNoHayLado()
        {
            var perfil = BordesCalzada.Medir(s => null, 0, 10, 1, 3.6);
            Assert.Equal(0, perfil.Signo);
            Assert.Null(perfil.Lado);
            Assert.Null(perfil.NominalMedido);
            Assert.Null(perfil.ComoDiccionario()["lado_medido"]);
        }

        // ------------------------------------------------------------------ comparación
        private static Ensanchamiento Pedido(double ini, double fin, double sa, double? lt = null)
            => new Ensanchamiento { Numero = 1, Borde = BordesCalzada.Izquierdo, ProgresivaInicial = ini, ProgresivaFinal = fin, Sobreancho = sa, LongitudTransicion = lt };

        [Fact]
        public void TransicionFueraDelRangoPedidoNoAvisa()
        {
            var perfil = BordesCalzada.Medir(Trapecio(-1, 3.6, 0.6, 180, 220, 345.6637, 385.6637), 0, 831.3274, 1, 3.6);
            var c = BordesCalzada.Comparar(Pedido(220, 345.6637, 0.6, 40), perfil, -3.6, "Eje 1 - Borde izq");
            Assert.Null(c.Error);
            Assert.Null(c.Aviso);
            Assert.Equal("fuera", c.Transicion);
            Assert.Equal(0.6, c.SobreanchoMedido.Value, 6);
            Assert.Contains("transiciones fuera", (string)c.ComoDiccionario()["explicacion_transicion"]);
        }

        [Fact]
        public void TransicionDentroDelRangoPedidoAvisa()
        {
            var perfil = BordesCalzada.Medir(Trapecio(-1, 3.6, 0.6, 220, 260, 305.6637, 345.6637), 0, 831.3274, 1, 3.6);
            var c = BordesCalzada.Comparar(Pedido(220, 345.6637, 0.6), perfil, -3.6, "Eje 1 - Borde izq");
            Assert.Null(c.Error);
            Assert.Equal("dentro", c.Transicion);
            Assert.Contains("transiciones dentro del rango pedido", c.Aviso);
        }

        [Fact]
        public void TransicionDeOtraLongitudAvisa()
        {
            var perfil = BordesCalzada.Medir(Trapecio(-1, 3.6, 0.6, 200, 220, 345.6637, 365.6637), 0, 831.3274, 1, 3.6);
            var c = BordesCalzada.Comparar(Pedido(220, 345.6637, 0.6, 40), perfil, -3.6, "Eje 1 - Borde izq");
            Assert.Null(c.Error);
            Assert.Equal("fuera", c.Transicion);
            Assert.Contains("se pidió una transición de 40 m y se midió 20 m a la entrada y 20 m a la salida", c.Aviso);
        }

        [Fact]
        public void SobreanchoDistintoEsError()
        {
            var perfil = BordesCalzada.Medir(Trapecio(-1, 3.6, 0.3, 180, 220, 345.6637, 385.6637), 0, 831.3274, 1, 3.6);
            var c = BordesCalzada.Comparar(Pedido(220, 345.6637, 0.6), perfil, -3.6, "Eje 1 - Borde izq");
            Assert.Contains("sobreancho medido de 0.3 m y se pidió 0.6 m", c.Error);
        }

        [Fact]
        public void SobreanchoSumadoAlNominalSeExplica()
        {
            var perfil = BordesCalzada.Medir(Trapecio(-1, 3.6, 4.2, 180, 220, 345.6637, 385.6637), 0, 831.3274, 1, 3.6);
            var c = BordesCalzada.Comparar(Pedido(220, 345.6637, 0.6), perfil, -3.6, "Eje 1 - Borde izq");
            Assert.Contains("suma el valor al nominal", c.Error);
        }

        [Fact]
        public void BordeSinEnsancharSeExplica()
        {
            var perfil = BordesCalzada.Medir(s => -3.6, 0, 800, 1, 3.6);
            var c = BordesCalzada.Comparar(Pedido(220, 345.6637, 0.6), perfil, -3.6, "Eje 1 - Borde izq");
            Assert.Contains("no se ensanchó", c.Error);
        }
    }
}

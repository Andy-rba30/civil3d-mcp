using System;
using System.Collections.Generic;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Lógica de crear_corredor (1.11.0) que no depende de Civil 3D: nombres por defecto, rango común del eje y la rasante
    /// con el ajuste de 'inicio' y 'fin', frecuencia, superficie objetivo por defecto, texto de la acción y comprobación
    /// de lo releído. Progresivas sintéticas (el eje genérico de las validaciones va de 0 a 831.3274; ningún dato real).
    /// </summary>
    public class CorredorPruebas
    {
        private const double FinEje = 831.3274;

        // ---------------------------------------------------------------- nombres por defecto
        [Fact]
        public void NombresPorDefectoYPedidos()
        {
            Assert.Equal("Línea base 1", Corredor.NombreLineaBase(null));
            Assert.Equal("Línea base 1", Corredor.NombreLineaBase("   "));
            Assert.Equal("BL Eje 1", Corredor.NombreLineaBase(" BL Eje 1 "));
            Assert.Equal("Región 1", Corredor.NombreRegion(null));
            Assert.Equal("Tramo A", Corredor.NombreRegion("Tramo A"));
        }

        // ---------------------------------------------------------------- rango
        [Fact]
        public void SinInicioNiFinUsaElRangoComun()
        {
            var r = Corredor.Rango(0, FinEje, 0, FinEje, null, null, "Eje 1", "Rasante 1");
            Assert.Equal(0, r.Inicio);
            Assert.Equal(FinEje, r.Fin);
            Assert.Equal(FinEje, r.Longitud, 6);
            Assert.False(r.InicioPedido);
            Assert.False(r.FinPedido);
            Assert.Empty(r.Avisos);
            var d = r.ComoDiccionario();
            Assert.Equal(831.3274, d["fin"]);
            Assert.Equal(0.0, d["inicio_comun"]);
        }

        [Fact]
        public void RasanteMasCortaQueElEjeRecortaElRangoYAvisa()
        {
            var r = Corredor.Rango(0, FinEje, 50, 700, null, null, "Eje 1", "Rasante 1");
            Assert.Equal(50, r.Inicio);
            Assert.Equal(700, r.Fin);
            Assert.Equal(50, r.InicioComun);
            Assert.Equal(700, r.FinComun);
            Assert.Single(r.Avisos);
            Assert.Contains("no cubre todo el eje", r.Avisos[0]);
        }

        [Fact]
        public void InicioYFinPedidosDentroDelRango()
        {
            var r = Corredor.Rango(0, FinEje, 0, FinEje, 100, 600, "Eje 1", "Rasante 1");
            Assert.Equal(100, r.Inicio);
            Assert.Equal(600, r.Fin);
            Assert.True(r.InicioPedido);
            Assert.True(r.FinPedido);
            Assert.Empty(r.Avisos);
            // Solo uno pedido: el otro es el extremo del tramo común
            var s = Corredor.Rango(0, FinEje, 0, FinEje, null, 500, "Eje 1", "Rasante 1");
            Assert.Equal(0, s.Inicio);
            Assert.Equal(500, s.Fin);
        }

        [Fact]
        public void UnPocoFueraSeAjustaAlExtremoConAviso()
        {
            var r = Corredor.Rango(0, FinEje, 0, FinEje, -0.004, 831.33, "Eje 1", "Rasante 1");
            Assert.Equal(0, r.Inicio);
            Assert.Equal(FinEje, r.Fin);
            Assert.Equal(2, r.Avisos.Count);
            Assert.Contains("se ajusta al inicio del tramo común (0)", r.Avisos[0]);
            Assert.Contains("831.33 se ajusta al final del tramo común (831.327)", r.Avisos[1]);
        }

        [Fact]
        public void FueraDelRangoComunEsErrorConLasHerramientasDeLectura()
        {
            var ex = Assert.Throws<ArgumentException>(() => Corredor.Rango(0, FinEje, 0, FinEje, null, 900, "Eje 1", "Rasante 1"));
            Assert.Contains("'fin' (900) queda fuera del tramo común", ex.Message);
            Assert.Contains("debe estar entre 0 y 831.327", ex.Message);
            Assert.Contains("listar_alineamientos", ex.Message);
            Assert.Contains("listar_perfiles", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => Corredor.Rango(0, FinEje, 100, FinEje, 50, null, "Eje 1", "Rasante 1"));
            Assert.Contains("'inicio' (50) queda fuera", ex.Message);
            Assert.Contains("la rasante 'Rasante 1' (100 a 831.327)", ex.Message);
        }

        [Fact]
        public void InicioNoMenorQueFinEsError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Corredor.Rango(0, FinEje, 0, FinEje, 500, 400, "Eje 1", "Rasante 1"));
            Assert.Contains("debe ser mayor que el 'inicio'", ex.Message);
            Assert.Throws<ArgumentException>(() => Corredor.Rango(0, FinEje, 0, FinEje, 300, 300, "Eje 1", "Rasante 1"));
        }

        [Fact]
        public void EjeYRasanteSinTramoComunEsError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Corredor.Rango(0, 400, 500, 800, null, null, "Eje 1", "Rasante X"));
            Assert.Contains("no tienen un tramo común", ex.Message);
            Assert.Contains("crear_rasante", ex.Message);
        }

        [Fact]
        public void ProgresivasNoValidasSonError()
        {
            Assert.Throws<ArgumentException>(() => Corredor.Rango(double.NaN, FinEje, 0, FinEje, null, null, "Eje 1", "Rasante 1"));
            var ex = Assert.Throws<ArgumentException>(() => Corredor.Rango(0, FinEje, 0, FinEje, double.NaN, null, "Eje 1", "Rasante 1"));
            Assert.Contains("'inicio' no es un número válido", ex.Message);
        }

        // ---------------------------------------------------------------- frecuencia
        [Fact]
        public void FrecuenciaObligatoriaYMayorQueCero()
        {
            Assert.Equal(10, Corredor.ValidarFrecuencia(10));
            Assert.Equal(2.5, Corredor.ValidarFrecuencia(2.5));
            var ex = Assert.Throws<ArgumentException>(() => Corredor.ValidarFrecuencia(null));
            Assert.Contains("Falta el parámetro obligatorio 'frecuencia'", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => Corredor.ValidarFrecuencia(0));
            Assert.Contains("mayor que 0", ex.Message);
            Assert.Throws<ArgumentException>(() => Corredor.ValidarFrecuencia(-5));
            Assert.Throws<ArgumentException>(() => Corredor.ValidarFrecuencia(double.NaN));
            Assert.Throws<ArgumentException>(() => Corredor.ValidarFrecuencia(double.PositiveInfinity));
        }

        [Fact]
        public void LasCuatroFrecuenciasLlevanLasClavesDeEstablecerFrecuencia()
        {
            var f = Corredor.Frecuencias(10);
            Assert.Equal(4, f.Count);
            foreach (var k in new[] { "frecuencia_tangentes", "frecuencia_curvas", "frecuencia_espirales", "frecuencia_perfil" })
                Assert.Equal(10, f[k]);
        }

        [Fact]
        public void AvisoSiLaFrecuenciaSuperaLaLongitud()
        {
            Assert.Null(Corredor.AvisoFrecuencia(10, FinEje));
            Assert.Null(Corredor.AvisoFrecuencia(FinEje, FinEje));
            Assert.Contains("mayor que la longitud del corredor (831.327 m)", Corredor.AvisoFrecuencia(1000, FinEje));
        }

        // ---------------------------------------------------------------- superficie objetivo
        [Fact]
        public void SuperficiePedidaSeDevuelveTalCual()
        {
            var avisos = new List<string>();
            Assert.Equal("Terreno", Corredor.ElegirSuperficie(" Terreno ", new List<string> { "Terreno", "Otra" }, avisos));
            Assert.Empty(avisos);
        }

        [Fact]
        public void NingunaEsSinObjetivosConAviso()
        {
            foreach (var t in new[] { "ninguna", "Ninguno", " NONE " })
            {
                var avisos = new List<string>();
                Assert.True(Corredor.EsSinObjetivo(t));
                Assert.Null(Corredor.ElegirSuperficie(t, new List<string> { "Terreno" }, avisos));
                Assert.Single(avisos);
                Assert.Contains("sin objetivos de superficie", avisos[0]);
            }
            Assert.False(Corredor.EsSinObjetivo("Terreno"));
            Assert.False(Corredor.EsSinObjetivo(null));
        }

        [Fact]
        public void SinPedirUsaLaUnicaSuperficieConAviso()
        {
            var avisos = new List<string>();
            Assert.Equal("Terreno", Corredor.ElegirSuperficie(null, new List<string> { "Terreno" }, avisos));
            Assert.Single(avisos);
            Assert.Contains("se usa 'Terreno', la única superficie", avisos[0]);
        }

        [Fact]
        public void SinPedirNiSuperficiesSeCreaSinObjetivos()
        {
            var avisos = new List<string>();
            Assert.Null(Corredor.ElegirSuperficie("", new List<string>(), avisos));
            Assert.Single(avisos);
            Assert.Contains("crear_superficie_tin", avisos[0]);
            Assert.Null(Corredor.ElegirSuperficie(null, null, null));
        }

        [Fact]
        public void SinPedirConVariasSuperficiesEsErrorConLaLista()
        {
            var ex = Assert.Throws<ArgumentException>(() => Corredor.ElegirSuperficie(null, new List<string> { "Terreno", "Terreno 2" }, new List<string>()));
            Assert.Contains("Indica 'superficie_objetivo'", ex.Message);
            Assert.Contains("2 superficies (Terreno, Terreno 2)", ex.Message);
            Assert.Contains("listar_superficies", ex.Message);
            Assert.Contains("\"ninguna\"", ex.Message);
        }

        // ---------------------------------------------------------------- acción
        [Fact]
        public void AccionDiceRangoFrecuenciaYObjetivo()
        {
            var r = Corredor.Rango(0, FinEje, 0, FinEje, null, null, "Eje 1", "Rasante 1");
            Assert.Equal("Crear el corredor 'Corredor 1' sobre el eje 'Eje 1' y la rasante 'Rasante 1' con el ensamblaje 'Ensamblaje 1', de 0 a 831.327 cada 10 m, con 'Terreno' como objetivo de los taludes, y reconstruirlo",
                Corredor.Accion("Corredor 1", "Eje 1", "Rasante 1", "Ensamblaje 1", r, 10, "Terreno"));
            Assert.Contains("sin objetivos de superficie", Corredor.Accion("Corredor 1", "Eje 1", "Rasante 1", "Ensamblaje 1", r, 10, null));
        }

        // ---------------------------------------------------------------- comprobación de lo releído
        [Fact]
        public void RangoReleidoConMargenDeUnCentimetro()
        {
            Assert.Null(Corredor.MotivoRangoNoCoincide(0, FinEje, 0.004, 831.33));
            Assert.Contains("empieza en 0.5 y se pidió 0", Corredor.MotivoRangoNoCoincide(0, FinEje, 0.5, FinEje));
            Assert.Contains("termina en 800 y se pidió 831.327", Corredor.MotivoRangoNoCoincide(0, FinEje, 0, 800));
            Assert.Contains("no son números válidos", Corredor.MotivoRangoNoCoincide(0, FinEje, double.NaN, FinEje));
        }

        [Fact]
        public void ObjetivosReleidosDebenSerLaSuperficiePedida()
        {
            var bien = new List<(string, IList<string>)>
            {
                ("BasicSideSlopeCutDitch - (Right) [TargetDTM]", new List<string> { "Terreno" }),
                ("BasicSideSlopeCutDitch - (Left) [TargetDTM]", new List<string> { "terreno " })
            };
            Assert.Null(Corredor.MotivoObjetivosNoCoinciden(bien, "Terreno"));
            Assert.Null(Corredor.MotivoObjetivosNoCoinciden(new List<(string, IList<string>)>(), "Terreno"));
            Assert.Null(Corredor.MotivoObjetivosNoCoinciden(null, "Terreno"));

            var vacio = new List<(string, IList<string>)> { ("Talud derecho", new List<string>()) };
            Assert.Contains("el objetivo de Talud derecho tiene ninguno y se pidió 'Terreno'", Corredor.MotivoObjetivosNoCoinciden(vacio, "Terreno"));
            var otro = new List<(string, IList<string>)> { ("Talud izquierdo", new List<string> { "Otra" }) };
            Assert.Contains("tiene Otra y se pidió 'Terreno'", Corredor.MotivoObjetivosNoCoinciden(otro, "Terreno"));
            var dos = new List<(string, IList<string>)> { ("Talud", new List<string> { "Terreno", "Otra" }) };
            Assert.NotNull(Corredor.MotivoObjetivosNoCoinciden(dos, "Terreno"));

            // Sin superficie pedida, todos deben quedar vacíos
            Assert.Null(Corredor.MotivoObjetivosNoCoinciden(vacio, null));
            Assert.Contains("no se pidió ninguno", Corredor.MotivoObjetivosNoCoinciden(otro, null));
        }
    }
}

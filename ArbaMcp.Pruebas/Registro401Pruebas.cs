using System;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    public class Registro401Pruebas
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 28, 10, 12, 5);

        [Fact]
        public void UnaLineaPorRutaYMinutoConElRecuentoAlCerrar()
        {
            var r = new Registro401();
            var l1 = r.Anotar("GET", "/tools", T0);
            Assert.Single(l1);
            Assert.StartsWith("MCP 401 GET /tools: token ausente o distinto", l1[0]);
            Assert.Empty(r.Anotar("GET", "/tools", T0.AddSeconds(5)));
            Assert.Empty(r.Anotar("GET", "/tools", T0.AddSeconds(10)));
            Assert.Single(r.Anotar("POST", "/execute", T0.AddSeconds(11)));   // otra ruta: su propia línea
            Assert.Empty(r.Cerrar(T0.AddSeconds(50)));                          // el minuto no ha terminado
            var cierre = r.Cerrar(T0.AddMinutes(1));
            Assert.Single(cierre);
            Assert.Equal("MCP 401 GET /tools: 3 peticiones rechazadas en el minuto 10:12", cierre[0]);   // /execute tuvo solo 1: sin resumen
            Assert.Empty(r.Cerrar(T0.AddMinutes(2)));
        }

        [Fact]
        public void ElPrimer401DelMinutoSiguienteCierraElAnterior()
        {
            var r = new Registro401();
            r.Anotar("GET", "/tools", T0);
            r.Anotar("GET", "/tools", T0.AddSeconds(1));
            var l = r.Anotar("GET", "/tools", T0.AddMinutes(1));
            Assert.Equal(2, l.Count);
            Assert.Contains("2 peticiones rechazadas en el minuto 10:12", l[0]);
            Assert.StartsWith("MCP 401 GET /tools: token ausente", l[1]);
        }
    }
}

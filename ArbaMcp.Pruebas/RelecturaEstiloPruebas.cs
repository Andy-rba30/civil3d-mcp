using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Relectura del estilo de los objetos creados (1.17.6): aviso si no se pudo releer, error (regla 4) si se releyó otro
    /// estilo. Hasta la 1.17.5 la reflexión caía en silencio al estilo previsto.
    /// </summary>
    public class RelecturaEstiloPruebas
    {
        [Fact]
        public void MismoEstiloSinMayusculasNiEspacios()
        {
            Assert.True(RelecturaEstilo.Mismo("Proposed", " proposed "));
            Assert.False(RelecturaEstilo.Mismo("Proposed", "Existing"));
            Assert.False(RelecturaEstilo.Mismo(null, "Proposed"));
        }

        [Fact]
        public void AvisoSoloSiFallo()
        {
            Assert.Null(RelecturaEstilo.Aviso("del alineamiento 'Eje 1'", null, "Proposed"));
            Assert.Equal("No se pudo releer el estilo del alineamiento 'Eje 1' (Entity.StyleId: id vacío; Entity.StyleName: vacío): 'estilo' es el previsto ('Proposed'), sin comprobar.",
                RelecturaEstilo.Aviso("del alineamiento 'Eje 1'", "Entity.StyleId: id vacío; Entity.StyleName: vacío", "Proposed"));
            Assert.EndsWith("'estilo' queda sin comprobar.", RelecturaEstilo.Aviso("de la superficie 'V'", "no es Autodesk.Civil.DatabaseServices.Entity", null));
        }

        [Fact]
        public void DiscrepanciaSoloConLosDosEstilosYDistintos()
        {
            Assert.Null(RelecturaEstilo.Discrepancia("del perfil 'Rasante 1'", "Design Profile", "design profile"));
            Assert.Null(RelecturaEstilo.Discrepancia("del perfil 'Rasante 1'", null, "Design Profile"));     // sin relectura: lo dice el aviso
            Assert.Null(RelecturaEstilo.Discrepancia("del perfil 'Rasante 1'", "Design Profile", null));     // sin estilo pedido conocido
            Assert.Equal("El estilo releído del perfil 'Rasante 1' es 'Existing Ground Profile' y se pidió 'Design Profile' (releído con Entity.StyleId y DBObject.Name): el dibujo no refleja el estilo pedido.",
                RelecturaEstilo.Discrepancia("del perfil 'Rasante 1'", "Existing Ground Profile", "Design Profile"));
        }
    }
}

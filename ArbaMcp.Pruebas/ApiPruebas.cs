using System;
using System.Collections.Generic;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    public class ApiPruebas
    {
        public enum Lado { Izquierda, Derecha }

        /// <summary>Simula un objeto de la API de Civil 3D con nombres "de 2027".</summary>
        public class RegionFalsa
        {
            public string Name { get; set; } = "R1";
            public double FrequencyAlongTangents { get; set; } = 20;
            public bool IsOutOfDate => true;
            public string SoloLectura { get; } = "fijo";
            public Lado Side { get; set; } = Lado.Izquierda;
            public double[] AdditionalStations() => new[] { 1.0, 2.0 };
            public string AddStation(double pk, string descripcion) => "añadida " + pk + " " + descripcion;
            public string AddStation(double pk) => "añadida " + pk;
            public void SinRetorno() { }
            public string Falla() => throw new InvalidOperationException("falló dentro de la API");
        }

        [Fact]
        public void LeerPruebaLosNombresAlternativosEnOrden()
        {
            var r = new RegionFalsa();
            Assert.Equal("R1", Api.Leer<string>(r, null, "DisplayName", "Name"));
            Assert.Equal("R1", Api.Leer<string>(r, null, "name"));
            Assert.Equal(20, Api.Leer<double>(r, -1, "FrequencyAlongTangents"));
            Assert.Equal(-1, Api.Leer<double>(r, -1, "NoExiste", "Tampoco"));
            Assert.True(Api.Leer<bool?>(r, null, "OutOfDate", "IsOutOfDate"));
            Assert.Equal(2, Api.Leer<double[]>(r, null, "AdditionalStations").Length);   // método sin parámetros
            Assert.Null(Api.Leer<string>(null, null, "Name"));
            Assert.True(Api.IntentarLeer(r, out object v, "Side"));
            Assert.Equal(Lado.Izquierda, v);
            Assert.False(Api.IntentarLeer(r, out _, "SinRetorno"), "un método void no es legible");
        }

        [Fact]
        public void LeerConvierteTiposYDevuelveDefSiNoConvierte()
        {
            var r = new RegionFalsa();
            Assert.Equal("20", Api.Leer<string>(r, null, "FrequencyAlongTangents"));
            Assert.Equal(20, Api.Leer<int>(r, 0, "FrequencyAlongTangents"));
            Assert.Equal(7, Api.Leer<int>(r, 7, "Name"));
        }

        [Fact]
        public void InvocarEligeElMetodoPorNombreYNumeroDeParametros()
        {
            var r = new RegionFalsa();
            Assert.Equal("añadida 12.5 MCP", Api.Invocar(r, new[] { "AddAdditionalStation", "AddStation" }, 12.5, "MCP"));
            Assert.Equal("añadida 3", Api.Invocar(r, new[] { "addstation" }, "3"));
            Assert.True(Api.IntentarInvocar(r, out object res, new[] { "AdditionalStations" }));
            Assert.Equal(2, ((double[])res).Length);
            Assert.False(Api.IntentarInvocar(r, out _, new[] { "AddStation" }, 1, 2, 3), "no hay sobrecarga de tres parámetros");
            Assert.False(Api.IntentarInvocar(null, out _, new[] { "AddStation" }, 1));
        }

        [Fact]
        public void ElMensajeDeMiembroAusenteEnumeraLosDisponibles()
        {
            var r = new RegionFalsa();
            var ex = Assert.Throws<MissingMethodException>(() => Api.Invocar(r, new[] { "Split", "SplitRegion" }, 10.0));
            Assert.Contains("La API de Civil 3D cargada no tiene ninguno de estos miembros en RegionFalsa: Split, SplitRegion", ex.Message);
            Assert.Contains("Miembros disponibles: ", ex.Message);
            Assert.Contains("AddStation()", ex.Message);
            Assert.Contains("FrequencyAlongTangents", ex.Message);
            Assert.DoesNotContain("GetHashCode", ex.Message, StringComparison.Ordinal);

            var ex2 = Assert.Throws<MissingMemberException>(() => Api.Asignar(r, 1, "NoExiste"));
            Assert.Contains("NoExiste", ex2.Message);
            Assert.Contains("Miembros disponibles", ex2.Message);
            var miembros = Api.Miembros(r);
            Assert.Contains("Name", miembros);
            Assert.Contains("AdditionalStations()", miembros);
        }

        [Fact]
        public void AsignarConvierteYRechazaSoloLectura()
        {
            var r = new RegionFalsa();
            Assert.Equal("FrequencyAlongTangents", Api.Asignar(r, "12,5".Replace(',', '.'), "FrequencyAlongTangents"));
            Assert.Equal(12.5, r.FrequencyAlongTangents);
            Assert.Equal("Side", Api.Asignar(r, "derecha", "Side"));
            Assert.Equal(Lado.Derecha, r.Side);
            var ex = Assert.Throws<ArgumentException>(() => Api.Asignar(r, "arriba", "Side"));
            Assert.Contains("Valores posibles: Izquierda, Derecha", ex.Message);
            var ro = Assert.Throws<InvalidOperationException>(() => Api.Asignar(r, "x", "SoloLectura"));
            Assert.Contains("solo lectura", ro.Message);
        }

        [Fact]
        public void LasExcepcionesDeLaApiLleganSinEnvoltorio()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Api.Invocar(new RegionFalsa(), new[] { "Falla" }));
            Assert.Equal("falló dentro de la API", ex.Message);
        }

        [Fact]
        public void TieneYLista()
        {
            var r = new RegionFalsa();
            Assert.True(Api.Tiene(r, "name"));
            Assert.True(Api.Tiene(r, "AddStation"));
            Assert.False(Api.Tiene(r, "Split"));
            Assert.False(Api.Tiene(null, "Name"));
            Assert.Equal(2, Api.Lista(new List<int> { 1, 2 }).Count);
            Assert.Empty(Api.Lista("texto"));
            Assert.Empty(Api.Lista(null));
        }
    }
}

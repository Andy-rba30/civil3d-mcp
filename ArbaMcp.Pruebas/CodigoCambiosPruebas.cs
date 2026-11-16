using System;
using System.Collections.Generic;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Qué creó, modificó o borró un script de ejecutar_codigo (1.17.5): combinación de las fuentes y recuento por fuente.
    /// Las claves son textos ("linea", "modelo") en lugar de ObjectId; el plugin usa la misma clase con ObjectId.
    /// </summary>
    public class CodigoCambiosPruebas
    {
        private static Dictionary<string, object> Fuente(Dictionary<string, object> informe, string nombre)
            => Assert.IsType<Dictionary<string, object>>(informe[nombre]);

        [Fact]
        public void CasoDeLaValidacion116_LaLineaDeFAgregarCuentaAunqueLasOtrasFuentesNoVeanNada()
        {
            // Informe de VALIDACION_116, pasos 4b y 5a: eventos y transacción del plugin sin nada; ahora F.Agregar anota la línea
            var c = new CodigoCambios<string>();
            c.Anotar(FuenteCambio.Fachada, "linea", nuevo: true, borrado: false);
            c.Leidos(FuenteCambio.TransaccionPlugin, 0);

            var r = c.Clasificar();
            Assert.Equal(new[] { "linea" }, r.Creados);
            Assert.Empty(r.Modificados);
            Assert.Empty(r.Borrados);
            Assert.Equal(0, r.Temporales);

            var informe = c.Informe(r, "al abortar");
            Assert.Equal("fachada", informe["creados_por"]);
            Assert.Equal(1, Fuente(informe, FuenteCambio.Fachada)["creados"]);
            Assert.Equal(0, Fuente(informe, FuenteCambio.TransaccionPlugin)["objetos"]);
            Assert.Equal(0, Fuente(informe, FuenteCambio.TransaccionPlugin)["creados"]);
            Assert.Null(Fuente(informe, FuenteCambio.TransaccionScript)["objetos"]);   // no se leyó: null, no 0
            Assert.Equal("al abortar", Fuente(informe, FuenteCambio.EventosAlCerrar)["momento"]);
        }

        [Fact]
        public void FachadaYTransaccionDelScript_LaLineaCreadaYElEspacioModeloModificado()
        {
            var c = new CodigoCambios<string>();
            c.Anotar(FuenteCambio.Fachada, "linea", nuevo: true, borrado: false);
            c.Leidos(FuenteCambio.TransaccionScript, 3);
            c.Anotar(FuenteCambio.TransaccionScript, "tabla", borrado: false);
            c.Anotar(FuenteCambio.TransaccionScript, "modelo", modificado: true, borrado: false);
            c.Anotar(FuenteCambio.TransaccionScript, "linea", nuevo: true, modificado: true, borrado: false);

            var r = c.Clasificar();
            Assert.Equal(new[] { "linea" }, r.Creados);
            Assert.Equal(new[] { "modelo" }, r.Modificados);   // la tabla de bloques solo se leyó

            var informe = c.Informe(r, "al confirmar");
            Assert.Equal("fachada, transaccion_script", informe["creados_por"]);
            var ts = Fuente(informe, FuenteCambio.TransaccionScript);
            Assert.Equal(3, ts["objetos"]);
            Assert.Equal(1, ts["creados"]);
            Assert.Equal(2, ts["modificados"]);   // cada fuente cuenta sus marcas: la línea nueva también sale modificada
            Assert.Equal(0, ts["borrados"]);
        }

        [Fact]
        public void CreadoYBorradoEnElMismoScript_EsTemporal()
        {
            var c = new CodigoCambios<string>();
            c.Anotar(FuenteCambio.Fachada, "linea", nuevo: true, borrado: false);
            c.Anotar(FuenteCambio.TransaccionScript, "linea", nuevo: true, borrado: true);

            var r = c.Clasificar();
            Assert.Empty(r.Creados);
            Assert.Empty(r.Borrados);
            Assert.Equal(1, r.Temporales);
            Assert.Equal("ninguna", c.Informe(r, "al confirmar")["creados_por"]);
        }

        [Fact]
        public void Borrado_EsElUltimoEstadoConocido()
        {
            // Los eventos ven borrar y recuperar; la transacción, leída después, dice que no está borrado
            var c = new CodigoCambios<string>();
            c.Anotar(FuenteCambio.Eventos, "circulo", modificado: true, borrado: true);
            c.Anotar(FuenteCambio.Eventos, "circulo", borrado: false);
            c.Anotar(FuenteCambio.Eventos, "texto", borrado: true);
            c.Anotar(FuenteCambio.TransaccionPlugin, "circulo", modificado: true, borrado: false);

            var r = c.Clasificar();
            Assert.Equal(new[] { "circulo" }, r.Modificados);
            Assert.Equal(new[] { "texto" }, r.Borrados);
        }

        [Fact]
        public void UnaFuenteSinDatoDeBorrado_NoCambiaElEstado()
        {
            var c = new CodigoCambios<string>();
            c.Anotar(FuenteCambio.Eventos, "texto", borrado: true);
            c.Anotar(FuenteCambio.Fachada, "texto");   // borrado: null
            Assert.Equal(new[] { "texto" }, c.Clasificar().Borrados);
        }

        [Fact]
        public void EventosAlCerrar_SeCuentanPeroNoClasifican_YSeDicenLosQueNadieMasVio()
        {
            var c = new CodigoCambios<string>();
            c.Anotar(FuenteCambio.Fachada, "linea", nuevo: true, borrado: false);
            c.Anotar(FuenteCambio.EventosAlCerrar, "linea", nuevo: true);
            c.Anotar(FuenteCambio.EventosAlCerrar, "eje", nuevo: true);
            c.Anotar(FuenteCambio.EventosAlCerrar, "modelo", modificado: true);

            var r = c.Clasificar();
            Assert.Equal(new[] { "linea" }, r.Creados);
            Assert.Empty(r.Modificados);
            Assert.Equal(new[] { "eje" }, c.NuevosSoloAlCerrar());

            var informe = c.Informe(r, "al confirmar");
            Assert.Equal("fachada", informe["creados_por"]);   // eventos_al_cerrar no cuenta en creados_por
            var cierre = Fuente(informe, FuenteCambio.EventosAlCerrar);
            Assert.Equal("al confirmar", cierre["momento"]);
            Assert.Equal(2, cierre["creados"]);
            Assert.Equal(1, cierre["modificados"]);
            Assert.False(cierre.ContainsKey("objetos"));   // no lee una transacción
        }

        [Fact]
        public void Errores_SeCuentanConElPrimero_YNoLeidaSoloEnLasDeTransaccion()
        {
            var c = new CodigoCambios<string>();
            c.Error(FuenteCambio.TransaccionPlugin, "eNotOpenForRead");
            c.Error(FuenteCambio.TransaccionPlugin, "otro");
            c.Error(FuenteCambio.Eventos, "");
            c.NoLeida(FuenteCambio.TransaccionScript, "el script cerró F.Tr");

            var informe = c.Informe(c.Clasificar(), "desactivados");
            var tp = Fuente(informe, FuenteCambio.TransaccionPlugin);
            Assert.Equal(2, tp["errores"]);
            Assert.Equal("eNotOpenForRead", tp["primer_error"]);
            Assert.Equal("(sin mensaje)", Fuente(informe, FuenteCambio.Eventos)["primer_error"]);
            Assert.Equal("el script cerró F.Tr", Fuente(informe, FuenteCambio.TransaccionScript)["no_leida"]);
            Assert.False(Fuente(informe, FuenteCambio.Fachada).ContainsKey("primer_error"));
            Assert.Equal(0, Fuente(informe, FuenteCambio.Fachada)["errores"]);
        }

        [Fact]
        public void Informe_TieneTodasLasFuentesEnOrden()
        {
            var informe = new CodigoCambios<string>().Informe(null, "al abortar");
            Assert.Equal(new[] { "creados_por", "fachada", "transaccion_script", "transaccion_plugin", "eventos", "eventos_al_cerrar" }, informe.Keys);
            Assert.Equal("ninguna", informe["creados_por"]);
        }

        [Fact]
        public void FuenteDesconocida_Falla()
        {
            var c = new CodigoCambios<string>();
            Assert.Throws<ArgumentException>(() => c.Anotar("otra", "x", nuevo: true));
        }

        [Fact]
        public void Clasificacion_EnElOrdenEnQueSeVioCadaObjeto()
        {
            var c = new CodigoCambios<int>();
            c.Anotar(FuenteCambio.Eventos, 30, nuevo: true);
            c.Anotar(FuenteCambio.Fachada, 10, nuevo: true);
            c.Anotar(FuenteCambio.TransaccionScript, 20, nuevo: true);
            c.Anotar(FuenteCambio.TransaccionScript, 30, nuevo: true);
            Assert.Equal(new[] { 30, 10, 20 }, c.Clasificar().Creados);
        }

        [Fact]
        public void Undoctl_EnPalabras()
        {
            Assert.Null(CodigoDeshacer.DescribirUndoctl(null));
            Assert.Equal("deshacer desactivado", CodigoDeshacer.DescribirUndoctl(0));
            Assert.Equal("deshacer activado; Auto", CodigoDeshacer.DescribirUndoctl(5));
            string t = CodigoDeshacer.DescribirUndoctl(1 + 4 + 8 + 16);
            Assert.StartsWith("deshacer activado; Auto; un grupo de deshacer activo", t);
            Assert.EndsWith("zoom y encuadre agrupados", t);
            Assert.Contains("solo se puede deshacer un comando", CodigoDeshacer.DescribirUndoctl(3));
            Assert.Contains("propiedades de capa agrupadas", CodigoDeshacer.DescribirUndoctl(33));
        }

        [Fact]
        public void Undoctl_AvisaSoloSiElDeshacerNoQuitaElCambio()
        {
            Assert.Null(CodigoDeshacer.Aviso(null));
            Assert.Contains("desactivado (UNDOCTL=0)", CodigoDeshacer.Aviso(0));
            Assert.Contains("un solo comando (UNDOCTL=3)", CodigoDeshacer.Aviso(3));
            Assert.Null(CodigoDeshacer.Aviso(5));
            Assert.Null(CodigoDeshacer.Aviso(1 + 4 + 8));   // un grupo activo puede ser el del propio pseudocomando
            Assert.Null(CodigoDeshacer.Aviso(21));
        }
    }
}

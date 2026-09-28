using System;
using System.Threading;
using System.Threading.Tasks;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    public class PendientePruebas
    {
        [Fact]
        public void ReclamarDosVecesSoloFuncionaLaPrimera()
        {
            var p = new Pendiente(() => 1, ContextoEjecucion.Documento, "x");
            Assert.True(p.EstaEnCola);
            Assert.True(p.Reclamar());
            Assert.True(p.EstaEjecutando);
            Assert.False(p.Reclamar());
            Assert.False(p.Descartar(), "reclamado no se puede descartar");
            Assert.NotNull(p.MsEspera);
            Assert.Null(p.MsEjecucion);
        }

        [Fact]
        public void DescartarTrasReclamarNoDescartaYDescartadoNoSeReclama()
        {
            var p = new Pendiente(() => 1, ContextoEjecucion.Documento, "x");
            Assert.True(p.Descartar());
            Assert.True(p.EstaDescartado);
            Assert.False(p.Descartar(), "solo se descarta una vez");
            Assert.False(p.Reclamar());
            p.Correr();
            Assert.True(p.Tarea.IsCanceled, "un trabajo descartado nunca se ejecuta");
        }

        [Fact]
        public async Task CorrerEntregaResultadoOExcepcionYMideTiempos()
        {
            var ok = new Pendiente(() => { Thread.Sleep(20); return "listo"; }, ContextoEjecucion.Inmediato, "ok");
            Thread.Sleep(15);
            ok.Correr();
            Assert.Equal("listo", await ok.Tarea);
            Assert.True(ok.MsEspera >= 10, "esperó al menos lo que dormimos antes de correr: " + ok.MsEspera);
            Assert.True(ok.MsEjecucion >= 15, "la ejecución duró al menos el Sleep: " + ok.MsEjecucion);

            var mal = new Pendiente(() => throw new InvalidOperationException("falló"), ContextoEjecucion.Inmediato, "mal");
            mal.Correr();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => mal.Tarea);
            Assert.Equal("falló", ex.Message);
        }

        [Fact]
        public void DescribirTiempoAgotadoDistingueLosTresCasos()
        {
            Assert.Contains("pudo haberse enviado", Pendiente.DescribirTiempoAgotado(null));
            var enCola = new Pendiente(() => 1, ContextoEjecucion.Documento, "a");
            Assert.Contains("se ha descartado: no se ejecutó", Pendiente.DescribirTiempoAgotado(enCola));
            Assert.True(enCola.EstaDescartado);
            var corriendo = new Pendiente(() => 1, ContextoEjecucion.Documento, "b");
            corriendo.Reclamar();
            Assert.Contains("empezó a ejecutarla y sigue ocupado", Pendiente.DescribirTiempoAgotado(corriendo));
            Assert.True(corriendo.EstaEjecutando);
        }
    }

    public class PlanificadorPruebas
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 28, 10, 0, 0);

        [Fact]
        public void LosInmediatosSalenAparteYSinEsperar()
        {
            var plan = new Planificador();
            var doc = plan.Encolar(() => 1, ContextoEjecucion.Documento, "doc");
            var inm = plan.Encolar(() => 2, ContextoEjecucion.Inmediato, "inm");
            Assert.True(plan.HayTrabajo);
            var inmediatos = plan.TomarInmediatos();
            Assert.Single(inmediatos);
            Assert.Same(inm, inmediatos[0]);
            Assert.Equal(1, plan.Encolados);
            Assert.Empty(plan.TomarInmediatos());
            Assert.Same(doc, plan.Decidir(true, null, T0).Trabajo);
        }

        [Fact]
        public void SinCivilLibreSeReintentaYElAvisoSeDaUnaSolaVez()
        {
            var plan = new Planificador();
            var p = plan.Encolar(() => 1, ContextoEjecucion.Documento, "listar_alineamientos");
            var d1 = plan.Decidir(false, "hay un comando activo en Civil 3D (CMDACTIVE=1)", T0);
            Assert.Equal(AccionPlanificada.Reintentar, d1.Accion);
            Assert.Equal("MCP ⏳ listar_alineamientos espera: hay un comando activo en Civil 3D (CMDACTIVE=1)", d1.Aviso);
            var d2 = plan.Decidir(false, "hay un comando activo en Civil 3D (CMDACTIVE=1)", T0.AddSeconds(1));
            Assert.Equal(AccionPlanificada.Reintentar, d2.Accion);
            Assert.Null(d2.Aviso);
            Assert.True(p.EstaEnCola, "sigue en cola sin ejecutarse");
            var d3 = plan.Decidir(true, null, T0.AddSeconds(2));
            Assert.Equal(AccionPlanificada.Ejecutar, d3.Accion);
            Assert.Same(p, d3.Trabajo);
            Assert.Equal(AccionPlanificada.Nada, plan.Decidir(true, null, T0).Accion);
        }

        [Fact]
        public void DescartePorTiempo_ElTrabajoDescartadoNoSeEjecutaYSeCancela()
        {
            var plan = new Planificador();
            var lento = plan.Encolar(() => 1, ContextoEjecucion.Documento, "lento");
            var otro = plan.Encolar(() => 2, ContextoEjecucion.Documento, "otro");
            // Civil 3D ocupado: el servidor agota el tiempo y descarta el primero
            Assert.Equal(AccionPlanificada.Reintentar, plan.Decidir(false, "ocupado", T0).Accion);
            Assert.Contains("se ha descartado", Pendiente.DescribirTiempoAgotado(lento));
            // Al liberarse, el descartado sale de la cola cancelado y se ejecuta el siguiente
            var d = plan.Decidir(true, null, T0.AddSeconds(30));
            Assert.Equal(AccionPlanificada.Ejecutar, d.Accion);
            Assert.Same(otro, d.Trabajo);
            Assert.True(lento.Tarea.IsCanceled);
            Assert.False(lento.EstaEjecutando);
        }

        [Fact]
        public void ConUnTrabajoDeDocumentoEnCursoSeEsperaHastaQueTermine()
        {
            var plan = new Planificador();
            var primero = plan.Encolar(() => 1, ContextoEjecucion.Documento, "primero");
            var segundo = plan.Encolar(() => 2, ContextoEjecucion.Documento, "segundo");
            var d = plan.Decidir(true, null, T0);
            Assert.Same(primero, d.Trabajo);
            plan.MarcarEnCurso(primero, T0);
            Assert.Same(primero, plan.EnCurso);
            Assert.Equal(AccionPlanificada.Reintentar, plan.Decidir(true, null, T0.AddMinutes(1)).Accion);
            primero.Correr();
            plan.Terminar(primero);
            Assert.Null(plan.EnCurso);
            Assert.NotNull(primero.MsEjecucion);
            Assert.Same(segundo, plan.Decidir(true, null, T0.AddMinutes(1)).Trabajo);
        }

        [Fact]
        public void UnTrabajoEnCursoDescartadoYSinArrancarSeAbandonaPasadoElMaximo()
        {
            var plan = new Planificador { MaximoEnCurso = TimeSpan.FromMinutes(10) };
            var colgado = plan.Encolar(() => 1, ContextoEjecucion.Documento, "colgado");
            var siguiente = plan.Encolar(() => 2, ContextoEjecucion.Documento, "siguiente");
            plan.Decidir(true, null, T0);
            plan.MarcarEnCurso(colgado, T0);
            Assert.True(colgado.Descartar(), "el servidor agotó el tiempo antes de que arrancara");
            Assert.Equal(AccionPlanificada.Reintentar, plan.Decidir(true, null, T0.AddMinutes(9)).Accion);
            var d = plan.Decidir(true, null, T0.AddMinutes(11));
            Assert.Equal(AccionPlanificada.Ejecutar, d.Accion);
            Assert.Same(siguiente, d.Trabajo);
            Assert.Contains("se deja de esperar por 'colgado'", d.Aviso);
            Assert.Null(plan.EnCurso);
        }

        [Fact]
        public void VaciarCancelaTodo()
        {
            var plan = new Planificador();
            var a = plan.Encolar(() => 1, ContextoEjecucion.Documento, "a");
            var b = plan.Encolar(() => 2, ContextoEjecucion.Inmediato, "b");
            plan.Vaciar();
            Assert.False(plan.HayTrabajo);
            Assert.True(a.Tarea.IsCanceled && b.Tarea.IsCanceled);
            Assert.True(a.EstaDescartado && b.EstaDescartado);
        }
    }
}

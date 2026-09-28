using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ArbaMcp.Nucleo;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ArbaMcp
{
    /// <summary>
    /// Cola de trabajos que se ejecutan en el hilo principal de AutoCAD. El servidor HTTP corre en otros hilos y nunca
    /// toca la API de AutoCAD directamente: encola aquí y espera la tarea.
    ///
    /// Cómo llega el trabajo al hilo principal: por el Dispatcher de WPF de ese hilo (el mismo que usa la cinta), que
    /// AutoCAD atiende en su bucle de mensajes incluso mientras un comando espera entrada; el evento Idle queda como
    /// respaldo y como reintento natural. Un trabajo de contexto Documento se entrega además a
    /// ExecuteInCommandContextAsync, que lo ejecuta en el contexto de comando del dibujo activo.
    ///
    /// La política (qué trabajo va antes, cuándo se espera, cuándo se descarta) está en ArbaMcp.Nucleo.Planificador y
    /// se prueba sin Civil 3D; aquí solo queda lo que necesita AutoCAD: el despachador, la ventana principal, CMDACTIVE
    /// y el contexto de comando. Los trabajos Documento y Aplicacion esperan a que Civil 3D esté libre (CMDACTIVE = 0
    /// y ventana principal habilitada, es decir, sin cuadro de diálogo modal) y se ejecutan de uno en uno; los Inmediato
    /// se atienden siempre y por delante. Mientras un trabajo espera, el servidor puede descartarlo (tiempo agotado) y
    /// entonces no se ejecuta a destiempo cuando Civil 3D se libere.
    /// </summary>
    internal static class HiloPrincipal
    {
        private static readonly Planificador Plan = new Planificador();

        private static Dispatcher _despachador;
        private static int _idHiloPrincipal = -1;
        private static IntPtr _ventanaPrincipal;
        private static bool _enganchado;
        private static int _atendiendo;          // evita reentrar en Atender (el despachador y el Idle pueden coincidir)
        private static int _reintentoProgramado;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        public static bool Iniciado => _enganchado;

        /// <summary>True si el hilo actual es el principal de AutoCAD.</summary>
        public static bool EnHiloPrincipal => Environment.CurrentManagedThreadId == _idHiloPrincipal;

        /// <summary>
        /// True mientras el hilo principal ejecuta un trabajo de contexto Documento. Dentro de él CMDACTIVE es distinto
        /// de cero por el propio pseudocomando de ExecuteInCommandContextAsync, no por un comando del usuario.
        /// </summary>
        public static bool EnTrabajoDeDocumento
        {
            get
            {
                var p = Plan.EnCurso;
                return EnHiloPrincipal && p != null && p.EstaEjecutando;
            }
        }

        /// <summary>Trabajo de contexto Documento que se está ejecutando en este momento (null si ninguno).</summary>
        public static Pendiente TrabajoEnCurso
        {
            get
            {
                var p = Plan.EnCurso;
                return p != null && p.EstaEjecutando ? p : null;
            }
        }

        /// <summary>Se llama desde el hilo principal (Initialize del plugin o el comando ARBAMCP).</summary>
        public static void Iniciar()
        {
            if (_enganchado) return;
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Historial.Registrar("HiloPrincipal.Iniciar se llamó fuera del hilo principal; no se inicia.");
                return;
            }
            _idHiloPrincipal = Environment.CurrentManagedThreadId;
            _despachador = Dispatcher.CurrentDispatcher;
            CapturarVentanaPrincipal();
            AcApp.Idle += AlEstarInactivo;
            _enganchado = true;
            Historial.Registrar("HiloPrincipal listo: despachador " + (_despachador != null ? "sí" : "no")
                + ", ventana principal " + (_ventanaPrincipal != IntPtr.Zero ? "sí" : "todavía no"));
        }

        public static void Detener()
        {
            if (!_enganchado) return;
            AcApp.Idle -= AlEstarInactivo;
            _enganchado = false;
            Plan.Vaciar();
        }

        // Solo en el hilo principal. Al cargar el plugin la ventana puede no existir aún; se reintenta al atender.
        private static void CapturarVentanaPrincipal()
        {
            if (_ventanaPrincipal != IntPtr.Zero) return;
            try { _ventanaPrincipal = AcApp.MainWindow?.Handle ?? IntPtr.Zero; }
            catch { _ventanaPrincipal = IntPtr.Zero; }
        }

        /// <summary>Encola una función y devuelve una tarea que se completa cuando el hilo principal la ejecuta.</summary>
        public static Task<object> Ejecutar(Func<object> funcion, ContextoEjecucion contexto = ContextoEjecucion.Documento, string nombre = null)
            => Encolar(funcion, contexto, nombre).Tarea;

        /// <summary>Como Ejecutar, pero devuelve el trabajo para poder descartarlo si sigue en cola y leer sus tiempos.</summary>
        public static Pendiente Encolar(Func<object> funcion, ContextoEjecucion contexto = ContextoEjecucion.Documento, string nombre = null)
        {
            var p = Plan.Encolar(funcion, contexto, nombre);
            Despertar();
            return p;
        }

        /// <summary>
        /// Acción de contexto Inmediato: en línea si ya estamos en el hilo principal (por ejemplo dentro de un evento
        /// de comando); si no, encolada. Así nunca se toca AutoCAD desde otro hilo.
        /// </summary>
        public static Task EjecutarInmediato(Action accion)
        {
            if (EnHiloPrincipal)
            {
                accion();
                return Task.CompletedTask;
            }
            return Ejecutar(() => { accion(); return null; }, ContextoEjecucion.Inmediato);
        }

        /// <summary>Pide al hilo principal que atienda las colas cuanto antes. Se puede llamar desde cualquier hilo.</summary>
        private static void Despertar()
        {
            var d = _despachador;
            if (d != null && !d.HasShutdownStarted)
            {
                try
                {
                    d.BeginInvoke(DispatcherPriority.Normal, new Action(Atender));
                    return;
                }
                catch (System.Exception ex)
                {
                    Historial.Registrar("HiloPrincipal: falló el despachador, se usa el respaldo por Idle: " + ex.Message);
                }
            }
            // Respaldo: un mensaje vacío a la ventana principal para que el bucle de mensajes dispare Idle cuanto antes
            if (_ventanaPrincipal != IntPtr.Zero)
            {
                try { PostMessage(_ventanaPrincipal, 0 /* WM_NULL */, IntPtr.Zero, IntPtr.Zero); } catch { }
            }
        }

        // Vuelve a llamar a Atender pasado EsperaReintento (una sola programación a la vez)
        private static void ProgramarReintento()
        {
            if (Interlocked.Exchange(ref _reintentoProgramado, 1) != 0) return;
            Task.Delay(Planificador.EsperaReintento).ContinueWith(_ =>
            {
                Interlocked.Exchange(ref _reintentoProgramado, 0);
                Despertar();
            }, TaskScheduler.Default);
        }

        private static void AlEstarInactivo(object sender, EventArgs e) => Atender();

        /// <summary>Atiende las colas. Solo en el hilo principal y una ejecución a la vez.</summary>
        private static void Atender()
        {
            if (!EnHiloPrincipal) return;
            if (Interlocked.Exchange(ref _atendiendo, 1) != 0) return;
            try
            {
                CapturarVentanaPrincipal();

                // 1. Inmediatos: siempre, aunque Civil 3D esté ocupado o haya un trabajo de documento en curso
                foreach (var inmediato in Plan.TomarInmediatos()) inmediato.Correr();

                // 2. Documento y Aplicacion: la política del planificador decide (descartados, trabajo en curso, Civil 3D libre)
                if (Plan.Encolados == 0) return;
                bool libre = CivilLibre(out string motivo);
                var decision = Plan.Decidir(libre, motivo, DateTime.UtcNow);
                if (decision.Aviso != null)
                    foreach (var linea in decision.Aviso.Split('\n')) Historial.Registrar(linea);

                if (decision.Accion == AccionPlanificada.Nada) return;
                if (decision.Accion == AccionPlanificada.Reintentar) { ProgramarReintento(); return; }

                var trabajo = decision.Trabajo;
                if (trabajo.Contexto == ContextoEjecucion.Documento && AcApp.DocumentManager.MdiActiveDocument != null)
                    EntregarAlDocumento(trabajo);
                else
                    trabajo.Correr(); // Aplicacion, o Documento sin dibujo abierto (la herramienta responderá que no hay dibujo)

                if (Plan.HayTrabajo) Despertar();
            }
            finally
            {
                Interlocked.Exchange(ref _atendiendo, 0);
            }
        }

        /// <summary>True si Civil 3D está libre: sin cuadro de diálogo modal (ventana principal habilitada) y sin comando activo.</summary>
        private static bool CivilLibre(out string motivo)
        {
            motivo = null;
            if (_ventanaPrincipal != IntPtr.Zero && !IsWindowEnabled(_ventanaPrincipal))
            {
                motivo = "hay un cuadro de diálogo abierto en Civil 3D";
                return false;
            }
            int cmd = 0;
            try { cmd = Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")); } catch { }
            if (cmd != 0)
            {
                motivo = "hay un comando activo en Civil 3D (CMDACTIVE=" + cmd + ")";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Entrega un trabajo al contexto de comando del dibujo activo sin bloquear el hilo principal. El propio
        /// delegado avisa al terminar (Terminar), así no dependemos del valor devuelto por ExecuteInCommandContextAsync,
        /// que cambia de tipo entre versiones de AutoCAD (Task en 2015-2024, DocumentCollection.ExecutionResult en 2027).
        /// Si además devuelve algo con una tarea dentro, se usa para detectar que AutoCAD falló antes de ejecutarlo.
        /// </summary>
        private static void EntregarAlDocumento(Pendiente p)
        {
            Plan.MarcarEnCurso(p, DateTime.UtcNow);
            try
            {
                object devuelto = AcApp.DocumentManager.ExecuteInCommandContextAsync(_ =>
                {
                    try { p.Correr(); }
                    finally { Terminar(p); }
                    return Task.CompletedTask;
                }, null);

                Task tarea = ExtraerTarea(devuelto);
                if (tarea != null)
                {
                    tarea.ContinueWith(t =>
                    {
                        if (!t.IsFaulted) return;
                        p.Resultado.TrySetException(t.Exception?.GetBaseException() ?? new System.Exception("ExecuteInCommandContextAsync falló"));
                        Terminar(p);
                    }, TaskScheduler.Default);
                }
            }
            catch (System.Exception ex)
            {
                p.Resultado.TrySetException(ex);
                Terminar(p);
            }
        }

        // Busca una Task en lo que devuelve ExecuteInCommandContextAsync: la propia Task, o una propiedad/método
        // sin parámetros de tipo Task. Devuelve null si no hay ninguna (entonces basta con el aviso del delegado).
        private static Task ExtraerTarea(object devuelto)
        {
            if (devuelto == null) return null;
            if (devuelto is Task t) return t;
            try
            {
                var tipo = devuelto.GetType();
                foreach (var prop in tipo.GetProperties())
                    if (typeof(Task).IsAssignableFrom(prop.PropertyType) && prop.GetIndexParameters().Length == 0)
                        return prop.GetValue(devuelto) as Task;
                foreach (var met in tipo.GetMethods())
                    if (typeof(Task).IsAssignableFrom(met.ReturnType) && met.GetParameters().Length == 0 && !met.IsSpecialName)
                        return met.Invoke(devuelto, null) as Task;
            }
            catch { }
            return null;
        }

        private static void Terminar(Pendiente p)
        {
            Plan.Terminar(p);
            Despertar();
        }
    }
}

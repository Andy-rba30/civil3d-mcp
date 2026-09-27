using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ArbaMcp
{
    /// <summary>
    /// Dónde y cuándo se ejecuta una herramienta dentro de Civil 3D. Siempre en el hilo principal; lo que cambia es
    /// el contexto de AutoCAD y si se espera a que Civil 3D esté libre (sin comando activo ni cuadro de diálogo).
    /// </summary>
    public enum ContextoEjecucion
    {
        /// <summary>
        /// Contexto de comando del dibujo activo (DocumentManager.ExecuteInCommandContextAsync): la herramienta corre
        /// como si fuera un comando, con el documento bloqueado y la actualización de gráficos al terminar. Espera a
        /// que Civil 3D esté libre. Valor por defecto y el único válido para leer o modificar el dibujo.
        /// </summary>
        Documento = 0,

        /// <summary>
        /// Contexto de aplicación en el hilo principal, esperando también a que Civil 3D esté libre. Para lo que no
        /// admite contexto de comando: abrir o activar dibujos, enviar una orden a la línea de comandos.
        /// </summary>
        Aplicacion = 1,

        /// <summary>
        /// Contexto de aplicación en el hilo principal, sin esperar: corre aunque haya un comando activo o un cuadro
        /// de diálogo abierto, y nunca se queda detrás de otros trabajos. Solo para acciones que no tocan la base de
        /// datos del dibujo: enviar teclas (ESC), leer variables de sistema, capturar la pantalla, leer el historial.
        /// </summary>
        Inmediato = 2
    }

    /// <summary>
    /// Cola de trabajos que se ejecutan en el hilo principal de AutoCAD. El servidor HTTP corre en otros hilos y nunca
    /// toca la API de AutoCAD directamente: encola aquí y espera la tarea.
    ///
    /// Cómo llega el trabajo al hilo principal: por el Dispatcher de WPF de ese hilo (el mismo que usa la cinta), que
    /// AutoCAD atiende en su bucle de mensajes incluso mientras un comando espera entrada; el evento Idle queda como
    /// respaldo y como reintento natural. Un trabajo de contexto Documento se entrega además a
    /// ExecuteInCommandContextAsync, que lo ejecuta en el contexto de comando del dibujo activo.
    ///
    /// Reglas: los trabajos Documento y Aplicacion esperan a que Civil 3D esté libre (CMDACTIVE = 0 y ventana
    /// principal habilitada, es decir, sin cuadro de diálogo modal) y se ejecutan de uno en uno; los Inmediato se
    /// atienden siempre y por delante. Mientras un trabajo espera, el servidor puede descartarlo (tiempo agotado) y
    /// entonces no se ejecuta a destiempo cuando Civil 3D se libere.
    /// </summary>
    internal static class HiloPrincipal
    {
        internal sealed class Pendiente
        {
            private const int EnCola = 0, Ejecutando = 1, Descartado = 2;
            private int _estado = EnCola;

            internal Func<object> Funcion;
            internal ContextoEjecucion Contexto;
            internal string Nombre = "";
            internal bool AvisoEsperaDado;
            internal readonly TaskCompletionSource<object> Resultado =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<object> Tarea => Resultado.Task;

            /// <summary>Descarta el trabajo si aún no empezó. Devuelve true si se descartó (no se ejecutará).</summary>
            public bool Descartar() => Interlocked.CompareExchange(ref _estado, Descartado, EnCola) == EnCola;

            /// <summary>Lo reclama el hilo principal justo antes de ejecutarlo. False si ya fue descartado.</summary>
            internal bool Reclamar() => Interlocked.CompareExchange(ref _estado, Ejecutando, EnCola) == EnCola;

            internal bool EstaDescartado => Volatile.Read(ref _estado) == Descartado;
            internal bool EstaEjecutando => Volatile.Read(ref _estado) == Ejecutando;
        }

        /// <summary>Cada cuánto se vuelve a comprobar si Civil 3D quedó libre mientras hay trabajos esperando.</summary>
        public static readonly TimeSpan EsperaReintento = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Si un trabajo entregado al contexto de comando no arranca en este tiempo y el servidor ya lo descartó,
        /// se deja de esperar por él (por ejemplo, porque se cerró el dibujo antes de que AutoCAD lo atendiera).
        /// </summary>
        public static readonly TimeSpan MaximoEnCurso = TimeSpan.FromMinutes(10);

        private static readonly ConcurrentQueue<Pendiente> Cola = new ConcurrentQueue<Pendiente>();          // Documento y Aplicacion, en orden de llegada
        private static readonly ConcurrentQueue<Pendiente> ColaInmediata = new ConcurrentQueue<Pendiente>(); // Inmediato: nunca esperan detrás de los demás

        private static Dispatcher _despachador;
        private static int _idHiloPrincipal = -1;
        private static IntPtr _ventanaPrincipal;
        private static bool _enganchado;
        private static Pendiente _enCurso;       // trabajo Documento entregado a ExecuteInCommandContextAsync y aún sin terminar
        private static DateTime _inicioEnCurso;
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
                var p = Volatile.Read(ref _enCurso);
                return EnHiloPrincipal && p != null && p.EstaEjecutando;
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
            Vaciar(ColaInmediata);
            Vaciar(Cola);
        }

        private static void Vaciar(ConcurrentQueue<Pendiente> cola)
        {
            while (cola.TryDequeue(out var p))
            {
                p.Descartar();
                p.Resultado.TrySetCanceled();
            }
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

        /// <summary>Como Ejecutar, pero devuelve el trabajo para poder descartarlo si sigue en cola.</summary>
        public static Pendiente Encolar(Func<object> funcion, ContextoEjecucion contexto = ContextoEjecucion.Documento, string nombre = null)
        {
            var p = new Pendiente { Funcion = funcion, Contexto = contexto, Nombre = nombre ?? "" };
            if (contexto == ContextoEjecucion.Inmediato) ColaInmediata.Enqueue(p);
            else Cola.Enqueue(p);
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
            Task.Delay(EsperaReintento).ContinueWith(_ =>
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
                while (ColaInmediata.TryDequeue(out var inmediato)) Correr(inmediato);

                // 2. Fuera de la cabeza los trabajos que el servidor ya dio por agotados
                while (Cola.TryPeek(out var cabeza) && cabeza.EstaDescartado && Cola.TryDequeue(out var descartado))
                    descartado.Resultado.TrySetCanceled();
                if (!Cola.TryPeek(out var siguiente)) return;

                // 3. Un trabajo de documento sigue en curso (en cola de AutoCAD o ejecutándose): esperar a que termine
                var enCurso = Volatile.Read(ref _enCurso);
                if (enCurso != null)
                {
                    if (!enCurso.EstaDescartado || DateTime.UtcNow - _inicioEnCurso < MaximoEnCurso)
                    {
                        ProgramarReintento();
                        return;
                    }
                    // El servidor ya lo descartó y el contexto de comando nunca lo arrancó: se deja de esperar por él
                    Historial.Registrar("HiloPrincipal: se deja de esperar por '" + enCurso.Nombre + "' (descartado y sin arrancar tras "
                        + MaximoEnCurso.TotalMinutes + " min)");
                    Interlocked.CompareExchange(ref _enCurso, null, enCurso);
                }

                // 4. Documento y Aplicacion solo con Civil 3D libre
                if (!CivilLibre(out string motivo))
                {
                    if (!siguiente.AvisoEsperaDado)
                    {
                        siguiente.AvisoEsperaDado = true;
                        Historial.Registrar("MCP ⏳ " + siguiente.Nombre + " espera: " + motivo);
                    }
                    ProgramarReintento();
                    return;
                }

                if (!Cola.TryDequeue(out var trabajo)) return;
                if (trabajo.Contexto == ContextoEjecucion.Documento && AcApp.DocumentManager.MdiActiveDocument != null)
                    EntregarAlDocumento(trabajo);
                else
                    Correr(trabajo); // Aplicacion, o Documento sin dibujo abierto (la herramienta responderá que no hay dibujo)

                if (!Cola.IsEmpty || !ColaInmediata.IsEmpty) Despertar();
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

        // Ejecuta el trabajo aquí mismo (hilo principal) y entrega el resultado o la excepción al servidor
        private static void Correr(Pendiente p)
        {
            if (!p.Reclamar())
            {
                p.Resultado.TrySetCanceled();
                return;
            }
            try { p.Resultado.TrySetResult(p.Funcion()); }
            catch (System.Exception ex) { p.Resultado.TrySetException(ex); }
        }

        /// <summary>
        /// Entrega un trabajo al contexto de comando del dibujo activo sin bloquear el hilo principal. El propio
        /// delegado avisa al terminar (Terminar), así no dependemos del valor devuelto por ExecuteInCommandContextAsync,
        /// que cambia de tipo entre versiones de AutoCAD (Task en 2015-2024, DocumentCollection.ExecutionResult en 2027).
        /// Si además devuelve algo con una tarea dentro, se usa para detectar que AutoCAD falló antes de ejecutarlo.
        /// </summary>
        private static void EntregarAlDocumento(Pendiente p)
        {
            Volatile.Write(ref _enCurso, p);
            _inicioEnCurso = DateTime.UtcNow;
            try
            {
                object devuelto = AcApp.DocumentManager.ExecuteInCommandContextAsync(_ =>
                {
                    try { Correr(p); }
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
            Interlocked.CompareExchange(ref _enCurso, null, p);
            Despertar();
        }
    }
}

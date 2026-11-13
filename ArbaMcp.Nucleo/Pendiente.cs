using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ArbaMcp.Nucleo
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
        /// que Civil 3D esté libre. Valor por defecto y el único válido para modificar el dibujo. AutoCAD anota cada
        /// uno de estos trabajos como una entrada "Executefunction" del menú Deshacer (validado en Civil 3D 2027 el
        /// 28/09/2026): por eso desde 1.3.0 las lecturas van en Aplicacion.
        /// </summary>
        Documento = 0,

        /// <summary>
        /// Contexto de aplicación en el hilo principal, esperando también a que Civil 3D esté libre. Para lo que no
        /// admite contexto de comando (abrir o activar dibujos, enviar una orden a la línea de comandos) y, desde
        /// 1.3.0, para las herramientas de solo lectura, que bloquean el dibujo mientras leen y así no dejan entrada
        /// en el menú Deshacer.
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
    /// Un trabajo encolado para el hilo principal. Máquina de estados: EnCola → Ejecutando (Reclamar) o
    /// EnCola → Descartado (Descartar); un trabajo descartado nunca se ejecuta y uno reclamado ya no se puede descartar.
    /// Mide cuánto esperó en cola (ms_espera) y cuánto tardó en ejecutarse (ms_ejecucion).
    /// </summary>
    public sealed class Pendiente
    {
        private const int EnCola = 0, Ejecutando = 1, Descartado = 2;
        private int _estado = EnCola;
        private readonly Stopwatch _reloj = Stopwatch.StartNew();
        private long _inicioMs = -1, _finMs = -1;

        public Func<object> Funcion;
        public ContextoEjecucion Contexto;
        public string Nombre = "";
        /// <summary>
        /// Comprobación previa opcional de un trabajo de contexto Documento (desde 1.10.1; ver ComprobacionPrevia). El
        /// hilo principal la corre con Comprobar() en contexto de aplicación, justo antes de entregar el trabajo al
        /// contexto de comando; su resultado se descarta. Null: no hay comprobación.
        /// </summary>
        public Func<object> Comprobacion;
        /// <summary>True cuando ya se anotó en el historial que este trabajo espera a que Civil 3D quede libre.</summary>
        public bool AvisoEsperaDado;

        public readonly TaskCompletionSource<object> Resultado =
            new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Pendiente() { }

        public Pendiente(Func<object> funcion, ContextoEjecucion contexto, string nombre)
        {
            Funcion = funcion;
            Contexto = contexto;
            Nombre = nombre ?? "";
        }

        public Task<object> Tarea => Resultado.Task;

        /// <summary>Descarta el trabajo si aún no empezó. Devuelve true si se descartó (no se ejecutará).</summary>
        public bool Descartar() => Interlocked.CompareExchange(ref _estado, Descartado, EnCola) == EnCola;

        /// <summary>Lo reclama el hilo principal justo antes de ejecutarlo. False si ya fue descartado o reclamado.</summary>
        public bool Reclamar()
        {
            if (Interlocked.CompareExchange(ref _estado, Ejecutando, EnCola) != EnCola) return false;
            _inicioMs = _reloj.ElapsedMilliseconds;
            return true;
        }

        public bool EstaEnCola => Volatile.Read(ref _estado) == EnCola;
        public bool EstaDescartado => Volatile.Read(ref _estado) == Descartado;
        public bool EstaEjecutando => Volatile.Read(ref _estado) == Ejecutando;

        /// <summary>Milisegundos desde que se encoló hasta que empezó a ejecutarse; null si no ha empezado.</summary>
        public long? MsEspera => _inicioMs < 0 ? (long?)null : _inicioMs;

        /// <summary>Milisegundos de ejecución; null si no ha terminado.</summary>
        public long? MsEjecucion => _inicioMs < 0 || _finMs < 0 ? (long?)null : _finMs - _inicioMs;

        /// <summary>Reclama y ejecuta la función aquí mismo, entregando el resultado o la excepción; si ya estaba descartado, cancela la tarea.</summary>
        public void Correr()
        {
            if (!Reclamar())
            {
                Resultado.TrySetCanceled();
                return;
            }
            try { Resultado.TrySetResult(Funcion()); }
            catch (Exception ex) { Resultado.TrySetException(ex); }
            finally { _finMs = _reloj.ElapsedMilliseconds; }
        }

        /// <summary>
        /// Corre la comprobación previa, si la hay, sin reclamar el trabajo. Devuelve true si el trabajo debe entregarse
        /// (no hay comprobación o pasó). Si la comprobación lanza, reclama el trabajo y entrega esa excepción como su
        /// resultado: devuelve false y el trabajo ya no se ejecuta. Si el trabajo ya estaba descartado, lo cancela sin
        /// correr la comprobación y devuelve false.
        /// </summary>
        public bool Comprobar()
        {
            if (EstaDescartado)
            {
                Resultado.TrySetCanceled();
                return false;
            }
            if (Comprobacion == null) return true;
            try
            {
                Comprobacion();
                return true;
            }
            catch (Exception ex)
            {
                if (Reclamar())
                {
                    Resultado.TrySetException(ex);
                    _finMs = _reloj.ElapsedMilliseconds;
                }
                else Resultado.TrySetCanceled();   // el servidor lo descartó mientras se comprobaba
                return false;
            }
        }

        /// <summary>Marca el fin de la ejecución cuando el resultado lo entrega otro (por ejemplo, un fallo del contexto de comando).</summary>
        public void MarcarFin()
        {
            if (_finMs < 0) _finMs = _reloj.ElapsedMilliseconds;
        }

        /// <summary>
        /// Qué decir cuando el servidor agota el tiempo de espera: descarta el trabajo si sigue en cola y explica
        /// en qué estado quedó. 'pendiente' es null para las herramientas asíncronas (envío de comandos).
        /// </summary>
        public static string DescribirTiempoAgotado(Pendiente pendiente)
        {
            if (pendiente == null) return "el comando pudo haberse enviado; consulta leer_historial.";
            if (pendiente.Descartar()) return "la acción seguía esperando a que Civil 3D quedara libre (comando activo o cuadro de diálogo) y se ha descartado: no se ejecutó.";
            return "Civil 3D empezó a ejecutarla y sigue ocupado; terminará por su cuenta.";
        }
    }
}

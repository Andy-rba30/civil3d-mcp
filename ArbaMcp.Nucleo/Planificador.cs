using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ArbaMcp.Nucleo
{
    public enum AccionPlanificada
    {
        /// <summary>No hay nada que hacer ahora.</summary>
        Nada,
        /// <summary>Hay trabajo esperando pero Civil 3D no está libre o hay otro en curso: volver a mirar pasado EsperaReintento.</summary>
        Reintentar,
        /// <summary>Ejecutar 'Trabajo' ahora.</summary>
        Ejecutar
    }

    public sealed class Decision
    {
        public AccionPlanificada Accion;
        public Pendiente Trabajo;
        /// <summary>Línea para el historial, o null.</summary>
        public string Aviso;
    }

    /// <summary>
    /// Política de espera y descarte de la cola del hilo principal, sin nada de AutoCAD: dos colas (Inmediato por
    /// delante; Documento y Aplicacion en orden de llegada y de uno en uno), descarte de los trabajos que el servidor
    /// dio por agotados, espera a que Civil 3D esté libre (con un solo aviso por trabajo) y abandono del trabajo de
    /// documento que el contexto de comando nunca llegó a arrancar. El plugin decide qué es "libre" y cómo ejecutar.
    /// </summary>
    public sealed class Planificador
    {
        /// <summary>Cada cuánto se vuelve a comprobar si Civil 3D quedó libre mientras hay trabajos esperando.</summary>
        public static readonly TimeSpan EsperaReintento = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Si un trabajo entregado al contexto de comando no arranca en este tiempo y el servidor ya lo descartó,
        /// se deja de esperar por él (por ejemplo, porque se cerró el dibujo antes de que AutoCAD lo atendiera).
        /// </summary>
        public TimeSpan MaximoEnCurso = TimeSpan.FromMinutes(10);

        private readonly ConcurrentQueue<Pendiente> _cola = new ConcurrentQueue<Pendiente>();          // Documento y Aplicacion
        private readonly ConcurrentQueue<Pendiente> _inmediata = new ConcurrentQueue<Pendiente>();     // Inmediato
        private Pendiente _enCurso;
        private DateTime _inicioEnCurso;

        /// <summary>Trabajo Documento entregado al contexto de comando y aún sin terminar (null si ninguno).</summary>
        public Pendiente EnCurso => Volatile.Read(ref _enCurso);

        public bool HayTrabajo => !_cola.IsEmpty || !_inmediata.IsEmpty;
        public int Encolados => _cola.Count;
        public int Inmediatos => _inmediata.Count;

        public Pendiente Encolar(Func<object> funcion, ContextoEjecucion contexto, string nombre)
        {
            var p = new Pendiente(funcion, contexto, nombre);
            Encolar(p);
            return p;
        }

        public void Encolar(Pendiente p)
        {
            if (p.Contexto == ContextoEjecucion.Inmediato) _inmediata.Enqueue(p);
            else _cola.Enqueue(p);
        }

        /// <summary>Saca todos los trabajos Inmediato en orden (se ejecutan siempre, aunque Civil 3D esté ocupado).</summary>
        public List<Pendiente> TomarInmediatos()
        {
            var l = new List<Pendiente>();
            while (_inmediata.TryDequeue(out var p)) l.Add(p);
            return l;
        }

        /// <summary>
        /// Decide qué hacer con la cola Documento/Aplicacion. 'civilLibre' y 'motivo' los aporta el plugin
        /// (CMDACTIVE y ventana principal); 'ahora' permite probar el abandono por tiempo.
        /// </summary>
        public Decision Decidir(bool civilLibre, string motivo, DateTime ahora)
        {
            // 1. Fuera de la cabeza los trabajos que el servidor ya dio por agotados
            while (_cola.TryPeek(out var cabeza) && cabeza.EstaDescartado && _cola.TryDequeue(out var descartado))
                descartado.Resultado.TrySetCanceled();
            if (!_cola.TryPeek(out var siguiente)) return new Decision { Accion = AccionPlanificada.Nada };

            // 2. Un trabajo de documento sigue en curso (en cola de AutoCAD o ejecutándose): esperar a que termine
            string aviso = null;
            var enCurso = Volatile.Read(ref _enCurso);
            if (enCurso != null)
            {
                if (!enCurso.EstaDescartado || ahora - _inicioEnCurso < MaximoEnCurso)
                    return new Decision { Accion = AccionPlanificada.Reintentar };
                // El servidor ya lo descartó y el contexto de comando nunca lo arrancó: se deja de esperar por él
                aviso = "HiloPrincipal: se deja de esperar por '" + enCurso.Nombre + "' (descartado y sin arrancar tras " + MaximoEnCurso.TotalMinutes + " min)";
                Interlocked.CompareExchange(ref _enCurso, null, enCurso);
            }

            // 3. Documento y Aplicacion solo con Civil 3D libre
            if (!civilLibre)
            {
                string avisoEspera = null;
                if (!siguiente.AvisoEsperaDado)
                {
                    siguiente.AvisoEsperaDado = true;
                    avisoEspera = "MCP ⏳ " + siguiente.Nombre + " espera: " + motivo;
                }
                return new Decision { Accion = AccionPlanificada.Reintentar, Aviso = Unir(aviso, avisoEspera) };
            }

            if (!_cola.TryDequeue(out var trabajo)) return new Decision { Accion = AccionPlanificada.Nada, Aviso = aviso };
            return new Decision { Accion = AccionPlanificada.Ejecutar, Trabajo = trabajo, Aviso = aviso };
        }

        /// <summary>Anota que 'p' se entregó al contexto de comando (hasta Terminar no se atiende otro trabajo de documento).</summary>
        public void MarcarEnCurso(Pendiente p, DateTime ahora)
        {
            Volatile.Write(ref _enCurso, p);
            _inicioEnCurso = ahora;
        }

        public void Terminar(Pendiente p)
        {
            p.MarcarFin();
            Interlocked.CompareExchange(ref _enCurso, null, p);
        }

        /// <summary>Descarta y cancela todo lo que hay en las dos colas (al descargar el plugin).</summary>
        public void Vaciar()
        {
            Vaciar(_inmediata);
            Vaciar(_cola);
        }

        private static void Vaciar(ConcurrentQueue<Pendiente> cola)
        {
            while (cola.TryDequeue(out var p))
            {
                p.Descartar();
                p.Resultado.TrySetCanceled();
            }
        }

        private static string Unir(string a, string b) => a == null ? b : b == null ? a : a + "\n" + b;
    }
}

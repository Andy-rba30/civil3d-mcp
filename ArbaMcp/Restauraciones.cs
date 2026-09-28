using System;
using System.Collections.Generic;
using System.Linq;

namespace ArbaMcp
{
    /// <summary>Un objetivo de subensamblaje tal como estaba antes de una escritura y como quedó después de ella.</summary>
    internal sealed class ObjetivoGuardado
    {
        public string LineaBase, Region, Subensamblaje, Grupo, Parametro, Tipo;
        /// <summary>Handles de los objetos objetivo antes de la escritura (vacío = 'ninguno').</summary>
        public List<string> AntesHandles = new List<string>();
        public List<string> AntesTexto = new List<string>();
        /// <summary>Handles que dejó la escritura: al deshacer se comprueba que nadie lo cambió después.</summary>
        public List<string> DespuesHandles = new List<string>();
        public List<string> DespuesTexto = new List<string>();
        /// <summary>SubassemblyTargetToOption (nombre del enum) antes y después; null si no se pudo leer.</summary>
        public string AntesOpcionApi, DespuesOpcionApi;
        /// <summary>La opción como la muestra listar_objetivos (mas_cercano, exterior, interior).</summary>
        public string AntesOpcion, DespuesOpcion;
        public bool? AntesMismoLado, DespuesMismoLado;

        public string Donde => Subensamblaje + " [" + Tipo + (string.IsNullOrEmpty(Parametro) ? "" : ", " + Parametro) + "] en la región '" + Region + "' de '" + LineaBase + "'";
        public string TextoAntes => AntesTexto.Count == 0 ? "ninguno" : string.Join(", ", AntesTexto);
        public string TextoDespues => DespuesTexto.Count == 0 ? "ninguno" : string.Join(", ", DespuesTexto);
    }

    /// <summary>Una escritura de objetivos que se puede deshacer: qué herramienta, sobre qué corredor y qué había antes.</summary>
    internal sealed class EntradaRestauracion
    {
        public int Id;
        public DateTime Hora;
        public string Herramienta;
        public string Corredor;
        public List<ObjetivoGuardado> Elementos = new List<ObjetivoGuardado>();

        public object Resumen() => new { id = Id, hora = Hora.ToString("yyyy-MM-dd HH:mm:ss"), herramienta = Herramienta, corredor = Corredor, objetivos = Elementos.Count };
    }

    /// <summary>
    /// Pila de restauración de objetivos, por dibujo. Civil 3D no deshace los cambios de objetivos de un corredor: ni
    /// tras BaselineRegion.SetTargets ni tras cambiarlos en Propiedades de corredor, `_.UNDO 1` y Ctrl+Z retiran la
    /// entrada del menú Deshacer pero el objetivo sigue en el valor nuevo (validado en Civil 3D 2027 el 28/09/2026,
    /// VALIDACION_13 1.3.2 pasos 9 y 9b). Por eso cada escritura de objetivos guarda aquí lo que había antes y
    /// deshacer_objetivos lo reaplica. Vive en memoria: se pierde al cerrar Civil 3D, y la respuesta de cada escritura
    /// lleva además el bloque 'restaurar' con lo anterior para reasignarlo a mano si hiciera falta.
    /// </summary>
    internal static class Restauraciones
    {
        public const int Maximo = 50;
        private static readonly object Cerrojo = new object();
        private static readonly Dictionary<string, List<EntradaRestauracion>> Pilas = new Dictionary<string, List<EntradaRestauracion>>(StringComparer.OrdinalIgnoreCase);
        private static int _ultimoId;

        /// <summary>
        /// Apila una escritura ya confirmada; devuelve la entrada (null si no cambió ningún objetivo). Si un lote tocó
        /// varias veces el mismo objetivo, queda un solo elemento con el 'antes' de la primera vez y el 'después' de la
        /// última: así se devuelve al estado original y la verificación por elemento cuadra.
        /// </summary>
        public static EntradaRestauracion Guardar(string dibujo, string herramienta, string corredor, List<ObjetivoGuardado> elementos)
        {
            if (elementos == null || elementos.Count == 0) return null;
            var unicos = new List<ObjetivoGuardado>();
            var indice = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in elementos)
            {
                string k = string.Join("|", g.LineaBase, g.Region, g.Subensamblaje, g.Tipo, g.Grupo, g.Parametro);
                if (indice.TryGetValue(k, out int i))
                {
                    var u = unicos[i];
                    u.DespuesHandles = g.DespuesHandles; u.DespuesTexto = g.DespuesTexto;
                    u.DespuesOpcionApi = g.DespuesOpcionApi; u.DespuesOpcion = g.DespuesOpcion; u.DespuesMismoLado = g.DespuesMismoLado;
                }
                else { indice[k] = unicos.Count; unicos.Add(g); }
            }
            lock (Cerrojo)
            {
                if (!Pilas.TryGetValue(dibujo, out var pila)) Pilas[dibujo] = pila = new List<EntradaRestauracion>();
                var e = new EntradaRestauracion { Id = ++_ultimoId, Hora = DateTime.Now, Herramienta = herramienta, Corredor = corredor, Elementos = unicos };
                pila.Add(e);
                while (pila.Count > Maximo) pila.RemoveAt(0);
                return e;
            }
        }

        /// <summary>La entrada pedida (por id) o la última del dibujo; null si no hay.</summary>
        public static EntradaRestauracion Buscar(string dibujo, int? id)
        {
            lock (Cerrojo)
            {
                if (!Pilas.TryGetValue(dibujo, out var pila) || pila.Count == 0) return null;
                if (id == null) return pila[pila.Count - 1];
                return pila.FirstOrDefault(e => e.Id == id.Value);
            }
        }

        public static bool Quitar(string dibujo, int id)
        {
            lock (Cerrojo) return Pilas.TryGetValue(dibujo, out var pila) && pila.RemoveAll(e => e.Id == id) > 0;
        }

        /// <summary>Entradas pendientes del dibujo, de la más reciente a la más antigua.</summary>
        public static List<object> Pendientes(string dibujo)
        {
            lock (Cerrojo)
            {
                if (!Pilas.TryGetValue(dibujo, out var pila)) return new List<object>();
                return pila.AsEnumerable().Reverse().Select(e => e.Resumen()).ToList();
            }
        }
    }
}

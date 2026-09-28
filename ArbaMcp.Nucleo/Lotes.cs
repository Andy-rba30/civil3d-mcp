using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>Un elemento de un lote: su índice, lo que se hace con él, antes/esperado/después y, si falló, el motivo.</summary>
    public sealed class ElementoLote
    {
        public int Indice;
        public string Accion;
        public IDictionary<string, object> Antes;
        public IDictionary<string, object> Esperado;
        public IDictionary<string, object> Despues;
        /// <summary>Motivo por el que no se aplicó (objeto inexistente, argumento inválido); null si se aplicó.</summary>
        public string Error;
        public bool Fallido => Error != null;
    }

    /// <summary>
    /// Lotes (asignar_objetivos, establecer_frecuencias): lectura y validación de la lista, valores por defecto del
    /// nivel superior y respuestas con antes/despues por índice y 'fallidos' sin abortar el lote.
    /// </summary>
    public static class Lotes
    {
        /// <summary>Elementos por lote salvo forzar=true.</summary>
        public const int Limite = 200;

        /// <summary>
        /// Lee el parámetro json 'nombre' como lista de objetos. Lanza ArgumentException si falta, si no es un arreglo,
        /// si está vacío, si algún elemento no es un objeto (con su índice) o si supera el límite sin 'forzar'.
        /// </summary>
        public static List<JsonElement> LeerLista(JsonElement args, string nombre, bool forzar, int limite = Limite)
        {
            var lista = Argumentos.Json(args, nombre);
            if (lista.ValueKind == JsonValueKind.Undefined) throw new ArgumentException("Falta el parámetro obligatorio '" + nombre + "' (arreglo JSON).");
            if (lista.ValueKind != JsonValueKind.Array) throw new ArgumentException("El parámetro '" + nombre + "' debe ser un arreglo JSON de objetos; llegó " + lista.ValueKind + ".");
            var elementos = new List<JsonElement>();
            foreach (var e in lista.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) throw new ArgumentException(nombre + "[" + elementos.Count + "] debe ser un objeto JSON; llegó " + e.ValueKind + ".");
                elementos.Add(e);
            }
            if (elementos.Count == 0) throw new ArgumentException("El parámetro '" + nombre + "' está vacío.");
            if (elementos.Count > limite && !forzar)
                throw new ArgumentException("El lote tiene " + elementos.Count + " elementos y el límite es " + limite + "; pásalo con forzar=true si es intencionado.");
            return elementos;
        }

        /// <summary>
        /// Objeto JSON con las claves del elemento más las 'claves' que falten tomadas de 'args' (valores por defecto
        /// del nivel superior, como corredor o linea_base). El elemento manda.
        /// </summary>
        public static JsonElement ConDefectos(JsonElement elemento, JsonElement args, params string[] claves)
        {
            var d = new Dictionary<string, object>();
            foreach (var p in elemento.EnumerateObject()) d[p.Name] = p.Value;
            foreach (var k in claves)
                if (!Argumentos.Tiene(elemento, k) && Argumentos.Tiene(args, k)) d[k] = args.GetProperty(k);
            using (var doc = JsonDocument.Parse(Json.Serializar(d))) return doc.RootElement.Clone();
        }

        /// <summary>Respuesta de un lote simulado: plan por índice, fallidos y recuento.</summary>
        public static object Simulacion(string herramienta, IList<ElementoLote> elementos, string accion, IList<string> avisos = null)
        {
            var plan = new List<object>();
            var antes = new List<object>();
            var despues = new List<object>();
            var cambios = new List<string>();
            foreach (var e in elementos)
            {
                if (e.Fallido) { plan.Add(new { indice = e.Indice, accion = e.Accion, error = e.Error }); antes.Add(null); despues.Add(null); continue; }
                var d = new Dictionary<string, object>(e.Antes);
                foreach (var kv in e.Esperado) d[kv.Key] = kv.Value;
                var c = Verificacion.Cambios(e.Antes, d);
                foreach (var k in c) cambios.Add(e.Indice + "." + k);
                var a = Verificacion.Filtrar(e.Antes, c);
                var p = Verificacion.Filtrar(d, c);
                plan.Add(new { indice = e.Indice, accion = e.Accion, cambios = c, antes = a, despues = p });
                antes.Add(a);
                despues.Add(p);
            }
            return new
            {
                simulado = true,
                herramienta,
                accion,
                plan,
                cambios,
                antes,
                despues,
                fallidos = Fallidos(elementos),
                datos = Recuento(elementos),
                avisos = avisos != null && avisos.Count > 0 ? avisos : null
            };
        }

        /// <summary>
        /// Respuesta de un lote aplicado. Verifica cada elemento aplicado (despues frente a esperado) y lanza, sin
        /// reintentar, si alguno no refleja el cambio; el mensaje incluye el índice y el campo.
        /// </summary>
        public static object Resultado(string herramienta, IList<ElementoLote> elementos, string mensaje, object copia = null, IList<string> avisos = null)
        {
            var antes = new List<object>();
            var despues = new List<object>();
            var cambios = new List<string>();
            foreach (var e in elementos)
            {
                if (e.Fallido) { antes.Add(null); despues.Add(null); continue; }
                if (e.Despues == null) throw new InvalidOperationException("El elemento " + e.Indice + " del lote no tiene estado 'despues' leído del dibujo.");
                Verificacion.ComprobarEsperado(e.Antes, e.Despues, e.Esperado, copia, "[" + e.Indice + "].");
                var c = Verificacion.Cambios(e.Antes, e.Despues);
                foreach (var k in c) cambios.Add(e.Indice + "." + k);
                antes.Add(Verificacion.Filtrar(e.Antes, c));
                despues.Add(Verificacion.Filtrar(e.Despues, c));
            }
            return new
            {
                simulado = false,
                herramienta,
                mensaje,
                cambios,
                antes,
                despues,
                copia,
                datos = Recuento(elementos),
                fallidos = Fallidos(elementos),
                avisos = avisos != null && avisos.Count > 0 ? avisos : null
            };
        }

        public static List<object> Fallidos(IList<ElementoLote> elementos)
            => elementos.Where(e => e.Fallido).Select(e => (object)new { indice = e.Indice, accion = e.Accion, motivo = e.Error }).ToList();

        public static object Recuento(IList<ElementoLote> elementos)
            => new { total = elementos.Count, aplicadas = elementos.Count(e => !e.Fallido), fallidas = elementos.Count(e => e.Fallido) };

        public static string ResumenSimulado(string que, IList<ElementoLote> elementos)
            => "Se aplicarían " + elementos.Count(e => !e.Fallido) + " " + que + "; " + elementos.Count(e => e.Fallido) + " fallidas (de " + elementos.Count + ")";

        public static string Resumen(string que, IList<ElementoLote> elementos)
            => elementos.Count(e => !e.Fallido) + " " + que + " aplicadas, " + elementos.Count(e => e.Fallido) + " fallidas (de " + elementos.Count + ")";
    }
}

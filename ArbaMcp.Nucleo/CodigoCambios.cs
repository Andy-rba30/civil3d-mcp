using System;
using System.Collections.Generic;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Fuentes con las que ejecutar_codigo averigua qué creó, modificó o borró un script (desde 1.17.5). En la
    /// validación de la 1.16.0 (informe de VALIDACION_116 del 14/11/2026, pasos 4b y 5a) las dos que había no vieron la
    /// línea creada con F.Agregar: los eventos se soltaban antes de que AutoCAD los mandara (los objetos de una
    /// transacción se notifican al terminar la transacción exterior) y GetAllObjects se leía en la transacción del
    /// plugin, no en la del script. Desde 1.17.5 se combinan cinco fuentes y la respuesta dice qué vio cada una
    /// (datos.objetos.fuente_deteccion), para que la validación siguiente muestre cuál funciona en Civil 3D.
    /// </summary>
    public static class FuenteCambio
    {
        /// <summary>F.Agregar anota el ObjectId que devuelve AppendEntity.</summary>
        public const string Fachada = "fachada";
        /// <summary>Transaction.GetAllObjects() de F.Tr, antes de confirmarla.</summary>
        public const string TransaccionScript = "transaccion_script";
        /// <summary>Transaction.GetAllObjects() de la transacción del plugin, después de confirmar F.Tr (el camino de 1.16.0).</summary>
        public const string TransaccionPlugin = "transaccion_plugin";
        /// <summary>Eventos ObjectAppended, ObjectModified y ObjectErased mientras corre el script.</summary>
        public const string Eventos = "eventos";
        /// <summary>
        /// Los mismos eventos mientras se confirma o se aborta la transacción del plugin. Llegan cuando la clasificación
        /// ya está hecha, así que solo sirven de diagnóstico y para avisar de lo que las otras fuentes no vieron.
        /// </summary>
        public const string EventosAlCerrar = "eventos_al_cerrar";

        /// <summary>Las fuentes que deciden la clasificación (todas menos EventosAlCerrar).</summary>
        public static readonly string[] QueClasifican = { Fachada, TransaccionScript, TransaccionPlugin, Eventos };

        /// <summary>Todas, en el orden de la respuesta.</summary>
        public static readonly string[] Todas = { Fachada, TransaccionScript, TransaccionPlugin, Eventos, EventosAlCerrar };

        /// <summary>Las que leen una transacción con GetAllObjects (en la respuesta llevan 'objetos').</summary>
        public static bool LeeTransaccion(string fuente) => fuente == TransaccionScript || fuente == TransaccionPlugin;
    }

    /// <summary>Resultado de clasificar: objetos creados, modificados y borrados por el script, y los temporales (creados y borrados).</summary>
    public sealed class ClasificacionCambios<T>
    {
        public readonly List<T> Creados = new List<T>();
        public readonly List<T> Modificados = new List<T>();
        public readonly List<T> Borrados = new List<T>();
        public int Temporales;
    }

    /// <summary>
    /// Combina lo que dicen las fuentes de FuenteCambio sobre cada objeto (T es el ObjectId en el plugin; en las pruebas,
    /// cualquier clave) y cuenta lo que vio cada una. Reglas de la combinación:
    ///  - 'nuevo' y 'modificado' se suman (basta con que una fuente lo diga);
    ///  - 'borrado' es el último estado conocido: los eventos lo marcan al borrar y lo quitan al recuperar, y las fuentes
    ///    que leen después del script dicen el estado de ese momento (por eso se anotan después de los eventos);
    ///  - un objeto nuevo y borrado es temporal; si no, cuenta como creado, borrado o modificado, en ese orden;
    ///  - EventosAlCerrar no entra en la combinación, solo en su recuento.
    /// No toca AutoCAD: se prueba sin Civil 3D (CodigoCambiosPruebas).
    /// </summary>
    public sealed class CodigoCambios<T>
    {
        private sealed class Estado
        {
            public bool Nuevo, Modificado, Borrado;
        }

        private sealed class PorFuente
        {
            public Dictionary<T, Estado> Objetos;
            public int? Leidos;
            public int Errores;
            public string PrimerError;
            public string NoLeida;
        }

        private readonly IEqualityComparer<T> _comparador;
        private readonly Dictionary<T, Estado> _combinado;
        private readonly List<T> _orden = new List<T>();
        private readonly Dictionary<string, PorFuente> _fuentes = new Dictionary<string, PorFuente>(StringComparer.Ordinal);

        public CodigoCambios(IEqualityComparer<T> comparador = null)
        {
            _comparador = comparador ?? EqualityComparer<T>.Default;
            _combinado = new Dictionary<T, Estado>(_comparador);
        }

        private PorFuente Fuente(string fuente)
        {
            if (!FuenteCambio.Todas.Contains(fuente)) throw new ArgumentException("Fuente desconocida: " + fuente);
            if (!_fuentes.TryGetValue(fuente, out var f)) _fuentes[fuente] = f = new PorFuente { Objetos = new Dictionary<T, Estado>(_comparador) };
            return f;
        }

        /// <summary>Lo que una fuente dice de un objeto. 'borrado' null: esa fuente no sabe si está borrado.</summary>
        public void Anotar(string fuente, T clave, bool nuevo = false, bool modificado = false, bool? borrado = null)
        {
            var f = Fuente(fuente);
            if (!f.Objetos.TryGetValue(clave, out var ef)) f.Objetos[clave] = ef = new Estado();
            Aplicar(ef, nuevo, modificado, borrado);
            if (fuente == FuenteCambio.EventosAlCerrar) return;
            if (!_combinado.TryGetValue(clave, out var e))
            {
                _combinado[clave] = e = new Estado();
                _orden.Add(clave);
            }
            Aplicar(e, nuevo, modificado, borrado);
        }

        private static void Aplicar(Estado e, bool nuevo, bool modificado, bool? borrado)
        {
            e.Nuevo |= nuevo;
            e.Modificado |= modificado;
            if (borrado.HasValue) e.Borrado = borrado.Value;
        }

        /// <summary>Cuántos objetos devolvió GetAllObjects en una fuente que lee una transacción (0 es un dato: no vio nada).</summary>
        public void Leidos(string fuente, int cuantos) => Fuente(fuente).Leidos = cuantos;

        /// <summary>Un error al leer un objeto o al usar la fuente; se cuentan y se guarda el primero (antes se tragaban sin rastro).</summary>
        public void Error(string fuente, string mensaje)
        {
            var f = Fuente(fuente);
            f.Errores++;
            if (f.PrimerError == null) f.PrimerError = string.IsNullOrWhiteSpace(mensaje) ? "(sin mensaje)" : mensaje;
        }

        /// <summary>La fuente no se pudo usar (por ejemplo, el script cerró F.Tr antes de terminar).</summary>
        public void NoLeida(string fuente, string motivo) => Fuente(fuente).NoLeida = motivo;

        /// <summary>Clasifica lo combinado, en el orden en que se vio cada objeto por primera vez.</summary>
        public ClasificacionCambios<T> Clasificar()
        {
            var c = new ClasificacionCambios<T>();
            foreach (var clave in _orden)
            {
                var e = _combinado[clave];
                if (e.Nuevo && e.Borrado) c.Temporales++;
                else if (e.Nuevo) c.Creados.Add(clave);
                else if (e.Borrado) c.Borrados.Add(clave);
                else if (e.Modificado) c.Modificados.Add(clave);
            }
            return c;
        }

        /// <summary>Objetos que solo EventosAlCerrar vio nuevos: ninguna fuente de la clasificación los dio por nuevos.</summary>
        public List<T> NuevosSoloAlCerrar()
        {
            var lista = new List<T>();
            if (!_fuentes.TryGetValue(FuenteCambio.EventosAlCerrar, out var f)) return lista;
            foreach (var kv in f.Objetos)
                if (kv.Value.Nuevo && !(_combinado.TryGetValue(kv.Key, out var e) && e.Nuevo)) lista.Add(kv.Key);
            return lista;
        }

        /// <summary>
        /// datos.objetos.fuente_deteccion: 'creados_por' (las fuentes que vieron nuevo algún objeto de la lista de creados,
        /// o "ninguna") y, por fuente, lo que vio: 'creados', 'modificados' y 'borrados' (cada fuente cuenta sus marcas:
        /// un objeto nuevo puede contar también como modificado), 'errores' y 'primer_error'; las que leen una
        /// transacción, 'objetos' (cuántos devolvió GetAllObjects, null si no se leyó) y 'no_leida'; EventosAlCerrar,
        /// 'momento' ("al confirmar", "al abortar" o por qué no se vigilaron).
        /// </summary>
        public Dictionary<string, object> Informe(ClasificacionCambios<T> c, string momentoAlCerrar)
        {
            var creados = new HashSet<T>(c?.Creados ?? new List<T>(), _comparador);
            var por = new List<string>();
            foreach (var nombre in FuenteCambio.QueClasifican)
                if (_fuentes.TryGetValue(nombre, out var f) && f.Objetos.Any(kv => kv.Value.Nuevo && creados.Contains(kv.Key))) por.Add(nombre);

            var d = new Dictionary<string, object> { ["creados_por"] = por.Count == 0 ? "ninguna" : string.Join(", ", por) };
            foreach (var nombre in FuenteCambio.Todas)
            {
                _fuentes.TryGetValue(nombre, out var f);
                var x = new Dictionary<string, object>();
                if (nombre == FuenteCambio.EventosAlCerrar) x["momento"] = momentoAlCerrar;
                if (FuenteCambio.LeeTransaccion(nombre)) x["objetos"] = f?.Leidos;
                x["creados"] = f?.Objetos.Values.Count(e => e.Nuevo) ?? 0;
                x["modificados"] = f?.Objetos.Values.Count(e => e.Modificado) ?? 0;
                x["borrados"] = f?.Objetos.Values.Count(e => e.Borrado) ?? 0;
                x["errores"] = f?.Errores ?? 0;
                if (f?.PrimerError != null) x["primer_error"] = f.PrimerError;
                if (FuenteCambio.LeeTransaccion(nombre) && f?.NoLeida != null) x["no_leida"] = f.NoLeida;
                d[nombre] = x;
            }
            return d;
        }
    }

    /// <summary>
    /// Diagnóstico de Deshacer de ejecutar_codigo (desde 1.17.5, datos.deshacer). En la validación de la 1.16.0 un
    /// _.UNDO 1 después de la escritura real no quitó la línea (informe de VALIDACION_116, paso 5c) y no se pudo saber
    /// por qué: la respuesta lleva ahora las variables UNDOCTL, DBMOD y CMDNAMES. Aquí solo se traduce UNDOCTL a texto
    /// y se decide si merece un aviso; la lectura de las variables la hace el plugin con GetSystemVariable.
    /// Valores de UNDOCTL según la ayuda de AutoCAD (por verificar en Civil 3D 2027): 0 deshacer desactivado,
    /// 1 activado, 2 solo un comando, 4 Auto, 8 un grupo activo, 16 zoom y encuadre agrupados, 32 propiedades de capa
    /// agrupadas (se suman).
    /// </summary>
    public static class CodigoDeshacer
    {
        /// <summary>UNDOCTL en palabras ("activado; Auto; ..."); null si no se pudo leer.</summary>
        public static string DescribirUndoctl(int? undoctl)
        {
            if (!undoctl.HasValue) return null;
            int v = undoctl.Value;
            if ((v & 1) == 0) return "deshacer desactivado";
            var partes = new List<string> { "deshacer activado" };
            if ((v & 2) != 0) partes.Add("solo se puede deshacer un comando");
            if ((v & 4) != 0) partes.Add("Auto");
            if ((v & 8) != 0) partes.Add("un grupo de deshacer activo (dentro de ejecutar_codigo puede ser el del propio pseudocomando)");
            if ((v & 16) != 0) partes.Add("zoom y encuadre agrupados");
            if ((v & 32) != 0) partes.Add("propiedades de capa agrupadas");
            return string.Join("; ", partes);
        }

        /// <summary>
        /// Aviso para la respuesta si el deshacer de AutoCAD no puede quitar este cambio con un _.UNDO 1 normal (desactivado
        /// o limitado a un comando); null si no hace falta. El bit 8 (grupo activo) no avisa: dentro del pseudocomando de
        /// ExecuteInCommandContextAsync podría estar puesto por el propio AutoCAD.
        /// </summary>
        public static string Aviso(int? undoctl)
        {
            if (!undoctl.HasValue) return null;
            int v = undoctl.Value;
            if ((v & 1) == 0)
                return "El deshacer de AutoCAD está desactivado (UNDOCTL=" + v + "): _.UNDO no quitará este cambio. La protección es la copia de seguridad (copia.ruta).";
            if ((v & 2) != 0)
                return "El deshacer de AutoCAD está limitado a un solo comando (UNDOCTL=" + v + "): solo se puede deshacer lo último. La protección es la copia de seguridad (copia.ruta).";
            return null;
        }
    }
}

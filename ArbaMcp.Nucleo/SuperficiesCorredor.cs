using System;
using System.Collections.Generic;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Lógica de crear_superficies_corredor (1.12.0) que no depende de Civil 3D: nombres por defecto de las superficies
    /// Top y Datum, comprobación de que los nombres están libres, búsqueda de los códigos de vínculo en los del corredor
    /// (con su grafía real), resolución por nombre de un valor de enumerado de la API (sin adivinar: si no existe, error
    /// con los valores reales), texto de la acción, estado esperado con claves planas (top_*, datum_*) y avisos de la
    /// relectura. Se prueba en ArbaMcp.Pruebas con datos sintéticos.
    /// </summary>
    public static class SuperficiesCorredor
    {
        public const string CodigoTop = "Top";
        public const string CodigoDatum = "Datum";
        /// <summary>Corrección de voladizo de la superficie Top: por los vínculos superiores (convención de Civil 3D; valor por verificar en 2027).</summary>
        public const string VoladizoTop = "TopLinks";
        /// <summary>Corrección de voladizo de la superficie Datum: por los vínculos inferiores (convención de Civil 3D; valor por verificar en 2027).</summary>
        public const string VoladizoDatum = "BottomLinks";
        public const string EnumeradoVoladizo = "OverhangCorrectionType";
        /// <summary>Nombre del contorno con la extensión del corredor que se añade a cada superficie.</summary>
        public const string NombreContorno = "Extensión del corredor";
        /// <summary>Tipo de contorno exterior (CorridorSurfaceBoundaryType.OutsideBoundary).</summary>
        public const string TipoContornoExterior = "OutsideBoundary";
        public const string PrefijoTop = "top";
        public const string PrefijoDatum = "datum";

        public const string AvisoSimulacion = "Si cada superficie de corredor tiene ya su superficie en el dibujo (la que ve listar_superficies y usará calcular_volumenes) se sabe al reconstruir: la simulación no lo puede comprobar.";

        private static readonly StringComparer SinMayusculas = StringComparer.OrdinalIgnoreCase;

        // ------------------------------------------------------------------ nombres
        /// <summary>Nombre de la superficie Top: el pedido o "&lt;corredor&gt; - Top".</summary>
        public static string NombreTop(string corredor, string pedido) => Nombre(corredor, pedido, CodigoTop);

        /// <summary>Nombre de la superficie Datum: el pedido o "&lt;corredor&gt; - Datum".</summary>
        public static string NombreDatum(string corredor, string pedido) => Nombre(corredor, pedido, CodigoDatum);

        private static string Nombre(string corredor, string pedido, string codigo)
            => string.IsNullOrWhiteSpace(pedido) ? (corredor ?? "").Trim() + " - " + codigo : pedido.Trim();

        /// <summary>
        /// Lanza si los dos nombres son iguales o si alguno ya lo usa una superficie del dibujo. 'ocupados': nombre de
        /// cada superficie existente → corredor que la genera (null si no es de un corredor).
        /// </summary>
        public static void ValidarNombres(string top, string datum, IDictionary<string, string> ocupados)
        {
            if (string.IsNullOrWhiteSpace(top) || string.IsNullOrWhiteSpace(datum))
                throw new ArgumentException("Los nombres de las superficies no pueden quedar vacíos: pasa 'nombre_top' y 'nombre_datum' con texto, o no los pases para usar los nombres por defecto.");
            if (string.Equals(top.Trim(), datum.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("'nombre_top' y 'nombre_datum' no pueden ser iguales ('" + top.Trim() + "'): pasa dos nombres distintos.");
            var mapa = new Dictionary<string, string>(SinMayusculas);
            if (ocupados != null)
                foreach (var kv in ocupados)
                    if (!string.IsNullOrWhiteSpace(kv.Key)) mapa[kv.Key.Trim()] = kv.Value;
            ComprobarLibre(top.Trim(), "nombre_top", mapa);
            ComprobarLibre(datum.Trim(), "nombre_datum", mapa);
        }

        private static void ComprobarLibre(string nombre, string parametro, Dictionary<string, string> ocupados)
        {
            if (!ocupados.TryGetValue(nombre, out string corredor)) return;
            throw new ArgumentException("Ya existe la superficie '" + nombre + "'"
                + (string.IsNullOrWhiteSpace(corredor) ? "" : " (la genera el corredor '" + corredor + "')")
                + ". Usa listar_superficies o estado_corredor, o pasa otro '" + parametro + "'.");
        }

        // ------------------------------------------------------------------ códigos de vínculo
        /// <summary>
        /// El código de vínculo tal como lo escribe el corredor (Corridor.GetLinkCodes), buscado sin distinguir
        /// mayúsculas. Lanza si el corredor no devuelve ningún código (no reconstruido) o si falta este.
        /// </summary>
        public static string CodigoDelCorredor(IEnumerable<string> codigosCorredor, string codigo, string corredor, string superficie)
        {
            var lista = Limpia(codigosCorredor);
            if (lista.Count == 0)
                throw new ArgumentException("El corredor '" + corredor + "' no devuelve ningún código de vínculo (Corridor.GetLinkCodes): reconstrúyelo con reconstruir_corredor, comprueba con estado_corredor que no está desactualizado y repite. Si sigue vacío, revisa su ensamblaje con listar_ensamblajes.");
            string real = lista.FirstOrDefault(c => string.Equals(c.Trim(), (codigo ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            if (real == null)
                throw new ArgumentException("El corredor '" + corredor + "' no tiene el código de vínculo '" + codigo + "', necesario para la superficie '" + superficie
                    + "'. Códigos de vínculo del corredor: " + string.Join(", ", lista) + ". Revisa los subensamblajes de su ensamblaje con listar_ensamblajes (los carriles y taludes de Civil 3D traen Top y Datum).");
            return real;
        }

        // ------------------------------------------------------------------ enumerados de la API
        /// <summary>
        /// El valor 'buscado' de un enumerado de la API, tal como lo escribe esta instalación ('valores' =
        /// Enum.GetNames), sin distinguir mayúsculas. No adivina: si no está, lanza con los valores reales.
        /// </summary>
        public static string ValorEnumerado(IEnumerable<string> valores, string buscado, string enumerado, string para)
        {
            var lista = Limpia(valores);
            string real = lista.FirstOrDefault(v => string.Equals(v.Trim(), (buscado ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            if (real == null)
                throw new InvalidOperationException("El enumerado " + enumerado + " de esta instalación de Civil 3D no tiene el valor '" + buscado + "' (" + para + "). Valores: "
                    + (lista.Count == 0 ? "ninguno" : string.Join(", ", lista)) + ". No se crea nada: anótalo en miembros_por_verificar_civil3d.md para corregir la herramienta.");
            return real;
        }

        // ------------------------------------------------------------------ textos
        /// <summary>Texto de la acción para la respuesta.</summary>
        public static string Accion(string corredor, string top, string datum)
            => "Crear en el corredor '" + corredor + "' las superficies '" + top + "' (código de vínculo Top, voladizo por los vínculos superiores) y '"
               + datum + "' (código de vínculo Datum, voladizo por los vínculos inferiores), como líneas de rotura y con el contorno de la extensión del corredor, y reconstruirlo";

        // ------------------------------------------------------------------ estado (claves planas)
        /// <summary>
        /// Añade al estado las claves de una superficie con el prefijo dado (top o datum): existe, codigos_enlace,
        /// codigos_rotura, voladizo y contornos. Si la superficie no existe, todo salvo 'existe' queda en null.
        /// </summary>
        public static void AnadirEstado(IDictionary<string, object> d, string prefijo, bool existe, IEnumerable<string> codigosEnlace,
            IEnumerable<string> codigosRotura, string voladizo, IEnumerable<string> contornos)
        {
            d[prefijo + "_existe"] = existe;
            d[prefijo + "_codigos_enlace"] = existe ? Ordenada(codigosEnlace) : null;
            d[prefijo + "_codigos_rotura"] = existe ? Ordenada(codigosRotura) : null;
            d[prefijo + "_voladizo"] = existe ? voladizo : null;
            d[prefijo + "_contornos"] = existe ? Ordenada(contornos) : null;
        }

        /// <summary>
        /// Estado esperado después de crear: las superficies del corredor más las dos nuevas (ordenadas como las lee el
        /// plugin), cada una con su código como único código de enlace y como línea de rotura, su voladizo y el contorno
        /// de la extensión del corredor, y el corredor reconstruido.
        /// </summary>
        public static Dictionary<string, object> Esperado(IEnumerable<string> superficiesAntes, string top, string datum,
            string codigoTop, string codigoDatum, string voladizoTop, string voladizoDatum)
        {
            var superficies = (superficiesAntes ?? new List<string>()).Where(x => x != null).ToList();
            superficies.Add(top);
            superficies.Add(datum);
            var d = new Dictionary<string, object> { ["superficies"] = Ordenada(superficies) };
            AnadirEstado(d, PrefijoTop, true, new[] { codigoTop }, new[] { codigoTop }, voladizoTop, new[] { NombreContorno });
            AnadirEstado(d, PrefijoDatum, true, new[] { codigoDatum }, new[] { codigoDatum }, voladizoDatum, new[] { NombreContorno });
            d["esta_desactualizado"] = false;
            return d;
        }

        /// <summary>
        /// Avisos de la relectura de una superficie recién creada (no son errores: el cambio pedido ya se verificó):
        /// contorno que no quedó exterior o cuyo tipo no se pudo leer, IsBuild en false y superficie de corredor sin
        /// superficie en el dibujo.
        /// </summary>
        public static List<string> AvisosRelectura(string superficie, string tipoContorno, bool? construir, bool tieneSuperficieDibujo)
        {
            var avisos = new List<string>();
            if (tipoContorno == null)
                avisos.Add("No se pudo leer el tipo del contorno '" + NombreContorno + "' de '" + superficie + "': compruébalo con estado_corredor (contornos).");
            else if (!string.Equals(tipoContorno.Trim(), TipoContornoExterior, StringComparison.OrdinalIgnoreCase))
                avisos.Add("El contorno '" + NombreContorno + "' de '" + superficie + "' quedó de tipo '" + tipoContorno + "', no '" + TipoContornoExterior + "' (contorno exterior): revísalo en Propiedades del corredor → Contornos.");
            if (construir == false)
                avisos.Add("La superficie de corredor '" + superficie + "' tiene IsBuild = false: puede que Civil 3D no la construya al reconstruir el corredor. Compruébalo con listar_superficies y estado_corredor; la herramienta no lo cambia (anótalo para la memoria técnica).");
            if (!tieneSuperficieDibujo)
                avisos.Add("La superficie de corredor '" + superficie + "' no tiene todavía su superficie en el dibujo (SurfaceId vacío después de reconstruir): no aparecerá en listar_superficies ni servirá para calcular volúmenes hasta que Civil 3D la construya. Prueba reconstruir_corredor y estado_corredor.");
            return avisos;
        }

        // ------------------------------------------------------------------ utilidades
        /// <summary>Lista ordenada sin distinguir mayúsculas (como Herramientas.Ordenada del plugin); null si la entrada es null.</summary>
        public static List<string> Ordenada(IEnumerable<string> l)
            => l == null ? null : l.Where(x => x != null).OrderBy(x => x, SinMayusculas).ToList();

        private static List<string> Limpia(IEnumerable<string> l)
            => (l ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(SinMayusculas).ToList();
    }
}

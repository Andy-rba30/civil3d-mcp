using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ArbaMcp.Nucleo
{
    /// <summary>Un punto (x = este, y = norte, z = cota) leído de un archivo de puntos.</summary>
    public struct Punto3
    {
        public double X, Y, Z;
        public Punto3(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    /// <summary>Un formato de archivo de puntos del dibujo (nombre y delimitador), tal como lo expone Civil 3D.</summary>
    public sealed class FormatoPuntos
    {
        public string Nombre;
        public string Delimitador;
        public override string ToString() => Nombre;
    }

    /// <summary>Mínimos y máximos de una lista de puntos (mismos nombres de campo que extension_superficie).</summary>
    public sealed class ExtensionPuntos
    {
        public double MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
        public int N;

        /// <summary>Extensión de una lista de puntos; null si la lista está vacía.</summary>
        public static ExtensionPuntos Calcular(IEnumerable<Punto3> puntos)
        {
            ExtensionPuntos e = null;
            foreach (var p in puntos)
            {
                if (e == null) e = new ExtensionPuntos { MinX = p.X, MaxX = p.X, MinY = p.Y, MaxY = p.Y, MinZ = p.Z, MaxZ = p.Z };
                else
                {
                    e.MinX = Math.Min(e.MinX, p.X); e.MaxX = Math.Max(e.MaxX, p.X);
                    e.MinY = Math.Min(e.MinY, p.Y); e.MaxY = Math.Max(e.MaxY, p.Y);
                    e.MinZ = Math.Min(e.MinZ, p.Z); e.MaxZ = Math.Max(e.MaxZ, p.Z);
                }
                e.N++;
            }
            return e;
        }

        /// <summary>Diccionario con min_x, max_x, min_y, max_y, min_z, max_z redondeados a 4 decimales.</summary>
        public Dictionary<string, object> ComoDiccionario() => Diccionario(MinX, MaxX, MinY, MaxY, MinZ, MaxZ);

        public static Dictionary<string, object> Diccionario(double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
            => new Dictionary<string, object>
            {
                ["min_x"] = Argumentos.Redondear(minX), ["max_x"] = Argumentos.Redondear(maxX),
                ["min_y"] = Argumentos.Redondear(minY), ["max_y"] = Argumentos.Redondear(maxY),
                ["min_z"] = Argumentos.Redondear(minZ), ["max_z"] = Argumentos.Redondear(maxZ)
            };
    }

    /// <summary>Lo que se sabe de un archivo de puntos de texto sin Civil 3D: líneas, separador, columnas y extensión.</summary>
    public sealed class ResumenArchivoPuntos
    {
        public string Ruta;
        /// <summary>Líneas totales del archivo.</summary>
        public int Lineas;
        /// <summary>Líneas con coordenadas legibles (lo que Civil 3D debería importar como puntos).</summary>
        public int LineasPuntos;
        /// <summary>Líneas en blanco, comentarios (#), cabeceras o líneas sin coordenadas legibles.</summary>
        public int LineasIgnoradas;
        /// <summary>Separador detectado: ",", ";", "\t" o " " (null si no se pudo decidir).</summary>
        public string Separador;
        /// <summary>Nombre del separador en español: coma, punto y coma, tabulador, espacio.</summary>
        public string SeparadorNombre => ArchivoPuntos.NombreSeparador(Separador);
        /// <summary>Número de campos de la primera línea de puntos.</summary>
        public int Columnas;
        /// <summary>Columnas usadas como este, norte y cota (índices desde 0); null si no se pudo decidir.</summary>
        public int[] ColumnasENZ;
        public List<Punto3> Puntos = new List<Punto3>();
        public ExtensionPuntos Extension;

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["lineas"] = Lineas,
            ["lineas_puntos"] = LineasPuntos,
            ["lineas_ignoradas"] = LineasIgnoradas,
            ["separador"] = SeparadorNombre,
            ["columnas"] = Columnas,
            ["columnas_este_norte_cota"] = ColumnasENZ == null ? null : ColumnasENZ.Select(i => i + 1).ToArray()
        };
    }

    /// <summary>
    /// Lectura de un archivo de puntos de texto (PENZD, ENZ, etc.) sin Civil 3D: detecta el separador, cuenta las líneas
    /// de puntos y calcula la extensión. Sirve para la simulación de crear_superficie_tin y para elegir el formato.
    /// </summary>
    public static class ArchivoPuntos
    {
        private static readonly string[] Candidatos = { ",", ";", "\t", " " };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string NombreSeparador(string sep)
        {
            switch (sep)
            {
                case ",": return "coma";
                case ";": return "punto y coma";
                case "\t": return "tabulador";
                case " ": return "espacio";
                default: return null;
            }
        }

        /// <summary>Lee el archivo completo. 'nombreFormato' (opcional) decide qué columnas son este, norte y cota.</summary>
        public static ResumenArchivoPuntos Leer(string ruta, string nombreFormato = null)
        {
            if (string.IsNullOrWhiteSpace(ruta)) throw new ArgumentException("Falta la ruta del archivo de puntos.");
            if (!File.Exists(ruta)) throw new FileNotFoundException("No existe el archivo de puntos '" + ruta + "'. Comprueba la ruta en la PC donde corre Civil 3D.", ruta);
            var lineas = File.ReadAllLines(ruta, Encoding.UTF8);
            var r = Analizar(lineas, nombreFormato);
            r.Ruta = ruta;
            return r;
        }

        /// <summary>Igual que Leer, sobre las líneas ya leídas (para las pruebas y para archivos pequeños).</summary>
        public static ResumenArchivoPuntos Analizar(IList<string> lineas, string nombreFormato = null)
        {
            var r = new ResumenArchivoPuntos { Lineas = lineas.Count };
            var datos = lineas.Where(EsLineaDeDatos).ToList();
            r.Separador = DetectarSeparador(datos);
            if (r.Separador == null) { r.LineasIgnoradas = r.Lineas; return r; }

            var primera = Campos(datos[0], r.Separador);
            r.Columnas = primera.Length;
            r.ColumnasENZ = SuperficieTin.ColumnasENZ(nombreFormato, r.Columnas);
            foreach (var l in datos)
            {
                var c = Campos(l, r.Separador);
                if (r.ColumnasENZ != null && LeerPunto(c, r.ColumnasENZ, r.Separador, out Punto3 p)) { r.Puntos.Add(p); r.LineasPuntos++; }
                else if (r.ColumnasENZ == null && c.Count(x => EsNumero(x, r.Separador)) >= 3) r.LineasPuntos++;
            }
            r.LineasIgnoradas = r.Lineas - r.LineasPuntos;
            r.Extension = ExtensionPuntos.Calcular(r.Puntos);
            return r;
        }

        /// <summary>
        /// Separador con el que todas las líneas de datos tienen el mismo número de campos (al menos 3) y más campos
        /// numéricos; en empate, coma, punto y coma, tabulador y espacio, en ese orden. Null si ninguno sirve.
        /// </summary>
        public static string DetectarSeparador(IEnumerable<string> lineasDeDatos)
        {
            var datos = lineasDeDatos.Where(EsLineaDeDatos).Take(200).ToList();
            if (datos.Count == 0) return null;
            string mejor = null; int mejorNumericos = -1;
            foreach (var sep in Candidatos)
            {
                int n = -1; int numericos = 0; bool vale = true, hayCoordenadas = false;
                foreach (var l in datos)
                {
                    var c = Campos(l, sep);
                    if (c.Length < 3 || (n >= 0 && c.Length != n)) { vale = false; break; }
                    n = c.Length;
                    int k = c.Count(x => EsNumero(x, sep));
                    numericos += k;
                    if (k >= 3) hayCoordenadas = true;   // al menos una línea con este, norte y cota
                }
                if (vale && hayCoordenadas && numericos > mejorNumericos) { mejor = sep; mejorNumericos = numericos; }
            }
            return mejor;
        }

        /// <summary>Una línea cuenta como datos si no está en blanco ni empieza por '#' (comentario de Civil 3D).</summary>
        public static bool EsLineaDeDatos(string l) => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#");

        public static string[] Campos(string linea, string sep)
            => sep == " "
                ? linea.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                : linea.Split(new[] { sep }, StringSplitOptions.None).Select(c => c.Trim()).ToArray();

        /// <summary>Número con punto decimal; si el separador no es la coma, también se acepta la coma decimal.</summary>
        public static bool EsNumero(string campo, string sep) => Numero(campo, sep).HasValue;

        public static double? Numero(string campo, string sep)
        {
            if (string.IsNullOrWhiteSpace(campo)) return null;
            string s = campo.Trim();
            if (sep != "," && s.Contains(',') && !s.Contains('.')) s = s.Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, Inv, out double d) && !double.IsNaN(d) && !double.IsInfinity(d) ? d : (double?)null;
        }

        private static bool LeerPunto(string[] campos, int[] enz, string sep, out Punto3 p)
        {
            p = default;
            if (enz.Max() >= campos.Length) return false;
            var x = Numero(campos[enz[0]], sep); var y = Numero(campos[enz[1]], sep); var z = Numero(campos[enz[2]], sep);
            if (!x.HasValue || !y.HasValue || !z.HasValue) return false;
            p = new Punto3(x.Value, y.Value, z.Value);
            return true;
        }
    }

    /// <summary>
    /// Reglas de crear_superficie_tin que no dependen de Civil 3D: origen (grupo o archivo), intervalos de curvas,
    /// elección del formato de archivo de puntos, columnas de un formato y aviso sobre los intervalos (viven en el estilo).
    /// </summary>
    public static class SuperficieTin
    {
        public const string OrigenGrupo = "grupo_puntos";
        public const string OrigenArchivo = "archivo";

        /// <summary>Exactamente uno de 'grupo_puntos' o 'archivo'. Devuelve OrigenGrupo u OrigenArchivo; lanza si ninguno o los dos.</summary>
        public static string Origen(string grupoPuntos, string archivo)
        {
            bool g = !string.IsNullOrWhiteSpace(grupoPuntos), a = !string.IsNullOrWhiteSpace(archivo);
            if (g && a) throw new ArgumentException("Indica solo uno: 'grupo_puntos' (un grupo de puntos del dibujo) o 'archivo' (ruta de un archivo de puntos de texto), no los dos.");
            if (!g && !a) throw new ArgumentException("Falta el origen de los puntos: indica 'grupo_puntos' (un grupo de puntos del dibujo) o 'archivo' (ruta de un archivo de puntos de texto en la PC de Civil 3D).");
            return g ? OrigenGrupo : OrigenArchivo;
        }

        /// <summary>Los intervalos, si se pasan, deben ser mayores que 0 y el mayor no puede ser menor que el menor.</summary>
        public static void ValidarIntervalos(double? menor, double? mayor)
        {
            if (menor.HasValue && !(menor.Value > 0)) throw new ArgumentException("El parámetro 'intervalo_curvas_menor' debe ser mayor que 0 (por ejemplo 1).");
            if (mayor.HasValue && !(mayor.Value > 0)) throw new ArgumentException("El parámetro 'intervalo_curvas_mayor' debe ser mayor que 0 (por ejemplo 5).");
            if (menor.HasValue && mayor.HasValue && mayor.Value < menor.Value)
                throw new ArgumentException("El intervalo mayor (" + Texto(mayor.Value) + ") no puede ser menor que el intervalo menor (" + Texto(menor.Value) + ").");
        }

        /// <summary>
        /// Índices (desde 0) de las columnas este, norte y cota según el nombre del formato ("PENZD (comma delimited)" →
        /// 1, 2, 3; "PNEZD" → 2, 1, 3; "ENZ" → 0, 1, 2; "XYZ" → 0, 1, 2). Sin nombre o sin letras reconocibles, se
        /// supone PENZD con 4 o más columnas y ENZ con 3. Null si no se puede decidir.
        /// </summary>
        public static int[] ColumnasENZ(string nombreFormato, int nColumnas)
        {
            string token = (nombreFormato ?? "").Trim().Split(new[] { ' ', '(', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? "";
            if (token.Length >= 3 && token.All(ch => "PENZDXY".IndexOf(ch) >= 0))
            {
                int e = token.IndexOf('E'), n = token.IndexOf('N'), z = token.IndexOf('Z');
                if (e < 0) e = token.IndexOf('X');
                if (n < 0) n = token.IndexOf('Y');
                if (e >= 0 && n >= 0 && z >= 0 && (nColumnas <= 0 || (e < nColumnas && n < nColumnas && z < nColumnas))) return new[] { e, n, z };
                return null;
            }
            if (nColumnas >= 4) return new[] { 1, 2, 3 };
            if (nColumnas == 3) return new[] { 0, 1, 2 };
            return null;
        }

        /// <summary>
        /// Elige el formato de archivo de puntos. Con 'pedido', el de ese nombre (sin distinguir mayúsculas) o error con la
        /// lista. Sin 'pedido', uno cuyas columnas casen con el archivo (PENZD para 5 columnas, PENZ para 4, ENZ para 3) y
        /// cuyo delimitador sea el separador detectado; 'motivo' explica la elección. Null si no hay ninguno razonable.
        /// </summary>
        public static FormatoPuntos ElegirFormato(IList<FormatoPuntos> disponibles, string pedido, string separador, int columnas, out string motivo)
        {
            disponibles = disponibles ?? new List<FormatoPuntos>();
            if (!string.IsNullOrWhiteSpace(pedido))
            {
                var f = disponibles.FirstOrDefault(x => string.Equals((x.Nombre ?? "").Trim(), pedido.Trim(), StringComparison.OrdinalIgnoreCase));
                if (f == null) throw new ArgumentException("No existe el formato de archivo de puntos '" + pedido + "' en el dibujo. Formatos disponibles: " + ListaNombres(disponibles) + ". En Civil 3D se ven en Espacio de herramientas → Configuración → Punto → Formatos de archivo de puntos.");
                motivo = "indicado en 'formato_archivo'";
                return f;
            }
            string[] prefijos = columnas >= 5 ? new[] { "PENZD", "PENZ" } : columnas == 4 ? new[] { "PENZ", "PENZD" } : columnas == 3 ? new[] { "ENZ", "XYZ", "NEZ" } : new[] { "PENZD" };
            foreach (var pre in prefijos)
            {
                var f = disponibles.FirstOrDefault(x => Empieza(x.Nombre, pre) && SeparadorCoincide(x, separador));
                if (f != null) { motivo = "elegido automáticamente: columnas " + pre + " con separador " + (ArchivoPuntos.NombreSeparador(separador) ?? "?"); return f; }
            }
            if (separador != null)
            {
                var f = disponibles.FirstOrDefault(x => SeparadorCoincide(x, separador) && ColumnasENZ(x.Nombre, columnas) != null);
                if (f != null) { motivo = "elegido automáticamente por el separador " + ArchivoPuntos.NombreSeparador(separador); return f; }
            }
            motivo = null;
            return null;
        }

        /// <summary>Mensaje de error cuando no se pudo elegir formato: pide 'formato_archivo' y lista los disponibles.</summary>
        public static string MensajeSinFormato(IList<FormatoPuntos> disponibles, string separador, int columnas)
            => "No se pudo elegir un formato de archivo de puntos para un archivo de " + columnas + " columnas separadas por " + (ArchivoPuntos.NombreSeparador(separador) ?? "un separador desconocido")
             + ". Indica 'formato_archivo' con uno de los formatos del dibujo: " + ListaNombres(disponibles) + ".";

        /// <summary>El delimitador del formato (carácter o palabra del nombre: comma/coma, semicolon, tab, space/espacio) coincide con el separador detectado.</summary>
        public static bool SeparadorCoincide(FormatoPuntos f, string separador)
        {
            if (f == null || separador == null) return false;
            string d = f.Delimitador;
            if (!string.IsNullOrEmpty(d) && d.Length == 1) return d == separador;
            string n = (f.Nombre ?? "").ToLowerInvariant();
            switch (separador)
            {
                case ",": return n.Contains("comma") || n.Contains("coma");
                case ";": return n.Contains("semicolon") || n.Contains("punto y coma");
                case "\t": return n.Contains("tab");
                case " ": return n.Contains("space") || n.Contains("espacio");
                default: return false;
            }
        }

        /// <summary>
        /// Aviso sobre los intervalos de curvas de nivel pedidos: en la API de Civil 3D 2027 viven en el estilo de
        /// superficie (SurfaceStyle.ContourStyle.MinorContourInterval / MajorContourInterval), no en la superficie, y el
        /// plugin no modifica estilos existentes. Null si no se pidió ningún intervalo.
        /// </summary>
        public static string AvisoIntervalos(double? menor, double? mayor, string estilo, double? estiloMenor, double? estiloMayor)
        {
            if (!menor.HasValue && !mayor.HasValue) return null;
            string pedido = (menor.HasValue ? "menor " + Texto(menor.Value) : "") + (menor.HasValue && mayor.HasValue ? " y " : "") + (mayor.HasValue ? "mayor " + Texto(mayor.Value) : "");
            string actual = estiloMenor.HasValue || estiloMayor.HasValue
                ? " El estilo '" + estilo + "' tiene menor " + (estiloMenor.HasValue ? Texto(estiloMenor.Value) : "?") + " y mayor " + (estiloMayor.HasValue ? Texto(estiloMayor.Value) : "?") + "."
                : "";
            bool coinciden = (!menor.HasValue || (estiloMenor.HasValue && Math.Abs(estiloMenor.Value - menor.Value) < 1e-6))
                          && (!mayor.HasValue || (estiloMayor.HasValue && Math.Abs(estiloMayor.Value - mayor.Value) < 1e-6));
            if (coinciden && (estiloMenor.HasValue || estiloMayor.HasValue))
                return "Intervalos de curvas pedidos (" + pedido + "): ya coinciden con los del estilo '" + estilo + "', así que no hubo que cambiar nada." + actual;
            return "Intervalos de curvas pedidos (" + pedido + ") sin aplicar: en Civil 3D 2027 los intervalos viven en el estilo de superficie (ContourStyle.MinorContourInterval / MajorContourInterval), no en la superficie, y el plugin no modifica ni crea estilos."
                 + actual + " Para verlos así, elige con 'estilo' un estilo del dibujo que ya tenga esos intervalos o ajústalos en Civil 3D (Espacio de herramientas → Configuración → Superficie → Estilos de superficie).";
        }

        private static bool Empieza(string nombre, string prefijo) => (nombre ?? "").Trim().StartsWith(prefijo, StringComparison.OrdinalIgnoreCase);
        private static string ListaNombres(IEnumerable<FormatoPuntos> f) { var l = f.Select(x => "'" + x.Nombre + "'").ToList(); return l.Count == 0 ? "ninguno" : string.Join(", ", l); }
        private static string Texto(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}

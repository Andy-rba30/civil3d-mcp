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
    /// Lo que el plugin lee de un estilo de superficie del dibujo para elegirlo por sus curvas de nivel (1.17.2):
    /// nombre, intervalos (SurfaceStyle.ContourStyle) y si dibuja en planta las curvas menores y mayores
    /// (GetDisplayStylePlan(...).Visible). Lo que no se pudo leer queda en null.
    /// </summary>
    public sealed class EstiloCurvas
    {
        public string Nombre;
        public double? Menor, Mayor;
        /// <summary>True o false si se pudo leer si el estilo dibuja en planta las curvas menores o mayores; null si no.</summary>
        public bool? MenorVisible, MayorVisible;
    }

    /// <summary>
    /// Qué estilo usa crear_superficie_tin cuando se piden intervalos de curvas (1.17.2) y por qué. Se calcula sin
    /// Civil 3D con SuperficieTin.ElegirEstilo; el plugin solo lee los estilos, nunca los modifica ni crea.
    /// </summary>
    public sealed class EleccionEstilo
    {
        /// <summary>Sin 'estilo': la herramienta eligió un estilo del dibujo con los intervalos pedidos.</summary>
        public const string Automatica = "automatica";
        /// <summary>Con 'estilo': se respeta siempre, coincida o no.</summary>
        public const string Indicada = "indicada";
        /// <summary>Sin 'estilo' y ningún estilo del dibujo coincide: el estilo por defecto, como antes de la 1.17.2.</summary>
        public const string PorDefecto = "por_defecto";

        /// <summary>Nombre del estilo con el que se crea la superficie; null = el estilo por defecto del dibujo.</summary>
        public string Estilo;
        /// <summary>Automatica, Indicada o PorDefecto.</summary>
        public string Eleccion;
        /// <summary>Explicación para 'avisos' y para intervalos_curvas.motivo.</summary>
        public string Motivo;
        /// <summary>Estilo que se usará, tal como se leyó (el elegido, el indicado o el por defecto); null si no se conoce.</summary>
        public EstiloCurvas Usado;
        /// <summary>Otros estilos del dibujo con los intervalos pedidos y las curvas no ocultas, en el orden de la regla.</summary>
        public List<string> OtrosQueCoinciden = new List<string>();
        /// <summary>Estilos con los intervalos pedidos que no dibujan esas curvas en planta (descartados).</summary>
        public List<string> DescartadosSinCurvasVisibles = new List<string>();
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
            => AvisoIntervalos(menor, mayor, estilo, estiloMenor, estiloMayor, null, null);

        /// <summary>
        /// Igual, con lo encontrado al recorrer los estilos del dibujo (1.17.2): 'alternativas' son los estilos que sí
        /// tienen los intervalos pedidos (null = no se buscó; vacía = no hay ninguno) y 'descartados' los que los tienen
        /// pero no dibujan esas curvas en planta.
        /// </summary>
        public static string AvisoIntervalos(double? menor, double? mayor, string estilo, double? estiloMenor, double? estiloMayor, IList<string> alternativas, IList<string> descartados)
        {
            if (!menor.HasValue && !mayor.HasValue) return null;
            string pedido = TextoPedido(menor, mayor);
            string actual = estiloMenor.HasValue || estiloMayor.HasValue
                ? " El estilo '" + estilo + "' tiene menor " + (estiloMenor.HasValue ? Texto(estiloMenor.Value) : "?") + " y mayor " + (estiloMayor.HasValue ? Texto(estiloMayor.Value) : "?") + "."
                : "";
            bool coinciden = Coinciden(menor, mayor, estiloMenor, estiloMayor);
            if (coinciden && (estiloMenor.HasValue || estiloMayor.HasValue))
                return "Intervalos de curvas pedidos (" + pedido + "): ya coinciden con los del estilo '" + estilo + "', así que no hubo que cambiar nada." + actual;
            string sinCurvas = descartados != null && descartados.Count > 0
                ? " Tienen esos intervalos pero no dibujan esas curvas en planta (descartados): " + Lista(descartados) + "."
                : "";
            string salida;
            if (alternativas != null && alternativas.Count > 0)
                salida = " Estilos del dibujo con esos intervalos: " + Lista(alternativas) + "; pasa uno en 'estilo', o quita 'estilo' y la herramienta elige uno. El estilo indicado se respeta y no se cambia.";
            else if (alternativas != null)
                salida = " Ningún estilo de superficie del dibujo tiene esos intervalos con las curvas visibles en planta (tolerancia " + Texto(ToleranciaIntervalos) + "); crea o ajusta uno en Civil 3D (Espacio de herramientas → Configuración → Superficie → Estilos de superficie) o pide otros intervalos.";
            else
                salida = " Para verlos así, elige con 'estilo' un estilo del dibujo que ya tenga esos intervalos o ajústalos en Civil 3D (Espacio de herramientas → Configuración → Superficie → Estilos de superficie).";
            return "Intervalos de curvas pedidos (" + pedido + ") sin aplicar: en Civil 3D 2027 los intervalos viven en el estilo de superficie (ContourStyle.MinorContourInterval / MajorContourInterval), no en la superficie, y el plugin no modifica ni crea estilos."
                 + actual + sinCurvas + salida;
        }

        // ------------------------------------------------------------------ 1.17.2: estilo elegido por sus curvas
        /// <summary>Diferencia máxima entre el intervalo pedido y el del estilo para que cuenten como iguales.</summary>
        public const double ToleranciaIntervalos = 0.001;

        /// <summary>Lugar de la API donde viven los intervalos (campo donde_viven de la respuesta).</summary>
        public const string DondeVivenIntervalos = "SurfaceStyle.ContourStyle (estilo de superficie)";

        /// <summary>True si el intervalo del estilo existe y está a ±0,001 del pedido.</summary>
        public static bool IntervaloCoincide(double pedido, double? delEstilo)
            => delEstilo.HasValue && Math.Abs(delEstilo.Value - pedido) <= ToleranciaIntervalos + 1e-9;

        /// <summary>
        /// True si se pidió al menos un intervalo y cada intervalo pedido coincide (±0,001) con el del estilo. El que no
        /// se pide no se compara (con solo el menor, vale cualquier mayor).
        /// </summary>
        public static bool Coinciden(double? menor, double? mayor, double? estiloMenor, double? estiloMayor)
        {
            if (!menor.HasValue && !mayor.HasValue) return false;
            return (!menor.HasValue || IntervaloCoincide(menor.Value, estiloMenor))
                && (!mayor.HasValue || IntervaloCoincide(mayor.Value, estiloMayor));
        }

        /// <summary>
        /// Si el estilo dibuja en planta las curvas pedidas: false si se sabe que alguna pedida está oculta, true si se
        /// sabe que todas las pedidas se ven, null si no se pudo leer.
        /// </summary>
        public static bool? CurvasVisibles(EstiloCurvas e, double? menor, double? mayor)
        {
            if (e == null) return null;
            var pedidas = new List<bool?>();
            if (menor.HasValue) pedidas.Add(e.MenorVisible);
            if (mayor.HasValue) pedidas.Add(e.MayorVisible);
            if (pedidas.Count == 0) return null;
            if (pedidas.Any(v => v == false)) return false;
            if (pedidas.All(v => v == true)) return true;
            return null;
        }

        /// <summary>Intervalos pedidos aplicados: el estilo coincide (±0,001) y no se sabe que oculte esas curvas.</summary>
        public static bool Aplicados(double? menor, double? mayor, EstiloCurvas usado)
            => usado != null && Coinciden(menor, mayor, usado.Menor, usado.Mayor) && CurvasVisibles(usado, menor, mayor) != false;

        /// <summary>
        /// Familia de un estilo: el texto del último paréntesis al final del nombre, en minúsculas
        /// ("Contours 1m and 5m (Background)" → "background"). Null si el nombre no termina en un paréntesis.
        /// </summary>
        public static string Familia(string nombre)
        {
            string n = (nombre ?? "").Trim();
            if (!n.EndsWith(")")) return null;
            int i = n.LastIndexOf('(');
            if (i < 0) return null;
            string f = n.Substring(i + 1, n.Length - i - 2).Trim();
            return f.Length == 0 ? null : f.ToLowerInvariant();
        }

        /// <summary>
        /// Estilo con el que se crea la superficie cuando se piden intervalos. Null si no se pidió ninguno (la herramienta
        /// se comporta como antes de la 1.17.2). Candidatos: los estilos cuyos intervalos coinciden con los pedidos
        /// (±0,001) y que no se sabe que oculten esas curvas en planta (esos se descartan y se nombran). Orden fijo:
        /// 1) los que se sabe que dibujan las curvas, antes que los de visibilidad desconocida; 2) el propio estilo por
        /// defecto; 3) los de su misma familia («Background», «Design»); 4) orden alfabético. Con 'estiloIndicado' se
        /// respeta siempre y solo se avisa si no coincide; sin candidatos, el estilo por defecto con el aviso de siempre.
        /// </summary>
        public static EleccionEstilo ElegirEstilo(IList<EstiloCurvas> estilos, double? menor, double? mayor, string estiloIndicado, string estiloPorDefecto)
        {
            if (!menor.HasValue && !mayor.HasValue) return null;
            var lista = (estilos ?? new List<EstiloCurvas>()).Where(e => e != null && !string.IsNullOrWhiteSpace(e.Nombre)).ToList();
            string porDefecto = string.IsNullOrWhiteSpace(estiloPorDefecto) ? null : estiloPorDefecto.Trim();
            string familiaDefecto = Familia(porDefecto);
            var coincidentes = lista.Where(e => Coinciden(menor, mayor, e.Menor, e.Mayor)).ToList();
            var descartados = coincidentes.Where(e => CurvasVisibles(e, menor, mayor) == false).Select(e => e.Nombre).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            var candidatos = coincidentes.Where(e => CurvasVisibles(e, menor, mayor) != false)
                .OrderBy(e => CurvasVisibles(e, menor, mayor) == true ? 0 : 1)
                .ThenBy(e => MismoNombre(e.Nombre, porDefecto) ? 0 : 1)
                .ThenBy(e => familiaDefecto != null && Familia(e.Nombre) == familiaDefecto ? 0 : 1)
                .ThenBy(e => e.Nombre, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Nombre, StringComparer.Ordinal)
                .ToList();
            var r = new EleccionEstilo { DescartadosSinCurvasVisibles = descartados };

            if (!string.IsNullOrWhiteSpace(estiloIndicado))
            {
                string indicado = estiloIndicado.Trim();
                r.Eleccion = EleccionEstilo.Indicada;
                r.Usado = lista.FirstOrDefault(e => MismoNombre(e.Nombre, indicado));
                r.Estilo = r.Usado?.Nombre ?? indicado;
                r.OtrosQueCoinciden = candidatos.Where(e => !MismoNombre(e.Nombre, indicado)).Select(e => e.Nombre).ToList();
                if (Aplicados(menor, mayor, r.Usado))
                    r.Motivo = AvisoIntervalos(menor, mayor, r.Estilo, r.Usado.Menor, r.Usado.Mayor) + NotaVisibilidad(r.Usado, menor, mayor);
                else if (r.Usado != null && Coinciden(menor, mayor, r.Usado.Menor, r.Usado.Mayor))
                    r.Motivo = "Intervalos de curvas pedidos (" + TextoPedido(menor, mayor) + "): el estilo indicado '" + r.Estilo + "' los tiene, pero no dibuja esas curvas en planta, así que no se verán. El estilo indicado se respeta y no se cambia."
                             + (r.OtrosQueCoinciden.Count > 0 ? " Estilos del dibujo con esos intervalos y las curvas visibles: " + Lista(r.OtrosQueCoinciden) + "." : "");
                else
                    r.Motivo = AvisoIntervalos(menor, mayor, r.Estilo, r.Usado?.Menor, r.Usado?.Mayor, r.OtrosQueCoinciden, null);
                return r;
            }

            if (candidatos.Count == 0)
            {
                r.Eleccion = EleccionEstilo.PorDefecto;
                r.Usado = porDefecto == null ? null : lista.FirstOrDefault(e => MismoNombre(e.Nombre, porDefecto));
                r.Motivo = AvisoIntervalos(menor, mayor, r.Usado?.Nombre ?? porDefecto ?? "por defecto", r.Usado?.Menor, r.Usado?.Mayor, new List<string>(), descartados);
                return r;
            }

            var elegido = candidatos[0];
            r.Eleccion = EleccionEstilo.Automatica;
            r.Usado = elegido;
            r.Estilo = elegido.Nombre;
            r.OtrosQueCoinciden = candidatos.Skip(1).Select(e => e.Nombre).ToList();
            string porque;
            if (MismoNombre(elegido.Nombre, porDefecto)) porque = ", que además es el estilo por defecto del dibujo.";
            else if (familiaDefecto != null && Familia(elegido.Nombre) == familiaDefecto && candidatos.Count > 1)
                porque = ", de la misma familia («" + FamiliaOriginal(elegido.Nombre) + "») que el estilo por defecto del dibujo '" + porDefecto + "'.";
            else if (candidatos.Count > 1 && porDefecto == null) porque = "; hay varios y no se pudo leer el estilo por defecto del dibujo, así que se toma el primero por orden alfabético.";
            else if (candidatos.Count > 1) porque = "; hay varios y ninguno es de la familia del estilo por defecto del dibujo '" + porDefecto + "', así que se toma el primero por orden alfabético.";
            else porque = " (el único del dibujo con esos intervalos).";
            r.Motivo = "Intervalos de curvas pedidos (" + TextoPedido(menor, mayor) + ") aplicados sin 'estilo': se usa el estilo '" + elegido.Nombre + "', que tiene menor "
                     + (elegido.Menor.HasValue ? Texto(elegido.Menor.Value) : "?") + " y mayor " + (elegido.Mayor.HasValue ? Texto(elegido.Mayor.Value) : "?")
                     + " (tolerancia " + Texto(ToleranciaIntervalos) + ")" + porque
                     + NotaVisibilidad(elegido, menor, mayor)
                     + (r.OtrosQueCoinciden.Count > 0 ? " También coinciden: " + Lista(r.OtrosQueCoinciden) + "." : "")
                     + (descartados.Count > 0 ? " Tienen esos intervalos pero no dibujan esas curvas en planta (descartados): " + Lista(descartados) + "." : "")
                     + " Pasa 'estilo' para usar otro. No se modificó ni se creó ningún estilo.";
            return r;
        }

        /// <summary>
        /// Bloque intervalos_curvas de la respuesta: lo pedido, si quedó aplicado con el estilo 'usado' (el previsto en la
        /// simulación, el releído de la superficie en la escritura real) y por qué. Sin intervalos pedidos (eleccion
        /// null) da el bloque de siempre con aplicados=false.
        /// </summary>
        public static Dictionary<string, object> ResumenIntervalos(double? menor, double? mayor, EleccionEstilo eleccion, EstiloCurvas usado)
        {
            bool? visibles = CurvasVisibles(usado, menor, mayor);
            return new Dictionary<string, object>
            {
                ["pedido_menor"] = menor,
                ["pedido_mayor"] = mayor,
                ["aplicados"] = Aplicados(menor, mayor, usado),
                ["estilo"] = usado?.Nombre ?? eleccion?.Estilo,
                ["eleccion"] = eleccion?.Eleccion,
                ["motivo"] = eleccion?.Motivo,
                ["otros_que_coinciden"] = eleccion?.OtrosQueCoinciden ?? new List<string>(),
                ["descartados_sin_curvas_visibles"] = eleccion?.DescartadosSinCurvasVisibles ?? new List<string>(),
                ["estilo_menor"] = usado?.Menor.HasValue == true ? Argumentos.Redondear(usado.Menor.Value) : null,
                ["estilo_mayor"] = usado?.Mayor.HasValue == true ? Argumentos.Redondear(usado.Mayor.Value) : null,
                ["curvas_visibles"] = (menor.HasValue || mayor.HasValue) ? visibles : null,
                ["tolerancia"] = ToleranciaIntervalos,
                ["donde_viven"] = DondeVivenIntervalos
            };
        }

        private static string NotaVisibilidad(EstiloCurvas e, double? menor, double? mayor)
            => CurvasVisibles(e, menor, mayor) == null ? " No se pudo comprobar si el estilo dibuja esas curvas en planta: compruébalo en pantalla." : "";

        private static string TextoPedido(double? menor, double? mayor)
            => (menor.HasValue ? "menor " + Texto(menor.Value) : "") + (menor.HasValue && mayor.HasValue ? " y " : "") + (mayor.HasValue ? "mayor " + Texto(mayor.Value) : "");

        private static bool MismoNombre(string a, string b)
            => a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string FamiliaOriginal(string nombre)
        {
            string n = (nombre ?? "").Trim();
            int i = n.LastIndexOf('(');
            return i < 0 || !n.EndsWith(")") ? "" : n.Substring(i + 1, n.Length - i - 2).Trim();
        }

        private static string Lista(IEnumerable<string> nombres) => string.Join(", ", nombres.Select(x => "'" + x + "'"));

        private static bool Empieza(string nombre, string prefijo) => (nombre ?? "").Trim().StartsWith(prefijo, StringComparison.OrdinalIgnoreCase);
        private static string ListaNombres(IEnumerable<FormatoPuntos> f) { var l = f.Select(x => "'" + x.Nombre + "'").ToList(); return l.Count == 0 ? "ninguno" : string.Join(", ", l); }
        private static string Texto(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}

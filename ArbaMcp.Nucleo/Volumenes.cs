using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>Una superficie del dibujo tal como la ve calcular_volumenes (lo que da listar_superficies).</summary>
    public sealed class SuperficieVolumen
    {
        public string Nombre;
        /// <summary>"TIN", "Grid", "TinVolume", "GridVolume" u otro nombre de clase.</summary>
        public string Tipo;
        /// <summary>Corredor que la genera; null si no es una superficie de corredor.</summary>
        public string Corredor;
        public SuperficieVolumen() { }
        public SuperficieVolumen(string nombre, string tipo, string corredor = null) { Nombre = nombre; Tipo = tipo; Corredor = corredor; }
    }

    /// <summary>Una superficie de corredor (Corridor.CorridorSurfaces) con sus códigos de vínculo y si ya tiene superficie en el dibujo.</summary>
    public sealed class SuperficieDeCorredorVolumen
    {
        public string Corredor;
        public string Nombre;
        public List<string> CodigosEnlace = new List<string>();
        /// <summary>True si CorridorSurface.SurfaceId no está vacío (la superficie existe en el dibujo).</summary>
        public bool TieneSuperficie;
        public SuperficieDeCorredorVolumen() { }
        public SuperficieDeCorredorVolumen(string corredor, string nombre, bool tieneSuperficie, params string[] codigos)
        {
            Corredor = corredor; Nombre = nombre; TieneSuperficie = tieneSuperficie;
            CodigosEnlace = (codigos ?? new string[0]).ToList();
        }
    }

    /// <summary>Superficies elegidas para la superficie de volumen y nombre de la nueva.</summary>
    public sealed class EleccionVolumen
    {
        public string Terreno;
        public string Datum;
        /// <summary>Corredor del Datum (null si el Datum no es de un corredor).</summary>
        public string Corredor;
        public string Nombre;
        public bool TerrenoAutomatico, DatumAutomatico, CorredorAutomatico, NombreAutomatico;
        public List<string> Avisos = new List<string>();
    }

    /// <summary>Lo que Civil 3D da de una superficie de volumen (TinVolumeSurface.GetVolumeProperties y los factores); null = no se pudo leer.</summary>
    public sealed class VolumenesCivil
    {
        public double? CorteSinAjustar, RellenoSinAjustar, NetoSinAjustar;
        public double? CorteAjustado, RellenoAjustado, NetoAjustado;
        public double? FactorCorte, FactorRelleno;
    }

    /// <summary>Corte, relleno y neto que devuelve calcular_volumenes, con la convención del plugin (neto = corte − relleno).</summary>
    public sealed class ResumenVolumenes
    {
        public double CorteSinAjustar, RellenoSinAjustar, NetoSinAjustar;
        public double Corte, Relleno, Neto;
        /// <summary>"sobra", "falta" o "compensado" (según el neto ajustado).</summary>
        public string Balance;
        public string Texto;
        /// <summary>Signo del neto de Civil 3D: "corte_menos_relleno", "relleno_menos_corte", "indeterminada" u "otra"; null si no se leyó.</summary>
        public string ConvencionNetoCivil3d;
        public List<string> Avisos = new List<string>();
    }

    /// <summary>Áreas de corte y de relleno de una sección (terreno frente a Datum) en el tramo de desplazamientos común.</summary>
    public sealed class AreasSeccion
    {
        /// <summary>Área donde el terreno queda por encima del Datum (se excava).</summary>
        public double Corte;
        /// <summary>Área donde el Datum queda por encima del terreno (se rellena).</summary>
        public double Relleno;
        /// <summary>Tramo de desplazamientos en que las dos secciones existen.</summary>
        public double Desde, Hasta;
    }

    /// <summary>Una línea de muestreo con las áreas de su sección (null si le falta la sección de alguna superficie).</summary>
    public sealed class SeccionLinea
    {
        public string Nombre;
        public double Progresiva;
        public AreasSeccion Areas;
        /// <summary>Por qué no tiene áreas (falta la sección de 'Terreno', no se solapan...); null si las tiene.</summary>
        public string Falta;
        public SeccionLinea() { }
        public SeccionLinea(string nombre, double progresiva, AreasSeccion areas, string falta = null) { Nombre = nombre; Progresiva = progresiva; Areas = areas; Falta = falta; }
    }

    /// <summary>
    /// Una comparación del supuesto de ejes (1.14.3): la cota de la sección de una superficie en un desplazamiento
    /// (Volumenes.CotaEn) frente a la de la superficie del dibujo en ese punto (Surface.FindElevationAtXY en
    /// Alignment.PointLocation(pk, desplazamiento)). Null la que no se pudo leer (fuera de la sección o de la TIN).
    /// </summary>
    public sealed class ComparacionCota
    {
        public string Superficie;
        public double Desplazamiento;
        public double? CotaSeccion, CotaSuperficie;
        public bool Comparable => CotaSeccion.HasValue && CotaSuperficie.HasValue && !double.IsNaN(CotaSeccion.Value) && !double.IsNaN(CotaSuperficie.Value)
                                  && !double.IsInfinity(CotaSeccion.Value) && !double.IsInfinity(CotaSuperficie.Value);
        public double? Diferencia => Comparable ? CotaSeccion.Value - CotaSuperficie.Value : (double?)null;
        public ComparacionCota() { }
        public ComparacionCota(string superficie, double desplazamiento, double? cotaSeccion, double? cotaSuperficie)
        {
            Superficie = superficie; Desplazamiento = desplazamiento; CotaSeccion = cotaSeccion; CotaSuperficie = cotaSuperficie;
        }
    }

    /// <summary>Una fila de la tabla por progresivas. Los volúmenes son los del tramo desde la línea anterior (null en la primera o sin áreas).</summary>
    public sealed class FilaVolumen
    {
        public string Linea;
        public double Progresiva;
        public double? AreaCorte, AreaRelleno;
        public double? VolumenCorte, VolumenRelleno;
        public double? VolumenCorteAjustado, VolumenRellenoAjustado;
        public double CorteAcumulado, RellenoAcumulado;
        /// <summary>Ordenada del diagrama de masas: corte ajustado acumulado − relleno ajustado acumulado.</summary>
        public double Masa;
        public string Falta;
    }

    /// <summary>Tabla de volúmenes por progresivas (método de las áreas medias).</summary>
    public sealed class TablaVolumenes
    {
        public List<FilaVolumen> Filas = new List<FilaVolumen>();
        public double Corte, Relleno, CorteAjustado, RellenoAjustado, Neto;
        public int Tramos, TramosSinVolumen;
        public List<string> Avisos = new List<string>();
    }

    /// <summary>Una sección de una línea de muestreo tal como la lee el plugin. Los ids van como texto (el handle); null si no se pudieron leer.</summary>
    public sealed class SeccionCandidata
    {
        /// <summary>Id de la propia sección (lo que da SampleLine.GetSectionIds).</summary>
        public string Clave;
        /// <summary>Section.SourceId.</summary>
        public string FuenteId;
        /// <summary>Section.CorridorSurfaceId.</summary>
        public string SuperficieCorredorId;
        /// <summary>Section.SourceName.</summary>
        public string NombreFuente;
        public SeccionCandidata() { }
        public SeccionCandidata(string clave, string fuenteId, string superficieCorredorId, string nombreFuente)
        {
            Clave = clave; FuenteId = fuenteId; SuperficieCorredorId = superficieCorredorId; NombreFuente = nombreFuente;
        }
    }

    /// <summary>La superficie cuya sección se busca: id de la superficie del dibujo (handle), su nombre y el corredor que la genera (null si no es de corredor).</summary>
    public sealed class SuperficieBuscada
    {
        public string Id;
        public string Nombre;
        public string Corredor;
        public SuperficieBuscada() { }
        public SuperficieBuscada(string id, string nombre, string corredor = null) { Id = id; Nombre = nombre; Corredor = corredor; }
    }

    /// <summary>Qué sección de la línea es la de la superficie (índice en la lista de candidatas; −1 si ninguna) y con qué intento se encontró.</summary>
    public sealed class SeccionIdentificada
    {
        public int Indice = -1;
        public string Metodo;
        public bool Encontrada => Indice >= 0;
    }

    /// <summary>Cuántas líneas de un grupo se identificaron con cada intento y cuántas no tienen sección de la superficie.</summary>
    public sealed class RecuentoIdentificacion
    {
        public Dictionary<string, int> Metodos = new Dictionary<string, int>();
        public int Lineas, SinSeccion;

        public void Anotar(SeccionIdentificada s)
        {
            Lineas++;
            if (s == null || !s.Encontrada) { SinSeccion++; return; }
            Metodos[s.Metodo] = Metodos.TryGetValue(s.Metodo, out int n) ? n + 1 : 1;
        }
    }

    /// <summary>
    /// Qué sección de una línea de muestreo es la de una superficie (calcular_volumenes, desde 1.14.2). Hasta la 1.14.1 se
    /// buscaba solo por Section.SourceId o por el nombre exacto, y la Datum no se encontraba: en sus secciones SourceId no
    /// es la superficie del dibujo y SourceName es "&lt;corredor&gt; &lt;superficie&gt;" ("Corredor 1 Corredor 1 - Datum",
    /// informe 1.14.0 del 14/11/2026, paso 6). Orden de intentos, el primero que encuentra gana: 1) la sección que
    /// devuelve SampleLine.GetSectionId(id de la superficie), si es de la línea; 2) Section.CorridorSurfaceId igual al id;
    /// 3) Section.SourceId igual al id; 4) SourceName igual al nombre de la superficie; 5) SourceName igual a
    /// "&lt;corredor&gt; &lt;superficie&gt;". Los nombres se comparan enteros (sin mayúsculas ni espacios de más), nunca por
    /// "contiene", para no confundir la sección del corredor ("Corredor 1"), la de la Top o "Terreno 2" con la buscada.
    /// </summary>
    public static class IdentificacionSecciones
    {
        public const string PorGetSectionId = "SampleLine.GetSectionId";
        public const string PorCorridorSurfaceId = "Section.CorridorSurfaceId";
        public const string PorSourceId = "Section.SourceId";
        public const string PorNombre = "Section.SourceName";
        public const string PorNombreConCorredor = "Section.SourceName (<corredor> <superficie>)";
        public static readonly IReadOnlyList<string> Orden = new[] { PorGetSectionId, PorCorridorSurfaceId, PorSourceId, PorNombre, PorNombreConCorredor };

        /// <summary>
        /// Sección de 'superficie' entre las de la línea. 'claveDeGetSectionId': el id que devolvió
        /// SampleLine.GetSectionId para esa superficie (null si no devolvió nada o lanzó); solo cuenta si es una de las
        /// secciones de la línea. 'excluidas': índices que ya son de otra superficie (no se pueden repetir).
        /// </summary>
        public static SeccionIdentificada Identificar(IList<SeccionCandidata> secciones, SuperficieBuscada superficie, string claveDeGetSectionId, ICollection<int> excluidas = null)
        {
            var r = new SeccionIdentificada();
            var l = secciones ?? new List<SeccionCandidata>();
            if (superficie == null) return r;
            bool Libre(int i) => l[i] != null && (excluidas == null || !excluidas.Contains(i));
            int Buscar(Func<SeccionCandidata, bool> cumple)
            {
                for (int i = 0; i < l.Count; i++) if (Libre(i) && cumple(l[i])) return i;
                return -1;
            }
            var intentos = new (string metodo, Func<SeccionCandidata, bool> cumple)[]
            {
                (PorGetSectionId, s => MismoId(s.Clave, claveDeGetSectionId)),
                (PorCorridorSurfaceId, s => MismoId(s.SuperficieCorredorId, superficie.Id)),
                (PorSourceId, s => MismoId(s.FuenteId, superficie.Id)),
                (PorNombre, s => NombreCoincide(s.NombreFuente, superficie.Nombre)),
                (PorNombreConCorredor, s => NombreConCorredorCoincide(s.NombreFuente, superficie.Corredor, superficie.Nombre))
            };
            foreach (var (metodo, cumple) in intentos)
            {
                int i = Buscar(cumple);
                if (i >= 0) { r.Indice = i; r.Metodo = metodo; return r; }
            }
            return r;
        }

        /// <summary>Las secciones del terreno y de la Datum de una línea: primero el terreno y luego la Datum, que nunca puede quedarse con la del terreno.</summary>
        public static (SeccionIdentificada terreno, SeccionIdentificada datum) IdentificarLasDos(IList<SeccionCandidata> secciones,
            SuperficieBuscada terreno, string claveTerreno, SuperficieBuscada datum, string claveDatum)
        {
            var t = Identificar(secciones, terreno, claveTerreno);
            var d = Identificar(secciones, datum, claveDatum, t.Encontrada ? new[] { t.Indice } : null);
            return (t, d);
        }

        /// <summary>True si el nombre de la fuente de la sección es el de la superficie (entero, sin mayúsculas ni espacios de más).</summary>
        public static bool NombreCoincide(string nombreFuente, string superficie)
            => !string.IsNullOrWhiteSpace(nombreFuente) && !string.IsNullOrWhiteSpace(superficie)
               && string.Equals(Normalizar(nombreFuente), Normalizar(superficie), StringComparison.OrdinalIgnoreCase);

        /// <summary>True si el nombre de la fuente es "&lt;corredor&gt; &lt;superficie&gt;" (el formato observado en las secciones de una superficie de corredor).</summary>
        public static bool NombreConCorredorCoincide(string nombreFuente, string corredor, string superficie)
            => !string.IsNullOrWhiteSpace(corredor) && NombreCoincide(nombreFuente, string.IsNullOrWhiteSpace(superficie) ? null : corredor.Trim() + " " + superficie.Trim());

        private static bool MismoId(string a, string b)
            => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string Normalizar(string s) => string.Join(" ", s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Lógica de calcular_volumenes (1.14.0) que no depende de Civil 3D: elección del terreno, del Datum, del corredor y
    /// del nombre de la superficie de volumen (con errores claros si falta o sobra alguno), validación de los factores
    /// de esponjamiento y compactación, resumen de corte, relleno y neto con la convención del plugin (corte ajustado =
    /// corte × esponjamiento, CutFactor; relleno ajustado = relleno × compactación, FillFactor; neto = corte − relleno),
    /// detección del signo del neto de Civil 3D, y la tabla por progresivas: áreas de corte y relleno de cada sección
    /// (terreno frente a Datum) y volúmenes entre líneas por áreas medias, con acumulados y ordenada de masas. Se prueba
    /// en ArbaMcp.Pruebas con datos sintéticos.
    /// </summary>
    public static class Volumenes
    {
        public const string CodigoDatum = "Datum";
        public const string SufijoDatum = " - Datum";
        public const string SufijoVolumen = " - Volumen";
        public const string TipoVolumenTin = "TinVolume";
        public const string Metodo = "áreas medias entre las secciones de la superficie base (terreno) y la de comparación (Datum) de cada línea de muestreo, calculadas por el plugin; no es la tabla de materiales de Civil 3D";
        public const string Convencion = "corte ajustado = corte × factor_esponjamiento (CutFactor de Civil 3D); relleno ajustado = relleno × factor_compactacion (FillFactor); neto = corte ajustado − relleno ajustado (positivo: sobra material; negativo: falta y hay que traer préstamo)";
        public const string Unidades = "unidades del dibujo al cubo (m³ si el dibujo está en metros)";
        /// <summary>Límite superior de un factor: por encima es casi seguro un error de escritura (por ejemplo 125 en vez de 1,25).</summary>
        public const double FactorMaximo = 5;
        public const double FactorHabitualMin = 0.5, FactorHabitualMax = 2;
        /// <summary>Tolerancia absoluta de volumen (unidades³) para comparar y decidir si el balance está compensado.</summary>
        public const double ToleranciaVolumen = 0.001;

        private static readonly StringComparer SinMayusculas = StringComparer.OrdinalIgnoreCase;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ factores
        /// <summary>
        /// Factor de esponjamiento o de compactación: 1 si no se pasa; error si no es un número finito mayor que 0 o
        /// pasa de 5 (casi seguro un error de escritura).
        /// </summary>
        public static double ValidarFactor(double? valor, string parametro)
        {
            if (!valor.HasValue) return 1;
            double v = valor.Value;
            if (double.IsNaN(v) || double.IsInfinity(v))
                throw new ArgumentException("'" + parametro + "' debe ser un número (por ejemplo 1.25); se recibió un valor no numérico.");
            if (v <= 0)
                throw new ArgumentException("'" + parametro + "' debe ser mayor que 0 (se recibió " + Txt(v) + "). Sin factor, pasa 1 o no lo pases.");
            if (v > FactorMaximo)
                throw new ArgumentException("'" + parametro + "' = " + Txt(v) + " es demasiado grande (máximo " + Txt(FactorMaximo) + "): un factor de 25 % se escribe 1.25, no 125 ni 25.");
            return v;
        }

        /// <summary>Aviso si el factor queda fuera de lo habitual (0,5 a 2); null si está dentro.</summary>
        public static string AvisoFactor(double valor, string parametro)
            => valor < FactorHabitualMin || valor > FactorHabitualMax
                ? "'" + parametro + "' = " + Txt(valor) + " es poco habitual (lo normal está entre " + Txt(FactorHabitualMin) + " y " + Txt(FactorHabitualMax) + "): revisa que no sea un error."
                : null;

        // ------------------------------------------------------------------ elección de superficies
        /// <summary>
        /// Elige el terreno (superficie base), el Datum (comparación), su corredor y el nombre de la superficie de
        /// volumen. Sin 'terreno': la única superficie TIN que no es de corredor ni de volumen. Sin 'datum': la superficie
        /// "&lt;corredor&gt; - Datum" del corredor pedido o del único corredor del dibujo (si no hay una con ese nombre, la
        /// única del corredor con el código de vínculo Datum). Lanza ArgumentException con la herramienta de lectura que
        /// corrige cada caso.
        /// </summary>
        public static EleccionVolumen Elegir(IEnumerable<SuperficieVolumen> superficies, IEnumerable<string> corredores,
            IEnumerable<SuperficieDeCorredorVolumen> superficiesCorredor, string terrenoPedido, string datumPedido, string corredorPedido, string nombrePedido)
        {
            var sup = (superficies ?? new List<SuperficieVolumen>()).Where(s => s != null && !string.IsNullOrWhiteSpace(s.Nombre)).ToList();
            var cors = (corredores ?? new List<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(SinMayusculas).ToList();
            var supCor = (superficiesCorredor ?? new List<SuperficieDeCorredorVolumen>()).Where(s => s != null && !string.IsNullOrWhiteSpace(s.Nombre)).ToList();
            var e = new EleccionVolumen();

            // Terreno
            if (!string.IsNullOrWhiteSpace(terrenoPedido))
            {
                var t = BuscarSuperficie(sup, supCor, terrenoPedido.Trim(), "terreno");
                if (!string.IsNullOrWhiteSpace(t.Corredor))
                    e.Avisos.Add("La superficie base '" + t.Nombre + "' la genera el corredor '" + t.Corredor + "': normalmente la base es el terreno natural. Revisa que sea la que quieres.");
                e.Terreno = t.Nombre;
            }
            else
            {
                var candidatas = sup.Where(s => EsTin(s.Tipo) && string.IsNullOrWhiteSpace(s.Corredor)).Select(s => s.Nombre).ToList();
                if (candidatas.Count == 0)
                    throw new ArgumentException("No hay ninguna superficie TIN de terreno en el dibujo (que no sea de un corredor ni de volumen). Superficies del dibujo: "
                        + Lista(sup.Select(s => s.Nombre)) + ". Pasa 'terreno' o crea el terreno con crear_superficie_tin; usa listar_superficies.");
                if (candidatas.Count > 1)
                    throw new ArgumentException("Hay varias superficies TIN que pueden ser el terreno: " + Lista(candidatas)
                        + ". Pasa 'terreno' con el nombre de la que quieres usar como base (ver listar_superficies).");
                e.Terreno = candidatas[0];
                e.TerrenoAutomatico = true;
            }

            // Datum y corredor
            string corPedido = string.IsNullOrWhiteSpace(corredorPedido) ? null : corredorPedido.Trim();
            if (corPedido != null)
            {
                string real = cors.FirstOrDefault(c => string.Equals(c.Trim(), corPedido, StringComparison.OrdinalIgnoreCase));
                if (real == null)
                    throw new ArgumentException("No existe el corredor '" + corPedido + "'. Corredores del dibujo: " + Lista(cors, "ninguno") + ". Usa listar_corredores.");
                corPedido = real;
            }
            if (!string.IsNullOrWhiteSpace(datumPedido))
            {
                var d = BuscarSuperficie(sup, supCor, datumPedido.Trim(), "datum");
                if (corPedido != null && !string.Equals(d.Corredor ?? "", corPedido, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("La superficie '" + d.Nombre + "' no es del corredor '" + corPedido + "' ("
                        + (string.IsNullOrWhiteSpace(d.Corredor) ? "no la genera ningún corredor" : "la genera el corredor '" + d.Corredor + "'")
                        + "). Quita 'corredor' o pasa en 'datum' la superficie Datum de ese corredor (ver estado_corredor y listar_superficies).");
                e.Datum = d.Nombre;
                e.Corredor = string.IsNullOrWhiteSpace(d.Corredor) ? null : d.Corredor;
                if (e.Corredor == null)
                    e.Avisos.Add("La superficie de comparación '" + d.Nombre + "' no la genera ningún corredor: se compara tal cual con el terreno.");
            }
            else
            {
                if (corPedido == null)
                {
                    if (cors.Count == 0)
                        throw new ArgumentException("No hay ningún corredor en el dibujo: pasa 'datum' con la superficie de comparación, o crea el corredor con crear_corredor y sus superficies con crear_superficies_corredor.");
                    if (cors.Count > 1)
                        throw new ArgumentException("Hay varios corredores en el dibujo: " + Lista(cors) + ". Pasa 'corredor' (o 'datum' con la superficie de comparación); usa listar_corredores.");
                    corPedido = cors[0];
                    e.CorredorAutomatico = true;
                }
                var delCorredor = supCor.Where(s => string.Equals((s.Corredor ?? "").Trim(), corPedido, StringComparison.OrdinalIgnoreCase)).ToList();
                string nombreDatum = corPedido + SufijoDatum;
                var datum = delCorredor.FirstOrDefault(s => string.Equals(s.Nombre.Trim(), nombreDatum, StringComparison.OrdinalIgnoreCase));
                if (datum == null)
                {
                    var conCodigo = delCorredor.Where(s => (s.CodigosEnlace ?? new List<string>()).Any(c => string.Equals((c ?? "").Trim(), CodigoDatum, StringComparison.OrdinalIgnoreCase))).ToList();
                    if (conCodigo.Count == 0)
                        throw new ArgumentException("El corredor '" + corPedido + "' no tiene superficie Datum: ninguna de sus superficies se llama '" + nombreDatum + "' ni usa el código de vínculo Datum. Superficies del corredor: "
                            + Lista(delCorredor.Select(s => s.Nombre)) + ". Créala con crear_superficies_corredor o pasa 'datum' (ver estado_corredor).");
                    if (conCodigo.Count > 1)
                        throw new ArgumentException("El corredor '" + corPedido + "' tiene varias superficies con el código de vínculo Datum: " + Lista(conCodigo.Select(s => s.Nombre))
                            + ". Pasa 'datum' con la que quieres usar (ver estado_corredor).");
                    datum = conCodigo[0];
                    e.Avisos.Add("El corredor '" + corPedido + "' no tiene una superficie llamada '" + nombreDatum + "': se usa '" + datum.Nombre + "', la única con el código de vínculo Datum.");
                }
                if (!datum.TieneSuperficie)
                    throw new ArgumentException(MensajeSinSuperficie(datum));
                e.Datum = datum.Nombre.Trim();
                e.Corredor = corPedido;
                e.DatumAutomatico = true;
            }

            if (string.Equals(e.Terreno.Trim(), e.Datum.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("La superficie base y la de comparación no pueden ser la misma ('" + e.Terreno + "'): pasa en 'terreno' el terreno natural y en 'datum' la superficie Datum del corredor.");

            // Nombre de la superficie de volumen
            if (string.IsNullOrWhiteSpace(nombrePedido))
            {
                e.Nombre = NombrePorDefecto(e.Corredor, e.Terreno, e.Datum);
                e.NombreAutomatico = true;
            }
            else e.Nombre = nombrePedido.Trim();
            return e;
        }

        /// <summary>Nombre por defecto: "&lt;corredor&gt; - Volumen"; si el Datum no es de un corredor, "Volumen &lt;terreno&gt; - &lt;datum&gt;".</summary>
        public static string NombrePorDefecto(string corredor, string terreno, string datum)
            => !string.IsNullOrWhiteSpace(corredor) ? corredor.Trim() + SufijoVolumen : "Volumen " + (terreno ?? "").Trim() + " - " + (datum ?? "").Trim();

        /// <summary>
        /// Lanza si el nombre de la superficie de volumen está vacío o ya lo usa una superficie del dibujo o de un
        /// corredor. 'ocupados': nombre → corredor que la genera (null si no es de un corredor).
        /// </summary>
        public static void ComprobarNombreLibre(string nombre, IDictionary<string, string> ocupados)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new ArgumentException("El nombre de la superficie de volumen no puede quedar vacío: pasa 'nombre' con texto o no lo pases para usar el nombre por defecto.");
            if (ocupados == null) return;
            foreach (var kv in ocupados)
            {
                if (kv.Key == null || !string.Equals(kv.Key.Trim(), nombre.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                throw new ArgumentException("Ya existe la superficie '" + nombre.Trim() + "'"
                    + (string.IsNullOrWhiteSpace(kv.Value) ? "" : " (la genera el corredor '" + kv.Value + "')")
                    + ". Usa listar_superficies o pasa otro 'nombre'.");
            }
        }

        private static SuperficieVolumen BuscarSuperficie(List<SuperficieVolumen> sup, List<SuperficieDeCorredorVolumen> supCor, string nombre, string parametro)
        {
            var s = sup.FirstOrDefault(x => string.Equals(x.Nombre.Trim(), nombre, StringComparison.OrdinalIgnoreCase));
            if (s == null)
            {
                var sinConstruir = supCor.FirstOrDefault(x => string.Equals(x.Nombre.Trim(), nombre, StringComparison.OrdinalIgnoreCase));
                if (sinConstruir != null && !sinConstruir.TieneSuperficie) throw new ArgumentException(MensajeSinSuperficie(sinConstruir));
                throw new ArgumentException("No existe la superficie '" + nombre + "' ('" + parametro + "'). Usa listar_superficies.");
            }
            if (EsVolumen(s.Tipo))
                throw new ArgumentException("'" + s.Nombre + "' es una superficie de volumen (" + s.Tipo + "): en '" + parametro + "' va "
                    + (parametro == "terreno" ? "el terreno natural" : "la superficie Datum del corredor") + ". Usa listar_superficies.");
            return s;
        }

        private static string MensajeSinSuperficie(SuperficieDeCorredorVolumen s)
            => "La superficie de corredor '" + s.Nombre + "' (corredor '" + s.Corredor + "') no tiene todavía su superficie en el dibujo: reconstruye el corredor con reconstruir_corredor, compruébalo con estado_corredor y listar_superficies, y repite.";

        public static bool EsVolumen(string tipo) => tipo != null && tipo.Trim().EndsWith("Volume", StringComparison.OrdinalIgnoreCase);

        private static bool EsTin(string tipo) => string.Equals((tipo ?? "").Trim(), "TIN", StringComparison.OrdinalIgnoreCase);

        // ------------------------------------------------------------------ textos
        /// <summary>Texto de la acción para la respuesta.</summary>
        public static string Accion(string nombre, string terreno, string datum, double esponjamiento, double compactacion)
            => "Crear la superficie de volumen TIN '" + nombre + "' con base '" + terreno + "' y comparación '" + datum + "', factor de esponjamiento (corte) "
               + Txt(esponjamiento) + " y de compactación (relleno) " + Txt(compactacion) + ", y reconstruirla";

        // ------------------------------------------------------------------ resumen de volúmenes
        /// <summary>
        /// Corte, relleno y neto sin ajustar y ajustados. Los ajustados son los de Civil 3D (AdjustedCutVolume y
        /// AdjustedFillVolume) si se leyeron; si no, los calcula con los factores. Avisa si los de Civil 3D no son
        /// corte × esponjamiento o relleno × compactación. El neto es siempre corte − relleno (convención del plugin);
        /// el signo del neto de Civil 3D se devuelve aparte. Lanza si no se pudo leer el corte o el relleno sin ajustar.
        /// </summary>
        public static ResumenVolumenes Resumir(VolumenesCivil v, double esponjamiento, double compactacion)
        {
            if (v == null || !Finito(v.CorteSinAjustar) || !Finito(v.RellenoSinAjustar))
                throw new InvalidOperationException("No se pudieron leer el corte y el relleno de la superficie de volumen (TinVolumeSurface.GetVolumeProperties: UnadjustedCutVolume y UnadjustedFillVolume).");
            var r = new ResumenVolumenes
            {
                CorteSinAjustar = v.CorteSinAjustar.Value,
                RellenoSinAjustar = v.RellenoSinAjustar.Value
            };
            r.NetoSinAjustar = r.CorteSinAjustar - r.RellenoSinAjustar;
            r.Corte = Ajustado(v.CorteAjustado, r.CorteSinAjustar, esponjamiento, "corte", "factor_esponjamiento", "AdjustedCutVolume", r.Avisos);
            r.Relleno = Ajustado(v.RellenoAjustado, r.RellenoSinAjustar, compactacion, "relleno", "factor_compactacion", "AdjustedFillVolume", r.Avisos);
            r.Neto = r.Corte - r.Relleno;
            r.Balance = Balance(r.Neto);
            r.Texto = TextoBalance(r.Corte, r.Relleno, r.Neto);
            r.ConvencionNetoCivil3d = Finito(v.NetoSinAjustar) ? ConvencionNeto(r.CorteSinAjustar, r.RellenoSinAjustar, v.NetoSinAjustar.Value) : null;
            if (r.ConvencionNetoCivil3d == null)
                r.Avisos.Add("No se pudo leer el neto de Civil 3D (UnadjustedNetVolume): el neto que se devuelve es el del plugin (corte − relleno).");
            return r;
        }

        private static double Ajustado(double? deCivil, double sinAjustar, double factor, string que, string parametro, string miembro, List<string> avisos)
        {
            double previsto = sinAjustar * factor;
            if (!Finito(deCivil))
            {
                avisos.Add("No se pudo leer el " + que + " ajustado de Civil 3D (" + miembro + "): se calcula como " + que + " × " + parametro + " = " + Txt(previsto) + ".");
                return previsto;
            }
            if (!Cerca(deCivil.Value, previsto))
                avisos.Add("El " + que + " ajustado de Civil 3D (" + miembro + " = " + Txt(deCivil.Value) + ") no es " + que + " sin ajustar × " + parametro + " ("
                    + Txt(sinAjustar) + " × " + Txt(factor) + " = " + Txt(previsto) + "). Se devuelve el de Civil 3D; anótalo en miembros_por_verificar_civil3d.md.");
            return deCivil.Value;
        }

        /// <summary>
        /// Signo del neto que da Civil 3D comparado con corte y relleno: "corte_menos_relleno", "relleno_menos_corte",
        /// "indeterminada" (corte y relleno iguales: no se distingue) u "otra" (no es ninguna de las dos diferencias).
        /// </summary>
        public static string ConvencionNeto(double corte, double relleno, double neto)
        {
            double diferencia = corte - relleno;
            if (Cerca(diferencia, 0)) return Cerca(neto, 0) ? "indeterminada" : "otra";
            if (Cerca(neto, diferencia)) return "corte_menos_relleno";
            if (Cerca(neto, -diferencia)) return "relleno_menos_corte";
            return "otra";
        }

        /// <summary>"sobra" si el neto es positivo, "falta" si es negativo, "compensado" si es 0 (±0,001).</summary>
        public static string Balance(double neto) => neto > ToleranciaVolumen ? "sobra" : neto < -ToleranciaVolumen ? "falta" : "compensado";

        /// <summary>Frase del balance en lenguaje sencillo.</summary>
        public static string TextoBalance(double corte, double relleno, double neto)
        {
            string detalle = " (corte ajustado " + Txt(corte) + " m³ y relleno ajustado " + Txt(relleno) + " m³)";
            switch (Balance(neto))
            {
                case "sobra": return "Sobran " + Txt(neto) + " m³ de material" + detalle + ": habrá que llevarlos a vertedero o a otra obra.";
                case "falta": return "Faltan " + Txt(-neto) + " m³ de material" + detalle + ": habrá que traerlos de préstamo.";
                default: return "Corte y relleno quedan compensados" + detalle + ".";
            }
        }

        // ------------------------------------------------------------------ secciones (tabla por progresivas)
        /// <summary>
        /// Áreas de corte (terreno por encima del Datum) y de relleno (Datum por encima del terreno) de una sección, en el
        /// tramo de desplazamientos en que existen las dos. Cada sección es una lista de puntos (X = desplazamiento,
        /// Y = cota) que se une con tramos rectos, en el orden dado (los escalones verticales, dos puntos con el mismo
        /// desplazamiento, no suman área). Si el terreno y el Datum se cruzan dentro de un tramo, el área se reparte en el
        /// cruce. Null si alguna sección tiene menos de dos puntos válidos o no se solapan.
        /// </summary>
        public static AreasSeccion Areas(IEnumerable<Punto2> terreno, IEnumerable<Punto2> datum)
        {
            var t = Tramos(terreno);
            var d = Tramos(datum);
            if (t.Count == 0 || d.Count == 0) return null;
            double desde = Math.Max(t[0].X0, d[0].X0), hasta = Math.Min(t[t.Count - 1].X1, d[d.Count - 1].X1);
            if (hasta - desde < 1e-9) return null;

            var cortes = new List<double> { desde, hasta };
            foreach (var s in t.Concat(d)) { cortes.Add(s.X0); cortes.Add(s.X1); }
            var xs = cortes.Where(x => x >= desde - 1e-12 && x <= hasta + 1e-12).OrderBy(x => x).ToList();
            var limpios = new List<double>();
            foreach (var x in xs) if (limpios.Count == 0 || x - limpios[limpios.Count - 1] > 1e-9) limpios.Add(x);

            double corte = 0, relleno = 0;
            for (int i = 0; i + 1 < limpios.Count; i++)
            {
                double a = limpios[i], b = limpios[i + 1];
                var st = TramoQueCubre(t, a, b);
                var sd = TramoQueCubre(d, a, b);
                if (st == null || sd == null) continue;   // hueco en una sección (no pasa con secciones continuas)
                double fa = st.Value.Cota(a) - sd.Value.Cota(a);
                double fb = st.Value.Cota(b) - sd.Value.Cota(b);
                Integrar(fa, fb, b - a, ref corte, ref relleno);
            }
            return new AreasSeccion { Corte = corte, Relleno = relleno, Desde = desde, Hasta = hasta };
        }

        /// <summary>Suma a corte la parte positiva y a relleno la negativa de una recta que va de fa a fb en un ancho dado.</summary>
        private static void Integrar(double fa, double fb, double ancho, ref double corte, ref double relleno)
        {
            if (fa >= 0 && fb >= 0) { corte += (fa + fb) / 2 * ancho; return; }
            if (fa <= 0 && fb <= 0) { relleno += -(fa + fb) / 2 * ancho; return; }
            double xr = fa / (fa - fb) * ancho;   // cruce dentro del tramo
            if (fa > 0) { corte += fa * xr / 2; relleno += -fb * (ancho - xr) / 2; }
            else { relleno += -fa * xr / 2; corte += fb * (ancho - xr) / 2; }
        }

        private struct Tramo
        {
            public double X0, Z0, X1, Z1;
            public double Cota(double x) => X1 - X0 < 1e-12 ? Z0 : Z0 + (Z1 - Z0) * (x - X0) / (X1 - X0);
        }

        /// <summary>Tramos de ancho positivo de una sección, de izquierda a derecha (orden estable por desplazamiento).</summary>
        private static List<Tramo> Tramos(IEnumerable<Punto2> puntos)
        {
            var p = (puntos ?? new List<Punto2>()).Where(q => !double.IsNaN(q.X) && !double.IsInfinity(q.X) && !double.IsNaN(q.Y) && !double.IsInfinity(q.Y))
                .Select((q, i) => (q, i)).OrderBy(x => x.q.X).ThenBy(x => x.i).Select(x => x.q).ToList();
            var l = new List<Tramo>();
            for (int i = 0; i + 1 < p.Count; i++)
                if (p[i + 1].X - p[i].X > 1e-9) l.Add(new Tramo { X0 = p[i].X, Z0 = p[i].Y, X1 = p[i + 1].X, Z1 = p[i + 1].Y });
            return l;
        }

        private static Tramo? TramoQueCubre(List<Tramo> tramos, double a, double b)
        {
            foreach (var s in tramos) if (s.X0 <= a + 1e-9 && s.X1 >= b - 1e-9) return s;
            return null;
        }

        /// <summary>Cota de una sección en un desplazamiento (interpolando entre puntos); null si queda fuera.</summary>
        public static double? CotaEn(IEnumerable<Punto2> puntos, double desplazamiento)
        {
            var t = Tramos(puntos);
            foreach (var s in t) if (desplazamiento >= s.X0 - 1e-9 && desplazamiento <= s.X1 + 1e-9) return s.Cota(desplazamiento);
            return null;
        }

        /// <summary>
        /// Compara los puntos de una sección con Section.LeftOffset/RightOffset (±0,01; |LeftOffset| y |RightOffset| por si
        /// el izquierdo viene con signo) y con su cota mínima y máxima (±0,01) cuando se leyeron. Devuelve el texto de la
        /// diferencia o null si cuadran o no hay con qué comparar. Desde 1.14.3 es solo diagnóstico (aviso), ya no bloquea
        /// la tabla: en la sección de una superficie de corredor LeftOffset/RightOffset dieron −3.9 a 3.6 con puntos de −30
        /// a 30 (informe 1.14.2 del 14/11/2026, paso 8), así que no son los extremos de los puntos.
        /// </summary>
        public static string MotivoPuntosNoValidos(IEnumerable<Punto2> puntos, double? izquierdo, double? derecho, double? cotaMinima, double? cotaMaxima, string seccion)
        {
            var p = (puntos ?? new List<Punto2>()).ToList();
            if (p.Count < 2) return "la sección de '" + seccion + "' tiene " + p.Count + " punto" + (p.Count == 1 ? "" : "s");
            const double tol = 0.01;
            if (Finito(izquierdo) && Finito(derecho))
            {
                double lim = Math.Max(Math.Abs(izquierdo.Value), Math.Abs(derecho.Value)) + tol;
                foreach (var q in p)
                    if (Math.Abs(q.X) > lim)
                        return "un punto de la sección de '" + seccion + "' tiene X = " + Txt(q.X) + ", fuera de los desplazamientos de la sección (" + Txt(izquierdo.Value) + " a " + Txt(derecho.Value) + ")";
            }
            if (Finito(cotaMinima) && Finito(cotaMaxima))
            {
                foreach (var q in p)
                    if (q.Y < cotaMinima.Value - tol || q.Y > cotaMaxima.Value + tol)
                        return "un punto de la sección de '" + seccion + "' tiene Y = " + Txt(q.Y) + ", fuera de sus cotas (" + Txt(cotaMinima.Value) + " a " + Txt(cotaMaxima.Value) + ")";
            }
            return null;
        }

        // ------------------------------------------------------------------ supuesto de ejes (1.14.3)
        /// <summary>Margen al comparar la cota de una sección con la de su superficie (unidades del dibujo).</summary>
        public const double ToleranciaCota = 0.01;
        /// <summary>Desplazamiento máximo a cada lado del eje en que se comparan las cotas (metros): con un 1 % de pendiente transversal da 0,05 de diferencia entre lados, de sobra frente a la tolerancia.</summary>
        public const double DesplazamientoComprobacion = 5;

        /// <summary>
        /// Comprueba que los puntos de una sección quedan dentro del ancho real de su línea de muestreo (desde 1.14.3; hasta
        /// la 1.14.2 se usaba Section.LeftOffset/RightOffset): X entre −anchoIzquierdo y +anchoDerecho, ±0,01. Los anchos
        /// son positivos (la izquierda es X negativa); un lado sin ancho cuenta como 0; sin ninguno de los dos, no se
        /// comprueba. Devuelve el motivo o null.
        /// </summary>
        public static string MotivoFueraDeLaLinea(IEnumerable<Punto2> puntos, double? anchoIzquierdo, double? anchoDerecho, string seccion)
        {
            if (!Finito(anchoIzquierdo) && !Finito(anchoDerecho)) return null;
            double izq = Finito(anchoIzquierdo) ? Math.Abs(anchoIzquierdo.Value) : 0, der = Finito(anchoDerecho) ? Math.Abs(anchoDerecho.Value) : 0;
            foreach (var q in puntos ?? new List<Punto2>())
                if (q.X < -izq - ToleranciaCota || q.X > der + ToleranciaCota)
                    return "un punto de la sección de '" + seccion + "' tiene X = " + Txt(q.X) + ", fuera del ancho de la línea de muestreo (" + Txt(izq) + " m a la izquierda y " + Txt(der) + " m a la derecha)";
            return null;
        }

        /// <summary>
        /// Desplazamientos en que se compara la cota de las secciones con la de las superficies: el eje (0) si está en el
        /// tramo común de las dos secciones, y uno a cada lado (DesplazamientoComprobacion o la mitad de lo que haya a ese
        /// lado, lo que sea menor), para comprobar también el signo izquierda/derecha.
        /// </summary>
        public static List<double> DesplazamientosDeComprobacion(double desde, double hasta)
        {
            var l = new List<double>();
            if (!Finito(desde) || !Finito(hasta) || hasta - desde < 1e-9) return l;
            if (desde <= 1e-9 && hasta >= -1e-9) l.Add(0);
            if (desde < -2 * ToleranciaCota) l.Add(-Math.Min(DesplazamientoComprobacion, -desde / 2));
            if (hasta > 2 * ToleranciaCota) l.Add(Math.Min(DesplazamientoComprobacion, hasta / 2));
            return l;
        }

        /// <summary>True si hay al menos una cota comparable (de la sección y de la superficie) del terreno y otra de la Datum.</summary>
        public static bool ComprobacionCompleta(IEnumerable<ComparacionCota> comparaciones, string terreno, string datum)
        {
            var c = (comparaciones ?? new List<ComparacionCota>()).Where(x => x != null && x.Comparable).ToList();
            return c.Any(x => string.Equals(x.Superficie, terreno, StringComparison.OrdinalIgnoreCase))
                && c.Any(x => string.Equals(x.Superficie, datum, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Null si en todas las comparaciones la cota de la sección y la de la superficie coinciden (±0,01); si no, el motivo
        /// (la primera que no coincide), para que la tabla salga sin números.
        /// </summary>
        public static string MotivoCotasNoCoinciden(IEnumerable<ComparacionCota> comparaciones, string linea, double progresiva)
        {
            foreach (var c in comparaciones ?? new List<ComparacionCota>())
            {
                if (c == null || !c.Comparable || Math.Abs(c.Diferencia.Value) <= ToleranciaCota) continue;
                string donde = Math.Abs(c.Desplazamiento) < 1e-9 ? "en el eje" : "a " + Txt(Math.Abs(c.Desplazamiento)) + " m a la " + (c.Desplazamiento < 0 ? "izquierda" : "derecha") + " del eje";
                return "en la línea '" + linea + "' (pk " + Txt(progresiva) + ") la sección de '" + c.Superficie + "' da cota " + Txt(c.CotaSeccion.Value) + " " + donde
                    + " y la superficie da " + Txt(c.CotaSuperficie.Value) + ": SectionPoint.Location no es (desplazamiento, cota)"
                    + (Math.Abs(c.Desplazamiento) < 1e-9 ? "" : " o el signo de los desplazamientos está cambiado");
            }
            return null;
        }

        /// <summary>
        /// Tabla por progresivas: ordena las líneas por progresiva y, entre cada par de líneas consecutivas con áreas,
        /// volumen = (área anterior + área actual) / 2 × distancia, de corte y de relleno; aplica los factores y acumula
        /// (la masa es corte ajustado acumulado − relleno ajustado acumulado). Un tramo con una línea sin áreas no suma
        /// volumen y se avisa.
        /// </summary>
        public static TablaVolumenes Tabla(IEnumerable<SeccionLinea> lineas, double esponjamiento, double compactacion)
        {
            var l = (lineas ?? new List<SeccionLinea>()).Where(x => x != null && Finito(x.Progresiva)).OrderBy(x => x.Progresiva).ToList();
            var tabla = new TablaVolumenes();
            double accCorte = 0, accRelleno = 0;
            var sinVolumen = new List<string>();
            for (int i = 0; i < l.Count; i++)
            {
                var x = l[i];
                var f = new FilaVolumen
                {
                    Linea = x.Nombre,
                    Progresiva = x.Progresiva,
                    AreaCorte = x.Areas?.Corte,
                    AreaRelleno = x.Areas?.Relleno,
                    Falta = x.Areas == null ? (x.Falta ?? "sin áreas") : null
                };
                if (i > 0)
                {
                    tabla.Tramos++;
                    var ant = l[i - 1];
                    if (ant.Areas != null && x.Areas != null)
                    {
                        double dl = x.Progresiva - ant.Progresiva;
                        f.VolumenCorte = (ant.Areas.Corte + x.Areas.Corte) / 2 * dl;
                        f.VolumenRelleno = (ant.Areas.Relleno + x.Areas.Relleno) / 2 * dl;
                        f.VolumenCorteAjustado = f.VolumenCorte * esponjamiento;
                        f.VolumenRellenoAjustado = f.VolumenRelleno * compactacion;
                        tabla.Corte += f.VolumenCorte.Value;
                        tabla.Relleno += f.VolumenRelleno.Value;
                        accCorte += f.VolumenCorteAjustado.Value;
                        accRelleno += f.VolumenRellenoAjustado.Value;
                    }
                    else
                    {
                        tabla.TramosSinVolumen++;
                        sinVolumen.Add(Txt(ant.Progresiva) + " a " + Txt(x.Progresiva));
                    }
                }
                f.CorteAcumulado = accCorte;
                f.RellenoAcumulado = accRelleno;
                f.Masa = accCorte - accRelleno;
                tabla.Filas.Add(f);
            }
            tabla.CorteAjustado = accCorte;
            tabla.RellenoAjustado = accRelleno;
            tabla.Neto = accCorte - accRelleno;
            if (l.Count < 2) tabla.Avisos.Add("La tabla por progresivas necesita al menos dos líneas de muestreo; hay " + l.Count + ".");
            if (sinVolumen.Count > 0)
                tabla.Avisos.Add(sinVolumen.Count + " de " + tabla.Tramos + " tramos no suman volumen porque una de sus líneas no tiene las dos secciones (de pk "
                    + string.Join("; ", sinVolumen.Take(5)) + (sinVolumen.Count > 5 ? "; …" : "") + "). Revisa las fuentes del grupo con listar_lineas_muestreo (fuentes=true).");
            return tabla;
        }

        /// <summary>Diferencia en % de la tabla frente a la superficie de volumen (null si la superficie da 0).</summary>
        public static double? DiferenciaPorcentaje(double tabla, double superficie)
            => Math.Abs(superficie) < ToleranciaVolumen ? (double?)null : (tabla - superficie) / superficie * 100;

        // ------------------------------------------------------------------ utilidades
        private static bool Finito(double? v) => v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value);

        private static bool Finito(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>Igualdad de volúmenes: 0,001 de margen absoluto o una millonésima del valor, lo que sea mayor.</summary>
        private static bool Cerca(double a, double b) => Math.Abs(a - b) <= Math.Max(ToleranciaVolumen, 1e-6 * Math.Max(Math.Abs(a), Math.Abs(b)));

        private static string Lista(IEnumerable<string> l, string vacia = "ninguna")
        {
            var x = (l ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            return x.Count == 0 ? vacia : string.Join(", ", x);
        }

        /// <summary>Número con hasta 4 decimales y punto decimal (como el resto de mensajes del plugin), para los textos.</summary>
        private static string Txt(double v) => Math.Round(v, 4).ToString("0.####", Inv);
    }
}

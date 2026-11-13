using System;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.Settings;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// Líneas de muestreo y vistas de sección. Desde 1.13.0: crear_secciones, la sexta herramienta de creación del flujo
    /// vial (tras crear_superficie_tin, crear_alineamiento_desde_polilinea, crear_perfil_terreno_y_vista, crear_rasante
    /// y crear_corredor), por el camino común Escritura.CrearObjeto. En UNA transacción (una entrada de Deshacer):
    /// SampleLineGroup.Create(nombre, eje); GetSectionSources() con el grupo abierto para escritura e IsSampled en las
    /// fuentes elegidas (el terreno y, si existen, los corredores del eje y sus superficies); una línea por progresiva
    /// con SampleLine.Create(nombre, grupo, Point2dCollection) y sus dos extremos calculados con Alignment.PointLocation
    /// (pk, −ancho izquierdo) y (pk, +ancho derecho); y, si se piden, una vista por línea con SectionView.Create(nombre,
    /// línea, punto) colocada en cuadrícula de N filas por columna leyendo la extensión real de la anterior
    /// (ArbaMcp.Nucleo.CuadriculaVistas) y con el estilo pedido (SectionView.StyleId). Si algo lanza, la transacción se
    /// descarta y no queda nada. Alternativa no usada: SectionViewGroupCollection.Add, que recibe estilo y bandas pero
    /// coloca las vistas con el estilo de grupo del dibujo (no deja fijar las filas por columna). Lo que no depende de
    /// Civil 3D (progresivas, nombres, fuentes, cuadrícula, comprobaciones) está en ArbaMcp.Nucleo.Secciones.
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaCrearSecciones = "crear_secciones";

        // ------------------------------------------------------------------ búsquedas
        /// <summary>Id del grupo de líneas de muestreo con ese nombre en el alineamiento o ObjectId.Null si no existe.</summary>
        private static ObjectId BuscarGrupoMuestreoONull(Transaction tr, CivAlignment al, string nombre)
        {
            foreach (ObjectId id in al.GetSampleLineGroupIds())
                if (tr.GetObject(id, OpenMode.ForRead) is Civ.SampleLineGroup g && string.Equals(g.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return id;
            return ObjectId.Null;
        }

        private static ObjectId BuscarEstiloVistaSeccion(Transaction tr, string nombre)
        {
            var estilos = CivilApplication.ActiveDocument.Styles.SectionViewStyles;
            return BuscarEnEstilos(tr, estilos, estilos.Contains, n => estilos[n], nombre, "el estilo de vista de sección", "Espacio de herramientas → Configuración → Visualización de sección → Estilos de visualización de sección");
        }

        /// <summary>
        /// Estilo de vista de sección por defecto del dibujo (SettingsSectionView.Styles.SectionViewStyleId), solo para
        /// decirlo en la respuesta: sin 'estilo_vista' no se toca el estilo de las vistas (lo pone Civil 3D). Null si no
        /// se puede leer.
        /// </summary>
        private static string NombreEstiloVistaSeccionPorDefecto(Transaction tr)
        {
            try
            {
                ObjectId id = CivilApplication.ActiveDocument.Settings.GetSettings<SettingsSectionView>().Styles.SectionViewStyleId.Value;
                if (!id.IsNull) return NombreEstilo(tr, id);
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearSecciones + ": no se pudo leer el estilo de vista de sección por defecto (SettingsSectionView.Styles): " + ex.Message); }
            return null;
        }

        private static bool CorredorUsaEje(Civ.Corridor cor, ObjectId idAl)
        {
            try
            {
                foreach (Civ.Baseline bl in cor.Baselines)
                    if (bl.AlignmentId == idAl) return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Fuentes que se prevé que ofrezca el grupo nuevo (para la simulación, en la que el grupo aún no existe): todas
        /// las superficies del dibujo (las de corredor marcadas como superficie_corredor) y los corredores con una línea
        /// base en este eje. La lista real la da GetSectionSources al crear el grupo.
        /// </summary>
        private static List<FuenteSeccion> FuentesPrevistas(Transaction tr, ObjectId idAl)
        {
            var deCorredorPorId = SuperficiesDeCorredores(tr, out var deCorredorPorNombre);
            var lista = new List<FuenteSeccion>();
            foreach (ObjectId idS in CivilApplication.ActiveDocument.GetSurfaceIds())
            {
                if (!(tr.GetObject(idS, OpenMode.ForRead) is CivSurface su)) continue;
                bool deCorredor = deCorredorPorId.ContainsKey(idS) || deCorredorPorNombre.ContainsKey(su.Name);
                lista.Add(new FuenteSeccion(su.Name, deCorredor ? Secciones.ClaseSuperficieCorredor : Secciones.ClaseSuperficie));
            }
            foreach (ObjectId idC in CivilApplication.ActiveDocument.CorridorCollection)
                if (tr.GetObject(idC, OpenMode.ForRead) is Civ.Corridor cor && CorredorUsaEje(cor, idAl))
                    lista.Add(new FuenteSeccion(cor.Name, Secciones.ClaseCorredor));
            return lista;
        }

        /// <summary>
        /// Describe una fuente real del grupo (SectionSource): el nombre del objeto de SourceId (o SourceName) y su clase
        /// según el tipo de ese objeto (el catálogo no da los valores de SectionSourceType; su texto se guarda en 'tipo').
        /// </summary>
        private static FuenteSeccion DescribirFuente(Transaction tr, Civ.SectionSource f, Dictionary<ObjectId, string> deCorredorPorId, Dictionary<string, string> deCorredorPorNombre)
        {
            ObjectId idF = Seguro(() => f.SourceId, ObjectId.Null);
            string nombre = null, clase = Secciones.ClaseOtra;
            try
            {
                var o = idF.IsNull ? null : tr.GetObject(idF, OpenMode.ForRead);
                if (o is CivSurface su)
                {
                    nombre = su.Name;
                    clase = deCorredorPorId.ContainsKey(idF) || deCorredorPorNombre.ContainsKey(su.Name) ? Secciones.ClaseSuperficieCorredor : Secciones.ClaseSuperficie;
                }
                else if (o is Civ.Corridor cor) { nombre = cor.Name; clase = Secciones.ClaseCorredor; }
                else if (o != null) nombre = NombreDe(tr, idF);
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearSecciones + ": no se pudo abrir el objeto de una fuente de sección: " + ex.Message); }
            if (string.IsNullOrWhiteSpace(nombre)) nombre = Seguro(() => f.SourceName, null);
            return new FuenteSeccion(nombre, clase, Seguro(() => f.SourceType.ToString(), null));
        }

        /// <summary>Anchos izquierdo y derecho de una línea releída: el mayor desplazamiento de sus vértices a cada lado del eje (Alignment.StationOffset).</summary>
        private static (double? izquierdo, double? derecho) AnchosLinea(CivAlignment al, Civ.SampleLine sl)
        {
            double? izq = null, der = null;
            try
            {
                foreach (Civ.SampleLineVertex v in sl.Vertices)
                {
                    var p = v.Location;
                    double pk = 0, off = 0;
                    al.StationOffset(p.X, p.Y, ref pk, ref off);
                    if (off < 0) izq = Math.Max(izq ?? 0, -off);
                    else if (off > 0) der = Math.Max(der ?? 0, off);
                }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearSecciones + ": no se pudieron leer los vértices de la línea " + Seguro(() => sl.Name, "?") + ": " + ex.Message); return (null, null); }
            return (izq, der);
        }

        private static Dictionary<string, object> PuntoJson(Punto2 p) => new Dictionary<string, object> { ["x"] = N(p.X), ["y"] = N(p.Y) };

        private static void AvisoUnico(List<string> avisos, string texto)
        {
            if (!string.IsNullOrEmpty(texto) && !avisos.Contains(texto)) avisos.Add(texto);
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarSecciones()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaCrearSecciones,
                Descripcion = "Crea un grupo de líneas de muestreo en un alineamiento con una línea cada 'intervalo' metros (y otra en el final del tramo) con los anchos izquierdo y derecho pedidos, marca como muestreadas las fuentes (por defecto todas las superficies, los corredores del eje y sus superficies; o las de 'fuentes') y, si crear_vistas=true (por defecto), crea una vista de sección por línea en cuadrícula de 'filas_por_columna' filas (por defecto 5): columnas de izquierda a derecha, en cada columna la progresiva crece de abajo arriba, desde 'punto_insercion' o, si falta, a la derecha de lo dibujado. Todo en una sola transacción y una sola entrada de Deshacer. Vía: SampleLineGroup.Create, GetSectionSources/IsSampled, SampleLine.Create con dos puntos (Alignment.PointLocation a −ancho_izquierdo y +ancho_derecho) y SectionView.Create con el estilo pedido (StyleId), cada vista encima de la anterior según su extensión real. Relee el grupo y comprueba el número de líneas y de vistas, cada progresiva (±0,01) y las fuentes muestreadas (si no, error sin reintentar). Devuelve las líneas (progresiva, anchos leídos, secciones), las fuentes, las vistas (fila, columna, punto) y los solapes. Con simular=true solo comprueba e informa (sin copia ni cambios). Si ya existe un grupo con ese nombre en el eje, error sin tocar nada.",
                Parametros =
                {
                    P("alineamiento", "string", "Nombre del alineamiento (eje); ver listar_alineamientos", true),
                    P("intervalo", "number", "Metros entre líneas de muestreo (mayor que 0); se añade una línea en el final del tramo si no cae en el intervalo", true),
                    P("ancho_izquierdo", "number", "Metros de franja a la izquierda del eje (mayor que 0)", true),
                    P("ancho_derecho", "number", "Metros de franja a la derecha del eje (mayor que 0)", true),
                    P("nombre", "string", "Nombre del grupo de líneas de muestreo (no debe existir en el eje; ver listar_lineas_muestreo). Por defecto '<alineamiento> - Muestreo'"),
                    P("inicio", "number", "Progresiva de la primera línea. Si falta, el inicio del eje (hasta 0,01 fuera se ajusta con aviso)"),
                    P("fin", "number", "Progresiva de la última línea. Si falta, el final del eje (hasta 0,01 fuera se ajusta con aviso)"),
                    P("fuentes", "json", "Nombres de las fuentes que se muestrean: [\"Terreno\", \"Corredor 1\"] (o \"Terreno; Corredor 1\"). Si falta: todas las superficies, los corredores del eje y sus superficies (error si no hay ninguna superficie de terreno)"),
                    P("crear_vistas", "boolean", "Con false solo crea el grupo y las líneas, sin vistas de sección (por defecto true)"),
                    P("filas_por_columna", "number", "Vistas de sección por columna de la cuadrícula (entero ≥ 1, por defecto " + Secciones.FilasPorDefecto + ")"),
                    P("estilo_vista", "string", "Estilo de vista de sección del dibujo. Si falta, el que pone Civil 3D (el de la configuración del dibujo); si no existe, error con la lista"),
                    P("punto_insercion", "json", "Esquina inferior izquierda de la primera vista: {\"x\": 2000, \"y\": 1900}. Si falta, a la derecha de lo dibujado (como crear_perfil_terreno_y_vista) y la respuesta devuelve el punto usado"),
                    P("simular", "boolean", "Con true comprueba eje, nombre libre, progresivas, anchos, fuentes, estilo y punto y devuelve lo previsto sin tocar el dibujo (ni copia)")
                },
                Ejecutar = a => Escritura.Ejecutar(HerramientaCrearSecciones, a, ctx =>
                {
                    string nombreAl = Requerido(a, "alineamiento").Trim();
                    double intervalo = Secciones.ValidarIntervalo(Tiene(a, "intervalo") ? Num(a, "intervalo", double.NaN) : (double?)null);
                    double anchoIzq = Secciones.ValidarAncho(Tiene(a, "ancho_izquierdo") ? Num(a, "ancho_izquierdo", double.NaN) : (double?)null, "ancho_izquierdo");
                    double anchoDer = Secciones.ValidarAncho(Tiene(a, "ancho_derecho") ? Num(a, "ancho_derecho", double.NaN) : (double?)null, "ancho_derecho");
                    double? inicioPedido = Tiene(a, "inicio") ? Num(a, "inicio", double.NaN) : (double?)null;
                    double? finPedido = Tiene(a, "fin") ? Num(a, "fin", double.NaN) : (double?)null;
                    bool conVistas = Bool(a, "crear_vistas", true);
                    int filas = Secciones.ValidarFilas(Tiene(a, "filas_por_columna") ? Num(a, "filas_por_columna", double.NaN) : (double?)null);
                    string estiloVista = Str(a, "estilo_vista");
                    bool estiloPedido = conVistas && !string.IsNullOrWhiteSpace(estiloVista);
                    Punto2? puntoPedido = PerfilVista.LeerPuntoInsercion(JsonArg(a, "punto_insercion"));
                    var fuentesPedidas = Secciones.LeerFuentes(a);
                    double separacion = Secciones.Separacion(anchoIzq, anchoDer);
                    var db = ctx.Db;

                    ObjectId idAl = ObjectId.Null, idEstilo = ObjectId.Null;
                    string nombreAlReal = nombreAl, nombreGrupo = null, estiloUsado = null;
                    RangoMuestreo rango = null;
                    List<string> nombresLineas = null;
                    List<FuenteSeccion> fuentesElegidas = null;
                    Punto2 punto = default;
                    bool puntoAutomatico = conVistas && !puntoPedido.HasValue;

                    // Lo que deja la creación
                    List<FuenteSeccion> fuentesReales = null;
                    var vistasCreadas = new List<Dictionary<string, object>>();
                    int extensionesLeidas = 0;

                    // Lo releído
                    var progresivasLeidas = new List<double>();
                    var lineasInfo = new List<object>();
                    var vistasInfo = new List<object>();
                    var extensionesVistas = new List<Extension2>();
                    var fuentesInfo = new List<object>();
                    var muestreadasLeidas = new List<string>();
                    var avisosAnchos = new List<string>();
                    var estilosLeidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int lineasSinSecciones = 0;
                    object gruposVistas = null;

                    Dictionary<string, object> EstadoPrevisto() => new Dictionary<string, object>
                    {
                        ["nombre"] = nombreGrupo,
                        ["alineamiento"] = nombreAlReal,
                        ["existe"] = true,
                        ["numero_lineas"] = rango.Numero,
                        ["progresiva_primera"] = N(rango.Progresivas[0]),
                        ["progresiva_ultima"] = N(rango.Progresivas[rango.Numero - 1]),
                        ["progresivas"] = rango.Progresivas.Select(p => N(p)).ToList(),
                        ["intervalo"] = N(intervalo),
                        ["ancho_izquierdo"] = N(anchoIzq),
                        ["ancho_derecho"] = N(anchoDer),
                        ["fuentes_muestreadas"] = fuentesElegidas.Select(f => f.Nombre).ToList(),
                        ["numero_vistas"] = conVistas ? rango.Numero : 0,
                        ["filas_por_columna"] = conVistas ? filas : (int?)null,
                        ["columnas"] = conVistas ? Secciones.Columnas(rango.Numero, filas) : 0,
                        ["estilo_vista"] = conVistas ? estiloUsado : null,
                        ["punto_insercion"] = conVistas ? PuntoJson(punto) : null
                    };

                    // Esperado: el grupo existe con una línea por progresiva y, si se piden, una vista por línea (el número se
                    // completa en Antes; las fuentes se comprueban en Verificar porque la lista real solo se conoce al crear)
                    var esperado = new Dictionary<string, object> { ["existe"] = true };

                    Escritura.Creacion creacion = null;
                    creacion = new Escritura.Creacion
                    {
                        // El texto definitivo (líneas, fuentes y vistas) se completa en Antes
                        Accion = "Crear líneas de muestreo y vistas de sección en el eje '" + nombreAl + "'",
                        Antes = tr =>
                        {
                            idAl = BuscarAlineamiento(tr, nombreAl);               // "No existe el alineamiento 'X'. Usa listar_alineamientos."
                            var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                            nombreAlReal = al.Name;
                            nombreGrupo = Secciones.NombreGrupo(Str(a, "nombre"), al.Name);
                            if (!BuscarGrupoMuestreoONull(tr, al, nombreGrupo).IsNull)
                                throw new ArgumentException("Ya existe el grupo de líneas de muestreo '" + nombreGrupo + "' en el alineamiento '" + al.Name + "'. Usa listar_lineas_muestreo o pasa otro 'nombre'.");

                            rango = Secciones.Progresivas(al.StartingStation, al.EndingStation, inicioPedido, finPedido, intervalo, al.Name);
                            ctx.Avisos.AddRange(rango.Avisos);
                            nombresLineas = Secciones.NombresLineas(rango.Progresivas);
                            esperado["numero_lineas"] = rango.Numero;
                            esperado["numero_vistas"] = conVistas ? rango.Numero : 0;

                            // Fuentes: las mismas reglas que en la escritura real, sobre las previstas
                            fuentesElegidas = Secciones.ElegirFuentes(FuentesPrevistas(tr, idAl), fuentesPedidas, ctx.Avisos);

                            if (conVistas)
                            {
                                if (estiloPedido)
                                {
                                    idEstilo = BuscarEstiloVistaSeccion(tr, estiloVista.Trim());   // error con la lista si no existe
                                    estiloUsado = NombreEstilo(tr, idEstilo) ?? estiloVista.Trim();
                                }
                                else
                                {
                                    estiloUsado = NombreEstiloVistaSeccionPorDefecto(tr);
                                    if (estiloUsado == null)
                                        ctx.Avisos.Add("No se pudo leer el estilo de vista de sección por defecto del dibujo: las vistas llevan el que ponga Civil 3D. Pasa 'estilo_vista' para elegir uno.");
                                }
                                if (puntoPedido.HasValue) punto = puntoPedido.Value;
                                else
                                {
                                    var total = Extension2.Unir(ExtensionDe(al), ExtensionDibujo(db));
                                    punto = PerfilVista.PuntoAutomatico(total);
                                    ctx.Avisos.Add(PerfilVista.ExplicacionPuntoAutomatico(total.Value, punto));
                                }
                            }
                            else if (!string.IsNullOrWhiteSpace(estiloVista) || puntoPedido.HasValue || Tiene(a, "filas_por_columna"))
                                ctx.Avisos.Add("Con crear_vistas=false no se crean vistas de sección: 'estilo_vista', 'punto_insercion' y 'filas_por_columna' no se usan.");

                            creacion.Accion = Secciones.Accion(nombreGrupo, al.Name, rango, intervalo, anchoIzq, anchoDer, fuentesElegidas.Select(f => f.Nombre), conVistas, filas, conVistas ? punto : (Punto2?)null);
                            return new Dictionary<string, object> { ["nombre"] = nombreGrupo, ["alineamiento"] = al.Name, ["existe"] = false, ["numero_lineas"] = 0, ["numero_vistas"] = 0 };
                        },
                        Prevision = tr =>
                        {
                            if (ctx.Simular)
                            {
                                ctx.Avisos.Add("'fuentes_muestreadas' es la lista prevista (superficies del dibujo y corredores del eje): la real la da Civil 3D al crear el grupo y va en la respuesta de la escritura.");
                                if (conVistas)
                                    ctx.Avisos.Add("La posición de cada vista se calcula al crearla, con el tamaño real de la anterior (separación " + N(separacion) + "): la simulación solo da el punto de la primera.");
                            }
                            return EstadoPrevisto();
                        },
                        Esperado = esperado,
                        Crear = tr =>
                        {
                            var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                            ObjectId idGrupo = Civ.SampleLineGroup.Create(nombreGrupo, idAl);
                            if (idGrupo.IsNull) return idGrupo;
                            var slg = (Civ.SampleLineGroup)tr.GetObject(idGrupo, OpenMode.ForWrite);

                            // 1) Fuentes (con el grupo abierto para escritura, lo exige GetSectionSources)
                            var deCorredorPorId = SuperficiesDeCorredores(tr, out var deCorredorPorNombre);
                            var objetos = new List<Civ.SectionSource>();
                            fuentesReales = new List<FuenteSeccion>();
                            foreach (Civ.SectionSource f in slg.GetSectionSources())
                            {
                                objetos.Add(f);
                                fuentesReales.Add(DescribirFuente(tr, f, deCorredorPorId, deCorredorPorNombre));
                            }
                            var avisosFuentes = new List<string>();
                            Secciones.ElegirFuentes(fuentesReales, fuentesPedidas, avisosFuentes);   // error si falta una pedida o no hay terreno: la transacción se descarta
                            foreach (string t in avisosFuentes) AvisoUnico(ctx.Avisos, t);
                            for (int i = 0; i < objetos.Count; i++)
                            {
                                try { objetos[i].IsSampled = fuentesReales[i].Muestrear; }
                                catch (Exception ex) { throw new InvalidOperationException("No se pudo marcar la fuente '" + fuentesReales[i].Nombre + "' (SectionSource.IsSampled: " + ex.Message + "). Nada queda escrito (la transacción se descarta) y no se reintenta.", ex); }
                            }

                            // 2) Líneas: dos puntos por progresiva, a −ancho_izquierdo y +ancho_derecho del eje
                            var idsLineas = new List<ObjectId>();
                            for (int i = 0; i < rango.Numero; i++)
                            {
                                double pk = rango.Progresivas[i];
                                try
                                {
                                    double xi = 0, yi = 0, xd = 0, yd = 0;
                                    al.PointLocation(pk, -anchoIzq, ref xi, ref yi);
                                    al.PointLocation(pk, anchoDer, ref xd, ref yd);
                                    var puntos = new Point2dCollection();
                                    puntos.Add(new Point2d(xi, yi));
                                    puntos.Add(new Point2d(xd, yd));
                                    ObjectId idL = Civ.SampleLine.Create(nombresLineas[i], idGrupo, puntos);
                                    if (idL.IsNull) throw new InvalidOperationException("SampleLine.Create no devolvió la línea");
                                    idsLineas.Add(idL);
                                }
                                catch (Exception ex)
                                {
                                    throw new InvalidOperationException("No se pudo crear la línea de muestreo '" + nombresLineas[i] + "' (progresiva " + N(pk) + "): " + ex.Message + ". La transacción se descarta: no queda ni el grupo ni ninguna línea. No se reintenta.", ex);
                                }
                            }

                            // 3) Vistas en cuadrícula: cada una encima de la anterior según su extensión real
                            if (conVistas)
                            {
                                var cuadricula = new CuadriculaVistas(punto, filas, separacion);
                                for (int i = 0; i < idsLineas.Count; i++)
                                {
                                    var lugar = cuadricula.Siguiente();
                                    string nombreVista = Secciones.NombreVista(nombreGrupo, nombresLineas[i]);
                                    Civ.SectionView sv;
                                    try
                                    {
                                        ObjectId idV = Civ.SectionView.Create(nombreVista, idsLineas[i], new Point3d(lugar.X, lugar.Y, 0));
                                        if (idV.IsNull) throw new InvalidOperationException("SectionView.Create no devolvió la vista");
                                        sv = (Civ.SectionView)tr.GetObject(idV, OpenMode.ForWrite);
                                        if (!idEstilo.IsNull) sv.StyleId = idEstilo;
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new InvalidOperationException("No se pudo crear la vista de sección '" + nombreVista + "' en (" + N(lugar.X) + ", " + N(lugar.Y) + "): " + ex.Message + ". La transacción se descarta: no quedan ni las líneas ni las vistas. No se reintenta; prueba con crear_vistas=false u otro 'estilo_vista'.", ex);
                                    }
                                    cuadricula.Registrar(lugar, ExtensionDe(sv), anchoIzq + anchoDer + 2 * separacion, (anchoIzq + anchoDer) / 2);
                                    var (fila, columna) = CuadriculaVistas.FilaColumna(i, filas);
                                    vistasCreadas.Add(new Dictionary<string, object> { ["nombre"] = nombreVista, ["fila"] = fila, ["columna"] = columna, ["x"] = N(lugar.X), ["y"] = N(lugar.Y) });
                                }
                                extensionesLeidas = cuadricula.ExtensionesLeidas;
                            }
                            return idGrupo;
                        },
                        Despues = (tr, idGrupo) =>
                        {
                            var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                            // GetSectionSources exige el grupo abierto para escritura (como listar_lineas_muestreo); estamos en el mismo contexto de comando
                            var slg = (Civ.SampleLineGroup)tr.GetObject(idGrupo, OpenMode.ForWrite);
                            string nombreLeido = Seguro(() => slg.Name, null);

                            // Líneas, en orden de progresiva
                            var lineas = new List<(double pk, Civ.SampleLine sl)>();
                            foreach (ObjectId idL in slg.GetSampleLineIds())
                                if (tr.GetObject(idL, OpenMode.ForRead) is Civ.SampleLine sl)
                                    lineas.Add((Seguro(() => sl.Station, double.NaN), sl));
                            lineas = lineas.OrderBy(x => x.pk).ToList();
                            int numeroVistas = 0;
                            Punto2? primerPunto = null;
                            foreach (var (pk, sl) in lineas)
                            {
                                progresivasLeidas.Add(pk);
                                string nombreLinea = Seguro(() => sl.Name, null);
                                var (izq, der) = AnchosLinea(al, sl);
                                if (avisosAnchos.Count < 3)
                                {
                                    string av = Secciones.AvisoAncho(nombreLinea, "izquierda", anchoIzq, izq) ?? Secciones.AvisoAncho(nombreLinea, "derecha", anchoDer, der);
                                    if (av != null) avisosAnchos.Add(av);
                                }
                                var secciones = new List<string>();
                                try
                                {
                                    foreach (ObjectId idS in sl.GetSectionIds())
                                        if (tr.GetObject(idS, OpenMode.ForRead) is Civ.Section s)
                                            secciones.Add(Seguro(() => s.SourceName, null) ?? NombreDe(tr, Seguro(() => s.SourceId, ObjectId.Null)));
                                }
                                catch (Exception ex) { Historial.Registrar(HerramientaCrearSecciones + ": no se pudieron leer las secciones de " + nombreLinea + ": " + ex.Message); }
                                if (secciones.Count == 0) lineasSinSecciones++;
                                try
                                {
                                    foreach (ObjectId idV in sl.GetSectionViewIds())
                                    {
                                        if (!(tr.GetObject(idV, OpenMode.ForRead) is Civ.SectionView sv)) continue;
                                        numeroVistas++;
                                        var ext = ExtensionDe(sv);
                                        if (ext.HasValue && ext.Value.EsValida) extensionesVistas.Add(ext.Value);
                                        Point3d loc = Seguro(() => sv.Location, new Point3d(double.NaN, double.NaN, 0));
                                        if (!primerPunto.HasValue && !double.IsNaN(loc.X)) primerPunto = new Punto2(loc.X, loc.Y);
                                        string estilo = Seguro(() => NombreDeEstilo(tr, ((Civ.Entity)sv).StyleId), null) ?? Seguro(() => ((Civ.Entity)sv).StyleName, null);
                                        if (estilo != null) estilosLeidos.Add(estilo);
                                        vistasInfo.Add(new
                                        {
                                            nombre = Seguro(() => sv.Name, null),
                                            linea = nombreLinea,
                                            progresiva = N(pk),
                                            x = N(loc.X),
                                            y = N(loc.Y),
                                            ancho = ext.HasValue ? N(ext.Value.Ancho) : null,
                                            alto = ext.HasValue ? N(ext.Value.Alto) : null,
                                            estilo
                                        });
                                    }
                                }
                                catch (Exception ex) { Historial.Registrar(HerramientaCrearSecciones + ": no se pudieron leer las vistas de " + nombreLinea + ": " + ex.Message); }
                                lineasInfo.Add(new { numero = Seguro(() => (int?)sl.Number, null), nombre = nombreLinea, progresiva = N(pk), ancho_izquierdo = izq.HasValue ? N(izq.Value) : null, ancho_derecho = der.HasValue ? N(der.Value) : null, secciones });
                            }
                            if (primerPunto.HasValue) punto = primerPunto.Value;

                            // Fuentes
                            var deCorredorPorId = SuperficiesDeCorredores(tr, out var deCorredorPorNombre);
                            foreach (Civ.SectionSource f in slg.GetSectionSources())
                            {
                                var d = DescribirFuente(tr, f, deCorredorPorId, deCorredorPorNombre);
                                bool muestreada = Seguro(() => f.IsSampled, false);
                                if (muestreada) muestreadasLeidas.Add(d.Nombre);
                                fuentesInfo.Add(new { nombre = d.Nombre, clase = d.Clase, tipo = d.Tipo, muestreada });
                            }

                            // Grupos de vistas que dejó SectionView.Create (para la memoria técnica)
                            try
                            {
                                var grupos = new List<object>();
                                foreach (Civ.SectionViewGroup g in slg.SectionViewGroups)
                                    grupos.Add(new { nombre = Seguro(() => g.Name, null), vistas = Seguro(() => (int?)g.GetSectionViewIds().Count, null) });
                                gruposVistas = new { numero = grupos.Count, grupos, individuales = Seguro(() => (int?)slg.IndividualSectionViewGroup.GetSectionViewIds().Count, null) };
                            }
                            catch (Exception ex) { gruposVistas = new { error = ex.Message }; }

                            var d0 = EstadoPrevisto();
                            d0["nombre"] = nombreLeido;
                            d0["existe"] = !BuscarGrupoMuestreoONull(tr, al, nombreGrupo).IsNull;
                            d0["numero_lineas"] = lineas.Count;
                            d0["progresiva_primera"] = lineas.Count > 0 ? N(lineas[0].pk) : null;
                            d0["progresiva_ultima"] = lineas.Count > 0 ? N(lineas[lineas.Count - 1].pk) : null;
                            d0["progresivas"] = progresivasLeidas.Select(p => N(p)).ToList();
                            d0["fuentes_muestreadas"] = muestreadasLeidas.ToList();
                            d0["numero_vistas"] = numeroVistas;
                            if (conVistas)
                            {
                                d0["estilo_vista"] = estilosLeidos.Count == 1 ? estilosLeidos.First() : estilosLeidos.Count == 0 ? estiloUsado : string.Join(", ", estilosLeidos);
                                d0["punto_insercion"] = PuntoJson(punto);
                            }
                            return d0;
                        },
                        Verificar = d =>
                        {
                            string cola = " No se reintenta; deshaz con _.UNDO 1 si no quieres el grupo (comprueba después con listar_lineas_muestreo que desapareció). despues=" + Nucleo.Json.Serializar(d);
                            string motivo = Secciones.MotivoLineasNoCoinciden(rango.Progresivas, progresivasLeidas);
                            if (motivo != null) throw new InvalidOperationException("El grupo '" + nombreGrupo + "' se creó pero " + motivo + "." + cola);
                            if (muestreadasLeidas.Count == 0) throw new InvalidOperationException("El grupo '" + nombreGrupo + "' se creó pero no quedó ninguna fuente muestreada." + cola);
                            var faltan = fuentesPedidas.Where(p => !muestreadasLeidas.Any(m => string.Equals(m, p, StringComparison.OrdinalIgnoreCase))).ToList();
                            if (faltan.Count > 0) throw new InvalidOperationException("El grupo '" + nombreGrupo + "' se creó pero no quedaron muestreadas " + string.Join(", ", faltan) + "." + cola);
                            string dif = Secciones.DiferenciaFuentes(fuentesElegidas.Select(f => f.Nombre), muestreadasLeidas);
                            if (dif != null) ctx.Avisos.Add("Las fuentes reales del grupo no son las previstas en la simulación: " + dif + ". Quedaron muestreadas: " + string.Join(", ", muestreadasLeidas) + ".");
                            foreach (string av in avisosAnchos) ctx.Avisos.Add(av);
                            if (lineasSinSecciones > 0)
                                ctx.Avisos.Add(lineasSinSecciones + " de " + progresivasLeidas.Count + " líneas no tienen secciones: revisa las fuentes con listar_lineas_muestreo (fuentes=true) y que el terreno cubra la franja.");
                            if (conVistas)
                            {
                                int solapes = Secciones.Solapes(extensionesVistas);
                                if (solapes > 0) ctx.Avisos.Add(solapes + " pares de vistas de sección se solapan: muévelas en Civil 3D o vuelve a crearlas con otro 'punto_insercion'.");
                                if (extensionesLeidas < vistasCreadas.Count)
                                    ctx.Avisos.Add("No se pudo leer la extensión de " + (vistasCreadas.Count - extensionesLeidas) + " de " + vistasCreadas.Count + " vistas al colocarlas: se estimó su tamaño (pueden quedar separadas de más o solaparse).");
                                if (estiloPedido && estilosLeidos.Count > 0 && !estilosLeidos.All(e => string.Equals(e, estiloUsado, StringComparison.OrdinalIgnoreCase)))
                                    ctx.Avisos.Add("Las vistas quedaron con el estilo " + string.Join(", ", estilosLeidos) + " y se pidió '" + estiloUsado + "'.");
                            }
                        },
                        Extra = () => new
                        {
                            grupo = nombreGrupo,
                            alineamiento = nombreAlReal,
                            intervalo,
                            ancho_izquierdo = anchoIzq,
                            ancho_derecho = anchoDer,
                            rango = new { inicio = N(rango.Inicio), fin = N(rango.Fin), numero_lineas = rango.Numero },
                            lineas = lineasInfo,
                            lineas_sin_secciones = lineasSinSecciones,
                            fuentes = fuentesInfo,
                            fuentes_previstas = fuentesElegidas.Select(f => f.Nombre).ToList(),
                            vistas = vistasInfo,
                            vistas_colocadas = vistasCreadas,
                            filas_por_columna = conVistas ? filas : (int?)null,
                            columnas = conVistas ? Secciones.Columnas(vistasCreadas.Count, filas) : 0,
                            separacion = conVistas ? N(separacion) : null,
                            punto_insercion = conVistas ? PuntoJson(punto) : null,
                            punto_insercion_automatico = puntoAutomatico,
                            estilo_vista = estiloUsado,
                            estilo_vista_pedido = estiloPedido,
                            extensiones_leidas = conVistas ? extensionesLeidas : (int?)null,
                            solapes = conVistas ? Secciones.Solapes(extensionesVistas) : (int?)null,
                            grupos_vistas = gruposVistas
                        }
                    };
                    return Escritura.CrearObjeto(ctx, creacion);
                })
            });
        }
    }
}

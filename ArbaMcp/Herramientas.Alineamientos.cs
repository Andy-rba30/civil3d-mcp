using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.Settings;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// Herramientas de alineamientos (ejes). Desde 1.8.0: crear_alineamiento_desde_polilinea, la segunda herramienta de
    /// creación del flujo vial (la primera fue crear_superficie_tin), por el camino común Escritura.CrearObjeto.
    /// Vía de creación: Alignment.Create con PolylineOptions SIN curvas automáticas (AddCurvesBetweenTangents=false: el
    /// radio automático lo decide la configuración de comandos del dibujo, no la API, y no sería el pedido) y después
    /// una curva libre por PI con el radio calculado en ArbaMcp.Nucleo.Alineamiento (AlignmentEntityCollection.AddFreeCurve
    /// entre las dos tangentes del PI). La polilínea no se borra ni se modifica (EraseExistingEntities=false).
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaCrearAlineamiento = "crear_alineamiento_desde_polilinea";

        // ------------------------------------------------------------------ búsquedas para crear_alineamiento_desde_polilinea (1.8.0)
        /// <summary>Id del alineamiento con ese nombre o ObjectId.Null si no existe (para comprobar que un nombre está libre).</summary>
        private static ObjectId BuscarAlineamientoONull(Transaction tr, string nombre)
        {
            foreach (ObjectId id in CivilApplication.ActiveDocument.GetAlignmentIds())
                if (tr.GetObject(id, OpenMode.ForRead) is CivAlignment al && string.Equals(al.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return id;
            return ObjectId.Null;
        }

        /// <summary>
        /// Valor de un enumerado de la API por su nombre. El catálogo de la memoria técnica no registra los valores de los
        /// enumerados, así que se resuelven en tiempo de ejecución: el plugin compila aunque el nombre cambie y, si no
        /// existe, el error dice cuáles hay (para corregir miembros_por_verificar_civil3d.md sin adivinar).
        /// </summary>
        private static T ValorEnum<T>(string nombre) where T : struct
        {
            if (Enum.TryParse(nombre, true, out T v)) return v;
            throw new InvalidOperationException("El enumerado " + typeof(T).Name + " de la API de Civil 3D no tiene el valor '" + nombre + "'. Valores disponibles: " + string.Join(", ", Enum.GetNames(typeof(T))) + ". Anótalo en herramientas-dev/miembros_por_verificar_civil3d.md.");
        }

        /// <summary>
        /// Polilínea de entrada: por 'handle' o por 'capa' (exactamente uno). Con capa, la capa debe existir y tener una sola
        /// polilínea 2D (si hay varias, el error lista sus handles). Solo se acepta la polilínea ligera (LWPOLYLINE, clase
        /// Polyline): una Polyline3d o una Polyline2d pesada responde error con cómo convertirla o dibujarla.
        /// </summary>
        private static ObjectId BuscarPolilinea(Transaction tr, Database db, string handle, string capa, out string referencia)
        {
            bool porHandle = !string.IsNullOrWhiteSpace(handle), porCapa = !string.IsNullOrWhiteSpace(capa);
            if (porHandle && porCapa) throw new ArgumentException("Indica solo uno: 'handle' o 'capa'.");
            if (!porHandle && !porCapa) throw new ArgumentException("Falta la polilínea: indica 'handle' (el handle de la polilínea, por ejemplo '1A2B'; se ve con _.LIST) o 'capa' (una capa que tenga una sola polilínea).");
            if (porHandle)
            {
                handle = handle.Trim();
                referencia = "con handle '" + handle + "'";
                if (!IntentarHandle(db, handle, out ObjectId id))
                    throw new ArgumentException("No existe ningún objeto con handle '" + handle + "' en el dibujo. Anota el handle con _.LIST sobre la polilínea o usa 'capa'.");
                ComprobarQueEsPolilinea(tr.GetObject(id, OpenMode.ForRead), referencia);
                return id;
            }
            capa = capa.Trim();
            referencia = "de la capa '" + capa + "'";
            var capas = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!capas.Has(capa)) throw new ArgumentException("No existe la capa '" + capa + "' en el dibujo. Dibuja la polilínea de tangentes en una capa propia (por ejemplo EJE_PRUEBA) o pasa 'handle'.");
            var clase = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline));
            var encontradas = new List<string>();
            ObjectId unica = ObjectId.Null;
            foreach (ObjectId id in EntidadesModelo(tr, db))
            {
                if (!id.ObjectClass.IsDerivedFrom(clase)) continue;
                if (!(tr.GetObject(id, OpenMode.ForRead) is Polyline pl) || !string.Equals(pl.Layer, capa, StringComparison.OrdinalIgnoreCase)) continue;
                encontradas.Add(id.Handle.ToString() + " (" + pl.NumberOfVertices + " vértices)");
                unica = id;
            }
            if (encontradas.Count == 0) throw new ArgumentException("No hay ninguna polilínea 2D en la capa '" + capa + "'. Dibuja ahí la polilínea de tangentes (_.PLINE) o pasa 'handle'.");
            if (encontradas.Count > 1) throw new ArgumentException("La capa '" + capa + "' tiene " + encontradas.Count + " polilíneas; indica cuál con 'handle': " + string.Join(", ", encontradas) + ".");
            return unica;
        }

        private static void ComprobarQueEsPolilinea(DBObject o, string referencia)
        {
            if (o is Polyline) return;
            string motivo = o is Polyline3d ? "es una polilínea 3D (POLYLINE 3D)"
                : o is Polyline2d ? "es una polilínea 2D pesada (POLYLINE); conviértela a ligera con _.CONVERTPOLY (opción Ligera)"
                : "es un objeto " + o.GetType().Name + ", no una polilínea";
            throw new ArgumentException(Alineamiento.MensajePolilineaNoValida(referencia, motivo));
        }

        /// <summary>
        /// Vértices (x, y) de la polilínea de tangentes. Error si está cerrada, tiene menos de 2 vértices, algún tramo es un
        /// arco (bulge distinto de 0) o no está en el plano XY (normal distinta de Z).
        /// </summary>
        private static List<Punto2> VerticesDePolilinea(Polyline pl, string referencia)
        {
            if (pl.Closed) throw new ArgumentException(Alineamiento.MensajePolilineaNoValida(referencia, "está cerrada"));
            int n = pl.NumberOfVertices;
            if (n < 2) throw new ArgumentException(Alineamiento.MensajePolilineaNoValida(referencia, "tiene " + n + " vértices"));
            var normal = pl.Normal;
            if (Math.Abs(Math.Abs(normal.Z) - 1) > 1e-9)
                throw new ArgumentException(Alineamiento.MensajePolilineaNoValida(referencia, "no está en el plano XY (su normal no es el eje Z)"));
            int arcos = 0;
            for (int i = 0; i < n - 1; i++) if (Math.Abs(pl.GetBulgeAt(i)) > 1e-12) arcos++;
            if (arcos > 0) throw new ArgumentException(Alineamiento.MensajePolilineaNoValida(referencia, "tiene " + arcos + " tramo" + (arcos == 1 ? "" : "s") + " de arco"));
            var vertices = new List<Punto2>();
            for (int i = 0; i < n; i++) { var p = pl.GetPoint2dAt(i); vertices.Add(new Punto2(p.X, p.Y)); }
            return vertices;
        }

        /// <summary>Nombre de un estilo o conjunto de etiquetas (StyleBase.Name se lee por reflexión, como en crear_superficie_tin).</summary>
        private static string NombreEstilo(Transaction tr, ObjectId id)
        {
            if (id.IsNull) return null;
            try { return Api.Leer<string>(tr.GetObject(id, OpenMode.ForRead), null, "Name", "DisplayName"); }
            catch { return null; }
        }

        /// <summary>Busca por nombre en una colección de estilos (Contains e indexador primero; si no, recorriendo); error con la lista si no está.</summary>
        private static ObjectId BuscarEnEstilos(Transaction tr, IEnumerable coleccion, Func<string, bool> contiene, Func<string, ObjectId> porNombre, string nombre, string tipo, string dondeSeVen)
        {
            try { if (contiene(nombre)) return porNombre(nombre); }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearAlineamiento + ": Contains/indexador de " + tipo + " falló, se recorre la colección: " + ex.Message); }
            var nombres = new List<string>();
            foreach (object o in coleccion)
            {
                if (!(o is ObjectId id)) continue;
                string n = NombreEstilo(tr, id);
                if (n == null) continue;
                if (string.Equals(n, nombre, StringComparison.OrdinalIgnoreCase)) return id;
                nombres.Add(n);
            }
            throw new ArgumentException("No existe " + tipo + " '" + nombre + "' en el dibujo. Disponibles: " + (nombres.Count == 0 ? "ninguno legible" : string.Join(", ", nombres)) + ". En Civil 3D se ven en " + dondeSeVen + ".");
        }

        private static ObjectId BuscarEstiloAlineamiento(Transaction tr, string nombre)
        {
            var estilos = CivilApplication.ActiveDocument.Styles.AlignmentStyles;
            return BuscarEnEstilos(tr, estilos, estilos.Contains, n => estilos[n], nombre, "el estilo de alineamiento", "Espacio de herramientas → Configuración → Alineación → Estilos de alineación");
        }

        private static ObjectId BuscarConjuntoEtiquetasAlineamiento(Transaction tr, string nombre)
        {
            var conjuntos = CivilApplication.ActiveDocument.Styles.LabelSetStyles.AlignmentLabelSetStyles;
            return BuscarEnEstilos(tr, conjuntos, conjuntos.Contains, n => conjuntos[n], nombre, "el conjunto de etiquetas de alineamiento", "Espacio de herramientas → Configuración → Alineación → Estilos de etiqueta → Conjuntos de etiquetas");
        }

        /// <summary>Primer ObjectId de una colección de estilos o ObjectId.Null si está vacía.</summary>
        private static ObjectId PrimerEstilo(IEnumerable coleccion)
        {
            foreach (object o in coleccion) if (o is ObjectId id && !id.IsNull) return id;
            return ObjectId.Null;
        }

        /// <summary>
        /// Estilo de alineamiento por defecto del dibujo: el de la configuración de alineaciones
        /// (SettingsAlignment.StyleSettings.AlignmentStyleId); si no se puede leer, el primero del dibujo con aviso.
        /// </summary>
        private static ObjectId EstiloAlineamientoPorDefecto(Transaction tr, List<string> avisos, out string nombre)
        {
            nombre = null;
            try
            {
                var ajustes = CivilApplication.ActiveDocument.Settings.GetSettings<SettingsAlignment>().StyleSettings;
                ObjectId id = ajustes.AlignmentStyleId.Value;
                if (!id.IsNull) { nombre = NombreEstilo(tr, id) ?? ajustes.AlignmentStyle.Value; return id; }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearAlineamiento + ": no se pudo leer el estilo por defecto (SettingsAlignment.StyleSettings): " + ex.Message); }
            ObjectId primero = PrimerEstilo(CivilApplication.ActiveDocument.Styles.AlignmentStyles);
            if (primero.IsNull)
                throw new ArgumentException("El dibujo no tiene ningún estilo de alineamiento; usa una plantilla de Civil 3D con estilos o crea uno en Espacio de herramientas → Configuración → Alineación → Estilos de alineación.");
            nombre = NombreEstilo(tr, primero);
            avisos.Add("No se pudo leer el estilo de alineamiento por defecto de la configuración del dibujo; se usa '" + (nombre ?? "?") + "' (el primero del dibujo). Pasa 'estilo' para elegir otro.");
            return primero;
        }

        /// <summary>
        /// Conjunto de etiquetas por defecto: el de la configuración de alineaciones (AlignmentLabelSetId); si no se puede
        /// leer, el primero del dibujo con aviso; si no hay ninguno, ObjectId.Null (sin etiquetas) con aviso.
        /// </summary>
        private static ObjectId ConjuntoEtiquetasPorDefecto(Transaction tr, List<string> avisos, out string nombre)
        {
            nombre = null;
            try
            {
                var ajustes = CivilApplication.ActiveDocument.Settings.GetSettings<SettingsAlignment>().StyleSettings;
                ObjectId id = ajustes.AlignmentLabelSetId.Value;
                if (!id.IsNull) { nombre = NombreEstilo(tr, id) ?? ajustes.AlignmentLabelSet.Value; return id; }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearAlineamiento + ": no se pudo leer el conjunto de etiquetas por defecto (SettingsAlignment.StyleSettings): " + ex.Message); }
            ObjectId primero = PrimerEstilo(CivilApplication.ActiveDocument.Styles.LabelSetStyles.AlignmentLabelSetStyles);
            if (primero.IsNull)
            {
                avisos.Add("El dibujo no tiene conjuntos de etiquetas de alineamiento: el eje se crea sin etiquetas.");
                return ObjectId.Null;
            }
            nombre = NombreEstilo(tr, primero);
            avisos.Add("No se pudo leer el conjunto de etiquetas por defecto de la configuración del dibujo; se usa '" + (nombre ?? "?") + "' (el primero del dibujo). Pasa 'conjunto_etiquetas' para elegir otro.");
            return primero;
        }

        /// <summary>Sitio por nombre (CivilDocument.GetSiteIds); error con la lista si no existe. Sin 'sitio', el eje se crea sin sitio.</summary>
        private static ObjectId BuscarSitio(Transaction tr, string nombre)
        {
            var nombres = new List<string>();
            foreach (ObjectId id in CivilApplication.ActiveDocument.GetSiteIds())
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Civ.Site s)) continue;
                if (string.Equals(s.Name, nombre, StringComparison.OrdinalIgnoreCase)) return id;
                nombres.Add(s.Name);
            }
            throw new ArgumentException("No existe el sitio '" + nombre + "'. Sitios del dibujo: " + (nombres.Count == 0 ? "ninguno" : string.Join(", ", nombres)) + ". Sin 'sitio' el eje se crea sin sitio (siteless), que es lo habitual para un eje de vía.");
        }

        /// <summary>
        /// Capa del alineamiento nuevo: la capa de objetos "Alineación" de la configuración del dibujo
        /// (ObjectLayerSettings.GetObjectLayerSetting(Alignment)); si no se puede leer o no existe, la capa actual, con aviso.
        /// </summary>
        private static ObjectId CapaParaAlineamientos(Transaction tr, Database db, List<string> avisos)
        {
            try
            {
                var ajuste = CivilApplication.ActiveDocument.Settings.DrawingSettings.ObjectLayerSettings.GetObjectLayerSetting(ValorEnum<SettingsObjectLayerType>("Alignment"));
                ObjectId idCapa = ajuste.LayerId;
                if (!idCapa.IsNull) return idCapa;
                string nombreCapa = ajuste.LayerName;
                var capas = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!string.IsNullOrWhiteSpace(nombreCapa) && capas.Has(nombreCapa)) return capas[nombreCapa];
                avisos.Add("La capa de alineaciones de la configuración del dibujo (" + (string.IsNullOrWhiteSpace(nombreCapa) ? "sin nombre" : "'" + nombreCapa + "'") + ") no existe en el dibujo; el eje va a la capa actual.");
            }
            catch (Exception ex) { avisos.Add("No se pudo leer la capa de alineaciones de la configuración del dibujo (" + ex.Message + "); el eje va a la capa actual."); }
            return db.Clayer;
        }

        /// <summary>Curvas (AlignmentArc) del alineamiento, en orden, leídas del dibujo: radio, longitud, deflexión y progresivas.</summary>
        private static List<Dictionary<string, object>> CurvasDelAlineamiento(CivAlignment al)
        {
            var lista = new List<Dictionary<string, object>>();
            var entidades = al.Entities;
            for (int i = 0; i < entidades.Count; i++)
            {
                if (!(entidades.GetEntityByOrder(i) is Civ.AlignmentArc arco)) continue;
                lista.Add(new Dictionary<string, object>
                {
                    ["orden"] = i,
                    ["radio"] = N(arco.Radius),
                    ["longitud"] = N(arco.EndStation - arco.StartStation),
                    ["deflexion_grados"] = N(Alineamiento.Grados(arco.Delta)),
                    ["progresiva_pi"] = N(arco.PIStation),
                    ["progresiva_pc"] = N(arco.StartStation),
                    ["progresiva_pt"] = N(arco.EndStation),
                    ["horario"] = arco.Clockwise
                });
            }
            return lista;
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarAlineamientos()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaCrearAlineamiento,
                Descripcion = "Crea un alineamiento (eje) nuevo a partir de una polilínea 2D de tangentes (sus vértices son los PI) indicada por 'handle' o por 'capa' (exactamente uno), con una curva circular en cada PI intermedio: 'radio' para todas y, opcionalmente, 'radios_por_pi' para cambiar alguna (0 = sin curva). Antes de crear calcula, sin Civil 3D, si cada radio cabe entre las tangentes; el que no cabe se deja sin curva y va a 'avisos' con el radio máximo que cabría. Usa Alignment.Create con PolylineOptions (sin curvas automáticas y sin borrar la polilínea) y añade cada curva con AddFreeCurve. Devuelve la lista 'pis' (número, progresiva, x, y, deflexión, radio pedido, radio aplicado, longitud de curva, aviso), longitud, progresivas, estilo, conjunto de etiquetas y sitio. Si ya existe un alineamiento con ese nombre, error sin tocar nada. La polilínea debe ser 2D, abierta y solo de tramos rectos: con arcos o 3D responde error explicando cómo dibujarla.",
                Parametros =
                {
                    P("nombre", "string", "Nombre del alineamiento nuevo (no debe existir)", true),
                    P("handle", "string", "Handle de la polilínea de tangentes (se ve con _.LIST). Excluyente con 'capa'"),
                    P("capa", "string", "Capa que contiene una sola polilínea de tangentes. Si tiene varias o ninguna, error con los handles. Excluyente con 'handle'"),
                    P("radio", "number", "Radio por defecto (> 0) de la curva de cada PI, en unidades del dibujo", true),
                    P("radios_por_pi", "json", "Radios distintos por PI: lista en orden de PI ([80, 0, 120]; 0 o null = sin curva) o lista de objetos [{\"pi\": 2, \"radio\": 0}] con pi desde 1 (primer PI intermedio). Los PI no nombrados usan 'radio'"),
                    P("estilo", "string", "Estilo de alineamiento del dibujo. Si falta, el de la configuración del dibujo; si no existe, error con la lista"),
                    P("conjunto_etiquetas", "string", "Conjunto de etiquetas de alineamiento del dibujo. Si falta, el de la configuración del dibujo; si no existe, error con la lista"),
                    P("sitio", "string", "Nombre de un sitio del dibujo. Si falta, el eje se crea sin sitio (siteless)"),
                    P("simular", "boolean", "Con true lee la polilínea, calcula los PI y las curvas que caben y devuelve 'pis' y 'avisos' sin tocar el dibujo (ni copia)")
                },
                Ejecutar = a => Escritura.Ejecutar(HerramientaCrearAlineamiento, a, ctx =>
                {
                    string nombre = Requerido(a, "nombre").Trim();
                    string handle = Str(a, "handle"), capa = Str(a, "capa"), estilo = Str(a, "estilo"), conjunto = Str(a, "conjunto_etiquetas"), sitio = Str(a, "sitio");
                    if (!Tiene(a, "radio")) throw new ArgumentException("Falta el parámetro obligatorio 'radio' (radio por defecto de las curvas, > 0).");
                    double radio = Num(a, "radio", 0);
                    Alineamiento.ValidarRadio(radio);
                    var jsonRadios = JsonArg(a, "radios_por_pi");
                    var db = ctx.Db;

                    ObjectId idPolilinea = ObjectId.Null, idEstilo = ObjectId.Null, idEtiquetas = ObjectId.Null, idSitio = ObjectId.Null, idCapa = ObjectId.Null;
                    string referencia = null, handlePolilinea = null, capaPolilinea = null, estiloUsado = estilo, conjuntoUsado = conjunto, capaEje = null, sitioLeido = null;
                    PlanAlineamiento plan = null;
                    List<Dictionary<string, object>> curvas = null;
                    double longitudReal = 0, inicioReal = 0, finReal = 0;
                    // 'numero_curvas' se completa en Antes, cuando ya se conoce el plan (CrearObjeto lo compara con 'despues' al final)
                    var esperado = new Dictionary<string, object> { ["existe"] = true };

                    Dictionary<string, object> EstadoPrevisto(bool real) => new Dictionary<string, object>
                    {
                        ["alineamiento"] = nombre,
                        ["existe"] = true,
                        ["longitud"] = N(real ? longitudReal : plan.LongitudTotal),
                        ["progresiva_inicial"] = N(real ? inicioReal : plan.ProgresivaInicial),
                        ["progresiva_final"] = N(real ? finReal : plan.ProgresivaFinal),
                        ["numero_pi"] = plan.NumeroPi,
                        ["numero_curvas"] = real ? curvas.Count : plan.NumeroCurvas,
                        ["estilo"] = estiloUsado,
                        ["conjunto_etiquetas"] = conjuntoUsado ?? "(sin etiquetas)",
                        ["sitio"] = real ? sitioLeido : sitio,
                        ["capa"] = capaEje,
                        ["pis"] = plan.PisComoDiccionarios()
                    };

                    return Escritura.CrearObjeto(ctx, new Escritura.Creacion
                    {
                        Accion = "Crear el alineamiento '" + nombre + "' desde la polilínea " + (string.IsNullOrWhiteSpace(handle) ? "de la capa '" + capa + "'" : "con handle '" + handle + "'") + " con radio " + radio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + (jsonRadios.ValueKind == System.Text.Json.JsonValueKind.Undefined ? "" : " y radios por PI") + " en cada PI",
                        Antes = tr =>
                        {
                            if (!BuscarAlineamientoONull(tr, nombre).IsNull) throw new ArgumentException("Ya existe el alineamiento '" + nombre + "'. Usa listar_alineamientos.");
                            idPolilinea = BuscarPolilinea(tr, db, handle, capa, out referencia);
                            var pl = (Polyline)tr.GetObject(idPolilinea, OpenMode.ForRead);
                            handlePolilinea = idPolilinea.Handle.ToString();
                            capaPolilinea = pl.Layer;
                            var vertices = VerticesDePolilinea(pl, referencia);
                            var radios = Alineamiento.RadiosPorPi(jsonRadios, vertices.Count - 2, radio);
                            plan = Alineamiento.Planificar(vertices, radio, radios);
                            idEstilo = string.IsNullOrWhiteSpace(estilo) ? EstiloAlineamientoPorDefecto(tr, ctx.Avisos, out estiloUsado) : BuscarEstiloAlineamiento(tr, estilo.Trim());
                            idEtiquetas = string.IsNullOrWhiteSpace(conjunto) ? ConjuntoEtiquetasPorDefecto(tr, ctx.Avisos, out conjuntoUsado) : BuscarConjuntoEtiquetasAlineamiento(tr, conjunto.Trim());
                            if (!string.IsNullOrWhiteSpace(sitio)) idSitio = BuscarSitio(tr, sitio.Trim());
                            idCapa = CapaParaAlineamientos(tr, db, ctx.Avisos);
                            try { capaEje = ((LayerTableRecord)tr.GetObject(idCapa, OpenMode.ForRead)).Name; } catch { capaEje = null; }
                            esperado["numero_curvas"] = plan.NumeroCurvas;
                            return new Dictionary<string, object> { ["alineamiento"] = nombre, ["existe"] = false, ["longitud"] = 0.0, ["numero_curvas"] = 0 };
                        },
                        Prevision = tr =>
                        {
                            ctx.Avisos.AddRange(plan.Avisos);
                            return EstadoPrevisto(false);
                        },
                        Esperado = esperado,
                        Crear = tr =>
                        {
                            var opciones = new Civ.PolylineOptions { PlineId = idPolilinea, AddCurvesBetweenTangents = false, EraseExistingEntities = false };
                            ObjectId id = CivAlignment.Create(CivilApplication.ActiveDocument, opciones, nombre, idSitio, idCapa, idEstilo, idEtiquetas);
                            if (id.IsNull) return id;
                            var al = (CivAlignment)tr.GetObject(id, OpenMode.ForWrite);
                            var entidades = al.Entities;
                            var idsTangentes = new List<int>();
                            for (int i = 0; i < entidades.Count; i++)
                            {
                                var e = entidades.GetEntityByOrder(i);
                                if (!(e is Civ.AlignmentLine)) throw new InvalidOperationException("Civil 3D creó desde la polilínea una entidad " + e.GetType().Name + " (orden " + i + ") en vez de una tangente; no se añaden curvas y nada queda escrito. No se reintenta.");
                                idsTangentes.Add(e.EntityId);
                            }
                            if (idsTangentes.Count != plan.Tramos.Count)
                                throw new InvalidOperationException("Civil 3D creó " + idsTangentes.Count + " tangentes desde la polilínea y la polilínea tiene " + plan.Tramos.Count + " tramos; no se añaden curvas y nada queda escrito. No se reintenta.");
                            var tipoParametro = ValorEnum<Civ.CurveParamType>("Radius");
                            var tipoCurva = ValorEnum<Civ.CurveType>("Compound");
                            foreach (var pi in plan.Pis)
                            {
                                if (!pi.RadioAplicado.HasValue) continue;
                                try { entidades.AddFreeCurve(idsTangentes[pi.Numero - 1], idsTangentes[pi.Numero], pi.RadioAplicado.Value, tipoParametro, false, tipoCurva); }
                                catch (Exception ex) { throw new InvalidOperationException("No se pudo crear la curva del PI " + pi.Numero + " (radio " + pi.RadioAplicado.Value + ", deflexión " + Math.Round(pi.DeflexionGrados, 3) + "°): " + ex.Message + ". Nada queda escrito (la transacción se descarta) y no se reintenta; prueba con 'radios_por_pi' poniendo 0 en ese PI.", ex); }
                            }
                            return id;
                        },
                        Despues = (tr, id) =>
                        {
                            var al = (CivAlignment)tr.GetObject(id, OpenMode.ForRead);
                            longitudReal = al.Length; inicioReal = al.StartingStation; finReal = al.EndingStation;
                            curvas = CurvasDelAlineamiento(al);
                            estiloUsado = Api.Leer<string>(al, estiloUsado, "StyleName") ?? estiloUsado;
                            try { sitioLeido = string.IsNullOrWhiteSpace(al.SiteName) ? null : al.SiteName; } catch { sitioLeido = sitio; }
                            try { capaEje = al.Layer ?? capaEje; } catch { }
                            return EstadoPrevisto(true);
                        },
                        Verificar = d =>
                        {
                            string cola = " No se reintenta; deshaz con Ctrl+Z si no quieres el eje. despues=" + Nucleo.Json.Serializar(d);
                            if (longitudReal <= 0)
                                throw new InvalidOperationException("El alineamiento '" + nombre + "' se creó pero su longitud es " + longitudReal + " (debería ser " + Math.Round(plan.LongitudTotal, 3) + ")." + cola);
                            if (curvas.Count != plan.NumeroCurvas)
                                throw new InvalidOperationException("El alineamiento '" + nombre + "' se creó con " + curvas.Count + " curvas y se previeron " + plan.NumeroCurvas + "." + cola);
                            var previstas = plan.Pis.Where(p => p.RadioAplicado.HasValue).ToList();
                            for (int i = 0; i < previstas.Count; i++)
                            {
                                double leido = Convert.ToDouble(curvas[i]["radio"] ?? 0.0);
                                if (Math.Abs(leido - previstas[i].RadioAplicado.Value) > 0.01)
                                    throw new InvalidOperationException("La curva del PI " + previstas[i].Numero + " del alineamiento '" + nombre + "' quedó con radio " + leido + " y se pidió " + previstas[i].RadioAplicado.Value + "." + cola);
                            }
                            if (Math.Abs(longitudReal - plan.LongitudTotal) > 0.01)
                                ctx.Avisos.Add("La longitud leída del eje (" + Math.Round(longitudReal, 4) + ") difiere de la prevista (" + Math.Round(plan.LongitudTotal, 4) + ") en más de 0,01; revisa las progresivas con listar_alineamientos.");
                        },
                        Extra = () => new
                        {
                            nombre,
                            handle_polilinea = handlePolilinea,
                            capa_polilinea = capaPolilinea,
                            vertices = plan.Vertices.Count,
                            estilo = estiloUsado,
                            conjunto_etiquetas = conjuntoUsado ?? "(sin etiquetas)",
                            sitio = sitioLeido,
                            capa = capaEje,
                            radio,
                            longitud_polilinea = N(plan.LongitudPolilinea),
                            longitud_total = N(longitudReal),
                            longitud_prevista = N(plan.LongitudTotal),
                            progresiva_inicial = N(inicioReal),
                            progresiva_final = N(finReal),
                            numero_pi = plan.NumeroPi,
                            numero_curvas = curvas.Count,
                            pis = plan.PisComoDiccionarios(),
                            curvas
                        }
                    });
                })
            });
        }
    }
}

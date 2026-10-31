using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivProfile = Autodesk.Civil.DatabaseServices.Profile;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// Rasantes (perfiles de diseño, FG). Desde 1.10.0: crear_rasante, la cuarta herramienta de creación del flujo vial
    /// (tras crear_superficie_tin, crear_alineamiento_desde_polilinea y crear_perfil_terreno_y_vista), por el camino común
    /// Escritura.CrearObjeto. Vía de creación: Profile.CreateByLayout (perfil de composición vacío, con estilo y conjunto
    /// de etiquetas de perfil), un ProfilePVICollection.AddPVI por cada PVI en orden de progresiva (los tramos entre PVI
    /// son las tangentes) y, en cada PVI intermedio con curva, ProfileEntityCollection.AddFreeSymmetricParabolaByPVIAndCurveLength
    /// (o AddFreeCircularCurveByPVIAndLength con tipo_curva=circular). Toda la geometría (pendientes, A, K, cresta o valle,
    /// si cada curva cabe) la calcula ArbaMcp.Nucleo.Rasante antes de tocar el dibujo; el perfil se relee y se comprueba
    /// (número de PVI, progresivas y cotas de cada PVI, rango) antes de responder.
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaCrearRasante = "crear_rasante";

        // ------------------------------------------------------------------ nombres de estilos para los errores (1.10.0)
        /// <summary>
        /// Nombre de un estilo o conjunto de etiquetas. La reflexión por nombre (Api.Leer "Name") devolvió null en Civil 3D
        /// 2027 (los errores de 1.7.0 a 1.9.0 listaron "ninguno legible"): StyleBase declara una propiedad Name solo de
        /// escritura que oculta la de su clase base Autodesk.Civil.DatabaseServices.DBObject (lectura/escritura), y
        /// Type.GetProperty("Name") no sabe cuál elegir. Aquí se lee con enlace en tiempo de compilación al getter de la
        /// clase base y, si tampoco, con la clave del diccionario de AutoCAD que contiene el estilo (DBDictionary.NameAt
        /// sobre OwnerId). Las dos rutas están confirmadas en el catálogo; cuál funciona lo dice la validación.
        /// </summary>
        private static string NombreDeEstilo(Transaction tr, ObjectId id)
        {
            if (id.IsNull) return null;
            DBObject o;
            try { o = tr.GetObject(id, OpenMode.ForRead); } catch { return null; }
            try
            {
                if (o is Civ.DBObject civil)
                {
                    string n = civil.Name;
                    if (!string.IsNullOrWhiteSpace(n)) return n;
                }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearRasante + ": DBObject.Name del estilo " + id.Handle + " falló (" + ex.GetType().Name + "); se prueba con el diccionario."); }
            try
            {
                ObjectId dueno = o.OwnerId;
                if (!dueno.IsNull && tr.GetObject(dueno, OpenMode.ForRead) is DBDictionary dic && dic.Contains(id))
                    return dic.NameAt(id);
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearRasante + ": DBDictionary.NameAt del estilo " + id.Handle + " falló (" + ex.GetType().Name + ")."); }
            return null;
        }

        /// <summary>Nombres legibles de una colección de estilos (para los errores y la memoria técnica); vacío si ninguno se pudo leer.</summary>
        private static List<string> NombresDeEstilos(Transaction tr, IEnumerable coleccion)
        {
            var nombres = new List<string>();
            foreach (object o in coleccion)
            {
                if (!(o is ObjectId id) || id.IsNull) continue;
                string n = NombreDeEstilo(tr, id);
                if (n != null) nombres.Add(n);
            }
            return nombres;
        }

        /// <summary>Busca por nombre (Contains e indexador; si fallan, recorriendo y leyendo nombres); error con la lista legible si no está.</summary>
        private static ObjectId BuscarEstiloConNombres(Transaction tr, IEnumerable coleccion, Func<string, bool> contiene, Func<string, ObjectId> porNombre, string nombre, string tipo, string dondeSeVen)
        {
            try { if (contiene(nombre)) return porNombre(nombre); }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearRasante + ": Contains/indexador de " + tipo + " falló, se recorre la colección: " + ex.Message); }
            var nombres = new List<string>();
            foreach (object o in coleccion)
            {
                if (!(o is ObjectId id) || id.IsNull) continue;
                string n = NombreDeEstilo(tr, id);
                if (n == null) continue;
                if (string.Equals(n, nombre, StringComparison.OrdinalIgnoreCase)) return id;
                nombres.Add(n);
            }
            throw new ArgumentException("No existe " + tipo + " '" + nombre + "' en el dibujo. Disponibles: " + (nombres.Count == 0 ? "ninguno legible" : string.Join(", ", nombres)) + ". En Civil 3D se ven en " + dondeSeVen + ".");
        }

        private static ObjectId BuscarEstiloPerfilRasante(Transaction tr, string nombre)
        {
            var estilos = CivilApplication.ActiveDocument.Styles.ProfileStyles;
            return BuscarEstiloConNombres(tr, estilos, estilos.Contains, n => estilos[n], nombre, "el estilo de perfil", "Espacio de herramientas → Configuración → Perfil → Estilos de perfil");
        }

        private static ObjectId BuscarConjuntoEtiquetasRasante(Transaction tr, string nombre)
        {
            var conjuntos = CivilApplication.ActiveDocument.Styles.LabelSetStyles.ProfileLabelSetStyles;
            return BuscarEstiloConNombres(tr, conjuntos, conjuntos.Contains, n => conjuntos[n], nombre, "el conjunto de etiquetas de perfil", "Espacio de herramientas → Configuración → Perfil → Estilos de etiqueta → Conjuntos de etiquetas");
        }

        // ------------------------------------------------------------------ relectura del perfil
        /// <summary>PVI releídos del perfil (progresiva por RawStation, cota, pendientes de la API tal cual y tipo).</summary>
        private static List<Dictionary<string, object>> PvisDelPerfil(CivProfile pr, out List<(double progresiva, double cota)> puntos)
        {
            var lista = new List<Dictionary<string, object>>();
            puntos = new List<(double, double)>();
            foreach (Civ.ProfilePVI pvi in pr.PVIs)
            {
                double s = pvi.RawStation, z = pvi.Elevation;
                puntos.Add((s, z));
                var d = new Dictionary<string, object> { ["progresiva"] = N(s), ["cota"] = N(z) };
                try { d["pendiente_entrada_api"] = pvi.GradeIn; d["pendiente_salida_api"] = pvi.GradeOut; } catch { d["pendiente_entrada_api"] = null; d["pendiente_salida_api"] = null; }
                try { d["tipo"] = pvi.PVIType.ToString(); } catch { d["tipo"] = null; }
                lista.Add(d);
            }
            lista.Sort((a, b) => Convert.ToDouble(a["progresiva"] ?? 0.0).CompareTo(Convert.ToDouble(b["progresiva"] ?? 0.0)));
            for (int i = 0; i < lista.Count; i++) lista[i]["numero"] = i + 1;
            return lista;
        }

        /// <summary>Entidades releídas del perfil (tangentes y curvas, en orden): tipo, progresivas, longitud y, en las curvas, K, PVI y tipo de la API.</summary>
        private static List<Dictionary<string, object>> EntidadesDelPerfil(CivProfile pr, out int tangentes, out List<Dictionary<string, object>> curvas)
        {
            var lista = new List<Dictionary<string, object>>();
            curvas = new List<Dictionary<string, object>>();
            tangentes = 0;
            foreach (Civ.ProfileEntity ent in pr.Entities)
            {
                var d = new Dictionary<string, object>
                {
                    ["tipo"] = ent.EntityType.ToString(),
                    ["progresiva_inicio"] = N(ent.StartStation),
                    ["progresiva_fin"] = N(ent.EndStation)
                };
                try { d["longitud"] = N(ent.Length); } catch { d["longitud"] = N(ent.EndStation - ent.StartStation); }
                try { d["cota_inicio"] = N(ent.StartElevation); d["cota_fin"] = N(ent.EndElevation); } catch { }
                if (ent is Civ.ProfileTangent tg)
                {
                    tangentes++;
                    try { d["pendiente_api"] = tg.Grade; } catch { d["pendiente_api"] = null; }
                }
                else if (ent is Civ.ProfileParabolaSymmetric par)
                {
                    try { d["k"] = N(par.K); } catch { d["k"] = null; }
                    try { d["progresiva_pvi"] = N(par.PVIStation); d["cota_pvi"] = N(par.PVIElevation); } catch { }
                    try { d["tipo_curva_api"] = par.CurveType.ToString(); } catch { d["tipo_curva_api"] = null; }
                    try { d["pendiente_entrada_api"] = par.GradeIn; d["pendiente_salida_api"] = par.GradeOut; } catch { }
                    try { d["progresiva_punto_alto_bajo"] = N(par.HighLowPointStation); d["cota_punto_alto_bajo"] = N(par.HighLowPointElevation); } catch { }
                    curvas.Add(d);
                }
                else if (ent is Civ.ProfileCircular cir)
                {
                    try { d["k"] = N(cir.K); d["radio"] = N(cir.Radius); } catch { }
                    try { d["progresiva_pvi"] = N(cir.PVIStation); d["cota_pvi"] = N(cir.PVIElevation); } catch { }
                    try { d["tipo_curva_api"] = cir.CurveType.ToString(); } catch { d["tipo_curva_api"] = null; }
                    try { d["pendiente_entrada_api"] = cir.GradeIn; d["pendiente_salida_api"] = cir.GradeOut; } catch { }
                    curvas.Add(d);
                }
                else if (d["tipo"] is string t && t.IndexOf("Tangent", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    curvas.Add(d);   // otra clase de curva (asimétrica...): no debería aparecer, pero cuenta como curva
                }
                else tangentes++;
                lista.Add(d);
            }
            return lista;
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarRasantes()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaCrearRasante,
                Descripcion = "Crea la rasante (perfil de diseño, tipo FG) de un alineamiento a partir de una lista ordenada de PVI (progresiva, cota) con sus longitudes de curva vertical. Antes de tocar el dibujo calcula, sin Civil 3D, la pendiente de cada tramo (%), y en cada PVI intermedio el cambio de pendiente A, K = L/|A|, si es cresta o valle, las progresivas y cotas de inicio y fin de la curva y si la curva cabe (la mitad de una curva no puede pasar del PVI vecino ni solaparse con la curva de al lado; la que no cabe se deja como quiebre de pendiente y 'avisos' dice la longitud máxima que cabría). Crea el perfil con Profile.CreateByLayout, un PVI por elemento (ProfilePVICollection.AddPVI) y una parábola simétrica por PVI intermedio con curva (AddFreeSymmetricParabolaByPVIAndCurveLength; con tipo_curva=circular, AddFreeCircularCurveByPVIAndLength), todo en una sola entrada de Deshacer. Relee el perfil y comprueba que el número de PVI, sus progresivas y cotas y el rango coinciden con lo pedido (si no, error sin reintentar). Devuelve 'tramos' (pendientes), 'curvas' (una por PVI intermedio: A, K, tipo, progresivas y cotas de inicio y fin, aviso), 'pvis_creados', progresivas y cotas extremas, estilo y conjunto de etiquetas usados. Con simular=true solo comprueba e informa (sin copia ni cambios). Si ya existe un perfil con ese nombre en el alineamiento, error sin tocar nada. Una pendiente mayor que el 12 % solo produce un aviso informativo (no hay norma cargada).",
                Parametros =
                {
                    P("alineamiento", "string", "Nombre del alineamiento (eje) existente", true),
                    P("nombre", "string", "Nombre del perfil de diseño nuevo (no debe existir en ese alineamiento)", true),
                    P("pvis", "json", "Lista ordenada por progresiva de objetos {\"progresiva\": n, \"cota\": n, \"longitud_curva\": n opcional}; mínimo 2; progresivas estrictamente crecientes y dentro del eje (hasta 0,01 fuera se ajusta al extremo con aviso); el primero y el último no llevan curva", true),
                    P("longitud_curva", "number", "Longitud de curva vertical (m) por defecto para los PVI intermedios que no traigan la suya. Sin este parámetro ni longitudes propias, los PVI intermedios quedan como quiebres de pendiente"),
                    P("tipo_curva", "string", "'parabola_simetrica' (por defecto) o 'circular'"),
                    P("estilo", "string", "Estilo de perfil del dibujo. Si falta, el de la configuración del dibujo; si no existe, error con la lista"),
                    P("conjunto_etiquetas", "string", "Conjunto de etiquetas de perfil del dibujo. Si falta, el de la configuración del dibujo; si no existe, error con la lista"),
                    P("vista_perfil", "string", "Nombre de una vista de perfil del alineamiento, solo para comprobar que existe e informarlo: el perfil se dibuja en las vistas del alineamiento según su configuración"),
                    P("simular", "boolean", "Con true comprueba alineamiento, nombre libre, orden y rango de los PVI y estilos, y devuelve 'tramos', 'curvas' y 'avisos' previstos sin tocar el dibujo (ni copia)")
                },
                Ejecutar = a => Escritura.Ejecutar(HerramientaCrearRasante, a, ctx =>
                {
                    string nombreAl = Requerido(a, "alineamiento").Trim();
                    string nombre = Requerido(a, "nombre").Trim();
                    string estilo = Str(a, "estilo"), conjunto = Str(a, "conjunto_etiquetas"), vistaPedida = Str(a, "vista_perfil");
                    string tipoCurva = Rasante.TipoCurvaNormalizado(Str(a, "tipo_curva"));
                    double? longitudDefecto = Tiene(a, "longitud_curva") ? Num(a, "longitud_curva", 0) : (double?)null;
                    Rasante.ValidarLongitudCurva(longitudDefecto, "'longitud_curva'");
                    var pvisPedidos = Rasante.LeerPvis(JsonArg(a, "pvis"), longitudDefecto);
                    var db = ctx.Db;

                    ObjectId idAl = ObjectId.Null, idEstilo = ObjectId.Null, idEtiquetas = ObjectId.Null, idCapa = ObjectId.Null;
                    string nombreAlReal = nombreAl, estiloUsado = estilo, conjuntoUsado = conjunto, capaPerfil = null, tipoLeido = null, vistaLeida = null;
                    double inicioEje = 0, finEje = 0, inicioReal = 0, finReal = 0, cotaMinReal = double.NaN, cotaMaxReal = double.NaN;
                    PlanRasante plan = null;
                    List<Dictionary<string, object>> pvisLeidos = null, entidadesLeidas = null, curvasLeidas = null;
                    List<(double progresiva, double cota)> puntosLeidos = null;
                    int tangentesLeidas = 0;
                    var esperado = new Dictionary<string, object> { ["existe"] = true };   // numero_pvis y numero_curvas se completan en Antes

                    Dictionary<string, object> EstadoPrevisto(bool real) => new Dictionary<string, object>
                    {
                        ["alineamiento"] = nombreAlReal,
                        ["nombre"] = nombre,
                        ["existe"] = true,
                        ["tipo"] = real ? tipoLeido : "FG",
                        ["estilo"] = estiloUsado,
                        ["conjunto_etiquetas"] = conjuntoUsado ?? "(sin etiquetas)",
                        ["progresiva_inicial"] = N(real ? inicioReal : plan.ProgresivaInicial),
                        ["progresiva_final"] = N(real ? finReal : plan.ProgresivaFinal),
                        ["cota_minima"] = N(real ? cotaMinReal : plan.CotaMinima),
                        ["cota_maxima"] = N(real ? cotaMaxReal : plan.CotaMaxima),
                        ["numero_pvis"] = real ? (pvisLeidos?.Count ?? 0) : plan.NumeroPvis,
                        ["numero_curvas"] = real ? (curvasLeidas?.Count ?? 0) : plan.NumeroCurvas,
                        ["tipo_curva"] = tipoCurva,
                        ["tramos"] = plan.TramosComoDiccionarios(),
                        ["curvas"] = plan.CurvasComoDiccionarios(),
                        ["capa"] = capaPerfil,
                        ["vista_perfil"] = vistaLeida
                    };

                    Escritura.Creacion creacion = null;
                    creacion = new Escritura.Creacion
                    {
                        // El texto definitivo (con las curvas que caben) se completa en Antes, cuando ya hay plan
                        Accion = "Crear la rasante '" + nombre + "' (perfil de diseño FG) del alineamiento '" + nombreAl + "' con " + pvisPedidos.Count + " PVI",
                        Antes = tr =>
                        {
                            idAl = BuscarAlineamiento(tr, nombreAl);          // "No existe el alineamiento 'X'. Usa listar_alineamientos."
                            var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                            nombreAlReal = al.Name;
                            inicioEje = al.StartingStation; finEje = al.EndingStation;
                            if (!BuscarPerfilONull(tr, al, nombre).IsNull)
                                throw new ArgumentException("Ya existe el perfil '" + nombre + "' en el alineamiento '" + al.Name + "'. Usa listar_perfiles o pasa otro 'nombre'.");
                            plan = Rasante.Planificar(pvisPedidos, inicioEje, finEje);   // orden, rango, pendientes, curvas que caben
                            creacion.Accion = Rasante.Accion(nombre, al.Name, plan, tipoCurva);
                            idEstilo = string.IsNullOrWhiteSpace(estilo) ? EstiloPerfilPorDefecto(tr, ctx.Avisos, out estiloUsado) : BuscarEstiloPerfilRasante(tr, estilo.Trim());
                            idEtiquetas = string.IsNullOrWhiteSpace(conjunto) ? ConjuntoEtiquetasPerfilPorDefecto(tr, ctx.Avisos, out conjuntoUsado) : BuscarConjuntoEtiquetasRasante(tr, conjunto.Trim());
                            if (estiloUsado == null) estiloUsado = NombreDeEstilo(tr, idEstilo);
                            if (conjuntoUsado == null && !idEtiquetas.IsNull) conjuntoUsado = NombreDeEstilo(tr, idEtiquetas);
                            idCapa = CapaParaPerfiles(tr, db, ctx.Avisos);
                            try { capaPerfil = ((LayerTableRecord)tr.GetObject(idCapa, OpenMode.ForRead)).Name; } catch { capaPerfil = null; }
                            if (!string.IsNullOrWhiteSpace(vistaPedida))
                            {
                                ObjectId idVista = BuscarVistaPerfilONull(tr, al, vistaPedida.Trim());
                                if (idVista.IsNull)
                                    throw new ArgumentException("No existe la vista de perfil '" + vistaPedida.Trim() + "' en el alineamiento '" + al.Name + "'. Las vistas se ven en Prospector → Alineaciones → el eje → Vistas en perfil, o créala con crear_perfil_terreno_y_vista. No hace falta pasar 'vista_perfil': el perfil se dibuja en las vistas del eje según su configuración.");
                                vistaLeida = ((Civ.ProfileView)tr.GetObject(idVista, OpenMode.ForRead)).Name;
                                ctx.Avisos.Add("La vista de perfil '" + vistaLeida + "' existe; la rasante se dibujará en ella (y en las demás vistas del eje) según la configuración de cada vista.");
                            }
                            esperado["numero_pvis"] = plan.NumeroPvis;
                            esperado["numero_curvas"] = plan.NumeroCurvas;
                            return new Dictionary<string, object> { ["alineamiento"] = al.Name, ["nombre"] = nombre, ["existe"] = false, ["numero_pvis"] = 0, ["numero_curvas"] = 0 };
                        },
                        Prevision = tr =>
                        {
                            ctx.Avisos.AddRange(plan.Avisos);
                            if (tipoCurva == Rasante.Circular && plan.NumeroCurvas > 0)
                                ctx.Avisos.Add("Las cotas de inicio y fin de cada curva y las cotas extremas se calculan como parábola; con curvas circulares la diferencia es de milímetros.");
                            return EstadoPrevisto(false);
                        },
                        Esperado = esperado,
                        Crear = tr =>
                        {
                            ObjectId id = CivProfile.CreateByLayout(nombre, idAl, idCapa, idEstilo, idEtiquetas);
                            if (id.IsNull) return id;
                            var pr = (CivProfile)tr.GetObject(id, OpenMode.ForWrite);
                            var pvisApi = new List<Civ.ProfilePVI>();
                            foreach (var p in plan.Pvis)
                            {
                                try { pvisApi.Add(pr.PVIs.AddPVI(p.Progresiva, p.Cota)); }
                                catch (Exception ex) { throw new InvalidOperationException("No se pudo añadir el PVI " + p.Numero + " (progresiva " + Math.Round(p.Progresiva, 4) + ", cota " + Math.Round(p.Cota, 4) + "): " + ex.Message + ". Nada queda escrito (la transacción se descarta) y no se reintenta.", ex); }
                            }
                            for (int i = 0; i < plan.Pvis.Count; i++)
                            {
                                var p = plan.Pvis[i];
                                if (!p.LongitudAplicada.HasValue) continue;
                                try
                                {
                                    if (tipoCurva == Rasante.Circular) pr.Entities.AddFreeCircularCurveByPVIAndLength(pvisApi[i], p.LongitudAplicada.Value);
                                    else pr.Entities.AddFreeSymmetricParabolaByPVIAndCurveLength(pvisApi[i], p.LongitudAplicada.Value);
                                }
                                catch (Exception ex) { throw new InvalidOperationException("No se pudo crear la curva vertical del PVI " + p.Numero + " (progresiva " + Math.Round(p.Progresiva, 4) + ", " + Rasante.TextoTipoCurva(tipoCurva) + " de " + Math.Round(p.LongitudAplicada.Value, 4) + " m, A = " + Math.Round(p.CambioPendiente ?? 0, 4) + " %): " + ex.Message + ". Nada queda escrito (la transacción se descarta) y no se reintenta; prueba con una longitud menor o con \"longitud_curva\": 0 en ese PVI.", ex); }
                            }
                            return id;
                        },
                        Despues = (tr, id) =>
                        {
                            var pr = (CivProfile)tr.GetObject(id, OpenMode.ForRead);
                            inicioReal = pr.StartingStation; finReal = pr.EndingStation;
                            try { cotaMinReal = pr.ElevationMin; cotaMaxReal = pr.ElevationMax; } catch { }
                            try { tipoLeido = pr.ProfileType.ToString(); } catch { tipoLeido = null; }
                            estiloUsado = Api.Leer<string>(pr, estiloUsado, "StyleName") ?? estiloUsado;
                            try { capaPerfil = pr.Layer ?? capaPerfil; } catch { }
                            try { pvisLeidos = PvisDelPerfil(pr, out puntosLeidos); } catch (Exception ex) { pvisLeidos = null; puntosLeidos = null; ctx.Avisos.Add("No se pudieron releer los PVI del perfil (" + ex.GetType().Name + ": " + ex.Message + ")."); }
                            try { entidadesLeidas = EntidadesDelPerfil(pr, out tangentesLeidas, out curvasLeidas); } catch (Exception ex) { entidadesLeidas = null; curvasLeidas = null; ctx.Avisos.Add("No se pudieron releer las entidades del perfil (" + ex.GetType().Name + ": " + ex.Message + ")."); }
                            var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                            var d = EstadoPrevisto(true);
                            d["existe"] = !BuscarPerfilONull(tr, al, nombre).IsNull;
                            if (curvasLeidas == null) d["numero_curvas"] = null;
                            if (pvisLeidos == null) d["numero_pvis"] = null;
                            return d;
                        },
                        Verificar = d =>
                        {
                            string cola = " No se reintenta; deshaz con Ctrl+Z si no quieres el perfil. despues=" + Nucleo.Json.Serializar(d);
                            string motivo = Rasante.MotivoPvisNoCoinciden(plan.Pvis, puntosLeidos);
                            if (motivo != null) throw new InvalidOperationException("La rasante '" + nombre + "' se creó pero " + motivo + "." + cola);
                            motivo = Rasante.MotivoRangoNoCoincide(plan.ProgresivaInicial, plan.ProgresivaFinal, inicioReal, finReal);
                            if (motivo != null) throw new InvalidOperationException("La rasante '" + nombre + "' se creó pero " + motivo + "." + cola);
                            if (tipoLeido != null && !string.Equals(tipoLeido, "FG", StringComparison.OrdinalIgnoreCase))
                                ctx.Avisos.Add("El perfil se creó con tipo '" + tipoLeido + "' en vez de 'FG'; revisa con listar_perfiles.");
                            var previstas = plan.Pvis.Where(p => p.LongitudAplicada.HasValue).ToList();
                            if (curvasLeidas != null && curvasLeidas.Count == previstas.Count)
                            {
                                for (int i = 0; i < previstas.Count; i++)
                                {
                                    double? lLeida = curvasLeidas[i]["longitud"] as double?;
                                    if (lLeida.HasValue && Math.Abs(lLeida.Value - previstas[i].LongitudAplicada.Value) > 0.01)
                                        ctx.Avisos.Add("La curva del PVI " + previstas[i].Numero + " quedó con longitud " + lLeida.Value + " y se pidió " + Math.Round(previstas[i].LongitudAplicada.Value, 4) + ".");
                                    double? kLeida = curvasLeidas[i].TryGetValue("k", out object ko) ? ko as double? : null;
                                    if (kLeida.HasValue && previstas[i].K.HasValue && Math.Abs(kLeida.Value - previstas[i].K.Value) > 0.05)
                                        ctx.Avisos.Add("K leída de la API en el PVI " + previstas[i].Numero + ": " + kLeida.Value + " frente a " + Math.Round(previstas[i].K.Value, 4) + " calculada (L/|A| con A en %); anótalo en miembros_por_verificar_civil3d.md.");
                                }
                            }
                            if (entidadesLeidas != null && tangentesLeidas != plan.Tramos.Count)
                                ctx.Avisos.Add("El perfil tiene " + tangentesLeidas + " tangentes y la rasante pedida tiene " + plan.Tramos.Count + " tramos; revisa 'entidades' en 'datos'.");
                            if (!double.IsNaN(cotaMinReal) && (Math.Abs(cotaMinReal - plan.CotaMinima) > 0.01 || Math.Abs(cotaMaxReal - plan.CotaMaxima) > 0.01))
                                ctx.Avisos.Add("Las cotas extremas leídas (" + Math.Round(cotaMinReal, 4) + " a " + Math.Round(cotaMaxReal, 4) + ") difieren de las previstas (" + Math.Round(plan.CotaMinima, 4) + " a " + Math.Round(plan.CotaMaxima, 4) + "); anótalo: puede que ElevationMin/Max usen los PVI y no los vértices de las curvas.");
                            // Unidad de las pendientes de la API (tanto por uno o porcentaje), para la memoria técnica
                            if (entidadesLeidas != null)
                            {
                                var primera = entidadesLeidas.FirstOrDefault(e => e.ContainsKey("pendiente_api") && e["pendiente_api"] is double);
                                if (primera != null && plan.Tramos.Count > 0)
                                {
                                    string unidad = Rasante.UnidadPendiente(plan.Tramos[0].PendientePct, (double)primera["pendiente_api"]);
                                    ctx.Avisos.Add(unidad == null
                                        ? "La pendiente de la primera tangente leída de la API (" + primera["pendiente_api"] + ") no coincide con la prevista (" + Math.Round(plan.Tramos[0].PendientePct, 4) + " %) ni en tanto por uno ni en porcentaje; anótalo."
                                        : "Las pendientes de la API (ProfileTangent.Grade, ProfilePVI.GradeIn/GradeOut) vienen en " + (unidad == "porcentaje" ? "porcentaje" : "tanto por uno (0,025 = 2,5 %)") + ".");
                                }
                            }
                        },
                        Extra = () => new
                        {
                            nombre,
                            alineamiento = nombreAlReal,
                            tipo = tipoLeido,
                            estilo = estiloUsado,
                            conjunto_etiquetas = conjuntoUsado ?? "(sin etiquetas)",
                            capa = capaPerfil,
                            vista_perfil = vistaLeida,
                            tipo_curva = tipoCurva,
                            longitud_curva_por_defecto = longitudDefecto,
                            progresiva_inicial = N(inicioReal),
                            progresiva_final = N(finReal),
                            longitud = N(finReal - inicioReal),
                            cota_minima = N(cotaMinReal),
                            cota_maxima = N(cotaMaxReal),
                            cota_minima_prevista = N(plan.CotaMinima),
                            cota_maxima_prevista = N(plan.CotaMaxima),
                            numero_pvis = pvisLeidos?.Count,
                            numero_curvas = curvasLeidas?.Count,
                            tramos = plan.TramosComoDiccionarios(),
                            curvas = plan.CurvasComoDiccionarios(),
                            pvis_creados = pvisLeidos,
                            entidades = entidadesLeidas,
                            progresiva_inicial_eje = N(inicioEje),
                            progresiva_final_eje = N(finEje)
                        }
                    };
                    return Escritura.CrearObjeto(ctx, creacion);
                })
            });
        }
    }
}

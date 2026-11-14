using System;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.Settings;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivProfile = Autodesk.Civil.DatabaseServices.Profile;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// Creación de corredores. Desde 1.11.0: crear_corredor, la quinta herramienta de creación del flujo vial (tras
    /// crear_superficie_tin, crear_alineamiento_desde_polilinea, crear_perfil_terreno_y_vista y crear_rasante), por el
    /// camino común Escritura.CrearObjeto. Vía de creación: CorridorCollection.Add(nombre, línea base, eje, rasante,
    /// región, ensamblaje) (confirmada en el catálogo, devuelve el ObjectId del corredor), ajuste del rango de la región
    /// (BaselineRegion.StartStation/EndStation; si la línea base quedara sin región, BaselineRegionCollection.Add con el
    /// rango), las cuatro frecuencias de AppliedAssemblySetting (como establecer_frecuencia), la superficie de terreno en
    /// todos los objetivos de tipo superficie (como asignar_objetivos_superficie, guardando lo anterior en Restauraciones)
    /// y Corridor.Rebuild(), todo en una sola transacción y una sola entrada de Deshacer. Lo que no depende de Civil 3D
    /// (rango común, frecuencia, superficie por defecto, comprobaciones) está en ArbaMcp.Nucleo.Corredor. Las superficies
    /// de corredor (Top, Datum) no se crean aquí.
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaCrearCorredor = "crear_corredor";

        // ------------------------------------------------------------------ búsquedas
        private static ObjectId BuscarCorredorONull(Transaction tr, string nombre)
        {
            foreach (ObjectId id in CivilApplication.ActiveDocument.CorridorCollection)
                if (tr.GetObject(id, OpenMode.ForRead) is Civ.Corridor cor && string.Equals(cor.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return id;
            return ObjectId.Null;
        }

        /// <summary>Busca el ensamblaje; si no está, el error de BuscarEnsamblaje (con la lista del dibujo) más la herramienta de lectura y cómo crearlo.</summary>
        private static ObjectId BuscarEnsamblajeParaCorredor(Transaction tr, string nombre)
        {
            try { return BuscarEnsamblaje(tr, nombre); }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ex.Message + " Usa listar_ensamblajes para ver sus subensamblajes. Si no hay ninguno, créalo en Civil 3D (Inicio → Ensamblaje → Crear ensamblaje y los subensamblajes de la paleta de herramientas).");
            }
        }

        /// <summary>Superficies generadas por corredores (por id y por nombre) → nombre del corredor, como las marca listar_superficies.</summary>
        private static Dictionary<ObjectId, string> SuperficiesDeCorredores(Transaction tr, out Dictionary<string, string> porNombre)
        {
            porNombre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var porId = new Dictionary<ObjectId, string>();
            foreach (ObjectId idCor in CivilApplication.ActiveDocument.CorridorCollection)
            {
                if (!(tr.GetObject(idCor, OpenMode.ForRead) is Civ.Corridor cor)) continue;
                try
                {
                    foreach (Civ.CorridorSurface cs in cor.CorridorSurfaces)
                    {
                        porNombre[cs.Name] = cor.Name;
                        var idSu = Api.Leer<ObjectId>(cs, ObjectId.Null, "SurfaceId");
                        if (!idSu.IsNull) porId[idSu] = cor.Name;
                    }
                }
                catch (Exception ex) { Historial.Registrar(HerramientaCrearCorredor + ": no se pudieron leer las superficies del corredor " + cor.Name + ": " + ex.Message); }
            }
            return porId;
        }

        /// <summary>Subensamblajes del ensamblaje (grupo, nombre y lado), para la respuesta y los avisos.</summary>
        private static List<Dictionary<string, object>> SubensamblajesDe(Transaction tr, Civ.Assembly asm)
        {
            var lista = new List<Dictionary<string, object>>();
            try
            {
                foreach (Civ.AssemblyGroup g in asm.Groups)
                    foreach (ObjectId idSub in g.GetSubassemblyIds())
                        if (tr.GetObject(idSub, OpenMode.ForRead) is Civ.Subassembly sub)
                            lista.Add(new Dictionary<string, object> { ["grupo"] = g.Name, ["nombre"] = sub.Name, ["lado"] = Lado(sub.Side) });
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearCorredor + ": no se pudieron leer los subensamblajes de " + asm.Name + ": " + ex.Message); }
            return lista;
        }

        /// <summary>Primera línea base del corredor y su primera región (las que crea CorridorCollection.Add); null si no hay.</summary>
        private static Civ.Baseline PrimeraLineaBase(Civ.Corridor cor, out Civ.BaselineRegion region)
        {
            region = null;
            foreach (Civ.Baseline bl in cor.Baselines)
            {
                foreach (Civ.BaselineRegion r in bl.BaselineRegions) { region = r; break; }
                return bl;
            }
            return null;
        }

        private static int NumeroObjetivosSuperficie(Civ.SubassemblyTargetInfoCollection col)
        {
            int n = 0;
            foreach (Civ.SubassemblyTargetInfo info in col) if (TipoObjetivo(info) == "superficie") n++;
            return n;
        }

        private static string DondeObjetivo(Civ.SubassemblyTargetInfo info) => info.SubassemblyName + " [" + (string.IsNullOrEmpty(info.LogicalName) ? "superficie" : info.LogicalName) + "]";

        /// <summary>
        /// Capa del corredor nuevo (desde 1.14.1): la capa de objetos "Corredor" de la configuración del dibujo
        /// (ObjectLayerSettings.GetObjectLayerSetting(Corridor), el valor del enumerado resuelto por nombre), como el eje y
        /// el perfil. CorridorCollection.Add no recibe capa, así que la herramienta la pone después con Entity.LayerId. Si
        /// no se puede leer o la capa no existe en el dibujo, devuelve ObjectId.Null con aviso y el corredor se queda en la
        /// capa que le pone Civil 3D (no la capa actual, a diferencia del eje y el perfil). El modificador de la capa
        /// (Modifier/ModifierValue, prefijo o sufijo con el nombre del objeto) no se aplica, igual que en el eje y el perfil.
        /// </summary>
        private static ObjectId CapaParaCorredores(Transaction tr, Database db, List<string> avisos, out string nombre)
        {
            nombre = null;
            try
            {
                var ajuste = CivilApplication.ActiveDocument.Settings.DrawingSettings.ObjectLayerSettings.GetObjectLayerSetting(ValorEnum<SettingsObjectLayerType>("Corridor"));
                ObjectId idCapa = ajuste.LayerId;
                string nombreCapa = ajuste.LayerName;
                if (idCapa.IsNull)
                {
                    var capas = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (string.IsNullOrWhiteSpace(nombreCapa) || !capas.Has(nombreCapa))
                    {
                        avisos.Add(Corredor.AvisoCapaNoEncontrada(nombreCapa));
                        return ObjectId.Null;
                    }
                    idCapa = capas[nombreCapa];
                }
                try { nombre = ((LayerTableRecord)tr.GetObject(idCapa, OpenMode.ForRead)).Name; } catch { nombre = nombreCapa; }
                return idCapa;
            }
            catch (Exception ex)
            {
                avisos.Add(Corredor.AvisoCapaNoEncontrada(null, ex.Message));
                return ObjectId.Null;
            }
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarCrearCorredor()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaCrearCorredor,
                Descripcion = "Crea un corredor nuevo con una línea base (un alineamiento y su rasante) y una región con un ensamblaje que ya existe en el dibujo, entre dos progresivas (por defecto el tramo común del eje y la rasante) y con una frecuencia dada (la misma en tangentes, curvas, espirales y curvas del perfil). Pone la superficie de terreno en todos los objetivos de tipo superficie de la región (los taludes, como asignar_objetivos_superficie) y reconstruye el corredor, todo en una sola entrada de Deshacer. Vía: CorridorCollection.Add(nombre, línea base, eje, rasante, región, ensamblaje), ajuste del rango de la región, AppliedAssemblySetting, GetTargets/SetTargets y Corridor.Rebuild(). Relee el corredor y comprueba línea base, eje, rasante, ensamblaje, rango (±0,01), frecuencias y que cada objetivo de superficie tiene la superficie pedida (si no, error sin reintentar). Desde 1.14.1 pone el corredor en la capa de corredores de la configuración del dibujo (Entity.LayerId después de Add; si no existe o no se puede poner, aviso y se queda en la que le pone Civil 3D). Devuelve 'estado_corredor' (el mismo bloque que la herramienta estado_corredor), la capa, los objetivos asignados, los subensamblajes del ensamblaje y 'restaurar': Civil 3D no deshace objetivos con Ctrl+Z, así que deshacer_objetivos los deja como estaban (sin objetivo). Con simular=true solo comprueba e informa (sin copia ni cambios). Si ya existe un corredor con ese nombre, error sin tocar nada. No crea superficies de corredor (Top, Datum).",
                Parametros =
                {
                    P("nombre", "string", "Nombre del corredor nuevo (no debe existir; ver listar_corredores)", true),
                    P("alineamiento", "string", "Nombre del alineamiento (eje) de la línea base", true),
                    P("perfil", "string", "Nombre de la rasante (perfil de diseño) de ese alineamiento; ver listar_perfiles", true),
                    P("ensamblaje", "string", "Nombre de un ensamblaje que ya existe en el dibujo; ver listar_ensamblajes", true),
                    P("frecuencia", "number", "Metros entre secciones del corredor (mayor que 0); se aplica a tangentes, curvas, espirales y curvas del perfil (cámbialas después con establecer_frecuencia si hace falta)", true),
                    P("inicio", "number", "Progresiva inicial de la región. Si falta, el inicio del tramo común del eje y la rasante (hasta 0,01 fuera se ajusta con aviso)"),
                    P("fin", "number", "Progresiva final de la región. Si falta, el final del tramo común del eje y la rasante (hasta 0,01 fuera se ajusta con aviso)"),
                    P("superficie_objetivo", "string", "Superficie de terreno para los objetivos de superficie (taludes). Si falta: la única superficie del dibujo que no sea de un corredor; con varias, error con la lista; sin ninguna, el corredor se crea sin objetivos. \"ninguna\" = sin objetivos"),
                    P("linea_base", "string", "Nombre de la línea base (por defecto \"" + Corredor.LineaBasePorDefecto + "\")"),
                    P("region", "string", "Nombre de la región (por defecto \"" + Corredor.RegionPorDefecto + "\")"),
                    P("simular", "boolean", "Con true comprueba nombre libre, eje, rasante, ensamblaje, superficie, rango y frecuencia y devuelve el corredor previsto sin tocar el dibujo (ni copia)")
                },
                Ejecutar = a => Escritura.Ejecutar(HerramientaCrearCorredor, a, ctx =>
                {
                    string nombre = Requerido(a, "nombre").Trim();
                    string nombreAl = Requerido(a, "alineamiento").Trim();
                    string nombrePerfil = Requerido(a, "perfil").Trim();
                    string nombreEns = Requerido(a, "ensamblaje").Trim();
                    double frecuencia = Corredor.ValidarFrecuencia(Tiene(a, "frecuencia") ? Num(a, "frecuencia", double.NaN) : (double?)null);
                    double? inicioPedido = Tiene(a, "inicio") ? Num(a, "inicio", double.NaN) : (double?)null;
                    double? finPedido = Tiene(a, "fin") ? Num(a, "fin", double.NaN) : (double?)null;
                    string superficiePedida = Str(a, "superficie_objetivo");
                    string nombreLb = Corredor.NombreLineaBase(Str(a, "linea_base"));
                    string nombreRegion = Corredor.NombreRegion(Str(a, "region"));
                    var frecuencias = Corredor.Frecuencias(frecuencia);

                    ObjectId idAl = ObjectId.Null, idPerfil = ObjectId.Null, idEns = ObjectId.Null, idSu = ObjectId.Null, idCapa = ObjectId.Null;
                    string nombreAlReal = nombreAl, nombrePerfilReal = nombrePerfil, nombreEnsReal = nombreEns, nombreSuperficie = null, tipoPerfil = null, capaPrevista = null;
                    RangoCorredor rango = null;
                    List<Dictionary<string, object>> subensamblajes = null;

                    // Lo que deja la creación (para la respuesta y la memoria técnica)
                    var guardados = new List<ObjetivoGuardado>();
                    EntradaRestauracion entrada = null;
                    string caminoObjetivos = null;
                    double? inicioRegionAdd = null, finRegionAdd = null;
                    bool regionAnadidaAparte = false, capaNoAplicada = false;
                    int reconstrucciones = 0;
                    long msReconstruccion = 0;

                    // Lo releído
                    string lbLeida = null, regionLeida = null, capa = null;
                    double inicioLeido = double.NaN, finLeido = double.NaN;
                    var objetivosLeidos = new List<(string donde, IList<string> objetivos)>();
                    List<object> objetivosInfo = null;
                    object estadoCorredor = null;
                    Dictionary<string, object> frecuenciasLeidas = null;

                    var esperado = new Dictionary<string, object> { ["existe"] = true, ["numero_lineas_base"] = 1, ["numero_regiones"] = 1 };
                    foreach (var kv in frecuencias) esperado[kv.Key] = N(kv.Value);

                    Dictionary<string, object> EstadoPrevisto()
                    {
                        var d = new Dictionary<string, object>
                        {
                            ["nombre"] = nombre,
                            ["existe"] = true,
                            ["numero_lineas_base"] = 1,
                            ["linea_base"] = nombreLb,
                            ["alineamiento"] = nombreAlReal,
                            ["perfil"] = nombrePerfilReal,
                            ["numero_regiones"] = 1,
                            ["region"] = nombreRegion,
                            ["ensamblaje"] = nombreEnsReal,
                            ["inicio"] = N(rango.Inicio),
                            ["fin"] = N(rango.Fin)
                        };
                        foreach (var kv in frecuencias) d[kv.Key] = N(kv.Value);
                        d["superficie_objetivo"] = nombreSuperficie;
                        d["objetivos_superficie"] = null;   // se conocen al crear la región
                        d["esta_desactualizado"] = false;
                        d["capa"] = capaPrevista;           // null: no se encontró la de la configuración (aviso) y la pone Civil 3D
                        return d;
                    }

                    void Reconstruir(Civ.Corridor cor)
                    {
                        var reloj = System.Diagnostics.Stopwatch.StartNew();
                        try { cor.Rebuild(); }
                        catch (Exception ex) { throw new InvalidOperationException("No se pudo reconstruir el corredor '" + nombre + "' (Corridor.Rebuild: " + ex.Message + "). Nada queda escrito (la transacción se descarta) y no se reintenta.", ex); }
                        msReconstruccion += reloj.ElapsedMilliseconds;
                        reconstrucciones++;
                    }

                    Escritura.Creacion creacion = null;
                    creacion = new Escritura.Creacion
                    {
                        // El texto definitivo (rango y superficie) se completa en Antes
                        Accion = "Crear el corredor '" + nombre + "' sobre el eje '" + nombreAl + "' y la rasante '" + nombrePerfil + "' con el ensamblaje '" + nombreEns + "'",
                        Antes = tr =>
                        {
                            if (!BuscarCorredorONull(tr, nombre).IsNull)
                                throw new ArgumentException("Ya existe el corredor '" + nombre + "'. Usa listar_corredores o pasa otro 'nombre'.");
                            idAl = BuscarAlineamiento(tr, nombreAl);               // "No existe el alineamiento 'X'. Usa listar_alineamientos."
                            var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                            nombreAlReal = al.Name;
                            idPerfil = BuscarPerfil(tr, idAl, nombrePerfil);       // "... no tiene un perfil llamado 'X'. Usa listar_perfiles."
                            var pr = (CivProfile)tr.GetObject(idPerfil, OpenMode.ForRead);
                            nombrePerfilReal = pr.Name;
                            try { tipoPerfil = pr.ProfileType.ToString(); } catch { tipoPerfil = null; }
                            if (tipoPerfil != null && !string.Equals(tipoPerfil, "FG", StringComparison.OrdinalIgnoreCase))
                                ctx.Avisos.Add("El perfil '" + pr.Name + "' es de tipo '" + tipoPerfil + "', no una rasante (FG): un corredor suele ir sobre la rasante. Crea una con crear_rasante si hace falta.");
                            idEns = BuscarEnsamblajeParaCorredor(tr, nombreEns);
                            var asm = (Civ.Assembly)tr.GetObject(idEns, OpenMode.ForRead);
                            nombreEnsReal = asm.Name;
                            subensamblajes = SubensamblajesDe(tr, asm);
                            if (subensamblajes.Count == 0)
                                ctx.Avisos.Add("El ensamblaje '" + asm.Name + "' no tiene subensamblajes legibles: el corredor no tendrá secciones. Revísalo con listar_ensamblajes.");

                            rango = Corredor.Rango(al.StartingStation, al.EndingStation, pr.StartingStation, pr.EndingStation, inicioPedido, finPedido, al.Name, pr.Name);
                            ctx.Avisos.AddRange(rango.Avisos);
                            string avisoFrecuencia = Corredor.AvisoFrecuencia(frecuencia, rango.Longitud);
                            if (avisoFrecuencia != null) ctx.Avisos.Add(avisoFrecuencia);

                            // Superficie objetivo: la pedida, o la única de terreno del dibujo
                            var deCorredorPorId = SuperficiesDeCorredores(tr, out var deCorredorPorNombre);
                            var terrenos = new List<string>();
                            foreach (ObjectId idS in CivilApplication.ActiveDocument.GetSurfaceIds())
                            {
                                if (!(tr.GetObject(idS, OpenMode.ForRead) is CivSurface su)) continue;
                                if (deCorredorPorId.ContainsKey(idS) || deCorredorPorNombre.ContainsKey(su.Name)) continue;
                                terrenos.Add(su.Name);
                            }
                            string elegida = Corredor.ElegirSuperficie(superficiePedida, terrenos, ctx.Avisos);
                            if (elegida != null)
                            {
                                idSu = BuscarSuperficie(tr, elegida);              // "No existe la superficie 'X'. Usa listar_superficies."
                                nombreSuperficie = NombreDe(tr, idSu) ?? elegida;
                                if (deCorredorPorId.TryGetValue(idSu, out string otro) || deCorredorPorNombre.TryGetValue(nombreSuperficie, out otro))
                                    ctx.Avisos.Add("La superficie '" + nombreSuperficie + "' la genera el corredor '" + otro + "': los taludes suelen ir al terreno. Revisa con listar_superficies si es la que quieres.");
                            }
                            // Capa: la de corredores de la configuración del dibujo (si no, aviso y la que ponga Civil 3D)
                            idCapa = CapaParaCorredores(tr, ctx.Db, ctx.Avisos, out capaPrevista);
                            creacion.Accion = Corredor.Accion(nombre, al.Name, pr.Name, asm.Name, rango, frecuencia, nombreSuperficie);
                            return new Dictionary<string, object> { ["nombre"] = nombre, ["existe"] = false, ["numero_lineas_base"] = 0, ["numero_regiones"] = 0 };
                        },
                        Prevision = tr =>
                        {
                            esperado["alineamiento"] = nombreAlReal;
                            esperado["perfil"] = nombrePerfilReal;
                            esperado["ensamblaje"] = nombreEnsReal;
                            if (ctx.Simular && nombreSuperficie != null)
                                ctx.Avisos.Add("Cuántos objetivos de superficie tiene el ensamblaje (uno por talud) se sabe al crear la región: la simulación no los puede contar ('objetivos_superficie' = null).");
                            return EstadoPrevisto();
                        },
                        Esperado = esperado,
                        Crear = tr =>
                        {
                            ObjectId id = CivilApplication.ActiveDocument.CorridorCollection.Add(nombre, nombreLb, idAl, idPerfil, nombreRegion, idEns);
                            if (id.IsNull) return id;
                            var cor = (Civ.Corridor)tr.GetObject(id, OpenMode.ForWrite);

                            // Capa (1.14.1): Add no la recibe. Si la API no deja ponerla, aviso y sigue: una capa no debe impedir crear el corredor
                            if (!idCapa.IsNull)
                            {
                                try { cor.LayerId = idCapa; }
                                catch (Exception ex) { capaNoAplicada = true; ctx.Avisos.Add(Corredor.AvisoCapaNoAplicada(capaPrevista, ex.Message)); }
                            }

                            var bl = PrimeraLineaBase(cor, out var reg);
                            if (bl == null)
                                throw new InvalidOperationException("CorridorCollection.Add creó el corredor '" + nombre + "' sin línea base. Nada queda escrito (la transacción se descarta) y no se reintenta; anótalo en miembros_por_verificar_civil3d.md.");

                            // Región con el rango pedido
                            if (reg == null)
                            {
                                reg = bl.BaselineRegions.Add(nombreRegion, idEns, rango.Inicio, rango.Fin);
                                regionAnadidaAparte = true;
                                ctx.Avisos.Add("CorridorCollection.Add no dejó ninguna región en la línea base; se añadió con BaselineRegionCollection.Add (anótalo para la memoria técnica).");
                            }
                            else
                            {
                                inicioRegionAdd = reg.StartStation; finRegionAdd = reg.EndStation;
                                if (Math.Abs(reg.StartStation - rango.Inicio) > 1e-6 || Math.Abs(reg.EndStation - rango.Fin) > 1e-6)
                                {
                                    // Orden que evita rangos invertidos temporales (como establecer_rango_region)
                                    if (rango.Inicio >= reg.EndStation) { reg.EndStation = rango.Fin; reg.StartStation = rango.Inicio; }
                                    else { reg.StartStation = rango.Inicio; reg.EndStation = rango.Fin; }
                                }
                            }

                            // Frecuencias (las mismas cuatro que establecer_frecuencia)
                            if (reg.AppliedAssemblySetting == null)
                                throw new InvalidOperationException("La región nueva no expone AppliedAssemblySetting: no se pueden poner las frecuencias. Nada queda escrito (la transacción se descarta) y no se reintenta.");
                            AplicarFrecuencias(reg, frecuencias);

                            // Objetivos de superficie (la lógica de asignar_objetivos_superficie)
                            if (!idSu.IsNull)
                            {
                                ComprobarNoSuperficieDelCorredor(tr, cor, "superficie", new ObjectIdCollection { idSu });   // un corredor nuevo aún no tiene superficies; por si Civil 3D las creara solo
                                var col = reg.GetTargets();
                                if (NumeroObjetivosSuperficie(col) > 0) caminoObjetivos = "antes_de_reconstruir";
                                else
                                {
                                    // Puede que Civil 3D no prepare los objetivos hasta la primera reconstrucción
                                    Reconstruir(cor);
                                    bl = PrimeraLineaBase(cor, out reg);
                                    if (bl == null || reg == null)
                                        throw new InvalidOperationException("Tras la primera reconstrucción el corredor '" + nombre + "' quedó sin línea base o sin región. Nada queda escrito (la transacción se descarta) y no se reintenta.");
                                    col = reg.GetTargets();
                                    caminoObjetivos = NumeroObjetivosSuperficie(col) > 0 ? "despues_de_reconstruir" : "sin_objetivos";
                                }
                                if (caminoObjetivos == "sin_objetivos")
                                    ctx.Avisos.Add("El ensamblaje '" + nombreEnsReal + "' no tiene objetivos de superficie (ni antes ni después de reconstruir): el corredor se creó sin objetivos. Los taludes (BasicSideSlopeCutDitch, DaylightGeneral...) los tienen; revisa el ensamblaje con listar_ensamblajes.");
                                else
                                {
                                    if (caminoObjetivos == "despues_de_reconstruir")
                                        ctx.Avisos.Add("Los objetivos de superficie no aparecieron hasta la primera reconstrucción: se reconstruyó, se asignaron y se reconstruyó otra vez (anótalo para la memoria técnica).");
                                    var tocados = new List<(ObjetivoGuardado g, Civ.SubassemblyTargetInfo info)>();
                                    foreach (Civ.SubassemblyTargetInfo info in col)
                                        if (TipoObjetivo(info) == "superficie")
                                        {
                                            var g = GuardarObjetivo(tr, bl, reg, info);
                                            info.TargetIds = new ObjectIdCollection { idSu };
                                            tocados.Add((g, info));
                                        }
                                    reg.SetTargets(col);
                                    foreach (var (g, info) in tocados) guardados.Add(CerrarObjetivoGuardado(tr, g, info));
                                }
                            }
                            else caminoObjetivos = "sin_superficie";

                            Reconstruir(cor);
                            return id;
                        },
                        AlConfirmar = () =>
                        {
                            lock (UltimaReconstruccion) UltimaReconstruccion[nombre] = DateTime.Now;
                            entrada = Restauraciones.Guardar(ClaveDibujo(ctx.Doc), HerramientaCrearCorredor, nombre, guardados);
                        },
                        Despues = (tr, id) =>
                        {
                            var cor = (Civ.Corridor)tr.GetObject(id, OpenMode.ForRead);
                            var bl = PrimeraLineaBase(cor, out var reg);
                            var d = new Dictionary<string, object>
                            {
                                ["nombre"] = cor.Name,
                                ["existe"] = !BuscarCorredorONull(tr, nombre).IsNull,
                                ["numero_lineas_base"] = cor.Baselines.Count,
                                ["linea_base"] = lbLeida = bl?.Name,
                                ["alineamiento"] = bl == null ? null : NombreDe(tr, Seguro(() => bl.AlignmentId, ObjectId.Null)),
                                ["perfil"] = bl == null ? null : NombreDe(tr, Seguro(() => bl.ProfileId, ObjectId.Null)),
                                ["numero_regiones"] = bl?.BaselineRegions.Count ?? 0,
                                ["region"] = regionLeida = reg?.Name,
                                ["ensamblaje"] = reg == null ? null : NombreDe(tr, reg.AssemblyId)
                            };
                            if (reg != null)
                            {
                                inicioLeido = reg.StartStation; finLeido = reg.EndStation;
                                d["inicio"] = N(inicioLeido);
                                d["fin"] = N(finLeido);
                                frecuenciasLeidas = EstadoFrecuencias(reg);
                                foreach (var k in Corredor.ClavesFrecuencia) d[k] = frecuenciasLeidas[k];
                                objetivosInfo = new List<object>();
                                int nSup = 0;
                                foreach (Civ.SubassemblyTargetInfo info in reg.GetTargets())
                                {
                                    objetivosInfo.Add(InfoObjetivo(tr, info, reg.AssemblyId));
                                    if (TipoObjetivo(info) != "superficie") continue;
                                    nSup++;
                                    objetivosLeidos.Add((DondeObjetivo(info), IdsObjetivo(info).Select(x => NombreDe(tr, x)).ToList()));
                                }
                                d["objetivos_superficie"] = nSup;
                            }
                            d["superficie_objetivo"] = nombreSuperficie;
                            d["esta_desactualizado"] = Seguro(() => (bool?)cor.IsOutOfDate, null);
                            try { capa = cor.Layer; } catch { capa = null; }
                            d["capa"] = capa;
                            estadoCorredor = EstadoCorredorInfo(tr, cor);
                            return d;
                        },
                        Verificar = d =>
                        {
                            string cola = " No se reintenta; deshaz con Ctrl+Z si no quieres el corredor (y deshacer_objetivos para los objetivos). despues=" + Nucleo.Json.Serializar(d);
                            if (double.IsNaN(inicioLeido))
                                throw new InvalidOperationException("El corredor '" + nombre + "' se creó pero su línea base no tiene región." + cola);
                            string motivo = Corredor.MotivoRangoNoCoincide(rango.Inicio, rango.Fin, inicioLeido, finLeido);
                            if (motivo != null) throw new InvalidOperationException("El corredor '" + nombre + "' se creó pero " + motivo + "." + cola);
                            motivo = Corredor.MotivoObjetivosNoCoinciden(objetivosLeidos, caminoObjetivos == "sin_superficie" ? null : nombreSuperficie);
                            if (motivo != null) throw new InvalidOperationException("El corredor '" + nombre + "' se creó pero " + motivo + "." + cola);
                            if (!string.Equals(lbLeida, nombreLb, StringComparison.OrdinalIgnoreCase))
                                ctx.Avisos.Add("La línea base quedó con el nombre '" + lbLeida + "' (se pidió '" + nombreLb + "'): úsalo en listar_regiones y asignar_objetivo.");
                            if (!string.Equals(regionLeida, nombreRegion, StringComparison.OrdinalIgnoreCase))
                                ctx.Avisos.Add("La región quedó con el nombre '" + regionLeida + "' (se pidió '" + nombreRegion + "'): úsalo (o su índice 0) en listar_objetivos y establecer_frecuencia.");
                            string avisoCapa = Corredor.AvisoCapa(capaPrevista, capa);   // aviso, no error: el corredor está bien creado
                            if (avisoCapa != null && !capaNoAplicada) ctx.Avisos.Add(avisoCapa);
                            if (d.TryGetValue("esta_desactualizado", out object des) && des is bool b && b)
                                ctx.Avisos.Add("El corredor sigue desactualizado después de reconstruir: usa reconstruir_corredor y estado_corredor, y revisa los mensajes de Civil 3D (por ejemplo 'TargetDTM not found').");
                        },
                        Extra = () => new
                        {
                            nombre,
                            linea_base = lbLeida,
                            region = regionLeida,
                            alineamiento = nombreAlReal,
                            perfil = nombrePerfilReal,
                            tipo_perfil = tipoPerfil,
                            ensamblaje = nombreEnsReal,
                            subensamblajes,
                            inicio = N(inicioLeido),
                            fin = N(finLeido),
                            rango = rango.ComoDiccionario(),
                            frecuencia,
                            frecuencias = frecuenciasLeidas,
                            superficie_objetivo = nombreSuperficie,
                            objetivos_superficie = objetivosLeidos.Count,
                            objetivos = objetivosInfo,
                            camino_objetivos = caminoObjetivos,
                            region_creada_por_add = inicioRegionAdd.HasValue ? new { inicio = N(inicioRegionAdd.Value), fin = N(finRegionAdd.Value) } : null,
                            region_anadida_aparte = regionAnadidaAparte,
                            reconstrucciones,
                            ms_reconstruccion = msReconstruccion,
                            capa,
                            capa_configuracion = capaPrevista,
                            estado_corredor = estadoCorredor
                        }
                    };
                    var respuesta = Escritura.CrearObjeto(ctx, creacion);
                    return entrada == null ? respuesta : ConExtras(respuesta, new Dictionary<string, object> { ["restaurar"] = BloqueRestaurar(entrada) });
                })
            });
        }
    }
}

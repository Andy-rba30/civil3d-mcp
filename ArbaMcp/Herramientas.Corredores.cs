using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivProfile = Autodesk.Civil.DatabaseServices.Profile;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// Herramientas de corredores, ensamblajes, objetivos e intersecciones sobre la API .NET de Civil 3D
    /// (Autodesk.Civil.DatabaseServices). Ninguna borra ni recrea regiones, líneas base, ensamblajes ni superficies.
    /// </summary>
    public static partial class Herramientas
    {
        // Nombres candidatos de los miembros de la API que cambian entre versiones (se resuelven por reflexión, ver Api.cs)
        private static readonly string[] FrecTangentes = { "FrequencyAlongTangents", "FrequencyAlongTangent", "TangentFrequency" };
        private static readonly string[] FrecCurvas = { "FrequencyAlongCurves", "FrequencyAlongCurve", "CurveFrequency" };
        private static readonly string[] FrecEspirales = { "FrequencyAlongSpirals", "FrequencyAlongSpiral", "SpiralFrequency" };
        private static readonly string[] FrecPerfil = { "FrequencyAlongProfileCurves", "FrequencyAlongProfileCurve", "ProfileCurveFrequency", "FrequencyAlongProfile" };
        private static readonly string[] EstacionesAdic = { "AdditionalStations", "AdditionalStationCollection" };
        private static readonly string[] NombreLogico = { "LogicalName", "TargetName", "ParameterName" };
        private static readonly string[] GrupoObjetivo = { "AssemblyGroupName", "GroupName" };

        private static readonly Dictionary<string, DateTime> UltimaReconstruccion = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------------ búsqueda
        private static ObjectId BuscarCorredor(Transaction tr, string nombre)
        {
            var nombres = new List<string>();
            foreach (ObjectId id in CivilApplication.ActiveDocument.CorridorCollection)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Civ.Corridor cor)) continue;
                if (string.Equals(cor.Name, nombre, StringComparison.OrdinalIgnoreCase)) return id;
                nombres.Add(cor.Name);
            }
            throw new ArgumentException("No existe el corredor '" + nombre + "'. Corredores del dibujo: " + (nombres.Count == 0 ? "ninguno" : string.Join(", ", nombres)) + ".");
        }

        private static ObjectId BuscarEnsamblaje(Transaction tr, string nombre)
        {
            var nombres = new List<string>();
            foreach (ObjectId id in CivilApplication.ActiveDocument.AssemblyCollection)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Civ.Assembly asm)) continue;
                if (string.Equals(asm.Name, nombre, StringComparison.OrdinalIgnoreCase)) return id;
                nombres.Add(asm.Name);
            }
            throw new ArgumentException("No existe el ensamblaje '" + nombre + "'. Ensamblajes del dibujo: " + (nombres.Count == 0 ? "ninguno" : string.Join(", ", nombres)) + ".");
        }

        private static Civ.Baseline BuscarLineaBase(Civ.Corridor cor, string nombre)
        {
            var nombres = new List<string>();
            var lineas = new List<Civ.Baseline>();
            foreach (Civ.Baseline bl in cor.Baselines)
            {
                if (string.Equals(bl.Name, nombre, StringComparison.OrdinalIgnoreCase)) return bl;
                nombres.Add(bl.Name);
                lineas.Add(bl);
            }
            if (int.TryParse(nombre, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && i >= 0 && i < lineas.Count) return lineas[i];
            throw new ArgumentException("El corredor '" + cor.Name + "' no tiene una línea base llamada '" + nombre + "'. Líneas base: " + string.Join(", ", nombres) + ".");
        }

        /// <summary>Busca una región por nombre o, si no coincide ninguno, por el índice que devuelve listar_regiones.</summary>
        private static Civ.BaselineRegion BuscarRegion(Civ.Baseline bl, string nombreOIndice)
        {
            var regiones = new List<Civ.BaselineRegion>();
            foreach (Civ.BaselineRegion r in bl.BaselineRegions) regiones.Add(r);
            foreach (var r in regiones)
                if (string.Equals(r.Name, nombreOIndice, StringComparison.OrdinalIgnoreCase)) return r;
            if (int.TryParse(nombreOIndice, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && i >= 0 && i < regiones.Count) return regiones[i];
            throw new ArgumentException("La línea base '" + bl.Name + "' no tiene una región '" + nombreOIndice + "'. Regiones (índice: nombre): "
                + string.Join(", ", regiones.Select((r, k) => k + ": " + r.Name)) + ".");
        }

        private static Civ.CorridorSurface BuscarSuperficieCorredor(Civ.Corridor cor, string nombre)
        {
            var nombres = new List<string>();
            foreach (Civ.CorridorSurface cs in cor.CorridorSurfaces)
            {
                if (string.Equals(cs.Name, nombre, StringComparison.OrdinalIgnoreCase)) return cs;
                nombres.Add(cs.Name);
            }
            throw new ArgumentException("El corredor '" + cor.Name + "' no tiene una superficie llamada '" + nombre + "'. Superficies del corredor: " + (nombres.Count == 0 ? "ninguna" : string.Join(", ", nombres)) + ".");
        }

        private static ObjectId BuscarLineaCaracteristica(Transaction tr, Database db, string nombre)
        {
            var clase = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Civ.FeatureLine));
            foreach (ObjectId id in EntidadesModelo(tr, db))
            {
                if (!id.ObjectClass.IsDerivedFrom(clase)) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is Civ.FeatureLine fl && string.Equals(fl.Name, nombre, StringComparison.OrdinalIgnoreCase)) return id;
            }
            return ObjectId.Null;
        }

        private static IEnumerable<ObjectId> EntidadesModelo(Transaction tr, Database db)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms) yield return id;
        }

        private static bool IntentarHandle(Database db, string texto, out ObjectId id)
        {
            id = ObjectId.Null;
            if (string.IsNullOrWhiteSpace(texto) || !long.TryParse(texto.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long h)) return false;
            try { return db.TryGetObjectId(new Handle(h), out id) && !id.IsNull; }
            catch { return false; }
        }

        private static T Seguro<T>(Func<T> f, T def = default)
        {
            try { return f(); } catch { return def; }
        }

        // ------------------------------------------------------------------ descripción de objetos
        private static string NombreDe(Transaction tr, ObjectId id)
        {
            if (id.IsNull) return null;
            try
            {
                var o = tr.GetObject(id, OpenMode.ForRead);
                return Api.Leer<string>(o, null, "Name") ?? o.GetType().Name + " " + id.Handle;
            }
            catch { return null; }
        }

        /// <summary>Tipo y nombre de un objeto usado como objetivo: eje, perfil, superficie, polilínea o línea característica.</summary>
        private static object DescribirObjeto(Transaction tr, ObjectId id)
        {
            if (id.IsNull) return new { tipo = "ninguno", nombre = (string)null, handle = (string)null };
            try
            {
                var o = tr.GetObject(id, OpenMode.ForRead);
                string handle = id.Handle.ToString();
                switch (o)
                {
                    case CivAlignment al: return new { tipo = "eje", nombre = al.Name, handle };
                    case CivProfile pr: return new { tipo = "perfil", nombre = pr.Name + " (" + NombreDe(tr, pr.AlignmentId) + ")", handle };
                    case CivSurface su: return new { tipo = "superficie", nombre = su.Name, handle };
                    case Civ.FeatureLine fl: return new { tipo = "linea_caracteristica", nombre = fl.Name, handle };
                    case Polyline _: case Polyline2d _: case Polyline3d _: return new { tipo = "polilinea", nombre = handle, handle };
                    default: return new { tipo = o.GetType().Name, nombre = Api.Leer<string>(o, handle, "Name"), handle };
                }
            }
            catch (Exception ex) { return new { tipo = "desconocido", nombre = ex.Message, handle = id.Handle.ToString() }; }
        }

        private static string TextoObjeto(Transaction tr, ObjectId id)
        {
            var d = DescribirObjeto(tr, id);
            return Api.Leer<string>(d, "?", "tipo") + ":" + Api.Leer<string>(d, "?", "nombre");
        }

        private static List<ObjectId> IdsObjetivo(Civ.SubassemblyTargetInfo info)
        {
            var l = new List<ObjectId>();
            try { var ids = info.TargetIds; if (ids != null) foreach (ObjectId id in ids) l.Add(id); } catch { }
            return l;
        }

        private static string TipoObjetivo(Civ.SubassemblyTargetInfo info)
        {
            string t = Seguro(() => info.TargetType.ToString(), "");
            if (t.IndexOf("Surface", StringComparison.OrdinalIgnoreCase) >= 0) return "superficie";
            if (t.IndexOf("Elevation", StringComparison.OrdinalIgnoreCase) >= 0) return "elevacion";
            if (t.IndexOf("Offset", StringComparison.OrdinalIgnoreCase) >= 0) return "desplazamiento";
            return t.ToLowerInvariant();
        }

        private static string NormalizarTipoObjetivo(string tipo)
        {
            switch ((tipo ?? "").Trim().ToLowerInvariant())
            {
                case "superficie": case "surface": return "superficie";
                case "elevacion": case "elevación": case "elevation": case "cota": return "elevacion";
                case "desplazamiento": case "offset": case "anchura": case "ancho": return "desplazamiento";
                default: throw new ArgumentException("El parámetro 'tipo' debe ser 'superficie', 'elevacion' o 'desplazamiento' (recibido: '" + tipo + "').");
            }
        }

        private static string OpcionObjetivo(Civ.SubassemblyTargetInfo info)
        {
            string o = Seguro(() => info.TargetToOption.ToString(), "");
            if (o.IndexOf("Nearest", StringComparison.OrdinalIgnoreCase) >= 0) return "mas_cercano";
            if (o.IndexOf("Outside", StringComparison.OrdinalIgnoreCase) >= 0) return "exterior";
            if (o.IndexOf("Inside", StringComparison.OrdinalIgnoreCase) >= 0) return "interior";
            return string.IsNullOrEmpty(o) ? null : o.ToLowerInvariant();
        }

        private static string OpcionApi(string opcion)
        {
            switch ((opcion ?? "").Trim().ToLowerInvariant())
            {
                case "mas_cercano": case "más_cercano": case "cercano": case "nearest": return "Nearest";
                case "exterior": case "outside": return "Outside";
                case "interior": case "inside": return "Inside";
                default: throw new ArgumentException("El parámetro 'opcion' debe ser 'mas_cercano', 'exterior' o 'interior' (recibido: '" + opcion + "').");
            }
        }

        private static string Lado(object side)
        {
            string s = side?.ToString() ?? "";
            if (s.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0) return "izquierda";
            if (s.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0) return "derecha";
            return "ninguno";
        }

        private static string LadoSubensamblaje(Transaction tr, ObjectId idSub)
        {
            if (idSub.IsNull) return null;
            try { return tr.GetObject(idSub, OpenMode.ForRead) is Civ.Subassembly sub ? Lado(sub.Side) : null; }
            catch { return null; }
        }

        /// <summary>Nombre del grupo del ensamblaje que contiene el subensamblaje (buscando en los grupos, sin depender de propiedades opcionales).</summary>
        private static string GrupoDeSubensamblaje(Transaction tr, ObjectId idEnsamblaje, ObjectId idSub)
        {
            if (idEnsamblaje.IsNull || idSub.IsNull) return null;
            try
            {
                if (!(tr.GetObject(idEnsamblaje, OpenMode.ForRead) is Civ.Assembly asm)) return null;
                foreach (Civ.AssemblyGroup g in asm.Groups)
                    foreach (ObjectId id in g.GetSubassemblyIds())
                        if (id == idSub) return g.Name;
            }
            catch { }
            return null;
        }

        private static Dictionary<string, object> ParametrosSubensamblaje(Civ.Subassembly sub)
        {
            var d = new Dictionary<string, object>();
            void Volcar(object coleccion)
            {
                foreach (var p in Api.Lista(coleccion))
                {
                    string n = Api.Leer<string>(p, null, "Name", "DisplayName");
                    if (n == null) continue;
                    Api.IntentarLeer(p, out object v, "Value");
                    d[n] = v is double dv ? N(dv) : v is float fv ? N(fv) : v is bool || v is string || v is int || v is long ? v : v?.ToString();
                }
            }
            Volcar(Seguro<object>(() => sub.ParamsDouble));
            Volcar(Seguro<object>(() => sub.ParamsLong));
            Volcar(Seguro<object>(() => sub.ParamsString));
            Volcar(Seguro<object>(() => sub.ParamsBool));
            return d;
        }

        private static double? Frecuencia(Civ.BaselineRegion reg, string[] nombres)
        {
            var v = Api.Leer<double?>(reg, null, nombres);
            return v.HasValue ? N(v.Value) : null;
        }

        private static List<double?> EstacionesAdicionales(Civ.BaselineRegion reg)
        {
            var l = new List<double?>();
            if (Api.IntentarLeer(reg, out object col, EstacionesAdic))
                foreach (var x in Api.Lista(col))
                {
                    try { l.Add(N(Convert.ToDouble(x, CultureInfo.InvariantCulture))); } catch { }
                }
            return l.OrderBy(x => x).ToList();
        }

        private static object InfoRegion(Transaction tr, Civ.Baseline bl, Civ.BaselineRegion reg, int indice) => new
        {
            linea_base = bl.Name,
            indice,
            nombre = reg.Name,
            inicio = N(reg.StartStation),
            fin = N(reg.EndStation),
            ensamblaje = NombreDe(tr, reg.AssemblyId),
            frecuencia_tangentes = Frecuencia(reg, FrecTangentes),
            frecuencia_curvas = Frecuencia(reg, FrecCurvas),
            frecuencia_espirales = Frecuencia(reg, FrecEspirales),
            frecuencia_perfil = Frecuencia(reg, FrecPerfil),
            estaciones_adicionales = EstacionesAdicionales(reg)
        };

        private static object InfoObjetivo(Transaction tr, Civ.SubassemblyTargetInfo info, ObjectId idEnsamblaje) => new
        {
            subensamblaje = info.SubassemblyName,
            grupo = GrupoDeSubensamblaje(tr, idEnsamblaje, info.SubassemblyId) ?? Api.Leer<string>(info, null, GrupoObjetivo),
            lado = LadoSubensamblaje(tr, info.SubassemblyId),
            tipo = TipoObjetivo(info),
            parametro = Api.Leer<string>(info, null, NombreLogico),
            objetivos = IdsObjetivo(info).Select(id => DescribirObjeto(tr, id)).ToList(),
            opcion_objetivo = OpcionObjetivo(info)
        };

        /// <summary>Estado comparable de un objetivo (para antes/después).</summary>
        private static Dictionary<string, object> EstadoObjetivo(Transaction tr, Civ.SubassemblyTargetInfo info) => new Dictionary<string, object>
        {
            ["subensamblaje"] = info.SubassemblyName,
            ["tipo"] = TipoObjetivo(info),
            ["parametro"] = Api.Leer<string>(info, null, NombreLogico),
            ["objetivos"] = IdsObjetivo(info).Select(id => TextoObjeto(tr, id)).ToList(),
            ["opcion"] = OpcionObjetivo(info)
        };

        /// <summary>
        /// Busca en la región el SubassemblyTargetInfo que coincide por nombre de subensamblaje y tipo; si hay varios,
        /// exige 'grupo' (o 'parametro') y falla con la lista de opciones. Devuelve la colección (para SetTargets) y el índice.
        /// </summary>
        private static (Civ.SubassemblyTargetInfoCollection coleccion, Civ.SubassemblyTargetInfo info) BuscarObjetivoInfo(
            Transaction tr, Civ.BaselineRegion reg, string subensamblaje, string tipo, string grupo, string parametro)
        {
            var coleccion = reg.GetTargets();
            var candidatos = new List<Civ.SubassemblyTargetInfo>();
            var todos = new List<string>();
            foreach (Civ.SubassemblyTargetInfo info in coleccion)
            {
                todos.Add(info.SubassemblyName + " [" + TipoObjetivo(info) + "]");
                if (!string.Equals(info.SubassemblyName, subensamblaje, StringComparison.OrdinalIgnoreCase)) continue;
                if (TipoObjetivo(info) != tipo) continue;
                candidatos.Add(info);
            }
            if (candidatos.Count == 0)
                throw new ArgumentException("La región '" + reg.Name + "' no tiene un objetivo de tipo '" + tipo + "' para el subensamblaje '" + subensamblaje + "'. Objetivos de la región: " + string.Join(", ", todos.Distinct()) + ".");

            if (candidatos.Count > 1)
            {
                var porGrupo = candidatos.Select(c => new { info = c, grupo = GrupoDeSubensamblaje(tr, reg.AssemblyId, c.SubassemblyId) ?? Api.Leer<string>(c, "?", GrupoObjetivo), parametro = Api.Leer<string>(c, "?", NombreLogico) }).ToList();
                if (porGrupo.Select(x => x.grupo).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                {
                    if (string.IsNullOrWhiteSpace(grupo))
                        throw new ArgumentException("Hay " + candidatos.Count + " subensamblajes llamados '" + subensamblaje + "' en la región '" + reg.Name + "'. Indica el parámetro 'grupo' con uno de: " + string.Join(", ", porGrupo.Select(x => x.grupo).Distinct()) + ".");
                    porGrupo = porGrupo.Where(x => string.Equals(x.grupo, grupo, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (porGrupo.Count == 0)
                        throw new ArgumentException("Ningún subensamblaje '" + subensamblaje + "' está en el grupo '" + grupo + "'. Grupos posibles: " + string.Join(", ", candidatos.Select(c => GrupoDeSubensamblaje(tr, reg.AssemblyId, c.SubassemblyId)).Distinct()) + ".");
                }
                if (porGrupo.Count > 1)
                {
                    if (string.IsNullOrWhiteSpace(parametro))
                        throw new ArgumentException("El subensamblaje '" + subensamblaje + "' tiene " + porGrupo.Count + " objetivos de tipo '" + tipo + "'. Indica el parámetro 'parametro' con uno de: " + string.Join(", ", porGrupo.Select(x => x.parametro)) + ".");
                    porGrupo = porGrupo.Where(x => string.Equals(x.parametro, parametro, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (porGrupo.Count != 1)
                        throw new ArgumentException("No hay exactamente un objetivo '" + parametro + "' de tipo '" + tipo + "' en el subensamblaje '" + subensamblaje + "'.");
                }
                return (coleccion, porGrupo[0].info);
            }
            return (coleccion, candidatos[0]);
        }

        /// <summary>Resuelve el objeto que se asignará como objetivo (o vacío para 'ninguno').</summary>
        private static ObjectIdCollection ResolverObjetivo(Transaction tr, Database db, string tipo, string objetivo, string alineamientoDelPerfil)
        {
            var ids = new ObjectIdCollection();
            string o = (objetivo ?? "").Trim();
            if (o.Length == 0 || string.Equals(o, "ninguno", StringComparison.OrdinalIgnoreCase) || string.Equals(o, "none", StringComparison.OrdinalIgnoreCase)) return ids;

            if (tipo == "superficie") { ids.Add(BuscarSuperficie(tr, o)); return ids; }

            if (tipo == "elevacion" && !string.IsNullOrWhiteSpace(alineamientoDelPerfil))
            {
                ids.Add(BuscarPerfil(tr, BuscarAlineamiento(tr, alineamientoDelPerfil), o));
                return ids;
            }
            if (tipo == "desplazamiento")
            {
                try { ids.Add(BuscarAlineamiento(tr, o)); return ids; } catch (ArgumentException) { }
            }
            var idFl = BuscarLineaCaracteristica(tr, db, o);
            if (!idFl.IsNull) { ids.Add(idFl); return ids; }
            if (IntentarHandle(db, o, out ObjectId idHandle)) { ids.Add(idHandle); return ids; }

            throw new ArgumentException("No se encontró el objetivo '" + o + "' para un objetivo de " + tipo + ". "
                + (tipo == "elevacion" ? "Para un perfil indica 'alineamiento_del_perfil'; " : "Para un eje usa su nombre; ")
                + "también vale el nombre de una línea característica o el handle (hexadecimal) de una polilínea. Usa 'ninguno' para quitarlo.");
        }

        private static List<string> Ordenada(IEnumerable<string> l) => l.Where(x => x != null).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>Códigos de enlace o de punto de una superficie de corredor; null si la API cargada no expone el miembro.</summary>
        private static List<string> Codigos(Civ.CorridorSurface cs, string tipo)
        {
            string[] nombres = tipo == "enlace" ? new[] { "LinkCodes", "GetLinkCodes" } : new[] { "PointCodes", "GetPointCodes" };
            return Api.IntentarLeer(cs, out object col, nombres) ? Ordenada(Api.Lista(col).Select(x => x?.ToString())) : null;
        }

        private static List<object> Contornos(Civ.CorridorSurface cs)
        {
            var l = new List<object>();
            foreach (var b in Api.Lista(Seguro<object>(() => cs.Boundaries)))
                l.Add(new
                {
                    nombre = Api.Leer<string>(b, null, "Name"),
                    tipo = Api.Leer<object>(b, null, "BoundaryType", "Type")?.ToString(),
                    usar_como_exterior = Api.Leer<bool?>(b, null, "UseAsOuterBoundary", "IsOuterBoundary", "UseAsOuter")
                });
            return l;
        }

        private static object InfoSuperficieCorredor(Transaction tr, Civ.CorridorSurface cs)
        {
            var idSu = Api.Leer<ObjectId>(cs, ObjectId.Null, "SurfaceId");
            bool? desact = Api.Leer<bool?>(cs, null, "IsOutOfDate", "OutOfDate");
            if (!desact.HasValue && !idSu.IsNull) desact = Api.Leer<bool?>(Seguro(() => tr.GetObject(idSu, OpenMode.ForRead)), null, "IsOutOfDate", "OutOfDate");
            return new
            {
                nombre = cs.Name,
                codigos_enlace = Codigos(cs, "enlace"),
                codigos_punto = Codigos(cs, "punto"),
                contornos = Contornos(cs),
                esta_desactualizada = desact
            };
        }

        // ------------------------------------------------------------------ patrón de escritura sobre un corredor
        /// <summary>
        /// Abre el corredor (ForWrite salvo en simulación), lee 'antes', calcula 'esperado', aplica 'cambiar', confirma y
        /// vuelve a leer 'despues' en otra transacción. Devuelve la respuesta estándar con antes/después verificados.
        /// </summary>
        private static object CambiarCorredor(Escritura.Contexto ctx, string corredor,
            Func<Transaction, Civ.Corridor, Dictionary<string, object>> leer,
            Func<Transaction, Civ.Corridor, Dictionary<string, object>> esperar,
            Action<Transaction, Civ.Corridor> cambiar,
            string accion, Func<object> extra = null)
        {
            var doc = ctx.Doc;
            Dictionary<string, object> antes, esperado;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var cor = (Civ.Corridor)tr.GetObject(BuscarCorredor(tr, corredor), ctx.Simular ? OpenMode.ForRead : OpenMode.ForWrite);
                antes = leer(tr, cor);
                esperado = esperar(tr, cor);
                if (ctx.Simular) { tr.Commit(); return Escritura.Simulacion(ctx, antes, esperado, accion); }
                cambiar(tr, cor);
                tr.Commit();
            }
            Dictionary<string, object> despues;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var cor = (Civ.Corridor)tr.GetObject(BuscarCorredor(tr, corredor), OpenMode.ForRead);
                despues = leer(tr, cor);
                tr.Commit();
            }
            return Escritura.Resultado(ctx, antes, despues, esperado, accion, extra?.Invoke());
        }

        private static void ComprobarSolape(Civ.Baseline bl, Civ.BaselineRegion reg, double inicio, double fin)
        {
            if (fin <= inicio) throw new ArgumentException("El fin (" + fin + ") debe ser mayor que el inicio (" + inicio + ").");
            double bi = Seguro(() => bl.StartStation, double.NaN), bf = Seguro(() => bl.EndStation, double.NaN);
            if (!double.IsNaN(bi) && (inicio < bi - 1e-6 || fin > bf + 1e-6))
                throw new ArgumentException("El rango " + inicio + "-" + fin + " sale de la línea base '" + bl.Name + "' (" + N(bi) + "-" + N(bf) + ").");
            foreach (Civ.BaselineRegion otra in bl.BaselineRegions)
            {
                if (ReferenceEquals(otra, reg) || string.Equals(otra.Name, reg.Name, StringComparison.OrdinalIgnoreCase)) continue;
                if (inicio < otra.EndStation - 1e-6 && fin > otra.StartStation + 1e-6)
                    throw new ArgumentException("El rango " + inicio + "-" + fin + " solapa con la región '" + otra.Name + "' (" + N(otra.StartStation) + "-" + N(otra.EndStation) + ").");
            }
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarCorredores()
        {
            // ============================================================== lectura
            Registrar(new Herramienta
            {
                Nombre = "listar_corredores",
                Descripcion = "Lista los corredores del dibujo activo: si está desactualizado, si se reconstruye automáticamente y sus líneas base (alineamiento, perfil, rango y número de regiones).",
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    var lista = new List<object>();
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId id in CivilApplication.ActiveDocument.CorridorCollection)
                        {
                            if (!(tr.GetObject(id, OpenMode.ForRead) is Civ.Corridor cor)) continue;
                            var lineas = new List<object>();
                            foreach (Civ.Baseline bl in cor.Baselines)
                                lineas.Add(new
                                {
                                    nombre = bl.Name,
                                    alineamiento = NombreDe(tr, Seguro(() => bl.AlignmentId, ObjectId.Null)),
                                    perfil = NombreDe(tr, Seguro(() => bl.ProfileId, ObjectId.Null)),
                                    inicio = Seguro(() => N(bl.StartStation)),
                                    fin = Seguro(() => N(bl.EndStation)),
                                    n_regiones = bl.BaselineRegions.Count
                                });
                            lista.Add(new
                            {
                                nombre = cor.Name,
                                esta_desactualizado = cor.IsOutOfDate,
                                reconstruir_automatico = cor.RebuildAutomatic,
                                lineas_base = lineas
                            });
                        }
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "listar_regiones",
                Descripcion = "Lista las regiones de un corredor (opcionalmente de una sola línea base): índice, nombre, rango, ensamblaje, frecuencias y estaciones adicionales.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("linea_base", "string", "Nombre de la línea base (si se omite, todas)")
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string lineaBase = Str(a, "linea_base");
                    var lista = new List<object>();
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var cor = (Civ.Corridor)tr.GetObject(BuscarCorredor(tr, Requerido(a, "corredor")), OpenMode.ForRead);
                        var lineas = new List<Civ.Baseline>();
                        if (string.IsNullOrWhiteSpace(lineaBase)) foreach (Civ.Baseline bl in cor.Baselines) lineas.Add(bl);
                        else lineas.Add(BuscarLineaBase(cor, lineaBase));
                        foreach (var bl in lineas)
                        {
                            int i = 0;
                            foreach (Civ.BaselineRegion reg in bl.BaselineRegions) lista.Add(InfoRegion(tr, bl, reg, i++));
                        }
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "listar_objetivos",
                Descripcion = "Lista los objetivos (superficie, elevación, desplazamiento) de cada subensamblaje de una región: grupo, lado, parámetro, objetos asignados y opción (más cercano, exterior, interior).",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("linea_base", "string", "Nombre de la línea base", true),
                    P("region", "string", "Nombre o índice de la región (ver listar_regiones)", true)
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    var lista = new List<object>();
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var cor = (Civ.Corridor)tr.GetObject(BuscarCorredor(tr, Requerido(a, "corredor")), OpenMode.ForRead);
                        var bl = BuscarLineaBase(cor, Requerido(a, "linea_base"));
                        var reg = BuscarRegion(bl, Requerido(a, "region"));
                        foreach (Civ.SubassemblyTargetInfo info in reg.GetTargets()) lista.Add(InfoObjetivo(tr, info, reg.AssemblyId));
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "listar_ensamblajes",
                Descripcion = "Lista los ensamblajes del dibujo con sus grupos, subensamblajes (tipo, lado, parámetros) y dónde se usan (corredor, línea base, región).",
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    var lista = new List<object>();
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        // Uso de cada ensamblaje en los corredores
                        var usos = new Dictionary<ObjectId, List<object>>();
                        foreach (ObjectId idCor in CivilApplication.ActiveDocument.CorridorCollection)
                        {
                            if (!(tr.GetObject(idCor, OpenMode.ForRead) is Civ.Corridor cor)) continue;
                            foreach (Civ.Baseline bl in cor.Baselines)
                                foreach (Civ.BaselineRegion reg in bl.BaselineRegions)
                                {
                                    if (!usos.TryGetValue(reg.AssemblyId, out var l)) usos[reg.AssemblyId] = l = new List<object>();
                                    l.Add(new { corredor = cor.Name, linea_base = bl.Name, region = reg.Name });
                                }
                        }

                        foreach (ObjectId id in CivilApplication.ActiveDocument.AssemblyCollection)
                        {
                            if (!(tr.GetObject(id, OpenMode.ForRead) is Civ.Assembly asm)) continue;
                            var grupos = new List<object>();
                            foreach (Civ.AssemblyGroup g in asm.Groups)
                            {
                                var subs = new List<object>();
                                var lados = new List<string>();
                                foreach (ObjectId idSub in g.GetSubassemblyIds())
                                {
                                    if (!(tr.GetObject(idSub, OpenMode.ForRead) is Civ.Subassembly sub)) continue;
                                    string lado = Lado(Seguro<object>(() => sub.Side));
                                    lados.Add(lado);
                                    subs.Add(new
                                    {
                                        nombre = sub.Name,
                                        tipo = Api.Leer<string>(sub, null, "MacroName", "SubassemblyName", "ClassName", "DefaultName") ?? sub.GetType().Name,
                                        lado,
                                        parametros = ParametrosSubensamblaje(sub)
                                    });
                                }
                                string ladoGrupo = Api.Leer<object>(g, null, "Side") is object s ? Lado(s)
                                    : lados.Distinct().Count() == 1 ? lados[0] : lados.Count == 0 ? "ninguno" : "mixto";
                                grupos.Add(new { nombre = g.Name, lado = ladoGrupo, subensamblajes = subs });
                            }
                            lista.Add(new
                            {
                                nombre = asm.Name,
                                tipo = Api.Leer<object>(asm, null, "AssemblyType", "Type")?.ToString(),
                                grupos,
                                usado_en = usos.TryGetValue(id, out var u) ? u : new List<object>()
                            });
                        }
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "listar_intersecciones",
                Descripcion = "Lista las intersecciones del dibujo: ejes principal y secundario, progresivas de cruce, corredor, tipo y regiones generadas en el corredor.",
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    var lista = new List<object>();
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var clase = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Civ.Intersection));
                        foreach (ObjectId id in EntidadesModelo(tr, doc.Database))
                        {
                            if (!id.ObjectClass.IsDerivedFrom(clase)) continue;
                            if (!(tr.GetObject(id, OpenMode.ForRead) is Civ.Intersection inter)) continue;

                            var idPrim = Api.Leer<ObjectId>(inter, ObjectId.Null, "PrimaryRoadAlignmentId", "PrimaryAlignmentId", "PrimaryRoadId");
                            var idSec = Api.Leer<ObjectId>(inter, ObjectId.Null, "SecondaryRoadAlignmentId", "SecondaryAlignmentId", "SecondaryRoadId");
                            var loc = Api.Leer<Point3d?>(inter, null, "Location", "IntersectionPoint", "Position");
                            double? pkPrim = Api.Leer<double?>(inter, null, "PrimaryRoadStation", "PrimaryStation");
                            double? pkSec = Api.Leer<double?>(inter, null, "SecondaryRoadStation", "SecondaryStation");
                            if (loc.HasValue)
                            {
                                if (!pkPrim.HasValue && !idPrim.IsNull) pkPrim = Seguro<double?>(() => { double s = 0, o = 0; ((CivAlignment)tr.GetObject(idPrim, OpenMode.ForRead)).StationOffset(loc.Value.X, loc.Value.Y, ref s, ref o); return s; });
                                if (!pkSec.HasValue && !idSec.IsNull) pkSec = Seguro<double?>(() => { double s = 0, o = 0; ((CivAlignment)tr.GetObject(idSec, OpenMode.ForRead)).StationOffset(loc.Value.X, loc.Value.Y, ref s, ref o); return s; });
                            }

                            // Corredor: propiedad si existe; si no, corredores con línea base sobre esos ejes en la progresiva de cruce
                            var idCor = Api.Leer<ObjectId>(inter, ObjectId.Null, "CorridorId");
                            var corredores = new List<string>();
                            var regiones = new List<object>();
                            foreach (ObjectId idC in CivilApplication.ActiveDocument.CorridorCollection)
                            {
                                if (!(tr.GetObject(idC, OpenMode.ForRead) is Civ.Corridor cor)) continue;
                                if (!idCor.IsNull && idC != idCor) continue;
                                bool implicado = idC == idCor;
                                foreach (Civ.Baseline bl in cor.Baselines)
                                {
                                    var idAl = Seguro(() => bl.AlignmentId, ObjectId.Null);
                                    double? pk = idAl == idPrim ? pkPrim : idAl == idSec ? pkSec : null;
                                    if (!pk.HasValue) continue;
                                    foreach (Civ.BaselineRegion reg in bl.BaselineRegions)
                                        if (pk.Value >= reg.StartStation - 1e-6 && pk.Value <= reg.EndStation + 1e-6)
                                        {
                                            implicado = true;
                                            regiones.Add(new { corredor = cor.Name, linea_base = bl.Name, region = reg.Name, inicio = N(reg.StartStation), fin = N(reg.EndStation), ensamblaje = NombreDe(tr, reg.AssemblyId) });
                                        }
                                }
                                if (implicado) corredores.Add(cor.Name);
                            }

                            lista.Add(new
                            {
                                nombre = inter.Name,
                                eje_principal = NombreDe(tr, idPrim),
                                eje_secundario = NombreDe(tr, idSec),
                                pk_principal = pkPrim.HasValue ? N(pkPrim.Value) : null,
                                pk_secundaria = pkSec.HasValue ? N(pkSec.Value) : null,
                                x = loc.HasValue ? N(loc.Value.X) : null,
                                y = loc.HasValue ? N(loc.Value.Y) : null,
                                corredor = corredores.Count == 1 ? corredores[0] : corredores.Count == 0 ? null : string.Join("; ", corredores),
                                tipo = Api.Leer<object>(inter, null, "IntersectionType", "Type")?.ToString(),
                                regiones_generadas = regiones
                            });
                        }
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "listar_lineas_muestreo",
                Descripcion = "Lista los grupos de líneas de muestreo (de un alineamiento o de todos): número de líneas, rango de progresivas y fuentes muestreadas (superficies y corredores).",
                Parametros = { P("alineamiento", "string", "Nombre del alineamiento (si se omite, todos)") },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string nombreAl = Str(a, "alineamiento");
                    var lista = new List<object>();
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var idsAl = new List<ObjectId>();
                        if (string.IsNullOrWhiteSpace(nombreAl)) foreach (ObjectId id in CivilApplication.ActiveDocument.GetAlignmentIds()) idsAl.Add(id);
                        else idsAl.Add(BuscarAlineamiento(tr, nombreAl));
                        foreach (var idAl in idsAl)
                        {
                            if (!(tr.GetObject(idAl, OpenMode.ForRead) is CivAlignment al)) continue;
                            foreach (ObjectId idG in al.GetSampleLineGroupIds())
                            {
                                if (!(tr.GetObject(idG, OpenMode.ForRead) is Civ.SampleLineGroup slg)) continue;
                                double? ini = null, fin = null; int n = 0;
                                foreach (ObjectId idSl in slg.GetSampleLineIds())
                                {
                                    n++;
                                    if (!(tr.GetObject(idSl, OpenMode.ForRead) is Civ.SampleLine sl)) continue;
                                    double s = Seguro(() => sl.Station, double.NaN);
                                    if (double.IsNaN(s)) continue;
                                    ini = ini.HasValue ? Math.Min(ini.Value, s) : s;
                                    fin = fin.HasValue ? Math.Max(fin.Value, s) : s;
                                }
                                var fuentes = new List<object>();
                                try
                                {
                                    foreach (Civ.SectionSource f in slg.GetSectionSources())
                                        fuentes.Add(new { nombre = NombreDe(tr, f.SourceId), tipo = f.SourceType.ToString(), muestreada = f.IsSampled });
                                }
                                catch (Exception ex) { fuentes.Add(new { nombre = (string)null, tipo = "error", muestreada = false, error = ex.Message }); }
                                lista.Add(new { nombre = slg.Name, alineamiento = al.Name, n_lineas = n, inicio = ini.HasValue ? N(ini.Value) : null, fin = fin.HasValue ? N(fin.Value) : null, fuentes });
                            }
                        }
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "estado_corredor",
                Descripcion = "Estado de un corredor: si está desactualizado, última reconstrucción hecha desde MCP y sus superficies con códigos de enlace y punto, contornos y si están desactualizadas.",
                Parametros = { P("corredor", "string", "Nombre del corredor", true) },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string nombre = Requerido(a, "corredor");
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var cor = (Civ.Corridor)tr.GetObject(BuscarCorredor(tr, nombre), OpenMode.ForRead);
                        var superficies = new List<object>();
                        foreach (Civ.CorridorSurface cs in cor.CorridorSurfaces) superficies.Add(InfoSuperficieCorredor(tr, cs));
                        DateTime? ultima = null;
                        lock (UltimaReconstruccion) if (UltimaReconstruccion.TryGetValue(cor.Name, out var t)) ultima = t;
                        var r = new
                        {
                            nombre = cor.Name,
                            esta_desactualizado = cor.IsOutOfDate,
                            reconstruir_automatico = cor.RebuildAutomatic,
                            ultima_reconstruccion = ultima?.ToString("yyyy-MM-dd HH:mm:ss") ?? Api.Leer<object>(cor, null, "LastRebuildTime", "LastBuildTime")?.ToString(),
                            n_lineas_base = cor.Baselines.Count,
                            superficies
                        };
                        tr.Commit();
                        return r;
                    }
                }
            });

            // ============================================================== escritura
            Registrar(new Herramienta
            {
                Nombre = "asignar_ensamblaje_region",
                Descripcion = "Cambia el ensamblaje de una región (region.AssemblyId). Escritura: hace copia, registra en el log y verifica el cambio.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("linea_base", "string", "Nombre de la línea base", true),
                    P("region", "string", "Nombre o índice de la región", true),
                    P("ensamblaje", "string", "Nombre del ensamblaje a asignar", true),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("asignar_ensamblaje_region", a, ctx =>
                {
                    string lineaBase = Requerido(a, "linea_base"), region = Requerido(a, "region"), ensamblaje = Requerido(a, "ensamblaje");
                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        (tr, cor) => { var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region); return new Dictionary<string, object> { ["region"] = reg.Name, ["ensamblaje"] = NombreDe(tr, reg.AssemblyId) }; },
                        (tr, cor) => new Dictionary<string, object> { ["ensamblaje"] = NombreDe(tr, BuscarEnsamblaje(tr, ensamblaje)) },
                        (tr, cor) => { BuscarRegion(BuscarLineaBase(cor, lineaBase), region).AssemblyId = BuscarEnsamblaje(tr, ensamblaje); },
                        "Asignar el ensamblaje '" + ensamblaje + "' a la región '" + region + "' de '" + lineaBase + "'");
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "asignar_objetivo",
                Descripcion = "Asigna el objetivo de superficie, elevación o desplazamiento de un subensamblaje en una región (GetTargets → SetTargets). Con varios subensamblajes del mismo nombre exige 'grupo'; con varios objetivos del mismo tipo exige 'parametro'.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("linea_base", "string", "Nombre de la línea base", true),
                    P("region", "string", "Nombre o índice de la región", true),
                    P("subensamblaje", "string", "Nombre del subensamblaje (ver listar_objetivos)", true),
                    P("tipo", "string", "superficie, elevacion o desplazamiento", true),
                    P("objetivo", "string", "Nombre del objeto (superficie, eje, perfil, línea característica) o handle de polilínea; 'ninguno' para quitarlo", true),
                    P("alineamiento_del_perfil", "string", "Alineamiento al que pertenece el perfil, cuando el objetivo es un perfil"),
                    P("opcion", "string", "mas_cercano, exterior o interior (objetivos de desplazamiento y elevación)"),
                    P("mismo_lado", "boolean", "Restringir el objetivo al mismo lado del subensamblaje, si la API lo permite"),
                    P("grupo", "string", "Grupo del ensamblaje, cuando hay varios subensamblajes con el mismo nombre"),
                    P("parametro", "string", "Nombre lógico del objetivo, cuando el subensamblaje tiene varios del mismo tipo"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("asignar_objetivo", a, ctx =>
                {
                    string lineaBase = Requerido(a, "linea_base"), region = Requerido(a, "region"), sub = Requerido(a, "subensamblaje");
                    string tipo = NormalizarTipoObjetivo(Requerido(a, "tipo"));
                    string objetivo = Requerido(a, "objetivo"), alPerfil = Str(a, "alineamiento_del_perfil");
                    string opcion = Str(a, "opcion"), grupo = Str(a, "grupo"), parametro = Str(a, "parametro");
                    bool? mismoLado = Tiene(a, "mismo_lado") ? Bool(a, "mismo_lado", false) : (bool?)null;
                    var db = ctx.Db;

                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        (tr, cor) => EstadoObjetivo(tr, BuscarObjetivoInfo(tr, BuscarRegion(BuscarLineaBase(cor, lineaBase), region), sub, tipo, grupo, parametro).info),
                        (tr, cor) =>
                        {
                            var ids = ResolverObjetivo(tr, db, tipo, objetivo, alPerfil);
                            var e = new Dictionary<string, object> { ["objetivos"] = ids.Cast<ObjectId>().Select(id => TextoObjeto(tr, id)).ToList() };
                            if (!string.IsNullOrWhiteSpace(opcion)) e["opcion"] = OpcionApi(opcion) == "Nearest" ? "mas_cercano" : OpcionApi(opcion) == "Outside" ? "exterior" : "interior";
                            return e;
                        },
                        (tr, cor) =>
                        {
                            var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region);
                            var (coleccion, info) = BuscarObjetivoInfo(tr, reg, sub, tipo, grupo, parametro);
                            info.TargetIds = ResolverObjetivo(tr, db, tipo, objetivo, alPerfil);
                            if (!string.IsNullOrWhiteSpace(opcion)) Api.Asignar(info, OpcionApi(opcion), "TargetToOption");
                            if (mismoLado.HasValue)
                            {
                                try { Api.Asignar(info, mismoLado.Value, "UseSameSide", "SameSide", "IsSameSide"); }
                                catch (MissingMemberException) { ctx.Avisos.Add("La API de esta versión no expone 'mismo_lado' en SubassemblyTargetInfo; se ignoró."); }
                            }
                            reg.SetTargets(coleccion);
                        },
                        "Asignar '" + objetivo + "' como objetivo de " + tipo + " del subensamblaje '" + sub + "' en la región '" + region + "'");
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "asignar_objetivos_superficie",
                Descripcion = "Pone una superficie en todos los objetivos de tipo superficie del corredor, de una línea base o de una región (equivale a 'Establecer todos los objetivos' de superficie).",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("superficie", "string", "Nombre de la superficie objetivo", true),
                    P("linea_base", "string", "Limitar a esta línea base"),
                    P("region", "string", "Limitar a esta región (requiere linea_base)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("asignar_objetivos_superficie", a, ctx =>
                {
                    string superficie = Requerido(a, "superficie"), lineaBase = Str(a, "linea_base"), region = Str(a, "region");
                    if (!string.IsNullOrWhiteSpace(region) && string.IsNullOrWhiteSpace(lineaBase)) throw new ArgumentException("Para limitar a una región indica también 'linea_base'.");

                    List<(Civ.Baseline bl, Civ.BaselineRegion reg)> Ambito(Civ.Corridor cor)
                    {
                        var l = new List<(Civ.Baseline, Civ.BaselineRegion)>();
                        foreach (Civ.Baseline bl in cor.Baselines)
                        {
                            if (!string.IsNullOrWhiteSpace(lineaBase) && !string.Equals(bl.Name, lineaBase, StringComparison.OrdinalIgnoreCase)) continue;
                            if (!string.IsNullOrWhiteSpace(region)) { l.Add((bl, BuscarRegion(bl, region))); continue; }
                            foreach (Civ.BaselineRegion reg in bl.BaselineRegions) l.Add((bl, reg));
                        }
                        if (!string.IsNullOrWhiteSpace(lineaBase) && l.Count == 0) BuscarLineaBase(cor, lineaBase); // lanza el error con la lista
                        return l;
                    }
                    Dictionary<string, object> Leer(Transaction tr, Civ.Corridor cor)
                    {
                        var d = new Dictionary<string, object>();
                        foreach (var (bl, reg) in Ambito(cor))
                            foreach (Civ.SubassemblyTargetInfo info in reg.GetTargets())
                                if (TipoObjetivo(info) == "superficie")
                                    d[bl.Name + " / " + reg.Name + " / " + info.SubassemblyName + " / " + (Api.Leer<string>(info, "superficie", NombreLogico))] = IdsObjetivo(info).Select(id => NombreDe(tr, id)).FirstOrDefault();
                        return d;
                    }
                    string nombreReal = null;
                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        Leer,
                        (tr, cor) =>
                        {
                            nombreReal = NombreDe(tr, BuscarSuperficie(tr, superficie));
                            var e = new Dictionary<string, object>();
                            foreach (var k in Leer(tr, cor).Keys) e[k] = nombreReal;
                            if (e.Count == 0) throw new InvalidOperationException("No hay objetivos de superficie en el ámbito indicado.");
                            return e;
                        },
                        (tr, cor) =>
                        {
                            var idSu = BuscarSuperficie(tr, superficie);
                            foreach (var (bl, reg) in Ambito(cor))
                            {
                                var col = reg.GetTargets();
                                bool tocada = false;
                                foreach (Civ.SubassemblyTargetInfo info in col)
                                    if (TipoObjetivo(info) == "superficie") { info.TargetIds = new ObjectIdCollection { idSu }; tocada = true; }
                                if (tocada) reg.SetTargets(col);
                            }
                        },
                        "Poner la superficie '" + superficie + "' en todos los objetivos de superficie" + (lineaBase != null ? " de '" + lineaBase + "'" : " del corredor") + (region != null ? " región '" + region + "'" : ""));
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "dividir_region",
                Descripcion = "Divide una región en una progresiva: recorta la región hasta 'pk' y añade otra con el mismo ensamblaje, frecuencias y objetivos desde 'pk' hasta el fin anterior. No borra nada.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("linea_base", "string", "Nombre de la línea base", true),
                    P("region", "string", "Nombre o índice de la región", true),
                    P("pk", "number", "Progresiva de corte (dentro de la región)", true),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("dividir_region", a, ctx =>
                {
                    string lineaBase = Requerido(a, "linea_base"), region = Requerido(a, "region");
                    double pk = Num(a, "pk", double.NaN);
                    if (double.IsNaN(pk)) throw new ArgumentException("Falta el parámetro obligatorio 'pk'.");
                    string nombreNueva = null;

                    Dictionary<string, object> Leer(Transaction tr, Civ.Corridor cor)
                    {
                        var bl = BuscarLineaBase(cor, lineaBase);
                        var reg = BuscarRegion(bl, region);
                        var regiones = new List<string>();
                        foreach (Civ.BaselineRegion r in bl.BaselineRegions) regiones.Add(r.Name + " [" + N(r.StartStation) + "-" + N(r.EndStation) + "]");
                        return new Dictionary<string, object> { ["region"] = reg.Name, ["inicio"] = N(reg.StartStation), ["fin"] = N(reg.EndStation), ["n_regiones"] = bl.BaselineRegions.Count, ["regiones"] = regiones };
                    }
                    return CambiarCorredor(ctx, Requerido(a, "corredor"), Leer,
                        (tr, cor) =>
                        {
                            var bl = BuscarLineaBase(cor, lineaBase);
                            var reg = BuscarRegion(bl, region);
                            if (pk <= reg.StartStation + 1e-6 || pk >= reg.EndStation - 1e-6)
                                throw new ArgumentException("La progresiva " + pk + " debe estar estrictamente dentro de la región '" + reg.Name + "' (" + N(reg.StartStation) + "-" + N(reg.EndStation) + ").");
                            var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (Civ.BaselineRegion r in bl.BaselineRegions) nombres.Add(r.Name);
                            nombreNueva = reg.Name + " (2)";
                            for (int k = 3; nombres.Contains(nombreNueva); k++) nombreNueva = reg.Name + " (" + k + ")";
                            return new Dictionary<string, object> { ["fin"] = N(pk), ["n_regiones"] = bl.BaselineRegions.Count + 1 };
                        },
                        (tr, cor) =>
                        {
                            var bl = BuscarLineaBase(cor, lineaBase);
                            var reg = BuscarRegion(bl, region);
                            double finAntiguo = reg.EndStation;
                            // Si la API tiene un Split nativo se usa; si no, se recorta y se añade la segunda región
                            if (Api.IntentarInvocar(bl.BaselineRegions, out _, new[] { "Split", "SplitRegion" }, reg, pk)
                                || Api.IntentarInvocar(reg, out _, new[] { "Split", "SplitAt" }, pk))
                                return;
                            reg.EndStation = pk;
                            var nueva = bl.BaselineRegions.Add(nombreNueva, reg.AssemblyId, pk, finAntiguo);
                            foreach (var nombres in new[] { FrecTangentes, FrecCurvas, FrecEspirales, FrecPerfil })
                            {
                                var v = Api.Leer<double?>(reg, null, nombres);
                                if (v.HasValue) { try { Api.Asignar(nueva, v.Value, nombres); } catch (Exception ex) { ctx.Avisos.Add("No se copió la frecuencia " + nombres[0] + ": " + ex.Message); } }
                            }
                            try { nueva.SetTargets(reg.GetTargets()); }
                            catch (Exception ex) { ctx.Avisos.Add("No se copiaron los objetivos a la región nueva: " + ex.Message + ". Asígnalos con asignar_objetivo."); }
                        },
                        "Dividir la región '" + region + "' en la progresiva " + pk);
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "establecer_rango_region",
                Descripcion = "Cambia el inicio y/o el fin de una región. Falla si el rango solapa con otra región o sale de la línea base.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("linea_base", "string", "Nombre de la línea base", true),
                    P("region", "string", "Nombre o índice de la región", true),
                    P("inicio", "number", "Nueva progresiva inicial (si se omite, no cambia)"),
                    P("fin", "number", "Nueva progresiva final (si se omite, no cambia)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("establecer_rango_region", a, ctx =>
                {
                    string lineaBase = Requerido(a, "linea_base"), region = Requerido(a, "region");
                    if (!Tiene(a, "inicio") && !Tiene(a, "fin")) throw new ArgumentException("Indica 'inicio', 'fin' o ambos.");
                    double? inicio = Tiene(a, "inicio") ? Num(a, "inicio", 0) : (double?)null;
                    double? fin = Tiene(a, "fin") ? Num(a, "fin", 0) : (double?)null;
                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        (tr, cor) => { var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region); return new Dictionary<string, object> { ["region"] = reg.Name, ["inicio"] = N(reg.StartStation), ["fin"] = N(reg.EndStation) }; },
                        (tr, cor) =>
                        {
                            var bl = BuscarLineaBase(cor, lineaBase);
                            var reg = BuscarRegion(bl, region);
                            double i = inicio ?? reg.StartStation, f = fin ?? reg.EndStation;
                            ComprobarSolape(bl, reg, i, f);
                            return new Dictionary<string, object> { ["inicio"] = N(i), ["fin"] = N(f) };
                        },
                        (tr, cor) =>
                        {
                            var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region);
                            // Orden que evita rangos invertidos temporales
                            if (inicio.HasValue && inicio.Value >= reg.EndStation && fin.HasValue) { reg.EndStation = fin.Value; reg.StartStation = inicio.Value; }
                            else { if (inicio.HasValue) reg.StartStation = inicio.Value; if (fin.HasValue) reg.EndStation = fin.Value; }
                        },
                        "Cambiar el rango de la región '" + region + "' a " + (inicio?.ToString(CultureInfo.InvariantCulture) ?? "(igual)") + "-" + (fin?.ToString(CultureInfo.InvariantCulture) ?? "(igual)"));
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "establecer_frecuencia",
                Descripcion = "Cambia las frecuencias de aplicación del ensamblaje en una región: en tangentes, curvas, espirales y curvas del perfil (las que se indiquen).",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("linea_base", "string", "Nombre de la línea base", true),
                    P("region", "string", "Nombre o índice de la región", true),
                    P("tangentes", "number", "Frecuencia en tangentes"),
                    P("curvas", "number", "Frecuencia en curvas"),
                    P("espirales", "number", "Frecuencia en espirales"),
                    P("perfil", "number", "Frecuencia en curvas verticales del perfil"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("establecer_frecuencia", a, ctx =>
                {
                    string lineaBase = Requerido(a, "linea_base"), region = Requerido(a, "region");
                    var pedidas = new Dictionary<string, (string[] nombres, double valor)>();
                    if (Tiene(a, "tangentes")) pedidas["frecuencia_tangentes"] = (FrecTangentes, Num(a, "tangentes", 0));
                    if (Tiene(a, "curvas")) pedidas["frecuencia_curvas"] = (FrecCurvas, Num(a, "curvas", 0));
                    if (Tiene(a, "espirales")) pedidas["frecuencia_espirales"] = (FrecEspirales, Num(a, "espirales", 0));
                    if (Tiene(a, "perfil")) pedidas["frecuencia_perfil"] = (FrecPerfil, Num(a, "perfil", 0));
                    if (pedidas.Count == 0) throw new ArgumentException("Indica al menos una frecuencia: tangentes, curvas, espirales o perfil.");
                    foreach (var kv in pedidas) if (kv.Value.valor <= 0) throw new ArgumentException("La frecuencia '" + kv.Key + "' debe ser mayor que 0.");

                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        (tr, cor) =>
                        {
                            var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region);
                            return new Dictionary<string, object>
                            {
                                ["region"] = reg.Name,
                                ["frecuencia_tangentes"] = Frecuencia(reg, FrecTangentes),
                                ["frecuencia_curvas"] = Frecuencia(reg, FrecCurvas),
                                ["frecuencia_espirales"] = Frecuencia(reg, FrecEspirales),
                                ["frecuencia_perfil"] = Frecuencia(reg, FrecPerfil)
                            };
                        },
                        (tr, cor) => pedidas.ToDictionary(kv => kv.Key, kv => (object)N(kv.Value.valor)),
                        (tr, cor) =>
                        {
                            var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region);
                            foreach (var kv in pedidas) Api.Asignar(reg, kv.Value.valor, kv.Value.nombres);
                        },
                        "Cambiar frecuencias de la región '" + region + "': " + string.Join(", ", pedidas.Select(kv => kv.Key + "=" + kv.Value.valor.ToString(CultureInfo.InvariantCulture))));
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "agregar_estacion_region",
                Descripcion = "Añade una estación adicional (progresiva donde se aplica el ensamblaje) a una región.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("linea_base", "string", "Nombre de la línea base", true),
                    P("region", "string", "Nombre o índice de la región", true),
                    P("pk", "number", "Progresiva a añadir (dentro de la región)", true),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("agregar_estacion_region", a, ctx =>
                {
                    string lineaBase = Requerido(a, "linea_base"), region = Requerido(a, "region");
                    double pk = Num(a, "pk", double.NaN);
                    if (double.IsNaN(pk)) throw new ArgumentException("Falta el parámetro obligatorio 'pk'.");
                    List<double?> Leer(Civ.BaselineRegion reg) => EstacionesAdicionales(reg);
                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        (tr, cor) => { var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region); return new Dictionary<string, object> { ["region"] = reg.Name, ["estaciones_adicionales"] = Leer(reg) }; },
                        (tr, cor) =>
                        {
                            var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region);
                            if (pk < reg.StartStation - 1e-6 || pk > reg.EndStation + 1e-6)
                                throw new ArgumentException("La progresiva " + pk + " está fuera de la región '" + reg.Name + "' (" + N(reg.StartStation) + "-" + N(reg.EndStation) + ").");
                            var l = Leer(reg);
                            if (!l.Any(x => x.HasValue && Math.Abs(x.Value - pk) < 1e-6)) l.Add(N(pk));
                            return new Dictionary<string, object> { ["estaciones_adicionales"] = l.OrderBy(x => x).ToList() };
                        },
                        (tr, cor) =>
                        {
                            var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region);
                            // 1) colección AdditionalStations con Add; 2) método de la región; si nada existe, error con los miembros disponibles
                            if (Api.IntentarLeer(reg, out object col, EstacionesAdic) && col != null && Api.IntentarInvocar(col, out _, new[] { "Add" }, pk)) { }
                            else Api.Invocar(reg, new[] { "AddAdditionalStation", "AddStation" }, pk);
                        },
                        "Añadir la estación " + pk + " a la región '" + region + "'");
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "reconstruir_corredor",
                Descripcion = "Reconstruye un corredor (corridor.Rebuild()) y devuelve el tiempo empleado y si sigue desactualizado.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("reconstruir_corredor", a, ctx =>
                {
                    long ms = 0;
                    string nombre = Requerido(a, "corredor");
                    return CambiarCorredor(ctx, nombre,
                        (tr, cor) => new Dictionary<string, object> { ["esta_desactualizado"] = cor.IsOutOfDate },
                        (tr, cor) => new Dictionary<string, object> { ["esta_desactualizado"] = false },
                        (tr, cor) =>
                        {
                            var reloj = System.Diagnostics.Stopwatch.StartNew();
                            cor.Rebuild();
                            ms = reloj.ElapsedMilliseconds;
                            lock (UltimaReconstruccion) UltimaReconstruccion[cor.Name] = DateTime.Now;
                        },
                        "Reconstruir el corredor '" + nombre + "'",
                        () => new { ms });
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "agregar_superficie_corredor",
                Descripcion = "Crea una superficie de corredor (corridor.CorridorSurfaces.Add) y le añade un código de enlace (por defecto Top). Reconstruye después con reconstruir_corredor.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("nombre", "string", "Nombre de la superficie nueva", true),
                    P("codigo_enlace", "string", "Código de enlace inicial (por defecto Top)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("agregar_superficie_corredor", a, ctx =>
                {
                    string nombre = Requerido(a, "nombre"), codigo = Str(a, "codigo_enlace", "Top");
                    Dictionary<string, object> Leer(Transaction tr, Civ.Corridor cor)
                    {
                        var nombres = new List<string>(); Civ.CorridorSurface mia = null;
                        foreach (Civ.CorridorSurface cs in cor.CorridorSurfaces) { nombres.Add(cs.Name); if (string.Equals(cs.Name, nombre, StringComparison.OrdinalIgnoreCase)) mia = cs; }
                        return new Dictionary<string, object> { ["superficies"] = Ordenada(nombres), ["codigos_enlace"] = mia == null ? null : Codigos(mia, "enlace") };
                    }
                    return CambiarCorredor(ctx, Requerido(a, "corredor"), Leer,
                        (tr, cor) =>
                        {
                            var antes = Leer(tr, cor);
                            var lista = (List<string>)antes["superficies"];
                            if (lista.Contains(nombre, StringComparer.OrdinalIgnoreCase))
                                throw new ArgumentException("El corredor ya tiene una superficie llamada '" + nombre + "'. Usa agregar_codigo_superficie_corredor para añadirle códigos.");
                            var e = new Dictionary<string, object> { ["superficies"] = Ordenada(new List<string>(lista) { nombre }) };
                            // Los códigos solo se verifican si la API cargada expone LinkCodes (se comprueba en una superficie existente)
                            Civ.CorridorSurface alguna = null;
                            foreach (Civ.CorridorSurface cs in cor.CorridorSurfaces) { alguna = cs; break; }
                            if (alguna == null || Codigos(alguna, "enlace") != null) e["codigos_enlace"] = new List<string> { codigo };
                            else ctx.Avisos.Add("La API no expone LinkCodes; no se verifica el código de enlace añadido.");
                            return e;
                        },
                        (tr, cor) => { var cs = cor.CorridorSurfaces.Add(nombre); cs.AddLinkCode(codigo, true); },
                        "Crear la superficie de corredor '" + nombre + "' con el código de enlace '" + codigo + "'");
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "agregar_codigo_superficie_corredor",
                Descripcion = "Añade un código de enlace (AddLinkCode) o de punto (AddPointCode) a una superficie de corredor.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("superficie", "string", "Nombre de la superficie del corredor", true),
                    P("codigo", "string", "Código, por ejemplo Top, Datum, Pave, Daylight", true),
                    P("tipo", "string", "enlace (por defecto) o punto"),
                    P("como_linea_rotura", "boolean", "Para códigos de enlace: añadir como líneas de rotura (por defecto true)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("agregar_codigo_superficie_corredor", a, ctx =>
                {
                    string superficie = Requerido(a, "superficie"), codigo = Requerido(a, "codigo");
                    string tipo = (Str(a, "tipo", "enlace") ?? "enlace").Trim().ToLowerInvariant();
                    if (tipo == "link") tipo = "enlace"; if (tipo == "point") tipo = "punto";
                    if (tipo != "enlace" && tipo != "punto") throw new ArgumentException("El parámetro 'tipo' debe ser 'enlace' o 'punto'.");
                    bool rotura = Bool(a, "como_linea_rotura", true);
                    string clave = tipo == "enlace" ? "codigos_enlace" : "codigos_punto";
                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        (tr, cor) => { var cs = BuscarSuperficieCorredor(cor, superficie); return new Dictionary<string, object> { ["superficie"] = cs.Name, ["codigos_enlace"] = Codigos(cs, "enlace"), ["codigos_punto"] = Codigos(cs, "punto") }; },
                        (tr, cor) =>
                        {
                            var l = Codigos(BuscarSuperficieCorredor(cor, superficie), tipo);
                            if (l == null) { ctx.Avisos.Add("La API no expone los códigos de la superficie; no se verifica el código añadido."); return new Dictionary<string, object>(); }
                            if (!l.Contains(codigo, StringComparer.OrdinalIgnoreCase)) l.Add(codigo);
                            return new Dictionary<string, object> { [clave] = Ordenada(l) };
                        },
                        (tr, cor) => { var cs = BuscarSuperficieCorredor(cor, superficie); if (tipo == "enlace") cs.AddLinkCode(codigo, rotura); else cs.AddPointCode(codigo); },
                        "Añadir el código de " + tipo + " '" + codigo + "' a la superficie '" + superficie + "'");
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "agregar_contorno_superficie_corredor",
                Descripcion = "Añade un contorno a una superficie de corredor: talud_automatico (extensión del corredor), talud_por_linea_base o exterior_poligono (polilínea cerrada, por handle o nombre). Si el talud no cierra, indica usar exterior_poligono.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("superficie", "string", "Nombre de la superficie del corredor", true),
                    P("tipo", "string", "talud_automatico (por defecto), talud_por_linea_base o exterior_poligono"),
                    P("usar_como_exterior", "boolean", "Usar como contorno exterior (por defecto true)"),
                    P("poligono", "string", "Handle o nombre de la polilínea cerrada (para exterior_poligono)"),
                    P("nombre", "string", "Nombre del contorno (por defecto según el tipo)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("agregar_contorno_superficie_corredor", a, ctx =>
                {
                    string superficie = Requerido(a, "superficie");
                    string tipo = (Str(a, "tipo", "talud_automatico") ?? "talud_automatico").Trim().ToLowerInvariant();
                    if (tipo != "talud_automatico" && tipo != "talud_por_linea_base" && tipo != "exterior_poligono")
                        throw new ArgumentException("El parámetro 'tipo' debe ser 'talud_automatico', 'talud_por_linea_base' o 'exterior_poligono'.");
                    bool exterior = Bool(a, "usar_como_exterior", true);
                    string poligono = Str(a, "poligono");
                    string nombre = Str(a, "nombre", tipo == "exterior_poligono" ? "Contorno exterior" : tipo == "talud_por_linea_base" ? "Talud por línea base" : "Talud automático");
                    var db = ctx.Db;

                    ObjectId ResolverPoligono(Transaction tr)
                    {
                        if (string.IsNullOrWhiteSpace(poligono)) throw new ArgumentException("Para 'exterior_poligono' indica 'poligono' (handle o nombre de una polilínea cerrada).");
                        if (IntentarHandle(db, poligono, out ObjectId id)) return id;
                        var idFl = BuscarLineaCaracteristica(tr, db, poligono);
                        if (!idFl.IsNull) return idFl;
                        throw new ArgumentException("No se encontró la polilínea '" + poligono + "' (usa el handle hexadecimal o el nombre de una línea característica).");
                    }
                    List<string> NombresContornos(Civ.CorridorSurface cs) => Ordenada(Contornos(cs).Select(c => Api.Leer<string>(c, null, "nombre")));

                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        (tr, cor) => { var cs = BuscarSuperficieCorredor(cor, superficie); var c = NombresContornos(cs); return new Dictionary<string, object> { ["superficie"] = cs.Name, ["n_contornos"] = c.Count, ["contornos"] = c }; },
                        (tr, cor) =>
                        {
                            var cs = BuscarSuperficieCorredor(cor, superficie);
                            if (tipo == "exterior_poligono") ResolverPoligono(tr);
                            var c = NombresContornos(cs); c.Add(nombre);
                            return new Dictionary<string, object> { ["n_contornos"] = c.Count, ["contornos"] = Ordenada(c) };
                        },
                        (tr, cor) =>
                        {
                            var cs = BuscarSuperficieCorredor(cor, superficie);
                            object contorno;
                            try
                            {
                                if (tipo == "exterior_poligono")
                                    contorno = Api.Invocar(cs.Boundaries, new[] { "Add", "AddPolygonBoundary" }, nombre, ResolverPoligono(tr));
                                else if (tipo == "talud_automatico")
                                    contorno = Api.Invocar(cs.Boundaries, new[] { "AddCorridorExtentsBoundary", "AddAutomaticBoundary", "AddDaylightBoundary" }, nombre);
                                else
                                    contorno = Api.Invocar(cs.Boundaries, new[] { "AddBaselineExtentsBoundary", "AddBaselineBoundary", "AddCorridorExtentsBoundary" }, nombre);
                            }
                            catch (Exception ex) when (EsErrorContornoAbierto(ex))
                            {
                                throw new InvalidOperationException("El talud no cierra; usa 'exterior_poligono' con una polilínea cerrada. Detalle: " + ex.Message);
                            }
                            if (contorno != null)
                            {
                                try { Api.Asignar(contorno, exterior, "UseAsOuterBoundary", "IsOuterBoundary", "UseAsOuter"); }
                                catch (MissingMemberException) { ctx.Avisos.Add("La API no expone 'usar_como_exterior' en el contorno; se dejó el valor por defecto."); }
                            }
                        },
                        "Añadir el contorno '" + nombre + "' (" + tipo + ") a la superficie '" + superficie + "'");
                })
            });
        }

        /// <summary>True si el error de la API indica que el contorno automático (talud) no cierra: eBoundaryNotClosed, máscara no añadida...</summary>
        private static bool EsErrorContornoAbierto(Exception ex)
        {
            if (ex is MissingMemberException || ex is MissingMethodException || ex is ArgumentException) return false;
            string m = (ex.Message ?? "") + " " + ex.GetType().Name;
            return m.IndexOf("NotClosed", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("not closed", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("no cierra", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("mask", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("máscara", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("OpenBoundary", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("InvalidBoundary", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

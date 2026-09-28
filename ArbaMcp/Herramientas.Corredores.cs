using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ArbaMcp.Nucleo;
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
            if (o.IndexOf("Farthest", StringComparison.OrdinalIgnoreCase) >= 0 || o.IndexOf("Outside", StringComparison.OrdinalIgnoreCase) >= 0) return "exterior";
            if (o.IndexOf("Flattest", StringComparison.OrdinalIgnoreCase) >= 0 || o.IndexOf("Inside", StringComparison.OrdinalIgnoreCase) >= 0) return "interior";
            return string.IsNullOrEmpty(o) ? null : o.ToLowerInvariant();
        }

        private static string OpcionApi(string opcion)
        {
            switch ((opcion ?? "").Trim().ToLowerInvariant())
            {
                case "mas_cercano": case "más_cercano": case "cercano": case "nearest": return "Nearest";
                case "exterior": case "outside": case "farthest": return "Farthest";
                case "interior": case "inside": case "flattest": return "Flattest";
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

        private static Civ.Subassembly BuscarSubensamblaje(Transaction tr, ObjectId idEnsamblaje, string assemblyGroupName, string subassemblyName, string logicalName = null)
        {
            if (idEnsamblaje.IsNull) return null;
            try
            {
                if (!(tr.GetObject(idEnsamblaje, OpenMode.ForRead) is Civ.Assembly asm)) return null;
                Civ.Subassembly primerCandidato = null;
                Civ.Subassembly candidatoDesempate = null;
                int coincidencias = 0;

                foreach (Civ.AssemblyGroup g in asm.Groups)
                {
                    if (!string.IsNullOrEmpty(assemblyGroupName) && !string.Equals(g.Name, assemblyGroupName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (ObjectId id in g.GetSubassemblyIds())
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is Civ.Subassembly sub)
                        {
                            if (string.Equals(sub.Name, subassemblyName, StringComparison.OrdinalIgnoreCase))
                            {
                                coincidencias++;
                                if (primerCandidato == null) primerCandidato = sub;
                                if (!string.IsNullOrEmpty(logicalName) && ParametrosSubensamblaje(sub).ContainsKey(logicalName))
                                {
                                    candidatoDesempate = sub;
                                }
                            }
                        }
                    }
                }
                if (coincidencias > 1 && candidatoDesempate != null) return candidatoDesempate;
                return primerCandidato;
            }
            catch { return null; }
        }

        private static string LadoDeSubensamblaje(Transaction tr, ObjectId idEnsamblaje, string assemblyGroupName, string subassemblyName, string logicalName = null)
        {
            var sub = BuscarSubensamblaje(tr, idEnsamblaje, assemblyGroupName, subassemblyName, logicalName);
            return sub != null ? Lado(sub.Side) : null;
        }

        private static string GrupoDeSubensamblaje(Transaction tr, ObjectId idEnsamblaje, string subassemblyName)
        {
            if (idEnsamblaje.IsNull || string.IsNullOrEmpty(subassemblyName)) return null;
            try
            {
                if (!(tr.GetObject(idEnsamblaje, OpenMode.ForRead) is Civ.Assembly asm)) return null;
                foreach (Civ.AssemblyGroup g in asm.Groups)
                {
                    foreach (ObjectId id in g.GetSubassemblyIds())
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is Civ.Subassembly sub &&
                            string.Equals(sub.Name, subassemblyName, StringComparison.OrdinalIgnoreCase))
                            return g.Name;
                    }
                }
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

        private static double? Frecuencia(Civ.BaselineRegion reg, string tipo)
        {
            try
            {
                var s = reg.AppliedAssemblySetting;
                if (s == null) return null;
                double v = tipo switch
                {
                    "tangentes" => s.FrequencyAlongTangents,
                    "curvas" => s.FrequencyAlongCurves,
                    "espirales" => s.FrequencyAlongSpirals,
                    "perfil" => s.FrequencyAlongProfileCurves,
                    _ => 0
                };
                return N(v);
            }
            catch { return null; }
        }

        private static List<double?> EstacionesAdicionales(Civ.BaselineRegion reg)
        {
            var l = new List<double?>();
            try
            {
                foreach (double st in reg.AdditionalStations())
                    l.Add(N(st));
            }
            catch { }
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
            frecuencia_tangentes = Frecuencia(reg, "tangentes"),
            frecuencia_curvas = Frecuencia(reg, "curvas"),
            frecuencia_espirales = Frecuencia(reg, "espirales"),
            frecuencia_perfil = Frecuencia(reg, "perfil"),
            estaciones_adicionales = EstacionesAdicionales(reg)
        };

        private static object InfoObjetivo(Transaction tr, Civ.SubassemblyTargetInfo info, ObjectId idEnsamblaje)
        {
            string grupo = info.AssemblyGroupName ?? GrupoDeSubensamblaje(tr, idEnsamblaje, info.SubassemblyName);
            return new
            {
                subensamblaje = info.SubassemblyName,
                grupo = grupo,
                lado = LadoDeSubensamblaje(tr, idEnsamblaje, grupo, info.SubassemblyName, info.LogicalName),
                tipo = TipoObjetivo(info),
                parametro = info.LogicalName,
                objetivos = IdsObjetivo(info).Select(id => DescribirObjeto(tr, id)).ToList(),
                opcion_objetivo = OpcionObjetivo(info)
            };
        }

        /// <summary>Estado comparable de un objetivo (para antes/después).</summary>
        private static Dictionary<string, object> EstadoObjetivo(Transaction tr, Civ.SubassemblyTargetInfo info) => new Dictionary<string, object>
        {
            ["subensamblaje"] = info.SubassemblyName,
            ["tipo"] = TipoObjetivo(info),
            ["parametro"] = info.LogicalName,
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
                var porGrupo = candidatos.Select(c => new { info = c, grupo = c.AssemblyGroupName ?? GrupoDeSubensamblaje(tr, reg.AssemblyId, c.SubassemblyName), parametro = c.LogicalName }).ToList();
                if (porGrupo.Select(x => x.grupo).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                {
                    if (string.IsNullOrWhiteSpace(grupo))
                        throw new ArgumentException("Hay " + candidatos.Count + " subensamblajes llamados '" + subensamblaje + "' en la región '" + reg.Name + "'. Indica el parámetro 'grupo' con uno de: " + string.Join(", ", porGrupo.Select(x => x.grupo).Distinct()) + ".");
                    porGrupo = porGrupo.Where(x => string.Equals(x.grupo, grupo, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (porGrupo.Count == 0)
                        throw new ArgumentException("Ningún subensamblaje '" + subensamblaje + "' está en el grupo '" + grupo + "'. Grupos posibles: " + string.Join(", ", candidatos.Select(c => c.AssemblyGroupName ?? GrupoDeSubensamblaje(tr, reg.AssemblyId, c.SubassemblyName)).Distinct()) + ".");
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

        /// <summary>Códigos de enlace, de punto o de línea característica de una superficie de corredor.</summary>
        private static List<string> Codigos(Civ.CorridorSurface cs, string tipo)
        {
            if (cs == null) return null;
            try
            {
                if (tipo == "enlace") return Ordenada(cs.LinkCodes());
                if (tipo == "linea_caracteristica") return Ordenada(cs.FeatureLineCodes());
                return Ordenada(cs.PointCodes());
            }
            catch { return null; }
        }

        private static List<object> Contornos(Civ.CorridorSurface cs)
        {
            var l = new List<object>();
            try
            {
                foreach (Civ.CorridorSurfaceBoundary b in cs.Boundaries)
                    l.Add(new
                    {
                        nombre = b.Name,
                        tipo = b.BoundaryType.ToString(),
                        usar_como_exterior = (bool?)b.IsCorridorExtents
                    });
            }
            catch { }
            return l;
        }

        private static object InfoSuperficieCorredor(Transaction tr, Civ.CorridorSurface cs)
        {
            var idSu = cs.SurfaceId;
            bool? desact = null;
            if (!idSu.IsNull)
            {
                try { if (tr.GetObject(idSu, OpenMode.ForRead) is Civ.Surface s) desact = s.IsOutOfDate; } catch { }
            }
            return new
            {
                nombre = cs.Name,
                codigos_enlace = Codigos(cs, "enlace"),
                codigos_punto = Codigos(cs, "punto"),
                codigos_lineas_caracteristicas = Codigos(cs, "linea_caracteristica"),
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
                ctx.EsperarCopia();   // nunca se escribe sin copia terminada
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

        /// <summary>
        /// Patrón de escritura por lotes sobre un corredor (asignar_objetivos, establecer_frecuencias): un solo contexto
        /// de comando, una copia, una línea de log y una entrada de Deshacer. Primero lee 'antes' y calcula 'esperado' de
        /// todos los elementos sin tocar nada (lo que no existe va a 'fallidos' con su motivo); en simulación devuelve el
        /// plan por índice; si no, espera la copia, aplica 'cambiar' en orden a los válidos, confirma, relee 'despues' en
        /// otra transacción y verifica cada elemento (ArbaMcp.Nucleo.Lotes). Una excepción de la API que no sea de
        /// argumentos aborta el lote entero: la transacción se descarta y nada queda a medias.
        /// </summary>
        private static object LoteCorredor(Escritura.Contexto ctx, string corredor, int total,
            Func<Transaction, Civ.Corridor, int, Dictionary<string, object>> leer,
            Func<Transaction, Civ.Corridor, int, Dictionary<string, object>> esperar,
            Action<Transaction, Civ.Corridor, int> cambiar,
            Func<int, string> accion, string que)
        {
            var doc = ctx.Doc;
            var elementos = new List<ElementoLote>();
            for (int i = 0; i < total; i++) elementos.Add(new ElementoLote { Indice = i, Accion = accion(i) });
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var cor = (Civ.Corridor)tr.GetObject(BuscarCorredor(tr, corredor), ctx.Simular ? OpenMode.ForRead : OpenMode.ForWrite);
                // 1. Validar todo (antes y esperado) sin tocar nada; lo que no existe va a fallidos
                foreach (var e in elementos)
                {
                    try { e.Antes = leer(tr, cor, e.Indice); e.Esperado = esperar(tr, cor, e.Indice); }
                    catch (ArgumentException ex) { e.Error = ex.Message; }
                }
                if (ctx.Simular) { tr.Commit(); return Lotes.Simulacion(ctx.Herramienta, elementos, Lotes.ResumenSimulado(que, elementos), ctx.Avisos); }
                // 2. Aplicar en orden, con la copia terminada
                ctx.EsperarCopia();
                foreach (var e in elementos)
                {
                    if (e.Fallido) continue;
                    try { cambiar(tr, cor, e.Indice); }
                    catch (ArgumentException ex) { e.Error = ex.Message; }
                }
                tr.Commit();
            }
            // 3. Releer y verificar por índice
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var cor = (Civ.Corridor)tr.GetObject(BuscarCorredor(tr, corredor), OpenMode.ForRead);
                foreach (var e in elementos) if (!e.Fallido) e.Despues = leer(tr, cor, e.Indice);
                tr.Commit();
            }
            return Lotes.Resultado(ctx.Herramienta, elementos, Lotes.Resumen(que, elementos), ctx.Copia, ctx.Avisos);
        }

        // ------------------------------------------------------------------ frecuencias (establecer_frecuencia y establecer_frecuencias)
        /// <summary>Frecuencias pedidas en los argumentos (tangentes, curvas, espirales, perfil): al menos una y mayores que 0.</summary>
        private static Dictionary<string, double> LeerFrecuencias(JsonElement a)
        {
            var pedidas = new Dictionary<string, double>();
            if (Tiene(a, "tangentes")) pedidas["frecuencia_tangentes"] = Num(a, "tangentes", 0);
            if (Tiene(a, "curvas")) pedidas["frecuencia_curvas"] = Num(a, "curvas", 0);
            if (Tiene(a, "espirales")) pedidas["frecuencia_espirales"] = Num(a, "espirales", 0);
            if (Tiene(a, "perfil")) pedidas["frecuencia_perfil"] = Num(a, "perfil", 0);
            if (pedidas.Count == 0) throw new ArgumentException("Indica al menos una frecuencia: tangentes, curvas, espirales o perfil.");
            foreach (var kv in pedidas) if (kv.Value <= 0) throw new ArgumentException("La frecuencia '" + kv.Key + "' debe ser mayor que 0.");
            return pedidas;
        }

        private static Dictionary<string, object> EstadoFrecuencias(Civ.BaselineRegion reg) => new Dictionary<string, object>
        {
            ["region"] = reg.Name,
            ["frecuencia_tangentes"] = Frecuencia(reg, "tangentes"),
            ["frecuencia_curvas"] = Frecuencia(reg, "curvas"),
            ["frecuencia_espirales"] = Frecuencia(reg, "espirales"),
            ["frecuencia_perfil"] = Frecuencia(reg, "perfil")
        };

        private static void AplicarFrecuencias(Civ.BaselineRegion reg, Dictionary<string, double> pedidas)
        {
            var s = reg.AppliedAssemblySetting;
            if (pedidas.TryGetValue("frecuencia_tangentes", out double ft)) s.FrequencyAlongTangents = ft;
            if (pedidas.TryGetValue("frecuencia_curvas", out double fc)) s.FrequencyAlongCurves = fc;
            if (pedidas.TryGetValue("frecuencia_espirales", out double fe)) s.FrequencyAlongSpirals = fe;
            if (pedidas.TryGetValue("frecuencia_perfil", out double fp)) s.FrequencyAlongProfileCurves = fp;
        }

        private static string TextoFrecuencias(Dictionary<string, double> pedidas)
            => string.Join(", ", pedidas.Select(kv => kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture)));

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
                                    string lado = Lado(sub.Side);
                                    lados.Add(lado);
                                    subs.Add(new
                                    {
                                        nombre = sub.Name,
                                        // Subassembly no expone nombre de catálogo/tipo de componente en propiedad tipada directa
                                        tipo = Api.Leer<string>(sub, null, "MacroName", "SubassemblyName", "ClassName", "DefaultName") ?? sub.GetType().Name,
                                        lado,
                                        parametros = ParametrosSubensamblaje(sub)
                                    });
                                }
                                // AssemblyGroup no expone Side tipado en Civil 3D 2027; se deduce de los subensamblajes
                                string ladoGrupo = Api.Leer<object>(g, null, "Side") is object s ? Lado(s)
                                    : lados.Distinct().Count() == 1 ? lados[0] : lados.Count == 0 ? "ninguno" : "mixto";
                                grupos.Add(new { nombre = g.Name, lado = ladoGrupo, subensamblajes = subs });
                            }
                            lista.Add(new
                            {
                                nombre = asm.Name,
                                tipo = asm.Type.ToString(),
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

                            ObjectId idPrim = inter.IntersectionRoads.Count > 0 ? inter.IntersectionRoads[0].CenterlineAlignmentId : ObjectId.Null;
                            ObjectId idSec = inter.IntersectionRoads.Count > 1 ? inter.IntersectionRoads[1].CenterlineAlignmentId : ObjectId.Null;
                            Point3d loc = inter.Location;
                            double? pkPrim = null;
                            double? pkSec = null;
                            if (!idPrim.IsNull) pkPrim = Seguro<double?>(() => { double st = 0, o = 0; ((CivAlignment)tr.GetObject(idPrim, OpenMode.ForRead)).StationOffset(loc.X, loc.Y, ref st, ref o); return st; });
                            if (!idSec.IsNull) pkSec = Seguro<double?>(() => { double st = 0, o = 0; ((CivAlignment)tr.GetObject(idSec, OpenMode.ForRead)).StationOffset(loc.X, loc.Y, ref st, ref o); return st; });

                            // Corredor: propiedad tipada directa CorridorId
                            var idCor = inter.CorridorId;
                            var corredores = new List<string>();
                            var regiones = new List<object>();
                            foreach (ObjectId idC in CivilApplication.ActiveDocument.CorridorCollection)
                            {
                                if (!(tr.GetObject(idC, OpenMode.ForRead) is Civ.Corridor cor)) continue;
                                if (!idCor.IsNull && idC != idCor) continue;
                                bool implicado = idC == idCor;
                                foreach (Civ.Baseline bl in cor.Baselines)
                                {
                                    var idAl = bl.AlignmentId;
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
                                x = N(loc.X),
                                y = N(loc.Y),
                                corredor = corredores.Count == 1 ? corredores[0] : corredores.Count == 0 ? null : string.Join("; ", corredores),
                                tipo = inter.GradeRuleType.ToString(),
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
                Descripcion = "Lista los grupos de líneas de muestreo (de un alineamiento o de todos): número de líneas y rango de progresivas. Con fuentes=true añade las fuentes muestreadas (superficies y corredores); esa consulta obliga a Civil 3D a abrir el grupo para escritura y puede tardar en dibujos grandes.",
                Parametros =
                {
                    P("alineamiento", "string", "Nombre del alineamiento (si se omite, todos)"),
                    P("fuentes", "boolean", "Con true incluye las fuentes muestreadas de cada grupo (por defecto false)")
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string nombreAl = Str(a, "alineamiento");
                    bool conFuentes = Bool(a, "fuentes", false);
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
                                // GetSectionSources exige el grupo abierto para escritura: con ForRead Civil 3D 2027 aborta con
                                // "eNotOpenForWrite" (error interno, no una excepción capturable). Solo se consulta si se pide.
                                List<object> fuentes = null;
                                if (conFuentes)
                                {
                                    fuentes = new List<object>();
                                    try
                                    {
                                        if (!slg.IsWriteEnabled) slg.UpgradeOpen();
                                        foreach (Civ.SectionSource f in slg.GetSectionSources())
                                            fuentes.Add(new { nombre = NombreDe(tr, f.SourceId), tipo = f.SourceType.ToString(), muestreada = f.IsSampled });
                                    }
                                    catch (Exception ex) { fuentes.Add(new { nombre = (string)null, tipo = "error", muestreada = false, error = ex.Message }); }
                                }
                                lista.Add(new { nombre = slg.Name, alineamiento = al.Name, n_lineas = n, inicio = ini.HasValue ? N(ini.Value) : null, fin = fin.HasValue ? N(fin.Value) : null, fuentes });
                            }
                        }
                        // Herramienta de solo lectura: se descarta la transacción para no guardar nada aunque se haya abierto el grupo para escritura
                        tr.Abort();
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
                            if (!string.IsNullOrWhiteSpace(opcion)) e["opcion"] = OpcionApi(opcion) == "Nearest" ? "mas_cercano" : OpcionApi(opcion) == "Farthest" ? "exterior" : "interior";
                            return e;
                        },
                        (tr, cor) =>
                        {
                            var reg = BuscarRegion(BuscarLineaBase(cor, lineaBase), region);
                            var (coleccion, info) = BuscarObjetivoInfo(tr, reg, sub, tipo, grupo, parametro);
                            info.TargetIds = ResolverObjetivo(tr, db, tipo, objetivo, alPerfil);
                            if (!string.IsNullOrWhiteSpace(opcion) && Enum.TryParse<Civ.SubassemblyTargetToOption>(OpcionApi(opcion), true, out var opt)) info.TargetToOption = opt;
                            if (mismoLado.HasValue) info.UseSameSideTarget = mismoLado.Value;
                            reg.SetTargets(coleccion);
                        },
                        "Asignar '" + objetivo + "' como objetivo de " + tipo + " del subensamblaje '" + sub + "' en la región '" + region + "'");
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "asignar_objetivos",
                Descripcion = "Lote de asignar_objetivo: varias asignaciones de objetivos en un solo contexto de comando, con una copia de seguridad, una línea de log y una entrada de Deshacer. Valida todas antes de tocar nada; si un objeto no existe, esa asignación va a 'fallidos' y el resto se aplica. Antes de encadenar varias llamadas a asignar_objetivo, usa esta.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor (común a todo el lote)", true),
                    P("linea_base", "string", "Línea base por defecto para las asignaciones que no la indiquen"),
                    P("asignaciones", "json", "Arreglo JSON (como texto) con los argumentos de asignar_objetivo por elemento: linea_base, region, subensamblaje, tipo, objetivo y los opcionales alineamiento_del_perfil, opcion, mismo_lado, grupo, parametro. Ejemplo: [{\"region\":\"0\",\"subensamblaje\":\"DaylightGeneral - (Right)\",\"tipo\":\"superficie\",\"objetivo\":\"Terreno\"},{\"region\":\"0\",\"subensamblaje\":\"LaneSuperelevationAOR - (Left)\",\"tipo\":\"desplazamiento\",\"objetivo\":\"Borde izq\",\"opcion\":\"mas_cercano\"}]", true),
                    P("forzar", "boolean", "Permitir más de " + Lotes.Limite + " elementos en el lote"),
                    P("simular", "boolean", "Con true devuelve el plan por índice sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("asignar_objetivos", a, ctx =>
                {
                    string corredor = Requerido(a, "corredor");
                    var elementos = Lotes.LeerLista(a, "asignaciones", Bool(a, "forzar", false));
                    var items = new List<(string lineaBase, string region, string sub, string tipo, string objetivo, string alPerfil, string opcion, string grupo, string parametro, bool? mismoLado)>();
                    for (int i = 0; i < elementos.Count; i++)
                    {
                        var e = Lotes.ConDefectos(elementos[i], a, "linea_base");
                        try
                        {
                            string opcion = Str(e, "opcion");
                            if (!string.IsNullOrWhiteSpace(opcion)) OpcionApi(opcion);   // valida el valor antes de tocar nada
                            items.Add((Requerido(e, "linea_base"), Requerido(e, "region"), Requerido(e, "subensamblaje"), NormalizarTipoObjetivo(Requerido(e, "tipo")),
                                Requerido(e, "objetivo"), Str(e, "alineamiento_del_perfil"), opcion, Str(e, "grupo"), Str(e, "parametro"),
                                Tiene(e, "mismo_lado") ? Bool(e, "mismo_lado", false) : (bool?)null));
                        }
                        catch (ArgumentException ex) { throw new ArgumentException("asignaciones[" + i + "]: " + ex.Message); }
                    }
                    var db = ctx.Db;
                    return LoteCorredor(ctx, corredor, items.Count,
                        (tr, cor, i) =>
                        {
                            var it = items[i];
                            return EstadoObjetivo(tr, BuscarObjetivoInfo(tr, BuscarRegion(BuscarLineaBase(cor, it.lineaBase), it.region), it.sub, it.tipo, it.grupo, it.parametro).info);
                        },
                        (tr, cor, i) =>
                        {
                            var it = items[i];
                            var ids = ResolverObjetivo(tr, db, it.tipo, it.objetivo, it.alPerfil);
                            var e = new Dictionary<string, object> { ["objetivos"] = ids.Cast<ObjectId>().Select(id => TextoObjeto(tr, id)).ToList() };
                            if (!string.IsNullOrWhiteSpace(it.opcion)) e["opcion"] = OpcionApi(it.opcion) == "Nearest" ? "mas_cercano" : OpcionApi(it.opcion) == "Farthest" ? "exterior" : "interior";
                            return e;
                        },
                        (tr, cor, i) =>
                        {
                            var it = items[i];
                            var reg = BuscarRegion(BuscarLineaBase(cor, it.lineaBase), it.region);
                            var (coleccion, info) = BuscarObjetivoInfo(tr, reg, it.sub, it.tipo, it.grupo, it.parametro);
                            info.TargetIds = ResolverObjetivo(tr, db, it.tipo, it.objetivo, it.alPerfil);
                            if (!string.IsNullOrWhiteSpace(it.opcion) && Enum.TryParse<Civ.SubassemblyTargetToOption>(OpcionApi(it.opcion), true, out var opt)) info.TargetToOption = opt;
                            if (it.mismoLado.HasValue) info.UseSameSideTarget = it.mismoLado.Value;
                            reg.SetTargets(coleccion);
                        },
                        i => "Asignar '" + items[i].objetivo + "' como objetivo de " + items[i].tipo + " del subensamblaje '" + items[i].sub + "' en la región '" + items[i].region + "' de '" + items[i].lineaBase + "'",
                        "asignaciones");
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
                                    d[bl.Name + " / " + reg.Name + " / " + info.SubassemblyName + " / " + (info.LogicalName ?? "superficie")] = IdsObjetivo(info).Select(id => NombreDe(tr, id)).FirstOrDefault();
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
                            try
                            {
                                reg.Split(pk);
                                return;
                            }
                            catch { }
                            reg.EndStation = pk;
                            var nueva = bl.BaselineRegions.Add(nombreNueva, reg.AssemblyId, pk, finAntiguo);
                            var sReg = reg.AppliedAssemblySetting;
                            var sNueva = nueva.AppliedAssemblySetting;
                            if (sReg != null && sNueva != null)
                            {
                                sNueva.FrequencyAlongTangents = sReg.FrequencyAlongTangents;
                                sNueva.FrequencyAlongCurves = sReg.FrequencyAlongCurves;
                                sNueva.FrequencyAlongSpirals = sReg.FrequencyAlongSpirals;
                                sNueva.FrequencyAlongProfileCurves = sReg.FrequencyAlongProfileCurves;
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
                    var pedidas = LeerFrecuencias(a);

                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        (tr, cor) => EstadoFrecuencias(BuscarRegion(BuscarLineaBase(cor, lineaBase), region)),
                        (tr, cor) => pedidas.ToDictionary(kv => kv.Key, kv => (object)N(kv.Value)),
                        (tr, cor) => AplicarFrecuencias(BuscarRegion(BuscarLineaBase(cor, lineaBase), region), pedidas),
                        "Cambiar frecuencias de la región '" + region + "': " + TextoFrecuencias(pedidas));
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "establecer_frecuencias",
                Descripcion = "Lote de establecer_frecuencia: cambia las frecuencias de varias regiones en un solo contexto de comando, con una copia de seguridad, una línea de log y una entrada de Deshacer. Valida todo antes de tocar nada; una región inexistente va a 'fallidos' sin abortar el lote. Antes de encadenar varias llamadas a establecer_frecuencia, usa esta.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor (común a todo el lote)", true),
                    P("linea_base", "string", "Línea base por defecto para las regiones que no la indiquen"),
                    P("regiones", "json", "Arreglo JSON (como texto): por elemento, region (nombre o índice), linea_base opcional y las frecuencias a cambiar (tangentes, curvas, espirales, perfil; al menos una, mayores que 0). Ejemplo: [{\"region\":\"0\",\"tangentes\":10,\"curvas\":5},{\"region\":\"Región (2)\",\"linea_base\":\"BL - Eje\",\"perfil\":20}]", true),
                    P("forzar", "boolean", "Permitir más de " + Lotes.Limite + " elementos en el lote"),
                    P("simular", "boolean", "Con true devuelve el plan por índice sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("establecer_frecuencias", a, ctx =>
                {
                    string corredor = Requerido(a, "corredor");
                    var elementos = Lotes.LeerLista(a, "regiones", Bool(a, "forzar", false));
                    var items = new List<(string lineaBase, string region, Dictionary<string, double> pedidas)>();
                    for (int i = 0; i < elementos.Count; i++)
                    {
                        var e = Lotes.ConDefectos(elementos[i], a, "linea_base");
                        try { items.Add((Requerido(e, "linea_base"), Requerido(e, "region"), LeerFrecuencias(e))); }
                        catch (ArgumentException ex) { throw new ArgumentException("regiones[" + i + "]: " + ex.Message); }
                    }
                    return LoteCorredor(ctx, corredor, items.Count,
                        (tr, cor, i) => EstadoFrecuencias(BuscarRegion(BuscarLineaBase(cor, items[i].lineaBase), items[i].region)),
                        (tr, cor, i) => items[i].pedidas.ToDictionary(kv => kv.Key, kv => (object)N(kv.Value)),
                        (tr, cor, i) => AplicarFrecuencias(BuscarRegion(BuscarLineaBase(cor, items[i].lineaBase), items[i].region), items[i].pedidas),
                        i => "Cambiar frecuencias de la región '" + items[i].region + "' de '" + items[i].lineaBase + "': " + TextoFrecuencias(items[i].pedidas),
                        "regiones");
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
                            reg.AddStation(pk, "MCP");
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
                Descripcion = "Añade un código de enlace (AddLinkCode) o de línea característica (AddFeatureLineCode) a una superficie de corredor.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor", true),
                    P("superficie", "string", "Nombre de la superficie del corredor", true),
                    P("codigo", "string", "Código, por ejemplo Top, Datum, Pave, Daylight", true),
                    P("tipo", "string", "enlace (por defecto), linea_caracteristica o punto (alias de compatibilidad)"),
                    P("como_linea_rotura", "boolean", "Para códigos de enlace: añadir como líneas de rotura (por defecto true)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("agregar_codigo_superficie_corredor", a, ctx =>
                {
                    string superficie = Requerido(a, "superficie"), codigo = Requerido(a, "codigo");
                    string tipo = (Str(a, "tipo", "enlace") ?? "enlace").Trim().ToLowerInvariant();
                    if (tipo == "link") tipo = "enlace";
                    if (tipo == "point" || tipo == "punto")
                    {
                        tipo = "linea_caracteristica";
                        ctx.Avisos.Add("El tipo 'punto' se procesó como 'linea_caracteristica' (AddFeatureLineCode), ya que Civil 3D 2027 define las superficies de corredor por líneas características.");
                    }
                    if (tipo != "enlace" && tipo != "linea_caracteristica")
                        throw new ArgumentException("El parámetro 'tipo' debe ser 'enlace' o 'linea_caracteristica' (o 'punto' como alias).");
                    bool rotura = Bool(a, "como_linea_rotura", true);
                    string clave = tipo == "enlace" ? "codigos_enlace" : "codigos_lineas_caracteristicas";
                    return CambiarCorredor(ctx, Requerido(a, "corredor"),
                        (tr, cor) =>
                        {
                            var cs = BuscarSuperficieCorredor(cor, superficie);
                            return new Dictionary<string, object>
                            {
                                ["superficie"] = cs.Name,
                                ["codigos_enlace"] = Codigos(cs, "enlace"),
                                ["codigos_lineas_caracteristicas"] = Codigos(cs, "linea_caracteristica")
                            };
                        },
                        (tr, cor) =>
                        {
                            var l = Codigos(BuscarSuperficieCorredor(cor, superficie), tipo);
                            if (l == null) { ctx.Avisos.Add("La API no expone los códigos de la superficie; no se verifica el código añadido."); return new Dictionary<string, object>(); }
                            if (!l.Contains(codigo, StringComparer.OrdinalIgnoreCase)) l.Add(codigo);
                            return new Dictionary<string, object> { [clave] = Ordenada(l) };
                        },
                        (tr, cor) =>
                        {
                            var cs = BuscarSuperficieCorredor(cor, superficie);
                            if (tipo == "enlace") cs.AddLinkCode(codigo, rotura);
                            else cs.AddFeatureLineCode(codigo);
                        },
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
                    List<string> NombresContornos(Civ.CorridorSurface cs) => Ordenada(cs.Boundaries.BoundaryNames());

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
                            Civ.CorridorSurfaceBoundary contorno;
                            try
                            {
                                if (tipo == "exterior_poligono")
                                    contorno = cs.Boundaries.Add(nombre, ResolverPoligono(tr));
                                else
                                    contorno = cs.Boundaries.AddCorridorExtentsBoundary(nombre);
                            }
                            catch (Exception ex) when (EsErrorContornoAbierto(ex))
                            {
                                throw new InvalidOperationException("El talud no cierra; usa 'exterior_poligono' con una polilínea cerrada. Detalle: " + ex.Message);
                            }
                            if (contorno != null)
                            {
                                try { contorno.BoundaryType = exterior ? Civ.CorridorSurfaceBoundaryType.OutsideBoundary : Civ.CorridorSurfaceBoundaryType.InsideBoundary; }
                                catch { }
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

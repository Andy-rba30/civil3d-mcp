using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivProfile = Autodesk.Civil.DatabaseServices.Profile;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>Herramientas de geometría de ejes, superficies y exportación.</summary>
    public static partial class Herramientas
    {
        private const string CapaLineasRotura = "MCP_LINEAS_ROTURA";

        // ------------------------------------------------------------------ patrón de escritura sobre una superficie
        private static object CambiarSuperficie(Escritura.Contexto ctx, string nombre,
            Func<Transaction, CivSurface, Dictionary<string, object>> leer,
            Func<Transaction, CivSurface, Dictionary<string, object>> esperar,
            Action<Transaction, CivSurface> cambiar,
            string accion, Func<object> extra = null)
        {
            var doc = ctx.Doc;
            Dictionary<string, object> antes, esperado;
            using (ctx.Simular ? BloquearParaLeer(doc) : doc.LockDocument())   // simulación: contexto de aplicación y solo lectura, sin entrada de Deshacer
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var su = (CivSurface)tr.GetObject(BuscarSuperficie(tr, nombre), ctx.Simular ? OpenMode.ForRead : OpenMode.ForWrite);
                antes = leer(tr, su);
                esperado = esperar(tr, su);
                if (ctx.Simular) { tr.Commit(); return Escritura.Simulacion(ctx, antes, esperado, accion); }
                ctx.EsperarCopia();   // nunca se escribe sin copia terminada
                cambiar(tr, su);
                tr.Commit();
            }
            Dictionary<string, object> despues;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var su = (CivSurface)tr.GetObject(BuscarSuperficie(tr, nombre), OpenMode.ForRead);
                despues = leer(tr, su);
                tr.Commit();
            }
            return Escritura.Resultado(ctx, antes, despues, esperado, accion, extra?.Invoke());
        }

        private static bool? Desactualizada(CivSurface su) => su.IsOutOfDate;

        // ------------------------------------------------------------------ búsquedas para crear_superficie_tin (1.7.0)
        /// <summary>Id de la superficie con ese nombre o ObjectId.Null si no existe (para comprobar que un nombre está libre).</summary>
        private static ObjectId BuscarSuperficieONull(Transaction tr, string nombre)
        {
            foreach (ObjectId id in CivilApplication.ActiveDocument.GetSurfaceIds())
                if (tr.GetObject(id, OpenMode.ForRead) is CivSurface su && string.Equals(su.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return id;
            return ObjectId.Null;
        }

        /// <summary>Grupo de puntos del dibujo por nombre (CivilDocument.PointGroups); error con la lista de grupos si no existe.</summary>
        private static ObjectId BuscarGrupoPuntos(Transaction tr, string nombre)
        {
            var grupos = CivilApplication.ActiveDocument.PointGroups;
            var nombres = new List<string>();
            foreach (ObjectId id in grupos)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Civ.PointGroup pg)) continue;
                if (string.Equals(pg.Name, nombre, StringComparison.OrdinalIgnoreCase)) return id;
                nombres.Add(pg.Name);
            }
            throw new ArgumentException("No existe el grupo de puntos '" + nombre + "'. Grupos de puntos del dibujo: " + (nombres.Count == 0 ? "ninguno" : string.Join(", ", nombres))
                + ". En Civil 3D se ven en Espacio de herramientas → Prospector → Grupos de puntos.");
        }

        /// <summary>Estilo de superficie del dibujo por nombre (Styles.SurfaceStyles); error con la lista de estilos si no existe.</summary>
        private static ObjectId BuscarEstiloSuperficie(Transaction tr, string nombre)
        {
            var estilos = CivilApplication.ActiveDocument.Styles.SurfaceStyles;
            if (estilos.Contains(nombre)) return estilos[nombre];
            var nombres = new List<string>();
            foreach (ObjectId id in estilos)
            {
                var st = tr.GetObject(id, OpenMode.ForRead);
                string n = Api.Leer<string>(st, null, "Name", "DisplayName");
                if (!string.IsNullOrEmpty(n)) nombres.Add(n);
            }
            throw new ArgumentException("No existe el estilo de superficie '" + nombre + "' en el dibujo. Estilos disponibles: " + (nombres.Count == 0 ? "ninguno legible" : string.Join(", ", nombres))
                + ". En Civil 3D se ven en Espacio de herramientas → Configuración → Superficie → Estilos de superficie.");
        }

        /// <summary>Intervalos de curvas (menor, mayor) del estilo de superficie; null si no se pueden leer.</summary>
        private static (double? menor, double? mayor) IntervalosDelEstilo(Transaction tr, ObjectId idEstilo)
        {
            try
            {
                if (idEstilo.IsNull || !(tr.GetObject(idEstilo, OpenMode.ForRead) is Civ.Styles.SurfaceStyle st)) return (null, null);
                var cs = st.ContourStyle;
                return (cs.MinorContourInterval, cs.MajorContourInterval);
            }
            catch (Exception ex) { Historial.Registrar("crear_superficie_tin: no se pudieron leer los intervalos del estilo: " + ex.Message); return (null, null); }
        }

        /// <summary>Extensión y número de puntos de una superficie (Surface.GetGeneralProperties), con los mismos nombres que extension_superficie.</summary>
        private static Dictionary<string, object> ExtensionDe(CivSurface su, out long nPuntos)
        {
            var p = su.GetGeneralProperties();
            nPuntos = Convert.ToInt64(p.NumberOfPoints);
            return ExtensionPuntos.Diccionario(p.MinimumCoordinateX, p.MaximumCoordinateX, p.MinimumCoordinateY, p.MaximumCoordinateY, p.MinimumElevation, p.MaximumElevation);
        }

        /// <summary>Líneas características del corredor (líneas base principales y desplazadas) como (código, puntos).</summary>
        private static List<(string codigo, Point3dCollection puntos)> LineasCaracteristicasCorredor(Civ.Corridor cor)
        {
            var lista = new List<(string, Point3dCollection)>();
            foreach (Civ.Baseline bl in cor.Baselines)
            {
                var contenedores = new List<object> { Api.Leer<object>(bl, null, "MainBaselineFeatureLines") };
                contenedores.AddRange(Api.Lista(Api.Leer<object>(bl, null, "OffsetBaselineFeatureLinesCol", "OffsetBaselineFeatureLines")));
                foreach (var bfl in contenedores)
                {
                    if (bfl == null) continue;
                    foreach (var flc in Api.Lista(Api.Leer<object>(bfl, null, "FeatureLineCollectionMap")))
                    {
                        string codigo = Api.Leer<string>(flc, null, "CodeName");
                        foreach (var fl in Api.Lista(flc))
                        {
                            var pts = Api.Leer<Point3dCollection>(fl, null, "FeatureLinePoints");
                            if (pts != null && pts.Count >= 2) lista.Add((codigo ?? Api.Leer<string>(fl, "?", "CodeName"), pts));
                        }
                    }
                }
            }
            return lista;
        }

        private static ObjectId AsegurarCapa(Transaction tr, Database db, string nombre)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(nombre)) return lt[nombre];
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord { Name = nombre };
            var id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return id;
        }

        // ------------------------------------------------------------------ intersección de ejes por muestreo
        private static List<Point2d> MuestrearEje(CivAlignment al, double paso)
        {
            var pts = new List<Point2d>();
            double ini = al.StartingStation, fin = al.EndingStation;
            for (double s = ini; s < fin; s += paso)
            {
                double x = 0, y = 0;
                try { al.PointLocation(s, 0, ref x, ref y); pts.Add(new Point2d(x, y)); } catch { }
            }
            { double x = 0, y = 0; try { al.PointLocation(fin, 0, ref x, ref y); pts.Add(new Point2d(x, y)); } catch { } }
            return pts;
        }

        private static bool CruceSegmentos(Point2d p1, Point2d p2, Point2d q1, Point2d q2, out Point2d cruce)
        {
            cruce = default;
            double rx = p2.X - p1.X, ry = p2.Y - p1.Y, sx = q2.X - q1.X, sy = q2.Y - q1.Y;
            double den = rx * sy - ry * sx;
            if (Math.Abs(den) < 1e-12) return false;
            double qpx = q1.X - p1.X, qpy = q1.Y - p1.Y;
            double t = (qpx * sy - qpy * sx) / den, u = (qpx * ry - qpy * rx) / den;
            if (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9) return false;
            cruce = new Point2d(p1.X + t * rx, p1.Y + t * ry);
            return true;
        }

        private static List<Point2d> CrucesPorMuestreo(CivAlignment a, CivAlignment b, double paso)
        {
            var pa = MuestrearEje(a, paso); var pb = MuestrearEje(b, paso);
            const double celda = 25.0;
            var rejilla = new Dictionary<(long, long), List<int>>();
            (long, long) Celda(double x, double y) => ((long)Math.Floor(x / celda), (long)Math.Floor(y / celda));
            for (int j = 0; j + 1 < pb.Count; j++)
            {
                var (c1x, c1y) = Celda(Math.Min(pb[j].X, pb[j + 1].X), Math.Min(pb[j].Y, pb[j + 1].Y));
                var (c2x, c2y) = Celda(Math.Max(pb[j].X, pb[j + 1].X), Math.Max(pb[j].Y, pb[j + 1].Y));
                for (long cx = c1x; cx <= c2x; cx++)
                    for (long cy = c1y; cy <= c2y; cy++)
                    {
                        if (!rejilla.TryGetValue((cx, cy), out var l)) rejilla[(cx, cy)] = l = new List<int>();
                        l.Add(j);
                    }
            }
            var cruces = new List<Point2d>();
            for (int i = 0; i + 1 < pa.Count; i++)
            {
                var (c1x, c1y) = Celda(Math.Min(pa[i].X, pa[i + 1].X), Math.Min(pa[i].Y, pa[i + 1].Y));
                var (c2x, c2y) = Celda(Math.Max(pa[i].X, pa[i + 1].X), Math.Max(pa[i].Y, pa[i + 1].Y));
                var vistos = new HashSet<int>();
                for (long cx = c1x; cx <= c2x; cx++)
                    for (long cy = c1y; cy <= c2y; cy++)
                    {
                        if (!rejilla.TryGetValue((cx, cy), out var l)) continue;
                        foreach (int j in l)
                        {
                            if (!vistos.Add(j)) continue;
                            if (CruceSegmentos(pa[i], pa[i + 1], pb[j], pb[j + 1], out var c) && !cruces.Any(x => x.GetDistanceTo(c) < paso)) cruces.Add(c);
                        }
                    }
            }
            return cruces;
        }

        // ------------------------------------------------------------------ exportación por comando (excepción documentada en CONTRATO.md)
        private static async Task<object> ExportarPorComando(string herramienta, JsonElement a, string comandoPorDefecto)
        {
            string ruta = Path.GetFullPath(Requerido(a, "ruta"));
            string comando = Str(a, "comando", comandoPorDefecto).Trim();
            int timeoutMs = (int)(Math.Max(1, Num(a, "timeout_s", 300)) * 1000);
            // La línea de mcp_log.jsonl (resultado, tiempo, id_log) la escribe Herramientas.EjecutarAsyncRegistrando (1.6.0)
            var ctx = (Escritura.Contexto)await HiloPrincipal.Ejecutar(() => Escritura.Preparar(herramienta, a, conCopia: false), ContextoEjecucion.Aplicacion, herramienta);
            if (ctx.Simular)
                return new { simulado = true, herramienta, accion = "Enviar el comando " + comando + " y responder con la ruta " + ruta + " en la línea de comandos (FILEDIA=0 durante la orden)", ruta };

            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            var inicio = DateTime.Now.AddSeconds(-2);
            object filediaAnterior = await HiloPrincipal.Ejecutar(() =>
            {
                object v = AcApp.GetSystemVariable("FILEDIA");
                AcApp.SetSystemVariable("FILEDIA", (short)0);
                return v;
            }, ContextoEjecucion.Aplicacion, herramienta);
            object estado;
            try { estado = await EjecutarComando("_." + comando.TrimStart('_', '.') + "\n" + ruta, timeoutMs, false); }
            // Se restaura cuando Civil 3D vuelva a estar libre, es decir, cuando el comando de exportación haya terminado
            finally { _ = HiloPrincipal.Ejecutar(() => { try { AcApp.SetSystemVariable("FILEDIA", filediaAnterior); } catch { } return true; }, ContextoEjecucion.Aplicacion, herramienta + " (restaurar FILEDIA)"); }

            bool existe = File.Exists(ruta) && File.GetLastWriteTime(ruta) >= inicio;
            return new
            {
                estado,
                ruta,
                existe,
                bytes = existe ? new FileInfo(ruta).Length : 0,
                nota = existe ? null : "El archivo no apareció. Este comando abre cuadros de diálogo que el usuario debe completar en Civil 3D; usa capturar_pantalla para ver el diálogo. Es la excepción documentada en CONTRATO.md (sin API .NET de exportación)."
            };
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarSuperficies()
        {
            // ============================================================== lectura
            Registrar(new Herramienta
            {
                Nombre = "punto_a_pk",
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Proyecta un punto (x, y) sobre un alineamiento y devuelve progresiva, desplazamiento y lado (Alignment.StationOffset).",
                Parametros =
                {
                    P("alineamiento", "string", "Nombre del alineamiento", true),
                    P("x", "number", "Coordenada X (este)", true),
                    P("y", "number", "Coordenada Y (norte)", true)
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    double x = Num(a, "x", double.NaN), y = Num(a, "y", double.NaN);
                    if (double.IsNaN(x) || double.IsNaN(y)) throw new ArgumentException("Faltan los parámetros obligatorios 'x' e 'y'.");
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var al = (CivAlignment)tr.GetObject(BuscarAlineamiento(tr, Requerido(a, "alineamiento")), OpenMode.ForRead);
                        double pk = 0, off = 0;
                        try { al.StationOffset(x, y, ref pk, ref off); }
                        catch (Exception ex) { throw new InvalidOperationException("El punto (" + x + ", " + y + ") no proyecta perpendicularmente sobre el alineamiento '" + al.Name + "': " + ex.Message); }
                        tr.Commit();
                        return new { alineamiento = al.Name, pk = N(pk), desplazamiento = N(off), lado = Math.Abs(off) < 1e-6 ? "eje" : off > 0 ? "derecha" : "izquierda" };
                    }
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "pk_a_punto",
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Devuelve las coordenadas (x, y) de una progresiva y desplazamiento de un alineamiento (Alignment.PointLocation) y, si se indica 'perfil', la cota (Profile.ElevationAt).",
                Parametros =
                {
                    P("alineamiento", "string", "Nombre del alineamiento", true),
                    P("pk", "number", "Progresiva", true),
                    P("desplazamiento", "number", "Desplazamiento (positivo a la derecha; por defecto 0)"),
                    P("perfil", "string", "Nombre del perfil del que leer la cota")
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    double pk = Num(a, "pk", double.NaN), off = Num(a, "desplazamiento", 0);
                    if (double.IsNaN(pk)) throw new ArgumentException("Falta el parámetro obligatorio 'pk'.");
                    string perfil = Str(a, "perfil");
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var idAl = BuscarAlineamiento(tr, Requerido(a, "alineamiento"));
                        var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                        double x = 0, y = 0;
                        try { al.PointLocation(pk, off, ref x, ref y); }
                        catch (Exception ex) { throw new InvalidOperationException("La progresiva " + pk + " no está en el alineamiento '" + al.Name + "' (" + N(al.StartingStation) + "-" + N(al.EndingStation) + "): " + ex.Message); }
                        double? cota = null;
                        if (!string.IsNullOrWhiteSpace(perfil))
                        {
                            var pr = (CivProfile)tr.GetObject(BuscarPerfil(tr, idAl, perfil), OpenMode.ForRead);
                            try { cota = N(pr.ElevationAt(pk)); }
                            catch (Exception ex) { throw new InvalidOperationException("El perfil '" + pr.Name + "' no cubre la progresiva " + pk + " (" + N(pr.StartingStation) + "-" + N(pr.EndingStation) + "): " + ex.Message); }
                        }
                        tr.Commit();
                        return new { alineamiento = al.Name, pk = N(pk), desplazamiento = N(off), x = N(x), y = N(y), perfil, cota };
                    }
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "cota_superficie",
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Cota de una superficie en un punto (Surface.FindElevationAtXY); error si el punto queda fuera de la superficie.",
                Parametros =
                {
                    P("superficie", "string", "Nombre de la superficie", true),
                    P("x", "number", "Coordenada X", true),
                    P("y", "number", "Coordenada Y", true)
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    double x = Num(a, "x", double.NaN), y = Num(a, "y", double.NaN);
                    if (double.IsNaN(x) || double.IsNaN(y)) throw new ArgumentException("Faltan los parámetros obligatorios 'x' e 'y'.");
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var su = (CivSurface)tr.GetObject(BuscarSuperficie(tr, Requerido(a, "superficie")), OpenMode.ForRead);
                        double z;
                        try { z = su.FindElevationAtXY(x, y); }
                        catch (Exception ex) { throw new InvalidOperationException("El punto (" + x + ", " + y + ") está fuera de la superficie '" + su.Name + "': " + ex.Message); }
                        tr.Commit();
                        return new { superficie = su.Name, x = N(x), y = N(y), cota = N(z) };
                    }
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "extension_superficie",
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Extensión de una superficie (Surface.GetGeneralProperties): mínimos y máximos de X (este), Y (norte) y cota, centro, número de puntos y el sistema de coordenadas del dibujo (UnitZoneSettings.CoordinateSystemCode). Para llevar la superficie a Revit por coordenadas compartidas: pasa 'origen_sugerido' (centro redondeado) a set_project_coordinates del MCP de Revit y vincula el DWG con link_file placement='shared'. Avisa si la superficie supera los 16 km recomendados o los 33 km que admite Revit.",
                Parametros = { P("superficie", "string", "Nombre de la superficie", true) },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string nombre, tipo;
                    double minX, maxX, minY, maxY, minZ, maxZ;
                    long nPuntos;
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var su = (CivSurface)tr.GetObject(BuscarSuperficie(tr, Requerido(a, "superficie")), OpenMode.ForRead);
                        var p = su.GetGeneralProperties();
                        nombre = su.Name;
                        tipo = TipoSuperficie(su);
                        minX = p.MinimumCoordinateX; maxX = p.MaximumCoordinateX;
                        minY = p.MinimumCoordinateY; maxY = p.MaximumCoordinateY;
                        minZ = p.MinimumElevation; maxZ = p.MaximumElevation;
                        nPuntos = Convert.ToInt64(p.NumberOfPoints);
                        tr.Commit();
                    }

                    // Sistema de coordenadas y unidades del dibujo (Configuración del dibujo → Unidades y zona)
                    string codigo = null, unidades = null, nota = null;
                    try
                    {
                        var uz = CivilApplication.ActiveDocument.Settings.DrawingSettings.UnitZoneSettings;
                        codigo = uz.CoordinateSystemCode;
                        unidades = uz.DrawingUnits.ToString();
                    }
                    catch (Exception ex) { nota = "No se pudo leer el sistema de coordenadas del dibujo: " + ex.Message; }
                    if (string.IsNullOrWhiteSpace(codigo))
                    {
                        codigo = null;
                        nota = nota ?? "El dibujo no tiene sistema de coordenadas asignado; las coordenadas son las del WCS tal cual.";
                    }

                    // Límites de Revit: 16 km (10 millas) recomendados por Autodesk, 33 km (20 millas) de aviso y cambio de posición
                    double aMetros = string.Equals(unidades, "Feet", StringComparison.OrdinalIgnoreCase) ? 0.3048 : 1.0;
                    double anchoX = maxX - minX, altoY = maxY - minY;
                    double diagonal = Math.Sqrt(anchoX * anchoX + altoY * altoY);
                    double diagonalKm = diagonal * aMetros / 1000.0;
                    var avisos = new List<string>();
                    if (diagonalKm > 32.187)
                        avisos.Add("La superficie mide " + N(diagonalKm) + " km en diagonal: supera los 33 km (20 millas) que Revit admite; aunque se centre, Revit avisará y forzará 'centro con centro'. Recorta la superficie o el DWG antes de vincular.");
                    else if (diagonalKm > 16.093)
                        avisos.Add("La superficie mide " + N(diagonalKm) + " km en diagonal: supera los 16 km (10 millas) recomendados por Autodesk; puede perderse precisión en los bordes.");

                    double centroX = (minX + maxX) / 2, centroY = (minY + maxY) / 2, centroZ = (minZ + maxZ) / 2;
                    return new
                    {
                        superficie = nombre,
                        tipo_superficie = tipo,
                        n_puntos = nPuntos,
                        min_x = N(minX), max_x = N(maxX), min_y = N(minY), max_y = N(maxY), min_z = N(minZ), max_z = N(maxZ),
                        ancho_x = N(anchoX), alto_y = N(altoY), diagonal = N(diagonal),
                        centro = new { x = N(centroX), y = N(centroY), z = N(centroZ) },
                        origen_sugerido = new { este = Math.Round(centroX / 100) * 100, norte = Math.Round(centroY / 100) * 100, cota = Math.Round(centroZ / 10) * 10 },
                        sistema_coordenadas = new { codigo, unidades, nota },
                        avisos
                    };
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "interseccion_ejes",
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Puntos de cruce entre dos alineamientos con sus progresivas en cada uno (Entity.IntersectWith; si no devuelve nada, muestreo cada 0.5 m).",
                Parametros =
                {
                    P("alineamiento_a", "string", "Nombre del primer alineamiento", true),
                    P("alineamiento_b", "string", "Nombre del segundo alineamiento", true)
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    var lista = new List<object>();
                    string metodo;
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var alA = (CivAlignment)tr.GetObject(BuscarAlineamiento(tr, Requerido(a, "alineamiento_a")), OpenMode.ForRead);
                        var alB = (CivAlignment)tr.GetObject(BuscarAlineamiento(tr, Requerido(a, "alineamiento_b")), OpenMode.ForRead);
                        if (alA.ObjectId == alB.ObjectId) throw new ArgumentException("Indica dos alineamientos distintos.");

                        var puntos = new List<Point2d>();
                        metodo = "IntersectWith";
                        try
                        {
                            var pts = new Point3dCollection();
                            alA.IntersectWith(alB, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
                            foreach (Point3d p in pts) puntos.Add(new Point2d(p.X, p.Y));
                        }
                        catch (Exception ex) { Historial.Registrar("interseccion_ejes: IntersectWith falló (" + ex.Message + "); se muestrea"); puntos.Clear(); }
                        if (puntos.Count == 0) { metodo = "muestreo 0.5 m"; puntos = CrucesPorMuestreo(alA, alB, 0.5); }

                        foreach (var p in puntos)
                        {
                            double pkA = 0, offA = 0, pkB = 0, offB = 0;
                            try { alA.StationOffset(p.X, p.Y, ref pkA, ref offA); } catch { pkA = double.NaN; }
                            try { alB.StationOffset(p.X, p.Y, ref pkB, ref offB); } catch { pkB = double.NaN; }
                            lista.Add(new { x = N(p.X), y = N(p.Y), pk_a = N(pkA), pk_b = N(pkB) });
                        }
                        tr.Commit();
                    }
                    return new { metodo, n = lista.Count, cruces = lista };
                }
            });

            // ============================================================== escritura
            Registrar(new Herramienta
            {
                Nombre = "agregar_linea_rotura_superficie",
                Descripcion = "Añade a una superficie TIN, como líneas de rotura estándar, las líneas características de un corredor de un código de punto (por ejemplo Crown, ETW, Daylight) o los códigos de punto de una superficie del corredor. Crea polilíneas 3D en la capa MCP_LINEAS_ROTURA y las añade con BreaklinesDefinition.AddStandardBreaklines.",
                Parametros =
                {
                    P("superficie", "string", "Superficie TIN de destino", true),
                    P("corredor", "string", "Corredor del que tomar las líneas características", true),
                    P("superficie_corredor", "string", "Superficie del corredor cuyos códigos de punto se usan (si no se indica 'codigo')"),
                    P("codigo", "string", "Código(s) de punto separados por ';'"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("agregar_linea_rotura_superficie", a, ctx =>
                {
                    string corredor = Requerido(a, "corredor"), supCor = Str(a, "superficie_corredor"), codigo = Str(a, "codigo");
                    if (string.IsNullOrWhiteSpace(supCor) && string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("Indica 'codigo' o 'superficie_corredor'.");
                    var db = ctx.Db;
                    int nLineas = 0;

                    List<string> CodigosPedidos(Transaction tr)
                    {
                        if (!string.IsNullOrWhiteSpace(codigo)) return ListaTextos(a, "codigo");
                        var cor = (Civ.Corridor)tr.GetObject(BuscarCorredor(tr, corredor), OpenMode.ForRead);
                        var l = Codigos(BuscarSuperficieCorredor(cor, supCor), "punto") ?? new List<string>();
                        if (l.Count == 0) throw new ArgumentException("La superficie '" + supCor + "' del corredor no tiene códigos de punto; indica 'codigo' con los códigos de línea característica (por ejemplo Crown;ETW;Daylight).");
                        return l;
                    }
                    List<(string codigo, Point3dCollection puntos)> Lineas(Transaction tr)
                    {
                        var cor = (Civ.Corridor)tr.GetObject(BuscarCorredor(tr, corredor), OpenMode.ForRead);
                        var pedidos = new HashSet<string>(CodigosPedidos(tr), StringComparer.OrdinalIgnoreCase);
                        var todas = LineasCaracteristicasCorredor(cor);
                        var mias = todas.Where(l => pedidos.Contains(l.codigo)).ToList();
                        if (mias.Count == 0)
                            throw new ArgumentException("El corredor '" + cor.Name + "' no tiene líneas características con los códigos " + string.Join(";", pedidos) + ". Códigos disponibles: " + string.Join(", ", todas.Select(l => l.codigo).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)) + ". ¿Está reconstruido el corredor?");
                        return mias;
                    }

                    return CambiarSuperficie(ctx, Requerido(a, "superficie"),
                        (tr, su) =>
                        {
                            if (!(su is Civ.TinSurface tin)) throw new ArgumentException("La superficie '" + su.Name + "' no es TIN (" + su.GetType().Name + "); las líneas de rotura solo se añaden a superficies TIN.");
                            return new Dictionary<string, object> { ["superficie"] = su.Name, ["n_lineas_rotura"] = tin.BreaklinesDefinition.Count, ["esta_desactualizada"] = Desactualizada(su) };
                        },
                        (tr, su) => { nLineas = Lineas(tr).Count; return new Dictionary<string, object>(); },
                        (tr, su) =>
                        {
                            var tin = (Civ.TinSurface)su;
                            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                            var capa = AsegurarCapa(tr, db, CapaLineasRotura);
                            var ids = new ObjectIdCollection();
                            foreach (var (cod, pts) in Lineas(tr))
                            {
                                var pl = new Polyline3d(Poly3dType.SimplePoly, pts, false) { LayerId = capa };
                                ids.Add(ms.AppendEntity(pl));
                                tr.AddNewlyCreatedDBObject(pl, true);
                            }
                            int antes = tin.BreaklinesDefinition.Count;
                            tin.BreaklinesDefinition.AddStandardBreaklines(ids, 1.0, 0.0, 0.0, 0.0);
                            if (tin.BreaklinesDefinition.Count <= antes)
                                throw new InvalidOperationException("La definición de la superficie no aumentó tras AddStandardBreaklines (" + antes + " → " + tin.BreaklinesDefinition.Count + "); no se reintenta.");
                        },
                        "Añadir las líneas características del corredor '" + corredor + "' (" + (codigo ?? "códigos de punto de " + supCor) + ") como líneas de rotura",
                        () => new { polilineas_creadas = nLineas, capa = CapaLineasRotura });
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "pegar_superficie",
                Descripcion = "Pega una superficie de origen en una superficie TIN de destino (TinSurface.PasteSurface). Si el origen ya estaba pegado, solo reconstruye el destino para que tome los cambios (por ejemplo, la rasante tras reconstruir el corredor).",
                Parametros =
                {
                    P("superficie_destino", "string", "Superficie TIN que recibe el pegado", true),
                    P("superficie_origen", "string", "Superficie que se pega", true),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("pegar_superficie", a, ctx =>
                {
                    string origen = Requerido(a, "superficie_origen");
                    ObjectId idOrigen = ObjectId.Null;
                    bool operacionesLegibles = true;
                    bool YaPegada(CivSurface su)
                    {
                        if (!Api.IntentarLeer(su, out object ops, "Operations", "EditsDefinition", "Edits")) { operacionesLegibles = false; return false; }
                        foreach (var op in Api.Lista(ops))
                            if (op.GetType().Name.IndexOf("Paste", StringComparison.OrdinalIgnoreCase) >= 0
                                && Api.Leer<ObjectId>(op, ObjectId.Null, "SurfaceId", "PastedSurfaceId", "PasteSurfaceId") == idOrigen) return true;
                        return false;
                    }
                    bool yaPegada = false;
                    return CambiarSuperficie(ctx, Requerido(a, "superficie_destino"),
                        (tr, su) =>
                        {
                            idOrigen = BuscarSuperficie(tr, origen);
                            if (idOrigen == su.ObjectId) throw new ArgumentException("La superficie de origen y la de destino son la misma.");
                            if (!(su is Civ.TinSurface)) throw new ArgumentException("La superficie de destino '" + su.Name + "' no es TIN (" + su.GetType().Name + ").");
                            yaPegada = YaPegada(su);
                            return new Dictionary<string, object> { ["destino"] = su.Name, ["origen_pegado"] = yaPegada, ["esta_desactualizada"] = Desactualizada(su) };
                        },
                        (tr, su) =>
                        {
                            var e = new Dictionary<string, object>();
                            if (operacionesLegibles) e["origen_pegado"] = true;
                            else ctx.Avisos.Add("La API no permite leer las operaciones de la superficie; no se verifica el pegado, solo se ejecuta.");
                            if (Desactualizada(su).HasValue) e["esta_desactualizada"] = false;
                            return e;
                        },
                        (tr, su) =>
                        {
                            var tin = (Civ.TinSurface)su;
                            if (!yaPegada) tin.PasteSurface(idOrigen);
                            tin.Rebuild();
                        },
                        "Pegar la superficie '" + origen + "' en '" + Str(a, "superficie_destino") + "' y reconstruir (si ya estaba pegada, solo reconstruir)");
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "reconstruir_superficie",
                Descripcion = "Reconstruye una superficie (surface.Rebuild()) y devuelve si sigue desactualizada.",
                Parametros =
                {
                    P("superficie", "string", "Nombre de la superficie", true),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("reconstruir_superficie", a, ctx =>
                {
                    long ms = 0;
                    string nombre = Requerido(a, "superficie");
                    return CambiarSuperficie(ctx, nombre,
                        (tr, su) => new Dictionary<string, object> { ["superficie"] = su.Name, ["esta_desactualizada"] = Desactualizada(su) },
                        (tr, su) => Desactualizada(su).HasValue ? new Dictionary<string, object> { ["esta_desactualizada"] = false } : new Dictionary<string, object>(),
                        (tr, su) => { var reloj = System.Diagnostics.Stopwatch.StartNew(); su.Rebuild(); ms = reloj.ElapsedMilliseconds; },
                        "Reconstruir la superficie '" + nombre + "'",
                        () => new { ms });
                })
            });

            Registrar(new Herramienta
            {
                Nombre = "crear_superficie_tin",
                Descripcion = "Crea una superficie TIN nueva (TinSurface.Create) a partir de un grupo de puntos del dibujo (PointGroupsDefinition.AddPointGroup) o de un archivo de puntos de texto (PointFilesDefinition.AddPointFile con un formato de archivo de puntos del dibujo, por ejemplo 'PENZD (comma delimited)'), la reconstruye y devuelve puntos, triángulos y extensión. Indica exactamente uno: 'grupo_puntos' o 'archivo'. Si ya existe una superficie con ese nombre responde error sin tocar nada. Los intervalos de curvas de nivel viven en el estilo de superficie (no en la superficie): no se modifica ningún estilo; si se piden, la respuesta lo explica en 'avisos'.",
                Parametros =
                {
                    P("nombre", "string", "Nombre de la superficie nueva (no debe existir)", true),
                    P("grupo_puntos", "string", "Nombre de un grupo de puntos del dibujo (Prospector → Grupos de puntos). Excluyente con 'archivo'"),
                    P("archivo", "string", "Ruta completa, en la PC de Civil 3D, de un archivo de puntos de texto (PENZD, ENZ...). Excluyente con 'grupo_puntos'"),
                    P("formato_archivo", "string", "Nombre de un formato de archivo de puntos del dibujo, por ejemplo 'PENZD (comma delimited)'. Si falta, se elige uno por las columnas y el separador del archivo y se dice cuál"),
                    P("estilo", "string", "Estilo de superficie del dibujo. Si falta, el estilo por defecto del dibujo"),
                    P("intervalo_curvas_menor", "number", "Intervalo de curvas menores pedido (solo informativo: vive en el estilo, ver avisos)"),
                    P("intervalo_curvas_mayor", "number", "Intervalo de curvas mayores pedido (solo informativo: vive en el estilo, ver avisos)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada (ni copia): antes/despues previstos con el número de puntos del archivo o del grupo")
                },
                Ejecutar = a => Escritura.Ejecutar("crear_superficie_tin", a, ctx =>
                {
                    string nombre = Requerido(a, "nombre");
                    string grupo = Str(a, "grupo_puntos"), archivo = Str(a, "archivo"), formatoPedido = Str(a, "formato_archivo"), estilo = Str(a, "estilo");
                    string origen = SuperficieTin.Origen(grupo, archivo);
                    bool esArchivo = origen == SuperficieTin.OrigenArchivo;
                    double? menor = Tiene(a, "intervalo_curvas_menor") ? Num(a, "intervalo_curvas_menor", 0) : (double?)null;
                    double? mayor = Tiene(a, "intervalo_curvas_mayor") ? Num(a, "intervalo_curvas_mayor", 0) : (double?)null;
                    SuperficieTin.ValidarIntervalos(menor, mayor);
                    var db = ctx.Db;

                    // El archivo se lee aquí (ArbaMcp.Nucleo, sin Civil 3D): líneas de puntos, separador y extensión prevista
                    ResumenArchivoPuntos resumen = null;
                    if (esArchivo)
                    {
                        archivo = Path.GetFullPath(archivo);
                        resumen = ArchivoPuntos.Leer(archivo, formatoPedido);
                        if (resumen.LineasPuntos == 0)
                            throw new ArgumentException("El archivo '" + archivo + "' no tiene ninguna línea con coordenadas legibles (" + resumen.Lineas + " líneas, separador " + (resumen.SeparadorNombre ?? "no detectado") + "). Se esperan columnas como PENZD (punto, este, norte, cota, descripción) separadas por coma, punto y coma, tabulador o espacio.");
                    }

                    ObjectId idEstilo = ObjectId.Null, idGrupo = ObjectId.Null;
                    long puntosGrupo = 0, puntos = 0;
                    int triangulos = -1;
                    string formatoUsado = null, motivoFormato = null, estiloUsado = null;
                    Civ.PointFileFormatCollection formatos = null;
                    Civ.PointFileFormat formato = null;
                    Dictionary<string, object> extension = null;
                    (double? menor, double? mayor) delEstilo = (null, null);
                    string textoOrigen = esArchivo ? "el archivo '" + archivo + "'" : "el grupo de puntos '" + grupo + "'";

                    try
                    {
                        return Escritura.CrearObjeto(ctx, new Escritura.Creacion
                        {
                            Accion = "Crear la superficie TIN '" + nombre + "' desde " + textoOrigen + (string.IsNullOrWhiteSpace(estilo) ? " con el estilo por defecto del dibujo" : " con el estilo '" + estilo + "'") + " y reconstruirla",
                            Antes = tr =>
                            {
                                if (!BuscarSuperficieONull(tr, nombre).IsNull) throw new ArgumentException("Ya existe la superficie '" + nombre + "'. Usa listar_superficies.");
                                if (!string.IsNullOrWhiteSpace(estilo)) { idEstilo = BuscarEstiloSuperficie(tr, estilo); estiloUsado = estilo; delEstilo = IntervalosDelEstilo(tr, idEstilo); }
                                if (esArchivo)
                                {
                                    formatos = Civ.PointFileFormatCollection.GetPointFileFormats(db);
                                    var disponibles = new List<FormatoPuntos>();
                                    var porNombre = new Dictionary<string, Civ.PointFileFormat>(StringComparer.OrdinalIgnoreCase);
                                    foreach (Civ.PointFileFormat f in formatos)
                                    {
                                        disponibles.Add(new FormatoPuntos { Nombre = f.Name, Delimitador = Api.Leer<string>(f, null, "Delimiter") });
                                        if (!string.IsNullOrEmpty(f.Name)) porNombre[f.Name.Trim()] = f;
                                    }
                                    var elegido = SuperficieTin.ElegirFormato(disponibles, formatoPedido, resumen.Separador, resumen.Columnas, out motivoFormato);
                                    if (elegido == null) throw new ArgumentException(SuperficieTin.MensajeSinFormato(disponibles, resumen.Separador, resumen.Columnas));
                                    formato = porNombre[elegido.Nombre.Trim()];
                                    formatoUsado = elegido.Nombre;
                                    if (string.IsNullOrWhiteSpace(formatoPedido)) ctx.Avisos.Add("Formato de archivo de puntos '" + formatoUsado + "' " + motivoFormato + "; pasa 'formato_archivo' si el archivo usa otro orden de columnas.");
                                }
                                else
                                {
                                    idGrupo = BuscarGrupoPuntos(tr, grupo);
                                    var pg = (Civ.PointGroup)tr.GetObject(idGrupo, OpenMode.ForRead);
                                    puntosGrupo = Convert.ToInt64(pg.PointsCount);
                                    if (puntosGrupo == 0) throw new ArgumentException("El grupo de puntos '" + pg.Name + "' no tiene puntos; una superficie sin puntos no se puede construir. Revisa el grupo en Espacio de herramientas → Prospector → Grupos de puntos.");
                                }
                                return new Dictionary<string, object> { ["superficie"] = nombre, ["existe"] = false, ["puntos"] = 0L };
                            },
                            Prevision = tr =>
                            {
                                var p = new Dictionary<string, object>
                                {
                                    ["existe"] = true,
                                    ["puntos"] = esArchivo ? (long)resumen.LineasPuntos : puntosGrupo,
                                    ["origen"] = origen,
                                    ["estilo"] = estiloUsado ?? "(estilo por defecto del dibujo)"
                                };
                                if (esArchivo) { p["formato_archivo"] = formatoUsado; if (resumen.Extension != null) p["extension"] = resumen.Extension.ComoDiccionario(); }
                                string aviso = SuperficieTin.AvisoIntervalos(menor, mayor, estiloUsado ?? "por defecto", delEstilo.menor, delEstilo.mayor);
                                if (aviso != null) ctx.Avisos.Add(aviso);
                                return p;
                            },
                            Esperado = new Dictionary<string, object> { ["existe"] = true },
                            Crear = tr =>
                            {
                                var id = idEstilo.IsNull ? Civ.TinSurface.Create(db, nombre) : Civ.TinSurface.Create(nombre, idEstilo);
                                var tin = (Civ.TinSurface)tr.GetObject(id, OpenMode.ForWrite);
                                if (esArchivo) tin.PointFilesDefinition.AddPointFile(archivo, formato);
                                else tin.PointGroupsDefinition.AddPointGroup(idGrupo);
                                tin.Rebuild();
                                return id;
                            },
                            Despues = (tr, id) =>
                            {
                                var su = (CivSurface)tr.GetObject(id, OpenMode.ForRead);
                                extension = ExtensionDe(su, out puntos);
                                try { triangulos = ((Civ.TinSurface)su).GetTinProperties().NumberOfTriangles; }
                                catch (Exception ex) { ctx.Avisos.Add("No se pudo leer el número de triángulos (TinSurface.GetTinProperties): " + ex.Message); }
                                estiloUsado = Api.Leer<string>(su, estiloUsado, "StyleName") ?? estiloUsado;
                                if (idEstilo.IsNull)
                                {
                                    // Estilo por defecto: ahora sí se sabe cuál es, y se leen sus intervalos para el aviso
                                    var idSt = Api.Leer<ObjectId>(su, ObjectId.Null, "StyleId");
                                    delEstilo = IntervalosDelEstilo(tr, idSt);
                                    string aviso = SuperficieTin.AvisoIntervalos(menor, mayor, estiloUsado ?? "por defecto", delEstilo.menor, delEstilo.mayor);
                                    ctx.Avisos.RemoveAll(x => x.StartsWith("Intervalos de curvas pedidos"));
                                    if (aviso != null) ctx.Avisos.Add(aviso);
                                }
                                var d = new Dictionary<string, object>
                                {
                                    ["superficie"] = su.Name,
                                    ["existe"] = true,
                                    ["puntos"] = puntos,
                                    ["triangulos"] = triangulos < 0 ? (object)null : triangulos,
                                    ["origen"] = origen,
                                    ["estilo"] = estiloUsado,
                                    ["esta_desactualizada"] = Desactualizada(su),
                                    ["extension"] = extension
                                };
                                if (esArchivo) d["formato_archivo"] = formatoUsado;
                                return d;
                            },
                            Verificar = d =>
                            {
                                if (puntos <= 0)
                                    throw new InvalidOperationException("La superficie '" + nombre + "' se creó pero quedó sin puntos (0) tras añadir " + textoOrigen + " y reconstruir; no se reintenta. Revisa el formato del archivo o el grupo de puntos y deshaz con Ctrl+Z si no la quieres. despues=" + Nucleo.Json.Serializar(d));
                            },
                            Extra = () => new
                            {
                                nombre,
                                origen,
                                grupo_puntos = esArchivo ? null : grupo,
                                archivo = esArchivo ? archivo : null,
                                formato_archivo = formatoUsado,
                                formato_elegido_automaticamente = esArchivo && string.IsNullOrWhiteSpace(formatoPedido),
                                estilo = estiloUsado ?? "(estilo por defecto del dibujo)",
                                puntos,
                                triangulos = triangulos < 0 ? (object)null : triangulos,
                                extension,
                                intervalos_curvas = new
                                {
                                    pedido_menor = menor, pedido_mayor = mayor,
                                    aplicados = false,
                                    donde_viven = "SurfaceStyle.ContourStyle (estilo de superficie)",
                                    estilo_menor = delEstilo.menor.HasValue ? N(delEstilo.menor.Value) : null,
                                    estilo_mayor = delEstilo.mayor.HasValue ? N(delEstilo.mayor.Value) : null
                                },
                                archivo_resumen = resumen?.ComoDiccionario()
                            }
                        });
                    }
                    finally { try { formatos?.Dispose(); } catch { } }
                })
            });

            // ============================================================== exportación (por comando: no hay API .NET de exportación)
            Registrar(new Herramienta
            {
                Nombre = "exportar_landxml",
                Descripcion = "Exporta a LandXML enviando el comando LANDXMLOUT (no hay API .NET directa). El cuadro de selección de objetos lo debe completar el usuario en Civil 3D; los parámetros 'alineamientos' y 'superficies' son informativos para indicarle qué marcar. Devuelve si el archivo apareció.",
                Parametros =
                {
                    P("ruta", "string", "Ruta completa del .xml de salida", true),
                    P("alineamientos", "string", "Nombres de alineamientos separados por ';' (informativo)"),
                    P("superficies", "string", "Nombres de superficies separados por ';' (informativo)"),
                    P("timeout_s", "number", "Segundos máximos de espera (por defecto 300)"),
                    P("comando", "string", "Comando a enviar (por defecto LANDXMLOUT)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                EjecutarAsync = a => ExportarPorComando("exportar_landxml", a, "LANDXMLOUT")
            });

            Registrar(new Herramienta
            {
                Nombre = "exportar_imx",
                Descripcion = "Exporta a IMX enviando el comando de exportación (por defecto EXPORTIMX; no hay API .NET directa). Si el comando abre un cuadro de diálogo, el usuario debe completarlo. Devuelve si el archivo apareció.",
                Parametros =
                {
                    P("ruta", "string", "Ruta completa del .imx de salida", true),
                    P("timeout_s", "number", "Segundos máximos de espera (por defecto 300)"),
                    P("comando", "string", "Comando a enviar (por defecto EXPORTIMX)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                EjecutarAsync = a => ExportarPorComando("exportar_imx", a, "EXPORTIMX")
            });
        }
    }
}

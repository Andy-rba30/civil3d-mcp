using System;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// seccion_corredor (tarea 7.3, ronda E): la sección aplicada de un corredor en una progresiva. Es una LECTURA, como
    /// listar_corredores: contexto de aplicación con bloqueo de lectura, sin copia, sin 'simular' y sin entrada en el menú
    /// Deshacer. Por línea base lee Baseline.GetAppliedAssemblyAtStation, sus subensamblajes aplicados
    /// (AppliedAssembly.GetAppliedSubassemblies), los parámetros aplicados (AppliedSubassembly.Parameters) y los puntos
    /// (AppliedSubassembly.Points → CalculatedPoint.StationOffsetElevationToBaseline y CorridorCodes). Todos por verificar
    /// en Civil 3D (confirmados en el catálogo). Cada lectura va en su try: un miembro que falle va a 'avisos' y no tumba
    /// la respuesta. Las pendientes y la respuesta están en ArbaMcp.Nucleo.SeccionCorredor.
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaSeccionCorredor = "seccion_corredor";

        /// <summary>Lee un valor de la API; si lanza, anota el aviso (una vez) y devuelve 'def'.</summary>
        private static T LeerConAviso<T>(Func<T> f, T def, string que, List<string> avisos)
        {
            try { return f(); }
            catch (Exception ex)
            {
                string aviso = "No se pudo leer " + que + ": " + Raiz(ex).Message;
                if (!avisos.Contains(aviso)) avisos.Add(aviso);
                return def;
            }
        }

        /// <summary>Puntos calculados de un subensamblaje aplicado: (progresiva, desplazamiento, cota) respecto de la línea base y códigos.</summary>
        private static List<PuntoSeccion> PuntosAplicados(Civ.AppliedSubassembly aps, List<string> avisos)
        {
            var lista = new List<PuntoSeccion>();
            var puntos = LeerConAviso(() => aps.Points, null, "AppliedSubassembly.Points", avisos);
            if (puntos == null) return lista;
            var todos = LeerConAviso(() =>
            {
                var l = new List<Civ.CalculatedPoint>();
                foreach (Civ.CalculatedPoint cp in puntos) l.Add(cp);
                return l;
            }, new List<Civ.CalculatedPoint>(), "los puntos de AppliedSubassembly.Points (CalculatedPointCollection)", avisos);
            foreach (var cp in todos)
            {
                var p = new PuntoSeccion();
                var soe = LeerConAviso<Autodesk.AutoCAD.Geometry.Point3d?>(() => cp.StationOffsetElevationToBaseline, null, "CalculatedPoint.StationOffsetElevationToBaseline", avisos);
                if (soe.HasValue)
                {
                    p.Progresiva = soe.Value.X;       // por verificar: X = progresiva
                    p.Desplazamiento = soe.Value.Y;   // por verificar: Y = desplazamiento (negativo a la izquierda)
                    p.Cota = soe.Value.Z;             // por verificar: Z = cota
                }
                var codigos = LeerConAviso(() => cp.CorridorCodes, null, "CalculatedPoint.CorridorCodes", avisos);
                if (codigos != null)
                    p.Codigos = LeerConAviso(() =>
                    {
                        var l = new List<string>();
                        foreach (string c in codigos) if (!string.IsNullOrEmpty(c)) l.Add(c);
                        return l;
                    }, new List<string>(), "los códigos de CalculatedPoint.CorridorCodes (CorridorCodeCollection)", avisos);
                lista.Add(p);
            }
            return lista;
        }

        /// <summary>Parámetros aplicados de un subensamblaje (IAppliedSubassemblyParam): clave, nombre, valor, valor de diseño y si se cambió a mano.</summary>
        private static List<ParametroAplicado> ParametrosAplicados(Civ.AppliedSubassembly aps, List<string> avisos)
        {
            var lista = new List<ParametroAplicado>();
            var parametros = LeerConAviso(() => aps.Parameters, null, "AppliedSubassembly.Parameters", avisos);
            if (parametros == null) return lista;
            foreach (var p in parametros)
            {
                if (p == null) continue;
                lista.Add(new ParametroAplicado
                {
                    Clave = LeerConAviso(() => p.KeyName, null, "IAppliedSubassemblyParam.KeyName", avisos),
                    Nombre = LeerConAviso(() => p.DisplayName, null, "IAppliedSubassemblyParam.DisplayName", avisos),
                    Valor = LeerConAviso(() => p.ValueAsObject, null, "IAppliedSubassemblyParam.ValueAsObject", avisos),
                    ValorDiseno = LeerConAviso(() => p.DesignValueAsObject, null, "IAppliedSubassemblyParam.DesignValueAsObject", avisos),
                    Modificado = LeerConAviso<bool?>(() => p.IsOverriden, null, "IAppliedSubassemblyParam.IsOverriden", avisos)
                });
            }
            return lista;
        }

        /// <summary>Un subensamblaje aplicado: nombre, clase y lado del subensamblaje del ensamblaje, grupo, parámetros y puntos.</summary>
        private static SubensamblajeSeccion SubensamblajeAplicado(Transaction tr, Civ.AppliedSubassembly aps, ObjectId idEnsamblaje, List<string> avisos)
        {
            var s = new SubensamblajeSeccion();
            var idSub = LeerConAviso(() => aps.SubassemblyId, ObjectId.Null, "AppliedSubassembly.SubassemblyId", avisos);
            Civ.Subassembly sub = null;
            if (!idSub.IsNull) sub = LeerConAviso(() => tr.GetObject(idSub, OpenMode.ForRead) as Civ.Subassembly, null, "el subensamblaje de AppliedSubassembly.SubassemblyId", avisos);
            if (sub != null)
            {
                s.Nombre = LeerConAviso(() => sub.Name, null, "Subassembly.Name", avisos);
                s.Clase = LeerConAviso(() => sub.GeometryGenerator?.MacroOrClassName, null, "Subassembly.GeometryGenerator.MacroOrClassName", avisos);
                s.Lado = LeerConAviso(() => Lado(sub.Side), null, "Subassembly.Side", avisos);
                if (s.Nombre != null) s.Grupo = GrupoDeSubensamblaje(tr, idEnsamblaje, s.Nombre);
            }
            s.Parametros = ParametrosAplicados(aps, avisos);
            s.Puntos = PuntosAplicados(aps, avisos);
            return s;
        }

        /// <summary>Región de la línea base que contiene la progresiva (o null).</summary>
        private static Civ.BaselineRegion RegionEn(Civ.Baseline bl, double pk, List<string> avisos)
        {
            return LeerConAviso(() =>
            {
                foreach (Civ.BaselineRegion r in bl.BaselineRegions)
                    if (SeccionCorredor.EnRango(pk, r.StartStation, r.EndStation)) return r;
                return null;
            }, null, "las regiones de la línea base '" + bl.Name + "'", avisos);
        }

        /// <summary>La sección aplicada de una línea base en la progresiva (o en la calculada más cercana si en la pedida no hay).</summary>
        private static LineaBaseSeccion SeccionDeLineaBase(Transaction tr, Civ.Baseline bl, double pk)
        {
            var l = new LineaBaseSeccion();
            var av = l.Avisos;
            l.Nombre = LeerConAviso(() => bl.Name, null, "Baseline.Name", av);
            l.Alineamiento = NombreDe(tr, LeerConAviso(() => bl.AlignmentId, ObjectId.Null, "Baseline.AlignmentId", av));
            l.Perfil = NombreDe(tr, LeerConAviso(() => bl.ProfileId, ObjectId.Null, "Baseline.ProfileId", av));
            l.Inicio = LeerConAviso<double?>(() => bl.StartStation, null, "Baseline.StartStation", av);
            l.Fin = LeerConAviso<double?>(() => bl.EndStation, null, "Baseline.EndStation", av);

            var region = RegionEn(bl, pk, av);
            if (region != null)
            {
                l.Region = LeerConAviso(() => region.Name, null, "BaselineRegion.Name", av);
                l.Ensamblaje = NombreDe(tr, LeerConAviso(() => region.AssemblyId, ObjectId.Null, "BaselineRegion.AssemblyId", av));
            }
            else av.Add("La progresiva " + SeccionCorredor.Numero(pk) + " no cae en ninguna región de la línea base '" + l.Nombre + "' (ver listar_regiones).");

            var estaciones = LeerConAviso(() => bl.SortedStations(), null, "Baseline.SortedStations", av);
            var vecinas = SeccionCorredor.EstacionesVecinas(estaciones, pk);
            l.EstacionAnterior = vecinas.anterior;
            l.EstacionSiguiente = vecinas.siguiente;

            Civ.AppliedAssembly aa = LeerConAviso(() => bl.GetAppliedAssemblyAtStation(pk), null, "Baseline.GetAppliedAssemblyAtStation(" + SeccionCorredor.Numero(pk) + ")", av);
            double usada = pk;
            if (aa == null && vecinas.masCercana.HasValue && !vecinas.exacta)
            {
                usada = vecinas.masCercana.Value;
                aa = LeerConAviso(() => bl.GetAppliedAssemblyAtStation(usada), null, "Baseline.GetAppliedAssemblyAtStation(" + SeccionCorredor.Numero(usada) + ")", av);
                if (aa != null)
                    av.Add("En la progresiva " + SeccionCorredor.Numero(pk) + " no hay sección calculada: se usa la más cercana del corredor, " + SeccionCorredor.Numero(usada)
                        + " (las progresivas salen de la frecuencia de la región; ver listar_regiones).");
            }
            if (aa == null)
            {
                av.Add("La línea base '" + l.Nombre + "' no tiene ensamblaje aplicado en la progresiva " + SeccionCorredor.Numero(pk) + " (Baseline.GetAppliedAssemblyAtStation no devolvió nada).");
                return l;
            }
            l.ProgresivaUsada = usada;
            var idEnsamblaje = LeerConAviso(() => aa.AssemblyId, ObjectId.Null, "AppliedAssembly.AssemblyId", av);
            if (l.Ensamblaje == null) l.Ensamblaje = NombreDe(tr, idEnsamblaje);

            var aplicados = LeerConAviso(() => aa.GetAppliedSubassemblies(), null, "AppliedAssembly.GetAppliedSubassemblies", av);
            if (aplicados == null) return l;
            var lista = LeerConAviso(() =>
            {
                var l2 = new List<Civ.AppliedSubassembly>();
                foreach (Civ.AppliedSubassembly x in aplicados) l2.Add(x);
                return l2;
            }, new List<Civ.AppliedSubassembly>(), "los subensamblajes de AppliedSubassemblyCollection", av);
            foreach (var aps in lista)
                if (aps != null) l.Subensamblajes.Add(SubensamblajeAplicado(tr, aps, idEnsamblaje, av));
            return l;
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarSeccionCorredor()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaSeccionCorredor,
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Devuelve la sección aplicada de un corredor en una progresiva, por línea base: eje, perfil, región, ensamblaje y, de cada subensamblaje aplicado, su nombre, clase (MacroOrClassName), lado y grupo, sus parámetros aplicados (clave interna, nombre, valor, valor de diseño y si se cambió a mano) y sus puntos (desplazamiento, cota, progresiva y códigos), con la pendiente de cada tramo del perfil superior y, en cada carril, la pendiente del punto Crown al ETW ('carriles'). Pendientes en %, con signo al alejarse del eje (negativa si baja); desplazamiento negativo a la izquierda. Si en la progresiva no hay sección calculada, usa la más cercana del corredor y lo avisa. Sirve para comprobar si el corredor sigue el peralte del eje. Solo lee: no cambia el dibujo.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor; ver listar_corredores", true),
                    P("progresiva", "number", "Progresiva (m) en la que se lee la sección", true),
                    P("linea_base", "string", "Nombre de la línea base (si se omite, todas las que incluyen la progresiva)")
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string nombreCor = Requerido(a, "corredor").Trim();
                    if (!Tiene(a, "progresiva")) throw new ArgumentException("Falta el parámetro obligatorio 'progresiva'.");
                    double pk = Num(a, "progresiva", double.NaN);
                    if (double.IsNaN(pk) || double.IsInfinity(pk)) throw new ArgumentException("El parámetro 'progresiva' debe ser un número finito.");
                    string nombreLb = Str(a, "linea_base")?.Trim();

                    var r = new ResultadoSeccionCorredor { ProgresivaPedida = pk };
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        ObjectId idCor;
                        try { idCor = BuscarCorredor(tr, nombreCor); }
                        catch (ArgumentException ex) { throw new ArgumentException(ex.Message + " Usa listar_corredores."); }
                        var cor = (Civ.Corridor)tr.GetObject(idCor, OpenMode.ForRead);
                        r.Corredor = cor.Name;
                        r.EstaDesactualizado = LeerConAviso<bool?>(() => cor.IsOutOfDate, null, "Corridor.IsOutOfDate", r.Avisos);

                        var todas = new List<Civ.Baseline>();
                        foreach (Civ.Baseline bl in cor.Baselines) todas.Add(bl);
                        var rangos = todas.Select(bl => (nombre: Seguro(() => bl.Name, "?"), inicio: Seguro<double?>(() => bl.StartStation), fin: Seguro<double?>(() => bl.EndStation))).ToList();

                        List<Civ.Baseline> elegidas;
                        if (!string.IsNullOrWhiteSpace(nombreLb))
                        {
                            Civ.Baseline bl;
                            try { bl = BuscarLineaBase(cor, nombreLb); }
                            catch (ArgumentException ex) { throw new ArgumentException(ex.Message + " Usa listar_corredores."); }
                            int i = todas.IndexOf(bl);
                            var rango = i >= 0 ? rangos[i] : (nombre: bl.Name, inicio: Seguro<double?>(() => bl.StartStation), fin: Seguro<double?>(() => bl.EndStation));
                            if (!SeccionCorredor.EnRango(pk, rango.inicio, rango.fin))
                                throw new ArgumentException(SeccionCorredor.ErrorFueraDeRango(cor.Name, pk, new[] { rango }, bl.Name));
                            elegidas = new List<Civ.Baseline> { bl };
                        }
                        else
                        {
                            elegidas = todas.Where((bl, i) => SeccionCorredor.EnRango(pk, rangos[i].inicio, rangos[i].fin)).ToList();
                            if (elegidas.Count == 0) throw new ArgumentException(SeccionCorredor.ErrorFueraDeRango(cor.Name, pk, rangos));
                        }

                        foreach (var bl in elegidas) r.Lineas.Add(SeccionDeLineaBase(tr, bl, pk));
                        tr.Commit();
                    }
                    return r.ComoDiccionario();
                }
            });
        }
    }
}

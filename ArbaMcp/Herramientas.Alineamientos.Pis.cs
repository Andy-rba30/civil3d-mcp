using System;
using System.Collections.Generic;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// listar_pis (1.15.0): tabla de PI de un alineamiento existente. Es una LECTURA, como listar_pvis: contexto de
    /// aplicación con bloqueo de lectura, sin copia, sin 'simular' y sin entrada en el menú Deshacer. Lee las entidades
    /// del eje en orden (Alignment.Entities.GetEntityByOrder, ejecutado en 1.8.0), los puntos con Alignment.PointLocation
    /// (ejecutado en 1.2.2) y, como comprobación, el PI que da Civil 3D (AlignmentSubEntityArc.PIPoint, por verificar).
    /// El cálculo de la tabla y la respuesta están en ArbaMcp.Nucleo.TablaPi.
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaListarPis = "listar_pis";

        /// <summary>Punto del eje en una progresiva (desplazamiento 0) o null si Civil 3D no lo da.</summary>
        private static Punto2? PuntoDelEje(CivAlignment al, double pk)
        {
            double x = 0, y = 0;
            try
            {
                al.PointLocation(pk, 0, ref x, ref y);
                return new Punto2(x, y);
            }
            catch (Exception ex)
            {
                Historial.Registrar(HerramientaListarPis + ": no se pudo leer el punto del eje '" + al.Name + "' en la progresiva " + pk + " (Alignment.PointLocation): " + ex.Message);
                return null;
            }
        }

        /// <summary>PI de la curva según Civil 3D (primera subentidad, AlignmentSubEntityArc.PIPoint) o null si no se puede leer.</summary>
        private static Punto2? PiDeCivil3d(Civ.AlignmentArc arco)
        {
            try
            {
                if (arco.SubEntityCount < 1) return null;
                if (arco[0] is Civ.AlignmentSubEntityArc sub)
                {
                    var p = sub.PIPoint;
                    return new Punto2(p.X, p.Y);
                }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaListarPis + ": no se pudo leer el PI de Civil 3D (AlignmentSubEntityArc.PIPoint): " + ex.Message); }
            return null;
        }

        /// <summary>Entidades del eje en orden: tangentes y curvas con sus datos; cualquier otro tipo con su clase y, si se leen, sus progresivas.</summary>
        private static List<EntidadEje> EntidadesDelEje(CivAlignment al)
        {
            var lista = new List<EntidadEje>();
            var entidades = al.Entities;
            for (int i = 0; i < entidades.Count; i++)
            {
                var e = entidades.GetEntityByOrder(i);
                if (e is Civ.AlignmentArc arco)
                {
                    lista.Add(new EntidadEje
                    {
                        Tipo = EntidadEje.Curva,
                        ProgresivaInicio = arco.StartStation,
                        ProgresivaFin = arco.EndStation,
                        PuntoInicio = PuntoDelEje(al, arco.StartStation),
                        PuntoFin = PuntoDelEje(al, arco.EndStation),
                        Radio = arco.Radius,
                        DeflexionRad = arco.Delta,
                        Horario = arco.Clockwise,
                        ProgresivaPi = arco.PIStation,
                        PiCivil3d = PiDeCivil3d(arco)
                    });
                }
                else if (e is Civ.AlignmentLine linea)
                {
                    lista.Add(new EntidadEje
                    {
                        Tipo = EntidadEje.Tangente,
                        ProgresivaInicio = linea.StartStation,
                        ProgresivaFin = linea.EndStation,
                        PuntoInicio = PuntoDelEje(al, linea.StartStation),
                        PuntoFin = PuntoDelEje(al, linea.EndStation)
                    });
                }
                else
                {
                    // Espirales, SCS, STS...: no se inventan miembros; solo el nombre de la clase y, por reflexión, las progresivas
                    double? inicio = null, fin = null;
                    try { inicio = Api.Leer<double?>(e, null, "StartStation"); fin = Api.Leer<double?>(e, null, "EndStation"); } catch { }
                    lista.Add(new EntidadEje { Tipo = e?.GetType().Name ?? "desconocido", ProgresivaInicio = inicio, ProgresivaFin = fin });
                }
            }
            return lista;
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarPis()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaListarPis,
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Devuelve la tabla de PI de un alineamiento existente, una fila por PI en orden: número, progresiva del PI (número y texto 0+300.000), coordenadas x, y, deflexión (grados, grados-minutos-segundos y giro izquierda/derecha), radio, longitud y tangente de la curva y progresivas de su PC y su PT (vacíos si el PI no tiene curva: dos tangentes seguidas). El inicio y el fin del eje van aparte, en 'inicio' y 'fin'. Las espirales y otros tipos salen como fila con su tipo y el aviso 'tipo no soportado todavía'. La progresiva del PI de una curva es la de Civil 3D (PC + T) y sus coordenadas se calculan desde el PC y el PT; si Civil 3D da su propio punto del PI, se compara (diferencia_pi_civil3d) y una diferencia de más de 0,001 va a 'avisos'. Valores en unidades del dibujo. Solo lee: no cambia el dibujo.",
                Parametros = { P("alineamiento", "string", "Nombre del alineamiento (eje); ver listar_alineamientos", true) },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string nombreAl = Requerido(a, "alineamiento").Trim();
                    Dictionary<string, object> respuesta;
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var idAl = BuscarAlineamiento(tr, nombreAl);   // "No existe el alineamiento 'X'. Usa listar_alineamientos."
                        var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                        string unidades = null;
                        try { unidades = CivilApplication.ActiveDocument.Settings.DrawingSettings.UnitZoneSettings.DrawingUnits.ToString(); }
                        catch (Exception ex) { Historial.Registrar(HerramientaListarPis + ": no se pudieron leer las unidades del dibujo: " + ex.Message); }
                        double inicio = al.StartingStation, fin = al.EndingStation;
                        var tabla = TablaPi.Calcular(al.Name, EntidadesDelEje(al), inicio, fin, PuntoDelEje(al, inicio), PuntoDelEje(al, fin), unidades, al.Length);
                        respuesta = tabla.ComoDiccionario();
                        tr.Commit();
                    }
                    return respuesta;
                }
            });
        }
    }
}

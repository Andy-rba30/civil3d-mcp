using System;
using System.Collections.Generic;
using System.IO;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ArbaMcp
{
    /// <summary>Herramientas ligadas a la seguridad de escritura: log de llamadas, guardado y copias del dibujo.</summary>
    public static partial class Herramientas
    {
        private static void RegistrarSeguridad()
        {
            Registrar(new Herramienta
            {
                Nombre = "leer_log",
                Contexto = ContextoEjecucion.Inmediato,
                Descripcion = "Devuelve las últimas líneas de mcp_log.jsonl de la carpeta del dibujo activo: una por cada llamada de herramienta (tipo 'llamada': id, hora, herramienta, pedido, args, ok, ms, error, version) y una por cada aprobación (tipo 'aprobacion': id, hora, id_log, aprobado, comentario).",
                Parametros = { P("ultimas_n", "number", "Cantidad de líneas (por defecto 50)") },
                Ejecutar = a =>
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    return new { ruta = Escritura.RutaLog(doc), lineas = Escritura.LeerLog(doc, (int)Num(a, "ultimas_n", 50)) };
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "aprobar_resultado",
                Contexto = ContextoEjecucion.Inmediato,   // como leer_log: solo lee y escribe mcp_log.jsonl, no toca el dibujo ni deja entrada en Deshacer
                Descripcion = "Anota en mcp_log.jsonl que el usuario confirmó (o rechazó) el resultado de una llamada anterior, identificada por el id_log que devolvió. Añade una línea de tipo 'aprobacion' (hora, id_log, aprobado, comentario); no toca el dibujo, no hace copia de seguridad ni deja entrada en Deshacer. exportar_dataset (puente) solo exporta las llamadas aprobadas.",
                Parametros =
                {
                    P("id_log", "string", "El id_log que devolvió la llamada que se aprueba (también aparece como 'id' en leer_log)", true),
                    P("aprobado", "boolean", "true si el usuario confirmó que el resultado fue correcto; false si lo rechazó", true),
                    P("comentario", "string", "Palabras del usuario sobre el resultado (opcional)")
                },
                Ejecutar = a =>
                {
                    string idLog = Requerido(a, "id_log").Trim();
                    if (!Tiene(a, "aprobado")) throw new ArgumentException("Falta el parámetro obligatorio 'aprobado' (true o false).");
                    bool aprobado = Bool(a, "aprobado", false);
                    string comentario = Str(a, "comentario");
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    string ruta = Escritura.RutaLog(doc);

                    var llamada = Aprobaciones.BuscarLlamada(Escritura.LeerLineasLog(doc), idLog);
                    if (llamada == null) throw new ArgumentException(Aprobaciones.MensajeNoExiste(idLog, ruta));

                    var hora = DateTime.Now;
                    string id = RegistroEscritura.NuevoId(hora);
                    Escritura.AnexarLinea(doc, Aprobaciones.LineaAprobacion(id, hora, idLog, aprobado, comentario));
                    Historial.Registrar(Aprobaciones.LineaHistorial(idLog, llamada.Herramienta, aprobado, comentario));
                    return new
                    {
                        id,
                        id_log = idLog,
                        aprobado,
                        comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(),
                        herramienta = llamada.Herramienta,
                        hora_llamada = llamada.Hora,
                        pedido = llamada.Pedido,
                        llamada_ok = llamada.Ok,
                        ruta
                    };
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "guardar_dibujo",
                Descripcion = "Guarda el dibujo activo por API (equivale a QSAVE). Con 'como' lo guarda en otra ruta y esa ruta pasa a ser el dibujo activo (SaveAs con renombrado).",
                Parametros =
                {
                    P("como", "string", "Ruta completa del DWG de destino (opcional)"),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("guardar_dibujo", a, ctx =>
                {
                    var doc = ctx.Doc;
                    var db = ctx.Db;
                    string como = Str(a, "como");
                    string actual = Escritura.RutaDibujo(doc);
                    string destino;
                    if (string.IsNullOrWhiteSpace(como))
                    {
                        if (actual == null) throw new InvalidOperationException("El dibujo no se ha guardado nunca (no tiene ruta); indica 'como' con la ruta de destino.");
                        destino = actual;
                    }
                    else destino = Path.GetFullPath(como);
                    if (!destino.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) destino += ".dwg";

                    var antes = new Dictionary<string, object> { ["ruta"] = actual, ["existe"] = File.Exists(destino) };
                    var esperado = new Dictionary<string, object> { ["ruta"] = destino, ["existe"] = true };
                    if (ctx.Simular) return Escritura.Simulacion(ctx, antes, esperado, "Guardar el dibujo en " + destino);

                    Directory.CreateDirectory(Path.GetDirectoryName(destino));
                    var inicio = DateTime.Now.AddSeconds(-2);
                    using (doc.LockDocument())
                        db.SaveAs(destino, true, DwgVersion.Current, db.SecurityParameters);

                    var despues = new Dictionary<string, object> { ["ruta"] = Escritura.RutaDibujo(doc) ?? destino, ["existe"] = File.Exists(destino) };
                    if (File.Exists(destino) && File.GetLastWriteTime(destino) < inicio)
                        throw new InvalidOperationException("El archivo " + destino + " no se actualizó al guardar (su fecha es anterior a la llamada).");
                    return Escritura.Resultado(ctx, antes, despues, esperado, "Dibujo guardado en " + destino,
                        new { bytes = File.Exists(destino) ? new FileInfo(destino).Length : 0, dibujo_activo = doc.Name });
                }, conCopia: false)
            });

            Registrar(new Herramienta
            {
                Nombre = "guardar_copia",
                Descripcion = "Guarda una copia del dibujo activo en backups\\<nombre>_<fecha>_<sufijo>.dwg sin renombrar el dibujo (misma rutina que las copias automáticas de escritura).",
                Parametros =
                {
                    P("sufijo", "string", "Sufijo del nombre de la copia, por ejemplo 'antes_de_objetivos'", true),
                    P("simular", "boolean", "Con true devuelve lo que haría sin tocar nada")
                },
                Ejecutar = a => Escritura.Ejecutar("guardar_copia", a, ctx =>
                {
                    string sufijo = Requerido(a, "sufijo");
                    string carpeta = Path.Combine(Escritura.CarpetaDatos(ctx.Doc), "backups");
                    if (ctx.Simular)
                        return new { simulado = true, herramienta = ctx.Herramienta, accion = "Guardar una copia en " + carpeta + " con el sufijo '" + sufijo + "' y conservar las últimas " + Escritura.CopiasConservadas };
                    string ruta = Escritura.GuardarCopia(ctx.Doc, sufijo);
                    if (!File.Exists(ruta)) throw new InvalidOperationException("La copia no aparece en disco: " + ruta);
                    return new { simulado = false, herramienta = ctx.Herramienta, copia = ruta, bytes = new FileInfo(ruta).Length, copias_conservadas = Escritura.CopiasConservadas };
                }, conCopia: false)
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
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
                Descripcion = "Devuelve las últimas líneas de mcp_log.jsonl (una por llamada de escritura: hora, herramienta, args, ok, ms, error) de la carpeta del dibujo activo.",
                Parametros = { P("ultimas_n", "number", "Cantidad de líneas (por defecto 50)") },
                Ejecutar = a =>
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    return new { ruta = Escritura.RutaLog(doc), lineas = Escritura.LeerLog(doc, (int)Num(a, "ultimas_n", 50)) };
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

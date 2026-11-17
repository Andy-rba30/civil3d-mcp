using System;
using System.Collections.Generic;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    // Sin Roslyn: lo usa el plugin en el contexto general de Civil 3D (como CodigoCambios y CodigoResultado).

    /// <summary>
    /// ejecutar_codigo con solo_lectura=true (desde 1.17.6): textos de la respuesta y decisiones que no tocan AutoCAD, para
    /// probarlos sin Civil 3D. Un script de solo lectura corre en contexto de aplicación (como las lecturas), con el dibujo
    /// bloqueado para lectura (DocumentLockMode.Read) y la transacción abortada al final, sin copia de seguridad y sin
    /// entrada en Deshacer. Capas: 1) al compilar no pasa lo que escribe (CodigoReglas.CategoriaSoloLectura, el OpenMode y
    /// la Fachada); 2) el bloqueo de lectura (AutoCAD debe rechazar abrir para escritura: por verificar en Civil 3D);
    /// 3) F.Agregar, F.EscribirTexto y F.Abrir(id, true) lanzan; 4) la transacción del plugin se aborta siempre; 5) después
    /// se miran las cinco fuentes de CodigoCambios y DBMOD: cualquier cambio da error; 6) un intento de escribir que
    /// AutoCAD rechaza da un error claro (EsIntentoDeEscritura).
    /// </summary>
    public static class CodigoSoloLectura
    {
        /// <summary>Nombre del parámetro de ejecutar_codigo.</summary>
        public const string Parametro = "solo_lectura";

        /// <summary>Comienzo de los errores de la Fachada en modo solo lectura (los reconoce EsIntentoDeEscritura).</summary>
        public const string PrefijoFachada = "solo_lectura=true: ";

        /// <summary>
        /// Textos de los errores de AutoCAD de un intento de escribir que se impide: sin bloqueo de escritura
        /// (eLockViolation), un objeto abierto para lectura (eNotOpenForWrite) o ya abierto para lectura fuera de una
        /// transacción (eWasOpenedForRead). Se reconocen por el texto, sin distinguir mayúsculas, con o sin la «e».
        /// </summary>
        public static readonly string[] ErroresDeEscritura = { "LockViolation", "NotOpenForWrite", "WasOpenedForRead" };

        /// <summary>Lo que pasa con el dibujo en un error de un script de solo lectura (va detrás del error).</summary>
        public const string Sufijo = " No se tocó el dibujo: solo lectura (dibujo bloqueado para lectura y transacción abortada), sin copia de seguridad y sin entrada en Deshacer.";

        /// <summary>Qué hacer si el script necesita escribir.</summary>
        public const string SiNecesitaEscribir = " Si el script necesita escribir, quita solo_lectura y simula primero (simular=true).";

        /// <summary>Aviso si llega simular=false con solo_lectura=true.</summary>
        public const string AvisoSimularIgnorado = "solo_lectura=true: el script nunca escribe, así que simular=false no cambia nada (la transacción se aborta igual).";

        /// <summary>Texto de 'accion' en la respuesta de un script de solo lectura.</summary>
        public static string Accion(int lineas)
            => "Ejecutar un script de solo lectura de " + lineas + " línea(s): con el dibujo bloqueado para lectura y la transacción abortada al final, sin copia de seguridad y sin entrada en Deshacer.";

        /// <summary>Error de la Fachada cuando un script de solo lectura intenta escribir con ella (F.Agregar, F.EscribirTexto, F.Abrir(id, true)).</summary>
        public static string MensajeFachada(string que)
            => PrefijoFachada + que + " escribe en el dibujo o en un archivo y no está permitido en un script de solo lectura." + SiNecesitaEscribir;

        /// <summary>True si el mensaje de una excepción del script es un intento de escribir que se impidió.</summary>
        public static bool EsIntentoDeEscritura(string mensaje)
        {
            if (string.IsNullOrEmpty(mensaje)) return false;
            if (mensaje.StartsWith(PrefijoFachada, StringComparison.Ordinal)) return true;
            return ErroresDeEscritura.Any(e => mensaje.IndexOf(e, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Error de ejecutar_codigo cuando un script de solo lectura intenta escribir y se impide.</summary>
        public static string MensajeIntento(int? linea, string tipo, string mensaje)
            => "Con solo_lectura=true el script intentó escribir" + (linea.HasValue ? " en la línea " + linea.Value : "")
               + " y se impidió (" + (string.IsNullOrEmpty(tipo) ? "" : tipo + ": ") + (mensaje ?? "") + ")." + Sufijo + SiNecesitaEscribir;

        /// <summary>
        /// Error si, tras un script de solo lectura, alguna fuente vio objetos creados, modificados, borrados o temporales
        /// (antes de abortar la transacción). Null si no hay ninguno. 'objetos': hasta unos pocos, ya descritos.
        /// </summary>
        public static string MensajeCambios(int creados, int modificados, int borrados, int temporales, IEnumerable<string> objetos)
        {
            if (creados <= 0 && modificados <= 0 && borrados <= 0 && temporales <= 0) return null;
            var partes = new List<string>();
            if (creados > 0) partes.Add(creados + " creado(s)");
            if (modificados > 0) partes.Add(modificados + " modificado(s)");
            if (borrados > 0) partes.Add(borrados + " borrado(s)");
            if (temporales > 0) partes.Add(temporales + " temporal(es)");
            var lista = (objetos ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            return "Con solo_lectura=true el script no puede cambiar el dibujo, pero se detectaron objetos " + string.Join(", ", partes)
                + (lista.Count > 0 ? " (" + string.Join(", ", lista) + ")" : "") + ". La transacción se abortó: no se aplicó nada, sin copia de seguridad y sin entrada en Deshacer."
                + SiNecesitaEscribir;
        }

        /// <summary>
        /// Error si, después de abortar, DBMOD cambió o los eventos de la base de datos vieron objetos nuevos que nadie más
        /// vio: algo pudo quedar fuera de la transacción. Null si DBMOD no cambió (o no se pudo leer) y no hay objetos así.
        /// </summary>
        public static string MensajeTrasCerrar(int? dbmodAntes, int? dbmodDespues, IList<string> nuevosAlCerrar)
        {
            var partes = new List<string>();
            if (dbmodAntes.HasValue && dbmodDespues.HasValue && dbmodAntes.Value != dbmodDespues.Value)
                partes.Add("DBMOD pasó de " + dbmodAntes.Value + " a " + dbmodDespues.Value + " (Civil 3D marcó el dibujo como modificado)");
            int n = nuevosAlCerrar?.Count ?? 0;
            if (n > 0)
                partes.Add("los eventos de la base de datos vieron " + n + " objeto(s) nuevo(s) al abortar (" + string.Join(", ", nuevosAlCerrar.Take(10)) + ")");
            if (partes.Count == 0) return null;
            return "ATENCIÓN: en un script de solo lectura " + string.Join(" y ", partes) + ", aunque la transacción se abortó. Algo pudo quedar fuera de la transacción:"
                + " revisa el dibujo antes de guardarlo (o ciérralo sin guardar); esta llamada no hizo copia de seguridad.";
        }

        /// <summary>
        /// Aviso que sugiere solo_lectura (sin ella): el script no cambió nada y el compilador no reconoció ninguna escritura.
        /// Null en otro caso (con solo_lectura, si hubo cambios o si el script usa algo que escribe).
        /// </summary>
        public static string AvisoSugerencia(bool soloLectura, int escriturasReconocidas, int creados, int modificados, int borrados, int temporales)
        {
            if (soloLectura || escriturasReconocidas > 0) return null;
            if (creados > 0 || modificados > 0 || borrados > 0 || temporales > 0) return null;
            return "El script no cambió nada y no usa ninguna de las escrituras que el plugin reconoce: si solo lee, pásalo con solo_lectura=true la próxima vez"
                + " (corre con el dibujo bloqueado para lectura, sin copia de seguridad y sin entrada en Deshacer).";
        }
    }
}

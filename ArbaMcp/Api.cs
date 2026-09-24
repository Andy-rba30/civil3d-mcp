using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ArbaMcp
{
    /// <summary>
    /// Acceso por reflexión a los miembros de la API de Civil 3D cuyo nombre exacto cambia entre versiones
    /// (frecuencias de región, contornos de superficie de corredor, intersecciones...). Cada llamada busca el
    /// primer nombre que exista en los metadatos de AeccDbMgd.dll cargados en Civil 3D; si no existe ninguno,
    /// el error enumera los miembros disponibles para poder corregir el nombre sin adivinar.
    /// Los miembros estables de la API se usan directamente; este ayudante es solo para los dudosos.
    /// </summary>
    internal static class Api
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        /// <summary>True si el objeto tiene una propiedad o un método con ese nombre.</summary>
        public static bool Tiene(object o, string nombre)
        {
            if (o == null) return false;
            var t = o.GetType();
            return t.GetProperty(nombre, Flags) != null
                || t.GetMethods(Flags).Any(m => string.Equals(m.Name, nombre, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Lee la primera propiedad (o método sin parámetros) que exista. Si no hay ninguna o vale null, devuelve def.</summary>
        public static T Leer<T>(object o, T def, params string[] nombres)
        {
            if (!IntentarLeer(o, out object v, nombres) || v == null) return def;
            try { return (T)Convertir(v, typeof(T)); } catch { return def; }
        }

        /// <summary>Lee la primera propiedad (o método sin parámetros) que exista. Devuelve false si ninguna existe.</summary>
        public static bool IntentarLeer(object o, out object valor, params string[] nombres)
        {
            valor = null;
            if (o == null) return false;
            var t = o.GetType();
            foreach (var n in nombres)
            {
                var p = t.GetProperty(n, Flags);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0)
                {
                    try { valor = p.GetValue(o); }
                    catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
                    return true;
                }
                var m = t.GetMethod(n, Flags, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType != typeof(void))
                {
                    try { valor = m.Invoke(o, null); }
                    catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
                    return true;
                }
            }
            return false;
        }

        /// <summary>Asigna la primera propiedad que exista y devuelve su nombre. Lanza si ninguna existe o es de solo lectura.</summary>
        public static string Asignar(object o, object valor, params string[] nombres)
        {
            var t = o.GetType();
            foreach (var n in nombres)
            {
                var p = t.GetProperty(n, Flags);
                if (p == null) continue;
                if (!p.CanWrite) throw new InvalidOperationException("La propiedad " + t.Name + "." + p.Name + " es de solo lectura en esta versión de la API de Civil 3D.");
                try { p.SetValue(o, Convertir(valor, p.PropertyType)); }
                catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
                return p.Name;
            }
            throw new MissingMemberException(Mensaje(t, nombres));
        }

        /// <summary>Invoca el primer método que exista con alguno de esos nombres y ese número de parámetros.</summary>
        public static object Invocar(object o, string[] nombres, params object[] args)
        {
            if (!IntentarInvocar(o, out object r, nombres, args)) throw new MissingMethodException(Mensaje(o.GetType(), nombres));
            return r;
        }

        /// <summary>Como Invocar, pero devuelve false (sin lanzar) si no existe ningún método compatible.</summary>
        public static bool IntentarInvocar(object o, out object resultado, string[] nombres, params object[] args)
        {
            resultado = null;
            if (o == null) return false;
            var t = o.GetType();
            foreach (var n in nombres)
            {
                foreach (var m in t.GetMethods(Flags).Where(m => string.Equals(m.Name, n, StringComparison.OrdinalIgnoreCase)))
                {
                    var ps = m.GetParameters();
                    if (ps.Length != args.Length) continue;
                    var conv = new object[args.Length];
                    bool compatible = true;
                    for (int i = 0; i < ps.Length && compatible; i++)
                    {
                        try { conv[i] = Convertir(args[i], ps[i].ParameterType); }
                        catch { compatible = false; }
                    }
                    if (!compatible) continue;
                    try { resultado = m.Invoke(o, conv); }
                    catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
                    return true;
                }
            }
            return false;
        }

        /// <summary>Enumera una colección de la API como lista (vacía si el objeto no es enumerable).</summary>
        public static List<object> Lista(object coleccion)
        {
            var l = new List<object>();
            if (coleccion is IEnumerable e && !(coleccion is string)) foreach (var x in e) l.Add(x);
            return l;
        }

        /// <summary>Nombres de propiedades y métodos públicos del objeto, para diagnosticar.</summary>
        public static List<string> Miembros(object o) => o == null ? new List<string>() : MiembrosDeTipo(o.GetType());

        private static object Convertir(object v, Type destino)
        {
            if (v == null) return null;
            if (destino.IsInstanceOfType(v)) return v;
            var sub = Nullable.GetUnderlyingType(destino);
            if (sub != null) return Convertir(v, sub);
            if (destino.IsEnum)
            {
                if (!(v is string s)) return Enum.ToObject(destino, v);
                try { return Enum.Parse(destino, s, true); }
                catch (ArgumentException)
                {
                    throw new ArgumentException("'" + s + "' no es un valor de " + destino.Name + ". Valores posibles: " + string.Join(", ", Enum.GetNames(destino)) + ".");
                }
            }
            if (destino == typeof(string)) return v.ToString();
            return Convert.ChangeType(v, destino, CultureInfo.InvariantCulture);
        }

        private static string Mensaje(Type t, string[] nombres)
            => "La API de Civil 3D cargada no tiene ninguno de estos miembros en " + t.Name + ": " + string.Join(", ", nombres)
             + ". Miembros disponibles: " + string.Join(", ", MiembrosDeTipo(t));

        private static List<string> MiembrosDeTipo(Type t)
            => t.GetProperties(Flags).Select(p => p.Name)
                .Concat(t.GetMethods(Flags).Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object)).Select(m => m.Name + "()"))
                .Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

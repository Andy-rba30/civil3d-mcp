using System;
using System.Collections.Generic;
using System.Globalization;
using ExtractorCatalogo;

// Solo interpreta los parámetros y llama a Extractor.Extraer; toda la lógica está en Extractor.cs para que las
// pruebas (ArbaMcp.Pruebas) la puedan llamar directamente.
class Program
{
    static int Main(string[] args)
    {
        var carpetas = new List<string>();
        var patrones = new List<string>();
        string salida = null;
        bool soloPatrones = false;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Valor() { if (i + 1 >= args.Length) throw new ArgumentException("Falta el valor de " + a); return args[++i]; }
            switch (a)
            {
                case "--dll": carpetas.Add(Valor()); break;
                case "--salida": salida = Valor(); break;
                case "--incluir": patrones.Add(Valor()); break;
                case "--solo-incluir": soloPatrones = true; break;
                case "-h": case "--help": case "--ayuda": Ayuda(); return 0;
                default: Console.Error.WriteLine("Parámetro desconocido: " + a); Ayuda(); return 2;
            }
        }
        if (carpetas.Count == 0 || string.IsNullOrEmpty(salida)) { Ayuda(); return 2; }

        try
        {
            var r = Extractor.Extraer(carpetas, salida, patrones, soloPatrones);
            foreach (var m in r.Mensajes) Console.WriteLine(m);
            Console.WriteLine();
            Console.WriteLine("Ensamblados leídos : " + r.Ensamblados);
            Console.WriteLine("Tipos extraídos    : " + r.Tipos);
            Console.WriteLine("Métodos extraídos  : " + r.Metodos);
            Console.WriteLine("Propiedades        : " + r.Propiedades);
            Console.WriteLine("Archivos saltados  : " + r.ArchivosSaltados);
            if (r.Avisos > 0) Console.WriteLine("Avisos             : " + r.Avisos);
            Console.WriteLine("Archivo de salida  : " + r.Salida + " (" + Tamano(r.TamanoSalida) + ")");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }

    static string Tamano(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        return (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    static void Ayuda()
    {
        Console.WriteLine("Extractor del catálogo de la API (lee las DLL solo como metadatos, sin cargarlas).");
        Console.WriteLine("Uso:");
        Console.WriteLine("  dotnet run --project herramientas-dev/ExtractorCatalogo -- --dll <carpeta> [--dll <otra>] --salida <archivo.sqlite> [--incluir <patron>]...");
        Console.WriteLine("Parámetros:");
        Console.WriteLine("  --dll <carpeta>     Carpeta donde buscar las DLL (se puede repetir; se miran también sus subcarpetas).");
        Console.WriteLine("  --salida <archivo>  Archivo .sqlite que se crea (si existe, se sobrescribe).");
        Console.WriteLine("  --incluir <patron>  Añade otras DLL por patrón, por ejemplo \"Aecc*.dll\" (se puede repetir).");
        Console.WriteLine("  --solo-incluir      Procesa solo los patrones de --incluir, sin las DLL por defecto.");
        Console.WriteLine("Por defecto procesa las DLL que referencia el plugin: " + string.Join(", ", Extractor.DllPorDefecto) + ".");
        Console.WriteLine("Los archivos que no sean ensamblados .NET se saltan y se cuentan.");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Uso:");
            Console.WriteLine("  dotnet run --project herramientas-dev\\InspectC3D -- <NombreDeTipo> [<NombreDeTipo> ...]");
            Console.WriteLine("  dotnet run --project herramientas-dev\\InspectC3D -- --buscar <texto>");
            return;
        }

        string c3d = @"C:\Program Files\Autodesk\AutoCAD 2027";
        string c3dSub = Path.Combine(c3d, "C3D");
        string aca = Path.Combine(c3d, "ACA");
        string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        string winDesktop = Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(runtimeDir))!, "Microsoft.WindowsDesktop.App", Path.GetFileName(Path.TrimEndingDirectorySeparator(runtimeDir)));

        var dllFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddDllsFrom(string dir)
        {
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*.dll"))
                {
                    dllFiles.Add(f);
                }
            }
        }

        AddDllsFrom(runtimeDir);
        AddDllsFrom(winDesktop);
        AddDllsFrom(c3d);
        AddDllsFrom(c3dSub);
        AddDllsFrom(aca);

        var resolver = new PathAssemblyResolver(dllFiles);
        using var mlc = new MetadataLoadContext(resolver);

        string targetDll = Path.Combine(c3dSub, "AeccDbMgd.dll");
        if (!File.Exists(targetDll))
        {
            Console.Error.WriteLine("No se encontró el ensamblado: " + targetDll);
            return;
        }

        var asmC3D = mlc.LoadFromAssemblyPath(targetDll);
        Assembly asmAcad = null;
        string acadDll = Path.Combine(c3d, "acdbmgd.dll");
        if (File.Exists(acadDll)) asmAcad = mlc.LoadFromAssemblyPath(acadDll);

        if (args[0] == "--buscar")
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Indica el texto a buscar. Ejemplo: --buscar Target");
                return;
            }
            string query = args[1];
            Console.WriteLine($"Buscando tipos que contengan '{query}' en AeccDbMgd.dll:");
            foreach (var t in asmC3D.GetTypes().OrderBy(t => t.FullName))
            {
                if (t.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  {t.FullName}");
                }
            }
            return;
        }

        foreach (var typeName in args)
        {
            var types = asmC3D.GetTypes().Where(t => 
                string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (types.Count == 0 && asmAcad != null)
            {
                types = asmAcad.GetTypes().Where(t => 
                    string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (types.Count == 0)
            {
                Console.WriteLine($"\n[TIPO NO ENCONTRADO: {typeName}]");
                continue;
            }

            foreach (var t in types)
            {
                Console.WriteLine($"\n=======================================================");
                Console.WriteLine($"Tipo: {t.FullName} (Base: {t.BaseType?.FullName})");
                Console.WriteLine($"=======================================================");

                Console.WriteLine("\n--- Propiedades ---");
                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                             .OrderBy(p => p.Name);
                int pCount = 0;
                foreach (var p in props)
                {
                    pCount++;
                    string setter = p.CanWrite ? " { get; set; }" : " { get; }";
                    Console.WriteLine($"  {p.PropertyType.Name} {p.Name}{setter}");
                }
                if (pCount == 0) Console.WriteLine("  (Ninguna declarada)");
                if (t.IsEnum)
                {
                    Console.WriteLine("\n--- Valores del Enum ---");
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                    {
                        Console.WriteLine($"  {f.Name} = {f.GetRawConstantValue()}");
                    }
                }

                Console.WriteLine("\n--- Métodos ---");
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                               .Where(m => !m.IsSpecialName)
                               .OrderBy(m => m.Name);
                int mCount = 0;
                foreach (var m in methods)
                {
                    mCount++;
                    var pars = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({pars})");
                }
                if (mCount == 0) Console.WriteLine("  (Ninguno declarado)");

                Console.WriteLine("\n--- Eventos ---");
                var events = t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                              .OrderBy(e => e.Name);
                int eCount = 0;
                foreach (var ev in events)
                {
                    eCount++;
                    Console.WriteLine($"  {ev.EventHandlerType?.Name} {ev.Name}");
                }
                if (eCount == 0) Console.WriteLine("  (Ninguno declarado)");
            }
        }
    }
}

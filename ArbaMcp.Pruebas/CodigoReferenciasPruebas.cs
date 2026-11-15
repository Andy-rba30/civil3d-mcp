using System;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Referencias de ejecutar_codigo (1.17.3). En acad.exe la lista TRUSTED_PLATFORM_ASSEMBLIES trae los .exe y las DLL
    /// nativas de la carpeta de AutoCAD, y cada una daba un CS0009 «La imagen PE no contiene metadatos administrados» al
    /// compilar (informe de VALIDACION_116, paso 4a, 14/11/2026). Aquí no aparece solo: el runtime de las pruebas solo
    /// trae ensamblados .NET; por eso se fabrica un PE nativo.
    /// </summary>
    public class CodigoReferenciasPruebas
    {
        private const string Acad = @"C:\Program Files\Autodesk\AutoCAD 2027";
        private const string Runtime = @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.0";

        /// <summary>
        /// Escribe en un archivo temporal una imagen PE nativa mínima, como slidelib.exe: cabecera DOS con e_lfanew,
        /// firma "PE\0\0", cabecera COFF y cabecera opcional PE32+ con 16 directorios a cero; sin secciones ni cabecera CLI.
        /// </summary>
        internal static string PeNativo()
        {
            var bytes = new byte[512];
            using (var ms = new MemoryStream(bytes))
            using (var w = new BinaryWriter(ms))
            {
                // Cabecera DOS: "MZ" y, en 0x3C, dónde empieza la cabecera PE
                w.Write((ushort)0x5A4D);
                ms.Position = 0x3C;
                w.Write(0x40);
                // Firma "PE\0\0"
                ms.Position = 0x40;
                w.Write(0x00004550u);
                // Cabecera COFF (20 bytes)
                w.Write((ushort)0x8664);      // Machine: AMD64
                w.Write((ushort)0);           // NumberOfSections
                w.Write(0);                   // TimeDateStamp
                w.Write(0);                   // PointerToSymbolTable
                w.Write(0);                   // NumberOfSymbols
                w.Write((ushort)0xF0);        // SizeOfOptionalHeader: PE32+ con 16 directorios (240 bytes)
                w.Write((ushort)0x2022);      // Characteristics: imagen ejecutable, DLL, direcciones grandes
                // Cabecera opcional PE32+ (240 bytes)
                w.Write((ushort)0x20B);       // Magic: PE32+
                w.Write((byte)14);            // MajorLinkerVersion
                w.Write((byte)0);             // MinorLinkerVersion
                w.Write(0);                   // SizeOfCode
                w.Write(0);                   // SizeOfInitializedData
                w.Write(0);                   // SizeOfUninitializedData
                w.Write(0);                   // AddressOfEntryPoint
                w.Write(0);                   // BaseOfCode
                w.Write(0x180000000UL);       // ImageBase
                w.Write(0x1000);              // SectionAlignment
                w.Write(0x200);               // FileAlignment
                w.Write((ushort)6); w.Write((ushort)0);   // versión del sistema operativo
                w.Write((ushort)0); w.Write((ushort)0);   // versión de la imagen
                w.Write((ushort)6); w.Write((ushort)0);   // versión del subsistema
                w.Write(0);                   // Win32VersionValue
                w.Write(0x1000);              // SizeOfImage
                w.Write(0x200);               // SizeOfHeaders
                w.Write(0);                   // CheckSum
                w.Write((ushort)2);           // Subsystem: Windows GUI
                w.Write((ushort)0x160);       // DllCharacteristics
                w.Write(0x100000UL);          // SizeOfStackReserve
                w.Write(0x1000UL);            // SizeOfStackCommit
                w.Write(0x100000UL);          // SizeOfHeapReserve
                w.Write(0x1000UL);            // SizeOfHeapCommit
                w.Write(0);                   // LoaderFlags
                w.Write(16);                  // NumberOfRvaAndSizes
                // Los 16 directorios (dirección y tamaño) quedan a cero, también el 15.º, el de la cabecera CLI
            }
            string ruta = Path.Combine(Carpeta(), "nativo_" + Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllBytes(ruta, bytes);
            return ruta;
        }

        private static string Carpeta()
        {
            string dir = Path.Combine(Path.GetTempPath(), "arbamcp_pruebas_referencias");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Borrar(string ruta)
        {
            try { File.Delete(ruta); } catch { }
        }

        // ------------------------------------------------------------------ EsEnsambladoAdministrado
        [Fact]
        public void EnsambladosDotNet_SonAdministrados()
        {
            Assert.True(CodigoReferencias.EsEnsambladoAdministrado(typeof(object).Assembly.Location));   // System.Private.CoreLib
            Assert.True(CodigoReferencias.EsEnsambladoAdministrado(typeof(CodigoReferencias).Assembly.Location));   // ArbaMcp.Nucleo.dll
        }

        [Fact]
        public void PeNativoSinMetadatos_NoEsAdministrado()
        {
            string ruta = PeNativo();
            try
            {
                // Es una imagen PE válida (sus cabeceras se leen) sin metadatos administrados: el caso de slidelib.exe
                using (var flujo = File.OpenRead(ruta))
                using (var pe = new PEReader(flujo))
                {
                    var cabeceras = pe.PEHeaders;
                    Assert.NotNull(cabeceras.PEHeader);
                    Assert.Equal(PEMagic.PE32Plus, cabeceras.PEHeader.Magic);
                    Assert.Null(cabeceras.CorHeader);
                    Assert.False(pe.HasMetadata);
                }
                Assert.False(CodigoReferencias.EsEnsambladoAdministrado(ruta));
            }
            finally
            {
                Borrar(ruta);
            }
        }

        [Fact]
        public void ArchivoDeTexto_NoEsAdministrado()
        {
            string ruta = Path.Combine(Carpeta(), "texto_" + Guid.NewGuid().ToString("N") + ".dll");
            File.WriteAllText(ruta, "Esto no es una DLL, solo texto con extensión .dll.");
            try
            {
                Assert.False(CodigoReferencias.EsEnsambladoAdministrado(ruta));
            }
            finally
            {
                Borrar(ruta);
            }
        }

        [Fact]
        public void RutaQueNoExisteNulaOVacia_NoEsAdministrado()
        {
            Assert.False(CodigoReferencias.EsEnsambladoAdministrado(Path.Combine(Carpeta(), "no_existe_" + Guid.NewGuid().ToString("N") + ".dll")));
            Assert.False(CodigoReferencias.EsEnsambladoAdministrado(null));
            Assert.False(CodigoReferencias.EsEnsambladoAdministrado(""));
        }

        // ------------------------------------------------------------------ DentroDe y FiltrarPlataforma
        [Fact]
        public void DentroDe_ExigeElSeparadorTrasLaCarpeta()
        {
            Assert.True(CodigoReferencias.DentroDe(Acad + @"\slidelib.exe", Acad));
            Assert.True(CodigoReferencias.DentroDe(Acad + @"\C3D\AeccDbMgd.dll", Acad));
            Assert.False(CodigoReferencias.DentroDe(Acad + @" X\a.dll", Acad));
            Assert.False(CodigoReferencias.DentroDe(Acad, Acad));
            Assert.False(CodigoReferencias.DentroDe(Acad + @"\a.dll", null));
            Assert.False(CodigoReferencias.DentroDe(null, Acad));
        }

        [Fact]
        public void Filtrar_QuitaLoDeLaCarpetaDeAutoCad_YDejaLoDemas()
        {
            var tpa = new[]
            {
                Runtime + @"\System.Runtime.dll",
                Acad + @"\slidelib.exe",
                Acad + @"\accoreconsole.exe",
                Acad + @"\w3dtk_wt.dll",
                Acad + @"\acdbmgd.dll",
                Acad + @"\C3D\AeccDbMgd.dll",
                @"C:\Program Files\Autodesk\AutoCAD 2027 X\otra.dll",
                @"C:\Otra\Cosa.dll",
                "",
            };
            var r = CodigoReferencias.FiltrarPlataforma(tpa, Acad, Runtime).ToList();
            Assert.Equal(new[] { Runtime + @"\System.Runtime.dll", @"C:\Program Files\Autodesk\AutoCAD 2027 X\otra.dll", @"C:\Otra\Cosa.dll" }, r);
        }

        [Fact]
        public void Filtrar_SinDistinguirMayusculasNiBarras()
        {
            var tpa = new[]
            {
                @"c:\program files\autodesk\autocad 2027\SLIDELIB.EXE",
                "C:/Program Files/Autodesk/AutoCAD 2027/accoreconsole.exe",
                Runtime + @"\System.Linq.dll",
            };
            var esperado = new[] { Runtime + @"\System.Linq.dll" };
            Assert.Equal(esperado, CodigoReferencias.FiltrarPlataforma(tpa, Acad, Runtime).ToList());
            Assert.Equal(esperado, CodigoReferencias.FiltrarPlataforma(tpa, Acad + @"\", Runtime).ToList());
            Assert.Equal(esperado, CodigoReferencias.FiltrarPlataforma(tpa, "C:/Program Files/Autodesk/AutoCAD 2027", Runtime).ToList());
            Assert.Equal(esperado, CodigoReferencias.FiltrarPlataforma(tpa, @"C:\PROGRAM FILES\AUTODESK\AUTOCAD 2027", Runtime).ToList());
        }

        [Fact]
        public void Filtrar_RuntimeDentroDeAutoCad_NoQuitaNada()
        {
            // Un runtime autocontenido dentro de AutoCAD: quitar su carpeta dejaría el script sin System.*
            var tpa = new[] { Acad + @"\System.Runtime.dll", Acad + @"\System.Private.CoreLib.dll", Acad + @"\slidelib.exe" };
            Assert.Equal(tpa, CodigoReferencias.FiltrarPlataforma(tpa, Acad, Acad).ToList());
            Assert.Equal(tpa, CodigoReferencias.FiltrarPlataforma(tpa, Acad, Acad + @"\dotnet").ToList());
        }

        [Fact]
        public void Filtrar_SinCarpetaDeAutoCad_NoQuitaNada()
        {
            var tpa = new[] { Acad + @"\slidelib.exe", Runtime + @"\System.Runtime.dll" };
            Assert.Equal(tpa, CodigoReferencias.FiltrarPlataforma(tpa, null, Runtime).ToList());
            Assert.Equal(tpa, CodigoReferencias.FiltrarPlataforma(tpa, "", Runtime).ToList());
            Assert.Empty(CodigoReferencias.FiltrarPlataforma(null, Acad, Runtime));
        }

        // ------------------------------------------------------------------ integración con el compilador
        [Fact]
        public void ConUnPeNativoEnLasReferencias_FiltradoCompila_SinFiltrarDaCS0009()
        {
            string nativo = PeNativo();
            try
            {
                var rutas = Scripts.RutasReferencias().Concat(new[] { nativo }).ToArray();
                var filtradas = rutas.Where(CodigoReferencias.EsEnsambladoAdministrado).ToArray();
                Assert.DoesNotContain(nativo, filtradas);
                Assert.Equal(rutas.Where(r => r != nativo), filtradas);   // no se pierde ningún ensamblado .NET

                var bien = ResultadoAislado.DesdeArreglo(CodigoPuerta.Compilar("return F.Doble(21);", filtradas, Scripts.TipoFachada));
                Assert.True(bien.Ok, bien.Mensaje);
                Assert.Empty(bien.ReferenciasFallidas);

                // Contraste: lo que pasaba en Civil 3D con la 1.17.2
                var mal = ResultadoAislado.DesdeArreglo(CodigoPuerta.Compilar("return F.Doble(21);", rutas, Scripts.TipoFachada));
                Assert.False(mal.Ok);
                Assert.Contains("CS0009", mal.Mensaje);
            }
            finally
            {
                Borrar(nativo);
            }
        }
    }
}

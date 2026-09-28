using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    public class CopiasPruebas
    {
        [Fact]
        public void LimpiarNombreQuitaCaracteresInvalidosYEspacios()
        {
            Assert.Equal("asignar_objetivo", Copias.LimpiarNombre("asignar_objetivo"));
            Assert.Equal("antes_de_objetivos", Copias.LimpiarNombre(" antes de objetivos "));
            Assert.Equal("mcp", Copias.LimpiarNombre("  "));
            Assert.Equal("mcp", Copias.LimpiarNombre(null));
            string limpio = Copias.LimpiarNombre("a/b\\c:d*e?f\"g<h>i|j");
            Assert.DoesNotContain(limpio, c => Path.GetInvalidFileNameChars().Contains(c));
        }

        [Fact]
        public void NombreCopiaLlevaFechaYSufijo()
            => Assert.Equal("obra_20260928_101530_asignar_objetivo.dwg", Copias.NombreCopia("obra", new DateTime(2026, 9, 28, 10, 15, 30), "asignar objetivo"));

        [Fact]
        public void PodaConservaLas20MasRecientes()
        {
            var copias = Enumerable.Range(0, 25).Select(i => ("c" + i, new DateTime(2026, 1, 1).AddMinutes(i))).ToList();
            var sobrantes = Copias.Sobrantes(copias);
            Assert.Equal(5, sobrantes.Count);
            Assert.Equal(new[] { "c4", "c3", "c2", "c1", "c0" }, sobrantes.ToArray());
            Assert.Empty(Copias.Sobrantes(copias.Take(20)));
            Assert.Empty(Copias.Sobrantes(new List<(string, DateTime)>()));
            Assert.Equal(20, Copias.Conservadas);
        }

        [Fact]
        public void PodaSobreUnaCarpetaRealSoloMiraLasCopiasDeEseDibujo()
        {
            string dir = Path.Combine(Path.GetTempPath(), "arba_pruebas_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                for (int i = 0; i < 22; i++)
                {
                    string f = Path.Combine(dir, "obra_2026010" + (i / 10) + "_00000" + (i % 10) + "_x.dwg");
                    File.WriteAllText(f, "x");
                    File.SetLastWriteTimeUtc(f, new DateTime(2026, 1, 1).AddMinutes(i));
                }
                File.WriteAllText(Path.Combine(dir, "otra_20260101_000000_x.dwg"), "x");
                var sobrantes = Copias.Sobrantes(dir, "obra");
                Assert.Equal(2, sobrantes.Count);
                Assert.All(sobrantes, s => Assert.Contains("obra_", Path.GetFileName(s)));
                Assert.Empty(Copias.Sobrantes(Path.Combine(dir, "no_existe"), "obra"));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    [Collection("copias")]
    public class CopiaSeguridadPruebas : IDisposable
    {
        private readonly string _dir;
        private readonly string _dwg;
        private readonly string _backups;

        public CopiaSeguridadPruebas()
        {
            _dir = Path.Combine(Path.GetTempPath(), "arba_copias_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _dwg = Path.Combine(_dir, "obra.dwg");
            File.WriteAllBytes(_dwg, new byte[4096]);
            _backups = Path.Combine(_dir, "backups");
            CopiaSeguridad.Olvidar();
            CopiaSeguridad.Copiador = (o, d) => { File.Copy(o, d, true); return new FileInfo(d).Length; };
        }

        public void Dispose()
        {
            CopiaSeguridad.Copiador = (o, d) => { File.Copy(o, d, true); return new FileInfo(d).Length; };
            CopiaSeguridad.Olvidar();
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Fact]
        public void CopiaEnOtroHiloYEsperarLaCompleta()
        {
            int hiloPrincipal = Environment.CurrentManagedThreadId;
            int hiloCopia = -1;
            // La copia avisa cuando ha empezado: si Esperar() llegara antes de que la tarea arranque, .NET podría ejecutarla
            // en línea en este mismo hilo (Task.Wait) y la comprobación del hilo fallaría por azar (CI del 28/09/2026).
            using var empezo = new ManualResetEventSlim(false);
            CopiaSeguridad.Copiador = (o, d) => { hiloCopia = Environment.CurrentManagedThreadId; empezo.Set(); Thread.Sleep(120); File.Copy(o, d, true); return new FileInfo(d).Length; };
            var registro = new System.Collections.Generic.List<string>();
            var copia = CopiaSeguridad.Planificar(_dwg, _backups, "asignar_objetivo", new DateTime(2026, 9, 28, 10, 0, 0), registro.Add).Iniciar();
            Assert.True(empezo.Wait(TimeSpan.FromSeconds(10)), "la copia no llegó a empezar en otro hilo");
            Assert.Equal("pendiente", copia.Info.Estado);
            Assert.False(copia.Info.Reutilizada);
            Assert.EndsWith("obra_20260928_100000_asignar_objetivo.dwg", copia.Info.Ruta);
            var info = copia.Esperar();
            Assert.Same(info, copia.Info);
            Assert.Equal("terminada", info.Estado);
            Assert.Equal(CopiaSeguridad.MetodoDisco, info.Metodo);
            Assert.True(File.Exists(info.Ruta));
            Assert.Equal(4096, info.Bytes);
            Assert.True(info.Ms >= 100, "ms mide la copia: " + info.Ms);
            Assert.True(info.EsperaMs > 0, "se esperó porque la copia era lenta");
            Assert.NotEqual(hiloPrincipal, hiloCopia);
            Assert.Equal(File.GetLastWriteTime(_dwg).ToString("yyyy-MM-dd HH:mm:ss"), info.RefleaGuardadoDe);
            Assert.Equal(CopiaSeguridad.NotaDisco, info.Nota);
            Assert.Contains(registro, l => l.StartsWith("Copia de seguridad: "));
            // idempotente
            Assert.Same(info, copia.Esperar());
            string json = Json.Serializar(info);
            var j = JsonSerializer.Deserialize<JsonElement>(json);
            foreach (var campo in new[] { "ruta", "metodo", "reutilizada", "ms", "espera_ms", "refleja_guardado_de", "bytes", "estado", "nota" })
                Assert.True(j.TryGetProperty(campo, out _), "falta " + campo + " en " + json);
        }

        [Fact]
        public void SeReutilizaSiElDwgNoCambioDeFechaNiTamano()
        {
            var primera = CopiaSeguridad.Planificar(_dwg, _backups, "a", DateTime.Now).Iniciar();
            primera.Esperar();
            var segunda = CopiaSeguridad.Planificar(_dwg, _backups, "b", DateTime.Now.AddMinutes(1)).Iniciar();
            Assert.True(segunda.Info.Reutilizada);
            Assert.Equal(primera.Info.Ruta, segunda.Info.Ruta);
            Assert.Equal(0, segunda.Info.Ms);
            Assert.Equal("terminada", segunda.Info.Estado);
            Assert.True(segunda.Terminada);
            Assert.Same(segunda.Info, segunda.Esperar());
            Assert.Equal(0, segunda.Info.EsperaMs);
            Assert.Single(Directory.GetFiles(_backups));

            // el usuario guarda el dibujo (cambia el tamaño): copia nueva
            File.WriteAllBytes(_dwg, new byte[5000]);
            var tercera = CopiaSeguridad.Planificar(_dwg, _backups, "c", DateTime.Now.AddMinutes(2)).Iniciar();
            Assert.False(tercera.Info.Reutilizada);
            tercera.Esperar();
            Assert.Equal(2, Directory.GetFiles(_backups).Length);

            // misma fecha y tamaño pero la copia anterior desapareció: copia nueva
            File.Delete(tercera.Info.Ruta);
            var cuarta = CopiaSeguridad.Planificar(_dwg, _backups, "d", DateTime.Now.AddMinutes(3)).Iniciar();
            Assert.False(cuarta.Info.Reutilizada);
            cuarta.Esperar();
            Assert.True(File.Exists(cuarta.Info.Ruta));
        }

        [Fact]
        public void CambioDeFechaSinCambioDeTamanoTambienCopia()
        {
            var primera = CopiaSeguridad.Planificar(_dwg, _backups, "a", DateTime.Now).Iniciar();
            primera.Esperar();
            File.SetLastWriteTimeUtc(_dwg, DateTime.UtcNow.AddMinutes(5));
            var segunda = CopiaSeguridad.Planificar(_dwg, _backups, "b", DateTime.Now.AddMinutes(1)).Iniciar();
            Assert.False(segunda.Info.Reutilizada);
        }

        [Fact]
        public void SiLaCopiaFallaEsperarLanzaYNoSeEscribe()
        {
            CopiaSeguridad.Copiador = (o, d) => throw new IOException("disco lleno");
            var copia = CopiaSeguridad.Planificar(_dwg, _backups, "a", DateTime.Now).Iniciar();
            var ex = Assert.Throws<InvalidOperationException>(() => copia.Esperar());
            Assert.Contains("No se pudo hacer la copia de seguridad", ex.Message);
            Assert.Contains("disco lleno", ex.Message);
            Assert.Contains("No se ha tocado el dibujo", ex.Message);
            Assert.Equal("error", copia.Info.Estado);
            Assert.Contains("disco lleno", copia.Info.Nota);
            Assert.Throws<InvalidOperationException>(() => copia.Esperar());
            // una copia fallida no se recuerda como reutilizable
            CopiaSeguridad.Copiador = (o, d) => { File.Copy(o, d, true); return new FileInfo(d).Length; };
            Assert.False(CopiaSeguridad.Planificar(_dwg, _backups, "b", DateTime.Now).Info.Reutilizada);
        }

        [Fact]
        public void PodaConserva20CopiasDelDibujo()
        {
            Directory.CreateDirectory(_backups);
            for (int i = 0; i < 22; i++)
            {
                string f = Path.Combine(_backups, "obra_20260101_0000" + i.ToString("00") + "_x.dwg");
                File.WriteAllText(f, "x");
                File.SetLastWriteTimeUtc(f, new DateTime(2026, 1, 1).AddMinutes(i));
            }
            File.WriteAllText(Path.Combine(_backups, "otro_20260101_000000_x.dwg"), "x");
            var copia = CopiaSeguridad.Planificar(_dwg, _backups, "nueva", new DateTime(2026, 9, 28, 12, 0, 0)).Iniciar();
            copia.Esperar();
            var restantes = Directory.GetFiles(_backups, "obra_*.dwg");
            Assert.Equal(20, restantes.Length);
            Assert.Contains(restantes, r => r == copia.Info.Ruta);
            Assert.True(File.Exists(Path.Combine(_backups, "otro_20260101_000000_x.dwg")), "las copias de otros dibujos no se tocan");
        }

        [Fact]
        public void DibujoInexistenteLanzaAlPlanificar()
            => Assert.Throws<FileNotFoundException>(() => CopiaSeguridad.Planificar(Path.Combine(_dir, "no.dwg"), _backups, "a", DateTime.Now));
    }
}

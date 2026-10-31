using System;
using System.Collections.Generic;
using System.IO;
using Ejemplo.Dibujo;
using ExtractorCatalogo;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Pruebas del extractor del catálogo (herramientas-dev/ExtractorCatalogo) sobre la biblioteca falsa
    /// herramientas-dev/ExtractorCatalogo.Ejemplo. Aquí nunca hay DLL de Autodesk: la biblioteca de ejemplo imita su
    /// forma (clases, propiedades, un método estático de creación, un enum, una interfaz y miembros [Obsolete]).
    /// </summary>
    public class ExtractorCatalogoPruebas : IDisposable
    {
        private readonly string _carpeta;      // carpeta temporal con la DLL de ejemplo y su .xml de documentación
        private readonly string _salida;
        private readonly ResultadoExtraccion _r;

        public ExtractorCatalogoPruebas()
        {
            string dll = typeof(SuperficieFalsa).Assembly.Location;
            _carpeta = Path.Combine(Path.GetTempPath(), "ArbaMcp_extractor_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_carpeta);
            File.Copy(dll, Path.Combine(_carpeta, Path.GetFileName(dll)));
            string xml = Path.ChangeExtension(dll, ".xml");
            if (File.Exists(xml)) File.Copy(xml, Path.Combine(_carpeta, Path.GetFileName(xml)));
            // Un archivo con extensión .dll que NO es un ensamblado .NET: debe saltarse y contarse, sin error.
            File.WriteAllText(Path.Combine(_carpeta, "nativa_falsa.dll"), "esto no es un ensamblado .NET");

            _salida = Path.Combine(_carpeta, "catalogo_prueba.sqlite");
            _r = Extractor.Extraer(new[] { _carpeta }, _salida, new[] { "*.dll" }, soloPatrones: true);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_carpeta, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private List<object[]> Consultar(string sql, params (string, object)[] args)
        {
            using var con = new SqliteConnection("Data Source=" + _salida + ";Mode=ReadOnly");
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);
            using var lector = cmd.ExecuteReader();
            var filas = new List<object[]>();
            while (lector.Read())
            {
                var fila = new object[lector.FieldCount];
                lector.GetValues(fila);
                filas.Add(fila);
            }
            return filas;
        }

        [Fact]
        public void ElSqliteDelPaqueteTieneFts5()
        {
            Assert.True(Extractor.Fts5Disponible(), "el SQLite que trae Microsoft.Data.Sqlite debe estar compilado con FTS5");
        }

        [Fact]
        public void LosConteosCoincidenConLaBibliotecaDeEjemplo()
        {
            Assert.Equal(1, _r.Ensamblados);
            Assert.Equal(1, _r.ArchivosSaltados);                    // nativa_falsa.dll
            Assert.Equal(5, _r.Tipos);                               // TipoSuperficie, ObjetoDibujo, SuperficieFalsa, AlineamientoFalso, IExportable
            Assert.Equal(8, _r.Metodos);                             // 2 constructores + Create, FindElevationAtXY, Rebuild, RebuildOld, AddPVI, Exportar
            Assert.Equal(5, _r.Propiedades);                         // Name, Id, Description, Tipo, Length
            Assert.Equal(8, _r.Parametros);
            Assert.Equal(0, _r.Avisos);
            Assert.True(File.Exists(_salida));
            Assert.True(_r.TamanoSalida > 0);
            Assert.Equal(new FileInfo(_salida).Length, _r.TamanoSalida);

            // Los mismos conteos quedan guardados dentro del archivo.
            Assert.Equal("5", Consultar("SELECT valor FROM catalogo_info WHERE clave = 'tipos'")[0][0]);
            Assert.Equal("1", Consultar("SELECT valor FROM catalogo_info WHERE clave = 'archivos_saltados'")[0][0]);
            Assert.Single(Consultar("SELECT nombre FROM espacios"));
            Assert.Equal("Ejemplo.Dibujo", Consultar("SELECT nombre FROM espacios")[0][0]);
        }

        [Fact]
        public void ElMetodoEstaticoDeCreacionSeGuardaConSusParametros()
        {
            var filas = Consultar(
                "SELECT m.estatico, m.tipo_retorno, m.es_constructor, m.firma FROM metodos m JOIN tipos t ON t.id = m.tipo_id " +
                "WHERE t.nombre = 'SuperficieFalsa' AND m.nombre = 'Create'");
            Assert.Single(filas);
            Assert.Equal(1L, filas[0][0]);
            Assert.Equal("System.Int64", filas[0][1]);
            Assert.Equal(0L, filas[0][2]);
            Assert.Equal("Create(System.String nombre, Ejemplo.Dibujo.TipoSuperficie tipo)", filas[0][3]);

            var parametros = Consultar(
                "SELECT p.posicion, p.nombre, p.tipo FROM parametros p JOIN metodos m ON m.id = p.metodo_id " +
                "WHERE m.nombre = 'Create' ORDER BY p.posicion");
            Assert.Equal(2, parametros.Count);
            Assert.Equal("nombre", parametros[0][1]);
            Assert.Equal("System.String", parametros[0][2]);
            Assert.Equal("tipo", parametros[1][1]);
            Assert.Equal("Ejemplo.Dibujo.TipoSuperficie", parametros[1][2]);

            // El resto de métodos de instancia no son estáticos, y el constructor va marcado como tal.
            Assert.Equal(0L, Consultar("SELECT estatico FROM metodos WHERE nombre = 'FindElevationAtXY'")[0][0]);
            Assert.Equal(1L, Consultar("SELECT es_constructor FROM metodos WHERE nombre = 'SuperficieFalsa'")[0][0]);
        }

        [Fact]
        public void LosMiembrosObsoletosLlevanSuMensaje()
        {
            var metodo = Consultar("SELECT obsoleto, mensaje_obsoleto FROM metodos WHERE nombre = 'RebuildOld'");
            Assert.Single(metodo);
            Assert.Equal(1L, metodo[0][0]);
            Assert.Equal("Usa Rebuild", metodo[0][1]);

            var propiedad = Consultar("SELECT obsoleto, mensaje_obsoleto FROM propiedades WHERE nombre = 'Description'");
            Assert.Single(propiedad);
            Assert.Equal(1L, propiedad[0][0]);
            Assert.Equal("Usa Name en su lugar", propiedad[0][1]);

            Assert.Equal(0L, Consultar("SELECT obsoleto FROM metodos WHERE nombre = 'Rebuild'")[0][0]);
            Assert.Equal(0L, Consultar("SELECT obsoleto FROM tipos WHERE nombre = 'SuperficieFalsa'")[0][0]);
        }

        [Fact]
        public void LosTiposGuardanClaseBaseYDescripcionDelXml()
        {
            var filas = Consultar("SELECT clase, tipo_base, nombre_completo, descripcion, fuente_descripcion FROM tipos WHERE nombre = 'SuperficieFalsa'");
            Assert.Single(filas);
            Assert.Equal("clase", filas[0][0]);
            Assert.Equal("Ejemplo.Dibujo.ObjetoDibujo", filas[0][1]);
            Assert.Equal("Ejemplo.Dibujo.SuperficieFalsa", filas[0][2]);
            Assert.Equal("Superficie falsa que imita a TinSurface de Civil 3D.", filas[0][3]);
            Assert.Equal("ExtractorCatalogo.Ejemplo.xml", filas[0][4]);

            Assert.Equal("enum", Consultar("SELECT clase FROM tipos WHERE nombre = 'TipoSuperficie'")[0][0]);
            Assert.Equal("interface", Consultar("SELECT clase FROM tipos WHERE nombre = 'IExportable'")[0][0]);
            Assert.Equal("Devuelve la cota de la superficie en un punto.", Consultar("SELECT descripcion FROM metodos WHERE nombre = 'FindElevationAtXY'")[0][0]);
            Assert.Equal("Longitud total del alineamiento.", Consultar("SELECT descripcion FROM propiedades WHERE nombre = 'Length'")[0][0]);
            // El constructor no tiene <summary> en el XML: descripción y fuente vacías.
            Assert.Equal(new object[] { "", "" }, Consultar("SELECT descripcion, fuente_descripcion FROM metodos WHERE nombre = 'AlineamientoFalso'")[0]);
        }

        [Fact]
        public void LasPropiedadesDicenSiSonDeLecturaOEscritura()
        {
            Assert.Equal(new object[] { "System.Int64", 1L, 0L, 0L }, Consultar("SELECT tipo_dato, lectura, escritura, estatico FROM propiedades WHERE nombre = 'Id'")[0]);
            Assert.Equal(new object[] { "System.String", 1L, 1L, 0L }, Consultar("SELECT tipo_dato, lectura, escritura, estatico FROM propiedades WHERE nombre = 'Name'")[0]);
            Assert.Equal("Ejemplo.Dibujo.TipoSuperficie", Consultar("SELECT tipo_dato FROM propiedades WHERE nombre = 'Tipo'")[0][0]);
        }

        [Fact]
        public void SoloSeExtraenMiembrosPublicos()
        {
            Assert.Empty(Consultar("SELECT id FROM metodos WHERE nombre IN ('SoloInterno', 'SoloPrivado')"));
            // Los accesores get_/set_ de las propiedades no se cuentan como métodos.
            Assert.Empty(Consultar("SELECT id FROM metodos WHERE nombre LIKE 'get\\_%' ESCAPE '\\' OR nombre LIKE 'set\\_%' ESCAPE '\\'"));
        }

        [Fact]
        public void LaBusquedaFts5EncuentraPorNombreYPorDescripcion()
        {
            // Por palabra de la descripción (el <summary> de FindElevationAtXY habla de "cota").
            var porDescripcion = Consultar("SELECT clase, nombre_completo FROM busqueda WHERE busqueda MATCH 'cota' ORDER BY rank");
            Assert.Contains(porDescripcion, f => (string)f[0] == "metodo" && (string)f[1] == "Ejemplo.Dibujo.SuperficieFalsa.FindElevationAtXY");

            // Por nombre del tipo y de su método de creación.
            var porNombre = Consultar("SELECT clase, nombre FROM busqueda WHERE busqueda MATCH 'SuperficieFalsa' ORDER BY rank");
            Assert.Contains(porNombre, f => (string)f[0] == "tipo" && (string)f[1] == "SuperficieFalsa");
            Assert.Contains(porNombre, f => (string)f[0] == "metodo" && (string)f[1] == "Create");

            // Búsqueda por prefijo, como hará el agente cuando no recuerde el nombre entero.
            var prefijo = Consultar("SELECT nombre FROM busqueda WHERE busqueda MATCH 'nombre:FindElev*'");
            Assert.Single(prefijo);
            Assert.Equal("FindElevationAtXY", prefijo[0][0]);

            Assert.Empty(Consultar("SELECT nombre FROM busqueda WHERE busqueda MATCH 'palabraquenoexiste'"));
        }

        [Fact]
        public void LaTablaGlosarioExisteYEstaVacia()
        {
            Assert.Equal(0L, Consultar("SELECT COUNT(*) FROM glosario")[0][0]);
            var columnas = Consultar("SELECT name FROM pragma_table_info('glosario') ORDER BY cid");
            Assert.Equal(new[] { "termino", "sinonimos", "concepto_api", "nota" }, columnas.ConvertAll(f => (string)f[0]).ToArray());
        }

        [Fact]
        public void UnaCarpetaInexistenteOSinCarpetasDaErrorClaro()
        {
            Assert.Throws<DirectoryNotFoundException>(() => Extractor.Extraer(new[] { Path.Combine(_carpeta, "no_existe") }, Path.Combine(_carpeta, "x.sqlite")));
            Assert.Throws<ArgumentException>(() => Extractor.Extraer(new string[0], Path.Combine(_carpeta, "x.sqlite")));
        }

        [Fact]
        public void SinLasDllPorDefectoAvisaPeroNoFalla()
        {
            // Modo normal (sin --solo-incluir): en esta carpeta no están acdbmgd.dll y compañía; se avisa por cada una
            // y se procesa lo que añade el patrón.
            string salida = Path.Combine(_carpeta, "catalogo_defecto.sqlite");
            var r = Extractor.Extraer(new[] { _carpeta }, salida, new[] { "ExtractorCatalogo.Ejemplo.dll" });
            Assert.Equal(1, r.Ensamblados);
            Assert.Equal(0, r.ArchivosSaltados);
            Assert.Equal(Extractor.DllPorDefecto.Length, r.Mensajes.FindAll(m => m.StartsWith("No se encontró ")).Count);
        }
    }
}

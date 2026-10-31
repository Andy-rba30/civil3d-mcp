# -*- coding: utf-8 -*-
"""Pruebas de buscar_api (1.5.0) con una memoria de ejemplo: catálogo SQLite pequeño con el mismo esquema que genera
herramientas-dev/ExtractorCatalogo (incluida la tabla FTS5 `busqueda`), un glosario YAML pequeño y dos recetas.
Nunca el catálogo real ni el glosario real: viven en el repositorio privado memoria-firma."""
import json
import os
import sqlite3
import subprocess
import sys

import pytest

from conftest import ejecutar

import buscar_api  # noqa: E402  (conftest ya puso PuenteMcp en sys.path)

CARPETA_PUENTE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

ESQUEMA = """
CREATE TABLE catalogo_info (clave TEXT PRIMARY KEY, valor TEXT NOT NULL);
CREATE TABLE ensamblados (id INTEGER PRIMARY KEY, nombre TEXT NOT NULL, archivo TEXT NOT NULL, version TEXT NOT NULL,
    documentacion TEXT NOT NULL DEFAULT '');
CREATE TABLE espacios (id INTEGER PRIMARY KEY, nombre TEXT NOT NULL UNIQUE);
CREATE TABLE tipos (id INTEGER PRIMARY KEY, nombre TEXT NOT NULL, nombre_completo TEXT NOT NULL,
    espacio_id INTEGER NOT NULL REFERENCES espacios(id), ensamblado_id INTEGER NOT NULL REFERENCES ensamblados(id),
    clase TEXT NOT NULL, tipo_base TEXT NOT NULL DEFAULT '', obsoleto INTEGER NOT NULL DEFAULT 0,
    mensaje_obsoleto TEXT NOT NULL DEFAULT '', descripcion TEXT NOT NULL DEFAULT '', fuente_descripcion TEXT NOT NULL DEFAULT '');
CREATE TABLE metodos (id INTEGER PRIMARY KEY, tipo_id INTEGER NOT NULL REFERENCES tipos(id), nombre TEXT NOT NULL,
    estatico INTEGER NOT NULL DEFAULT 0, tipo_retorno TEXT NOT NULL DEFAULT '', es_constructor INTEGER NOT NULL DEFAULT 0,
    firma TEXT NOT NULL DEFAULT '', obsoleto INTEGER NOT NULL DEFAULT 0, mensaje_obsoleto TEXT NOT NULL DEFAULT '',
    descripcion TEXT NOT NULL DEFAULT '', fuente_descripcion TEXT NOT NULL DEFAULT '');
CREATE TABLE propiedades (id INTEGER PRIMARY KEY, tipo_id INTEGER NOT NULL REFERENCES tipos(id), nombre TEXT NOT NULL,
    tipo_dato TEXT NOT NULL DEFAULT '', estatico INTEGER NOT NULL DEFAULT 0, lectura INTEGER NOT NULL DEFAULT 0,
    escritura INTEGER NOT NULL DEFAULT 0, obsoleto INTEGER NOT NULL DEFAULT 0, mensaje_obsoleto TEXT NOT NULL DEFAULT '',
    descripcion TEXT NOT NULL DEFAULT '', fuente_descripcion TEXT NOT NULL DEFAULT '');
CREATE TABLE parametros (id INTEGER PRIMARY KEY, metodo_id INTEGER NOT NULL REFERENCES metodos(id),
    posicion INTEGER NOT NULL, nombre TEXT NOT NULL, tipo TEXT NOT NULL);
CREATE TABLE glosario (termino TEXT PRIMARY KEY, sinonimos TEXT NOT NULL DEFAULT '', concepto_api TEXT NOT NULL DEFAULT '',
    nota TEXT NOT NULL DEFAULT '');
CREATE VIRTUAL TABLE busqueda USING fts5(clase UNINDEXED, ref_id UNINDEXED, nombre, nombre_completo, descripcion, tokenize = 'unicode61');
"""

ESPACIO = "Ejemplo.Civil.DatabaseServices"

GLOSARIO_YAML = """# glosario de ejemplo para las pruebas
version: 1
catalogo_base: catalogo_c3d2027.sqlite
terminos:
  - termino: rasante
    sinonimos: [perfil de diseño, subrasante, FG, design profile]
    paso: 5
    conceptos:
      - {concepto_api: Ejemplo.Civil.DatabaseServices.Profile, estado: confirmado}
      - {concepto_api: Ejemplo.Civil.DatabaseServices.Profile.CreateByLayout, estado: confirmado}
      - {concepto_api: Ejemplo.Civil.DatabaseServices.ProfileType.FG, estado: por confirmar}
    nota: "Perfil de diseño trazado por el proyectista."
  - termino: PIV
    sinonimos: [PVI, vértice vertical, ProfilePVI]
    paso: 5
    conceptos:
      - {concepto_api: Ejemplo.Civil.DatabaseServices.ProfilePVI, estado: confirmado}
      - {concepto_api: Ejemplo.Civil.DatabaseServices.ProfilePVI.RawStation, estado: confirmado}
    nota: "Punto donde cambian las pendientes."
  - termino: superficie
    sinonimos: [terreno, Surface, TinSurface]
    paso: 1
    conceptos:
      - {concepto_api: Ejemplo.Civil.DatabaseServices.TinSurface, estado: confirmado}
    nota: "Superficie de triángulos."
"""

RECETA_RASANTE = """# Crear una rasante con PIV desde el agente

Cuándo aplica: hay un alineamiento y hace falta un perfil de diseño (rasante) nuevo.

1. `listar_alineamientos` para elegir el eje.
2. `listar_pvis` si ya existe una rasante parecida.
3. Código con `Profile.CreateByLayout` y `ProfilePVI`.
"""

RECETA_TALUD = """# Talud a terreno en una intersección editada a mano

Cuándo aplica: el corredor avisa `TargetDTM not found`.

1. `listar_objetivos` y `asignar_objetivo` con `tipo=superficie`.
"""


def crear_catalogo(ruta, con_glosario_en_tabla=False):
    con = sqlite3.connect(ruta)
    con.executescript(ESQUEMA)
    con.execute("INSERT INTO ensamblados (id, nombre, archivo, version) VALUES (1, 'AeccDbMgd', 'AeccDbMgd.dll', '1.0')")
    con.execute("INSERT INTO espacios (id, nombre) VALUES (1, ?)", (ESPACIO,))
    tipos = [
        (1, "Profile", "clase", "Ejemplo.Civil.DatabaseServices.Feature", 0, ""),
        (2, "ProfilePVI", "clase", "Ejemplo.Civil.CivilWrapper", 0, ""),
        (3, "TinSurface", "clase", "Ejemplo.Civil.DatabaseServices.Surface", 0, ""),
        (4, "ProfileType", "enum", "System.Enum", 0, ""),
        (5, "Corridor", "clase", "Ejemplo.Civil.DatabaseServices.Entity", 0, ""),
        (6, "ProfileViejo", "clase", "", 1, "Usa Profile"),
    ]
    for tid, nombre, clase, base, obs, msg in tipos:
        con.execute("INSERT INTO tipos (id, nombre, nombre_completo, espacio_id, ensamblado_id, clase, tipo_base, obsoleto, mensaje_obsoleto) "
                    "VALUES (?, ?, ?, 1, 1, ?, ?, ?, ?)", (tid, nombre, f"{ESPACIO}.{nombre}", clase, base, obs, msg))
    metodos = [
        (1, 1, "CreateByLayout", 1, "Ejemplo.AutoCAD.ObjectId", "CreateByLayout(System.String profileName, Ejemplo.AutoCAD.ObjectId alignmentId)", 0, ""),
        (2, 3, "FindElevationAtXY", 0, "System.Double", "FindElevationAtXY(System.Double x, System.Double y)", 0, ""),
        (3, 5, "Rebuild", 0, "System.Void", "Rebuild()", 0, ""),
    ]
    for mid, tid, nombre, est, ret, firma, obs, msg in metodos:
        con.execute("INSERT INTO metodos (id, tipo_id, nombre, estatico, tipo_retorno, firma, obsoleto, mensaje_obsoleto) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    (mid, tid, nombre, est, ret, firma, obs, msg))
    con.executemany("INSERT INTO parametros (metodo_id, posicion, nombre, tipo) VALUES (?, ?, ?, ?)", [
        (1, 0, "profileName", "System.String"), (1, 1, "alignmentId", "Ejemplo.AutoCAD.ObjectId"),
        (2, 0, "x", "System.Double"), (2, 1, "y", "System.Double"),
    ])
    propiedades = [
        (1, 2, "RawStation", "System.Double", 1, 1, 0, ""),
        (2, 2, "Station", "System.Double", 1, 1, 1, "Use RawStation"),
        (3, 2, "Elevation", "System.Double", 1, 1, 0, ""),
        (4, 1, "PVIs", "Ejemplo.Civil.DatabaseServices.ProfilePVICollection", 1, 0, 0, ""),
    ]
    for pid, tid, nombre, dato, lec, esc, obs, msg in propiedades:
        con.execute("INSERT INTO propiedades (id, tipo_id, nombre, tipo_dato, lectura, escritura, obsoleto, mensaje_obsoleto) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    (pid, tid, nombre, dato, lec, esc, obs, msg))
    con.executescript("""
INSERT INTO busqueda (clase, ref_id, nombre, nombre_completo, descripcion) SELECT 'tipo', id, nombre, nombre_completo, descripcion FROM tipos;
INSERT INTO busqueda (clase, ref_id, nombre, nombre_completo, descripcion)
    SELECT 'metodo', m.id, m.nombre, t.nombre_completo || '.' || m.nombre, m.descripcion FROM metodos m JOIN tipos t ON t.id = m.tipo_id;
INSERT INTO busqueda (clase, ref_id, nombre, nombre_completo, descripcion)
    SELECT 'propiedad', p.id, p.nombre, t.nombre_completo || '.' || p.nombre, p.descripcion FROM propiedades p JOIN tipos t ON t.id = p.tipo_id;
""")
    if con_glosario_en_tabla:
        # el formato que deja catalogo/cargar_glosario.py de memoria-firma
        con.execute("INSERT INTO glosario VALUES (?, ?, ?, ?)", (
            "rasante", "perfil de diseño; subrasante; FG",
            f"{ESPACIO}.Profile [confirmado]; {ESPACIO}.Profile.CreateByLayout [confirmado]; {ESPACIO}.ProfileType.FG [por confirmar]",
            "paso 5 · Perfil de diseño trazado por el proyectista."))
    con.commit()
    con.close()


@pytest.fixture
def memoria(tmp_path, monkeypatch):
    """Carpeta de memoria-firma de ejemplo (catálogo, glosario YAML y dos recetas) apuntada por MEMORIA_FIRMA_DIR."""
    carpeta = tmp_path / "memoria-firma"
    (carpeta / "catalogo").mkdir(parents=True)
    (carpeta / "recetas").mkdir()
    crear_catalogo(str(carpeta / "catalogo" / "catalogo_c3d2027.sqlite"))
    (carpeta / "catalogo" / "glosario.yaml").write_text(GLOSARIO_YAML, encoding="utf-8")
    (carpeta / "recetas" / "rasante_con_piv.md").write_text(RECETA_RASANTE, encoding="utf-8")
    (carpeta / "recetas" / "talud_interseccion.md").write_text(RECETA_TALUD, encoding="utf-8")
    monkeypatch.setenv(buscar_api.VARIABLE_ENTORNO, str(carpeta))
    return carpeta


def seccion(texto, titulo):
    """Las líneas de una sección (RECETAS, GLOSARIO, CATÁLOGO) del texto de salida."""
    lineas = texto.splitlines()
    inicio = next((i for i, l in enumerate(lineas) if l.startswith(titulo)), None)
    if inicio is None:
        return []
    salida = []
    for l in lineas[inicio + 1:]:
        if l == "":
            break
        salida.append(l)
    return salida


def elementos(texto, titulo):
    return [l for l in seccion(texto, titulo) if l.startswith("- ")]


# ---------------------------------------------------------------------------------------------- normalización y expansión
def test_normalizar_quita_tildes_y_mayusculas():
    assert buscar_api.normalizar("  Vértice  VERTICAL ") == "vertice vertical"
    assert buscar_api._contiene_palabra("rasante con pvi", "pvi")
    assert not buscar_api._contiene_palabra("profilepvi", "pvi"), "solo palabras completas"


def test_expande_la_consulta_por_sinonimo(memoria):
    texto = buscar_api.buscar("subrasante")
    assert "Consulta ampliada con el glosario: Profile, CreateByLayout, ProfileType, FG" in texto
    glosario = elementos(texto, "GLOSARIO")
    assert len(glosario) == 1 and glosario[0].startswith('- rasante (coincide por "subrasante", paso 5;')
    assert f"    {ESPACIO}.Profile.CreateByLayout [confirmado]" in texto
    assert f"    {ESPACIO}.ProfileType.FG [por confirmar]" in texto
    assert "    nota: Perfil de diseño trazado por el proyectista." in texto
    catalogo = seccion(texto, "CATÁLOGO")
    assert catalogo[0] == f"- clase {ESPACIO}.Profile (base: Feature)"
    assert catalogo[1] == f"- método {ESPACIO}.Profile.CreateByLayout → ObjectId [estático]"
    assert catalogo[2] == "    firma: CreateByLayout(System.String profileName, Ejemplo.AutoCAD.ObjectId alignmentId)"
    assert catalogo[3] == "    parámetros: profileName: String, alignmentId: ObjectId"


def test_sin_tildes_ni_mayusculas_en_la_consulta(memoria):
    texto = buscar_api.buscar("VÉRTICE vertical")
    assert '- PIV (coincide por "vértice vertical"' in texto


# ---------------------------------------------------------------------------------------------- orden y límite
def test_orden_recetas_glosario_catalogo(memoria):
    texto = buscar_api.buscar("rasante con PVI")
    assert texto.startswith('buscar_api "rasante con PVI" → 1 recetas, 2 entradas del glosario, ')
    lineas = texto.splitlines()
    i_rec = next(i for i, l in enumerate(lineas) if l.startswith("RECETAS (1)"))
    i_glo = next(i for i, l in enumerate(lineas) if l.startswith("GLOSARIO (2"))
    i_cat = next(i for i, l in enumerate(lineas) if l.startswith("CATÁLOGO ("))
    assert i_rec < i_glo < i_cat
    recetas = seccion(texto, "RECETAS")
    assert recetas[0] == "- rasante_con_piv.md: Crear una rasante con PIV desde el agente"
    assert recetas[1].strip().startswith("Cuándo aplica: hay un alineamiento")
    assert "talud_interseccion.md" not in texto
    assert [l for l in elementos(texto, "GLOSARIO")][0].startswith("- rasante")
    assert [l for l in elementos(texto, "GLOSARIO")][1].startswith("- PIV")
    assert f"- clase {ESPACIO}.ProfilePVI" in texto
    assert f"- propiedad {ESPACIO}.ProfilePVI.RawStation: Double (lectura/escritura)" in texto


def test_limite_total_con_prioridad(memoria):
    texto = buscar_api.buscar("rasante con PVI", limite=2)
    assert "→ 1 recetas, 1 entradas del glosario, 0 del catálogo (límite 2)" in texto
    assert "CATÁLOGO" not in texto
    texto = buscar_api.buscar("rasante con PVI", limite=4)
    assert "→ 1 recetas, 2 entradas del glosario, 1 del catálogo (límite 4)" in texto
    assert len(elementos(texto, "CATÁLOGO")) == 1
    assert "(límite 1)" in buscar_api.buscar("rasante", limite=0), "un límite inválido se corrige, no falla"
    assert "(límite 10)" in buscar_api.buscar("rasante", limite="x")


def test_nombre_exacto_antes_que_el_glosario_y_marca_obsoleto(memoria):
    texto = buscar_api.buscar("ProfilePVI Station", limite=20)
    catalogo = seccion(texto, "CATÁLOGO")
    assert catalogo[0] == f"- clase {ESPACIO}.ProfilePVI (base: CivilWrapper)"
    assert catalogo[1] == f"- propiedad {ESPACIO}.ProfilePVI.Station: Double (lectura/escritura)"
    assert catalogo[2] == "    OBSOLETO: Use RawStation"


def test_indice_de_texto_completa_el_catalogo(memoria):
    texto = buscar_api.buscar("Rebuild")   # no está en el glosario: solo el índice FTS5
    assert "0 entradas del glosario, 1 del catálogo" in texto
    assert f"- método {ESPACIO}.Corridor.Rebuild → Void" in texto
    assert "    parámetros: ninguno" in texto
    texto = buscar_api.buscar("Profile*")  # los comodines y la puntuación no rompen la expresión FTS5
    assert f"- clase {ESPACIO}.Profile (base: Feature)" in texto


def test_sin_resultados(memoria):
    texto = buscar_api.buscar("xyzzy")
    assert "0 recetas, 0 entradas del glosario, 0 del catálogo" in texto
    assert 'Sin resultados para "xyzzy"' in texto


# ---------------------------------------------------------------------------------------------- errores claros
def test_error_sin_variable_de_entorno(monkeypatch):
    monkeypatch.delenv(buscar_api.VARIABLE_ENTORNO, raising=False)
    texto = buscar_api.buscar("rasante")
    assert texto.startswith("Error: Falta la variable de entorno MEMORIA_FIRMA_DIR")
    assert "[Environment]::SetEnvironmentVariable('MEMORIA_FIRMA_DIR','C:\\Proyectos\\memoria-firma','User')" in texto


def test_error_carpeta_inexistente(monkeypatch, tmp_path):
    monkeypatch.setenv(buscar_api.VARIABLE_ENTORNO, str(tmp_path / "no_existe"))
    texto = buscar_api.buscar("rasante")
    assert texto.startswith("Error: La carpeta de MEMORIA_FIRMA_DIR no existe:") and "no_existe" in texto


def test_error_falta_el_catalogo(memoria):
    os.remove(memoria / "catalogo" / "catalogo_c3d2027.sqlite")
    texto = buscar_api.buscar("rasante")
    assert texto.startswith("Error: Falta el catálogo ") and "catalogo_c3d2027.sqlite" in texto
    assert "ExtractorCatalogo" in texto


def test_error_sin_glosario_ni_tabla(memoria):
    os.remove(memoria / "catalogo" / "glosario.yaml")
    texto = buscar_api.buscar("rasante")
    assert texto.startswith("Error: Falta el glosario ") and "glosario.yaml" in texto and "vacía" in texto


def test_sin_yaml_usa_la_tabla_glosario_del_catalogo(memoria):
    os.remove(memoria / "catalogo" / "glosario.yaml")
    os.remove(memoria / "catalogo" / "catalogo_c3d2027.sqlite")
    crear_catalogo(str(memoria / "catalogo" / "catalogo_c3d2027.sqlite"), con_glosario_en_tabla=True)
    texto = buscar_api.buscar("subrasante")
    assert "GLOSARIO (1, de tabla glosario del catálogo):" in texto
    assert '- rasante (coincide por "subrasante", paso 5; sinónimos: perfil de diseño, subrasante, FG)' in texto
    assert f"    {ESPACIO}.ProfileType.FG [por confirmar]" in texto
    assert "    nota: Perfil de diseño trazado por el proyectista." in texto


def test_sin_carpeta_de_recetas_sigue_buscando(memoria):
    os.remove(memoria / "recetas" / "rasante_con_piv.md")
    os.remove(memoria / "recetas" / "talud_interseccion.md")
    os.rmdir(memoria / "recetas")
    texto = buscar_api.buscar("rasante")
    assert texto.startswith('buscar_api "rasante" → 0 recetas, 1 entradas del glosario')


def test_consulta_vacia(memoria):
    assert buscar_api.buscar("   ").startswith("Error: la consulta está vacía")


# ---------------------------------------------------------------------------------------------- como comando
def _comando(args, env):
    return subprocess.run([sys.executable] + args, cwd=CARPETA_PUENTE, env=env, capture_output=True, text=True, encoding="utf-8")


def test_comando_python_buscar_api_py(memoria):
    env = dict(os.environ, PYTHONIOENCODING="utf-8")
    r = _comando(["buscar_api.py", "rasante con PVI", "--limite", "3"], env)
    assert r.returncode == 0, r.stderr
    assert r.stdout.startswith('buscar_api "rasante con PVI" → 1 recetas, 2 entradas del glosario, 0 del catálogo (límite 3)')


def test_comando_python_m_buscar_api(memoria):
    env = dict(os.environ, PYTHONIOENCODING="utf-8")
    r = _comando(["-m", "buscar_api", "subrasante"], env)
    assert r.returncode == 0, r.stderr
    assert "GLOSARIO (1, de glosario.yaml):" in r.stdout
    r = _comando(["-m", "buscar_api", "rasante", "--memoria", str(memoria)], dict(env, MEMORIA_FIRMA_DIR=""))
    assert r.returncode == 0 and "1 entradas del glosario" in r.stdout, "--memoria sustituye a la variable"


def test_comando_sin_variable_sale_con_2(memoria):
    env = dict(os.environ, PYTHONIOENCODING="utf-8")
    env.pop(buscar_api.VARIABLE_ENTORNO, None)
    r = _comando(["buscar_api.py", "rasante"], env)
    assert r.returncode == 2 and r.stdout.startswith("Error: Falta la variable de entorno MEMORIA_FIRMA_DIR")


# ---------------------------------------------------------------------------------------------- en el puente MCP
def texto_mcp(resultado):
    return resultado.content[0].text


def test_el_puente_expone_buscar_api_sin_plugin(servidor, puente, memoria, monkeypatch):
    nombres = sorted(t.name for t in puente.mcp._tool_manager.list_tools())
    assert "buscar_api" in nombres and "buscar_api" in puente.HERRAMIENTAS_LOCALES
    herramienta = puente.mcp._tool_manager.get_tool("buscar_api")
    assert herramienta.parameters["required"] == ["consulta"], "los argumentos van directos, no dentro de 'args'"
    assert herramienta.parameters["properties"]["limite"]["default"] == 10
    assert "MEMORIA_FIRMA_DIR" in herramienta.description
    monkeypatch.setenv("ARBA_MCP_PORT", "1")   # Civil 3D cerrado: nadie escucha
    assert ejecutar(puente.sincronizar_herramientas())["estado"] == "sin_conexion"
    assert "buscar_api" in sorted(t.name for t in puente.mcp._tool_manager.list_tools())
    texto = texto_mcp(ejecutar(puente.mcp.call_tool("buscar_api", {"consulta": "rasante con PVI", "limite": 4})))
    assert texto.startswith('buscar_api "rasante con PVI" → 1 recetas, 2 entradas del glosario, 1 del catálogo (límite 4)')


def test_el_puente_no_quita_buscar_api_al_sincronizar(servidor, puente, memoria):
    servidor.herramienta("ping")
    servidor.herramienta("buscar_api", [], "del plugin: debe ignorarse")
    r = ejecutar(puente.sincronizar_herramientas())
    assert r["estado"] == "ok" and r["añadidas"] == ["ping"] and "buscar_api" not in puente.known_tools
    assert puente.mcp._tool_manager.get_tool("buscar_api").description.startswith("Busca en la memoria técnica")
    servidor.tools = []
    r = ejecutar(puente.sincronizar_herramientas())
    assert r["quitadas"] == ["ping"]
    assert "buscar_api" in [t.name for t in puente.mcp._tool_manager.list_tools()]


def test_el_puente_devuelve_el_error_claro_sin_variable(puente, monkeypatch):
    monkeypatch.delenv(buscar_api.VARIABLE_ENTORNO, raising=False)
    texto = texto_mcp(ejecutar(puente.mcp.call_tool("buscar_api", {"consulta": "rasante"})))
    assert texto.startswith("Error: Falta la variable de entorno MEMORIA_FIRMA_DIR")
    with pytest.raises(json.JSONDecodeError):
        json.loads(texto)   # es texto, no JSON como las herramientas del plugin

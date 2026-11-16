# -*- coding: utf-8 -*-
"""Pruebas de las recetas de código (1.17.0): guardar_receta y ejecutar_receta, sin Civil 3D.

Cada prueba usa una carpeta temporal como MEMORIA_FIRMA_DIR (nunca la memoria real). ejecutar_receta llama a
ejecutar_codigo del plugin: aquí responde el servidor falso de conftest.py, que imita sus respuestas (simulación,
ejecución real, desactivada, plugin sin la herramienta)."""
import json
import os

import pytest
import yaml

from conftest import ejecutar

import buscar_api  # noqa: E402  (conftest ya puso PuenteMcp en sys.path)
import recetas  # noqa: E402

NOMBRE = "crear_capa_con_color"
PEDIDO = "Crea la capa MCP Receta A con el color 3 (verde)"
CODIGO = ('var nombre = Parametros["nombre_capa"].GetString();\n'
          'var color = Parametros["color"].GetInt32();\n'
          'return new { capa = nombre, color = color };\n')
PARAMETROS = [
    {"nombre": "nombre_capa", "tipo": "string", "descripcion": "Nombre de la capa", "obligatorio": True},
    {"nombre": "color", "tipo": "number", "descripcion": "Color ACI", "por_defecto": 7},
]
APROBACION = "Sí, guárdala como receta"


@pytest.fixture
def memoria(tmp_path, monkeypatch):
    """memoria-firma de prueba: solo la carpeta (guardar y ejecutar recetas no necesitan el catálogo)."""
    carpeta = tmp_path / "memoria-firma"
    carpeta.mkdir()
    monkeypatch.setenv(buscar_api.VARIABLE_ENTORNO, str(carpeta))
    return carpeta


def guardar(**cambios):
    args = dict(nombre=NOMBRE, pedido=PEDIDO, codigo=CODIGO, parametros=PARAMETROS, notas="Receta de prueba.",
                aprobada_por_el_dueno=True, aprobacion_del_dueno=APROBACION, version_conector="1.17.0")
    args.update(cambios)
    return recetas.guardar(**args)


def leer_yaml(memoria, nombre=NOMBRE):
    with open(memoria / "recetas" / f"{nombre}.yaml", encoding="utf-8") as f:
        return yaml.safe_load(f)


def archivos(carpeta):
    """Todos los archivos bajo una carpeta, como rutas relativas (para comprobar que no se escribió nada)."""
    salida = []
    for raiz, _dirs, nombres in os.walk(carpeta):
        for n in nombres:
            salida.append(os.path.relpath(os.path.join(raiz, n), carpeta))
    return sorted(salida)


# ---------------------------------------------------------------------------------------------- guardar_receta
def test_sin_aprobacion_devuelve_la_vista_previa_y_no_escribe(memoria):
    r = guardar(aprobada_por_el_dueno=False, aprobacion_del_dueno="")
    assert r["ok"] is True and r["guardada"] is False and r["reemplazaria"] is False
    assert "No se guardó nada" in r["siguiente_paso"] and "aprobada_por_el_dueno=true" in r["siguiente_paso"]
    previsto = yaml.safe_load(r["yaml"])
    assert previsto["nombre"] == NOMBRE and previsto["pedido"] == PEDIDO and previsto["veces_usada"] == 0
    assert r["archivos_previstos"]["codigo"].endswith(os.path.join("recetas", NOMBRE + ".cs"))
    assert r["lineas_codigo"] == 3 and r["avisos"] == []
    assert archivos(memoria) == [], "la vista previa no crea ni la carpeta recetas"


def test_palabras_del_dueno_sin_aprobada_true_tampoco_escribe(memoria):
    r = guardar(aprobada_por_el_dueno=False)
    assert r["ok"] is True and r["guardada"] is False
    assert archivos(memoria) == []


def test_aprobada_sin_las_palabras_del_dueno_es_error(memoria):
    for palabras in ("", "   ", None):
        r = guardar(aprobacion_del_dueno=palabras)
        assert r["ok"] is False and r["guardada"] is False
        assert r["error"].startswith("Falta 'aprobacion_del_dueno'") and "no guardes nada sin su sí" in r["error"]
    assert archivos(memoria) == []


def test_aprobada_tiene_que_ser_el_booleano_true(memoria):
    r = guardar(aprobada_por_el_dueno="true")
    assert r["ok"] is True and r["guardada"] is False, "un texto no vale como aprobación"
    assert archivos(memoria) == []


def test_con_aprobacion_guarda_el_cs_y_el_yaml(memoria):
    r = guardar()
    assert r["ok"] is True and r["guardada"] is True and r["reemplazada"] is False and r["veces_usada"] == 0
    assert archivos(memoria) == [os.path.join("recetas", NOMBRE + ".cs"), os.path.join("recetas", NOMBRE + ".yaml")]
    with open(memoria / "recetas" / f"{NOMBRE}.cs", encoding="utf-8", newline="") as f:
        assert f.read() == CODIGO, "el código se guarda tal cual"
    datos = leer_yaml(memoria)
    assert list(datos) == ["nombre", "pedido", "parametros", "notas", "codigo", "version_civil3d", "version_conector",
                           "fecha", "veces_usada", "ultima_vez", "aprobacion"]
    assert datos["nombre"] == NOMBRE and datos["pedido"] == PEDIDO and datos["codigo"] == NOMBRE + ".cs"
    assert datos["parametros"] == [
        {"nombre": "nombre_capa", "tipo": "string", "descripcion": "Nombre de la capa", "obligatorio": True},
        {"nombre": "color", "tipo": "number", "descripcion": "Color ACI", "obligatorio": False, "por_defecto": 7},
    ]
    assert datos["version_civil3d"] == "2027" and datos["version_conector"] == "1.17.0"
    assert len(datos["fecha"]) == 10 and datos["veces_usada"] == 0 and datos["ultima_vez"] is None
    assert datos["aprobacion"] == {"palabras_del_dueno": APROBACION, "fecha": datos["fecha"]}
    assert "L.2" in r["recordatorio"] and "sincronizar_memoria.ps1" in r["recordatorio"] and str(memoria) in r["recordatorio"]
    texto = (memoria / "recetas" / f"{NOMBRE}.yaml").read_text(encoding="utf-8")
    assert texto.startswith("# Receta de código de civil3d-mcp") and "Sí, guárdala" in texto, "UTF-8 legible, sin \\u"


def test_notas_de_varias_lineas_se_leen_igual(memoria):
    guardar(notas="Primera línea.\nSegunda línea: con dos puntos.")
    assert leer_yaml(memoria)["notas"] == "Primera línea.\nSegunda línea: con dos puntos."


@pytest.mark.parametrize("nombre", ["../fuera", "..\\fuera", "a/b", "recetas/x", "C:\\x", ".oculto", "receta.cs", "con",
                                    "nul", "lpt1", "Crear Capa", "CrearCapa", "ab", "", "   ", None, "x" * 65, "_abc"])
def test_rechaza_nombres_que_no_son_un_nombre_de_archivo_seguro(memoria, nombre):
    r = guardar(nombre=nombre)
    assert r["ok"] is False and r["error"].startswith("El nombre de receta"), r
    assert "minúsculas, números y guiones bajos" in r["error"]
    assert archivos(memoria.parent) == [], "nada escrito, ni dentro ni fuera de la memoria"


def test_el_error_de_nombre_sugiere_uno_valido(memoria):
    assert "por ejemplo 'crear_capa_de_prueba'" in guardar(nombre="Crear Capa de Prueba")["error"]
    assert "por ejemplo 'fuera'" in guardar(nombre="../fuera")["error"]
    assert recetas.sugerir_nombre("con") == "receta_con" and recetas.sugerir_nombre("Línea ñandú") == "linea_nandu"


def test_no_sobrescribe_una_receta_existente_sin_reemplazar(memoria):
    guardar()
    recetas.anotar_uso(str(memoria / "recetas" / f"{NOMBRE}.yaml"))
    antes = {a: (memoria / a).read_bytes() for a in archivos(memoria)}
    for aprobada in (False, True):
        r = guardar(codigo="return 1;\n", aprobada_por_el_dueno=aprobada)
        assert r["ok"] is False and r["error"].startswith(f"Ya existe la receta '{NOMBRE}'")
        assert "veces_usada 1" in r["error"] and "reemplazar=true" in r["error"]
    assert {a: (memoria / a).read_bytes() for a in archivos(memoria)} == antes


def test_reemplazar_lo_dice_y_conserva_veces_usada(memoria):
    guardar()
    recetas.anotar_uso(str(memoria / "recetas" / f"{NOMBRE}.yaml"))
    fecha = leer_yaml(memoria)["fecha"]
    previa = guardar(codigo="return 2;\n", parametros=[], reemplazar=True, aprobada_por_el_dueno=False)
    assert previa["ok"] is True and previa["guardada"] is False and previa["reemplazaria"] is True
    r = guardar(codigo="return 2;\n", parametros=[], reemplazar=True)
    assert r["ok"] is True and r["guardada"] is True and r["reemplazada"] is True and r["veces_usada"] == 1
    datos = leer_yaml(memoria)
    assert datos["veces_usada"] == 1 and datos["fecha"] == fecha and "actualizada" in datos and datos["parametros"] == []
    assert (memoria / "recetas" / f"{NOMBRE}.cs").read_text(encoding="utf-8") == "return 2;\n"


def test_solo_el_cs_existente_tambien_cuenta_como_receta_existente(memoria):
    (memoria / "recetas").mkdir()
    (memoria / "recetas" / f"{NOMBRE}.cs").write_text("return 0;", encoding="utf-8")
    r = guardar()
    assert r["ok"] is False and r["error"].startswith("Ya existe la receta")


def test_rechaza_el_token_del_plugin_sin_repetirlo(memoria):
    token = "f0" * 32
    for campo in ("codigo", "pedido", "notas", "aprobacion_del_dueno"):
        r = guardar(token=token, **{campo: f"texto con {token} dentro"})
        assert r["ok"] is False and token not in json.dumps(r), "el mensaje nunca repite el token"
        assert f"'{campo}' contiene el token del plugin (<oculto>)" in r["error"]
    r = guardar(token="corto-pero-secreto", codigo="// corto-pero-secreto")
    assert r["ok"] is False and "<oculto>" in r["error"] and "corto-pero-secreto" not in r["error"]
    assert archivos(memoria) == []


def test_rechaza_lo_que_parece_un_token_o_lo_lee(memoria):
    r = guardar(codigo="// " + "A1" * 32)
    assert r["ok"] is False and "parece un token (<oculto>)" in r["error"] and "A1A1" not in r["error"]
    for codigo in ('File.ReadAllText(@"%LOCALAPPDATA%\\ArbaMcp\\token")', 'var h = "X-Arba-Token";'):
        r = guardar(codigo=codigo)
        assert r["ok"] is False and ("token del plugin" in r["error"] or "ruta absoluta" in r["error"])
    r = guardar(codigo='var t = Path.Combine(carpeta, "ArbaMcp/token");')
    assert r["ok"] is False and "archivo o la cabecera del token" in r["error"]
    assert archivos(memoria) == []


@pytest.mark.parametrize("campo, texto, mensaje", [
    ("codigo", 'var ruta = @"C:\\Proyectos\\dibujo.dwg";', "ruta absoluta"),
    ("codigo", 'var ruta = "C:/Proyectos/x";', "ruta absoluta"),
    ("notas", "copia en \\\\servidor\\compartida\\x", "ruta absoluta"),
    ("pedido", "Abre PlanoGeneral.dwg y crea la capa", "nombre de un dibujo (.dwg)"),
])
def test_rechaza_rutas_y_nombres_de_dibujo(memoria, campo, texto, mensaje):
    r = guardar(**{campo: texto})
    assert r["ok"] is False and f"'{campo}' lleva" in r["error"] and mensaje in r["error"] and "parámetro" in r["error"]
    assert archivos(memoria) == []


def test_admite_la_extension_dwg_y_las_urls_sin_ruta(memoria):
    codigo = 'if (doc.Name.EndsWith(".dwg")) { var u = "https://example.com/a"; }\n' + CODIGO
    assert guardar(codigo=codigo)["guardada"] is True


def test_rechaza_los_nombres_de_la_lista_de_prohibidos_sin_repetirlos(memoria):
    (memoria / "casos").mkdir()
    (memoria / "casos" / "PROHIBIDOS.txt").write_text("# obras\nObra Secreta Norte\n", encoding="utf-8")
    r = guardar(notas="Se usó en la obra secreta norte")
    assert r["ok"] is False and "casos\\PROHIBIDOS.txt" in r["error"] and "Secreta" not in r["error"]
    r = guardar(parametros=[{"nombre": "capa", "tipo": "string", "por_defecto": "Obra Secreta Norte"}])
    assert r["ok"] is False and "'parametros.capa'" in r["error"]
    assert archivos(memoria) == ["casos" + os.sep + "PROHIBIDOS.txt"]
    assert guardar(notas="Una secretaria del norte")["guardada"] is True, "solo el nombre completo, no trozos"


@pytest.mark.parametrize("parametros, mensaje", [
    ("[no es json", "no es JSON válido"),
    ({"nombre_capa": "string"}, "debe ser una lista JSON"),
    (["nombre_capa"], "debe ser un objeto con nombre y tipo"),
    ([{"nombre": "Capa", "tipo": "string"}], "necesita un 'nombre' en minúsculas"),
    ([{"nombre": "capa", "tipo": "texto"}], "usa uno de: string, number, boolean, json"),
    ([{"nombre": "capa", "tipo": "string"}, {"nombre": "capa", "tipo": "number"}], "está repetido"),
    ([{"nombre": "capa", "tipo": "string", "default": "x"}], "claves desconocidas: default"),
    ([{"nombre": "color", "tipo": "number", "por_defecto": "7"}], "debe ser number (llegó string)"),
    ([{"nombre": "color", "tipo": "number", "por_defecto": True}], "debe ser number (llegó boolean)"),
    ([{"nombre": "capa", "tipo": "string", "obligatorio": True, "por_defecto": "x"}], "es obligatorio y tiene por_defecto"),
    ([{"nombre": "capa", "tipo": "string", "obligatorio": "si"}], "debe ser true o false"),
])
def test_rechaza_parametros_mal_declarados(memoria, parametros, mensaje):
    r = guardar(parametros=parametros)
    assert r["ok"] is False and mensaje in r["error"], r
    assert archivos(memoria) == []


def test_parametros_como_texto_json_y_sin_parametros(memoria):
    assert guardar(parametros=json.dumps(PARAMETROS))["guardada"] is True
    assert guardar(nombre="sin_parametros", codigo="return 1;", parametros=None)["guardada"] is True
    assert leer_yaml(memoria, "sin_parametros")["parametros"] == []


def test_avisa_si_un_parametro_no_aparece_en_el_codigo(memoria):
    r = guardar(codigo="return 1;\n")
    assert r["guardada"] is True
    assert r["avisos"] == ["El parámetro 'nombre_capa' no aparece en el código: el script no lo leerá de Parametros.",
                           "El parámetro 'color' no aparece en el código: el script no lo leerá de Parametros."]


@pytest.mark.parametrize("cambios, mensaje", [
    ({"pedido": ""}, "Falta 'pedido'"),
    ({"pedido": None}, "Falta 'pedido'"),
    ({"codigo": "  \n"}, "Falta 'codigo'"),
    ({"codigo": "x" * (recetas.MAXIMO_CODIGO + 1)}, "el máximo es"),
])
def test_faltan_textos_obligatorios(memoria, cambios, mensaje):
    r = guardar(**cambios)
    assert r["ok"] is False and mensaje in r["error"]


def test_sin_memoria_firma_dir_o_carpeta_inexistente(monkeypatch, tmp_path):
    monkeypatch.delenv(buscar_api.VARIABLE_ENTORNO, raising=False)
    r = guardar()
    assert r["ok"] is False and r["error"].startswith("Falta la variable de entorno MEMORIA_FIRMA_DIR")
    assert "SetEnvironmentVariable" in r["error"]
    monkeypatch.setenv(buscar_api.VARIABLE_ENTORNO, str(tmp_path / "no_existe"))
    r = guardar()
    assert r["ok"] is False and r["error"].startswith("La carpeta de MEMORIA_FIRMA_DIR no existe")
    assert not (tmp_path / "no_existe").exists()


def test_no_necesita_el_catalogo(memoria):
    assert not (memoria / "catalogo").exists()
    assert guardar()["guardada"] is True


# ---------------------------------------------------------------------------------------------- parámetros al ejecutar
DECLARADOS = recetas.normalizar_parametros(PARAMETROS)


def test_preparar_parametros_completa_los_por_defecto_en_orden():
    assert recetas.preparar_parametros(DECLARADOS, {"nombre_capa": "Capa B"}) == {"nombre_capa": "Capa B", "color": 7}
    assert recetas.preparar_parametros(DECLARADOS, {"color": 3, "nombre_capa": "C"}) == {"nombre_capa": "C", "color": 3}
    assert recetas.preparar_parametros(DECLARADOS, '{"nombre_capa": "D", "color": 2.5}') == {"nombre_capa": "D", "color": 2.5}
    assert recetas.preparar_parametros(DECLARADOS, {"nombre_capa": "E", "color": None}) == {"nombre_capa": "E", "color": 7}
    assert recetas.preparar_parametros([], None) == {} and recetas.preparar_parametros([], "") == {}
    opcional = recetas.normalizar_parametros([{"nombre": "extra", "tipo": "json", "obligatorio": False}])
    assert recetas.preparar_parametros(opcional, {}) == {}, "un opcional sin por_defecto no se envía"
    assert recetas.preparar_parametros(opcional, {"extra": [1, {"a": 2}]}) == {"extra": [1, {"a": 2}]}


@pytest.mark.parametrize("recibidos, mensaje", [
    ({"colour": 3, "nombre_capa": "x"}, "La receta no tiene los parámetros: colour"),
    ({}, "Falta el parámetro obligatorio 'nombre_capa'"),
    ({"nombre_capa": 3}, "'nombre_capa' debe ser string (llegó number)"),
    ({"nombre_capa": "x", "color": "3"}, "'color' debe ser number (llegó string)"),
    ({"nombre_capa": "x", "color": True}, "'color' debe ser number (llegó boolean)"),
    ("[1, 2]", "debe ser un objeto JSON"),
    ("{roto", "no es JSON válido"),
])
def test_preparar_parametros_rechaza(recibidos, mensaje):
    with pytest.raises(recetas.ErrorReceta) as info:
        recetas.preparar_parametros(DECLARADOS, recibidos)
    assert mensaje in str(info.value)
    assert "nombre_capa (string, obligatorio), color (number, por defecto 7)" in str(info.value) or "objeto" in str(info.value)


# ---------------------------------------------------------------------------------------------- ejecutar_receta (puente)
def texto_mcp(resultado):
    return resultado.content[0].text


def receta(puente, **args):
    """Llama a ejecutar_receta como lo hace el agente y devuelve el JSON de la respuesta."""
    return json.loads(texto_mcp(ejecutar(puente.mcp.call_tool("ejecutar_receta", {"nombre": NOMBRE, **args}))))


def respuesta_plugin(simulado, resultado):
    return {"ok": True, "tool": "ejecutar_codigo", "id_log": "20261114-120000-abc123", "ms": 12,
            "result": {"simulado": simulado, "resultado": resultado, "avisos": []}}


@pytest.fixture
def con_receta(memoria):
    assert guardar()["guardada"] is True
    return memoria


def test_simula_por_defecto_no_suma_y_fija_el_cuerpo_enviado(servidor, puente, con_receta):
    servidor.respuestas["ejecutar_codigo"] = respuesta_plugin(True, {"capa": "Capa B", "color": 7})
    r = receta(puente, parametros={"nombre_capa": "Capa B"}, pedido="Hazme una capa Capa B")
    # el cuerpo exacto que recibe el plugin: parametros viaja como texto JSON, como todo parámetro json del puente
    assert servidor.llamadas == [{
        "tool": "ejecutar_codigo",
        "args": {"codigo_csharp": CODIGO, "simular": True, "parametros": '{"nombre_capa": "Capa B", "color": 7}',
                 "pedido": "Hazme una capa Capa B"},
        "timeout_s": puente._timeout_s("ejecutar_codigo", [{"name": "simular"}], {}),
    }]
    assert r["simulado"] is True and r["resultado"] == {"capa": "Capa B", "color": 7}
    assert r["id_log"] == "20261114-120000-abc123" and r["ms"] == 12 and "ms_puente" in r
    assert r["receta"]["nombre"] == NOMBRE and r["receta"]["parametros"] == {"nombre_capa": "Capa B", "color": 7}
    assert r["receta"]["sumada"] is False and r["receta"]["veces_usada"] == 0
    assert r["receta"]["nota"].startswith("Simulación: veces_usada no cambia")
    assert leer_yaml(con_receta)["veces_usada"] == 0


def test_la_ejecucion_real_correcta_suma_uno_y_luego_dos(servidor, puente, con_receta):
    servidor.respuestas["ejecutar_codigo"] = respuesta_plugin(False, {"capa": "Capa B", "color": 3})
    r = receta(puente, parametros={"nombre_capa": "Capa B", "color": 3}, simular=False)
    assert servidor.llamadas[-1]["args"]["simular"] is False and "pedido" not in servidor.llamadas[-1]["args"]
    assert r["receta"]["sumada"] is True and r["receta"]["veces_usada"] == 1
    assert "sube a 1" in r["receta"]["nota"] and "L.2" in r["receta"]["nota"]
    datos = leer_yaml(con_receta)
    assert datos["veces_usada"] == 1 and datos["ultima_vez"] == datos["fecha"]
    r = receta(puente, parametros={"nombre_capa": "Capa C"}, simular=False)
    assert r["receta"]["veces_usada"] == 2 and leer_yaml(con_receta)["veces_usada"] == 2
    assert leer_yaml(con_receta)["aprobacion"]["palabras_del_dueno"] == APROBACION, "el resto del .yaml no cambia"


def test_parametros_como_texto_llegan_igual(servidor, puente, con_receta):
    servidor.respuestas["ejecutar_codigo"] = respuesta_plugin(True, None)
    receta(puente, parametros='{"nombre_capa": "Capa T"}')
    assert servidor.llamadas[-1]["args"]["parametros"] == '{"nombre_capa": "Capa T", "color": 7}'


def test_si_el_plugin_simula_aunque_se_pida_real_no_suma(servidor, puente, con_receta):
    servidor.respuestas["ejecutar_codigo"] = respuesta_plugin(True, None)
    r = receta(puente, parametros={"nombre_capa": "Capa B"}, simular=False)
    assert r["receta"]["sumada"] is False and leer_yaml(con_receta)["veces_usada"] == 0


def test_ejecutar_codigo_desactivada_no_suma(servidor, puente, con_receta):
    servidor.respuestas["ejecutar_codigo"] = {
        "ok": False, "id_log": "20261114-120001-def456",
        "error": "ejecutar_codigo está desactivada. Actívala con la variable de entorno ARBA_MCP_CODIGO=1."}
    for simular in (False, True):
        r = receta(puente, parametros={"nombre_capa": "Capa B"}, simular=simular)
        assert r["ok"] is False and "ejecutar_codigo está desactivada" in r["error"] and r["id_log"] == "20261114-120001-def456"
        assert r["receta"]["sumada"] is False and r["receta"]["nota"] == "La ejecución falló: veces_usada no cambia."
    assert leer_yaml(con_receta)["veces_usada"] == 0


def test_plugin_sin_ejecutar_codigo_lo_dice_claro(servidor, puente, con_receta):
    r = receta(puente, parametros={"nombre_capa": "Capa B"}, simular=False)   # el servidor falso no la conoce
    assert r["ok"] is False and r["error"].startswith("ejecutar_receta necesita la herramienta ejecutar_codigo del plugin (1.16.0")
    assert "Herramienta desconocida" in r["error"] and leer_yaml(con_receta)["veces_usada"] == 0


def test_civil3d_cerrado_no_suma(servidor, puente, con_receta, monkeypatch):
    monkeypatch.setenv("ARBA_MCP_PORT", "1")
    r = receta(puente, parametros={"nombre_capa": "Capa B"}, simular=False)
    assert r["ok"] is False and r["error"] == puente.MENSAJE_SIN_CONEXION
    assert leer_yaml(con_receta)["veces_usada"] == 0


@pytest.mark.parametrize("parametros, mensaje", [
    ({"colour": 3, "nombre_capa": "x"}, "La receta no tiene los parámetros: colour"),
    ({}, "Falta el parámetro obligatorio 'nombre_capa'"),
    ({"nombre_capa": 5}, "debe ser string"),
])
def test_parametros_mal_no_llaman_al_plugin(servidor, puente, con_receta, parametros, mensaje):
    r = receta(puente, parametros=parametros, simular=False)
    assert r["ok"] is False and mensaje in r["error"] and "ms_puente" in r
    assert servidor.llamadas == [] and leer_yaml(con_receta)["veces_usada"] == 0


def test_receta_inexistente_o_incompleta_no_llama_al_plugin(servidor, puente, con_receta):
    r = receta(puente, nombre="no_existe_esta")
    assert r["ok"] is False and r["error"].startswith("No existe la receta 'no_existe_esta'")
    assert "Usa buscar_api" in r["error"] and f"Recetas de código de la carpeta: {NOMBRE}." in r["error"]
    assert receta(puente, nombre="../fuera")["error"].startswith("El nombre de receta '../fuera' no vale")
    (con_receta / "recetas" / "procedimiento.md").write_text("# Pasos\n1. listar", encoding="utf-8")
    assert "receta de procedimiento (procedimiento.md)" in receta(puente, nombre="procedimiento")["error"]
    (con_receta / "recetas" / "solo_datos.yaml").write_text("nombre: solo_datos\nparametros: []\n", encoding="utf-8")
    assert "falta solo_datos.cs" in receta(puente, nombre="solo_datos")["error"]
    (con_receta / "recetas" / "solo_codigo.cs").write_text("return 1;", encoding="utf-8")
    assert "falta solo_codigo.yaml" in receta(puente, nombre="solo_codigo")["error"]
    (con_receta / "recetas" / "rota.cs").write_text("return 1;", encoding="utf-8")
    (con_receta / "recetas" / "rota.yaml").write_text("parametros: [{nombre: x, tipo: raro}]\n", encoding="utf-8")
    assert "tiene parámetros inválidos" in receta(puente, nombre="rota")["error"]
    assert servidor.llamadas == []


# ---------------------------------------------------------------------------------------------- registro en el puente
def test_las_recetas_son_herramientas_locales_del_puente(servidor, puente, memoria, monkeypatch):
    assert {"guardar_receta", "ejecutar_receta"} <= puente.HERRAMIENTAS_LOCALES
    g = puente.mcp._tool_manager.get_tool("guardar_receta")
    assert g.parameters["required"] == ["nombre", "pedido", "codigo"], "argumentos directos, no dentro de 'args'"
    assert g.parameters["properties"]["aprobada_por_el_dueno"]["default"] is False
    descripcion = " ".join(g.description.split())
    assert "SOLO CON LA APROBACIÓN EXPLÍCITA DEL DUEÑO" in descripcion and "L.2" in descripcion
    e = puente.mcp._tool_manager.get_tool("ejecutar_receta")
    assert e.parameters["required"] == ["nombre"] and e.parameters["properties"]["simular"]["default"] is True
    assert "veces_usada" in e.description and "ejecutar_codigo" in e.description
    monkeypatch.setenv("ARBA_MCP_PORT", "1")   # Civil 3D cerrado
    assert ejecutar(puente.sincronizar_herramientas())["estado"] == "sin_conexion"
    nombres = [t.name for t in puente.mcp._tool_manager.list_tools()]
    assert "guardar_receta" in nombres and "ejecutar_receta" in nombres


def test_la_sincronizacion_no_las_quita_ni_las_pisa(servidor, puente):
    servidor.herramienta("ping")
    servidor.herramienta("guardar_receta", [], "del plugin: debe ignorarse")
    r = ejecutar(puente.sincronizar_herramientas())
    assert r["añadidas"] == ["ping"] and "guardar_receta" not in puente.known_tools
    assert puente.mcp._tool_manager.get_tool("guardar_receta").description.startswith("Guarda un script de ejecutar_codigo")
    servidor.tools = []
    ejecutar(puente.sincronizar_herramientas())
    nombres = [t.name for t in puente.mcp._tool_manager.list_tools()]
    assert "guardar_receta" in nombres and "ejecutar_receta" in nombres


def test_guardar_receta_por_el_puente_con_civil3d_cerrado(servidor, puente, memoria, monkeypatch):
    monkeypatch.setenv("ARBA_MCP_PORT", "1")
    args = {"nombre": NOMBRE, "pedido": PEDIDO, "codigo": CODIGO, "parametros": PARAMETROS}
    previa = json.loads(texto_mcp(ejecutar(puente.mcp.call_tool("guardar_receta", args))))
    assert previa["ok"] is True and previa["guardada"] is False and archivos(memoria) == []
    con_token = dict(args, codigo=CODIGO + "// " + servidor.token, aprobada_por_el_dueno=True, aprobacion_del_dueno=APROBACION)
    r = json.loads(texto_mcp(ejecutar(puente.mcp.call_tool("guardar_receta", con_token))))
    assert r["ok"] is False and "token del plugin (<oculto>)" in r["error"] and servidor.token not in json.dumps(r)
    texto = dict(args, parametros=json.dumps(PARAMETROS), aprobada_por_el_dueno=True, aprobacion_del_dueno=APROBACION)
    r = json.loads(texto_mcp(ejecutar(puente.mcp.call_tool("guardar_receta", texto))))
    assert r["ok"] is True and r["guardada"] is True
    assert leer_yaml(memoria)["version_conector"] == puente.__version__

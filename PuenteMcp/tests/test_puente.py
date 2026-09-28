# -*- coding: utf-8 -*-
"""Pruebas del puente sin Civil 3D: contra el servidor falso de conftest.py."""
import json

import httpx
import pytest

from conftest import ejecutar

P_CORREDOR = {"name": "corredor", "type": "string", "description": "Nombre del corredor", "required": True}
P_SIMULAR = {"name": "simular", "type": "boolean", "description": "Con true devuelve lo que haría sin tocar nada"}
P_TIMEOUT = {"name": "timeout_s", "type": "number", "description": "Segundos"}


def nombres(puente):
    return sorted(t.name for t in puente.mcp._tool_manager.list_tools())


# ---------------------------------------------------------------------------------------------- registro dinámico
def test_registra_las_herramientas_que_publica_el_plugin(servidor, puente):
    servidor.herramienta("ping")
    servidor.herramienta("listar_regiones", [P_CORREDOR], "Regiones de un corredor")
    r = ejecutar(puente.sincronizar_herramientas())
    assert r["estado"] == "ok" and r["añadidas"] == ["ping", "listar_regiones"] and r["quitadas"] == []
    assert nombres(puente) == ["listar_regiones", "ping"]
    herramienta = puente.mcp._tool_manager.get_tool("listar_regiones")
    assert herramienta.description == "Regiones de un corredor"
    esquema = herramienta.parameters
    modelo = esquema["$defs"]["listar_regiones_Model"]
    assert modelo["properties"]["corredor"]["type"] == "string" and modelo["required"] == ["corredor"]
    assert esquema["required"] == ["args"], "el agente pasa los argumentos dentro de 'args'"
    # segunda pasada sin cambios: nada que añadir ni quitar
    r = ejecutar(puente.sincronizar_herramientas())
    assert r == {"estado": "ok", "añadidas": [], "quitadas": []}


def test_quita_las_herramientas_que_desaparecen(servidor, puente):
    servidor.herramienta("ping")
    servidor.herramienta("vieja")
    ejecutar(puente.sincronizar_herramientas())
    assert "vieja" in nombres(puente)
    servidor.tools = [t for t in servidor.tools if t["name"] != "vieja"]
    r = ejecutar(puente.sincronizar_herramientas())
    assert r["quitadas"] == ["vieja"] and nombres(puente) == ["ping"]
    assert puente.known_tools == {"ping"}


def test_tipos_del_contrato(servidor, puente):
    servidor.herramienta("h", [
        {"name": "n", "type": "number", "required": True},
        {"name": "b", "type": "boolean"},
        {"name": "s", "type": "string"},
        {"name": "j", "type": "json", "description": "Lista JSON. Ejemplo: [{\"region\":\"0\"}]"},
    ])
    ejecutar(puente.sincronizar_herramientas())
    modelo = puente.mcp._tool_manager.get_tool("h").parameters["$defs"]["h_Model"]["properties"]
    assert modelo["n"]["type"] == "number"
    assert {"type": "boolean"} in modelo["b"]["anyOf"]
    assert {"type": "string"} in modelo["s"]["anyOf"]
    assert {"type": "string"} in modelo["j"]["anyOf"], "json se expone como texto"
    assert "Ejemplo" in modelo["j"]["description"]
    assert puente.type_to_python("json") is str


def test_sin_conexion_y_401_al_sincronizar(servidor, puente, monkeypatch):
    servidor.herramienta("ping")
    monkeypatch.setenv("ARBA_MCP_PORT", "1")   # nadie escucha
    assert ejecutar(puente.sincronizar_herramientas())["estado"] == "sin_conexion"
    monkeypatch.setenv("ARBA_MCP_PORT", str(servidor.puerto))
    servidor.token = "b" * 64                  # Civil 3D se reinició: el archivo aún tiene el token viejo
    r = ejecutar(puente.sincronizar_herramientas())
    assert r["estado"] == "401" and puente._cached_token is None, "olvida el token para releerlo"


# ---------------------------------------------------------------------------------------------- timeout por herramienta
@pytest.mark.parametrize("nombre, parametros, args, esperado", [
    ("listar_alineamientos", [], {}, 30),
    ("asignar_objetivo", [P_CORREDOR, P_SIMULAR], {}, 120),
    ("ejecutar_comando", [P_TIMEOUT], {}, 120),
    ("abrir_dibujo", [], {}, 120),
    ("reconstruir_corredor", [P_CORREDOR, P_SIMULAR], {}, 300),
    ("exportar_landxml", [P_TIMEOUT, P_SIMULAR], {}, 300),
    ("ejecutar_comando", [P_TIMEOUT], {"timeout_s": 200}, 210),
    ("exportar_imx", [P_TIMEOUT], {"timeout_s": 600}, 610),
    ("ejecutar_comando", [P_TIMEOUT], {"timeout_s": 5}, 120),
    ("ejecutar_comando", [P_TIMEOUT], {"timeout_s": "no"}, 120),
])
def test_timeout_por_herramienta(puente, nombre, parametros, args, esperado):
    assert puente._timeout_s(nombre, parametros, args) == esperado


def test_el_timeout_elegido_viaja_en_la_peticion(servidor, puente):
    servidor.herramienta("reconstruir_corredor", [P_CORREDOR, P_SIMULAR])
    servidor.respuestas["reconstruir_corredor"] = {"ok": True, "tool": "reconstruir_corredor", "ms": 5, "result": {"simulado": False}}
    ejecutar(puente.sincronizar_herramientas())
    ejecutar(puente.mcp.call_tool("reconstruir_corredor", {"args": {"corredor": "C1"}}))
    assert servidor.llamadas[-1] == {"tool": "reconstruir_corredor", "args": {"corredor": "C1"}, "timeout_s": 300}


# ---------------------------------------------------------------------------------------------- token y 401
def test_reintenta_una_vez_ante_401_releyendo_el_token(servidor, puente, tmp_path):
    servidor.respuestas["ping"] = {"ok": True, "result": {"plugin": "ArbaMcp"}}
    assert ejecutar(puente._c3d_execute("ping", {}, 30)) == {"plugin": "ArbaMcp"}
    # Civil 3D arranca de nuevo: token nuevo en el archivo, el puente aún tiene el viejo en memoria
    servidor.token = "c" * 64
    (tmp_path / "token").write_text(servidor.token, encoding="utf-8")
    assert ejecutar(puente._c3d_execute("ping", {}, 30)) == {"plugin": "ArbaMcp"}
    tokens = [t for (m, r, t) in servidor.peticiones if r == "/execute"]
    assert tokens == ["a" * 64, "a" * 64, "c" * 64], "una petición con el viejo (401) y el reintento con el nuevo"
    # si el archivo tampoco tiene el token bueno, el reintento es uno solo y se informa
    servidor.token = "d" * 64
    r = ejecutar(puente._c3d_execute("ping", {}, 30))
    assert r == {"ok": False, "error": "Error HTTP 401 de Civil 3D"}
    assert len([1 for (m, ru, t) in servidor.peticiones if ru == "/execute"]) == 5


# ---------------------------------------------------------------------------------------------- JSON y errores
def test_devuelve_json_valido_con_tildes(servidor, puente):
    servidor.herramienta("listar_regiones", [P_CORREDOR])
    servidor.respuestas["listar_regiones"] = {"ok": True, "result": [{"nombre": "Región 1 – Ñandú", "ensamblaje": "Calzada 7 m", "inicio": 0.0}]}
    ejecutar(puente.sincronizar_herramientas())
    resultado = ejecutar(puente.mcp.call_tool("listar_regiones", {"args": {"corredor": "C1"}}))
    texto = resultado.content[0].text
    assert "Región 1 – Ñandú" in texto and "\\u00" not in texto
    assert json.loads(texto)["result"][0]["ensamblaje"] == "Calzada 7 m"   # las listas van envueltas con los tiempos (1.3.0)


def test_a_texto_con_objetos_no_serializables():
    import main
    assert main._a_texto({"a": 1}) == '{"a": 1}'
    assert json.loads(main._a_texto({1, 2}))["ok"] is True


def test_error_del_plugin_se_traduce(servidor, puente):
    servidor.respuestas["asignar_objetivo"] = {"ok": False, "error": "No existe el corredor 'X'."}
    r = ejecutar(puente._c3d_execute("asignar_objetivo", {"corredor": "X"}, 120))
    assert r == {"ok": False, "error": "Error de Civil 3D: No existe el corredor 'X'."}


def test_connect_error_se_traduce_al_mensaje_del_contrato(puente, monkeypatch):
    monkeypatch.setenv("ARBA_MCP_PORT", "1")
    r = ejecutar(puente._c3d_execute("ping", {}, 30))
    assert r == {"ok": False, "error": "Civil 3D no está abierto o ArbaMcp no cargó; pulsa Conexión IA en la pestaña ARBA"}


def test_timeout_se_traduce_al_mensaje_del_contrato(servidor, puente, monkeypatch):
    async def lento(*a, **k):
        raise httpx.ReadTimeout("lento")
    monkeypatch.setattr(httpx.AsyncClient, "post", lento)
    r = ejecutar(puente._c3d_execute("reconstruir_corredor", {"corredor": "C1"}, 300))
    assert r == {"ok": False, "error": "Civil 3D no respondió en 300 s; puede estar ocupado o con un cuadro de diálogo abierto (usa capturar_pantalla)"}


def test_timeout_real_con_un_servidor_lento(servidor, puente, monkeypatch):
    # El puente espera timeout_s + 10 segundos: con timeout_s=0 y el servidor durmiendo más de 10 s tardaría;
    # se acorta el margen con un reloj falso vía monkeypatch del cliente.
    servidor.retardo_s = 0.6
    servidor.respuestas["ping"] = {"ok": True, "result": {}}
    original = httpx.AsyncClient.post

    async def post_corto(self, *a, **k):
        k["timeout"] = 0.2
        return await original(self, *a, **k)
    monkeypatch.setattr(httpx.AsyncClient, "post", post_corto)
    r = ejecutar(puente._c3d_execute("ping", {}, 7))
    assert r["ok"] is False and "7 s" in r["error"]


# ---------------------------------------------------------------------------------------------- servidor
def test_construir_app_expone_mcp_y_sse(puente):
    app = puente.construir_app()
    rutas = sorted(getattr(r, "path", "") for r in app.routes)
    assert "/mcp" in rutas and "/sse" in rutas
    assert puente.PUERTO_PUENTE == 8001
    assert puente.__version__ == "1.3.1"

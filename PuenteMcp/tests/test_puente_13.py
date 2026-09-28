# -*- coding: utf-8 -*-
"""Bloque 3 (1.3.0): ms_puente, /ping sin token mientras Civil 3D arranca, nombres retirados."""
import asyncio
import json

import pytest

from conftest import ServidorFalso, ejecutar

P_CORREDOR = {"name": "corredor", "type": "string", "description": "Nombre del corredor", "required": True}


def texto(resultado):
    return json.loads(resultado.content[0].text)


# ---------------------------------------------------------------------------------------------- ms_puente
def test_ms_puente_junto_al_ms_de_civil_en_un_resultado_objeto(servidor, puente):
    servidor.herramienta("estado_corredor", [P_CORREDOR])
    servidor.respuestas["estado_corredor"] = {"ok": True, "tool": "estado_corredor", "ms": 42, "ms_espera": 3, "ms_ejecucion": 39,
                                              "result": {"esta_desactualizado": False, "ms": 7}}
    ejecutar(puente.sincronizar_herramientas())
    r = texto(ejecutar(puente.mcp.call_tool("estado_corredor", {"args": {"corredor": "C1"}})))
    assert r["esta_desactualizado"] is False
    assert r["ms"] == 7, "una clave ms propia del resultado no se pisa"
    assert r["ms_espera"] == 3 and r["ms_ejecucion"] == 39
    assert isinstance(r["ms_puente"], int) and r["ms_puente"] >= 0


def test_ms_puente_envuelve_una_lista(servidor, puente):
    servidor.herramienta("listar_alineamientos")
    servidor.respuestas["listar_alineamientos"] = {"ok": True, "tool": "listar_alineamientos", "ms": 12, "ms_espera": 0, "ms_ejecucion": 12,
                                                   "result": [{"nombre": "Eje"}]}
    ejecutar(puente.sincronizar_herramientas())
    r = texto(ejecutar(puente.mcp.call_tool("listar_alineamientos", {"args": {}})))
    assert r["result"] == [{"nombre": "Eje"}]
    assert r["ms"] == 12 and r["ms_espera"] == 0 and r["ms_ejecucion"] == 12 and "ms_puente" in r


def test_ms_puente_en_los_errores(servidor, puente):
    servidor.herramienta("asignar_objetivo", [P_CORREDOR])
    servidor.respuestas["asignar_objetivo"] = {"ok": False, "error": "No existe el corredor 'X'."}
    ejecutar(puente.sincronizar_herramientas())
    r = texto(ejecutar(puente.mcp.call_tool("asignar_objetivo", {"args": {"corredor": "X"}})))
    assert r["ok"] is False and r["error"] == "Error de Civil 3D: No existe el corredor 'X'." and "ms_puente" in r


def test_con_tiempos_sin_tiempos_del_plugin(puente):
    assert puente.con_tiempos({"resultado": "terminado", "tiempos": {}}, 5) == {"result": "terminado", "ms_puente": 5}
    assert puente.con_tiempos({"resultado": {"a": 1}, "tiempos": {"ms": 2}}, 5) == {"a": 1, "ms": 2, "ms_puente": 5}
    assert puente.con_tiempos({"error": "x"}, 5) == {"ok": False, "error": "x", "ms_puente": 5}


# ---------------------------------------------------------------------------------------------- /ping sin token
def test_ping_sin_token_devuelve_la_version(servidor, puente):
    r = ejecutar(puente.ping_plugin())
    assert r == {"ok": True, "servidor": "ArbaMcp", "version": "1.3.0"}
    assert [t for (m, ru, t) in servidor.peticiones if ru == "/ping"] == [None], "sin cabecera de token"


def test_ping_con_el_plugin_apagado(puente, monkeypatch):
    monkeypatch.setenv("ARBA_MCP_PORT", "1")
    assert ejecutar(puente.ping_plugin()) is None
    assert ejecutar(puente.esperar_plugin(intervalo_s=0.05, maximo_s=0.2)) is False


def test_esperar_plugin_hasta_que_arranca_y_releer_el_token(puente, tmp_path, monkeypatch):
    tardio = ServidorFalso()
    tardio.herramienta("ping")

    async def escenario():
        monkeypatch.setenv("ARBA_MCP_PORT", "1")
        puente._cached_token = "viejo"
        tarea = asyncio.create_task(puente.esperar_plugin(intervalo_s=0.05, maximo_s=5))
        await asyncio.sleep(0.15)
        assert not tarea.done()
        tardio.arrancar()                                   # Civil 3D termina de arrancar
        monkeypatch.setenv("ARBA_MCP_PORT", str(tardio.puerto))
        return await tarea

    try:
        assert ejecutar(escenario()) is True
        assert puente._cached_token is None, "tras esperar se relee el token del archivo"
    finally:
        tardio.parar()


# ---------------------------------------------------------------------------------------------- nombres retirados
def test_tabla_de_retiradas_vacia_y_mecanismo_instalado(puente):
    assert puente.HERRAMIENTAS_RETIRADAS == {}
    assert puente.RETIRADAS_INSTALADAS_EN == "_tool_manager.call_tool"
    assert getattr(puente.mcp._tool_manager.call_tool, "_retiradas_instaladas", False) is True
    assert puente.instalar_retiradas(puente.mcp) == "_tool_manager.call_tool", "instalar dos veces no envuelve dos veces"
    assert puente.mensaje_retirada("nada") == "Unknown tool: nada"


def test_un_nombre_retirado_responde_con_la_sustituta(servidor, puente, monkeypatch):
    from mcp.server.mcpserver.exceptions import ToolError

    monkeypatch.setitem(puente.HERRAMIENTAS_RETIRADAS, "listar_objetivos_region", "listar_objetivos(corredor=..., linea_base=..., region=...)")
    with pytest.raises(ToolError) as info:
        ejecutar(puente.mcp._tool_manager.call_tool("listar_objetivos_region", {"args": {}}, None))
    assert "se retiró en 1.3.0" in str(info.value) and "listar_objetivos(" in str(info.value)
    # si el nombre vuelve a existir en el plugin, gana la herramienta real
    servidor.herramienta("listar_objetivos_region")
    servidor.respuestas["listar_objetivos_region"] = {"ok": True, "result": []}
    ejecutar(puente.sincronizar_herramientas())
    r = texto(ejecutar(puente.mcp.call_tool("listar_objetivos_region", {"args": {}})))
    assert r["result"] == []

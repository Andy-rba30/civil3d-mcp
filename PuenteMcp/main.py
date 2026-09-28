"""Puente MCP para Civil 3D: expone al agente, por streamable-http en 127.0.0.1:8001/mcp, las herramientas que publica
el plugin ArbaMcp en GET /tools (127.0.0.1:8765). Registra las herramientas de forma dinámica, elige el tiempo máximo
por herramienta y traduce los errores de conexión al contrato (CONTRATO.md).

La construcción del servidor está separada de uvicorn para poder probar el puente sin Civil 3D (tests/).
"""
import os
import sys
import json
import asyncio
import typing

import httpx
import anyio
import uvicorn
from mcp.server.mcpserver import MCPServer
from pydantic import create_model, Field

__version__ = "1.3.0"

CARPETA = os.path.dirname(os.path.abspath(__file__))


def _leer_instrucciones() -> str:
    """Instrucciones para el agente (precedencia API > comando, flujo de corredores, glosario)."""
    ruta = os.path.join(CARPETA, "INSTRUCCIONES_AGENTE.md")
    try:
        with open(ruta, "r", encoding="utf-8") as f:
            return f.read()
    except Exception as e:
        print(f"Aviso: no se pudo leer {ruta}: {e}")
        return ""


try:
    mcp = MCPServer("Civil 3D MCP Server", instructions=_leer_instrucciones())
except TypeError:
    # Versiones del SDK sin el argumento 'instructions'
    mcp = MCPServer("Civil 3D MCP Server")

ARBA_HOST = "127.0.0.1"
PUERTO_PUENTE = 8001


def puerto_plugin() -> int:
    """Puerto del servidor del plugin (ARBA_MCP_PORT, por defecto 8765). Se lee en cada uso para poder probarlo."""
    return int(os.environ.get("ARBA_MCP_PORT", 8765))


def base_url() -> str:
    return f"http://{ARBA_HOST}:{puerto_plugin()}"


def ruta_token() -> str:
    """%LOCALAPPDATA%\\ArbaMcp\\token, o la ruta de ARBA_MCP_TOKEN_FILE (pruebas)."""
    return os.environ.get("ARBA_MCP_TOKEN_FILE") or os.path.expandvars(r"%LOCALAPPDATA%\ArbaMcp\token")


# Tiempo máximo por herramienta (segundos). Lectura 30 s; escritura 120 s; reconstrucciones y exportaciones 300 s.
TIMEOUT_LECTURA = 30
TIMEOUT_ESCRITURA = 120
TIMEOUT_LARGO = 300
HERRAMIENTAS_LARGAS = {"reconstruir_corredor", "reconstruir_superficie", "exportar_landxml", "exportar_imx"}
HERRAMIENTAS_ESCRITURA_SIN_SIMULAR = {"ejecutar_comando", "abrir_dibujo"}


def _timeout_s(nombre: str, parametros: list, args: dict) -> int:
    if nombre in HERRAMIENTAS_LARGAS:
        base = TIMEOUT_LARGO
    elif nombre in HERRAMIENTAS_ESCRITURA_SIN_SIMULAR or any(p.get("name") == "simular" for p in parametros):
        base = TIMEOUT_ESCRITURA
    else:
        base = TIMEOUT_LECTURA
    # Si la herramienta recibe su propio timeout_s (ejecutar_comando, exportar_*), el del puente debe superarlo
    try:
        propio = float(args.get("timeout_s") or 0)
    except (TypeError, ValueError):
        propio = 0
    return int(max(base, propio + 10))


# ---------------------------------------------------------------------------------------------- token y cliente HTTP
_cached_token = None


def _get_token() -> str:
    global _cached_token
    if _cached_token:
        return _cached_token
    try:
        with open(ruta_token(), "r", encoding="utf-8") as f:
            _cached_token = f.read().strip()
    except Exception as e:
        print(f"Warning: could not read token: {e}")
        _cached_token = ""
    return _cached_token


def _clear_token():
    global _cached_token
    _cached_token = None


_http_client = None
_http_client_base = None


def _get_client() -> httpx.AsyncClient:
    global _http_client, _http_client_base
    url = base_url()
    if _http_client is None or _http_client.is_closed or _http_client_base != url:
        _http_client = httpx.AsyncClient(base_url=url, limits=httpx.Limits(max_keepalive_connections=10, max_connections=20))
        _http_client_base = url
    return _http_client


async def cerrar_cliente():
    global _http_client
    if _http_client is not None and not _http_client.is_closed:
        await _http_client.aclose()
    _http_client = None


def _error(mensaje: str) -> dict:
    return {"ok": False, "error": mensaje}


MENSAJE_SIN_CONEXION = "Civil 3D no está abierto o ArbaMcp no cargó; pulsa Conexión IA en la pestaña ARBA"


def _mensaje_timeout(timeout_s: int) -> str:
    return f"Civil 3D no respondió en {timeout_s} s; puede estar ocupado o con un cuadro de diálogo abierto (usa capturar_pantalla)"


async def _c3d_execute(tool_name: str, args: dict, timeout_s: int = TIMEOUT_ESCRITURA, retry=True):
    """Ejecuta una herramienta en el plugin. Devuelve el resultado (objeto JSON) o {"ok": false, "error": ...}.

    Ante un 401 relee el token del archivo (Civil 3D lo cambia en cada arranque) y reintenta una sola vez.
    """
    try:
        client = _get_client()
        headers = {"Content-Type": "application/json"}
        token = _get_token()
        if token:
            headers["X-Arba-Token"] = token

        response = await client.post("/execute", json={"tool": tool_name, "args": args, "timeout_s": timeout_s}, headers=headers, timeout=timeout_s + 10.0)

        if response.status_code == 401 and retry:
            _clear_token()
            return await _c3d_execute(tool_name, args, timeout_s, retry=False)

        if response.status_code != 200:
            return _error(f"Error HTTP {response.status_code} de Civil 3D")

        data = response.json()
        if not data.get("ok"):
            return _error(f"Error de Civil 3D: {data.get('error')}")
        return data.get("result", data)
    except httpx.ConnectError:
        return _error(MENSAJE_SIN_CONEXION)
    except httpx.TimeoutException:
        return _error(_mensaje_timeout(timeout_s))
    except Exception as e:
        return _error(f"Error de conexión: {e}")


def _a_texto(resultado) -> str:
    """JSON válido para el agente (no la representación de Python), con las tildes tal cual."""
    try:
        return json.dumps(resultado, ensure_ascii=False)
    except (TypeError, ValueError):
        return json.dumps({"ok": True, "result": str(resultado)}, ensure_ascii=False)


def type_to_python(t: str):
    """Tipos del contrato: number → float, boolean → bool, string y json → str (json es un texto con un valor JSON)."""
    if t == "number":
        return float
    if t == "boolean":
        return bool
    return str


# ---------------------------------------------------------------------------------------------- registro dinámico
known_tools = set()


def _crear_handler(nombre: str, parametros: list, descripcion: str):
    """Función que el SDK expone como herramienta MCP: un único argumento 'args' con el modelo de la herramienta."""
    fields = {}
    for p in parametros:
        ptype = type_to_python(p["type"])
        desc = p.get("description", "")
        if p.get("required", False):
            fields[p["name"]] = (ptype, Field(..., description=desc))
        else:
            fields[p["name"]] = (typing.Optional[ptype], Field(None, description=desc))
    SchemaModel = create_model(f"{nombre}_Model", **fields)

    async def handler(args: SchemaModel) -> str:  # type: ignore[valid-type]
        datos = args.model_dump(exclude_none=True)
        return _a_texto(await _c3d_execute(nombre, datos, _timeout_s(nombre, parametros, datos)))

    handler.__name__ = nombre
    handler.__doc__ = descripcion or ""
    return handler


async def sincronizar_herramientas() -> dict:
    """Una pasada: lee GET /tools, registra las herramientas nuevas y quita las que ya no están.

    Devuelve {"estado": "ok" | "401" | "sin_conexion" | "error", "añadidas": [...], "quitadas": [...]}.
    """
    resultado = {"estado": "error", "añadidas": [], "quitadas": []}
    try:
        client = _get_client()
        headers = {}
        token = _get_token()
        if token:
            headers["X-Arba-Token"] = token
        res = await client.get("/tools", headers=headers, timeout=5.0)
    except httpx.ConnectError:
        resultado["estado"] = "sin_conexion"
        return resultado
    except Exception as e:
        resultado["detalle"] = str(e)
        return resultado

    if res.status_code == 401:
        _clear_token()
        resultado["estado"] = "401"
        return resultado
    if res.status_code != 200:
        resultado["detalle"] = f"HTTP {res.status_code}"
        return resultado
    data = res.json()
    if not data.get("ok"):
        resultado["detalle"] = data.get("error")
        return resultado

    herramientas = data.get("tools", [])
    actuales = set(t["name"] for t in herramientas)
    for tname in list(known_tools):
        if tname not in actuales:
            mcp.remove_tool(tname)
            known_tools.remove(tname)
            resultado["quitadas"].append(tname)
    for info in herramientas:
        if info["name"] in known_tools:
            continue
        mcp.add_tool(_crear_handler(info["name"], info.get("parameters", []), info.get("description", "")))
        known_tools.add(info["name"])
        resultado["añadidas"].append(info["name"])
    resultado["estado"] = "ok"
    return resultado


async def update_tools_loop():
    while True:
        try:
            await sincronizar_herramientas()
        except Exception:
            pass
        await asyncio.sleep(5)


# ---------------------------------------------------------------------------------------------- servidor
def construir_app():
    """Aplicación ASGI con streamable-http (/mcp) y SSE (/sse, /messages/), sin arrancar uvicorn."""
    http_app = mcp.streamable_http_app(host="127.0.0.1", stateless_http=True, json_response=True)
    sse_app = mcp.sse_app(host="127.0.0.1")
    for route in sse_app.routes:
        http_app.routes.append(route)
    return http_app


async def run_combined_async():
    asyncio.create_task(update_tools_loop())
    config = uvicorn.Config(construir_app(), host="127.0.0.1", port=PUERTO_PUENTE, log_level="info")
    server = uvicorn.Server(config)
    await server.serve()


if __name__ == "__main__":
    anyio.run(run_combined_async)

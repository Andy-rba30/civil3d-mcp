import os
import sys
import json
import httpx
import anyio
import asyncio
import uvicorn
from mcp.server.mcpserver import MCPServer
from mcp.types import Tool
from pydantic import create_model, Field
import typing

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
ARBA_PORT = int(os.environ.get("ARBA_MCP_PORT", 8765))
BASE_URL = f"http://{ARBA_HOST}:{ARBA_PORT}"

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


_cached_token = None
def _get_token() -> str:
    global _cached_token
    if _cached_token:
        return _cached_token
    token_path = os.path.expandvars(r"%LOCALAPPDATA%\ArbaMcp\token")
    try:
        with open(token_path, "r", encoding="utf-8") as f:
            _cached_token = f.read().strip()
    except Exception as e:
        print(f"Warning: could not read token: {e}")
        _cached_token = ""
    return _cached_token

def _clear_token():
    global _cached_token
    _cached_token = None

_http_client = None
def _get_client():
    global _http_client
    if _http_client is None or _http_client.is_closed:
        _http_client = httpx.AsyncClient(base_url=BASE_URL, limits=httpx.Limits(max_keepalive_connections=10, max_connections=20))
    return _http_client


def _error(mensaje: str) -> dict:
    return {"ok": False, "error": mensaje}


async def _c3d_execute(tool_name: str, args: dict, timeout_s: int = TIMEOUT_ESCRITURA, retry=True):
    """Ejecuta una herramienta en el plugin. Devuelve el resultado (objeto JSON) o {"ok": false, "error": ...}."""
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
        return _error("Civil 3D no está abierto o ArbaMcp no cargó; pulsa Conexión IA en la pestaña ARBA")
    except httpx.TimeoutException:
        return _error(f"Civil 3D no respondió en {timeout_s} s; puede estar ocupado o con un cuadro de diálogo abierto (usa capturar_pantalla)")
    except Exception as e:
        return _error(f"Error de conexión: {e}")


def _a_texto(resultado) -> str:
    """JSON válido para el agente (no la representación de Python)."""
    try:
        return json.dumps(resultado, ensure_ascii=False)
    except (TypeError, ValueError):
        return json.dumps({"ok": True, "result": str(resultado)}, ensure_ascii=False)

def type_to_python(t: str):
    if t == "number": return float
    if t == "boolean": return bool
    return str

known_tools = set()

async def update_tools_loop():
    global known_tools
    while True:
        try:
            client = _get_client()
            headers = {}
            token = _get_token()
            if token:
                headers["X-Arba-Token"] = token
                
            res = await client.get("/tools", headers=headers, timeout=5.0)
            if res.status_code == 401:
                _clear_token()
            elif res.status_code == 200:
                data = res.json()
                if data.get("ok"):
                    current_tool_names = set(t["name"] for t in data.get("tools", []))
                    for tname in list(known_tools):
                        if tname not in current_tool_names:
                            mcp.remove_tool(tname)
                            known_tools.remove(tname)
                    for tool_info in data.get("tools", []):
                        if tool_info["name"] not in known_tools:
                            parametros = tool_info.get("parameters", [])
                            fields = {}
                            for p in parametros:
                                ptype = type_to_python(p["type"])
                                desc = p.get("description", "")
                                if p.get("required", False):
                                    fields[p["name"]] = (ptype, Field(..., description=desc))
                                else:
                                    fields[p["name"]] = (typing.Optional[ptype], Field(None, description=desc))
                            
                            SchemaModel = create_model(f"{tool_info['name']}_Model", **fields)
                            
                            def make_handler(tname, tparams):
                                async def handler(args: SchemaModel) -> str:
                                    datos = args.model_dump(exclude_none=True)
                                    return _a_texto(await _c3d_execute(tname, datos, _timeout_s(tname, tparams, datos)))
                                return handler
                            
                            handler = make_handler(tool_info["name"], parametros)
                            handler.__name__ = tool_info["name"]
                            handler.__doc__ = tool_info.get("description", "")
                            
                            mcp.add_tool(handler)
                            known_tools.add(tool_info["name"])
        except Exception as e:
            pass
        await asyncio.sleep(5)

async def run_combined_async():
    asyncio.create_task(update_tools_loop())
    http_app = mcp.streamable_http_app(host="127.0.0.1", stateless_http=True, json_response=True)
    sse_app = mcp.sse_app(host="127.0.0.1")
    for route in sse_app.routes:
        http_app.routes.append(route)
    config = uvicorn.Config(http_app, host="127.0.0.1", port=8001, log_level="info")
    server = uvicorn.Server(config)
    await server.serve()

if __name__ == "__main__":
    anyio.run(run_combined_async)

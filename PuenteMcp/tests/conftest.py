"""Servidor HTTP falso que imita el contrato de ArbaMcp (GET /ping, GET /tools, POST /execute) en un hilo."""
import json
import os
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import main  # noqa: E402

TOKEN = "a" * 64


class ServidorFalso:
    """Estado compartido con el manejador: token válido, herramientas publicadas, respuestas y llamadas recibidas."""

    def __init__(self):
        self.token = TOKEN
        self.tools = []
        self.respuestas = {}      # nombre → dict de respuesta de /execute, o función(cuerpo) → dict
        self.llamadas = []        # cuerpos JSON recibidos en /execute
        self.peticiones = []      # (método, ruta, token recibido)
        self.retardo_s = 0.0      # espera antes de responder /execute
        self.version = "1.3.0"
        self.ping_publico = True
        self.httpd = None
        self.puerto = None

    def herramienta(self, nombre, parametros=None, descripcion=""):
        self.tools.append({"name": nombre, "description": descripcion, "parameters": parametros or []})

    def arrancar(self):
        estado = self

        class Manejador(BaseHTTPRequestHandler):
            def log_message(self, *a):
                pass

            def _responder(self, codigo, cuerpo):
                datos = json.dumps(cuerpo, ensure_ascii=False).encode("utf-8")
                self.send_response(codigo)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Content-Length", str(len(datos)))
                self.end_headers()
                self.wfile.write(datos)

            def _autorizada(self, ruta):
                recibido = self.headers.get("X-Arba-Token")
                estado.peticiones.append((self.command, ruta, recibido))
                if self.command == "GET" and ruta == "/ping" and estado.ping_publico:
                    return True
                return recibido == estado.token

            def do_GET(self):
                ruta = self.path.split("?")[0]
                if not self._autorizada(ruta):
                    return self._responder(401, {"ok": False, "error": "Unauthorized"})
                if ruta == "/ping":
                    return self._responder(200, {"ok": True, "servidor": "ArbaMcp", "version": estado.version})
                if ruta == "/tools":
                    return self._responder(200, {"ok": True, "tools": estado.tools})
                self._responder(404, {"ok": False, "error": "Ruta no encontrada"})

            def do_POST(self):
                ruta = self.path.split("?")[0]
                largo = int(self.headers.get("Content-Length") or 0)
                cuerpo = json.loads(self.rfile.read(largo) or b"{}")
                if not self._autorizada(ruta):
                    return self._responder(401, {"ok": False, "error": "Unauthorized"})
                if ruta != "/execute":
                    return self._responder(404, {"ok": False, "error": "Ruta no encontrada"})
                estado.llamadas.append(cuerpo)
                if estado.retardo_s:
                    time.sleep(estado.retardo_s)
                respuesta = estado.respuestas.get(cuerpo.get("tool"))
                if callable(respuesta):
                    respuesta = respuesta(cuerpo)
                if respuesta is None:
                    respuesta = {"ok": False, "error": f"Herramienta desconocida: '{cuerpo.get('tool')}'. Consulta GET /tools."}
                self._responder(200, respuesta)

        self.httpd = ThreadingHTTPServer(("127.0.0.1", 0), Manejador)
        self.puerto = self.httpd.server_address[1]
        hilo = threading.Thread(target=self.httpd.serve_forever, daemon=True)
        hilo.start()
        return self

    def parar(self):
        if self.httpd is not None:
            self.httpd.shutdown()
            self.httpd.server_close()


@pytest.fixture
def servidor():
    s = ServidorFalso().arrancar()
    yield s
    s.parar()


@pytest.fixture
def puente(servidor, tmp_path, monkeypatch):
    """El módulo main apuntando al servidor falso, con el token en un archivo temporal y sin herramientas registradas."""
    archivo = tmp_path / "token"
    archivo.write_text(servidor.token, encoding="utf-8")
    monkeypatch.setenv("ARBA_MCP_PORT", str(servidor.puerto))
    monkeypatch.setenv("ARBA_MCP_TOKEN_FILE", str(archivo))
    _reiniciar()
    yield main
    _reiniciar()


def _reiniciar():
    main._clear_token()
    for nombre in list(main.known_tools):
        try:
            main.mcp.remove_tool(nombre)
        except Exception:
            pass
    main.known_tools.clear()
    import asyncio
    asyncio.run(main.cerrar_cliente())


def ejecutar(corutina):
    import asyncio
    return asyncio.run(corutina)

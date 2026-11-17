"""Puente MCP para Civil 3D: expone al agente, por streamable-http en 127.0.0.1:8001/mcp, las herramientas que publica
el plugin ArbaMcp en GET /tools (127.0.0.1:8765). Registra las herramientas de forma dinámica, elige el tiempo máximo
por herramienta, añade ms_puente a cada respuesta, sondea GET /ping (sin token) mientras Civil 3D arranca y traduce
los errores de conexión al contrato (CONTRATO.md).

La construcción del servidor está separada de uvicorn para poder probar el puente sin Civil 3D (tests/).
"""
import os
import sys
import json
import time
import asyncio
import typing

import httpx
import anyio
import uvicorn
from mcp.server.mcpserver import MCPServer
from pydantic import create_model, Field

import buscar_api as memoria_firma  # herramienta local del puente (1.5.0): busca en memoria-firma sin Civil 3D
import recetas  # herramientas locales del puente (1.17.0): guardar_receta y ejecutar_receta
import norma as norma_vial  # herramienta local del puente (1.18.0): verificar_norma (DG-2018)
import norma_alineamiento as norma_eje  # 1.19.0: crear_alineamiento_desde_polilinea con velocidad_diseno (DG-2018)

__version__ = "1.21.1"

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
HERRAMIENTAS_LARGAS = {"reconstruir_corredor", "crear_corredor", "crear_superficies_corredor", "crear_secciones", "calcular_volumenes", "reconstruir_superficie", "exportar_landxml", "exportar_imx"}
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


CLAVES_TIEMPO = ("ms", "ms_espera", "ms_ejecucion")
# Desde 1.6.0 el plugin devuelve en cada respuesta (también en los errores) el id de la línea que la llamada dejó en
# mcp_log.jsonl; el puente lo pasa al agente tal cual para que pueda citarlo en aprobar_resultado.
CLAVE_ID_LOG = "id_log"


async def _ejecutar(tool_name: str, args: dict, timeout_s: int = TIMEOUT_ESCRITURA, retry=True) -> dict:
    """POST /execute. Devuelve {"resultado": <result>, "tiempos": {ms, ms_espera, ms_ejecucion}} o
    {"error": <mensaje>} ya traducido al contrato.

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
            return await _ejecutar(tool_name, args, timeout_s, retry=False)

        if response.status_code != 200:
            return {"error": f"Error HTTP {response.status_code} de Civil 3D"}

        data = response.json()
        if not data.get("ok"):
            salida = {"error": f"Error de Civil 3D: {data.get('error')}"}
            if data.get(CLAVE_ID_LOG):
                salida[CLAVE_ID_LOG] = data[CLAVE_ID_LOG]
            return salida
        tiempos = {k: data[k] for k in CLAVES_TIEMPO if k in data}
        salida = {"resultado": data.get("result", data), "tiempos": tiempos}
        if data.get(CLAVE_ID_LOG):
            salida[CLAVE_ID_LOG] = data[CLAVE_ID_LOG]
        return salida
    except httpx.ConnectError:
        return {"error": MENSAJE_SIN_CONEXION}
    except httpx.TimeoutException:
        return {"error": _mensaje_timeout(timeout_s)}
    except Exception as e:
        return {"error": f"Error de conexión: {e}"}


async def _c3d_execute(tool_name: str, args: dict, timeout_s: int = TIMEOUT_ESCRITURA, retry=True):
    """Ejecuta una herramienta en el plugin. Devuelve el resultado (objeto JSON) o {"ok": false, "error": ...}."""
    r = await _ejecutar(tool_name, args, timeout_s, retry)
    if "error" in r:
        return _error(r["error"])
    return r["resultado"]


def con_tiempos(respuesta: dict, ms_puente: int):
    """Lo que ve el agente: el resultado de Civil 3D más ms (Civil 3D), ms_espera, ms_ejecucion y ms_puente (total
    visto desde Python) y, desde 1.6.0, id_log (la línea de mcp_log.jsonl de esta llamada). Si el resultado es un
    objeto, van como claves suyas (sin pisar las que ya tenga); si es una lista o un valor, se envuelve en
    {"result": ..., ...}. Un error lleva ok=false, error, ms_puente y, si el plugin lo dio, id_log."""
    if "error" in respuesta:
        salida = {"ok": False, "error": respuesta["error"], "ms_puente": ms_puente}
        if respuesta.get(CLAVE_ID_LOG):
            salida[CLAVE_ID_LOG] = respuesta[CLAVE_ID_LOG]
        return salida
    resultado = respuesta["resultado"]
    tiempos = dict(respuesta.get("tiempos") or {})
    if respuesta.get(CLAVE_ID_LOG):
        tiempos[CLAVE_ID_LOG] = respuesta[CLAVE_ID_LOG]
    tiempos["ms_puente"] = ms_puente
    if isinstance(resultado, dict):
        salida = dict(resultado)
        for k, v in tiempos.items():
            salida.setdefault(k, v)
        return salida
    salida = {"result": resultado}
    salida.update(tiempos)
    return salida


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


# ---------------------------------------------------------------------------------------------- herramientas locales
# Herramientas que viven en el puente y no en el plugin: se registran al arrancar, están disponibles aunque Civil 3D
# esté cerrado y la sincronización con GET /tools nunca las quita (no están en known_tools).
HERRAMIENTAS_LOCALES = {"buscar_api", "guardar_receta", "ejecutar_receta", "verificar_norma"}


async def buscar_api(consulta: str, limite: int = memoria_firma.LIMITE_POR_DEFECTO) -> str:
    """Busca en la memoria técnica de la firma (memoria-firma) antes de escribir código contra la API de Civil 3D:
    primero las recetas (recetas/*.md, procedimientos para seguir, y las recetas de código recetas/*.cs + *.yaml, que
    se reutilizan con ejecutar_receta), luego el glosario español → API (catalogo/glosario.yaml, con el estado
    'confirmado' o 'por confirmar' de cada concepto) y después el catálogo de la API de Civil 3D 2027 (clases,
    métodos con firma y parámetros, propiedades, obsoletos). La consulta se amplía con el glosario antes de buscar.
    Funciona con Civil 3D cerrado. Los argumentos van directos (consulta, limite), no dentro de 'args'. Necesita la
    variable de entorno MEMORIA_FIRMA_DIR; si falta algo, devuelve un texto que empieza por 'Error:' y dice cómo
    arreglarlo. Devuelve texto, no JSON."""
    return await anyio.to_thread.run_sync(memoria_firma.buscar, consulta, limite)


mcp.add_tool(buscar_api)


async def guardar_receta(nombre: str, pedido: str, codigo: str,
                         parametros: typing.Union[list, str, None] = None, notas: str = "",
                         aprobada_por_el_dueno: bool = False, aprobacion_del_dueno: str = "",
                         reemplazar: bool = False) -> str:
    """Guarda un script de ejecutar_codigo que ya funcionó como receta reutilizable de la memoria técnica:
    MEMORIA_FIRMA_DIR\\recetas\\<nombre>.cs (el código tal cual) y <nombre>.yaml (pedido de ejemplo en español,
    parámetros, versión de Civil 3D, fecha, veces_usada=0 y la aprobación del dueño). SOLO CON LA APROBACIÓN EXPLÍCITA
    DEL DUEÑO: sin aprobada_por_el_dueno=true no escribe nada y devuelve la vista previa (guardada=false); enséñasela,
    pregúntale si la guarda y, solo si responde que sí, repite la llamada con aprobada_por_el_dueno=true y sus palabras
    literales en aprobacion_del_dueno. Nunca lo decidas tú ni inventes su respuesta. nombre: de 3 a 64 caracteres en
    minúsculas, números y _ (crear_capa_con_color). pedido: lo que pidió el usuario, con sus palabras. parametros: lista
    JSON [{"nombre": "nombre_capa", "tipo": "string", "descripcion": "Nombre de la capa", "obligatorio": true},
    {"nombre": "color", "tipo": "number", "por_defecto": 7}] (tipos string, number, boolean, json); el script los lee de
    Parametros. Rechaza rutas absolutas, nombres .dwg, nombres de obra (casos\\PROHIBIDOS.txt) y el token: pásalos como
    parámetro. No sobrescribe una receta que ya existe salvo reemplazar=true, solo si el dueño pidió cambiarla.
    Argumentos directos (no dentro de 'args'); funciona con Civil 3D cerrado; responde JSON. Al guardar devuelve el
    recordatorio de correr L.2 para subir la receta a GitHub: díselo al dueño."""
    def guardar() -> dict:
        return recetas.guardar(nombre, pedido, codigo, parametros, notas, aprobada_por_el_dueno, aprobacion_del_dueno,
                               reemplazar, token=_get_token(), version_conector=__version__)

    return _a_texto(await anyio.to_thread.run_sync(guardar))


async def ejecutar_receta(nombre: str, parametros: typing.Union[dict, str, None] = None, simular: bool = True,
                          solo_lectura: bool = False, pedido: typing.Optional[str] = None) -> str:
    """Reutiliza una receta de código de la memoria técnica (buscar_api la muestra en RECETAS como «receta de
    código»): lee MEMORIA_FIRMA_DIR\\recetas\\<nombre>.cs y <nombre>.yaml, comprueba parametros contra los declarados
    (objeto JSON con un valor por parámetro, {"nombre_capa": "Capa 1"}; los que faltan toman su por_defecto) y ejecuta
    el código guardado con la herramienta ejecutar_codigo del plugin (tiene que estar activada). simular=true por
    defecto: simula primero, enseña el resultado, confirma con el usuario y después repite con simular=false. Solo una
    ejecución real (simular=false) que sale bien suma 1 a veces_usada; una simulación no suma. Si la receta solo lee
    (contar, listar, medir), pasa solo_lectura=true (desde 1.17.6, igual que en ejecutar_codigo): corre con el dibujo
    bloqueado para lectura, sin copia de seguridad y sin entrada en Deshacer, no compila si el código escribe, y si sale
    bien suma 1 a veces_usada. Devuelve la respuesta de ejecutar_codigo (resultado, id_log, tiempos) más el bloque
    receta (nombre, parametros usados, veces_usada, sumada y nota). Envía pedido con las palabras del usuario.
    Argumentos directos (no dentro de 'args'); responde JSON."""
    inicio = time.perf_counter()

    def ms() -> int:
        return int((time.perf_counter() - inicio) * 1000)

    try:
        preparada = await anyio.to_thread.run_sync(
            lambda: recetas.preparar_ejecucion(nombre, parametros, simular, pedido, solo_lectura=solo_lectura))
    except recetas.ErrorReceta as e:
        return _a_texto({"ok": False, "error": str(e), "ms_puente": ms()})
    args = preparada["args"]
    respuesta = recetas.traducir_error(
        await _ejecutar("ejecutar_codigo", args, _timeout_s("ejecutar_codigo", [{"name": "simular"}], args)))
    bloque = await anyio.to_thread.run_sync(lambda: recetas.cerrar_ejecucion(preparada, respuesta))
    salida = con_tiempos(respuesta, ms())
    salida["receta"] = bloque
    return _a_texto(salida)


mcp.add_tool(guardar_receta)
mcp.add_tool(ejecutar_receta)


def _args_lectura(args: dict, pedido: typing.Optional[str]) -> dict:
    if pedido:
        args["pedido"] = pedido
    return args


async def verificar_norma(norma: str, tipo_elemento: str, nombre: str, alineamiento: typing.Optional[str] = None,
                          velocidad_diseno: typing.Optional[float] = None, clasificacion: typing.Optional[str] = None,
                          orografia: typing.Optional[str] = None, ubicacion: typing.Optional[str] = None,
                          peralte_maximo: typing.Optional[float] = None, pavimentada: typing.Optional[bool] = None,
                          altitud_msnm: typing.Optional[float] = None, cotas_msnm: typing.Optional[bool] = None,
                          pedido: typing.Optional[str] = None) -> str:
    """Verifica un eje o una rasante del dibujo contra la norma DG-2018 (solo norma='DG-2018') con la base
    MEMORIA_FIRMA_DIR\\normas\\tablas\\dg2018.sqlite. tipo_elemento='alineamiento' (nombre = el eje; se lee con
    listar_pis) o 'rasante' (nombre = el perfil y alineamiento = su eje; se leen listar_pis y listar_pvis). Datos del
    proyecto: velocidad_diseno (km/h, una celda de la T204.01), clasificacion ('Carretera de segunda clase'...),
    orografia (Plano, Ondulado, Accidentado, Escarpado), ubicacion (rural, urbana, rural con peligro de hielo),
    peralte_maximo (%; en un eje de Tercera Clase o en terreno accidentado), pavimentada (Tercera Clase),
    altitud_msnm o cotas_msnm=true (rasante en terreno accidentado o escarpado). SI FALTA UN DATO no lee el dibujo:
    responde ok=false con 'faltan' (dato, motivo y valores válidos); pregúntaselo al usuario, nunca lo supongas ni
    interpoles una velocidad. Si no, devuelve 'filas' (elemento, progresiva, comprobación, valor de diseño, valor
    exigido, resultado Cumple / No cumple / Cumple con aviso / Aviso / No verificable / Sin valor en la norma, y la
    referencia con tabla, sección y páginas), 'resumen', 'no_comprobado', 'avisos' y 'tabla' (markdown para mostrar).
    Manda la tabla de la norma; si la fórmula pide más, «Cumple con aviso». Solo lee el dibujo. Argumentos directos
    (no dentro de 'args'); responde JSON. Envía pedido con las palabras del usuario."""
    inicio = time.perf_counter()

    def ms() -> int:
        return int((time.perf_counter() - inicio) * 1000)

    datos = {"velocidad_diseno": velocidad_diseno, "clasificacion": clasificacion, "orografia": orografia,
             "ubicacion": ubicacion, "peralte_maximo": peralte_maximo, "pavimentada": pavimentada,
             "altitud_msnm": altitud_msnm, "cotas_msnm": cotas_msnm}
    try:
        preparada = await anyio.to_thread.run_sync(
            lambda: norma_vial.preparar(norma, tipo_elemento, nombre, alineamiento, datos))
    except norma_vial.ErrorNorma as e:
        return _a_texto({"ok": False, "error": str(e), "ms_puente": ms()})
    try:
        if preparada["faltan"]:
            salida = norma_vial.respuesta_faltan(preparada)
            salida["ms_puente"] = ms()
            return _a_texto(salida)
        lecturas = {}
        r_pis = await _ejecutar("listar_pis", _args_lectura({"alineamiento": preparada["eje"]}, pedido), TIMEOUT_LECTURA)
        if "error" in r_pis:
            return _a_texto(con_tiempos(r_pis, ms()))
        lecturas["listar_pis"] = r_pis.get(CLAVE_ID_LOG)
        pvis = None
        if preparada["tipo_elemento"] == "rasante" and not norma_vial.unidades_no_validas(r_pis["resultado"]):
            r_pvis = await _ejecutar("listar_pvis", _args_lectura(
                {"alineamiento": preparada["eje"], "perfil": preparada["perfil"]}, pedido), TIMEOUT_LECTURA)
            if "error" in r_pvis:
                return _a_texto(con_tiempos(r_pvis, ms()))
            lecturas["listar_pvis"] = r_pvis.get(CLAVE_ID_LOG)
            pvis = r_pvis["resultado"]
        salida = await anyio.to_thread.run_sync(
            lambda: norma_vial.verificar(preparada, r_pis["resultado"], pvis, lecturas))
    except norma_vial.ErrorNorma as e:
        return _a_texto({"ok": False, "error": str(e), "ms_puente": ms()})
    finally:
        preparada["base"].cerrar()
    salida["ms_puente"] = ms()
    return _a_texto(salida)


mcp.add_tool(verificar_norma)


# ---------------------------------------------------------------------------------------------- eje con la norma (1.19.0)
# Herramientas del plugin que el puente amplía con los datos del proyecto de la DG-2018 (norma_alineamiento.py). La
# ampliación se aplica en _crear_handler, el único camino por el que se registran las herramientas del plugin (también
# al volver a sincronizar), así que no se pierde.
AMPLIACIONES_NORMA = {"crear_alineamiento_desde_polilinea"}


async def _crear_eje_con_norma(nombre: str, parametros: list, datos: dict) -> dict:
    """crear_alineamiento_desde_polilinea con velocidad_diseno: valida los datos del proyecto (sin llamar al plugin si
    falta alguno), comprueba INSUNITS, simula una vez para leer los PI, propone el radio mínimo de la norma por PI,
    llama al plugin con radios_por_pi y adjunta la verificación (prevista si se simula). El puente no escribe nada:
    la única escritura es la llamada final del plugin, con sus reglas (copia, una entrada de Deshacer, log)."""
    inicio = time.perf_counter()

    def ms() -> int:
        return int((time.perf_counter() - inicio) * 1000)

    def con_ms(salida: dict) -> dict:
        salida["ms_puente"] = ms()
        return salida

    args, proyecto = norma_eje.separar(datos)
    timeout = _timeout_s(nombre, parametros, args)
    pedido = args.get("pedido")
    try:
        preparada = await anyio.to_thread.run_sync(
            lambda: norma_vial.preparar(norma_vial.NORMA, "alineamiento", args.get("nombre"), None, proyecto))
    except norma_vial.ErrorNorma as e:
        return con_ms({"ok": False, "error": str(e)})
    base, ctx = preparada["base"], preparada["contexto"]
    try:
        if preparada["faltan"]:
            return con_ms(norma_eje.respuesta_faltan(preparada["faltan"]))
        comunes = await anyio.to_thread.run_sync(lambda: norma_eje.minimos_del_proyecto(base, ctx))
        if not comunes and args.get("radio") is None:
            return con_ms({"ok": False, "error": f"La base de la norma no tiene radio mínimo (T302.02/T302.04) ni radio sin "
                                                 f"curva de transición (T302.11) para {ctx['velocidad']} km/h: no se puede "
                                                 "proponer. Pasa 'radio' (y 'radios_por_pi' si hace falta). No se llamó a "
                                                 "Civil 3D."})

        r_unidades = await _ejecutar("leer_variable", _args_lectura({"nombre": "INSUNITS"}, pedido), TIMEOUT_LECTURA)
        if "error" in r_unidades:
            return con_tiempos(r_unidades, ms())
        insunits = {"INSUNITS": (r_unidades["resultado"] or {}).get("valor") if isinstance(r_unidades["resultado"], dict)
                    else None, "id_log": r_unidades.get(CLAVE_ID_LOG)}
        motivo = norma_eje.unidades_no_metros(r_unidades["resultado"])
        if motivo:
            return con_ms({"ok": False, "error": motivo, "unidades": insunits})

        # Simulación de ensayo: solo para leer los PI (el plugin valida aquí nombre, polilínea y radios_por_pi)
        ensayo = dict(args)
        ensayo["simular"] = True
        if ensayo.get("radio") is None:
            ensayo["radio"] = norma_eje.RADIO_PROVISIONAL
        r_ensayo = await _ejecutar(nombre, ensayo, timeout)
        if "error" in r_ensayo:
            return con_tiempos(r_ensayo, ms())
        ensayo_id = r_ensayo.get(CLAVE_ID_LOG)
        despues_ensayo = (r_ensayo["resultado"] or {}).get("despues") if isinstance(r_ensayo["resultado"], dict) else None

        def calcular():
            geo = norma_eje.geometria(despues_ensayo)
            fijos = norma_eje.radios_del_usuario(args.get("radio"), args.get("radios_por_pi"), len(geo["pis"]))
            return norma_eje.proponer(base, ctx, geo, fijos)

        try:
            propuesta = await anyio.to_thread.run_sync(calcular)
        except norma_vial.ErrorNorma as e:
            return con_ms({"ok": False, "error": str(e), "propuesta_norma": {"simulacion_de_ensayo": {"id_log": ensayo_id}}})
        # 1.21.1: en la escritura real, un PI bajo la norma no se crea sin la confirmación del usuario (radios_por_pi)
        bloqueo = norma_eje.falta_confirmacion(propuesta, args.get("simular"))
        if bloqueo:
            bloqueo["propuesta_norma"] = norma_eje.bloque_propuesta(ctx, propuesta, None, insunits, ensayo_id)
            return con_ms(bloqueo)

        final = dict(args)
        final["radio"] = norma_eje.radio_por_defecto(args.get("radio"), propuesta["radios_por_pi"])
        final["radios_por_pi"] = json.dumps(propuesta["radios_por_pi"])
        bloque = norma_eje.bloque_propuesta(ctx, propuesta, final["radio"], insunits, ensayo_id)
        r_final = await _ejecutar(nombre, final, timeout)
        if "error" in r_final:
            salida = con_tiempos(r_final, ms())
            salida["propuesta_norma"] = bloque
            return salida

        resultado = r_final["resultado"] if isinstance(r_final["resultado"], dict) else {}
        despues = resultado.get("despues") or {}
        norma_eje.completar_con_plugin(bloque, despues)
        eje = args.get("nombre")
        if resultado.get("simulado"):
            tabla = norma_eje.tabla_pis_simulada(eje, despues, norma_vial.UNIDADES_METROS)
            verificacion = await anyio.to_thread.run_sync(
                lambda: norma_vial.verificar(preparada, tabla, None, {"simulacion": r_final.get(CLAVE_ID_LOG)}))
            verificacion["prevista"] = True
            verificacion["nota"] = ("Verificación prevista sobre la tabla de PI de la simulación: el eje aún no existe. Al "
                                    "crearlo se verifica de nuevo con listar_pis.")
        else:
            r_pis = await _ejecutar("listar_pis", _args_lectura({"alineamiento": eje}, pedido), TIMEOUT_LECTURA)
            if "error" in r_pis:
                verificacion = {"ok": False, "verificado": False, "error": r_pis["error"] + " (el eje sí se creó: verifica "
                                "después con verificar_norma)", "id_log": r_pis.get(CLAVE_ID_LOG)}
            else:
                verificacion = await anyio.to_thread.run_sync(
                    lambda: norma_vial.verificar(preparada, r_pis["resultado"], None,
                                                 {"listar_pis": r_pis.get(CLAVE_ID_LOG)}))
        salida = con_tiempos(r_final, ms())
        salida["propuesta_norma"] = bloque
        salida["verificacion"] = verificacion
        return salida
    finally:
        base.cerrar()


# ---------------------------------------------------------------------------------------------- registro dinámico
known_tools = set()


def _crear_handler(nombre: str, parametros: list, descripcion: str):
    """Función que el SDK expone como herramienta MCP: un único argumento 'args' con el modelo de la herramienta.
    Las herramientas de AMPLIACIONES_NORMA (1.19.0) reciben además los datos del proyecto de la DG-2018 y 'radio'
    opcional, si el plugin publica los parámetros que hacen falta y ninguno con el mismo nombre."""
    ampliada = nombre in AMPLIACIONES_NORMA and norma_eje.se_puede_ampliar(parametros)
    if nombre in AMPLIACIONES_NORMA and not ampliada:
        print(f"Aviso: no se amplía '{nombre}' con la norma: el plugin no publica "
              f"{', '.join(sorted(norma_eje.PARAMETROS_NECESARIOS))} o ya publica {norma_eje.campos_que_chocan(parametros)}")
    fields = {}
    for p in parametros:
        ptype = type_to_python(p["type"])
        desc = p.get("description", "")
        if ampliada and p["name"] == "radio":
            fields[p["name"]] = (typing.Optional[ptype], Field(None, description=desc + norma_eje.DESCRIPCION_RADIO))
        elif p.get("required", False):
            fields[p["name"]] = (ptype, Field(..., description=desc))
        else:
            fields[p["name"]] = (typing.Optional[ptype], Field(None, description=desc))
    if ampliada:
        for campo, (ptype, desc) in norma_eje.CAMPOS_PROYECTO.items():
            fields[campo] = (typing.Optional[ptype], Field(None, description=desc))
    SchemaModel = create_model(f"{nombre}_Model", **fields)

    async def handler(args: SchemaModel) -> str:  # type: ignore[valid-type]
        datos = args.model_dump(exclude_none=True)
        if ampliada and norma_eje.es_con_norma(datos):
            return _a_texto(await _crear_eje_con_norma(nombre, parametros, datos))
        inicio = time.perf_counter()
        respuesta = await _ejecutar(nombre, datos, _timeout_s(nombre, parametros, datos))
        return _a_texto(con_tiempos(respuesta, int((time.perf_counter() - inicio) * 1000)))

    handler.__name__ = nombre
    handler.__doc__ = (descripcion or "") + (norma_eje.DESCRIPCION if ampliada else "")
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
        if info["name"] in HERRAMIENTAS_LOCALES:
            print(f"Aviso: el plugin publica '{info['name']}', que ya existe como herramienta local del puente; se ignora la del plugin")
            continue
        mcp.add_tool(_crear_handler(info["name"], info.get("parameters", []), info.get("description", "")))
        known_tools.add(info["name"])
        resultado["añadidas"].append(info["name"])
    resultado["estado"] = "ok"
    return resultado


# ---------------------------------------------------------------------------------------------- arranque de Civil 3D
async def ping_plugin(timeout: float = 3.0):
    """GET /ping sin token: {"ok": true, "servidor": "ArbaMcp", "version": "1.3.0"} si el plugin escucha; None si no."""
    try:
        res = await _get_client().get("/ping", timeout=timeout)
        if res.status_code == 200:
            data = res.json()
            if data.get("ok"):
                return data
    except Exception:
        pass
    return None


async def esperar_plugin(intervalo_s: float = 2.0, maximo_s: float = None) -> bool:
    """Sondea /ping hasta que el plugin responde (Civil 3D arrancando). Después olvida el token para releerlo:
    cada arranque de Civil 3D genera uno nuevo. Devuelve False si se agota 'maximo_s' (None = sin límite)."""
    inicio = time.monotonic()
    while True:
        if await ping_plugin() is not None:
            _clear_token()
            return True
        if maximo_s is not None and time.monotonic() - inicio >= maximo_s:
            return False
        await asyncio.sleep(intervalo_s)


async def update_tools_loop():
    while True:
        try:
            r = await sincronizar_herramientas()
            if r["estado"] == "sin_conexion":
                await esperar_plugin()
                continue
        except Exception:
            pass
        await asyncio.sleep(5)


# ---------------------------------------------------------------------------------------------- nombres retirados
# Herramientas retiradas o renombradas: nombre viejo → con qué sustituirlo. En 1.3.0 no se retira ninguna; el
# mecanismo queda listo (como en revit-mcp): si un cliente llama a un nombre de esta tabla, recibe el mensaje en vez
# del "Unknown tool" genérico del SDK.
HERRAMIENTAS_RETIRADAS = {}


def mensaje_retirada(nombre: str) -> str:
    sustituta = HERRAMIENTAS_RETIRADAS.get(nombre)
    if sustituta is None:
        return f"Unknown tool: {nombre}"
    return (f"La herramienta '{nombre}' se retiró en {__version__}. Usa en su lugar: {sustituta}. "
            "Consulta la lista de herramientas del servidor (tools/list).")


def instalar_retiradas(mcp_server):
    """Envuelve MCPServer._tool_manager.call_tool (o MCPServer.call_tool) para que un nombre retirado responda con su
    sustituta como ToolError (resultado is_error, sin traza). No se registran herramientas ocultas: el SDK no
    permite excluir una herramienta de tools/list. Devuelve el nombre del método envuelto o None."""
    try:
        from mcp.server.mcpserver.exceptions import ToolError
    except ImportError:  # pragma: no cover - SDK distinto
        ToolError = Exception

    manager = getattr(mcp_server, "_tool_manager", None)
    objetivo = manager if manager is not None and hasattr(manager, "call_tool") else mcp_server
    original = getattr(objetivo, "call_tool", None)
    if original is None:
        print("Aviso: no se pudo instalar la interceptación de herramientas retiradas")
        return None
    if getattr(original, "_retiradas_instaladas", False):
        return getattr(original, "_retiradas_objetivo", None)

    def _registrada(nombre):
        get_tool = getattr(objetivo, "get_tool", None)
        if get_tool is None:
            return False
        try:
            return get_tool(nombre) is not None
        except Exception:
            return False

    async def call_tool(name, *args, **kwargs):
        if name in HERRAMIENTAS_RETIRADAS and not _registrada(name):
            raise ToolError(mensaje_retirada(name))
        return await original(name, *args, **kwargs)

    etiqueta = "_tool_manager.call_tool" if objetivo is manager else "call_tool"
    call_tool._retiradas_instaladas = True
    call_tool._retiradas_objetivo = etiqueta
    setattr(objetivo, "call_tool", call_tool)
    return etiqueta


RETIRADAS_INSTALADAS_EN = instalar_retiradas(mcp)


# ---------------------------------------------------------------------------------------------- servidor
def construir_app():
    """Aplicación ASGI con streamable-http (/mcp) y SSE (/sse, /messages/), sin arrancar uvicorn."""
    http_app = mcp.streamable_http_app(host="127.0.0.1", stateless_http=True, json_response=True)
    sse_app = mcp.sse_app(host="127.0.0.1")
    for route in sse_app.routes:
        http_app.routes.append(route)
    return http_app


async def run_combined_async():
    print(f"Puente MCP Civil 3D {__version__}: plugin en {base_url()}, agente en http://127.0.0.1:{PUERTO_PUENTE}/mcp")
    asyncio.create_task(update_tools_loop())
    config = uvicorn.Config(construir_app(), host="127.0.0.1", port=PUERTO_PUENTE, log_level="info")
    server = uvicorn.Server(config)
    await server.serve()


if __name__ == "__main__":
    anyio.run(run_combined_async)

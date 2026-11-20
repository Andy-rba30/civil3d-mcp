# -*- coding: utf-8 -*-
"""crear_alineamiento_desde_polilinea con velocidad_diseno (1.19.0): el puente propone el radio mínimo de la DG-2018 en
cada PI, con la base de ejemplo (base_norma_ejemplo.py) y un plugin falso que reproduce el planificador de
ArbaMcp.Nucleo/Alineamiento.cs (Planificar) para la simulación, la creación y listar_pis.
"""
import json
import math

import pytest

import norma as n
import norma_alineamiento as ne
from base_norma_ejemplo import SEGUNDA, TERCERA, crear_base
from conftest import ejecutar

HERRAMIENTA = "crear_alineamiento_desde_polilinea"
PARAMETROS = [
    {"name": "nombre", "type": "string", "description": "Nombre del alineamiento nuevo", "required": True},
    {"name": "handle", "type": "string", "description": "Handle de la polilínea"},
    {"name": "capa", "type": "string", "description": "Capa con una sola polilínea"},
    {"name": "radio", "type": "number", "description": "Radio por defecto (> 0)", "required": True},
    {"name": "radios_por_pi", "type": "json", "description": "Radios por PI"},
    {"name": "estilo", "type": "string", "description": "Estilo"},
    {"name": "simular", "type": "boolean", "description": "Simular"},
    {"name": "pedido", "type": "string", "description": "Pedido original del usuario"},
]
TERCERA_60 = {"velocidad_diseno": 60, "clasificacion": TERCERA, "orografia": "Ondulado", "ubicacion": "rural",
              "pavimentada": False, "peralte_maximo": 8}


def _direccion(grados, largo, desde):
    return (round(desde[0] + largo * math.cos(math.radians(grados)), 6),
            round(desde[1] + largo * math.sin(math.radians(grados)), 6))


CAPAS = {
    # VALIDACION_119, polilínea A: tramos 500, 700 y 600 m; giros de 90° a la izquierda y a la derecha
    "EJE_NORMA_A": [(1000, 2000), (1500, 2000), (1500, 2700), (2100, 2700)],
    # VALIDACION_119, polilínea B: tramos de 300 m (la de la 1.18.0)
    "EJE_NORMA_B": [(1000, 3500), (1300, 3500), (1300, 3800), (1600, 3800)],
    # un PI de 1,2° (sin curva a 60 km/h) y otro de 4° (L > 30 (10 − Δ))
    "DEFLEXIONES": [(0, 0), (1000, 0), _direccion(1.2, 1000, (1000, 0)),
                    _direccion(5.2, 1000, _direccion(1.2, 1000, (1000, 0)))],
    # sin PI intermedios
    "RECTA": [(0, 0), (500, 0)],
}


def _por_rumbos(tramos, giros_izquierda):
    """Vértices desde (0, 0) hacia el este con esos tramos y giros a la izquierda (grados) en cada PI."""
    vertices, rumbo = [(0.0, 0.0)], 0.0
    for i, largo in enumerate(tramos):
        vertices.append(_direccion(rumbo, largo, vertices[-1]))
        if i < len(giros_izquierda):
            rumbo += giros_izquierda[i]
    return vertices


# Auditoría de la ronda C. I1: giros de 40°, 1° y 40° a la izquierda con tramos 600, 160, 160 y 600.
CAPAS["SALTO"] = _por_rumbos([600, 160, 160, 600], [40, 1, 40])
# I2: un PI de 20° (autopista a 100 km/h). M1: dos curvas con 2000 m de recta entre ellas, y una sola curva.
CAPAS["AUTOPISTA_1"] = _por_rumbos([1000, 2500], [20])
CAPAS["AUTOPISTA_2"] = _por_rumbos([1000, 2000, 3000], [20, 20])
# Tarea 6.9. b) un PI de 0,5° a 100 km/h. f) un PI de 120° que ocupa la recta y otro de 20° detrás (reparto).
CAPAS["AUTOPISTA_05"] = _por_rumbos([1000, 2500], [0.5])
CAPAS["REPARTO"] = _por_rumbos([600, 300, 600], [120, -20])
SEGUNDA_60 = {"velocidad_diseno": 60, "clasificacion": SEGUNDA, "orografia": "Ondulado", "ubicacion": "rural"}
AUTOPISTA_100 = {"velocidad_diseno": 100, "clasificacion": "Autopista de primera clase", "orografia": "Plano",
                 "ubicacion": "rural"}


# ---------------------------------------------------------------------------------------------- plugin falso
def planificar(vertices, radios):
    """Alineamiento.Planificar (Alineamiento.cs) en Python, con los mismos redondeos de salida (4 decimales)."""
    tramos = [math.dist(a, b) for a, b in zip(vertices, vertices[1:])]
    pis, t_anterior = [], 0.0
    for k in range(1, len(vertices) - 1):
        a, b, c = vertices[k - 1], vertices[k], vertices[k + 1]
        ux, uy, vx, vy = b[0] - a[0], b[1] - a[1], c[0] - b[0], c[1] - b[1]
        cos = max(-1.0, min(1.0, (ux * vx + uy * vy) / (math.hypot(ux, uy) * math.hypot(vx, vy))))
        delta = math.acos(cos)
        cruz = ux * vy - uy * vx
        giro = "ninguno" if delta < 1e-6 else ("derecha" if cruz < 0 else "izquierda")
        pi = {"numero": k, "x": b[0], "y": b[1], "deflexion_grados": math.degrees(delta), "giro": giro,
              "radio_pedido": radios[k - 1], "radio_aplicado": None, "tangente": None, "longitud_curva": None,
              "progresiva_pc": None, "progresiva_pt": None, "radio_maximo": None, "aviso": None}
        libre_antes, libre_despues = tramos[k - 1] - t_anterior, tramos[k]
        r = radios[k - 1]
        if r > 0 and giro != "ninguno" and math.pi - delta >= 1e-6:
            tan = math.tan(delta / 2)
            pi["radio_maximo"] = math.floor(min(libre_antes, libre_despues) / tan * 1000) / 1000
            t = r * tan
            if t <= libre_antes + 1e-6 and t <= libre_despues + 1e-6:
                pi.update(radio_aplicado=r, tangente=t, longitud_curva=r * delta)
            else:
                pi["aviso"] = f"PI {k}: el radio {r} no cabe; se deja sin curva."
        pis.append(pi)
        t_anterior = pi["tangente"] or 0.0
    s, consumida = 0.0, 0.0
    for k, pi in enumerate(pis, start=1):
        recta = tramos[k - 1] - consumida
        if pi["tangente"] is not None:
            pi["progresiva_pc"] = s + recta - pi["tangente"]
            pi["progresiva"] = pi["progresiva_pc"] + pi["tangente"]
            pi["progresiva_pt"] = pi["progresiva_pc"] + pi["longitud_curva"]
            s, consumida = pi["progresiva_pt"], pi["tangente"]
        else:
            pi["progresiva"] = s + recta
            s, consumida = pi["progresiva"], 0.0
    final = s + tramos[-1] - consumida
    for pi in pis:
        for clave, valor in list(pi.items()):
            if isinstance(valor, float):
                pi[clave] = round(valor, 4)
    return {"pis": pis, "progresiva_final": round(final, 4), "longitud": round(final, 4),
            "avisos": [p["aviso"] for p in pis if p["aviso"]]}


def radios_plugin(args, numero_pi):
    """Alineamiento.RadiosPorPi: un radio por PI desde 'radio' y 'radios_por_pi'."""
    radios = [float(args["radio"])] * numero_pi
    texto = args.get("radios_por_pi")
    if texto:
        for posicion, e in enumerate(json.loads(texto), start=1):
            if isinstance(e, dict):
                radios[int(e["pi"]) - 1] = float(e.get("radio") or 0)
            else:
                radios[posicion - 1] = float(e or 0)
    return radios


class PluginFalso:
    def __init__(self, servidor):
        self.servidor = servidor
        self.insunits = "6"
        self.creados = {}
        servidor.herramienta(HERRAMIENTA, PARAMETROS, "Crea un alineamiento desde una polilínea.")
        servidor.respuestas[HERRAMIENTA] = self.crear
        servidor.respuestas["leer_variable"] = lambda c: {
            "ok": True, "id_log": "id-insunits", "result": {"variable": "INSUNITS", "valor": self.insunits, "tipo": "Int16"}}
        servidor.respuestas["listar_pis"] = self.listar_pis

    def crear(self, cuerpo):
        a = cuerpo["args"]
        if a.get("nombre") == "Existe":
            return {"ok": False, "error": "Ya existe el alineamiento 'Existe'. Usa listar_alineamientos.", "id_log": "id-existe"}
        if not a.get("radio"):
            return {"ok": False, "error": "Falta el parámetro obligatorio 'radio' (radio por defecto de las curvas, > 0).",
                    "id_log": "id-sin-radio"}
        vertices = CAPAS[a["capa"]]
        plan = planificar(vertices, radios_plugin(a, len(vertices) - 2))
        despues = {"existe": True, "longitud": plan["longitud"], "progresiva_inicial": 0.0,
                   "progresiva_final": plan["progresiva_final"], "numero_pi": len(plan["pis"]),
                   "numero_curvas": sum(1 for p in plan["pis"] if p["radio_aplicado"] is not None),
                   "estilo": "Basic", "capa": "C-ROAD", "pis": plan["pis"]}
        simulado = bool(a.get("simular"))
        indice = len(self.servidor.llamadas) - 1
        resultado = {"simulado": simulado, "herramienta": HERRAMIENTA, "cambios": ["existe", "pis"],
                     "antes": {"existe": False}, "despues": despues, "avisos": plan["avisos"] or None}
        if not simulado:
            self.creados[a["nombre"]] = plan
            resultado["copia"] = {"ruta": "C:\\Proyectos\\prueba\\backups\\prueba.dwg"}
        return {"ok": True, "tool": HERRAMIENTA, "id_log": f"id-crear-{indice}", "ms": 7, "result": resultado}

    def listar_pis(self, cuerpo):
        nombre = cuerpo["args"]["alineamiento"]
        plan = self.creados.get(nombre)
        if plan is None:
            return {"ok": False, "error": f"No existe el alineamiento '{nombre}'. Usa listar_alineamientos.", "id_log": "id-no"}
        tabla = ne.tabla_pis_simulada(nombre, {"pis": plan["pis"], "progresiva_inicial": 0.0,
                                               "progresiva_final": plan["progresiva_final"],
                                               "longitud": plan["longitud"]}, "Meters")
        return {"ok": True, "tool": "listar_pis", "id_log": "id-listar-pis", "result": tabla}


@pytest.fixture
def memoria(tmp_path, monkeypatch):
    carpeta = tmp_path / "memoria-firma"
    crear_base(str(carpeta))
    monkeypatch.setenv("MEMORIA_FIRMA_DIR", str(carpeta))
    return carpeta


@pytest.fixture
def plugin(servidor, puente, memoria):
    p = PluginFalso(servidor)
    ejecutar(puente.sincronizar_herramientas())
    return p


def crear(puente, **args):
    texto = ejecutar(puente.mcp.call_tool(HERRAMIENTA, {"args": args})).content[0].text
    return json.loads(texto)


def herramientas_llamadas(servidor):
    return [c["tool"] for c in servidor.llamadas]


def pi(respuesta, numero):
    return next(p for p in respuesta["propuesta_norma"]["pis"] if p["numero"] == numero)


def filas(verificacion, elemento, comprobacion):
    return [f for f in verificacion["filas"] if f["elemento"] == elemento and f["comprobacion"].startswith(comprobacion)]


# ---------------------------------------------------------------------------------------------- sin la norma
def test_sin_velocidad_todo_igual_que_antes(servidor, puente, plugin):
    args = {"nombre": "Eje 1", "capa": "EJE_NORMA_A", "radio": 80, "simular": True, "pedido": "crea el eje"}
    r = crear(puente, **args)
    assert servidor.llamadas == [{"tool": HERRAMIENTA, "args": args, "timeout_s": 120}]
    assert r["simulado"] is True and "propuesta_norma" not in r and "verificacion" not in r
    assert r["id_log"] == "id-crear-0" and r["ms_puente"] >= 0


def test_sin_velocidad_y_sin_radio_responde_el_plugin(servidor, puente, plugin):
    r = crear(puente, nombre="Eje 1", capa="EJE_NORMA_A", simular=True)
    assert r["ok"] is False and "Falta el parámetro obligatorio 'radio'" in r["error"]
    assert herramientas_llamadas(servidor) == [HERRAMIENTA]


def test_esquema_ampliado_y_se_conserva_al_volver_a_sincronizar(servidor, puente, plugin):
    def esquema():
        herramienta = puente.mcp._tool_manager.get_tool(HERRAMIENTA)
        return herramienta, herramienta.parameters["$defs"][f"{HERRAMIENTA}_Model"]

    herramienta, modelo = esquema()
    assert set(ne.CAMPOS_PROYECTO) <= set(modelo["properties"])
    assert modelo["required"] == ["nombre"], "radio deja de ser obligatorio"
    assert "velocidad_diseno" in herramienta.description and "DG-2018" in herramienta.description
    assert "Con 'velocidad_diseno' no hace falta" in modelo["properties"]["radio"]["description"]
    # el plugin deja de publicarla y vuelve: la ampliación sigue
    tools = list(servidor.tools)
    servidor.tools = [t for t in tools if t["name"] != HERRAMIENTA]
    assert ejecutar(puente.sincronizar_herramientas())["quitadas"] == [HERRAMIENTA]
    servidor.tools = tools
    assert ejecutar(puente.sincronizar_herramientas())["añadidas"] == [HERRAMIENTA]
    _, modelo = esquema()
    assert "velocidad_diseno" in modelo["properties"] and modelo["required"] == ["nombre"]


def test_no_se_amplia_si_el_plugin_cambia_sus_parametros(servidor, puente):
    servidor.herramienta(HERRAMIENTA, [p for p in PARAMETROS if p["name"] != "radios_por_pi"])
    ejecutar(puente.sincronizar_herramientas())
    modelo = puente.mcp._tool_manager.get_tool(HERRAMIENTA).parameters["$defs"][f"{HERRAMIENTA}_Model"]
    assert "velocidad_diseno" not in modelo["properties"] and "radio" in modelo["required"]
    assert ne.campos_que_chocan([{"name": "velocidad_diseno"}]) == ["velocidad_diseno"]
    assert not ne.se_puede_ampliar(PARAMETROS + [{"name": "velocidad_diseno"}])


# ---------------------------------------------------------------------------------------------- propuesta
def test_tercera_clase_propone_el_radio_sin_transicion_y_verifica_la_simulacion(servidor, puente, plugin):
    r = crear(puente, nombre="Eje Norma A", capa="EJE_NORMA_A", simular=True, pedido="crea el eje con la norma",
              **TERCERA_60)
    assert herramientas_llamadas(servidor) == ["leer_variable", HERRAMIENTA, HERRAMIENTA]
    ensayo, final = servidor.llamadas[1]["args"], servidor.llamadas[2]["args"]
    assert ensayo["simular"] is True and ensayo["radio"] == ne.RADIO_PROVISIONAL and "velocidad_diseno" not in ensayo
    assert final["radios_por_pi"] == "[215, 215]" and final["radio"] == 215 and final["simular"] is True
    assert final["pedido"] == "crea el eje con la norma" and servidor.llamadas[0]["args"]["pedido"] == final["pedido"]
    assert not set(ne.CAMPOS_PROYECTO) & set(final), "los datos del proyecto no llegan al plugin"
    propuesta = r["propuesta_norma"]
    assert propuesta["radios_por_pi"] == [215, 215] and propuesta["velocidad_diseno"] == 60
    assert propuesta["unidades"] == {"INSUNITS": "6", "id_log": "id-insunits"}
    assert propuesta["simulacion_de_ensayo"]["id_log"] == "id-crear-1"
    assert propuesta["tramos"] == [500, 700, 600]
    assert propuesta["tangentes_minimas"][0]["valor"] == 83 and propuesta["tangentes_minimas"][0]["sentido"] == "sentido contrario"
    p1 = pi(r, 1)
    assert p1["origen"] == "norma" and p1["radio_minimo_norma"] == 215 and p1["radio_pedido"] == 215 and p1["cumple_minimo"]
    assert p1["manda"]["comprobacion"] == "Radio para prescindir de la curva de transición"
    assert p1["manda"]["referencia"].startswith("T302.11B (302.05.07") and "PDF 147" in p1["manda"]["referencia"]
    assert "redondeado hacia arriba a un múltiplo de 5 m (desigualdad estricta)" in p1["manda"]["explicacion"]
    minimos = {m["comprobacion"]: m for m in p1["minimos"]}
    assert minimos["Radio mínimo"]["valor_exigido"] == 125 and minimos["Radio mínimo"]["referencia"]["id"] == "T302.02"
    assert minimos["Radio para prescindir de la curva de transición"]["valor_exigido"] == 210
    assert p1["radio_maximo_que_cabe"] == 500 and p1["aviso"] is None and p1["nota_3v"] is None
    assert p1["requiere_confirmacion"] is False and p1["eliminar_pi"] is False and p1["reparto"] is None
    # g) la alternativa de 302.05.06: 125 m con clotoide de Le entre 0,0178·V³/R y el menor de √(24R) y 1,5·Le mín
    alt = p1["alternativa_con_clotoide"]
    assert alt["radio"] == 125 and alt["le_minimo"] == 30.758 and alt["le_maximo_24r"] == 54.772
    assert alt["le_maximo_1_5_le_minimo"] == 46.138 and alt["le_maximo"] == 46.138, "manda el más exigente"
    assert "de 30.758 a 46.138 m" in alt["texto"] and "velocidad específica" in alt["nota"]
    assert [(c["seccion"], c["pagina_impresa"], c["pagina_pdf"]) for c in alt["referencias"]] == [
        ("302.05.06", "145", "146"), ("302.05.06", "146", "147")]
    assert pi(r, 2)["radio_maximo_que_cabe"] == 402
    # la respuesta del plugin va tal cual y la verificación es la prevista
    assert r["simulado"] is True and r["id_log"] == "id-crear-2" and r["despues"]["numero_curvas"] == 2
    v = r["verificacion"]
    assert v["ok"] and v["verificado"] and v["prevista"] is True and "aún no existe" in v["nota"]
    assert v["resumen"]["No cumple"] == 0 and v["lecturas"] == {"simulacion": "id-crear-2"}
    assert filas(v, "Curva PI 1", "Radio para prescindir")[0]["resultado"] == "Cumple"
    assert filas(v, "Tangente PI 1–PI 2", "Tangente mínima")[0]["valor_diseno"] == 270
    assert v["avisos"].count(n.AVISO_TRANSICION_PERALTE) == 1, "a) el aviso de 302.05.06, una sola vez"


def test_escritura_real_adjunta_la_verificacion_con_listar_pis(servidor, puente, plugin):
    r = crear(puente, nombre="Eje Norma A", capa="EJE_NORMA_A", pedido="crea el eje", **TERCERA_60)
    assert herramientas_llamadas(servidor) == ["leer_variable", HERRAMIENTA, HERRAMIENTA, "listar_pis"]
    assert servidor.llamadas[1]["args"]["simular"] is True and "simular" not in servidor.llamadas[2]["args"]
    assert servidor.llamadas[3]["args"] == {"alineamiento": "Eje Norma A", "pedido": "crea el eje"}
    assert r["simulado"] is False and r["copia"]["ruta"].endswith(".dwg")
    v = r["verificacion"]
    assert v["verificado"] and "prevista" not in v and v["lecturas"] == {"listar_pis": "id-listar-pis"}
    assert v["resumen"]["No cumple"] == 0
    assert [f["valor_diseno"] for f in v["filas"] if f["comprobacion"] == "Radio mínimo"] == [215, 215]


def test_pi_sin_curva_por_deflexion_y_longitud_con_deflexion_pequena(servidor, puente, plugin):
    r = crear(puente, nombre="Eje D", capa="DEFLEXIONES", simular=True, **TERCERA_60)
    p1, p2 = pi(r, 1), pi(r, 2)
    assert p1["origen"] == "sin_curva" and p1["radio_pedido"] == 0 and p1["cumple_minimo"] is True
    assert "cuadro de deflexión sin curva" in p1["nota"] and "Se propone 0" in p1["nota"]
    assert p2["deflexion_grados"] == 4 and p2["radio_minimo_norma"] == 2580 and p2["radio_pedido"] == 2580
    assert p2["manda"]["comprobacion"].startswith("Longitud de curva con deflexión pequeña")
    assert "2578.31 m redondeado hacia arriba a un múltiplo de 5 m (desigualdad estricta)" in p2["manda"]["explicacion"]
    pequena = next(m for m in p2["minimos"] if m["comprobacion"].startswith("Longitud"))
    assert pequena["valor_exigido"] == 180 and pequena["referencia"]["id"] == "R302.02-1"
    assert pequena["formula"]["id"] == "F37"
    assert json.loads(servidor.llamadas[2]["args"]["radios_por_pi"]) == [0, 2580]
    v = r["verificacion"]
    assert filas(v, "PI 1 (sin curva)", "Deflexión máxima")[0]["resultado"] == "Cumple"
    assert filas(v, "Curva PI 2", "Longitud de curva con deflexión")[0]["resultado"] == "Cumple"
    assert v["resumen"]["No cumple"] == 0


def test_transicion_a_es_estricta_como_la_b_y_segunda_clase(servidor, puente, plugin):
    """a) Decisión del dueño (17/11/2026): la T302.11A también pide un radio superior (igual no basta): 250 → 255.
    Fuera de Tercera Clase no hay alternativa con clotoide de 302.05.06 (sus fórmulas son de Tercera Clase)."""
    r = crear(puente, nombre="Eje S", capa="EJE_NORMA_A", simular=True, velocidad_diseno=60, clasificacion=SEGUNDA,
              orografia="Ondulado", ubicacion="rural")
    p1 = pi(r, 1)
    assert p1["radio_pedido"] == 255 and p1["manda"]["referencia"].startswith("T302.11A")
    assert "250 m redondeado hacia arriba a un múltiplo de 5 m (desigualdad estricta)" in p1["manda"]["explicacion"]
    transicion = next(m for m in p1["minimos"] if m["referencia"]["id"] == "T302.11A")
    assert transicion["criterio"] == n.MAYOR_ESTRICTO and "igual no basta" in transicion["nota"]
    assert p1["alternativa_con_clotoide"] is None
    assert filas(r["verificacion"], "Curva PI 1", "Radio para prescindir")[0]["resultado"] == "Cumple"


def test_redondeo_hacia_arriba_y_hacia_abajo():
    assert ne.arriba_5(123.2) == 125 and ne.arriba_5(125) == 125 and ne.arriba_5(125.0000000001) == 125
    assert ne.abajo_5(217) == 215 and ne.abajo_5(4.9) == 0 and ne.abajo_5(-3) == 0
    assert ne.abajo_5(84.99999999999) == 85


def test_minimo_que_no_cabe_avisa_y_no_lo_fuerza(servidor, puente, plugin):
    r = crear(puente, nombre="Eje Norma B", capa="EJE_NORMA_B", simular=True, **TERCERA_60)
    p1, p2 = pi(r, 1), pi(r, 2)
    assert p1["radio_pedido"] == 215 and p1["cumple_minimo"] and p1["radio_maximo_que_cabe"] == 217
    assert p2["radio_minimo_norma"] == 215 and p2["radio_maximo_que_cabe"] == 2 and p2["cumple_minimo"] is False
    assert p2["radio_pedido"] == 80, "85 cabe justo: con 1 mm de margen se baja a 80"
    assert "La polilínea no permite cumplir la norma en el PI 2" in p2["aviso"]
    assert "mueve los vértices o baja la velocidad" in p2["aviso"] and "sin dejar la tangente mínima" in p2["aviso"]
    assert p2["aviso"] in r["propuesta_norma"]["avisos"]
    # f) decisiones del dueño (17/11/2026)
    assert "baja la velocidad de diseño del tramo homogéneo (T204.01" in p2["aviso"]
    assert "dos incumplimientos a la vez, el radio bajo el mínimo" in p2["aviso"] and p2["nivel"] == 2
    assert [m["referencia"].split(" ")[0] for m in p2["incumple"]] == ["T302.02", "T302.11B"]
    assert "V = √(127·R·(pmáx + fmáx)) = 48.3 km/h" in p2["aviso"] and p2["velocidad_que_admite"]["formula"]["valor_kmh"] == 48.34
    assert p2["velocidad_que_admite"]["formula"]["formula"]["id"] == "F08"
    assert p2["velocidad_que_admite"]["tabla_t20401"] == {"velocidad_kmh": None, "sin_filas": [40]}
    assert "Sección 301, p. 124; PDF 125" in p2["aviso"] and p2["seccion_301"]["pagina_impresa"] == "124"
    assert p2["requiere_confirmacion"] is True and p1["requiere_confirmacion"] is False
    assert json.loads(servidor.llamadas[2]["args"]["radios_por_pi"]) == [215, 80]
    assert r["despues"]["numero_curvas"] == 2, "el plugin no deja ningún PI sin curva por sorpresa"
    assert p2["progresiva"] == r["despues"]["pis"][1]["progresiva"], "la progresiva del eje final, no la del ensayo"
    v = r["verificacion"]
    assert filas(v, "Curva PI 2", "Radio mínimo")[0]["resultado"] == "No cumple"
    assert filas(v, "Tangente PI 1–PI 2", "Tangente mínima")[0]["resultado"] == "No cumple"


def test_tres_niveles_cuando_no_cabe():
    tan = math.tan(math.radians(90) / 2)
    assert ne._radio_que_cabe(tan, 102.0, 185.0) == (100.0, 1)
    assert ne._radio_que_cabe(tan, 2.0, 85.0) == (80.0, 2)
    assert ne._radio_que_cabe(tan, -81.0, 4.0) == (0.0, 3)
    # f) el nivel 2 no pasa del mínimo: un radio mayor solo acortaría más la tangente
    tan20 = math.tan(math.radians(20) / 2)
    assert ne._radio_que_cabe(tan20, 0.5, 83.5) == (470.0, 2)
    assert ne._radio_que_cabe(tan20, 0.5, 83.5, tope=215) == (215, 2)


def test_los_radios_del_usuario_mandan_y_se_avisa(servidor, puente, plugin):
    r = crear(puente, nombre="Eje U", capa="EJE_NORMA_A", simular=True, radios_por_pi='[{"pi": 2, "radio": 150}]',
              **TERCERA_60)
    ensayo = servidor.llamadas[1]["args"]
    assert ensayo["radios_por_pi"] == '[{"pi": 2, "radio": 150}]', "el plugin valida los del usuario en el ensayo"
    assert json.loads(servidor.llamadas[2]["args"]["radios_por_pi"]) == [215, 150]
    p2 = pi(r, 2)
    assert p2["origen"] == "usuario" and p2["radio_pedido"] == 150 and p2["cumple_minimo"] is False
    assert "por debajo del mínimo de la norma (215 m, T302.11B" in p2["aviso"]
    assert filas(r["verificacion"], "Curva PI 2", "Radio para prescindir")[0]["resultado"] == "No cumple"
    servidor.llamadas.clear()
    r = crear(puente, nombre="Eje U", capa="EJE_NORMA_A", simular=True, radio=300, **TERCERA_60)
    assert servidor.llamadas[1]["args"]["radio"] == 300
    assert json.loads(servidor.llamadas[2]["args"]["radios_por_pi"]) == [300, 300]
    assert all(p["origen"] == "usuario" and p["cumple_minimo"] and p["aviso"] is None for p in r["propuesta_norma"]["pis"])
    r = crear(puente, nombre="Eje U", capa="EJE_NORMA_A", simular=True, radios_por_pi="[0, 900]", **TERCERA_60)
    assert "pediste el PI sin curva" in pi(r, 1)["aviso"] and "no cabe" in pi(r, 2)["aviso"]


def test_radios_del_usuario_lectura():
    assert ne.radios_del_usuario(None, None, 3) == {}
    assert ne.radios_del_usuario(100, "[80, null, \"120,5\"]", 3) == {1: 80, 2: 0, 3: 120.5}
    assert ne.radios_del_usuario(None, '[{"pi": 3, "radio": 0}]', 3) == {3: 0}
    for malo in ("[1, 2, 3, 4]", "{\"pi\": 1}", "no", '[{"radio": 4}]', "[-5]"):
        with pytest.raises(n.ErrorNorma):
            ne.radios_del_usuario(None, malo, 3)


def test_polilinea_sin_pi(servidor, puente, plugin):
    r = crear(puente, nombre="Recta", capa="RECTA", simular=True, **TERCERA_60)
    assert r["propuesta_norma"]["pis"] == [] and r["propuesta_norma"]["tramos"] == [500]
    assert servidor.llamadas[2]["args"]["radios_por_pi"] == "[]" and r["verificacion"]["verificado"]


# ---------------------------------------------------------------------------------------------- sin llamar al plugin
def test_faltan_datos_no_llama_al_plugin(servidor, puente, plugin):
    r = crear(puente, nombre="Eje 1", capa="EJE_NORMA_A", simular=True, velocidad_diseno=60, clasificacion=TERCERA,
              orografia="Ondulado", ubicacion="rural")
    assert r["ok"] is False and {f["dato"] for f in r["faltan"]} == {"pavimentada", "peralte_maximo"}
    assert "No se llamó a Civil 3D" in r["error"] and "no los supongas" in r["error"]
    assert servidor.llamadas == []
    r = crear(puente, nombre="Eje 1", capa="EJE_NORMA_A", radio=80, clasificacion=TERCERA)
    assert "velocidad_diseno" in {f["dato"] for f in r["faltan"]}, "cualquier dato del proyecto activa la norma"
    assert servidor.llamadas == []


def test_sin_minimo_en_la_base_pide_radio(servidor, puente, plugin):
    datos = {**TERCERA_60, "velocidad_diseno": 70}
    r = crear(puente, nombre="Eje 70", capa="EJE_NORMA_A", simular=True, **datos)
    assert r["ok"] is False and "Pasa 'radio'" in r["error"] and servidor.llamadas == []
    r = crear(puente, nombre="Eje 70", capa="EJE_NORMA_A", simular=True, radio=300, **datos)
    assert json.loads(servidor.llamadas[-1]["args"]["radios_por_pi"]) == [300, 300]


def test_dibujo_que_no_esta_en_metros(servidor, puente, plugin):
    plugin.insunits = "2"
    r = crear(puente, nombre="Eje 1", capa="EJE_NORMA_A", simular=True, **TERCERA_60)
    assert r["ok"] is False and "INSUNITS = 2, pies" in r["error"] and r["unidades"]["INSUNITS"] == "2"
    assert herramientas_llamadas(servidor) == ["leer_variable"]
    assert ne.unidades_no_metros({"valor": None}).startswith("El dibujo no está en metros (INSUNITS = vacío")
    assert ne.unidades_no_metros({"valor": 6}) is None


def test_error_del_plugin_en_el_ensayo(servidor, puente, plugin):
    r = crear(puente, nombre="Existe", capa="EJE_NORMA_A", simular=True, **TERCERA_60)
    assert r["ok"] is False and "Ya existe el alineamiento 'Existe'" in r["error"] and r["id_log"] == "id-existe"
    assert herramientas_llamadas(servidor) == ["leer_variable", HERRAMIENTA]


def test_errores_de_la_memoria(servidor, puente, plugin, memoria, monkeypatch):
    monkeypatch.setenv("MEMORIA_FIRMA_DIR", str(memoria / "no-existe"))
    r = crear(puente, nombre="Eje 1", capa="EJE_NORMA_A", simular=True, **TERCERA_60)
    assert r["ok"] is False and "no existe" in r["error"] and servidor.llamadas == []


def test_listar_pis_falla_despues_de_crear(servidor, puente, plugin):
    servidor.respuestas["listar_pis"] = {"ok": False, "error": "Civil 3D ocupado", "id_log": "id-fallo"}
    r = crear(puente, nombre="Eje Norma A", capa="EJE_NORMA_A", **TERCERA_60)
    assert r["simulado"] is False and r["verificacion"]["ok"] is False
    assert "el eje sí se creó" in r["verificacion"]["error"] and r["verificacion"]["id_log"] == "id-fallo"


# ---------------------------------------------------------------------------------------------- funciones puras
def test_geometria_desde_la_simulacion():
    plan = planificar(CAPAS["EJE_NORMA_B"], [215, 80])
    geo = ne.geometria({"pis": plan["pis"], "progresiva_inicial": 0.0, "progresiva_final": plan["progresiva_final"]})
    assert [round(t, 3) for t in geo["tramos"]] == [300, 300, 300]
    plan = planificar(CAPAS["EJE_NORMA_B"], [1, 0])
    geo = ne.geometria({"pis": plan["pis"], "progresiva_final": plan["progresiva_final"]})
    assert [round(t, 3) for t in geo["tramos"]] == [300, 300, 300], "con el último PI sin curva"
    with pytest.raises(n.ErrorNorma):
        ne.geometria({"pis": []})


def test_tangente_minima_mismo_sentido_aplica_la_mas_exigente(memoria):
    base = n.BaseNorma(n.localizar_base(str(memoria)))
    try:
        ctx, _ = n.resolver_proyecto(base, "alineamiento", dict(TERCERA_60))
        o = ne.tangente_minima(base, ctx, True)
        assert o["valor"] == 167 and o["referencia"].startswith("T302.01") and o["otra_tabla"]["valor"] == 85
        assert ne.tangente_minima(base, ctx, False)["valor"] == 83
        ctx80 = dict(ctx, velocidad=70)
        sin_fila = ne.tangente_minima(base, ctx80, False)
        assert sin_fila["valor"] == round(1.39 * 70, 3) and "fórmula" in sin_fila["nota"]
    finally:
        base.cerrar()


# ---------------------------------------------------------------------------------------------- auditoría de la ronda C
def test_tangente_minima_con_un_pi_sin_curva_en_medio(servidor, puente, plugin):
    """I1: la propuesta busca la curva anterior real (PI 1) saltando el PI 2 sin curva y deja la tangente mínima del
    mismo sentido (167 m, T302.01 L mín.o) en la recta de PT 1 a PC 3."""
    r = crear(puente, nombre="Eje Salto", capa="SALTO", simular=True, **SEGUNDA_60)
    p1, p2, p3 = pi(r, 1), pi(r, 2), pi(r, 3)
    assert p2["radio_pedido"] == 0 and p2["origen"] == "sin_curva"
    assert p1["radio_pedido"] == 255 and p1["tangente_minima_despues"]["valor"] == 167, "T302.11A estricta (6.9)"
    assert p1["tangente_minima_despues"]["pi_sin_curva_en_medio"] == [2]
    assert p3["radio_pedido"] == 165 and p3["cumple_minimo"] is False and p3["radio_maximo_que_cabe"] == 165.364
    assert "Ese radio cumple el radio mínimo" in p3["aviso"] and "60 km/h ya es la menor velocidad" in p3["aviso"]
    assert "La polilínea no permite cumplir la norma en el PI 3" in p3["aviso"]
    assert p3["tangente_minima_antes"]["entre"] == "PI 1 y PI 3" and "Hay PI sin curva en medio (PI 2)" in p3["tangente_minima_antes"]["nota"]
    minima = r["propuesta_norma"]["tangentes_minimas"]
    assert minima[0]["tramo"] == "PI 1–PI 3" and minima[0]["sentido"] == "mismo sentido" and minima[0]["valor"] == 167
    salto = filas(r["verificacion"], "Tangente PI 1–PI 3", "Tangente mínima entre curvas del mismo sentido")[0]
    assert salto["resultado"] != "No cumple" and salto["valor_diseno"] >= 167
    # con los radios de antes (250, 0, 250) la recta es de unos 138 m y la verificación ya lo marca
    r = crear(puente, nombre="Eje Salto", capa="SALTO", simular=True, radios_por_pi="[255, 0, 255]", **SEGUNDA_60)
    assert "no deja la tangente mínima" in pi(r, 3)["aviso"]
    r = crear(puente, nombre="Eje Salto", capa="SALTO", simular=True, radios_por_pi="[250, 0, 250]", **SEGUNDA_60)
    assert "queda por debajo del mínimo de la norma (255 m, T302.11A" in pi(r, 3)["aviso"], "T302.11A estricta (6.9)"
    salto = filas(r["verificacion"], "Tangente PI 1–PI 3", "Tangente mínima entre curvas del mismo sentido")[0]
    assert salto["resultado"] == "No cumple" and round(salto["valor_diseno"]) == 138 and salto["valor_exigido"] == 167
    assert "Hay un PI sin curva en medio (PI 2)" in salto["detalle"]["nota"]
    otras = [f["comprobacion"] for f in r["verificacion"]["filas"] if f["elemento"] == "Tangente PI 1–PI 3"]
    assert any("500 m" in c for c in otras) and any("400 m" in c for c in otras), "avisos de 500 y 400 m"
    relaciones = [f["elemento"] for f in r["verificacion"]["filas"] if f["comprobacion"] == "Relación entre radios consecutivos"]
    assert relaciones == ["PI 1 → PI 3", "PI 3 → PI 1"], "recta de 138 m (≤ 200): relación de radios"


def test_pi_sin_curva_por_encima_de_80_kmh(servidor, puente, plugin):
    """I2: a 100 km/h el cuadro de deflexión sin curva no tiene fila: la propuesta pone curva y, si el usuario la
    quita, la verificación dice «No cumple» (lo mismo que avisa la propuesta)."""
    r = crear(puente, nombre="Eje Autopista", capa="AUTOPISTA_1", simular=True, **AUTOPISTA_100)
    assert pi(r, 1)["radio_pedido"] == 905 and pi(r, 1)["origen"] == "norma", "T302.11A estricta (6.9): 900 → 905"
    r = crear(puente, nombre="Eje Autopista", capa="AUTOPISTA_1", simular=True, radios_por_pi="[0]", **AUTOPISTA_100)
    assert "pediste el PI sin curva y la norma pide curva" in pi(r, 1)["aviso"]
    fila = filas(r["verificacion"], "PI 1 (sin curva)", "Deflexión máxima sin curva")[0]
    assert fila["resultado"] == "No cumple" and "solo permite omitir la curva" in fila["detalle"]["nota"]


def test_radio_tras_recta_larga_solo_en_la_curva_de_salida(servidor, puente, plugin):
    """M1: R302.04.05-3 solo entre dos curvas y solo para la curva de salida; nada en las rectas inicial y final."""
    regla = "Radio tras una recta de más de 400 m (autopista)"
    r = crear(puente, nombre="Eje A2", capa="AUTOPISTA_2", simular=True, radios_por_pi="[900, 600]", **AUTOPISTA_100)
    salida = [f for f in r["verificacion"]["filas"] if f["comprobacion"] == regla]
    assert [(f["elemento"], f["resultado"]) for f in salida] == [("Curva PI 2", "No cumple")]
    r = crear(puente, nombre="Eje A1", capa="AUTOPISTA_1", simular=True, radios_por_pi="[600]", **AUTOPISTA_100)
    assert [f for f in r["verificacion"]["filas"] if f["comprobacion"] == regla] == [], "recta final de 2000 m: nada"


# ---------------------------------------------------------------------------------------------- decisiones del dueño (6.9)
CAPAS["PEQUENA_60"] = _por_rumbos([1000, 1000], [0.5])


def test_escritura_real_bajo_el_minimo_pide_confirmacion(servidor, puente, plugin):
    """f) En la escritura real, un radio bajo el mínimo no se crea sin la confirmación del usuario: error sin la llamada
    de escritura (solo INSUNITS y la simulación de ensayo). La confirmación es pasar ese radio en radios_por_pi."""
    r = crear(puente, nombre="Eje Norma B", capa="EJE_NORMA_B", pedido="crea el eje", **TERCERA_60)
    assert herramientas_llamadas(servidor) == ["leer_variable", HERRAMIENTA], "sin la llamada de escritura"
    assert servidor.llamadas[1]["args"]["simular"] is True and plugin.creados == {}
    assert r["ok"] is False and r["error"].startswith("No se creó el eje: la propuesta de la DG-2018 deja 1 PI bajo la norma")
    assert "PI 2: 80 m con mínimo 215 m" in r["error"] and "sin copia ni entrada en Deshacer" in r["error"]
    assert "Sección 301, p. 124; PDF 125" in r["error"] and "baja la velocidad de diseño del tramo homogéneo" in r["error"]
    assert r["radios_por_pi_para_confirmar"] == '[{"pi": 2, "radio": 80}]'
    assert r["sin_confirmar"][0]["pi"] == 2 and r["sin_confirmar"][0]["radio_propuesto"] == 80
    assert r["seccion_301"]["cita"].endswith("autorización del Ministerio de Transportes y Comunicaciones.")
    assert r["propuesta_norma"]["radios_por_pi"] == [215, 80] and r["propuesta_norma"]["radio"] is None
    assert "copia" not in r and "verificacion" not in r and r["ms_puente"] >= 0
    # confirmado: el radio va en radios_por_pi, pasa a ser del usuario y se crea con su aviso
    servidor.llamadas.clear()
    r = crear(puente, nombre="Eje Norma B", capa="EJE_NORMA_B", pedido="crea el eje con 80 m en el PI 2",
              radios_por_pi=r["radios_por_pi_para_confirmar"], **TERCERA_60)
    assert herramientas_llamadas(servidor) == ["leer_variable", HERRAMIENTA, HERRAMIENTA, "listar_pis"]
    assert r["simulado"] is False and "Eje Norma B" in plugin.creados
    p2 = pi(r, 2)
    assert p2["origen"] == "usuario" and p2["radio_pedido"] == 80 and p2["requiere_confirmacion"] is False
    assert "queda por debajo del mínimo de la norma (215 m" in p2["aviso"]
    assert filas(r["verificacion"], "Curva PI 2", "Radio mínimo")[0]["resultado"] == "No cumple"
    # con simular=true no se bloquea: muestra el mayor radio que cabe
    assert ne.falta_confirmacion({"pis": [{"numero": 2, "requiere_confirmacion": True}]}, True) is None


def test_elimina_el_pi_a_mas_de_80_kmh_con_deflexion_menor_de_59_minutos(servidor, puente, plugin):
    """b) A 100 km/h un PI de 0,5°: la norma no admite curva (302.02) ni el PI sin curva (cuadro de 30 a 80 km/h). La
    propuesta no pide curva (pediría R > 32 658 m): 0 y «elimina el PI»; la escritura real pide confirmarlo con 0."""
    r = crear(puente, nombre="Eje 05", capa="AUTOPISTA_05", simular=True, **AUTOPISTA_100)
    p1 = pi(r, 1)
    assert p1["radio_pedido"] == 0 and p1["origen"] == "sin_curva" and p1["eliminar_pi"] is True
    assert p1["cumple_minimo"] is False and p1["requiere_confirmacion"] is True
    assert p1["aviso"].startswith("PI 1: elimina el PI (realinea la polilínea)") and "32658.594" in p1["aviso"]
    assert "«No se usará nunca ángulos de deflexión menores de 59' (minutos).»" in p1["aviso"]
    v = r["verificacion"]
    sin_curva = filas(v, "PI 1 (sin curva)", "Deflexión máxima sin curva")[0]
    assert sin_curva["resultado"] == "No cumple" and "quita el PI y realinea el eje" in sin_curva["detalle"]["nota"]
    assert filas(v, "PI 1 (sin curva)", "Deflexión no menor de 0°59′")[0]["resultado"] == "Aviso"
    # escritura real: sin confirmar no se crea; confirmado con 0, sí (y avisa)
    servidor.llamadas.clear()
    r = crear(puente, nombre="Eje 05", capa="AUTOPISTA_05", **AUTOPISTA_100)
    assert r["ok"] is False and "PI 1: elimina el PI (sin curva, 0)" in r["error"]
    assert r["radios_por_pi_para_confirmar"] == '[{"pi": 1, "radio": 0}]' and len(servidor.llamadas) == 2
    r = crear(puente, nombre="Eje 05", capa="AUTOPISTA_05", radios_por_pi='[{"pi": 1, "radio": 0}]', **AUTOPISTA_100)
    assert r["simulado"] is False and "elimina el PI de la polilínea" in pi(r, 1)["aviso"]
    # una curva del usuario en esa deflexión: la norma no la admite
    r = crear(puente, nombre="Eje 05", capa="AUTOPISTA_05", simular=True, radios_por_pi="[40000]", **AUTOPISTA_100)
    assert "la norma no admite esa curva (302.02)" in pi(r, 1)["aviso"] and pi(r, 1)["cumple_minimo"] is False
    assert filas(r["verificacion"], "Curva PI 1", "Deflexión no menor de 0°59′")[0]["resultado"] == "No cumple"


def test_deflexion_menor_de_59_minutos_a_60_kmh(servidor, puente, plugin):
    """c) A 60 km/h el cuadro permite el PI de 0,5° sin curva (Cumple y aviso de 0°59′); con curva, «No cumple»."""
    r = crear(puente, nombre="Eje P", capa="PEQUENA_60", simular=True, **TERCERA_60)
    p1 = pi(r, 1)
    assert p1["radio_pedido"] == 0 and p1["cumple_minimo"] is True and "valora quitar el PI" in p1["nota"]
    assert filas(r["verificacion"], "PI 1 (sin curva)", "Deflexión no menor de 0°59′")[0]["resultado"] == "Aviso"
    r = crear(puente, nombre="Eje P", capa="PEQUENA_60", simular=True, radios_por_pi="[40000]", **TERCERA_60)
    assert "la norma no admite esa curva" in pi(r, 1)["aviso"] and pi(r, 1)["cumple_minimo"] is False
    assert filas(r["verificacion"], "Curva PI 1", "Deflexión no menor de 0°59′")[0]["resultado"] == "No cumple"


def test_reparto_avisa_si_otro_reparto_haria_cumplir(servidor, puente, plugin):
    """f) El PI 1 (120°) no cabe y ocupa la recta; el PI 2 (20°) queda sin la tangente mínima. Con R ≤ 100 m en el PI 1
    (que ya incumple), el PI 2 tendría su mínimo: se avisa. El nivel 2 del PI 2 no pasa del mínimo (215, no 470)."""
    r = crear(puente, nombre="Eje R", capa="REPARTO", simular=True, **TERCERA_60)
    p1, p2 = pi(r, 1), pi(r, 2)
    assert p1["radio_pedido"] == 125 and p1["nivel"] == 1 and p1["reparto"] is None
    assert p2["radio_pedido"] == 215 and p2["nivel"] == 2 and p2["incumple"] == []
    assert "y sin pasar del mínimo: un incumplimiento, la tangente bajo la mínima" in p2["aviso"]
    assert p2["reparto"]["pi_anterior"] == 1 and p2["reparto"]["radio_anterior_maximo"] == 100
    assert "Con R ≤ 100 m en el PI 1 (que ya queda bajo la norma), el PI 2 tendría su mínimo (215 m)" in p2["reparto"]["aviso"]
    assert p2["reparto"]["aviso"] in r["propuesta_norma"]["avisos"]
    # con ese reparto (confirmado por el usuario) el PI 2 cumple
    r = crear(puente, nombre="Eje R", capa="REPARTO", simular=True, radios_por_pi='[{"pi": 1, "radio": 100}]', **TERCERA_60)
    assert pi(r, 2)["cumple_minimo"] is True and pi(r, 2)["radio_pedido"] == 215 and pi(r, 2)["reparto"] is None
    servidor.llamadas.clear()
    r = crear(puente, nombre="Eje R", capa="REPARTO", **TERCERA_60)
    assert "PI 2: 215 m, el mínimo, pero sin la tangente mínima" in r["error"]
    assert r["radios_por_pi_para_confirmar"] == '[{"pi": 1, "radio": 125}, {"pi": 2, "radio": 215}]'


def test_velocidad_de_la_t20401_que_admite_el_radio(memoria):
    """f) La mayor velocidad de la T204.01 (menor que la de diseño) con la que el radio cumpliría los mínimos de las
    tablas; una velocidad sin fila en alguna tabla no se da por buena."""
    base = n.BaseNorma(n.localizar_base(str(memoria)))
    try:
        ctx, _ = n.resolver_proyecto(base, "alineamiento", dict(TERCERA_60))
        comunes = ne.minimos_del_proyecto(base, ctx)
        a50 = ne.velocidad_de_tabla_que_admite(base, ctx, comunes, 160, 90)
        assert a50["velocidad_kmh"] == 50 and a50["sin_filas"] == []
        assert {m["comprobacion"]: m["radio"] for m in a50["minimos"]} == {
            "Radio mínimo": 85, "Radio para prescindir de la curva de transición": 150}
        assert ne.velocidad_de_tabla_que_admite(base, ctx, comunes, 150, 90) == {"velocidad_kmh": None, "sin_filas": [40]}
        assert ne.velocidad_de_tabla_que_admite(base, ctx, comunes, 160, 4)["velocidad_kmh"] is None, "L > 30 (10 − Δ)"
    finally:
        base.cerrar()

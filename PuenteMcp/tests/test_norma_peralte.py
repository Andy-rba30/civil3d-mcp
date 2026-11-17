# -*- coding: utf-8 -*-
"""aplicar_peralte con velocidad_diseno (1.20.0): el puente calcula las progresivas críticas de cada curva y llama al
plugin. Se prueba la geometría, la cabida, la traducción a los nombres de Civil 3D y el envoltorio de main.py con un
plugin falso.

Los valores de la norma (peralte, bombeo, longitudes de transición y proporción en la tangente) y los nombres de los
enumerados de Civil 3D de estas pruebas son «ejemplo»: inventados solo para probar la lógica. Los reales salen de
dg2018.sqlite (sesión 6.4) y de listar_peralte → enumerados (VALIDACION_120, paso 1).
"""
import json

import pytest

import norma as n
import norma_peralte as npe
from base_norma_ejemplo import TERCERA, crear_base
from conftest import ejecutar

HERRAMIENTA = "aplicar_peralte"
PARAMETROS = [
    {"name": "alineamiento", "type": "string", "description": "Eje sin peralte", "required": True},
    {"name": "curvas", "type": "json", "description": "Curvas con sus progresivas críticas", "required": True},
    {"name": "simular", "type": "boolean", "description": "Simular"},
    {"name": "pedido", "type": "string", "description": "Pedido original del usuario"},
]
TERCERA_60 = {"velocidad_diseno": 60, "clasificacion": TERCERA, "orografia": "Ondulado", "ubicacion": "rural",
              "pavimentada": False, "peralte_maximo": 8}
# «ejemplo»: valores inventados para probar la lógica, no son de la DG-2018
VALORES_EJEMPLO = {"peralte_pct": 6.0, "bombeo_pct": 2.0, "longitud_peralte_m": 40.0, "longitud_bombeo_m": 40.0 / 3,
                   "proporcion_tangente": 0.7,
                   "valores": [{"valor": "peralte", "referencia": "ejemplo"}], "avisos": []}
# «ejemplo»: nombres inventados; los reales los da listar_peralte → enumerados
CORRESPONDENCIA_EJEMPLO = {
    "tipo": {npe.FBN: "TipoEjemploFBN", npe.BN: "TipoEjemploBN", npe.BI: "TipoEjemploBI", npe.IPT: "TipoEjemploIPT",
             npe.FPT: "TipoEjemploFPT", npe.IBN: "TipoEjemploIBN"},
    "region": {npe.ENTRADA: "RegionEjemploEntrada", npe.SALIDA: "RegionEjemploSalida"},
    "segmento": {npe.IZQUIERDO: "SegmentoEjemploIzq", npe.DERECHO: "SegmentoEjemploDer"},
}

CURVA_DERECHA = {"pi": 1, "radio": 150.0, "giro": "derecha", "progresiva_pc": 100.0, "progresiva_pt": 180.0}


def por_concepto(estaciones):
    return {(e["concepto"], e["region"]): e for e in estaciones}


# ---------------------------------------------------------------------------------------------- geometría
def test_progresivas_de_una_curva_a_la_derecha():
    est = npe.progresivas_criticas(CURVA_DERECHA, 6, 2, 40, 40 / 3, 0.7)
    assert [(e["concepto"], e["region"]) for e in est] == [
        (npe.FBN, npe.ENTRADA), (npe.BN, npe.ENTRADA), (npe.BI, npe.ENTRADA), (npe.IPT, npe.ENTRADA),
        (npe.FPT, npe.SALIDA), (npe.BI, npe.SALIDA), (npe.BN, npe.SALIDA), (npe.IBN, npe.SALIDA)]
    assert [e["progresiva"] for e in est] == [58.667, 72, 85.333, 112, 168, 194.667, 208, 221.333]
    c = por_concepto(est)
    # carril exterior (izquierdo en una curva a la derecha): −b → 0 → +b → +e; el interior: −b → −e
    assert c[(npe.FBN, npe.ENTRADA)]["pendientes"] == {npe.IZQUIERDO: -2, npe.DERECHO: -2}
    assert c[(npe.BN, npe.ENTRADA)]["pendientes"] == {npe.IZQUIERDO: 0, npe.DERECHO: -2}
    assert c[(npe.BI, npe.ENTRADA)]["pendientes"] == {npe.IZQUIERDO: 2, npe.DERECHO: -2}
    assert c[(npe.IPT, npe.ENTRADA)]["pendientes"] == {npe.IZQUIERDO: 6, npe.DERECHO: -6}
    assert c[(npe.IBN, npe.SALIDA)]["pendientes"] == {npe.IZQUIERDO: -2, npe.DERECHO: -2}
    assert c[(npe.IPT, npe.ENTRADA)]["progresiva_texto"] == "0+112.000"


def test_curva_a_la_izquierda_sube_el_carril_derecho():
    est = npe.progresivas_criticas({**CURVA_DERECHA, "giro": "izquierda"}, 6, 2, 40, 40 / 3, 0.7)
    c = por_concepto(est)
    assert c[(npe.BN, npe.SALIDA)]["pendientes"] == {npe.IZQUIERDO: -2, npe.DERECHO: 0}
    assert c[(npe.FPT, npe.SALIDA)]["pendientes"] == {npe.IZQUIERDO: -6, npe.DERECHO: 6}


def test_sin_bombeo_invertido_si_el_peralte_no_pasa_del_bombeo():
    est = npe.progresivas_criticas(CURVA_DERECHA, 2, 2, 20, 20, 0.5)
    assert npe.BI not in {e["concepto"] for e in est}
    assert len(est) == 6


@pytest.mark.parametrize("args, mensaje", [
    ((0, 2, 40, 10, 0.7), "peralte no válido"),
    ((6, -2, 40, 10, 0.7), "bombeo no válido"),
    ((6, 2, float("nan"), 10, 0.7), "longitud de transición del peralte no válido"),
    ((6, 2, 40, 0, 0.7), "longitud de transición del bombeo no válido"),
    ((6, 2, 40, 10, 1.2), "proporción de la transición en la tangente no válida"),
])
def test_valores_que_no_tienen_sentido(args, mensaje):
    with pytest.raises(n.ErrorNorma, match=mensaje):
        npe.progresivas_criticas(CURVA_DERECHA, *args)


def test_curva_sin_giro():
    with pytest.raises(n.ErrorNorma, match="no tiene giro"):
        npe.progresivas_criticas({**CURVA_DERECHA, "giro": "ninguno"}, 6, 2, 40, 10, 0.7)


# ---------------------------------------------------------------------------------------------- cabida
def curva_con(pi, pc, pt, giro="derecha"):
    c = {"pi": pi, "radio": 150.0, "giro": giro, "progresiva_pc": pc, "progresiva_pt": pt}
    return {**c, "estaciones": npe.progresivas_criticas(c, 6, 2, 40, 40 / 3, 0.7)}


def test_cabe_con_tangente_suficiente():
    assert npe.comprobar_cabida([curva_con(1, 100, 180), curva_con(2, 400, 480)], 0, 600) == []


def test_curva_corta_sin_tramo_de_peralte_total():
    problemas = npe.comprobar_cabida([curva_con(1, 100, 124)], 0, 600)
    assert len(problemas) == 1 and "PI 1: la curva es corta para el peralte" in problemas[0]


def test_transiciones_fuera_del_eje_y_cruzadas():
    problemas = npe.comprobar_cabida([curva_con(1, 30, 110), curva_con(2, 200, 280)], 0, 290)
    assert any("PI 1: las transiciones (-0+011.333 a 0+151.333) salen del eje" in p for p in problemas)
    assert any("PI 2: las transiciones" in p and "salen del eje" in p for p in problemas)
    assert any("Las transiciones de las curvas del PI 1 (hasta 0+151.333) y del PI 2 (desde 0+158.667)" in p
               for p in problemas) is False   # 151.333 < 158.667: no se cruzan
    cruzadas = npe.comprobar_cabida([curva_con(1, 100, 180), curva_con(2, 230, 300)], 0, 600)
    assert len(cruzadas) == 1 and "se cruzan" in cruzadas[0] and "R302.02-9" in cruzadas[0]


# ---------------------------------------------------------------------------------------------- curvas del eje
def tabla_pis(*curvas, fin=600.0, extra=None):
    pis = [{"numero": c["pi"], "tipo": "curva", "tiene_curva": True, "radio": c["radio"], "giro": c["giro"],
            "progresiva_pc": c["progresiva_pc"], "progresiva_pt": c["progresiva_pt"], "deflexion_grados": 30.0}
           for c in curvas]
    pis += extra or []
    return {"alineamiento": "Eje 1", "unidades": "Meters", "inicio": {"progresiva": 0.0}, "fin": {"progresiva": fin},
            "pis": pis}


def test_curvas_del_eje_solo_circulares_y_avisa_las_demas():
    tabla = tabla_pis(CURVA_DERECHA, extra=[
        {"numero": 2, "tipo": "sin_curva", "tiene_curva": False},
        {"numero": 3, "tipo": "AlignmentSCS", "tiene_curva": False},
        {"numero": 4, "tipo": "curva", "tiene_curva": True, "radio": None, "progresiva_pc": 300, "progresiva_pt": 350}])
    curvas, avisos = npe.curvas_del_eje(tabla)
    assert [c["pi"] for c in curvas] == [1]
    assert avisos == ["PI 3: tipo AlignmentSCS no soportado (solo curvas circulares); queda sin peralte.",
                      "PI 4: listar_pis no da radio, PC o PT; la curva queda sin peralte."]


# ---------------------------------------------------------------------------------------------- traducción
def test_la_correspondencia_esta_vacia_hasta_la_validacion():
    faltan = npe.correspondencia_incompleta()
    assert len(faltan) == len(npe.TIPOS) + len(npe.REGIONES) + len(npe.SEGMENTOS)
    with pytest.raises(n.ErrorNorma, match="no se inventan"):
        npe.curvas_para_el_plugin([curva_con(1, 100, 180)])


def test_curvas_para_el_plugin_con_nombres_de_ejemplo():
    curvas = npe.curvas_para_el_plugin([curva_con(1, 100, 180)], CORRESPONDENCIA_EJEMPLO)
    assert curvas[0]["pi"] == 1 and len(curvas[0]["estaciones"]) == 8
    primera = curvas[0]["estaciones"][0]
    assert primera == {"progresiva": 58.667, "tipo": "TipoEjemploFBN", "region": "RegionEjemploEntrada",
                       "pendientes": {"SegmentoEjemploIzq": -2, "SegmentoEjemploDer": -2}}


def test_verificar_lectura():
    curva = curva_con(1, 100, 180)
    leido = {"curvas": [{"nombre": "Curva.1", "estaciones": [
        {"progresiva": 112.0004, "pendientes": {"SegmentoEjemploIzq": 6, "SegmentoEjemploDer": -6}}]}]}
    v = npe.verificar_lectura([curva], leido, CORRESPONDENCIA_EJEMPLO)
    assert v["verificado"] is True and len(v["filas"]) == 2
    leido["curvas"][0]["estaciones"][0]["pendientes"]["SegmentoEjemploDer"] = 6
    v = npe.verificar_lectura([curva], leido, CORRESPONDENCIA_EJEMPLO)
    assert v["verificado"] is False and v["avisos"] == ["PI 1: el peralte total de carril_derecho leído es 6 % y se pidió -6 %."]


def test_sin_tablas_de_peralte_en_la_base(tmp_path):
    carpeta = tmp_path / "memoria-firma"
    with n.BaseNorma(crear_base(str(carpeta))) as base:
        with pytest.raises(n.ErrorNorma, match="todavía no tiene las tablas de peralte"):
            npe.preparar_peralte(base, None, {})


# ---------------------------------------------------------------------------------------------- envoltorio de main.py
class PluginFalso:
    def __init__(self, servidor):
        self.servidor = servidor
        self.insunits = "6"
        self.tabla = tabla_pis(CURVA_DERECHA, {"pi": 2, "radio": 200.0, "giro": "izquierda", "progresiva_pc": 400.0,
                                               "progresiva_pt": 480.0})
        self.aplicado = None
        servidor.herramienta(HERRAMIENTA, PARAMETROS, "Pone el peralte en un eje.")
        servidor.respuestas[HERRAMIENTA] = self.aplicar
        servidor.respuestas["leer_variable"] = lambda c: {
            "ok": True, "id_log": "id-insunits", "result": {"variable": "INSUNITS", "valor": self.insunits, "tipo": "Int16"}}
        servidor.respuestas["listar_pis"] = lambda c: {"ok": True, "tool": "listar_pis", "id_log": "id-pis",
                                                       "result": self.tabla}
        servidor.respuestas["listar_peralte"] = self.listar

    def aplicar(self, cuerpo):
        a = cuerpo["args"]
        curvas = json.loads(a["curvas"]) if isinstance(a["curvas"], str) else a["curvas"]
        simulado = bool(a.get("simular"))
        if not simulado:
            self.aplicado = curvas
        return {"ok": True, "tool": HERRAMIENTA, "id_log": f"id-aplicar-{len(self.servidor.llamadas)}",
                "result": {"simulado": simulado, "herramienta": HERRAMIENTA, "cambios": ["tiene_peralte"],
                           "antes": {"tiene_peralte": False}, "despues": {"tiene_peralte": True,
                                                                          "numero_curvas": len(curvas)}}}

    def listar(self, cuerpo):
        curvas = [{"nombre": f"Curva.{c['pi']}", "estaciones": c["estaciones"]} for c in self.aplicado or []]
        return {"ok": True, "tool": "listar_peralte", "id_log": "id-listar-peralte",
                "result": {"tiene_peralte": bool(curvas), "curvas": curvas}}


@pytest.fixture
def memoria(tmp_path, monkeypatch):
    carpeta = tmp_path / "memoria-firma"
    crear_base(str(carpeta))
    monkeypatch.setenv("MEMORIA_FIRMA_DIR", str(carpeta))
    return carpeta


@pytest.fixture
def norma_ejemplo(monkeypatch):
    """Sustituye las consultas de las tablas de peralte (aún sin esquema) y la correspondencia por las de ejemplo."""
    monkeypatch.setattr(npe, "preparar_peralte", lambda base, ctx, proyecto: ({}, []))
    monkeypatch.setattr(npe, "valores_de_curva", lambda base, ctx, datos, curva: dict(VALORES_EJEMPLO))
    monkeypatch.setattr(npe, "CORRESPONDENCIA", CORRESPONDENCIA_EJEMPLO)


@pytest.fixture
def plugin(servidor, puente, memoria):
    p = PluginFalso(servidor)
    ejecutar(puente.sincronizar_herramientas())
    return p


def aplicar(puente, **args):
    texto = ejecutar(puente.mcp.call_tool(HERRAMIENTA, {"args": args})).content[0].text
    return json.loads(texto)


def herramientas_llamadas(servidor):
    return [c["tool"] for c in servidor.llamadas]


def test_sin_velocidad_va_directo_al_plugin(servidor, puente, plugin):
    curvas = json.dumps([{"pi": 1, "estaciones": [{"progresiva": 80, "tipo": "T", "pendientes": {"S": -2}}]}])
    r = aplicar(puente, alineamiento="Eje 1", curvas=curvas, simular=True)
    assert herramientas_llamadas(servidor) == [HERRAMIENTA]
    assert r["simulado"] is True and "propuesta_norma" not in r


def test_esquema_ampliado_curvas_opcional(servidor, puente, plugin):
    herramienta = puente.mcp._tool_manager.get_tool(HERRAMIENTA)
    modelo = herramienta.parameters["$defs"][f"{HERRAMIENTA}_Model"]
    assert set(npe.CAMPOS) <= set(modelo["properties"])
    assert modelo["required"] == ["alineamiento"], "curvas deja de ser obligatoria"
    assert "DG-2018" in herramienta.description
    assert "Con 'velocidad_diseno' no se pasa" in modelo["properties"]["curvas"]["description"]
    assert npe.campos_que_chocan([{"name": "velocidad_diseno"}]) == ["velocidad_diseno"]
    assert not npe.se_puede_ampliar([p for p in PARAMETROS if p["name"] != "curvas"])


def test_la_ampliacion_del_eje_sigue_igual(servidor, puente, plugin):
    import norma_alineamiento as ne
    assert puente.AMPLIACIONES_NORMA["crear_alineamiento_desde_polilinea"][0] is ne
    assert ne.CAMPOS is ne.CAMPOS_PROYECTO and "radio" in ne.PARAMETROS_OPCIONALES


def test_curvas_y_norma_a_la_vez(servidor, puente, plugin):
    r = aplicar(puente, alineamiento="Eje 1", curvas="[]x", **TERCERA_60)
    assert r["ok"] is False and "no los dos" in r["error"] and servidor.llamadas == []


def test_faltan_datos_no_llama_al_plugin(servidor, puente, plugin, norma_ejemplo):
    r = aplicar(puente, alineamiento="Eje 1", velocidad_diseno=60)
    assert r["ok"] is False and "faltan" in r and servidor.llamadas == []
    assert {"clasificacion", "orografia"} <= {f["dato"] for f in r["faltan"]}


def test_sin_tablas_de_peralte_no_llama_al_plugin(servidor, puente, plugin):
    r = aplicar(puente, alineamiento="Eje 1", simular=True, **TERCERA_60)
    assert r["ok"] is False and "todavía no tiene las tablas de peralte" in r["error"] and servidor.llamadas == []


def test_simulacion_con_la_norma(servidor, puente, plugin, norma_ejemplo):
    r = aplicar(puente, alineamiento="Eje 1", simular=True, pedido="pon el peralte", **TERCERA_60)
    assert herramientas_llamadas(servidor) == ["leer_variable", "listar_pis", HERRAMIENTA]
    final = servidor.llamadas[2]["args"]
    assert final["simular"] is True and final["pedido"] == "pon el peralte" and final["alineamiento"] == "Eje 1"
    assert not set(npe.CAMPOS) & set(final), "los datos del proyecto no llegan al plugin"
    curvas = json.loads(final["curvas"])
    assert [c["pi"] for c in curvas] == [1, 2] and len(curvas[0]["estaciones"]) == 8
    assert r["simulado"] is True and "verificacion" not in r
    propuesta = r["propuesta_norma"]
    assert propuesta["velocidad_diseno"] == 60 and propuesta["lecturas"]["listar_pis"] == "id-pis"
    assert propuesta["curvas"][0]["peralte_pct"] == 6 and propuesta["curvas"][0]["valores"]
    assert propuesta["unidades"]["INSUNITS"] == "6"


def test_escritura_real_verifica_con_listar_peralte(servidor, puente, plugin, norma_ejemplo):
    r = aplicar(puente, alineamiento="Eje 1", **TERCERA_60)
    assert herramientas_llamadas(servidor) == ["leer_variable", "listar_pis", HERRAMIENTA, "listar_peralte"]
    assert r["simulado"] is False
    assert r["verificacion"]["verificado"] is True and r["verificacion"]["id_log"] == "id-listar-peralte"
    assert len(r["verificacion"]["filas"]) == 4


def test_sin_correspondencia_devuelve_la_propuesta_sin_escribir(servidor, puente, plugin, norma_ejemplo, monkeypatch):
    monkeypatch.setattr(npe, "CORRESPONDENCIA", {"tipo": {c: None for c in npe.TIPOS},
                                                 "region": {c: None for c in npe.REGIONES},
                                                 "segmento": {c: None for c in npe.SEGMENTOS}})
    r = aplicar(puente, alineamiento="Eje 1", simular=True, **TERCERA_60)
    assert herramientas_llamadas(servidor) == ["leer_variable", "listar_pis"]
    assert r["ok"] is False and "Falta la correspondencia" in r["error"] and r["propuesta_norma"]["curvas"]


def test_no_cabe_no_escribe(servidor, puente, plugin, norma_ejemplo):
    plugin.tabla = tabla_pis(CURVA_DERECHA, {"pi": 2, "radio": 200.0, "giro": "izquierda", "progresiva_pc": 230.0,
                                             "progresiva_pt": 300.0})
    r = aplicar(puente, alineamiento="Eje 1", simular=True, **TERCERA_60)
    assert herramientas_llamadas(servidor) == ["leer_variable", "listar_pis"]
    assert r["ok"] is False and "no cabe" in r["error"] and "se cruzan" in r["error"]


def test_eje_sin_curvas(servidor, puente, plugin, norma_ejemplo):
    plugin.tabla = tabla_pis(extra=[{"numero": 1, "tipo": "sin_curva", "tiene_curva": False}])
    r = aplicar(puente, alineamiento="Eje 1", simular=True, **TERCERA_60)
    assert r["ok"] is False and "no tiene curvas circulares" in r["error"]
    assert herramientas_llamadas(servidor) == ["leer_variable", "listar_pis"]


def test_dibujo_que_no_esta_en_metros(servidor, puente, plugin, norma_ejemplo):
    plugin.insunits = "2"
    r = aplicar(puente, alineamiento="Eje 1", simular=True, **TERCERA_60)
    assert r["ok"] is False and "INSUNITS = 2, pies" in r["error"] and "aplicar_peralte" in r["error"]
    assert herramientas_llamadas(servidor) == ["leer_variable"]


def test_error_de_listar_pis(servidor, puente, plugin, norma_ejemplo):
    servidor.respuestas["listar_pis"] = {"ok": False, "error": "No existe el alineamiento 'X'. Usa listar_alineamientos.",
                                         "id_log": "id-no"}
    r = aplicar(puente, alineamiento="X", simular=True, **TERCERA_60)
    assert r["ok"] is False and "No existe el alineamiento 'X'" in r["error"] and r["id_log"] == "id-no"

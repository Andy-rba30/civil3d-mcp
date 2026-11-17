# -*- coding: utf-8 -*-
"""aplicar_peralte con velocidad_diseno (1.20.0): el puente calcula las progresivas críticas de cada curva y llama al
plugin. Se prueba la geometría, la cabida, la traducción a los nombres de Civil 3D y el envoltorio de main.py con un
plugin falso.

Los valores de la norma (peralte, bombeo, longitudes de transición y proporción en la tangente) y los nombres de los
enumerados de Civil 3D de estas pruebas son «ejemplo»: inventados solo para probar la lógica. Los reales salen de
dg2018.sqlite (sesión 6.4) y de listar_peralte → enumerados (VALIDACION_120, paso 1).
"""
import io
import json
import sqlite3

import pytest

import casos_norma_reales

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

# «ejemplo»: nodos inventados de las figuras (ref, orden, velocidad, radio, peralte, error, tipo de punto); los reales
# son los de la tarea 6.10 en dg2018.sqlite
NODOS_EJEMPLO = [
    ("F302.03", 1, 30, 20.0, 8.0, 0.1, "extremo_superior"), ("F302.03", 2, 30, 40.0, 6.0, 0.1, "nodo"),
    ("F302.03", 3, 30, 80.0, 4.0, 0.1, "nodo"), ("F302.03", 4, 30, 160.0, 2.0, 0.2, "extremo_inferior"),
    ("F302.03", 1, 50, 100.0, 8.0, 0.1, "extremo_superior"), ("F302.03", 2, 50, 400.0, 7.1, 0.1, "nodo"),
    ("F302.03", 3, 50, 1000.0, 2.0, 0.2, "extremo_inferior"),
    ("F302.03", 1, 60, 140.0, 8.0, 0.1, "extremo_superior"), ("F302.03", 2, 60, 200.0, 6.0, 0.15, "nodo"),
    ("F302.03", 3, 60, 400.0, 4.0, 0.1, "nodo"), ("F302.03", 4, 60, 800.0, 2.0, 0.1, "nodo"),
    ("F302.03", 5, 60, 1600.0, 1.5, 0.3, "extremo_inferior"),
    ("F302.04", 1, 30, 30.0, 12.0, 0.1, "extremo_superior"), ("F302.04", 2, 30, 300.0, 2.0, 0.2, "extremo_inferior"),
    ("F302.04", 1, 60, 120.0, 12.0, 0.1, "extremo_superior"), ("F302.04", 2, 60, 300.0, 8.0, 0.2, "nodo"),
    ("F302.04", 3, 60, 900.0, 2.0, 0.2, "extremo_inferior"),
    ("F302.02", 1, 60, 150.0, 4.0, 0.1, "extremo_superior"), ("F302.02", 2, 60, 600.0, 2.0, 0.1, "extremo_inferior"),
]


def agregar_figuras(ruta_base: str, metodo: str = "vectorial"):
    """Añade a la base de ejemplo la tabla peralte_figura (esquema de la tarea 6.10) con NODOS_EJEMPLO."""
    con = sqlite3.connect(ruta_base)
    con.execute("CREATE TABLE peralte_figura (ref TEXT, pagina_pdf INTEGER, orden INTEGER, velocidad_kmh INTEGER, "
                "radio_m REAL, peralte_pct REAL, error_pct REAL, tipo_punto TEXT, x_pt REAL, y_pt REAL, metodo TEXT)")
    con.executemany("INSERT INTO peralte_figura VALUES (?, 0, ?, ?, ?, ?, ?, ?, 0, 0, ?)",
                    [(*fila, metodo) for fila in NODOS_EJEMPLO])
    con.commit()
    con.close()
    return ruta_base


@pytest.fixture
def base_figura(tmp_path):
    ruta = agregar_figuras(crear_base(str(tmp_path / "memoria-firma")))
    with n.BaseNorma(ruta) as base:
        yield base


def rmin(base, valor=125.0):
    return {"exigido": valor, "referencia": base.referencia("T302.02")}


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


def test_sin_la_tabla_de_la_figura(tmp_path):
    carpeta = tmp_path / "memoria-firma"
    with n.BaseNorma(crear_base(str(carpeta))) as base:
        with pytest.raises(n.ErrorNorma, match="no tiene la tabla peralte_figura"):
            npe.preparar_peralte(base, None, {})


# ---------------------------------------------------------------------------------------------- peralte de la figura
def test_interpolacion_en_log_del_radio_con_un_decimal(base_figura):
    r = npe.peralte_de_figura(base_figura, "F302.03", 60, 150, rmin(base_figura))
    # entre (140; 8,0) y (200; 6,0): t = log(150/140) / log(200/140) = 0,1934 → 7,613
    assert r["regla"] == 7 and r["resultado"] == npe.LECTURA and r["etiqueta"] == "lectura gráfica"
    assert r["peralte_calculado_pct"] == pytest.approx(7.613, abs=0.001) and r["peralte_pct"] == 7.6
    assert r["error_pct"] == 0.15 and [x["radio_m"] for x in r["nodos"]] == [140, 200]
    assert any("lectura gráfica, error ±0.15 %" in a and "no es una tabla de la norma" in a for a in r["avisos"])
    assert r["a_confirmar"] is False


def test_redondeo_de_la_mitad_hacia_arriba(base_figura):
    # entre (100; 8,0) y (400; 7,1), R = 200 es el punto medio en log: 7,55 → 7,6 (no 7,5)
    r = npe.peralte_de_figura(base_figura, "F302.03", 50, 200, rmin(base_figura, 70))
    assert r["peralte_calculado_pct"] == pytest.approx(7.55) and r["peralte_pct"] == 7.6


def test_radio_en_un_nodo(base_figura):
    r = npe.peralte_de_figura(base_figura, "F302.03", 60, 200, rmin(base_figura))
    assert r["peralte_pct"] == 6.0


def test_a_30_kmh_entre_el_radio_minimo_y_el_extremo_superior_va_el_p_maximo(base_figura):
    r = npe.peralte_de_figura(base_figura, "F302.03", 30, 15, rmin(base_figura, 10))
    assert r["regla"] == 6 and r["resultado"] == npe.P_MAXIMO and r["peralte_pct"] == 8.0
    assert r["a_confirmar"] is True and any("A CONFIRMAR por Jose" in a for a in r["avisos"])
    r = npe.peralte_de_figura(base_figura, "F302.03", 30, 30, rmin(base_figura, 10))
    assert r["regla"] == 7 and r["peralte_pct"] == 6.8   # 8 − 2·log(1,5)/log(2) = 6,83


def test_radio_menor_que_el_minimo_no_cumple_y_no_lee_la_figura(base_figura):
    r = npe.peralte_de_figura(base_figura, "F302.03", 60, 100, rmin(base_figura))
    assert r["regla"] == 3 and r["resultado"] == n.NO_CUMPLE and r["peralte_pct"] is None and r["nodos"] == []
    assert "es menor que el radio mínimo (125 m, T302.02" in r["avisos"][0]


def test_t30404_sin_peralte_y_pendientes_sin_la_6_4(base_figura):
    r = npe.peralte_de_figura(base_figura, "F302.03", 60, 750, rmin(base_figura), t30404={"radio_m": 700})
    assert r["regla"] == 4 and r["resultado"] == npe.SIN_PERALTE and r["peralte_pct"] is None
    r = npe.peralte_de_figura(base_figura, "F302.03", 60, 300, rmin(base_figura))
    assert len(r["pendientes"]) == 3 and any("T304.04" in x for x in r["pendientes"])
    assert any("T304.06" in x for x in r["pendientes"]) and any("bombeo" in x for x in r["pendientes"])


def test_t30406_minimo_del_2_y_aviso_por_debajo_del_bombeo(base_figura):
    # entre (800; 2,0) y (1600; 1,5): R 1200 → 2 − 0,5·log(1,5)/log(2) = 1,71 → 1,7; con la T304.06, 2,0
    r = npe.peralte_de_figura(base_figura, "F302.03", 60, 1200, rmin(base_figura), t30404={"radio_m": 3000},
                              t30406={"aplica": True}, bombeo_pct=2.5)
    assert r["peralte_pct"] == 2.0
    assert any("menor que el peralte mínimo de la T304.06 (2 %)" in a for a in r["avisos"])
    assert any("queda por debajo del bombeo (2.5 %)" in a for a in r["avisos"])
    assert r["pendientes"] == []
    r = npe.peralte_de_figura(base_figura, "F302.03", 60, 1200, rmin(base_figura), t30404={"radio_m": 3000},
                              t30406={"aplica": False}, bombeo_pct=1.5)
    assert r["peralte_pct"] == 1.7 and not any("bombeo" in a for a in r["avisos"])


def test_mas_alla_del_extremo_inferior_no_hay_valor(base_figura):
    r = npe.peralte_de_figura(base_figura, "F302.03", 60, 2000, rmin(base_figura))
    assert r["resultado"] == n.SIN_VALOR and r["peralte_pct"] is None and r["a_confirmar"] is True


def test_aviso_si_la_relacion_no_es_de_la_figura_digitalizada(tmp_path):
    ruta = agregar_figuras(crear_base(str(tmp_path / "m")), metodo="AASHTO")
    with n.BaseNorma(ruta) as base:
        r = npe.peralte_de_figura(base, "F302.03", 60, 150, rmin(base))
    assert any("tipo AASHTO, no de la DG-2018" in a for a in r["avisos"])


def ctx_de(base, **datos):
    ctx, faltan = n.resolver_proyecto(base, "alineamiento", {**TERCERA_60, **datos})
    assert not faltan, faltan
    return ctx


def test_la_figura_sale_de_la_t30405_y_en_accidentado_de_la_respuesta(tmp_path):
    ruta = agregar_figuras(crear_base(str(tmp_path / "m")))
    with n.BaseNorma(ruta) as base:
        assert npe.figura_de_condicion("Atravesamiento de zonas urbanas") == "F302.02"
        assert npe.figura_de_condicion("Zona rural con peligro de hielo") == "F302.05"
        datos, faltan = npe.preparar_peralte(base, ctx_de(base), {})
        assert faltan == [] and datos["figura"] == "F302.03" and datos["radio_minimo"]["exigido"]
        for pmax, figura in ((12, "F302.04"), (8, "F302.03")):
            ctx = ctx_de(base, orografia="Accidentado", velocidad_diseno=30, peralte_maximo=pmax)
            assert npe.preparar_peralte(base, ctx, {})[0]["figura"] == figura


def test_velocidad_que_no_es_una_curva_de_la_figura(tmp_path):
    ruta = agregar_figuras(crear_base(str(tmp_path / "m")))
    with n.BaseNorma(ruta) as base:
        datos, faltan = npe.preparar_peralte(base, ctx_de(base, velocidad_diseno=70), {})
    assert datos is None and faltan[0]["dato"] == "velocidad_diseno"
    assert faltan[0]["valores_validos"] == [30, 50, 60] and "no se interpola entre velocidades" in faltan[0]["motivo"]


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
    agregar_figuras(crear_base(str(carpeta)))
    monkeypatch.setenv("MEMORIA_FIRMA_DIR", str(carpeta))
    return carpeta


@pytest.fixture
def norma_ejemplo(monkeypatch):
    """Sustituye las tablas de bombeo y transición (sesión 6.4, aún sin esquema) y la correspondencia por las de
    ejemplo. El peralte sale de la figura de ejemplo (agregar_figuras)."""
    def transicion(base, ctx, datos, curva, peralte_pct):
        v = dict(VALORES_EJEMPLO)
        v["longitud_bombeo_m"] = v["longitud_peralte_m"] * v["bombeo_pct"] / peralte_pct
        return v
    monkeypatch.setattr(npe, "transicion_de_curva", transicion)
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


def test_sin_la_figura_no_llama_a_civil_3d(servidor, puente, plugin, memoria):
    crear_base(str(memoria))   # la base sin peralte_figura
    r = aplicar(puente, alineamiento="Eje 1", simular=True, **TERCERA_60)
    assert r["ok"] is False and "no tiene la tabla peralte_figura" in r["error"] and servidor.llamadas == []


def test_sin_tablas_de_transicion_devuelve_el_peralte_sin_escribir(servidor, puente, plugin):
    r = aplicar(puente, alineamiento="Eje 1", simular=True, **TERCERA_60)
    assert herramientas_llamadas(servidor) == ["leer_variable", "listar_pis"]
    assert r["ok"] is False and "bombeo y de longitud de transición" in r["error"]
    curvas = r["propuesta_norma"]["curvas"]
    assert [c["peralte_pct"] for c in curvas] == [7.6, 6.0]   # R 150 y R 200 a 60 km/h en la F302.03 de ejemplo
    assert curvas[0]["peralte"]["figura"] == "F302.03" and curvas[0]["peralte"]["etiqueta"] == "lectura gráfica"
    assert curvas[0]["estaciones"] == []


def test_curva_por_debajo_del_radio_minimo_no_escribe(servidor, puente, plugin, norma_ejemplo):
    plugin.tabla = tabla_pis({**CURVA_DERECHA, "radio": 100.0})
    r = aplicar(puente, alineamiento="Eje 1", simular=True, **TERCERA_60)
    assert r["ok"] is False and "no cumple o no cabe" in r["error"] and "PI 1: No cumple" in r["error"]
    assert herramientas_llamadas(servidor) == ["leer_variable", "listar_pis"]


def test_velocidad_sin_curva_en_la_figura_pregunta(servidor, puente, plugin, norma_ejemplo):
    r = aplicar(puente, alineamiento="Eje 1", simular=True, **{**TERCERA_60, "velocidad_diseno": 70})
    assert r["ok"] is False and r["faltan"][0]["dato"] == "velocidad_diseno" and servidor.llamadas == []


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
    assert propuesta["curvas"][0]["peralte_pct"] == 7.6 and propuesta["curvas"][0]["valores"]
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


# ---------------------------------------------------------------------------------------------- casos de control (PC)
def test_casos_de_control_de_la_figura_se_saltan_sin_la_tabla(tmp_path):
    with n.BaseNorma(crear_base(str(tmp_path / "m"))) as base:
        informe = casos_norma_reales.Informe(io.StringIO())
        casos_norma_reales.comprobar_figuras_peralte(base, informe)
    assert "no tiene peralte_figura (tarea 6.10); se omiten 6 casos" in informe.salida.getvalue()
    assert informe.ok == 0 and informe.fallos == 0


def test_casos_de_control_de_la_figura_con_la_base_de_ejemplo(base_figura):
    # «ejemplo»: los valores salen de NODOS_EJEMPLO, no son los de control reales
    casos = (("F302.03", 60, 150, 7.61), ("F302.03", 50, 200, 7.55), ("F302.03", 60, 150, 8.0), ("F302.03", 70, 150, 5.0))
    informe = casos_norma_reales.Informe(io.StringIO())
    casos_norma_reales.comprobar_figuras_peralte(base_figura, informe, casos)
    texto = informe.salida.getvalue()
    assert informe.ok == 2 and informe.fallos == 2, texto
    assert "[OK   ] F302.03 · 60 km/h · R = 150 m: esperado 7.61, obtenido 7.613 (tolerancia 0.2)" in texto
    assert "[FALLO] F302.03 · 70 km/h · R = 150 m" in texto and "no se interpola entre velocidades" in texto
    assert len(casos_norma_reales.CASOS_PERALTE_FIGURA) == 6

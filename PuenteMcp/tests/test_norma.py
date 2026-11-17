# -*- coding: utf-8 -*-
"""verificar_norma (1.18.0): comprobaciones contra la DG-2018 con una tabla pequeña de ejemplo (base_norma_ejemplo.py)
y, de punta a punta, contra el servidor falso del puente (listar_pis y listar_pvis).

Los escenarios de radio mínimo, pendiente máxima y curva vertical siguen los casos calculados a mano por el dueño
(casos 1, 4 y 5). Los casos 2 (peralte de una curva) y 3 (visibilidad de parada disponible) no se pueden comprobar con
verificar_norma: el peralte de diseño y la visibilidad disponible no se leen del dibujo.
"""
import io
import json
import math
import os

import pytest

import casos_norma_reales
import norma as n
from base_norma_ejemplo import SEGUNDA, TERCERA, crear_base
from conftest import ejecutar

TERCERA_ONDULADO = {"clasificacion": "Carretera de Tercera Clase", "orografia": "Ondulado", "ubicacion": "rural",
                    "pavimentada": False, "peralte_maximo": 8}


@pytest.fixture
def memoria(tmp_path, monkeypatch):
    carpeta = tmp_path / "memoria-firma"
    crear_base(str(carpeta))
    monkeypatch.setenv("MEMORIA_FIRMA_DIR", str(carpeta))
    return carpeta


@pytest.fixture
def base(memoria):
    b = n.BaseNorma(n.localizar_base(str(memoria)))
    yield b
    b.cerrar()


def contexto(base, tipo="alineamiento", **datos):
    ctx, faltan = n.resolver_proyecto(base, tipo, datos)
    assert faltan == [], faltan
    return ctx


# ---------------------------------------------------------------------------------------------- caso 1: radio mínimo
@pytest.mark.parametrize("v, radio, esperado", [
    (40, 45, n.NO_CUMPLE), (40, 60, n.CUMPLE), (40, 50, n.CUMPLE_AVISO),
    (60, 120, n.NO_CUMPLE), (60, 130, n.CUMPLE), (60, 124, n.NO_CUMPLE),
    (80, 220, n.NO_CUMPLE), (80, 250, n.CUMPLE),
])
def test_radio_minimo_tercera_clase_manda_la_tabla(base, v, radio, esperado):
    ctx = contexto(base, velocidad_diseno=v, **TERCERA_ONDULADO)
    f = n.comprobar_radio_minimo(base, ctx, radio, "Curva PI 1", "0+100.000")
    assert f["resultado"] == esperado
    assert f["valor_exigido"] == {40: 50, 60: 125, 80: 230}[v]
    assert f["referencia"]["id"] == "T302.02" and f["referencia"]["pagina_pdf"] == "130"
    formula = {40: 50.39, 60: 123.25, 80: 229.06}[v]
    assert abs(f["detalle"]["valor_formula"] - formula) <= 0.005, "F08 = V² / (127 (e + f))"
    assert f["detalle"]["formula"]["id"] in ("F08", "F09")


def test_radio_minimo_dos_tablas_distintas_aplica_la_mas_exigente(base):
    ctx = contexto(base, velocidad_diseno=50, **TERCERA_ONDULADO)
    ex = n.radio_minimo_exigido(base, ctx)
    assert ex["exigido"] == 85 and ex["referencia"]["id"] == "T302.02"
    assert ex["otra_tabla"]["valor"] == 80 and "T302.04" in ex["otra_tabla"]["referencia"]
    assert "más exigente" in ex["aviso"]
    assert n.comprobar_radio_minimo(base, ctx, 82)["resultado"] == n.NO_CUMPLE
    assert n.comprobar_radio_minimo(base, ctx, 90)["resultado"] == n.CUMPLE_AVISO


def test_radio_minimo_sin_fila_es_sin_valor(base):
    ctx = contexto(base, velocidad_diseno=70, **TERCERA_ONDULADO)
    f = n.comprobar_radio_minimo(base, ctx, 300)
    assert f["resultado"] == n.SIN_VALOR and "interpolar" in f["detalle"]["nota"]


def test_terreno_accidentado_el_peralte_elige_la_fila_de_la_t30202(base):
    ctx8 = contexto(base, velocidad_diseno=60, clasificacion=SEGUNDA, orografia="Accidentado", ubicacion="rural",
                    peralte_maximo=8)
    ctx12 = contexto(base, velocidad_diseno=60, clasificacion=SEGUNDA, orografia="Accidentado", ubicacion="rural",
                     peralte_maximo=12)
    assert n.radio_minimo_exigido(base, ctx8)["exigido"] == 125
    assert n.radio_minimo_exigido(base, ctx12)["exigido"] == 105


# ---------------------------------------------------------------------------------------------- caso 4: pendiente máxima
@pytest.mark.parametrize("altitud, pendiente, esperado, exigido, excepcional", [
    (3000, 8.5, n.NO_CUMPLE, 8, 9), (3000, 7.5, n.CUMPLE, 8, 9), (3000, 8.0, n.CUMPLE, 8, 9),
    (3500, 7.5, n.NO_CUMPLE, 7, 8), (3500, 7.0, n.CUMPLE, 7, 8), (3500, -7.5, n.NO_CUMPLE, 7, 8),
])
def test_pendiente_maxima_segunda_clase_accidentado(base, altitud, pendiente, esperado, exigido, excepcional):
    ctx = contexto(base, "rasante", velocidad_diseno=60, clasificacion=SEGUNDA, orografia="3", altitud_msnm=altitud)
    f = n.comprobar_pendiente_maxima(base, ctx, pendiente, altitud, "Tramo PVI 1–PVI 2")
    assert f["resultado"] == esperado
    assert f["valor_exigido"] == exigido and f["detalle"]["maximo_excepcional"] == excepcional
    assert f["referencia"]["id"] == "T303.01"
    if esperado == n.NO_CUMPLE:
        assert "excepción" in f["detalle"]["nota"]
    if altitud > 3000:
        assert "3000 msnm" in f["detalle"]["nota"] and "pendiente del dueño" in f["detalle"]["nota"]


def test_pendiente_maxima_celda_vacia(base):
    ctx = contexto(base, "rasante", velocidad_diseno=70, clasificacion=SEGUNDA, orografia="Ondulado")
    assert n.comprobar_pendiente_maxima(base, ctx, 5)["resultado"] == n.SIN_VALOR


# ---------------------------------------------------------------------------------------------- caso 5: curva vertical
@pytest.mark.parametrize("pe, ps, longitud, esperado, exigido, formula, formula_id, tabla", [
    (4, -3, 60, n.NO_CUMPLE, 77, 125.2, "F26", "T303.02"),
    (4, -3, 130, n.CUMPLE, 77, 125.2, "F26", "T303.02"),
    (4, -3, 80, n.CUMPLE_AVISO, 77, 125.2, "F26", "T303.02"),
    (-5, 3, 120, n.NO_CUMPLE, 144, 138.4, "F30", "T303.03"),
    (-5, 3, 150, n.CUMPLE, 144, 138.4, "F30", "T303.03"),
    (-5, 3, 140, n.NO_CUMPLE, 144, 138.4, "F30", "T303.03"),
])
def test_curva_vertical_tercera_clase_con_k(base, pe, ps, longitud, esperado, exigido, formula, formula_id, tabla):
    ctx = contexto(base, "rasante", velocidad_diseno=60, **TERCERA_ONDULADO)
    f = n.comprobar_curva_vertical(base, ctx, pe, ps, longitud, "PVI 2")
    assert f["resultado"] == esperado
    assert f["valor_exigido"] == exigido and f["referencia"]["id"] == tabla
    assert abs(f["detalle"]["valor_formula"] - formula) <= 0.1
    assert f["detalle"]["formula"]["id"] == formula_id and f["detalle"]["distancia_visibilidad_m"] == 85


def test_curva_vertical_fuera_de_tercera_clase_usa_la_formula(base):
    ctx = contexto(base, "rasante", velocidad_diseno=60, clasificacion=SEGUNDA, orografia="Ondulado")
    f = n.comprobar_curva_vertical(base, ctx, 4, -3, 120)
    assert f["resultado"] == n.NO_CUMPLE and f["referencia"]["id"] == "F26"
    assert f["detalle"]["distancia_de"].startswith("T205.01")
    assert n.comprobar_curva_vertical(base, ctx, 4, -3, 126)["resultado"] == n.CUMPLE


def test_formulas_de_curva_vertical():
    assert abs(n.convexa_f26(7, 85) - 125.11) < 0.01
    assert n.longitud_convexa(1, 85)[1] == "F27", "con A pequeño la curva es más corta que D"
    assert abs(n.concava_f30(8, 85) - 138.44) < 0.01
    assert n.longitud_concava(1, 85)[1] == "F31"
    assert n.longitud_convexa(0, 85) == (None, None)


# ---------------------------------------------------------------------------------------------- datos del proyecto
def test_sin_datos_pregunta_todo_con_los_valores_validos(base):
    ctx, faltan = n.resolver_proyecto(base, "alineamiento", {})
    assert ctx is None
    datos = {f["dato"]: f for f in faltan}
    assert set(datos) == {"clasificacion", "orografia", "velocidad_diseno"}
    assert TERCERA in datos["clasificacion"]["valores_validos"]
    assert datos["orografia"]["valores_validos"] == ["Plano", "Ondulado", "Accidentado"]


def test_velocidad_que_no_es_celda_de_la_t20401_se_pregunta(base):
    _, faltan = n.resolver_proyecto(base, "alineamiento", {**TERCERA_ONDULADO, "velocidad_diseno": 65})
    assert [f["dato"] for f in faltan] == ["velocidad_diseno"]
    assert faltan[0]["valores_validos"] == [40, 50, 60, 70, 80] and "interpolar" in faltan[0]["motivo"]


def test_tercera_clase_pregunta_pavimentada_y_peralte(base):
    _, faltan = n.resolver_proyecto(base, "alineamiento", {"clasificacion": TERCERA, "orografia": "Ondulado",
                                                           "ubicacion": "rural", "velocidad_diseno": 60})
    datos = {f["dato"]: f for f in faltan}
    assert set(datos) == {"pavimentada", "peralte_maximo"}
    assert datos["pavimentada"]["valores_validos"] == [True, False]
    assert datos["peralte_maximo"]["valores_validos"] == [6, 8, 12] and "pavimentada" in datos["peralte_maximo"]["motivo"]


def test_alineamiento_pregunta_la_ubicacion_y_rasante_no_si_coincide(base):
    datos = {"clasificacion": SEGUNDA, "orografia": "Ondulado", "velocidad_diseno": 60}
    _, faltan = n.resolver_proyecto(base, "alineamiento", datos)
    assert [f["dato"] for f in faltan] == ["ubicacion"] and "rural" in faltan[0]["valores_validos"]
    ctx, faltan = n.resolver_proyecto(base, "rasante", datos)
    assert faltan == [] and ctx["clase"] == "Segunda clase"


def test_accidentado_pregunta_el_peralte_8_o_12(base):
    datos = {"clasificacion": SEGUNDA, "orografia": "Accidentado", "ubicacion": "rural", "velocidad_diseno": 60}
    _, faltan = n.resolver_proyecto(base, "alineamiento", datos)
    assert [f["dato"] for f in faltan] == ["peralte_maximo"] and faltan[0]["valores_validos"] == [8, 12]
    _, faltan = n.resolver_proyecto(base, "alineamiento", {**datos, "peralte_maximo": 10})
    assert faltan[0]["dato"] == "peralte_maximo" and "no es un valor" in faltan[0]["motivo"]


def test_rasante_accidentada_pregunta_la_altitud(base):
    datos = {"clasificacion": SEGUNDA, "orografia": "Accidentado", "velocidad_diseno": 60}
    _, faltan = n.resolver_proyecto(base, "rasante", datos)
    assert [f["dato"] for f in faltan] == ["altitud_msnm"]
    assert n.resolver_proyecto(base, "rasante", {**datos, "cotas_msnm": True})[1] == []


def test_valores_invalidos_y_mayusculas(base):
    _, faltan = n.resolver_proyecto(base, "rasante", {"clasificacion": "Carretera de cuarta clase", "orografia": "Llano",
                                                      "velocidad_diseno": 60})
    assert {f["dato"] for f in faltan} == {"clasificacion", "orografia"}
    ctx = contexto(base, "rasante", clasificacion="CARRETERA DE SEGUNDA CLASE", orografia="ondulado", velocidad_diseno=60)
    assert ctx["clasificacion"] == SEGUNDA and ctx["orografia"] == "Ondulado"


def test_tercera_pavimentada_se_verifica_como_segunda(base):
    ctx = contexto(base, velocidad_diseno=60, **{**TERCERA_ONDULADO, "pavimentada": True})
    assert ctx["aplica_101_05"] and ctx["clase"] == "Segunda clase" and not ctx["tercera_clase"]
    assert any("Segunda Clase" in a and "R101.05-1" in a for a in ctx["avisos"])
    ex = n.radio_minimo_exigido(base, ctx)
    assert ex["referencia"]["id"] == "T302.02" and ex["otra_tabla"] is None, "sin la T302.04 de Tercera Clase"
    assert n.comprobar_transicion(base, ctx, 240)["referencia"]["id"] == "T302.11A"
    f = n.comprobar_curva_obligatoria(base, ctx, 1.5, False)
    assert f["resultado"] == n.NO_CUMPLE and f["referencia"]["id"] == "R303.04.01-1"


# ---------------------------------------------------------------------------------------------- planta
def test_longitud_de_curva_con_deflexion_pequena_es_estricta(base):
    ctx = contexto(base, velocidad_diseno=60, **TERCERA_ONDULADO)
    exacta = n.comprobar_longitud_curva(base, ctx, 4.0, 180.0)
    assert exacta[0]["resultado"] == n.NO_CUMPLE and exacta[0]["criterio"] == n.MAYOR_ESTRICTO
    assert n.comprobar_longitud_curva(base, ctx, 4.0, 180.5)[0]["resultado"] == n.CUMPLE
    assert "≤ 5°" in n.comprobar_longitud_curva(base, ctx, 5.0, 200.0)[0]["detalle"]["nota"]
    assert len(n.comprobar_longitud_curva(base, ctx, 20.0, 200.0)) == 1, "sin la regla de Δ < 5°"


def test_longitud_minima_3v_solo_avisa_y_800_m(base):
    ctx = contexto(base, velocidad_diseno=60, **TERCERA_ONDULADO)
    f = n.comprobar_longitud_curva(base, ctx, 30.0, 150.0)[0]
    assert f["resultado"] == n.AVISO and f["valor_exigido"] == 180 and f["referencia"]["id"] == "C302.02-LMIN"
    filas = n.comprobar_longitud_curva(base, ctx, 60.0, 900.0)
    assert [x["resultado"] for x in filas] == [n.CUMPLE, n.AVISO] and filas[1]["referencia"]["id"] == "R302.02-7"
    autopista = contexto(base, velocidad_diseno=100, clasificacion="Autopista de primera clase", orografia="Plano",
                         ubicacion="rural")
    assert n.comprobar_longitud_curva(base, autopista, 30.0, 550.0)[0]["valor_exigido"] == 600


def test_pi_sin_curva_y_deflexion_minima(base):
    ctx = contexto(base, velocidad_diseno=60, **TERCERA_ONDULADO)
    assert n.comprobar_pi_sin_curva(base, ctx, 1.5)["resultado"] == n.CUMPLE
    assert n.comprobar_pi_sin_curva(base, ctx, 1.6)["resultado"] == n.NO_CUMPLE
    assert n.comprobar_deflexion_minima(base, ctx, 0.5)["resultado"] == n.AVISO
    assert n.comprobar_deflexion_minima(base, ctx, 1.0) is None
    autopista = contexto(base, velocidad_diseno=100, clasificacion="Autopista de primera clase", orografia="Plano",
                         ubicacion="rural")
    assert n.comprobar_pi_sin_curva(base, autopista, 0.5)["resultado"] == n.SIN_VALOR


def test_tangentes_en_s_y_del_mismo_sentido(base):
    ctx = contexto(base, velocidad_diseno=60, **TERCERA_ONDULADO)
    s = n.comprobar_tangente_minima(base, ctx, 83.2, mismo_sentido=False)
    assert len(s) == 1 and s[0]["valor_exigido"] == 83 and s[0]["resultado"] == n.CUMPLE_AVISO, "1,39 V = 83,4"
    assert n.comprobar_tangente_minima(base, ctx, 90, mismo_sentido=False)[0]["resultado"] == n.CUMPLE
    o = n.comprobar_tangente_minima(base, ctx, 170, mismo_sentido=True)
    assert o[0]["valor_exigido"] == 167 and o[0]["referencia"]["id"] == "T302.01"
    assert o[0]["detalle"]["otra_tabla"]["valor"] == 85 and "más exigente" in o[0]["detalle"]["aviso"]
    assert o[0]["resultado"] == n.CUMPLE_AVISO
    assert [x["referencia"]["id"] for x in o[1:]] == ["R302.02-8", "R302.06.03-1"]
    assert all(x["resultado"] == n.AVISO for x in o[1:])
    assert n.comprobar_tangente_maxima(base, ctx, 1100)["resultado"] == n.AVISO
    assert n.comprobar_tangente_maxima(base, ctx, 900)["resultado"] == n.CUMPLE


def test_relacion_de_radios(base):
    ctx = contexto(base, velocidad_diseno=60, **TERCERA_ONDULADO)
    dentro = n.comprobar_relacion_radios(base, ctx, 60, 80)
    assert dentro["resultado"] == n.CUMPLE and dentro["valor_exigido"] == {"minimo": 50, "maximo": 90}
    assert dentro["referencia"]["id"] == "T302.08"
    assert n.comprobar_relacion_radios(base, ctx, 60, 95)["resultado"] == n.NO_CUMPLE
    entre = n.comprobar_relacion_radios(base, ctx, 75, 106)
    assert entre["resultado"] == n.NO_CUMPLE and entre["valor_exigido"] == {"minimo": 53, "maximo": 105}
    assert n.comprobar_relacion_radios(base, ctx, 75, 100)["resultado"] == n.CUMPLE_AVISO
    assert n.comprobar_relacion_radios(base, ctx, 30, 40)["resultado"] == n.SIN_VALOR
    sin_tope = n.comprobar_relacion_radios(base, ctx, 680, 5000)
    assert sin_tope["resultado"] == n.CUMPLE and sin_tope["valor_exigido"]["maximo"] is None
    grupo1 = dict(ctx, grupo_relacion_radios=1)
    errata = n.comprobar_relacion_radios(base, grupo1, 880, 512)
    assert errata["resultado"] == n.NO_CUMPLE and errata["valor_exigido"]["minimo"] == 517
    assert "errata" in errata["detalle"]["aviso"]


def test_transicion_y_curva_compuesta(base):
    tercera = contexto(base, velocidad_diseno=60, **TERCERA_ONDULADO)
    igual = n.comprobar_transicion(base, tercera, 210)
    assert igual["resultado"] == n.NO_CUMPLE and igual["referencia"]["id"] == "T302.11B", "en la B igual no basta"
    assert "transición" in igual["detalle"]["nota"]
    assert n.comprobar_transicion(base, tercera, 211)["resultado"] == n.CUMPLE
    segunda = contexto(base, velocidad_diseno=60, clasificacion=SEGUNDA, orografia="Ondulado", ubicacion="rural")
    assert n.comprobar_transicion(base, segunda, 250)["resultado"] == n.CUMPLE, "en la A igual basta"
    assert n.comprobar_curva_compuesta(base, tercera, 100, 150)["resultado"] == n.CUMPLE_AVISO
    assert n.comprobar_curva_compuesta(base, tercera, 100, 151)["resultado"] == n.NO_CUMPLE


def test_autopista_radio_tras_recta_larga(base):
    ctx = contexto(base, velocidad_diseno=100, clasificacion="Autopista de primera clase", orografia="Plano",
                   ubicacion="rural")
    assert n.comprobar_radio_tras_recta_larga(base, ctx, 450, 650)["resultado"] == n.NO_CUMPLE
    assert n.comprobar_radio_tras_recta_larga(base, ctx, 450, 700)["resultado"] == n.CUMPLE
    assert n.comprobar_radio_tras_recta_larga(base, ctx, 400, 650) is None


# ---------------------------------------------------------------------------------------------- perfil
def test_curva_vertical_obligatoria_segun_el_pavimento(base):
    sin_pavimento = contexto(base, "rasante", velocidad_diseno=60, **TERCERA_ONDULADO)
    assert n.comprobar_curva_obligatoria(base, sin_pavimento, 2.0, False)["resultado"] == n.CUMPLE, "2 % justo no"
    f = n.comprobar_curva_obligatoria(base, sin_pavimento, 2.1, False)
    assert f["resultado"] == n.NO_CUMPLE and f["referencia"]["id"] == "R303.04.01-2"
    segunda = contexto(base, "rasante", velocidad_diseno=60, clasificacion=SEGUNDA, orografia="Ondulado")
    assert n.comprobar_curva_obligatoria(base, segunda, 1.0, False)["resultado"] == n.CUMPLE
    assert n.comprobar_curva_obligatoria(base, segunda, 1.2, True)["resultado"] == n.CUMPLE


def test_pendiente_minima_avisa(base):
    ctx = contexto(base, "rasante", velocidad_diseno=60, clasificacion=SEGUNDA, orografia="Ondulado")
    assert n.comprobar_pendiente_minima(base, ctx, 0.3)["resultado"] == n.AVISO
    assert n.comprobar_pendiente_minima(base, ctx, -0.5) is None


def pvi(numero, progresiva, cota, pe=None, ps=None, curva=None):
    a = abs(ps - pe) if pe is not None and ps is not None else None
    return {"n": numero, "progresiva": progresiva, "cota": cota, "pe_pct": pe, "ps_pct": ps, "a_pct": a,
            "tiene_curva": curva is not None, "tipo_curva": "SymmetricParabola" if curva else None,
            "longitud_curva": curva}


def test_reglas_de_tercera_clase_en_el_perfil(base):
    ctx = contexto(base, "rasante", velocidad_diseno=60, **TERCERA_ONDULADO)
    pvis = [pvi(1, 0, 100, None, 11), pvi(2, 100, 111, 11, 12), pvi(3, 200, 123, 12, 3), pvi(4, 2500, 192, 3, None)]
    tramos = n._tramos(pvis)
    largos = n.comprobar_tramos_mas_de_10(base, ctx, tramos)
    assert len(largos) == 1 and largos[0]["valor_diseno"] == 200 and largos[0]["resultado"] == n.NO_CUMPLE
    media = n.comprobar_pendiente_media(base, ctx, pvis)
    assert media["resultado"] == n.CUMPLE and media["referencia"]["id"] == "R303.03.03-4"
    empinada = [pvi(1, 0, 100, None, 6.3), pvi(2, 3500, 320.5, 6.3, None)]
    assert n.comprobar_pendiente_media(base, ctx, empinada)["resultado"] == n.NO_CUMPLE
    descansos = n.comprobar_descansos(base, ctx, n._tramos(empinada))
    assert len(descansos) == 1 and descansos[0]["resultado"] == n.AVISO
    pis = [{"numero": 1, "tipo": "curva", "radio": 40, "progresiva_pc": 50, "progresiva_pt": 90}]
    cerradas = n.comprobar_pendiente_en_curvas_cerradas(base, ctx, tramos, pis)
    assert len(cerradas) == 1 and cerradas[0]["resultado"] == n.AVISO


def test_lomo_quebrado(base):
    ctx = contexto(base, "rasante", velocidad_diseno=60, clasificacion=SEGUNDA, orografia="Ondulado")
    pvis = [pvi(1, 0, 100, None, 4), pvi(2, 200, 108, 4, 1), pvi(3, 300, 109, 1, -2), pvi(4, 500, 105, -2, None)]
    filas = n.comprobar_lomo_quebrado(base, ctx, pvis)
    assert len(filas) == 1 and filas[0]["resultado"] == n.AVISO and filas[0]["referencia"]["seccion"] == "303.02"


# ---------------------------------------------------------------------------------------------- utilidades
def test_progresiva_y_resumen():
    assert n.progresiva_texto(300) == "0+300.000" and n.progresiva_texto(1234.5) == "1+234.500"
    filas = [{"resultado": n.CUMPLE}, {"resultado": n.NO_CUMPLE}, {"resultado": n.CUMPLE}]
    resumen = n.resumir(filas)
    assert resumen[n.CUMPLE] == 2 and resumen[n.NO_CUMPLE] == 1 and set(resumen) == set(n.RESULTADOS)


def test_errores_de_la_memoria(tmp_path, monkeypatch):
    monkeypatch.delenv("MEMORIA_FIRMA_DIR", raising=False)
    with pytest.raises(n.ErrorNorma, match="MEMORIA_FIRMA_DIR"):
        n.localizar_base()
    vacia = tmp_path / "vacia"
    vacia.mkdir()
    with pytest.raises(n.ErrorNorma, match="Falta la base de la norma"):
        n.localizar_base(str(vacia))
    crear_base(str(tmp_path / "rota"), sin_tablas=("indice_k", "reglas"))
    with pytest.raises(n.ErrorNorma, match="no tiene las tablas: indice_k, reglas"):
        n.BaseNorma(n.localizar_base(str(tmp_path / "rota")))


def test_el_codigo_solo_cita_referencias_de_la_base(base):
    assert [i for i in n.REFERENCIAS_USADAS if base.referencia(i).get("tipo") is None] == []


# ---------------------------------------------------------------------------------------------- de punta a punta
def tabla_pis(unidades="Meters"):
    """Eje a 40 km/h: dos curvas a la izquierda con 30 m de tangente, un PI sin curva, una curva cerrada de deflexión
    pequeña y una espiral (tipo no soportado)."""
    return {
        "alineamiento": "Eje 1", "unidades": unidades, "longitud": 1200.0,
        "inicio": {"progresiva": 0.0, "progresiva_texto": "0+000.000"},
        "fin": {"progresiva": 1200.0, "progresiva_texto": "1+200.000"},
        "numero_pi": 5, "numero_curvas": 3, "avisos": [],
        "pis": [
            {"numero": 1, "tipo": "curva", "tiene_curva": True, "progresiva": 116.08, "progresiva_texto": "0+116.080",
             "deflexion_grados": 30.0, "giro": "izquierda", "radio": 60.0, "longitud_curva": 31.416,
             "progresiva_pc": 100.0, "progresiva_pt": 131.416},
            {"numero": 2, "tipo": "curva", "tiene_curva": True, "progresiva": 175.5, "progresiva_texto": "0+175.500",
             "deflexion_grados": 20.0, "giro": "izquierda", "radio": 80.0, "longitud_curva": 27.925,
             "progresiva_pc": 161.416, "progresiva_pt": 189.341},
            {"numero": 3, "tipo": "sin_curva", "tiene_curva": False, "progresiva": 400.0, "progresiva_texto": "0+400.000",
             "deflexion_grados": 2.0, "giro": "derecha", "radio": None, "longitud_curva": None,
             "progresiva_pc": None, "progresiva_pt": None},
            {"numero": 4, "tipo": "curva", "tiene_curva": True, "progresiva": 601.57, "progresiva_texto": "0+601.570",
             "deflexion_grados": 4.0, "giro": "derecha", "radio": 45.0, "longitud_curva": 3.142,
             "progresiva_pc": 600.0, "progresiva_pt": 603.142},
            {"numero": 5, "tipo": "AlignmentSCS", "tiene_curva": False, "progresiva": None, "progresiva_texto": None,
             "deflexion_grados": None, "giro": None, "radio": None, "longitud_curva": None,
             "progresiva_pc": None, "progresiva_pt": None, "aviso": "tipo no soportado todavía"},
        ],
    }


def respuesta(resultado):
    return {"ok": True, "tool": "x", "id_log": "20261117-100000-abcdef", "ms": 5, "result": resultado}


def llamar(puente, **kwargs):
    return json.loads(ejecutar(puente.verificar_norma(**kwargs)))


def test_verificar_un_eje(servidor, puente, memoria):
    servidor.respuestas["listar_pis"] = respuesta(tabla_pis())
    r = llamar(puente, norma="DG-2018", tipo_elemento="alineamiento", nombre="Eje 1", velocidad_diseno=40,
               pedido="Verifica el Eje 1 con la norma", **TERCERA_ONDULADO)
    assert r["ok"] and r["verificado"] and r["norma"] == "DG-2018" and r["alineamiento"] == "Eje 1"
    assert [c["tool"] for c in servidor.llamadas] == ["listar_pis"]
    assert servidor.llamadas[0]["args"] == {"alineamiento": "Eje 1", "pedido": "Verifica el Eje 1 con la norma"}
    assert r["lecturas"] == {"listar_pis": "20261117-100000-abcdef"} and r["ms_puente"] >= 0
    filas = r["filas"]

    def buscar(elemento, comprobacion):
        return [f for f in filas if f["elemento"] == elemento and f["comprobacion"].startswith(comprobacion)]

    elementos = [f["elemento"] for f in filas]
    assert elementos[0] == "Proyecto", "primero la velocidad, luego en el orden del eje"
    assert elementos.index("Tangente inicio–PI 1") < elementos.index("Curva PI 1") < elementos.index("Tangente PI 1–PI 2")
    assert elementos.index("Tangente PI 1–PI 2") < elementos.index("Curva PI 2") < elementos.index("PI 3 (sin curva)")
    radio = buscar("Curva PI 4", "Radio mínimo")[0]
    assert radio["resultado"] == "No cumple" and radio["valor_diseno"] == 45 and radio["valor_exigido"] == 50
    assert radio["referencia"]["id"] == "T302.02" and radio["progresiva"] == "0+601.570"
    assert buscar("Curva PI 4", "Longitud de curva con deflexión")[0]["resultado"] == "No cumple"
    assert buscar("Curva PI 1", "Radio mínimo")[0]["resultado"] == "Cumple"
    assert buscar("PI 3 (sin curva)", "Deflexión máxima")[0]["resultado"] == "Cumple"
    tangente = buscar("Tangente PI 1–PI 2", "Tangente mínima entre curvas del mismo sentido")[0]
    assert tangente["resultado"] == "No cumple" and tangente["valor_diseno"] == 30 and tangente["valor_exigido"] == 111
    relaciones = [f for f in filas if f["comprobacion"] == "Relación entre radios consecutivos"]
    assert [f["elemento"] for f in relaciones] == ["PI 1 → PI 2", "PI 2 → PI 1"]
    assert all(f["resultado"] == "Cumple" for f in relaciones)
    assert buscar("PI 5 (AlignmentSCS)", "Tipo")[0]["resultado"] == "No verificable"
    assert buscar("Tangente PI 4–PI 5", "Tangente")[0]["resultado"] == "No verificable"
    assert any(x["que"] == "PI 5 (AlignmentSCS)" for x in r["no_comprobado"])
    assert any("Peralte de diseño" in x["que"] for x in r["no_comprobado"])
    assert sum(r["resumen"].values()) == len(filas) and r["resumen"]["No cumple"] >= 3
    assert r["tabla"].startswith("| Elemento |") and "T302.02 (302.04.02, p. 129; PDF 130)" in r["tabla"]
    assert r["proyecto"]["clase"] == "Tercera clase" and r["base"]["metadatos"]["script_version"] == "ejemplo"


def test_verificar_una_rasante(servidor, puente, memoria):
    servidor.respuestas["listar_pis"] = respuesta(tabla_pis())
    servidor.respuestas["listar_pvis"] = respuesta([
        pvi(1, 0, 100, None, 4.0), pvi(2, 200, 108, 4.0, -3.0, 80), pvi(3, 400, 102, -3.0, 1.5, 60),
        pvi(4, 600, 105, 1.5, 0.2), pvi(5, 800, 105.4, 0.2, None)])
    r = llamar(puente, norma="dg-2018", tipo_elemento="Rasante", nombre="Rasante 1", alineamiento="Eje 1",
               velocidad_diseno=60, **TERCERA_ONDULADO)
    assert r["ok"] and r["verificado"] and r["nombre"] == "Rasante 1"
    assert [c["tool"] for c in servidor.llamadas] == ["listar_pis", "listar_pvis"]
    assert servidor.llamadas[1]["args"] == {"alineamiento": "Eje 1", "perfil": "Rasante 1"}
    por_elemento = {}
    for f in r["filas"]:
        por_elemento.setdefault((f["elemento"], f["comprobacion"]), f)
    convexa = por_elemento[("PVI 2", "Longitud de curva vertical convexa (visibilidad de parada)")]
    assert convexa["resultado"] == "Cumple con aviso" and convexa["valor_exigido"] == 77
    concava = por_elemento[("PVI 3", "Longitud de curva vertical cóncava (visibilidad de parada)")]
    assert concava["resultado"] == "No cumple" and concava["valor_exigido"] == 81 and concava["referencia"]["id"] == "T303.03"
    assert por_elemento[("PVI 4", "Curva vertical obligatoria si A > 2 %")]["resultado"] == "Cumple"
    assert por_elemento[("Tramo PVI 4–PVI 5", "Pendiente mínima del orden de 0,5 %")]["resultado"] == "Aviso"
    assert por_elemento[("Tramo PVI 1–PVI 2", "Pendiente máxima")]["resultado"] == "Cumple"
    assert any("visibilidad de paso" in x["que"].lower() for x in r["no_comprobado"])


def test_sin_un_dato_obligatorio_no_lee_el_dibujo(servidor, puente, memoria):
    servidor.respuestas["listar_pis"] = respuesta(tabla_pis())
    r = llamar(puente, norma="DG-2018", tipo_elemento="alineamiento", nombre="Eje 1", clasificacion=TERCERA,
               orografia="Ondulado", ubicacion="rural", velocidad_diseno=60)
    assert r["ok"] is False and r["verificado"] is False and "no los supongas" in r["error"]
    assert {f["dato"] for f in r["faltan"]} == {"pavimentada", "peralte_maximo"}
    assert servidor.llamadas == [], "no debe llamar al plugin"


def test_unidades_que_no_son_metros(servidor, puente, memoria):
    servidor.respuestas["listar_pis"] = respuesta(tabla_pis("Feet"))
    r = llamar(puente, norma="DG-2018", tipo_elemento="rasante", nombre="Rasante 1", alineamiento="Eje 1",
               velocidad_diseno=60, **TERCERA_ONDULADO)
    assert r["ok"] and r["verificado"] is False and "Feet" in r["motivo"]
    assert [c["tool"] for c in servidor.llamadas] == ["listar_pis"], "no lee la rasante"


def test_errores_de_la_llamada(servidor, puente, memoria, monkeypatch):
    datos = dict(velocidad_diseno=60, **TERCERA_ONDULADO)
    r = llamar(puente, norma="AASHTO", tipo_elemento="alineamiento", nombre="Eje 1", **datos)
    assert r["ok"] is False and "DG-2018" in r["error"]
    r = llamar(puente, norma="DG-2018", tipo_elemento="perfil", nombre="Eje 1", **datos)
    assert r["ok"] is False and "tipo_elemento" in r["error"]
    r = llamar(puente, norma="DG-2018", tipo_elemento="rasante", nombre="Rasante 1", **datos)
    assert r["ok"] is False and "alineamiento" in r["error"]
    servidor.respuestas["listar_pis"] = {"ok": False, "error": "No existe el alineamiento 'Eje 9'. Usa listar_alineamientos.",
                                         "id_log": "20261117-100001-000001"}
    r = llamar(puente, norma="DG-2018", tipo_elemento="alineamiento", nombre="Eje 9", **datos)
    assert r["ok"] is False and "Usa listar_alineamientos" in r["error"] and r["id_log"] == "20261117-100001-000001"
    monkeypatch.setenv("MEMORIA_FIRMA_DIR", str(memoria / "no-existe"))
    r = llamar(puente, norma="DG-2018", tipo_elemento="alineamiento", nombre="Eje 1", **datos)
    assert r["ok"] is False and "no existe" in r["error"]
    os.remove(os.path.join(str(memoria), "normas", "tablas", "dg2018.sqlite"))
    monkeypatch.setenv("MEMORIA_FIRMA_DIR", str(memoria))
    r = llamar(puente, norma="DG-2018", tipo_elemento="alineamiento", nombre="Eje 1", **datos)
    assert r["ok"] is False and "Falta la base de la norma" in r["error"]
    assert len(servidor.llamadas) == 1, "solo la llamada del eje que no existe"


def test_se_registra_como_herramienta_local(puente):
    assert "verificar_norma" in puente.HERRAMIENTAS_LOCALES
    herramienta = puente.mcp._tool_manager.get_tool("verificar_norma")
    propiedades = herramienta.parameters["properties"]
    assert {"norma", "tipo_elemento", "nombre", "velocidad_diseno", "pavimentada", "cotas_msnm"} <= set(propiedades)
    assert herramienta.parameters["required"] == ["norma", "tipo_elemento", "nombre"], "argumentos directos"


# ---------------------------------------------------------------------------------------------- script de casos reales
YAML_DE_EJEMPLO = """
version: 0
casos:
  - id: 1
    clave: radio_ejemplo
    entrada: {clasificacion: Carretera de Tercera Clase, orografia: 2, zona: rural, pavimentada: false,
              peralte_maximo_pct: 8}
    filas:
      - velocidad_kmh: 40
        fmax: 0.17
        exigido: {valor: 50, unidad: m, criterio: minimo}
        valor_calculado_en_tabla: 50.4
        formula: {valor: 50.39, unidad: m}
        ejemplos:
          - {valor_diseno: 49, resultado: No cumple}
          - {valor_diseno: 50, resultado: Cumple, aviso: true}
  - id: 2
    clave: fuera_de_alcance
    entrada: {}
    filas:
      - {radio_m: 100, exigido: {valor: 6, unidad: '%'}}
  - id: 4
    clave: pendiente_ejemplo
    entrada: {clasificacion: Carretera de Segunda Clase, orografia: 3, zona: rural, velocidad_kmh: 60}
    filas:
      - fila: 9z
        altitud_msnm: 3500
        exigido: {valor: 7.0, unidad: '%', criterio: maximo}
        maximo_excepcional_pct: 8.0
        ejemplos:
          - {valor_diseno: 7.0, resultado: Cumple}
  - id: 5
    clave: vertical_ejemplo
    entrada: {clasificacion: Carretera de Tercera Clase, orografia: 2, pavimentada: false, velocidad_kmh: 60,
              distancia_parada_m: 85}
    filas:
      - tipo: convexa
        pendiente_entrada_pct: 4.0
        pendiente_salida_pct: -3.0
        diferencia_algebraica_pct: 7.0
        k_tabla: 11
        exigido: {valor: 77, unidad: m, criterio: minimo}
        formula: {valor: 125.2, tolerancia: 0.1}
        ejemplos:
          - {valor_diseno: 80, resultado: Cumple, aviso: true}
"""


def test_script_de_casos_reales_con_un_yaml_de_ejemplo(memoria):
    carpeta = memoria / "normas" / "casos_prueba"
    carpeta.mkdir(parents=True)
    (carpeta / "casos_dg2018.yaml").write_text(YAML_DE_EJEMPLO, encoding="utf-8")
    salida = io.StringIO()
    codigo = casos_norma_reales.main(["--memoria", str(memoria)], salida)
    texto = salida.getvalue()
    assert codigo == 0, texto
    assert "FALLO" not in texto and "fuera de alcance" in texto
    assert "0 fallos" in texto and "1 casos fuera de alcance" in texto
    (carpeta / "casos_dg2018.yaml").write_text(YAML_DE_EJEMPLO.replace("resultado: No cumple", "resultado: Cumple"),
                                               encoding="utf-8")
    salida = io.StringIO()
    assert casos_norma_reales.main(["--memoria", str(memoria)], salida) == 1
    assert "[FALLO] 40 km/h · R = 49 m" in salida.getvalue()


def test_script_sin_memoria(tmp_path, monkeypatch):
    monkeypatch.delenv("MEMORIA_FIRMA_DIR", raising=False)
    salida = io.StringIO()
    assert casos_norma_reales.main([], salida) == 2 and "MEMORIA_FIRMA_DIR" in salida.getvalue()


def test_tolerancia_de_los_casos():
    numero = casos_norma_reales.Numero(125.2)
    numero.texto = "125.2"
    assert casos_norma_reales.tolerancia(numero) == pytest.approx(0.05)
    assert casos_norma_reales.tolerancia(numero, 0.1) == 0.1
    assert casos_norma_reales.evaluar("R*math.tan(math.radians(Delta)/2)", {"R": 100, "Delta": 90}) == pytest.approx(100)
    with pytest.raises(ValueError):
        casos_norma_reales.evaluar("__import__('os')", {})
    assert math.isclose(casos_norma_reales.evaluar("abs(S1 - S2)", {"S1": 4, "S2": -3}), 7)

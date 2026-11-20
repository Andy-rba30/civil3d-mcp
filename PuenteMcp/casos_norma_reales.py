# -*- coding: utf-8 -*-
"""Corre los casos de prueba de la DG-2018 contra la base real con las mismas funciones que usa verificar_norma, y dice
caso a caso lo esperado y lo obtenido. Los casos y la base están en el repositorio privado memoria-firma; aquí no hay
ninguno.

Uso, desde PuenteMcp (con MEMORIA_FIRMA_DIR apuntando a memoria-firma, o con --memoria):

    uv run --frozen python casos_norma_reales.py
    uv run --frozen python casos_norma_reales.py --memoria C:\\Proyectos\\memoria-firma

Lee normas/casos_prueba/casos_dg2018.yaml y normas/tablas/dg2018.sqlite. Además comprueba que las fórmulas del código
dan lo mismo que `formulas.expresion_python` de la base, que los umbrales de las reglas coinciden con `reglas.valor_num`
y que todas las referencias que cita el código existen en la base. Desde 1.20.0, si la base trae `peralte_figura` (las
Figuras 302.02 a 302.05 digitalizadas, tarea 6.10), compara la lectura de la figura de aplicar_peralte con los valores
de control de la orquestadora (CASOS_PERALTE_FIGURA); si no la trae, los salta y lo dice. Desde 1.21.0 también comprueba
el sobreancho (norma_sobreancho.py: la F46, el mínimo de 0,40 m de la R302.09.03-2 y sus referencias) y, desde la
tarea 7.5, el sobreancho aplicado sin y con la reducción de la T302.20 (P5, opción F) frente a los valores de control del
dueño (CASOS_SOBREANCHO_P5; si la base no tiene las filas que necesitan, los salta y lo dice) y que no sube al crecer el
radio.

Sale con 0 si todo coincide, 1 si algo no coincide y 2 si falta la memoria, la base o el archivo de casos.
"""
import argparse
import ast
import math
import operator
import os
import sys

import yaml

import norma
import norma_peralte
import norma_sobreancho

RUTA_CASOS = os.path.join("normas", "casos_prueba", "casos_dg2018.yaml")


# ---------------------------------------------------------------------------------------------- YAML con decimales
class Numero(float):
    """Un número del YAML que recuerda cómo estaba escrito (para la tolerancia de media unidad del último decimal)."""
    texto = None


class _Cargador(yaml.SafeLoader):
    pass


def _numero(cargador, nodo):
    valor = Numero(cargador.construct_yaml_float(nodo))
    valor.texto = nodo.value
    return valor


_Cargador.add_constructor("tag:yaml.org,2002:float", _numero)


def tolerancia(valor, dada=None) -> float:
    """La tolerancia del caso si la trae; si no, media unidad del último decimal escrito (125.2 → 0.05)."""
    if dada is not None:
        return float(dada)
    texto = getattr(valor, "texto", None) or repr(valor)
    if "." in texto:
        decimales = len(texto.split(".", 1)[1].rstrip())
        return 0.5 * 10 ** (-decimales)
    return 0.5


# ---------------------------------------------------------------------------------------------- informe
class Informe:
    def __init__(self, salida=None):
        self.salida = salida or sys.stdout
        self.ok = 0
        self.fallos = 0
        self.fuera = 0
        self.sin_comparar = 0

    def linea(self, texto=""):
        print(texto, file=self.salida)

    def comparar(self, que: str, esperado, obtenido, tol: float = norma.TOLERANCIA, detalle: str = ""):
        if isinstance(esperado, str) or isinstance(obtenido, str) or esperado is None or obtenido is None:
            bien = esperado == obtenido
        else:
            bien = abs(float(esperado) - float(obtenido)) <= tol + 1e-12
        self.ok += bien
        self.fallos += not bien
        marca = "OK   " if bien else "FALLO"
        extra = f" (tolerancia {tol:g})" if not isinstance(esperado, str) and esperado is not None and tol != norma.TOLERANCIA else ""
        self.linea(f"  [{marca}] {que}: esperado {_ver(esperado)}, obtenido {_ver(obtenido)}{extra}"
                   + (f" · {detalle}" if detalle else ""))

    def sin_valor(self, que: str, motivo: str):
        self.sin_comparar += 1
        self.linea(f"  [??   ] {que}: {motivo}")


def _ver(valor):
    if isinstance(valor, float):
        return f"{round(valor, 4):g}"
    return str(valor)


# ---------------------------------------------------------------------------------------------- casos
def tipo_de_caso(caso: dict) -> str | None:
    """Qué comprobación de verificar_norma corresponde al caso, por lo que traen sus filas (None: fuera de alcance)."""
    filas = caso.get("filas") or []
    if any("paso_decision_6" in f for f in filas):
        return "peralte_figura"   # 1.20.0: peralte según el radio (Decisión 6), casos 10 a 12
    if any(str(f.get("tipo", "")).lower() in ("convexa", "concava", "cóncava") for f in filas):
        return "curva_vertical"
    if any("altitud_msnm" in f or "maximo_excepcional_pct" in f for f in filas):
        return "pendiente_maxima"
    if any("fmax" in f for f in filas) and all(((f.get("exigido") or {}).get("unidad") or "m") == "m" for f in filas):
        return "radio_minimo"
    return None


def esperado(ejemplo: dict) -> str:
    r = ejemplo.get("resultado")
    if r == "Cumple, con aviso":
        return norma.CUMPLE_AVISO
    if r == norma.CUMPLE and ejemplo.get("aviso"):
        return norma.CUMPLE_AVISO
    return r


def _datos(entrada: dict, fila: dict) -> dict:
    v = fila.get("velocidad_kmh", entrada.get("velocidad_kmh"))
    datos = {
        "clasificacion": entrada.get("clasificacion"),
        "orografia": None if entrada.get("orografia") is None else str(entrada.get("orografia")),
        "ubicacion": entrada.get("zona"),
        "pavimentada": entrada.get("pavimentada"),
        "peralte_maximo": entrada.get("peralte_maximo_pct"),
        "velocidad_diseno": v,
        "altitud_msnm": fila.get("altitud_msnm", entrada.get("altitud_msnm")),
    }
    return datos


def _nombre_fila(fila: dict) -> str:
    if fila.get("fila") is not None:
        return f"fila {fila['fila']}"
    if fila.get("tipo"):
        return f"{fila['tipo']}"
    return f"{fila.get('velocidad_kmh')} km/h"


def correr_caso(base: norma.BaseNorma, caso: dict, informe: Informe):
    titulo = f"Caso {caso.get('id')} ({caso.get('clave') or caso.get('titulo')})"
    tipo = tipo_de_caso(caso)
    if tipo is None:
        informe.fuera += 1
        informe.linea(f"{titulo}: fuera de alcance de verificar_norma (necesita un dato que no se lee del dibujo, "
                      "como el peralte de diseño o la visibilidad disponible).")
        return
    informe.linea(f"{titulo}: {tipo.replace('_', ' ')}")
    entrada = caso.get("entrada") or {}
    tipo_elemento = "alineamiento" if tipo in ("radio_minimo", "peralte_figura") else "rasante"
    if tipo == "peralte_figura" and not norma_peralte.tiene_tabla(base, norma_peralte.TABLA_FIGURA):
        informe.sin_valor(titulo, f"la base no tiene {norma_peralte.TABLA_FIGURA} (tarea 6.10)")
        return
    for f in caso.get("filas") or []:
        nombre = _nombre_fila(f)
        ctx, faltan = norma.resolver_proyecto(base, tipo_elemento, _datos(entrada, f))
        if faltan:
            informe.fallos += 1
            informe.linea(f"  [FALLO] {nombre}: verificar_norma pediría datos: "
                          + "; ".join(f"{x['dato']} ({x['motivo']})" for x in faltan))
            continue
        exigido = f.get("exigido") or {}
        formula = f.get("formula") or {}
        ejemplos = f.get("ejemplos") or []
        if tipo == "radio_minimo":
            ex = norma.radio_minimo_exigido(base, ctx)
            ref = norma.texto_referencia(ex["referencia"])
            informe.comparar(f"{nombre} · radio exigido", exigido.get("valor"), ex["exigido"], detalle=ref)
            if f.get("valor_calculado_en_tabla") is not None:
                informe.comparar(f"{nombre} · calculado en la tabla", f["valor_calculado_en_tabla"],
                                 ex.get("valor_calculado_en_tabla"), tolerancia(f["valor_calculado_en_tabla"]))
            if formula.get("valor") is not None:
                informe.comparar(f"{nombre} · fórmula {ex.get('formula')}", formula["valor"], ex.get("valor_formula"),
                                 tolerancia(formula["valor"], formula.get("tolerancia")))
            for e in ejemplos:
                r = norma.comprobar_radio_minimo(base, ctx, float(e["valor_diseno"]))
                informe.comparar(f"{nombre} · R = {_ver(float(e['valor_diseno']))} m", esperado(e), r["resultado"])
        elif tipo == "peralte_figura":
            correr_fila_peralte(base, ctx, entrada, f, nombre, informe)
        elif tipo == "pendiente_maxima":
            altitud = f.get("altitud_msnm", entrada.get("altitud_msnm"))
            ex = norma.pendiente_maxima_exigida(base, ctx, altitud)
            ref = norma.texto_referencia(ex["referencia"])
            informe.comparar(f"{nombre} · pendiente máxima ({_ver(altitud)} msnm)", exigido.get("valor"), ex["exigido"],
                             detalle=ref)
            if f.get("maximo_excepcional_pct") is not None:
                informe.comparar(f"{nombre} · máximo excepcional", f["maximo_excepcional_pct"], ex["excepcional"])
            for e in ejemplos:
                r = norma.comprobar_pendiente_maxima(base, ctx, float(e["valor_diseno"]), altitud)
                informe.comparar(f"{nombre} · pendiente {_ver(float(e['valor_diseno']))} %", esperado(e), r["resultado"])
        else:
            pe, ps = float(f["pendiente_entrada_pct"]), float(f["pendiente_salida_pct"])
            ex = norma.longitud_curva_vertical_exigida(base, ctx, pe, ps)
            ref = norma.texto_referencia(ex["referencia"])
            if f.get("diferencia_algebraica_pct") is not None:
                informe.comparar(f"{nombre} · A", f["diferencia_algebraica_pct"], ex["a_pct"])
            distancia = f.get("distancia_parada_m", entrada.get("distancia_parada_m"))
            if distancia is not None:
                informe.comparar(f"{nombre} · distancia de visibilidad de parada", distancia, ex["distancia"],
                                 detalle=ex["referencia_distancia"] or "")
            if f.get("k_tabla") is not None:
                informe.comparar(f"{nombre} · K de la tabla", f["k_tabla"], ex["k_tabla"], detalle=ref)
            informe.comparar(f"{nombre} · longitud exigida", exigido.get("valor"), ex["exigido"], detalle=ref)
            if formula.get("valor") is not None:
                informe.comparar(f"{nombre} · fórmula {ex.get('formula')}", formula["valor"], ex["valor_formula"],
                                 tolerancia(formula["valor"], formula.get("tolerancia")))
            for e in ejemplos:
                r = norma.comprobar_curva_vertical(base, ctx, pe, ps, float(e["valor_diseno"]))
                informe.comparar(f"{nombre} · L = {_ver(float(e['valor_diseno']))} m", esperado(e), r["resultado"])


def correr_fila_peralte(base: norma.BaseNorma, ctx: dict, entrada: dict, f: dict, nombre: str, informe: Informe):
    """Una fila de los casos 10 a 12 (peralte según el radio con la Decisión 6): figura, paso, lectura, peralte
    exigido y, con el paso 8, cada ejemplo."""
    condicion = norma_peralte.condicion_del_proyecto(base, ctx)
    ref = norma_peralte.figura_de_condicion(condicion, base)
    lectura = f.get("lectura") or {}
    if lectura.get("figura"):
        informe.comparar(f"{nombre} · figura", "F" + str(lectura["figura"]).lstrip("F"), ref)
    v, radio = ctx["velocidad"], float(f["radio_m"])
    t30404 = norma_peralte.t30404_del_proyecto(base, v)[0] if norma_peralte.tiene_tabla(base, "radio_sin_peralte") else None
    t30406 = norma_peralte.t30406_para(base, v, radio) if norma_peralte.tiene_tabla(base, "peralte_minimo") else None
    bombeo = entrada.get("bombeo_pct")
    r = norma_peralte.peralte_de_figura(base, ref, v, radio, norma.radio_minimo_exigido(base, ctx), t30404, t30406, bombeo)
    informe.comparar(f"{nombre} · paso de la decisión 6", f.get("paso_decision_6"), r["regla"])
    if lectura.get("valor_pct") is not None:
        informe.comparar(f"{nombre} · lectura de la figura", lectura["valor_pct"], r["peralte_calculado_pct"],
                         tolerancia(lectura["valor_pct"]))
    exigido = (f.get("exigido") or {}).get("valor")
    informe.comparar(f"{nombre} · peralte obtenido", exigido, r["peralte_pct"], 0.05, detalle=r["resultado"] or "")
    maximo = base.peralte_de_condicion(condicion)
    for e in f.get("ejemplos") or []:
        v_e = float(e["valor_diseno"])
        resultado = norma_peralte.verificar_peralte_diseno(v_e, r, maximo, bombeo)["resultado"]
        informe.comparar(f"{nombre} · p = {_ver(v_e)} %", esperado(e), resultado)


# ---------------------------------------------------------------------------------------------- fórmulas de la base
_OPERADORES = {ast.Add: operator.add, ast.Sub: operator.sub, ast.Mult: operator.mul, ast.Div: operator.truediv,
               ast.Pow: operator.pow}
_MATH = {"sqrt", "sin", "cos", "tan", "radians", "degrees", "pi", "atan", "asin", "acos", "exp", "log"}


def evaluar(expresion: str, variables: dict) -> float:
    """Evalúa una expresion_python de la base solo con aritmética, abs y math (sin eval)."""
    def ev(n):
        if isinstance(n, ast.Expression):
            return ev(n.body)
        if isinstance(n, ast.Constant) and isinstance(n.value, (int, float)) and not isinstance(n.value, bool):
            return n.value
        if isinstance(n, ast.Name):
            if n.id in variables:
                return variables[n.id]
            if n.id == "abs":
                return abs
            raise ValueError(f"variable desconocida: {n.id}")
        if isinstance(n, ast.BinOp) and type(n.op) in _OPERADORES:
            return _OPERADORES[type(n.op)](ev(n.left), ev(n.right))
        if isinstance(n, ast.UnaryOp) and isinstance(n.op, (ast.USub, ast.UAdd)):
            return -ev(n.operand) if isinstance(n.op, ast.USub) else ev(n.operand)
        if isinstance(n, ast.Attribute) and isinstance(n.value, ast.Name) and n.value.id == "math" and n.attr in _MATH:
            return getattr(math, n.attr)
        if isinstance(n, ast.Call) and not n.keywords:
            funcion = ev(n.func)
            if funcion is abs or getattr(math, getattr(funcion, "__name__", ""), None) is funcion:
                return funcion(*[ev(a) for a in n.args])
        raise ValueError(f"expresión no permitida: {ast.dump(n)[:60]}")
    return float(ev(ast.parse(expresion, mode="eval")))


MUESTRA = {"V": 60.0, "Pmax": 0.08, "fmax": 0.15, "emax": 8.0, "A": 7.0, "Dp": 85.0, "D": 85.0, "h1": norma.ALTURA_OJO_M,
           "h2": norma.ALTURA_OBJETO_M, "Delta": 3.0, "n": 2, "R": 60.0, "L": 10.55}
FORMULAS_CODIGO = {
    "F08": lambda m: norma.radio_minimo_formula(m["V"], m["Pmax"] * 100, m["fmax"]),
    "F09": lambda m: norma.radio_minimo_formula(m["V"], m["emax"], m["fmax"]),
    "F26": lambda m: norma.convexa_f26(m["A"], m["Dp"]),
    "F27": lambda m: norma.convexa_f27(m["A"], m["Dp"]),
    "F30": lambda m: norma.concava_f30(m["A"], m["D"]),
    "F31": lambda m: norma.concava_f31(m["A"], m["D"]),
    "F34": lambda m: norma.tangente_minima_s(m["V"]),
    "F35": lambda m: norma.tangente_minima_o(m["V"]),
    "F36": lambda m: norma.tangente_maxima(m["V"]),
    "F37": lambda m: norma.longitud_deflexion_pequena(m["Delta"]),
    "F46": lambda m: norma_sobreancho.sobreancho_formula(m["n"], m["R"], m["L"], m["V"])["sobreancho"],   # 1.21.0
}


def comprobar_formulas(base: norma.BaseNorma, informe: Informe):
    informe.linea("Fórmulas del código frente a formulas.expresion_python de la base (V 60, A 7, D 85, Δ 3, n 2, R 60, "
                  "L 10.55...):")
    for ident, codigo in FORMULAS_CODIGO.items():
        r = base.fila("SELECT expresion_python FROM formulas WHERE id = ?", ident)
        if not r or not r["expresion_python"]:
            informe.sin_valor(ident, "la base no trae expresion_python")
            continue
        try:
            de_la_base = evaluar(r["expresion_python"], MUESTRA)
        except Exception as e:  # variable desconocida o expresión con algo más que aritmética
            informe.sin_valor(ident, f"no se pudo evaluar '{r['expresion_python']}': {e}")
            continue
        informe.comparar(ident, de_la_base, codigo(MUESTRA), 1e-6 * max(1.0, abs(de_la_base)))


def comprobar_umbrales(base: norma.BaseNorma, informe: Informe):
    informe.linea("Umbrales del código frente a reglas.valor_num de la base:")
    for ident, umbral in {**norma.UMBRALES_REGLAS, **norma_sobreancho.UMBRALES_REGLAS}.items():
        r = base.fila("SELECT valor_num FROM reglas WHERE id = ?", ident)
        if r is None:
            informe.comparar(ident, "en la base", "no está en la base")
        elif r["valor_num"] is None:
            informe.sin_valor(ident, "la base no trae valor_num")
        else:
            informe.comparar(ident, abs(float(r["valor_num"])), abs(float(umbral)))


def comprobar_referencias(base: norma.BaseNorma, informe: Informe):
    for que, usadas in (("el código", norma.REFERENCIAS_USADAS), ("el sobreancho", norma_sobreancho.REFERENCIAS_USADAS)):
        faltan = [i for i in usadas if base.referencia(i).get("tipo") is None]
        informe.comparar(f"Referencias que cita {que} ({len(usadas)}) presentes en la base",
                         "todas", "todas" if not faltan else "faltan " + ", ".join(faltan))


# ---------------------------------------------------------------------------------------------- peralte según el radio
# Valores de control de la lectura de las figuras de peralte (mensaje de la orquestadora del 17/11/2026, tarea 6.10):
# (figura, velocidad en km/h, radio en m, peralte en %). Tolerancia ±0,2 %: es una lectura gráfica.
CASOS_PERALTE_FIGURA = (
    ("F302.03", 60, 150, 7.55),
    ("F302.04", 60, 150, 10.35),
    ("F302.03", 30, 150, 3.91),
    ("F302.03", 40, 150, 5.49),
    ("F302.03", 50, 150, 6.55),
    ("F302.04", 40, 60, 11.00),
)
TOLERANCIA_FIGURA_PCT = 0.2


def comprobar_figuras_peralte(base: norma.BaseNorma, informe: Informe, casos=CASOS_PERALTE_FIGURA):
    if not norma_peralte.tiene_tabla(base, norma_peralte.TABLA_FIGURA):
        informe.linea(f"Peralte según el radio: la base no tiene {norma_peralte.TABLA_FIGURA} (tarea 6.10); se omiten "
                      f"{len(casos)} casos de control.")
        return
    informe.linea("Peralte según el radio (lectura de las Figuras 302.02 a 302.05, Decisión 6), sin el radio mínimo:")
    for ref, v, radio, esperado_pct in casos:
        que = f"{ref} · {v} km/h · R = {radio} m"
        try:
            r = norma_peralte.peralte_de_figura(base, ref, v, radio, {"exigido": None, "referencia": None})
        except norma.ErrorNorma as e:
            informe.comparar(que, esperado_pct, str(e))
            continue
        informe.comparar(que, esperado_pct, r["peralte_calculado_pct"], TOLERANCIA_FIGURA_PCT,
                         f"{r['resultado']}, entregado {r['peralte_pct']} %")


# ---------------------------------------------------------------------------------------------- sobreancho, P5 opción F
# Valores de control de la decisión P5 (opción F, decidida por el dueño el 20/11/2026; tarea 7.5), con B2 (L 10,55 m),
# n = 2 y calzada de 7,20 m: (velocidad en km/h, radio en m, sobreancho aplicado sin y con reduccion_t30220, en m). 0 es
# que no se aplica (Sa < 0,40 m, P7). Se comparan a dos decimales.
CASOS_SOBREANCHO_P5 = (
    (30, 25, 5.27, 4.53), (30, 30, 4.38, 3.64), (60, 125, 1.43, 0.77), (60, 150, 1.23, 0.58), (60, 200, 0.98, 0.40),
    (60, 400, 0.58, 0.40), (60, 500, 0.49, 0.40), (60, 600, 0.43, 0.40), (80, 600, 0.51, 0.40), (80, 700, 0.46, 0.40),
    (30, 500, 0.0, 0.0),
)
L_B2_CONTROL_M = 10.55
# Filas de la T302.20 que necesitan esos controles (los radios entre dos filas usan las dos vecinas).
RADIOS_P5 = (25, 30, 120, 130, 150, 200, 400, 450, 500)


def _sobreancho_aplicado(filas: list, velocidad: float, radio: float, reduccion: bool) -> float:
    sa = norma_sobreancho.sobreancho_formula(2, radio, L_B2_CONTROL_M, velocidad)["sobreancho"]
    elegido = norma_sobreancho.factor_de_tabla(filas, radio) if velocidad <= norma_sobreancho.V_MAX_FACTOR_KMH else None
    return norma_sobreancho.decidir(sa, elegido["valor"] if elegido else None, reduccion)["sobreancho"]


def comprobar_sobreancho_p5(base: norma.BaseNorma, informe: Informe, casos=CASOS_SOBREANCHO_P5):
    filas = norma_sobreancho.factores_reduccion(base)
    radios = {float(f["radio_m"]) for f in filas}
    faltan = [r for r in RADIOS_P5 if not any(abs(r - x) <= norma.TOLERANCIA for x in radios)]
    if faltan:
        informe.linea(f"Sobreancho con la T302.20 (P5, opción F): la base no tiene las filas de {faltan} m; se omiten "
                      f"{len(casos)} casos de control.")
    else:
        informe.linea("Sobreancho aplicado sin y con reduccion_t30220 (P5, opción F; B2, n = 2, calzada 7,20 m):")
        for v, radio, sin, con in casos:
            for reduccion, esperado in ((False, sin), (True, con)):
                obtenido = round(_sobreancho_aplicado(filas, v, radio, reduccion), 2)
                informe.comparar(f"{v} km/h · R = {radio} m · {'con' if reduccion else 'sin'} reducción", esperado,
                                 obtenido, 0.001)
    # Al crecer el radio, el sobreancho aplicado no sube (barrido de 11 a 1000 m cada 0,5 m), con y sin reducción.
    radios_barrido = sorted({11 + 0.5 * i for i in range(1979)} | {r for r in radios if r > L_B2_CONTROL_M})
    for v in (30, 50, 60, 80):
        for reduccion in (False, True):
            valores = [_sobreancho_aplicado(filas, v, r, reduccion) for r in radios_barrido]
            sube = next((radios_barrido[i + 1] for i in range(len(valores) - 1) if valores[i + 1] > valores[i] + 1e-9),
                        None)
            informe.comparar(f"Barrido de radios a {v} km/h {'con' if reduccion else 'sin'} reducción: el sobreancho no "
                             "sube", "no sube", "no sube" if sube is None else f"sube en R = {sube} m")


# ---------------------------------------------------------------------------------------------- principal
def main(argv=None, salida=None) -> int:
    parser = argparse.ArgumentParser(description="Casos de la DG-2018 contra la base real de memoria-firma")
    parser.add_argument("--memoria", help="Carpeta de memoria-firma (por defecto, MEMORIA_FIRMA_DIR)")
    args = parser.parse_args(argv)
    informe = Informe(salida)
    try:
        ruta_base = norma.localizar_base(args.memoria)
    except norma.ErrorNorma as e:
        informe.linea(f"Error: {e}")
        return 2
    carpeta = os.path.dirname(os.path.dirname(os.path.dirname(ruta_base)))
    ruta_casos = os.path.join(carpeta, RUTA_CASOS)
    if not os.path.isfile(ruta_casos):
        informe.linea(f"Error: falta el archivo de casos {ruta_casos}. Actualiza memoria-firma (git pull).")
        return 2
    with open(ruta_casos, "r", encoding="utf-8") as f:
        documento = yaml.load(f, Loader=_Cargador) or {}
    try:
        base = norma.BaseNorma(ruta_base)
    except norma.ErrorNorma as e:
        informe.linea(f"Error: {e}")
        return 2
    with base:
        meta = base.metadatos()
        informe.linea(f"Base: {ruta_base} ({meta.get('documento', '')}, {meta.get('script_version', '')})")
        informe.linea(f"Casos: {ruta_casos} (versión {documento.get('version')})")
        informe.linea()
        for caso in documento.get("casos") or []:
            correr_caso(base, caso, informe)
        informe.linea()
        comprobar_formulas(base, informe)
        comprobar_umbrales(base, informe)
        comprobar_referencias(base, informe)
        comprobar_figuras_peralte(base, informe)
        comprobar_sobreancho_p5(base, informe)
    informe.linea()
    informe.linea(f"Resultado: {informe.ok} comprobaciones OK, {informe.fallos} fallos, {informe.sin_comparar} sin comparar, "
                  f"{informe.fuera} casos fuera de alcance.")
    return 1 if informe.fallos else 0


if __name__ == "__main__":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    sys.exit(main())

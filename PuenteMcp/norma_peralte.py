# -*- coding: utf-8 -*-
"""aplicar_peralte con la norma (1.20.0): calcula con la DG-2018 el peralte de cada curva del eje y sus progresivas
críticas, y llama al plugin con esos valores.

El plugin (ArbaMcp, 1.20.0) tiene `listar_peralte` (lectura) y `aplicar_peralte` (escritura con valores explícitos:
por curva, las progresivas críticas con su tipo, su región y las pendientes por segmento). El puente amplía
`aplicar_peralte` con `velocidad_diseno` y los datos del proyecto (los de verificar_norma y los del peralte). Con
alguno de ellos, el puente:

1. valida los datos del proyecto (norma.preparar y los datos del peralte); si falta algo, responde «faltan» sin llamar
   al plugin;
2. comprueba que el dibujo está en metros (INSUNITS = 6) y lee las curvas del eje con listar_pis;
3. por curva circular: peralte de diseño e leído en la figura de peralte según el radio digitalizada (peralte_figura,
   regla de la Decisión 6, «lectura gráfica»); bombeo b, longitudes de transición del peralte y del bombeo y su reparto
   entre tangente y curva (tablas de la sesión 6.4), y las progresivas críticas (progresivas_criticas);
4. comprueba que las transiciones caben (dentro del eje, sin cruzarse con las de la curva vecina y con peralte total
   no negativo); si no, responde el error sin escribir;
5. traduce los conceptos de la norma a los nombres de Civil 3D (CORRESPONDENCIA) y llama al plugin; con escritura
   real, relee con listar_peralte y compara el peralte total de cada curva.

Este módulo no usa la red ni Civil 3D: funciones puras sobre lo que devuelven el plugin y la base de la norma. Las
llamadas las hace main.py; la escritura la hace el plugin (copia, una entrada de Deshacer, verificación).

Convención (por verificar en Civil 3D, VALIDACION_120 paso 8): pendiente transversal en %, con signo, medida desde el
eje hacia el borde: negativa = baja hacia el borde (bombeo normal −b a los dos lados). En una curva a la derecha el
carril exterior es el izquierdo: sube hasta +e y el interior baja hasta −e.

Progresivas críticas de una curva circular sin espirales, con giro alrededor del eje (cada carril gira solo):
- entrada (puntos A, B, C y D de la Figura 302.15): fin del bombeo normal (FBN, A) → bombeo nivelado (BN, B, el
  exterior en 0 %) → bombeo invertido (BI, C, el exterior en +b; solo si e > b) → inicio del peralte total (IPT, D,
  ±e). BN = PC − k·Lp; FBN = BN − Lb; BI = BN + Lb; IPT = BN + Lp. Lp: longitud de transición del peralte (de 0 a
  e); Lb: la del bombeo (de −b a 0); k: proporción de Lp en la tangente. Si el tramo con el peralte completo queda
  corto (V/3.6, o 30 m si la curva mide menos de 30 m), se centra en la curva y las transiciones se corren;
- salida: simétrica respecto al PT: fin del peralte total (FPT), bombeo invertido, bombeo nivelado e inicio del
  bombeo normal (IBN).
"""
import math
import re
from decimal import ROUND_HALF_UP, Decimal

import norma as n
import norma_alineamiento as ne

# ---------------------------------------------------------------------------------------------- parámetros
# Datos del peralte que el puente añade a los de verificar_norma (esquema de dg2018.sqlite 1.2, sesión 6.4). Nunca se
# suponen: si falta alguno, «faltan».
CAMPOS_PERALTE = {
    "tipo_superficie": (str, "Superficie de rodadura para el bombeo (T304.03): 'Pavimento asfáltico y/o concreto Portland', "
                             "'Tratamiento superficial' o 'Afirmado'"),
    "precipitacion": (str, "Precipitación para el bombeo (T304.03): '<500 mm/año' o '>500 mm/año' (o los mm/año)"),
    "bombeo": (float, "Bombeo en % (dos aguas). Obligatorio si la T304.03 da un rango para la superficie y la "
                      "precipitación (R304.05-2); si se da, manda sobre la tabla"),
    "ancho_carril": (float, "Distancia en m del borde de la calzada al eje de giro: el ancho de un carril, porque el "
                            "peralte gira sobre el eje (R304.06.02-5). Es la B de la F20"),
}
CAMPOS = {**ne.CAMPOS_PROYECTO, **CAMPOS_PERALTE}
# Lo que el plugin tiene que publicar para que el puente amplíe la herramienta.
PARAMETROS_NECESARIOS = {"alineamiento", "curvas", "simular"}
DESCRIPCION_CURVAS = " Con 'velocidad_diseno' no se pasa (el puente calcula las progresivas críticas con la DG-2018)."
PARAMETROS_OPCIONALES = {"curvas": DESCRIPCION_CURVAS}

DESCRIPCION = (
    "\n\nCON LA NORMA DG-2018 (puente, desde 1.20.0): con 'velocidad_diseno' y los datos del proyecto ('clasificacion', "
    "'orografia', 'ubicacion', 'peralte_maximo', 'pavimentada': los de verificar_norma, y los del peralte que pida), "
    "dentro de 'args' y SIN 'curvas', el puente lee las curvas del eje con listar_pis y calcula en cada curva circular el "
    "peralte de diseño (leído en la figura de peralte según el radio de la DG-2018 digitalizada: 'lectura gráfica', con "
    "su error; explícalo así al usuario), el bombeo, las longitudes de transición y las progresivas críticas (fin del "
    "bombeo normal, bombeo nivelado, bombeo invertido, inicio y fin del peralte total, inicio del bombeo normal), y las "
    "aplica. Si una curva no cumple el radio mínimo, la figura no da valor o las transiciones no caben (fuera del eje, "
    "cruzadas con las de la curva vecina o sin tramo de peralte total) responde el error sin escribir. Si falta un dato del proyecto responde 'faltan' sin llamar a Civil 3D: pregúntalo al usuario, no "
    "lo supongas. Lee INSUNITS (debe ser 6, metros). Devuelve la respuesta del plugin más 'propuesta_norma' (por curva: "
    "cada valor con su tabla, sección y página, y las progresivas) y, con la escritura real, 'verificacion' (el peralte "
    "total leído con listar_peralte). Simula primero (simular=true). Necesita MEMORIA_FIRMA_DIR."
)

TOLERANCIA_M = 0.001          # m, la de las progresivas del plugin
TOLERANCIA_PCT = 0.001        # %, la de las pendientes del plugin

# ---------------------------------------------------------------------------------------------- conceptos de la norma
FBN, BN, BI, IPT, FPT, IBN = ("fin_bombeo_normal", "bombeo_nivelado", "bombeo_invertido", "inicio_peralte_total",
                              "fin_peralte_total", "inicio_bombeo_normal")
TIPOS = (FBN, BN, BI, IPT, FPT, IBN)
ENTRADA, SALIDA = "entrada", "salida"
REGIONES = (ENTRADA, SALIDA)
IZQUIERDO, DERECHO = "carril_izquierdo", "carril_derecho"
SEGMENTOS = (IZQUIERDO, DERECHO)
NOMBRES = {FBN: "Fin del bombeo normal", BN: "Bombeo nivelado", BI: "Bombeo invertido", IPT: "Inicio del peralte total",
           FPT: "Fin del peralte total", IBN: "Inicio del bombeo normal"}

# Concepto de la norma → nombre del enumerado de Civil 3D (SuperelevationCriticalStationType,
# SuperelevationAttainmentRegionType, SuperelevationCrossSegmentType). El catálogo de la API no trae los valores de los
# enumerados: estos nombres son los que devolvió listar_peralte → enumerados en Civil 3D 2027 (17/11/2026,
# VALIDACION_120 pasos 2 y 3, ronda D), con la elección de cada concepto de esa prueba (1.21.2). «BeginingAttainmentRegion»
# lleva la errata de Autodesk (una sola «n»): es el nombre real. Los carriles «Out» (exteriores) son los de una calzada de
# dos carriles sin mediana: POR VERIFICAR con el editor tabular de peralte (VALIDACION_120, paso 5b) y la sección (paso 9).
CORRESPONDENCIA = {
    "tipo": {FBN: "EndNormalCrown", BN: "LevelCrown", BI: "ReverseCrown", IPT: "BeginFullSuper", FPT: "EndFullSuper",
             IBN: "BeginNormalCrown"},
    "region": {ENTRADA: "BeginingAttainmentRegion", SALIDA: "EndingAttainmentRegion"},
    "segmento": {IZQUIERDO: "LeftOutLaneCrossSlope", DERECHO: "RightOutLaneCrossSlope"},
}


# ---------------------------------------------------------------------------------------------- llamada
def campos_que_chocan(parametros: list) -> list:
    """Parámetros del plugin con el mismo nombre que uno de los del puente (entonces no se amplía la herramienta)."""
    return sorted({p.get("name") for p in parametros} & set(CAMPOS))


def se_puede_ampliar(parametros: list) -> bool:
    nombres = {p.get("name") for p in parametros}
    return PARAMETROS_NECESARIOS <= nombres and not campos_que_chocan(parametros)


def es_con_norma(datos: dict) -> bool:
    """La llamada usa la norma si trae cualquiera de los datos del proyecto (no solo velocidad_diseno)."""
    return any(datos.get(c) is not None for c in CAMPOS)


def separar(datos: dict) -> tuple[dict, dict]:
    """(argumentos para el plugin, datos del proyecto para la norma)."""
    plugin = {k: v for k, v in datos.items() if k not in CAMPOS}
    proyecto = {k: datos.get(k) for k in CAMPOS}
    return plugin, proyecto


def error_curvas_y_norma(args: dict) -> str | None:
    """Con la norma, 'curvas' no se pasa: las progresivas las calcula el puente."""
    if args.get("curvas") in (None, "", []):
        return None
    return ("Pasa 'curvas' (valores explícitos) o 'velocidad_diseno' con los datos del proyecto (el puente calcula el "
            "peralte con la DG-2018), no los dos. No se llamó a Civil 3D.")


def respuesta_faltan(faltan: list) -> dict:
    return {
        "ok": False,
        "error": "Faltan datos del proyecto para calcular el peralte con la DG-2018: " + ", ".join(f["dato"] for f in faltan)
                 + ". Pregúntalos al usuario con los valores válidos; no los supongas. No se llamó a Civil 3D: ni copia, "
                   "ni Deshacer, ni línea en mcp_log.jsonl.",
        "faltan": faltan,
    }


# ---------------------------------------------------------------------------------------------- peralte según el radio
# Las Figuras 302.02 a 302.05 de la DG-2018 digitalizadas (tarea 6.10 de memoria-firma), en la tabla peralte_figura:
# una fila por nodo de cada curva de velocidad (orden 1 = radio menor, el extremo superior). Regla de lectura: Decisión
# 6 del dueño, «A CONFIRMAR por Jose» (mensaje de la orquestadora del 17/11/2026):
#   1) la figura sale de la condición de la T304.05 (en terreno accidentado, la que elige el usuario con peralte_maximo);
#   2) la velocidad tiene que ser una de las curvas de la figura: no se interpola entre velocidades;
#   3) R < radio mínimo (T302.02/T302.04): «No cumple» y no se lee la figura;
#   4) R ≥ radio de la T304.04: sin peralte (bombeo normal);
#   5) si aplica la T304.06, el peralte es el mayor entre la lectura y el 2 %;
#   6) radio mínimo ≤ R < extremo superior de la curva: el p máx de la figura, con aviso (A CONFIRMAR);
#   7) si no, interpolación lineal en log10(R) entre dos nodos consecutivos de la misma curva, con 1 decimal, la
#      etiqueta «lectura gráfica» y su error_pct.
# Además se avisa si la lectura queda por debajo del bombeo. La T304.04, la T304.06 y el bombeo vienen de las tablas de
# la sesión 6.4: hasta que lleguen, esas reglas quedan como «pendientes» en la respuesta y no se escribe nada.
TABLA_FIGURA = "peralte_figura"
FIGURAS = {"F302.02": "zona urbana (4 %)", "F302.03": "zona rural, terreno plano u ondulado (8 %)",
           "F302.04": "zona rural, terreno accidentado o escarpado (12 %)", "F302.05": "zona con peligro de hielo (6 %)"}
ETIQUETA_GRAFICA = "lectura gráfica"
DECISION_6 = "Decisión 6, A CONFIRMAR por Jose"
PERALTE_MINIMO_T30406_PCT = 2.0   # regla 5 de la Decisión 6
SIN_PERALTE = "Sin peralte (bombeo normal)"
LECTURA = "Lectura gráfica"
P_MAXIMO = "Peralte máximo de la figura"
SIN_FIGURA = ("La base de la norma no tiene la tabla peralte_figura (las Figuras 302.02 a 302.05 de la DG-2018 "
              "digitalizadas, tarea 6.10): actualiza memoria-firma (git pull). No se llamó a Civil 3D.")
SIN_TABLAS = ("La base de la norma no tiene las tablas de bombeo, transición y peralte mínimo de la sesión 6.4 "
              "(dg2018.sqlite 1.2): actualiza memoria-firma (git pull). No se llamó a Civil 3D.")


def _un_decimal(valor: float) -> float:
    """1 decimal, redondeando la mitad hacia arriba (7,55 → 7,6), sin el error de la coma flotante."""
    return float(Decimal(repr(round(valor, 9))).quantize(Decimal("0.1"), rounding=ROUND_HALF_UP))


def tiene_tabla(base: n.BaseNorma, tabla: str) -> bool:
    return base.fila("SELECT name FROM sqlite_master WHERE type IN ('table', 'view') AND name = ?", tabla) is not None


def figura_de_condicion(condicion: str | None, base: n.BaseNorma | None = None) -> str | None:
    """Figura de peralte de una condición de la T304.05: la columna peralte_maximo.figura de la base ('302.03' →
    'F302.03', paso 1 de la Decisión 6) y, si la base no la trae, por el texto (urbana, hielo, escarpado, rural)."""
    if base is not None and condicion:
        columnas = {r["name"] for r in base.filas("PRAGMA table_info(peralte_maximo)")}
        if "figura" in columnas:
            r = base.fila("SELECT figura FROM peralte_maximo WHERE condicion = ? ORDER BY orden", condicion)
            if r and r["figura"]:
                f = str(r["figura"]).strip()
                return f if f.upper().startswith("F") else "F" + f
    t = n.normalizar(condicion)
    if not t:
        return None
    if "urban" in t:
        return "F302.02"
    if "hielo" in t:
        return "F302.05"
    if "escarpado" in t:
        return "F302.04"
    if "rural" in t:
        return "F302.03"
    return None


def condicion_del_proyecto(base: n.BaseNorma, ctx: dict) -> str | None:
    """La condición de la T304.05 del proyecto. Si la fila tiene dos (terreno accidentado: Decisión 5), la que tiene el
    peralte máximo que eligió el usuario."""
    condiciones = [ctx.get("condicion_peralte")]
    for f in base.equivalencias(ctx["clasificacion"], ctx["orografia"], ctx.get("zona")) if ctx.get("zona") else []:
        condiciones += [f["condicion_peralte"], f["condicion_peralte_otra"]]
    condiciones = list(dict.fromkeys(c for c in condiciones if c))
    pmax = ctx.get("peralte_maximo")
    if pmax is not None and len(condiciones) > 1:
        for c in condiciones:
            absoluto = base.peralte_de_condicion(c)
            if absoluto is not None and abs(absoluto - pmax) <= n.TOLERANCIA:
                return c
    return ctx.get("condicion_peralte")


def velocidades_de_figura(base: n.BaseNorma, ref: str) -> list[int]:
    return [int(r["velocidad_kmh"]) for r in base.filas(
        f"SELECT DISTINCT velocidad_kmh FROM {TABLA_FIGURA} WHERE ref = ? ORDER BY velocidad_kmh", ref)]


def nodos_de_figura(base: n.BaseNorma, ref: str, v: int) -> list[dict]:
    return base.filas(f"SELECT * FROM {TABLA_FIGURA} WHERE ref = ? AND velocidad_kmh = ? ORDER BY orden, radio_m", ref, v)


def peralte_maximo_de_figura(base: n.BaseNorma, ref: str) -> float | None:
    """El «p máx.» de la figura (figuras_peralte.p_max_pct) o, si la base no lo trae, el mayor de sus nodos."""
    if tiene_tabla(base, "figuras_peralte"):
        r = base.fila("SELECT p_max_pct FROM figuras_peralte WHERE ref = ?", ref)
        if r and r["p_max_pct"] is not None:
            return r["p_max_pct"]
    r = base.fila(f"SELECT MAX(peralte_pct) AS pmax FROM {TABLA_FIGURA} WHERE ref = ?", ref)
    return r["pmax"] if r else None


def _aviso_grafico(ref: str, metodo: str | None, error_pct) -> str:
    m = n.normalizar(metodo)
    if "aashto" in m:
        origen = "de una tabla tipo AASHTO, no de la DG-2018"
    elif m in ("", "vectorial"):
        origen = "de la figura digitalizada (" + (metodo or "método sin indicar") + ")"
    else:
        origen = f"de la figura leída a mano ({metodo})"
    error = f", error ±{n._limpio(error_pct)} %" if error_pct is not None else ""
    return (f"El peralte sale {origen}: {ref} de la DG-2018 ({ETIQUETA_GRAFICA}{error}); no es una tabla de la norma, "
            "revísalo en la figura.")


def peralte_de_figura(base: n.BaseNorma, ref: str, v: int, radio: float, radio_minimo: dict,
                      t30404: dict | None = None, t30406: dict | None = None, bombeo_pct: float | None = None) -> dict:
    """Peralte de diseño de una curva de radio R con la Decisión 6 (ver arriba). radio_minimo: la salida de
    norma.radio_minimo_exigido. t30404: {"radio_m", "referencia"} o None si la base aún no la tiene; t30406:
    {"aplica", "minimo_pct", "referencia"} o None; bombeo_pct: el del proyecto o None. Lo que falta queda en
    'pendientes'."""
    salida = {"figura": ref, "figura_nombre": FIGURAS.get(ref), "referencia": n.texto_referencia(base.referencia(ref)),
              "velocidad_kmh": v, "radio_m": n._limpio(radio), "radio_minimo_m": n._limpio(radio_minimo.get("exigido")),
              "radio_minimo_referencia": n.texto_referencia(radio_minimo.get("referencia")),
              "regla": None, "resultado": None, "peralte_calculado_pct": None, "peralte_pct": None, "error_pct": None,
              "etiqueta": None, "nodos": [], "avisos": [], "pendientes": [], "a_confirmar": False}
    rmin = radio_minimo.get("exigido")
    if rmin is not None and radio < rmin - n.TOLERANCIA:
        salida.update(regla=3, resultado=n.NO_CUMPLE)
        salida["avisos"].append(f"R = {n._limpio(radio)} m es menor que el radio mínimo ({n._limpio(rmin)} m, "
                                f"{salida['radio_minimo_referencia']}): «No cumple»; no se lee la figura. Corrige el eje.")
        return salida
    if rmin is None:
        salida["pendientes"].append("radio mínimo (T302.02/T302.04): la base no lo da para esta velocidad")
    if t30404 is None:
        salida["pendientes"].append("T304.04 (radio a partir del cual no hace falta peralte): falta en la base (sesión 6.4)")
    elif t30404.get("radio_m") is not None and radio >= t30404["radio_m"] - n.TOLERANCIA:
        salida.update(regla=4, resultado=SIN_PERALTE)
        salida["avisos"].append(f"R = {n._limpio(radio)} m no es menor que el radio de la T304.04 "
                                f"({n._limpio(t30404['radio_m'])} m): la curva no lleva peralte (bombeo normal).")
        return salida

    nodos = sorted(nodos_de_figura(base, ref, v), key=lambda x: x["radio_m"])
    if not nodos:
        raise n.ErrorNorma(f"La {ref} no tiene la curva de {v} km/h en la base: no se interpola entre velocidades.")
    salida["etiqueta"] = ETIQUETA_GRAFICA
    primero, ultimo = nodos[0], nodos[-1]
    if radio < primero["radio_m"] - n.TOLERANCIA:
        pmax = peralte_maximo_de_figura(base, ref)
        salida.update(regla=6, resultado=P_MAXIMO, peralte_calculado_pct=n._limpio(pmax), error_pct=primero["error_pct"],
                      nodos=[primero], a_confirmar=True)
        salida["avisos"].append(f"R = {n._limpio(radio)} m está entre el radio mínimo ({n._limpio(rmin)} m) y el extremo "
                                f"superior de la curva de {v} km/h de la {ref} ({n._limpio(primero['radio_m'])} m): se usa "
                                f"el p máx de la figura, {n._limpio(pmax)} % ({DECISION_6}).")
        valor = pmax
    elif radio > ultimo["radio_m"] + n.TOLERANCIA:
        salida.update(regla=7, resultado=n.SIN_VALOR, nodos=[ultimo], a_confirmar=True)
        salida["avisos"].append(f"R = {n._limpio(radio)} m está más allá del extremo inferior de la curva de {v} km/h de la "
                                f"{ref} ({n._limpio(ultimo['radio_m'])} m) y la T304.04 no dice que no lleve peralte: la "
                                f"figura no da valor ({DECISION_6}).")
        return salida
    else:
        i = next(k for k in range(len(nodos) - 1) if nodos[k]["radio_m"] - n.TOLERANCIA <= radio
                 <= nodos[k + 1]["radio_m"] + n.TOLERANCIA) if len(nodos) > 1 else 0
        a = nodos[i]
        b = nodos[i + 1] if len(nodos) > 1 else nodos[i]
        if b["radio_m"] == a["radio_m"]:
            valor = a["peralte_pct"]
        else:
            t = (math.log10(radio) - math.log10(a["radio_m"])) / (math.log10(b["radio_m"]) - math.log10(a["radio_m"]))
            t = min(1.0, max(0.0, t))
            valor = a["peralte_pct"] + t * (b["peralte_pct"] - a["peralte_pct"])
        errores = [x["error_pct"] for x in (a, b) if x["error_pct"] is not None]
        salida.update(regla=7, resultado=LECTURA, peralte_calculado_pct=round(valor, 3),
                      error_pct=max(errores) if errores else None, nodos=[a] if a is b else [a, b])
    salida["avisos"].append(_aviso_grafico(ref, salida["nodos"][0].get("metodo"), salida["error_pct"]))

    entregado = _un_decimal(valor)
    if t30406 is None:
        salida["pendientes"].append("T304.06 (peralte mínimo del 2 %): falta en la base (sesión 6.4)")
    elif t30406.get("aplica"):
        minimo = t30406.get("minimo_pct", PERALTE_MINIMO_T30406_PCT)
        if entregado < minimo - n.TOLERANCIA:
            salida["avisos"].append(f"La lectura ({n._limpio(entregado)} %) es menor que el peralte mínimo de la T304.06 "
                                    f"({n._limpio(minimo)} %): se usa {n._limpio(minimo)} %.")
            entregado = float(minimo)
            salida["regla"] = 5
    if bombeo_pct is None:
        salida["pendientes"].append("bombeo del proyecto: falta en la base (sesión 6.4)")
    elif entregado < bombeo_pct - n.TOLERANCIA:
        salida["avisos"].append(f"El peralte ({n._limpio(entregado)} %) queda por debajo del bombeo ({n._limpio(bombeo_pct)} %).")
    salida["peralte_pct"] = entregado
    salida["nodos"] = [{k: x.get(k) for k in ("orden", "radio_m", "peralte_pct", "error_pct", "tipo_punto", "metodo")}
                       for x in salida["nodos"]]
    return salida


def verificar_peralte_diseno(diseno_pct: float, lectura: dict, maximo_pct: float | None,
                             bombeo_pct: float | None = None) -> dict:
    """Paso 8 de la Decisión 6 (A CONFIRMAR): un peralte de diseño cumple si es igual o mayor que el obtenido
    (redondeado a 0,1 %) y no pasa del máximo de la T304.05. «Cumple con aviso» si el obtenido sale del paso 6 (p máx de
    la figura) o el diseño queda por debajo del bombeo. 'lectura' es la salida de peralte_de_figura."""
    if lectura["resultado"] == n.NO_CUMPLE:
        return {"resultado": n.NO_CUMPLE, "motivo": "el radio es menor que el mínimo"}
    obtenido = lectura["peralte_pct"]
    if maximo_pct is not None and diseno_pct > maximo_pct + n.TOLERANCIA:
        return {"resultado": n.NO_CUMPLE, "motivo": f"pasa del máximo de la T304.05 ({n._limpio(maximo_pct)} %)"}
    if obtenido is not None and diseno_pct < obtenido - n.TOLERANCIA:
        return {"resultado": n.NO_CUMPLE, "motivo": f"es menor que el obtenido ({n._limpio(obtenido)} %)"}
    aviso = lectura["regla"] == 6 or (bombeo_pct is not None and diseno_pct < bombeo_pct - n.TOLERANCIA)
    return {"resultado": n.CUMPLE_AVISO if aviso else n.CUMPLE, "motivo": None}


# ---------------------------------------------------------------------------------------------- datos del proyecto
TABLAS_PERALTE = ("bombeo", "radio_sin_peralte", "peralte_minimo", "proporcion_peralte_tangente", "transicion_peralte",
                  "transicion_peralte_tercera_clase")
PROPORCION_RECOMENDADA = 0.7         # P2, opción b: 0,7p siempre (R302.08-12, Figura 302.15)
TRAMO_CURVA_CORTA_M = 30.0           # R302.08-11: curva circular de menos de 30 m → 30 m con el peralte completo
DESVANECIMIENTO_MAX_M = 20.0         # R302.08-4: calzada única, el bombeo se desvanece en 20 m como máximo
TOLERANCIA_ANCHO_M = 0.05


def _precipitacion(valor, opciones: list[str]) -> str | None:
    """'<500 mm/año' o '>500 mm/año' de la T304.03 a partir del texto o de los mm/año (500 justos: no se elige)."""
    if valor is None or (isinstance(valor, str) and not valor.strip()):
        return None
    elegida = n._elegir(valor, opciones)
    if elegida:
        return elegida
    numero = n._numero(str(valor).replace(",", ".").replace("mm/año", "").replace("mm/ano", "").strip())
    texto = n.normalizar(valor).replace(" ", "")
    if numero is None:
        signo = "<" if texto.startswith("<") else ">" if texto.startswith(">") else None
    else:
        signo = "<" if numero < 500 else ">" if numero > 500 else None
    return next((o for o in opciones if signo and o.strip().startswith(signo)), None)


def bombeo_del_proyecto(base: n.BaseNorma, proyecto: dict) -> tuple[dict | None, list, list]:
    """Bombeo del proyecto (T304.03): (bombeo, faltan, avisos). Si la tabla da un rango, se pregunta (R304.05-2)."""
    filas = base.filas("SELECT * FROM bombeo ORDER BY orden")
    tipos = list(dict.fromkeys(f["tipo_superficie"] for f in filas))
    precipitaciones = list(dict.fromkeys(f["precipitacion"] for f in filas))
    explicito = n._numero(proyecto.get("bombeo"))
    tipo = n._elegir(proyecto.get("tipo_superficie"), tipos)
    precip = _precipitacion(proyecto.get("precipitacion"), precipitaciones)
    fila = next((f for f in filas if f["tipo_superficie"] == tipo and f["precipitacion"] == precip), None)
    referencia = n.texto_referencia(base.referencia("T304.03"))
    faltan, avisos = [], []
    if explicito is not None:
        if explicito <= 0:
            faltan.append(n._falta("bombeo", "El bombeo es un porcentaje positivo (dos aguas).", None))
            return None, faltan, avisos
        if fila and not (fila["bombeo_min_pct"] - n.TOLERANCIA <= explicito <= fila["bombeo_max_pct"] + n.TOLERANCIA):
            avisos.append(f"El bombeo dado ({n._limpio(explicito)} %) está fuera del de la T304.03 para '{tipo}' con "
                          f"{precip} ({fila['bombeo_texto_pdf']} %): manda el dado.")
        return {"valor_pct": explicito, "origen": "usuario", "referencia": referencia,
                "tabla": fila["bombeo_texto_pdf"] if fila else None}, faltan, avisos
    if tipo is None:
        faltan.append(n._falta("tipo_superficie", "Superficie de rodadura, para el bombeo de la T304.03 (o pasa 'bombeo').",
                               tipos))
    if precip is None:
        faltan.append(n._falta("precipitacion", "Precipitación media anual, para el bombeo de la T304.03 (o pasa "
                                                "'bombeo'); con 500 mm/año justos hay que elegir.", precipitaciones))
    if faltan:
        return None, faltan, avisos
    if fila["bombeo_max_pct"] - fila["bombeo_min_pct"] > n.TOLERANCIA:
        faltan.append(n._falta("bombeo", f"La T304.03 da {fila['bombeo_texto_pdf']} % para '{tipo}' con {precip}: elige el "
                                         "valor dentro del rango (R304.05-2); no se elige solo.",
                               [n._limpio(fila["bombeo_min_pct"]), n._limpio(fila["bombeo_max_pct"])]))
        return None, faltan, avisos
    return {"valor_pct": fila["bombeo_min_pct"], "origen": "T304.03", "referencia": referencia,
            "tabla": fila["bombeo_texto_pdf"]}, faltan, avisos


def t30404_del_proyecto(base: n.BaseNorma, v: int) -> tuple[dict, list]:
    """Radio a partir del cual no hace falta peralte (T304.04, R304.06.01-1). Si la tabla no tiene la velocidad, la fila
    de la velocidad siguiente (la más exigente), con aviso."""
    filas = base.filas("SELECT * FROM radio_sin_peralte ORDER BY velocidad_kmh")
    ref = n.texto_referencia(base.referencia("T304.04"))
    fila = next((f for f in filas if f["velocidad_kmh"] == v), None)
    avisos = []
    if fila is None:
        abierta = next((f for f in filas if "≥" in str(f["velocidad_texto"]) and v >= f["velocidad_kmh"]), None)
        fila = abierta or next((f for f in filas if f["velocidad_kmh"] > v), None) or (filas[-1] if filas else None)
        if fila is not None and abierta is None:
            avisos.append(f"La T304.04 no tiene fila para {v} km/h: se usa la de {fila['velocidad_texto']} km/h "
                          f"({n._limpio(fila['radio_m'])} m), la más exigente (A CONFIRMAR).")
    if fila is None:
        raise n.ErrorNorma("La base de la norma no tiene filas en radio_sin_peralte (T304.04). Actualiza memoria-firma.")
    return {"radio_m": fila["radio_m"], "velocidad_texto": fila["velocidad_texto"], "referencia": ref}, avisos


def t30406_para(base: n.BaseNorma, v: int, radio: float) -> dict:
    """Si la curva cae en un rango de la T304.06 (peralte al menos del 2 %, R304.06.01-2)."""
    for f in base.filas("SELECT * FROM peralte_minimo ORDER BY orden"):
        en_v = f["v_min_kmh"] <= v and (f["v_max_kmh"] is None or v < f["v_max_kmh"])
        en_r = f["r_min_m"] <= radio and (f["r_max_m"] is None or radio < f["r_max_m"])
        if en_v and en_r:
            return {"aplica": True, "minimo_pct": f["peralte_min_pct"], "referencia": n.texto_referencia(base.referencia("T304.06"))}
    return {"aplica": False}


def preparar_peralte(base: n.BaseNorma, ctx: dict | None, proyecto: dict) -> tuple[dict | None, list]:
    """Datos del peralte del proyecto comprobados contra la base: (datos, faltan). Elige la figura (regla 1), comprueba
    que la velocidad es una de sus curvas (regla 2), el bombeo (T304.03), el ancho girado (B de la F20) y la T304.04.
    Lanza ErrorNorma si la base no tiene peralte_figura o las tablas de la sesión 6.4."""
    if not tiene_tabla(base, TABLA_FIGURA):
        raise n.ErrorNorma(SIN_FIGURA)
    faltan_tablas = [t for t in TABLAS_PERALTE if not tiene_tabla(base, t)]
    if faltan_tablas:
        raise n.ErrorNorma(SIN_TABLAS + " Faltan: " + ", ".join(faltan_tablas) + ".")
    faltan, avisos = [], []
    bombeo, f_bombeo, a_bombeo = bombeo_del_proyecto(base, proyecto)
    faltan += f_bombeo
    avisos += a_bombeo
    ancho = n._numero(proyecto.get("ancho_carril"))
    if ancho is None or ancho <= 0:
        faltan.append(n._falta("ancho_carril", "Distancia en m del borde de la calzada al eje de giro (el ancho de un carril; "
                                               "el peralte gira sobre el eje, R304.06.02-5): es la B de la F20.",
                               ["número en m, por ejemplo 3.6"]))
    if ctx is None:
        return None, faltan
    condicion = condicion_del_proyecto(base, ctx)
    ref = figura_de_condicion(condicion, base)
    if ref is None:
        raise n.ErrorNorma(f"No se sabe qué figura de peralte (302.02 a 302.05) corresponde a la condición «{condicion}» de "
                           "la T304.05. Revisa la base de la norma. No se llamó a Civil 3D.")
    velocidades = velocidades_de_figura(base, ref)
    if ctx["velocidad"] not in velocidades:
        faltan.append(n._falta("velocidad_diseno", f"La {ref} no tiene curva para {ctx['velocidad']} km/h: no se "
                                                    "interpola entre velocidades.", velocidades))
    if faltan:
        return None, faltan
    t30404, a_t30404 = t30404_del_proyecto(base, ctx["velocidad"])
    avisos += a_t30404
    return {"figura": ref, "condicion_t30405": condicion, "maximo_t30405": base.peralte_de_condicion(condicion),
            "radio_minimo": n.radio_minimo_exigido(base, ctx), "bombeo": bombeo, "ancho_carril": ancho,
            "t30404": t30404, "avisos": avisos}, []


def peralte_de_curva(base: n.BaseNorma, ctx: dict, datos: dict, curva: dict) -> dict:
    return peralte_de_figura(base, datos["figura"], ctx["velocidad"], curva["radio"], datos["radio_minimo"],
                             datos.get("t30404"), t30406_para(base, ctx["velocidad"], curva["radio"]),
                             (datos.get("bombeo") or {}).get("valor_pct"))


def _proporcion_t30407(base: n.BaseNorma, p: float) -> tuple[float | None, str | None]:
    """Proporción de la T304.07 para el peralte p, leyendo los rangos de texto («p < 4.5%», «4.5% < p < 7%», «p >7%»).
    En los límites (4,5 y 7 %), que la tabla deja fuera, la fila del medio."""
    filas = base.filas("SELECT * FROM proporcion_peralte_tangente ORDER BY orden")
    for f in filas:
        numeros = [float(x) for x in re.findall(r"\d+(?:\.\d+)?", str(f["rango_peralte"]).replace(",", "."))]
        texto = str(f["rango_peralte"]).replace(" ", "")
        if len(numeros) == 2 and numeros[0] - n.TOLERANCIA <= p <= numeros[1] + n.TOLERANCIA:
            return f["proporcion"], f["rango_peralte"]
        if len(numeros) == 1 and texto.startswith("p<") and p < numeros[0] - n.TOLERANCIA:
            return f["proporcion"], f["rango_peralte"]
        if len(numeros) == 1 and texto.startswith("p>") and p > numeros[0] + n.TOLERANCIA:
            return f["proporcion"], f["rango_peralte"]
    return None, None


def transicion_de_curva(base: n.BaseNorma, ctx: dict, datos: dict, curva: dict, peralte_pct: float) -> dict:
    """Bombeo b, longitudes de transición del bombeo (Lb, de −b a 0) y del peralte (Lp, de 0 a p), proporción k de Lp en
    la tangente y tramo mínimo con el peralte completo, cada valor con su referencia:
    - ipmáx = 1.8 − 0.01 V (F19); Lb y Lp con F20 (B = ancho_carril). En Tercera Clase, además la T302.13 (R302.08-2;
      sin fila para p, la del peralte siguiente, con aviso); en las demás, las Tablas 302.14 a 302.18 si tienen fila
      exacta. Se usa el mayor (con los dos, el diseño cumple los dos) y se avisa si difieren.
    - k = 0,7 (P2, opción b, A CONFIRMAR), con la T304.07 informada.
    - Peralte completo de al menos V/3.6 m (F33) y, si la curva circular mide menos de 30 m, 30 m (R302.08-11) (P3,
      opción a, A CONFIRMAR)."""
    v = ctx["velocidad"]
    b = datos["bombeo"]["valor_pct"]
    ancho = datos["ancho_carril"]
    p = peralte_pct
    ipmax = 1.8 - 0.01 * v
    if ipmax <= 0:
        raise n.ErrorNorma(f"ipmáx = 1.8 − 0.01·{v} no es positivo (F19): no se puede calcular la transición.")
    lb_f20, lp_f20 = b / ipmax * ancho, p / ipmax * ancho
    lb, lp = lb_f20, lp_f20
    avisos, a_confirmar = [], []
    valores = [
        {"valor": "bombeo", "valor_pct": n._limpio(b), "origen": datos["bombeo"]["origen"],
         "referencia": datos["bombeo"]["referencia"]},
        {"valor": "ipmáx", "valor_pct": n._limpio(ipmax), "formula": "1.8 − 0.01·V",
         "referencia": n.texto_referencia(base.referencia("F19"))},
    ]
    detalle_lb = {"valor": "longitud de transición del bombeo (de −b a 0)", "F20_m": n._limpio(lb_f20),
                  "referencia": n.texto_referencia(base.referencia("F20"))}
    detalle_lp = {"valor": "longitud de transición del peralte (de 0 a p)", "F20_m": n._limpio(lp_f20),
                  "referencia": n.texto_referencia(base.referencia("F20"))}
    if ctx.get("tercera_clase"):
        filas = base.filas("SELECT * FROM transicion_peralte_tercera_clase WHERE velocidad_kmh = ? ORDER BY orden", v)
        f_b = next((f for f in filas if f["tipo"] == "bombeo"), None)
        f_p = sorted((f for f in filas if f["tipo"] == "peralte" and f["peralte_pct"] >= p - n.TOLERANCIA),
                     key=lambda f: f["peralte_pct"])
        ref_t = n.texto_referencia(base.referencia("T302.13"))
        if f_b is None or not f_p:
            avisos.append(f"La T302.13 no tiene fila para {v} km/h y p = {n._limpio(p)} %: solo F20.")
        else:
            f_p = f_p[0]
            if abs(f_p["peralte_pct"] - p) > n.TOLERANCIA:
                avisos.append(f"PI {curva['pi']}: la T302.13 no tiene fila para p = {n._limpio(p)} %: se usa la de "
                              f"{n._limpio(f_p['peralte_pct'])} % ({n._limpio(f_p['longitud_m'])} m), la más exigente "
                              "(A CONFIRMAR).")
                a_confirmar.append("T302.13 sin fila para p")
            detalle_lb.update(tabla_m=n._limpio(f_b["longitud_m"]), tabla=ref_t)
            detalle_lp.update(tabla_m=n._limpio(f_p["longitud_m"]), tabla=ref_t)
            lb, lp = max(lb_f20, f_b["longitud_m"]), max(lp_f20, f_p["longitud_m"])
            if abs(f_b["longitud_m"] - lb_f20) > n.TOLERANCIA or abs(f_p["longitud_m"] - lp_f20) > n.TOLERANCIA:
                avisos.append(f"PI {curva['pi']}: Tercera Clase (R302.08-2): la T302.13 da Lb = {n._limpio(f_b['longitud_m'])} "
                              f"m y Lp = {n._limpio(f_p['longitud_m'])} m; F20 da {n._limpio(lb_f20)} y {n._limpio(lp_f20)} m: "
                              "se usa el mayor de cada uno.")
    else:
        fila = next((f for f in base.filas("SELECT * FROM transicion_peralte WHERE velocidad_kmh = ? ORDER BY ref, orden", v)
                     if abs(f["eje_giro_m"] - ancho) <= TOLERANCIA_ANCHO_M and abs(f["ancho_calzada_m"] - 2 * ancho) <= TOLERANCIA_ANCHO_M
                     and abs(f["peralte_inicial_pct"] - b) <= n.TOLERANCIA and abs(f["peralte_final_pct"] + p) <= n.TOLERANCIA),
                    None)
        total_f20 = lb_f20 + lp_f20
        if fila is None:
            valores.append({"valor": "comprobación con las Tablas 302.14 a 302.18", "nota":
                            f"sin fila para {v} km/h, calzada {n._limpio(2 * ancho)} m con giro sobre el eje, de "
                            f"{n._limpio(b)} % a −{n._limpio(p)} %: solo F20"})
        else:
            ref_t = n.texto_referencia(base.referencia(fila["ref"]))
            valores.append({"valor": "longitud total de −b a p", "F20_m": n._limpio(total_f20), "tabla_m": fila["longitud_m"],
                            "tabla": ref_t})
            if abs(fila["longitud_m"] - total_f20) > n.TOLERANCIA:
                avisos.append(f"PI {curva['pi']}: la {fila['ref']} da {n._limpio(fila['longitud_m'])} m de −{n._limpio(b)} % "
                              f"a +{n._limpio(p)} % y F20 da {n._limpio(total_f20)} m: se usa el mayor.")
            if fila["longitud_m"] > total_f20:
                factor = fila["longitud_m"] / total_f20
                lb, lp = lb_f20 * factor, lp_f20 * factor
    detalle_lb["usado_m"] = n._limpio(lb)
    detalle_lp["usado_m"] = n._limpio(lp)
    valores += [detalle_lb, detalle_lp]
    if lb > DESVANECIMIENTO_MAX_M + n.TOLERANCIA:
        avisos.append(f"PI {curva['pi']}: el desvanecimiento del bombeo mide {n._limpio(lb)} m y en calzada única no "
                      f"debería pasar de {n._limpio(DESVANECIMIENTO_MAX_M)} m (R302.08-4).")

    k_tabla, rango = _proporcion_t30407(base, p)
    k = PROPORCION_RECOMENDADA
    valores.append({"valor": "proporción del peralte en la tangente", "usado": k,
                    "referencia": n.texto_referencia(base.referencia("R302.08-12")) + "; " + n.texto_referencia(base.referencia("F302.15")),
                    "T304_07": k_tabla, "rango_T304_07": rango, "a_confirmar": "P2 (opción b)"})
    a_confirmar.append("P2")
    if k_tabla is not None and abs(k_tabla - k) > n.TOLERANCIA:
        avisos.append(f"PI {curva['pi']}: la T304.07 da {n._limpio(k_tabla)}p para p = {n._limpio(p)} % ({rango}); se usa "
                      f"{k}p (recomendación b de P2: 0,7p siempre; 0,5p u 0,8p solo con curvas próximas; A CONFIRMAR por Jose).")

    longitud_curva = curva["progresiva_pt"] - curva["progresiva_pc"]
    v_36 = v / 3.6
    minimo = max(v_36, TRAMO_CURVA_CORTA_M if longitud_curva < TRAMO_CURVA_CORTA_M - n.TOLERANCIA else 0.0)
    valores.append({"valor": "tramo mínimo con el peralte completo", "usado_m": n._limpio(minimo), "V_3_6_m": n._limpio(v_36),
                    "curva_m": n._limpio(longitud_curva),
                    "referencia": n.texto_referencia(base.referencia("F33")) + "; " + n.texto_referencia(base.referencia("R302.08-11")),
                    "a_confirmar": "P3 (opción a)"})
    a_confirmar.append("P3")
    return {"bombeo_pct": b, "longitud_peralte_m": lp, "longitud_bombeo_m": lb, "proporcion_tangente": k,
            "peralte_completo_min_m": minimo, "valores": valores, "avisos": avisos, "a_confirmar": a_confirmar}


def unidades_no_metros(resultado) -> str | None:
    """Motivo para no calcular si INSUNITS no es 6 (metros); None si el dibujo está en metros."""
    motivo = ne.unidades_no_metros(resultado)
    if motivo is None:
        return None
    valor = resultado.get("valor") if isinstance(resultado, dict) else None
    valor = "" if valor is None else str(valor).strip()
    nombre = ne.INSUNITS_NOMBRES.get(valor, "unidades desconocidas")
    return (f"El dibujo no está en metros (INSUNITS = {valor or 'vacío'}, {nombre}): la DG-2018 está en metros y las "
            "progresivas y longitudes de transición estarían mal. No se llamó a aplicar_peralte. Si el usuario confirma "
            "que el dibujo está en metros, que ponga INSUNITS en 6 y repite; o pasa 'curvas' con tus valores.")


def calcular(base: n.BaseNorma, ctx: dict, datos: dict, tabla_pis: dict) -> dict:
    """Propuesta por curva circular del eje: peralte de la figura, transiciones, progresivas críticas y problemas.
    Devuelve {curvas, avisos, problemas, error_tablas}: si error_tablas no es None, faltan tablas de la 6.4 y las curvas
    traen solo el peralte."""
    curvas, avisos = curvas_del_eje(tabla_pis)
    avisos = list(datos.get("avisos") or []) + avisos
    salida, problemas, error_tablas = [], [], None
    for curva in curvas:
        pf = peralte_de_curva(base, ctx, datos, curva)
        avisos.extend(f"PI {curva['pi']}: {a}" for a in pf["avisos"])
        fila = {**curva, "peralte": pf, "peralte_pct": pf["peralte_pct"], "estaciones": []}
        salida.append(fila)
        if pf["resultado"] in (n.NO_CUMPLE, n.SIN_VALOR):
            problemas.append(f"PI {curva['pi']}: {pf['resultado']} ({pf['avisos'][0] if pf['avisos'] else ''})")
            continue
        if pf["resultado"] == SIN_PERALTE:
            continue
        maximo = datos.get("maximo_t30405")
        if maximo is not None and pf["peralte_pct"] is not None and pf["peralte_pct"] > maximo + n.TOLERANCIA:
            problemas.append(f"PI {curva['pi']}: el peralte obtenido ({n._limpio(pf['peralte_pct'])} %) pasa del máximo de "
                             f"la T304.05 ({n._limpio(maximo)} %).")
            continue
        try:
            v = transicion_de_curva(base, ctx, datos, curva, pf["peralte_pct"])
        except n.ErrorNorma as e:
            error_tablas = str(e)
            continue
        avisos.extend(v.get("avisos") or [])
        estaciones = progresivas_criticas(curva, pf["peralte_pct"], v["bombeo_pct"], v["longitud_peralte_m"],
                                          v["longitud_bombeo_m"], v["proporcion_tangente"], v["peralte_completo_min_m"])
        pks = {(e["concepto"], e["region"]): e["progresiva"] for e in estaciones}
        completo = pks[(FPT, SALIDA)] - pks[(IPT, ENTRADA)]
        sin_correr = (curva["progresiva_pt"] - curva["progresiva_pc"]) - 2 * (1 - v["proporcion_tangente"]) * v["longitud_peralte_m"]
        corrida = completo - sin_correr > TOLERANCIA_M
        if corrida:
            avisos.append(f"PI {curva['pi']}: el tramo con el peralte completo mediría {n._limpio(sin_correr)} m; las "
                          f"transiciones se corren hacia afuera para que mida {n._limpio(completo)} m, centrado en la curva "
                          "(R302.08-11 y F33; P3, A CONFIRMAR).")
        if completo < TRAMO_CURVA_CORTA_M - TOLERANCIA_M:
            avisos.append(f"PI {curva['pi']}: el tramo con el peralte completo mide {n._limpio(completo)} m, menos de 30 m "
                          "(P3: aviso).")
        fila.update({"bombeo_pct": n._limpio(v["bombeo_pct"]), "longitud_peralte_m": n._limpio(v["longitud_peralte_m"]),
                     "longitud_bombeo_m": n._limpio(v["longitud_bombeo_m"]),
                     "proporcion_tangente": n._limpio(v["proporcion_tangente"]),
                     "longitud_peralte_completo_m": n._limpio(completo), "transiciones_corridas": corrida,
                     "valores": v.get("valores") or [], "a_confirmar": v.get("a_confirmar") or [],
                     "estaciones": estaciones})
    inicio = float(((tabla_pis or {}).get("inicio") or {}).get("progresiva") or 0.0)
    fin = ((tabla_pis or {}).get("fin") or {}).get("progresiva")
    fin = float(fin) if fin is not None else math.inf
    problemas += comprobar_cabida([c for c in salida if c["estaciones"]], inicio, fin)
    return {"curvas": salida, "avisos": avisos, "problemas": problemas, "error_tablas": error_tablas}


# ---------------------------------------------------------------------------------------------- curvas del eje
def curvas_del_eje(tabla_pis: dict) -> tuple[list, list]:
    """Las curvas circulares de la tabla de listar_pis, en orden, y los avisos (PI de tipo no soportado: espirales...)."""
    curvas, avisos = [], []
    for p in (tabla_pis or {}).get("pis") or []:
        if p.get("tiene_curva") and p.get("tipo") == "curva":
            if p.get("progresiva_pc") is None or p.get("progresiva_pt") is None or not p.get("radio"):
                avisos.append(f"PI {p.get('numero')}: listar_pis no da radio, PC o PT; la curva queda sin peralte.")
                continue
            curvas.append({"pi": int(p["numero"]), "radio": float(p["radio"]), "giro": p.get("giro"),
                           "progresiva_pc": float(p["progresiva_pc"]), "progresiva_pt": float(p["progresiva_pt"]),
                           "deflexion_grados": p.get("deflexion_grados")})
        elif p.get("tipo") not in ("curva", "sin_curva"):
            avisos.append(f"PI {p.get('numero')}: tipo {p.get('tipo')} no soportado (solo curvas circulares); queda sin "
                          "peralte.")
    return curvas, avisos


# ---------------------------------------------------------------------------------------------- geometría
def _pendientes(giro: str, exterior: float, interior: float) -> dict:
    """Pendiente de cada carril según el giro: en una curva a la derecha el exterior es el izquierdo."""
    if giro == "derecha":
        return {IZQUIERDO: exterior, DERECHO: interior}
    if giro == "izquierda":
        return {IZQUIERDO: interior, DERECHO: exterior}
    raise n.ErrorNorma(f"La curva no tiene giro (izquierda o derecha): '{giro}'. Revisa el eje con listar_pis.")


def progresivas_criticas(curva: dict, e: float, b: float, lp: float, lb: float, p: float,
                         peralte_completo_min: float = 0.0) -> list[dict]:
    """Progresivas críticas de una curva circular sin espirales (ver el docstring del módulo; puntos A, B, C y D de la
    Figura 302.15). e y b en % (positivos), lp y lb en m (> 0), p (la proporción k de lp en la tangente) entre 0 y 1.
    Si el tramo con el peralte completo (de PC + (1 − k)·lp a PT − (1 − k)·lp) mide menos de peralte_completo_min, se
    centra en la curva con esa longitud y las transiciones se corren hacia afuera (caso 7b). El bombeo invertido (C =
    B + lb) solo se pone si e > b y queda antes del peralte completo. Lanza ErrorNorma si un dato no tiene sentido."""
    for nombre, valor in (("peralte", e), ("bombeo", b), ("longitud de transición del peralte", lp),
                          ("longitud de transición del bombeo", lb)):
        if valor is None or not math.isfinite(valor) or valor <= 0:
            raise n.ErrorNorma(f"PI {curva['pi']}: {nombre} no válido ({valor}).")
    if p is None or not 0 <= p <= 1:
        raise n.ErrorNorma(f"PI {curva['pi']}: proporción de la transición en la tangente no válida ({p}).")
    pc, pt, giro = curva["progresiva_pc"], curva["progresiva_pt"], curva["giro"]
    ipt, fpt = pc + (1 - p) * lp, pt - (1 - p) * lp
    if peralte_completo_min and fpt - ipt < peralte_completo_min - TOLERANCIA_M:
        medio = (pc + pt) / 2
        ipt, fpt = medio - peralte_completo_min / 2, medio + peralte_completo_min / 2
    bn_e, bn_s = ipt - lp, fpt + lp
    con_bi = e > b + TOLERANCIA_PCT and lb < lp - TOLERANCIA_M
    filas = [
        (FBN, ENTRADA, bn_e - lb, -b, -b),
        (BN, ENTRADA, bn_e, 0.0, -b),
    ]
    if con_bi:
        filas.append((BI, ENTRADA, bn_e + lb, b, -b))
    filas.append((IPT, ENTRADA, ipt, e, -e))
    filas.append((FPT, SALIDA, fpt, e, -e))
    if con_bi:
        filas.append((BI, SALIDA, bn_s - lb, b, -b))
    filas.append((BN, SALIDA, bn_s, 0.0, -b))
    filas.append((IBN, SALIDA, bn_s + lb, -b, -b))
    return [{"concepto": c, "nombre": NOMBRES[c], "region": r, "progresiva": n._limpio(pk),
             "progresiva_texto": n.progresiva_texto(pk), "pendientes": {k: n._limpio(v) for k, v in
                                                                       _pendientes(giro, ext, inte).items()}}
            for c, r, pk, ext, inte in filas]


def comprobar_cabida(curvas: list, inicio: float, fin: float) -> list[str]:
    """Problemas que impiden escribir: peralte total de longitud negativa (curva corta), progresivas fuera del eje y
    transiciones de dos curvas seguidas que se cruzan. Cada curva trae 'pi' y 'estaciones' (de progresivas_criticas)."""
    problemas = []
    for c in curvas:
        pks = {(e["concepto"], e["region"]): e["progresiva"] for e in c["estaciones"]}
        ipt, fpt = pks[(IPT, ENTRADA)], pks[(FPT, SALIDA)]
        if fpt - ipt <= TOLERANCIA_M:
            problemas.append(f"PI {c['pi']}: la curva es corta para el peralte: no queda tramo con el peralte total (empezaría "
                             f"en {n.progresiva_texto(ipt)} y acabaría en {n.progresiva_texto(fpt)}).")
        primera, ultima = c["estaciones"][0]["progresiva"], c["estaciones"][-1]["progresiva"]
        if primera < inicio - TOLERANCIA_M or ultima > fin + TOLERANCIA_M:
            problemas.append(f"PI {c['pi']}: las transiciones ({n.progresiva_texto(primera)} a {n.progresiva_texto(ultima)}) "
                             f"salen del eje ({n.progresiva_texto(inicio)} a {n.progresiva_texto(fin)}).")
    for a, b in zip(curvas, curvas[1:]):
        fin_a, inicio_b = a["estaciones"][-1]["progresiva"], b["estaciones"][0]["progresiva"]
        if fin_a > inicio_b + TOLERANCIA_M:
            problemas.append(f"Las transiciones de las curvas del PI {a['pi']} (hasta {n.progresiva_texto(fin_a)}) y del "
                             f"PI {b['pi']} (desde {n.progresiva_texto(inicio_b)}) se cruzan: la tangente entre las dos "
                             "curvas no deja sitio. Mueve los vértices, baja la velocidad o pasa 'curvas' con tus valores "
                             "(las curvas de sentido contrario, R302.02-9, no se resuelven todavía).")
    return problemas


# ---------------------------------------------------------------------------------------------- traducción a Civil 3D
def correspondencia_incompleta(correspondencia: dict | None = None) -> list[str]:
    """Conceptos sin nombre de Civil 3D en la correspondencia."""
    c = correspondencia or CORRESPONDENCIA
    return [f"{grupo}.{concepto}" for grupo in ("tipo", "region", "segmento") for concepto, valor in c[grupo].items()
            if not valor]


def mensaje_sin_correspondencia(faltan: list) -> str:
    return ("Falta la correspondencia entre la DG-2018 y los nombres de Civil 3D para " + ", ".join(faltan) + ": los "
            "valores de los enumerados del peralte no están en el catálogo de la API y no se inventan. Se rellenan con lo "
            "que devuelve listar_peralte → enumerados (VALIDACION_120, paso 1). No se llamó a aplicar_peralte; la "
            "propuesta de la norma va en 'propuesta_norma'.")


def curvas_para_el_plugin(curvas: list, correspondencia: dict | None = None) -> list:
    """El parámetro 'curvas' de aplicar_peralte del plugin con los nombres de Civil 3D. Lanza ErrorNorma si falta algún
    nombre en la correspondencia."""
    c = correspondencia or CORRESPONDENCIA
    faltan = correspondencia_incompleta(c)
    if faltan:
        raise n.ErrorNorma(mensaje_sin_correspondencia(faltan))
    return [{"pi": cu["pi"], "estaciones": [
        {"progresiva": e["progresiva"], "tipo": c["tipo"][e["concepto"]], "region": c["region"][e["region"]],
         "pendientes": {c["segmento"][s]: v for s, v in e["pendientes"].items()}} for e in cu["estaciones"]]}
        for cu in curvas if cu.get("estaciones")]


# ---------------------------------------------------------------------------------------------- verificación
def verificar_lectura(curvas: list, leido: dict, correspondencia: dict | None = None) -> dict:
    """Compara el peralte total pedido de cada curva con el que lee listar_peralte (en la progresiva del inicio del
    peralte total). Devuelve {verificado, filas, avisos}."""
    c = correspondencia or CORRESPONDENCIA
    estaciones = [e for cu in (leido or {}).get("curvas") or [] for e in cu.get("estaciones") or []]
    filas, avisos = [], []
    for cu in (x for x in curvas if x.get("estaciones")):
        ipt = next(e for e in cu["estaciones"] if e["concepto"] == IPT)
        real = next((e for e in estaciones if abs(float(e.get("progresiva") or 0) - ipt["progresiva"]) <= TOLERANCIA_M),
                    None)
        for segmento, pedido in ipt["pendientes"].items():
            nombre = c["segmento"].get(segmento)
            valor = (real or {}).get("pendientes", {}).get(nombre) if real else None
            cumple = valor is not None and abs(float(valor) - pedido) <= TOLERANCIA_PCT
            filas.append({"pi": cu["pi"], "progresiva": ipt["progresiva"], "segmento": segmento, "pedido_pct": pedido,
                          "leido_pct": valor, "cumple": cumple})
            if not cumple:
                avisos.append(f"PI {cu['pi']}: el peralte total de {segmento} leído es {valor} % y se pidió {pedido} %.")
    return {"verificado": not avisos, "filas": filas, "avisos": avisos}


# ---------------------------------------------------------------------------------------------- respuesta
def bloque_propuesta(ctx: dict, curvas: list, avisos: list, insunits: dict, lecturas: dict) -> dict:
    return {
        "norma": n.NORMA,
        "velocidad_diseno": ctx["velocidad"],
        "proyecto": n._proyecto(ctx),
        "avisos_proyecto": list(ctx["avisos"]),
        "convencion": "Pendientes en %, con signo, del eje hacia el borde (negativa = baja hacia el borde); el carril "
                      "exterior de la curva sube hasta +e y el interior baja hasta −e. Por verificar en Civil 3D.",
        "unidades": insunits,
        "lecturas": lecturas,
        "curvas": curvas,
        "avisos": avisos,
    }

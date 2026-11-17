# -*- coding: utf-8 -*-
"""crear_bordes_calzada con la norma (1.21.0): sobreancho de cada curva según la DG-2018 (sección 302.09).

El puente amplía la herramienta del plugin con cinco parámetros opcionales: velocidad_diseno, numero_carriles,
ancho_calzada, vehiculo_diseno y longitud_transicion. Con alguno de ellos, el puente:

1. valida los datos del proyecto; si falta alguno, responde 'faltan' sin llamar a Civil 3D;
2. lee las curvas del eje con listar_pis (y comprueba que el dibujo está en metros);
3. calcula el sobreancho de cada curva circular con la F46 de la 302.09, Sa = n (R − √(R² − L²)) + V / (10 √R), con
   L la del vehículo de diseño (T202.01), y el factor de reducción de la T302.20 cuando aplica (criterio a confirmar);
4. lo pone solo en el borde INTERIOR de la curva (giro a la izquierda → borde izquierdo), en el tramo del PC al PT,
   con un desarrollo lineal (F47) de 40 m que termina en el PC y empieza en el PT (o longitud_transicion);
5. llama al plugin con esos ensanchamientos y devuelve su respuesta más 'propuesta_norma'.

Este módulo no usa la red ni Civil 3D: son funciones puras sobre lo que devuelve listar_pis y la base de la norma. La
escritura la sigue haciendo el plugin (Escritura.CrearObjeto): el puente no escribe nada en el dibujo.

Base: dg2018.sqlite de memoria-firma, versión 1.2 de la tarea 6.4 (commit 13776b1), con las tablas vehiculos_diseno
(T202.01: L = l_frontal_eje_trasero_m, solo en los vehículos de una separación de ejes; NULL en los articulados) y
sobreancho_reduccion (T302.20: radio_m y factor_reduccion, para calzadas de 7,20 m en tangente), las fórmulas F46 a F49
y las reglas R302.09.02-1 a -4 y R302.09.03-1 a -4. sobreancho_holguras (T302.19) es solo informativa: la F46 no la usa.

Decisiones pendientes del dueño (P4 a P8 de la 6.4): se usa la opción recomendada y la respuesta la marca a_confirmar.
- P4 (R302.09.03-1): el segundo término, V / (10 √R), lo evalúa el diseñador; se incluye siempre (la F46 entera).
- P5 (T302.20): el factor se aplica solo con calzada de 7,20 m, V ≤ 80 km/h y el radio exacto de la tabla; la
  respuesta da Sa con y sin factor.
- P6 (T202.01): los articulados no tienen L; si el usuario elige uno, 'faltan' con los vehículos que sí la tienen.
- P7 (R302.09.03-2): «un valor mínimo de 0.40 m de sobreancho para justificar su adopción»: si Sa < 0,40 m no se
  aplica y se avisa; no se sube a 0,40.
- P8 (R302.09.02, F47/F48): desarrollo en 40 m («Normalmente ... será de 40 m»), no en la transición del peralte.
Además: con V > 80 km/h, «el valor del sobreancho será calculado para cada caso» (R302.09.03-4): fórmula sin factor y
aviso; velocidades fuera de 30 a 80 km/h (figura 302.18B): fórmula y aviso. Sin redondeo: el valor exacto.
Ningún valor de la norma está escrito en este archivo, salvo los umbrales que cita el texto (0,40 m, 40 m, 7,20 m, 30 y
80 km/h).
"""
import json
import math

import norma as n

# Parámetros que el puente añade a crear_bordes_calzada: nombre → (tipo de Python, descripción).
CAMPOS_PROYECTO = {
    "velocidad_diseno": (float, "Velocidad de diseño en km/h. Con este parámetro y los otros tres datos del proyecto "
                                "(numero_carriles, ancho_calzada, vehiculo_diseno) el puente calcula el sobreancho de "
                                "cada curva con la DG-2018 (302.09)"),
    "numero_carriles": (int, "Número de carriles de la calzada (n de la fórmula del sobreancho, F46 de la 302.09)"),
    "ancho_calzada": (float, "Ancho de la calzada en tangente, en m (decide el factor de reducción de la T302.20 y, si "
                             "no pasas 'desplazamiento', la media calzada = ancho_calzada / 2)"),
    "vehiculo_diseno": (str, "Vehículo de diseño de la T202.01 por su código (VL, B2...) o su nombre: da L, la distancia "
                             "del frente al eje trasero. Si no está o es articulado (sin L), 'faltan' trae los válidos"),
    "longitud_transicion": (float, "Longitud (m) del desarrollo lineal del sobreancho, antes del PC y después del PT. "
                                   "Por defecto 40 m (302.09)"),
}
OBLIGATORIOS = ("velocidad_diseno", "numero_carriles", "ancho_calzada", "vehiculo_diseno")
# Lo que el plugin tiene que publicar para que el puente amplíe la herramienta.
PARAMETROS_NECESARIOS = {"eje", "desplazamiento", "ensanchamientos", "simular"}
# Parámetro obligatorio del plugin que con la norma pasa a ser opcional (sale de ancho_calzada / 2).
PARAMETRO_OPCIONAL = "desplazamiento"

DESCRIPCION = (
    "\n\nCON LA NORMA DG-2018 (puente, desde 1.21.0): con 'velocidad_diseno', 'numero_carriles', 'ancho_calzada' y "
    "'vehiculo_diseno' (código de la T202.01: VL, B2...) dentro de 'args' (y 'longitud_transicion' opcional), el puente "
    "lee las curvas del eje con listar_pis, calcula el sobreancho de cada curva con la F46 de la 302.09, Sa = "
    "n(R − √(R² − L²)) + V/(10√R), y el factor de reducción de la T302.20 cuando aplica, y lo pone solo en el borde "
    "INTERIOR de la curva, del PC al PT, con un desarrollo lineal de 40 m (o longitud_transicion) antes del PC y después "
    "del PT. Un sobreancho menor de 0,40 m no se aplica. No pases 'ensanchamientos' (los calcula el puente) ni "
    "'desplazamiento' salvo que el usuario lo pida (por defecto ancho_calzada / 2). Si falta un dato responde 'faltan' "
    "sin llamar a Civil 3D: pregúntalo al usuario, no lo supongas. Devuelve la respuesta del plugin más "
    "'propuesta_norma' (por curva: R, V, n, L, los términos de la fórmula, Sa sin y con factor, si se aplica, el borde y "
    "las progresivas; qué manda; y 'pendiente_de_decision', las decisiones del dueño que se tomaron a_confirmar: "
    "díselas). Simula primero (simular=true). Necesita MEMORIA_FIRMA_DIR."
)
DESCRIPCION_DESPLAZAMIENTO = " Con 'velocidad_diseno' no hace falta: por defecto ancho_calzada / 2."
# Para main._crear_handler (forma común de las herramientas ampliadas con la norma, 1.20.0): los parámetros que se
# añaden y los del plugin que dejan de ser obligatorios, con el texto que se añade a su descripción.
CAMPOS = CAMPOS_PROYECTO
PARAMETROS_OPCIONALES = {PARAMETRO_OPCIONAL: DESCRIPCION_DESPLAZAMIENTO}

SECCION = "302.09"
PAGINAS_IMPRESAS = "159 a 162"
REF_VEHICULOS = "T202.01"
REF_REDUCCION = "T302.20"
FORMULA_SA = "F46"
FORMULA_DESARROLLO = "F47"
REGLA_DESARROLLO = "R302.09.02-1"
REGLA_SEGUNDO_TERMINO = "R302.09.03-1"
REGLA_MINIMO = "R302.09.03-2"
REGLA_BORDE_INTERIOR = "R302.09.03-3"
REGLA_MAS_DE_80 = "R302.09.03-4"
# Todo lo que este módulo cita por id (las tablas, fórmulas y reglas de la 6.4).
REFERENCIAS_USADAS = (REF_VEHICULOS, REF_REDUCCION, FORMULA_SA, FORMULA_DESARROLLO, REGLA_DESARROLLO,
                      REGLA_SEGUNDO_TERMINO, REGLA_MINIMO, REGLA_BORDE_INTERIOR, REGLA_MAS_DE_80)

ANCHO_TABLA_REDUCCION_M = 7.20     # título de la T302.20: «para anchos de calzada en tangente de 7.20 m»
V_MIN_FIGURA_KMH = 30.0            # rango de la figura 302.18B
V_MAX_FIGURA_KMH = 80.0
V_MAX_FACTOR_KMH = 80.0            # «V ≤ 80» (R302.09.03-3); por encima, «calculado para cada caso» (R302.09.03-4)
SOBREANCHO_MINIMO_M = 0.40         # «un valor mínimo de 0.40 m ... para justificar su adopción» (R302.09.03-2)
LONGITUD_TRANSICION_M = 40.0       # «Normalmente la longitud para desarrollar el sobreancho será de 40 m»
TABLAS_SOBREANCHO = ("vehiculos_diseno", "sobreancho_reduccion")
# Umbrales de las reglas de la 302.09 que usa el código; casos_norma_reales.py los compara con reglas.valor_num.
UMBRALES_REGLAS = {REGLA_MINIMO: SOBREANCHO_MINIMO_M}
# Filas de la T302.20 que la 6.4 marcó como posible errata (450 m da 0,08 y 400 m da 0,07).
POSIBLE_ERRATA_RADIOS_M = (400.0, 450.0)

FORMULA = "Sa = n (R − √(R² − L²)) + V / (10 √R)"
CRITERIO = (
    "Por curva circular: Sa = n (R − √(R² − L²)) + V / (10 √R) (F46, 302.09), con L la del vehículo de diseño "
    "(T202.01), por el factor de reducción de la T302.20 si la calzada en tangente es de 7,20 m, V no pasa de 80 km/h y "
    "el radio está en la tabla (a confirmar, P5). Sin redondear. Si Sa < 0,40 m no se aplica (P7). Solo en el borde "
    "interior (giro a la izquierda → borde izquierdo), sobreancho pleno del PC al PT y desarrollo lineal (F47) de 40 m "
    "(o longitud_transicion) que termina en el PC y empieza en el PT, recortado si no cabe en el eje (P8). Si las zonas "
    "de dos curvas del mismo borde se tocan, se unen con el mayor sobreancho."
)
# Decisiones pendientes del dueño (6.4): la opción usada es la recomendada, siempre a_confirmar.
PENDIENTES = [
    {"id": "P4", "tema": "El segundo término de la F46, V / (10 √R), que «lo evalúa el diseñador»",
     "referencia": REGLA_SEGUNDO_TERMINO, "opcion_usada": "Se incluye siempre: la F46 entera.", "a_confirmar": True},
    {"id": "P5", "tema": "Cómo se aplica el factor de reducción de la T302.20",
     "referencia": REF_REDUCCION, "opcion_usada": "Solo con calzada de 7,20 m en tangente, V ≤ 80 km/h y el radio exacto "
     "de la tabla; la respuesta da Sa sin y con factor.", "a_confirmar": True},
    {"id": "P6", "tema": "L de los vehículos articulados (NULL en la T202.01)",
     "referencia": REF_VEHICULOS, "opcion_usada": "No se inventa: si el usuario elige un articulado, 'faltan' con los "
     "vehículos que sí tienen L.", "a_confirmar": True},
    {"id": "P7", "tema": "Sobreancho menor de 0,40 m", "referencia": REGLA_MINIMO,
     "opcion_usada": "No se aplica y se avisa; no se sube a 0,40 m.", "a_confirmar": True},
    {"id": "P8", "tema": "Longitud del desarrollo en curvas circulares simples: 40 m (F47) o la transición del peralte "
     "(F48, la calcula la tarea 6.5)", "referencia": REGLA_DESARROLLO,
     "opcion_usada": "40 m terminando en el PC y empezando en el PT, o 'longitud_transicion'.", "a_confirmar": True},
]
NOTA_REDONDEO = "Sin redondear: el texto de la 302.09 no dice cómo redondear el sobreancho; se da el valor exacto."


# ---------------------------------------------------------------------------------------------- llamada
def campos_que_chocan(parametros: list) -> list:
    """Parámetros del plugin con el mismo nombre que uno de los del puente (entonces no se amplía la herramienta)."""
    return sorted({p.get("name") for p in parametros} & set(CAMPOS_PROYECTO))


def se_puede_ampliar(parametros: list) -> bool:
    nombres = {p.get("name") for p in parametros}
    return PARAMETROS_NECESARIOS <= nombres and not campos_que_chocan(parametros)


def es_con_norma(datos: dict) -> bool:
    """La llamada usa la norma si trae cualquiera de los datos del proyecto."""
    return any(datos.get(c) is not None for c in CAMPOS_PROYECTO)


def separar(datos: dict) -> tuple[dict, dict]:
    """(argumentos para el plugin, datos del proyecto para la norma)."""
    plugin = {k: v for k, v in datos.items() if k not in CAMPOS_PROYECTO}
    proyecto = {k: datos.get(k) for k in CAMPOS_PROYECTO}
    return plugin, proyecto


def respuesta_faltan(faltan: list) -> dict:
    return {
        "ok": False,
        "error": "Faltan datos del proyecto para calcular el sobreancho de la DG-2018: " + ", ".join(f["dato"] for f in faltan)
                 + ". Pregúntalos al usuario con los valores válidos; no los supongas. No se llamó a Civil 3D: ni copia, "
                   "ni Deshacer, ni línea en mcp_log.jsonl.",
        "faltan": faltan,
    }


def referencia_seccion() -> dict:
    return n.referencia_seccion(SECCION, None, PAGINAS_IMPRESAS)


def _cita(base: n.BaseNorma, ident: str) -> str:
    return n.texto_referencia(base.referencia(ident))


# ---------------------------------------------------------------------------------------------- base (esquema de la 6.4)
def comprobar_tablas(base: n.BaseNorma):
    """Lanza ErrorNorma si la base no tiene las tablas del sobreancho (las añadió la tarea 6.4, base 1.2)."""
    existentes = {r["name"] for r in base.filas("SELECT name FROM sqlite_master WHERE type IN ('table', 'view')")}
    faltan = [t for t in TABLAS_SOBREANCHO if t not in existentes]
    if faltan:
        raise n.ErrorNorma(f"La base de la norma {base.ruta} no tiene todavía las tablas del sobreancho: {', '.join(faltan)}. "
                           "Actualiza memoria-firma (git pull): las añadió la tarea 6.4 (base 1.2). No se llamó a Civil 3D.")


def vehiculos(base: n.BaseNorma) -> list[dict]:
    """Vehículos de diseño de la T202.01 con su L (l_frontal_eje_trasero_m; NULL en los articulados), en orden."""
    return base.filas("SELECT codigo, tipo_vehiculo, l_frontal_eje_trasero_m AS l_m, n_separaciones, ref "
                      "FROM vehiculos_diseno ORDER BY orden")


def factores_reduccion(base: n.BaseNorma) -> list[dict]:
    """Filas de la T302.20 (radio_m, factor_reduccion), para calzadas de 7,20 m en tangente."""
    return base.filas("SELECT radio_m, factor_reduccion, factor_texto_pdf, ref FROM sobreancho_reduccion ORDER BY radio_m")


# ---------------------------------------------------------------------------------------------- datos del proyecto
def _numero(valor):
    if valor is None or isinstance(valor, bool):
        return None
    try:
        v = float(str(valor).replace(",", ".")) if isinstance(valor, str) else float(valor)
    except ValueError:
        return None
    return v if math.isfinite(v) else None


def _texto_vehiculo(v: dict) -> str:
    return f"{v['codigo']} ({v['tipo_vehiculo']}, L = {n._limpio(v['l_m'])} m)" if v.get("tipo_vehiculo") else v["codigo"]


def _elegir_vehiculo(lista: list[dict], texto) -> dict | None:
    """El vehículo por su código o por su nombre (sin mayúsculas ni tildes), o None."""
    clave = n.normalizar(texto)
    for v in lista:
        if clave and clave in (n.normalizar(v.get("codigo")), n.normalizar(v.get("tipo_vehiculo"))):
            return v
    return None


def resolver(base: n.BaseNorma, proyecto: dict) -> tuple[dict | None, list]:
    """Comprueba los datos del proyecto. Devuelve (contexto, faltan); si 'faltan' no está vacío, hay que preguntarlos."""
    faltan = []
    lista = vehiculos(base)
    con_l = [v for v in lista if v.get("l_m") is not None]
    codigos = [v["codigo"] for v in con_l]
    detalle = "; ".join(_texto_vehiculo(v) for v in con_l)

    v = _numero(proyecto.get("velocidad_diseno"))
    if v is None or v <= 0:
        faltan.append(n._falta("velocidad_diseno", "Velocidad de diseño en km/h (> 0): entra en la fórmula del sobreancho "
                                                   "y decide si se usa el factor de la T302.20."))
    carriles = _numero(proyecto.get("numero_carriles"))
    if carriles is None or carriles < 1 or abs(carriles - round(carriles)) > 1e-9:
        faltan.append(n._falta("numero_carriles", "Número de carriles de la calzada (n de la fórmula, entero ≥ 1).", [1, 2, 3, 4]))
    ancho = _numero(proyecto.get("ancho_calzada"))
    if ancho is None or ancho <= 0:
        faltan.append(n._falta("ancho_calzada", "Ancho de la calzada en tangente en m (> 0): decide el factor de reducción "
                                                "de la T302.20 y la media calzada por defecto."))
    elegido = _elegir_vehiculo(lista, proyecto.get("vehiculo_diseno"))
    if n._vacio(proyecto.get("vehiculo_diseno")):
        faltan.append(n._falta("vehiculo_diseno", f"Vehículo de diseño de la {REF_VEHICULOS}: da L, la distancia del frente "
                                                  f"al eje trasero. Con L: {detalle}.", codigos))
    elif elegido is None:
        faltan.append(n._falta("vehiculo_diseno", f"'{proyecto.get('vehiculo_diseno')}' no es un vehículo de la "
                                                  f"{REF_VEHICULOS}. Con L: {detalle}.", codigos))
    elif elegido.get("l_m") is None:
        faltan.append(n._falta("vehiculo_diseno", f"'{elegido['codigo']}' es un vehículo articulado: la {REF_VEHICULOS} no da "
                                                  "su L y no se inventa (pendiente del dueño, P6). Elige uno con L: "
                                                  f"{detalle}.", codigos))
    lt = _numero(proyecto.get("longitud_transicion"))
    if proyecto.get("longitud_transicion") is not None and (lt is None or lt <= 0):
        faltan.append(n._falta("longitud_transicion", "Longitud del desarrollo del sobreancho en m (> 0); sin ella, 40 m "
                                                      "(302.09)."))
    if faltan:
        return None, faltan
    return {
        "velocidad": v,
        "numero_carriles": int(round(carriles)),
        "ancho_calzada": ancho,
        "vehiculo": elegido["codigo"],
        "vehiculo_nombre": elegido.get("tipo_vehiculo"),
        "l_m": float(elegido["l_m"]),
        "referencia_vehiculo": _cita(base, elegido.get("ref") or REF_VEHICULOS),
        "longitud_transicion": lt if lt is not None else LONGITUD_TRANSICION_M,
        "longitud_transicion_origen": "usuario" if lt is not None else "por defecto, 40 m (302.09, P8)",
    }, []


# ---------------------------------------------------------------------------------------------- cálculo
def sobreancho_formula(carriles: int, radio: float, l_m: float, v_kmh: float) -> dict:
    """F46: Sa = n (R − √(R² − L²)) + V / (10 √R), con sus dos términos. Lanza ValueError si R ≤ L."""
    if radio <= l_m:
        raise ValueError(f"el radio ({n._limpio(radio)} m) no es mayor que L ({n._limpio(l_m)} m): la fórmula no vale")
    geometrico = carriles * (radio - math.sqrt(radio * radio - l_m * l_m))
    velocidad = v_kmh / (10.0 * math.sqrt(radio))
    return {"termino_geometrico": geometrico, "termino_velocidad": velocidad, "sobreancho": geometrico + velocidad}


def factor_reduccion(base: n.BaseNorma, ctx: dict, radio: float) -> tuple[dict | None, str | None]:
    """(factor, motivo por el que no se aplica). Criterio a confirmar (P5): calzada de 7,20 m, V ≤ 80 y el radio exacto
    de la T302.20."""
    if ctx["velocidad"] > V_MAX_FACTOR_KMH + n.TOLERANCIA:
        return None, (f"V = {n._limpio(ctx['velocidad'])} km/h pasa de 80: «el valor del sobreancho será calculado para cada "
                      f"caso» ({_cita(base, REGLA_MAS_DE_80)}), así que se usa la fórmula sin el factor de la "
                      f"{REF_REDUCCION}.")
    if abs(ctx["ancho_calzada"] - ANCHO_TABLA_REDUCCION_M) > n.TOLERANCIA:
        return None, (f"La {REF_REDUCCION} es para calzadas de 7,20 m en tangente y la del proyecto es de "
                      f"{n._limpio(ctx['ancho_calzada'])} m: no se aplica factor (criterio a confirmar, P5).")
    fila = next((f for f in factores_reduccion(base) if abs(float(f["radio_m"]) - radio) <= n.TOLERANCIA), None)
    if fila is None:
        return None, (f"El radio {n._limpio(radio)} m no está en la {REF_REDUCCION}: no se aplica factor (criterio a "
                      "confirmar, P5: no se interpola).")
    factor = {"valor": float(fila["factor_reduccion"]), "texto_pdf": fila.get("factor_texto_pdf"),
              "radio_tabla": float(fila["radio_m"]), "referencia": _cita(base, fila.get("ref") or REF_REDUCCION),
              "a_confirmar": True}
    if any(abs(radio - r) <= n.TOLERANCIA for r in POSIBLE_ERRATA_RADIOS_M):
        factor["nota"] = (f"Posible errata en la {REF_REDUCCION}: 400 m da 0,07 y 450 m da 0,08 (el factor debería bajar "
                          "al subir el radio). Revisa el PDF.")
    return factor, None


def _curvas(tabla_pis: dict) -> tuple[list, list]:
    """(curvas circulares de listar_pis con radio, PC, PT y giro; avisos de las filas no soportadas)."""
    curvas, avisos = [], []
    for p in (tabla_pis or {}).get("pis") or []:
        tipo = p.get("tipo")
        if tipo == "curva" and p.get("radio") and p.get("progresiva_pc") is not None and p.get("progresiva_pt") is not None:
            curvas.append(p)
        elif tipo not in ("curva", "sin_curva"):
            avisos.append(f"PI {p.get('numero')}: tipo '{tipo}' no soportado (solo curvas circulares): no se calcula su "
                          "sobreancho. Con espiral, la norma lo desarrolla en la longitud de la espiral si es menor de "
                          "40 m (F47); hazlo con 'ensanchamientos' a mano.")
    return curvas, avisos


def proponer(base: n.BaseNorma, ctx: dict, tabla_pis: dict) -> dict:
    """Sobreancho por curva y ensanchamientos para el plugin. Devuelve {curvas, ensanchamientos, avisos}."""
    curvas, avisos = _curvas(tabla_pis)
    inicio = float(((tabla_pis or {}).get("inicio") or {}).get("progresiva") or 0.0)
    fin_eje = ((tabla_pis or {}).get("fin") or {}).get("progresiva")
    fin = float(fin_eje) if fin_eje is not None else float("inf")
    v = ctx["velocidad"]
    if v < V_MIN_FIGURA_KMH - n.TOLERANCIA or v > V_MAX_FIGURA_KMH + n.TOLERANCIA:
        avisos.append(f"V = {n._limpio(v)} km/h está fuera del rango de la figura 302.18B (30 a 80 km/h): se usa la fórmula "
                      f"de la 302.09 ({FORMULA_SA}).")
    if v > V_MAX_FACTOR_KMH + n.TOLERANCIA:
        avisos.append(f"Con V > 80 km/h «el valor del sobreancho será calculado para cada caso» ({_cita(base, REGLA_MAS_DE_80)}): "
                      "revisa cada curva; el sobreancho propuesto es el de la fórmula, en el borde interior.")
    cita_formula = _cita(base, FORMULA_SA)
    filas, zonas = [], []
    for p in curvas:
        radio = float(p["radio"])
        pc, pt = float(p["progresiva_pc"]), float(p["progresiva_pt"])
        borde = "izquierdo" if p.get("giro") == "izquierda" else "derecho" if p.get("giro") == "derecha" else None
        fila = {"pi": p.get("numero"), "radio": n._limpio(radio), "giro": p.get("giro"), "borde": borde,
                "progresiva_pc": n._limpio(pc), "progresiva_pt": n._limpio(pt),
                "datos": {"n": ctx["numero_carriles"], "R": n._limpio(radio), "L": n._limpio(ctx["l_m"]), "V": n._limpio(v)},
                "termino_geometrico": None, "termino_velocidad": None, "sobreancho_sin_factor": None,
                "factor_reduccion": None, "sobreancho_con_factor": None, "sobreancho": None, "se_aplica": False,
                "manda": None, "motivo": None, "ensanchamiento": None}
        filas.append(fila)
        if borde is None:
            fila["motivo"] = "La curva no tiene giro (izquierda o derecha) en listar_pis: no se sabe cuál es el borde interior."
            avisos.append(f"PI {p.get('numero')}: {fila['motivo']}")
            continue
        try:
            f = sobreancho_formula(ctx["numero_carriles"], radio, ctx["l_m"], v)
        except ValueError as e:
            fila["motivo"] = f"PI {p.get('numero')}: {e}."
            avisos.append(fila["motivo"])
            continue
        factor, sin_factor = factor_reduccion(base, ctx, radio)
        con_factor = f["sobreancho"] * factor["valor"] if factor else None
        sa = con_factor if factor else f["sobreancho"]
        fila.update({"termino_geometrico": round(f["termino_geometrico"], 4), "termino_velocidad": round(f["termino_velocidad"], 4),
                     "sobreancho_sin_factor": round(f["sobreancho"], 4), "factor_reduccion": factor,
                     "sobreancho_con_factor": round(con_factor, 4) if con_factor is not None else None,
                     "sobreancho": round(sa, 4),
                     "manda": f"Fórmula {cita_formula}: {FORMULA}" + (f" × factor {n._limpio(factor['valor'])} de la "
                                                                      f"{factor['referencia']} (a confirmar, P5)"
                                                                      if factor else "")})
        if sin_factor:
            fila["nota_factor"] = sin_factor
        if not n.cumple(sa, SOBREANCHO_MINIMO_M, n.MINIMO):
            fila["motivo"] = (f"Sa = {round(sa, 4)} m es menor de 0,40 m: «Se considera apropiado un valor mínimo de 0.40 m "
                              f"de sobreancho para justificar su adopción» ({_cita(base, REGLA_MINIMO)}). No se aplica "
                              "(a confirmar, P7).")
            avisos.append(f"PI {p.get('numero')}: {fila['motivo']}")
            continue
        fila["se_aplica"] = True
        lt = ctx["longitud_transicion"]
        cabe = min(lt, pc - inicio, fin - pt)
        if cabe < lt - n.TOLERANCIA:
            avisos.append(f"PI {p.get('numero')}: el desarrollo de {n._limpio(lt)} m no cabe entre el extremo del eje y la "
                          f"curva: se recorta a {n._limpio(max(cabe, 0.0))} m.")
        zonas.append({"borde": borde, "progresiva_inicial": pc, "progresiva_final": pt, "sobreancho": sa,
                      "longitud_transicion": max(cabe, 0.0), "pis": [p.get("numero")], "fila": fila})

    ensanchamientos = []
    for borde in ("izquierdo", "derecho"):
        propias = sorted((z for z in zonas if z["borde"] == borde), key=lambda z: z["progresiva_inicial"])
        unidas = []
        for z in propias:
            ultima = unidas[-1] if unidas else None
            if ultima and z["progresiva_inicial"] - z["longitud_transicion"] < ultima["progresiva_final"] + ultima["longitud_transicion"] - n.TOLERANCIA:
                avisos.append(f"Las zonas de sobreancho de los PI {', '.join(str(x) for x in ultima['pis'])} y {z['pis'][0]} "
                              f"(borde {borde}) se tocan: se unen en una sola, con el mayor sobreancho, de "
                              f"{n._limpio(ultima['progresiva_inicial'])} a {n._limpio(z['progresiva_final'])}.")
                ultima["progresiva_final"] = max(ultima["progresiva_final"], z["progresiva_final"])
                ultima["sobreancho"] = max(ultima["sobreancho"], z["sobreancho"])
                ultima["longitud_transicion"] = min(ultima["longitud_transicion"], z["longitud_transicion"])
                ultima["pis"] = ultima["pis"] + [z["pis"][0]]
                ultima["filas"].append(z["fila"])
            else:
                unidas.append({**z, "filas": [z["fila"]]})
        for z in unidas:
            e = {"borde": borde, "progresiva_inicial": round(z["progresiva_inicial"], 4),
                 "progresiva_final": round(z["progresiva_final"], 4), "sobreancho": round(z["sobreancho"], 4),
                 "longitud_transicion": round(z["longitud_transicion"], 4)}
            ensanchamientos.append(e)
            for fila in z["filas"]:
                fila["ensanchamiento"] = {**e, "pis": list(z["pis"])}
    return {"curvas": filas, "ensanchamientos": ensanchamientos, "avisos": avisos}


def desplazamiento(args: dict, ctx: dict) -> tuple[float, str, str | None]:
    """(media calzada, de dónde sale, aviso). El 'desplazamiento' del usuario manda."""
    mitad = ctx["ancho_calzada"] / 2.0
    d = args.get("desplazamiento")
    if d is None:
        return mitad, "ancho_calzada / 2", None
    aviso = None
    if abs(float(d) - mitad) > n.TOLERANCIA:
        aviso = (f"'desplazamiento' ({n._limpio(d)} m) no es la mitad de 'ancho_calzada' ({n._limpio(ctx['ancho_calzada'])} "
                 "m): manda el tuyo.")
    return float(d), "usuario", aviso


def pendientes(base: n.BaseNorma) -> list[dict]:
    """Las decisiones pendientes del dueño (P4 a P8) con la referencia citada de la base."""
    return [{**p, "referencia": _cita(base, p["referencia"])} for p in PENDIENTES]


def bloque_propuesta(base: n.BaseNorma, ctx: dict, propuesta: dict, d: float, origen_d: str, lecturas: dict) -> dict:
    return {
        "norma": n.NORMA,
        "seccion": SECCION,
        "referencia": n.texto_referencia(referencia_seccion()),
        "formula": FORMULA,
        "formula_referencia": _cita(base, FORMULA_SA),
        "criterio": CRITERIO,
        "proyecto": {"velocidad_diseno": n._limpio(ctx["velocidad"]), "numero_carriles": ctx["numero_carriles"],
                     "ancho_calzada": n._limpio(ctx["ancho_calzada"]), "vehiculo_diseno": ctx["vehiculo"],
                     "vehiculo_nombre": ctx["vehiculo_nombre"], "l_m": n._limpio(ctx["l_m"]),
                     "referencia_vehiculo": ctx["referencia_vehiculo"]},
        "desplazamiento": n._limpio(d),
        "desplazamiento_origen": origen_d,
        "longitud_transicion": n._limpio(ctx["longitud_transicion"]),
        "longitud_transicion_origen": ctx["longitud_transicion_origen"],
        "curvas": propuesta["curvas"],
        "ensanchamientos": propuesta["ensanchamientos"],
        "pendiente_de_decision": pendientes(base),
        "nota_redondeo": NOTA_REDONDEO,
        "avisos": propuesta["avisos"],
        "lecturas": lecturas,
    }


def argumentos_plugin(args: dict, d: float, ensanchamientos: list) -> dict:
    final = dict(args)
    final["desplazamiento"] = d
    final["ensanchamientos"] = json.dumps(ensanchamientos, ensure_ascii=False)
    return final

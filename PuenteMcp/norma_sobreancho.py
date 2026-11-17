# -*- coding: utf-8 -*-
"""crear_bordes_calzada con la norma (1.21.0): sobreancho de cada curva según la DG-2018 (sección 302.09).

El puente amplía la herramienta del plugin con cinco parámetros opcionales: velocidad_diseno, numero_carriles,
ancho_calzada, vehiculo_diseno y longitud_transicion. Con alguno de ellos, el puente:

1. valida los datos del proyecto; si falta alguno, responde 'faltan' sin llamar a Civil 3D;
2. lee las curvas del eje con listar_pis (y comprueba que el dibujo está en metros);
3. calcula el sobreancho de cada curva circular con la fórmula de la sección 302.09,
   Sa = n (R − √(R² − L²)) + V / (10 √R), y el factor de reducción de la T302.20 cuando aplica;
4. lo pone solo en el borde INTERIOR de la curva (giro a la izquierda → borde izquierdo), en el tramo del PC al PT,
   con una transición lineal de 40 m que termina en el PC y empieza en el PT (o longitud_transicion);
5. llama al plugin con esos ensanchamientos y devuelve su respuesta más 'propuesta_norma'.

Este módulo no usa la red ni Civil 3D: son funciones puras sobre lo que devuelve listar_pis y la base de la norma. La
escritura la sigue haciendo el plugin (Escritura.CrearObjeto): el puente no escribe nada en el dibujo.

Reglas (texto de la DG-2018, 302.09, páginas impresas 159 a 162, que la orquestadora leyó en el PDF el 17/11/2026):
- (a) «Se considera apropiado un valor mínimo de 0.40 m de sobreancho para justificar su adopción» y «los valores muy
  pequeños de sobreancho no deben considerarse»: si Sa < 0,40 m no se aplica (se avisa); no se sube a 0,40.
- (b) En curvas circulares simples el sobreancho se desarrolla «linealmente a lo largo del lado interno de la calzada,
  en la misma longitud utilizada para la transición del peralte»; con espiral, en la longitud de la espiral.
  «Normalmente la longitud para desarrollar el sobreancho será de 40 m»: por defecto 40 m terminando en el PC y
  empezando en el PT, o longitud_transicion. Usar la transición del peralte (la calcula la tarea 6.5) queda como
  pendiente de decisión del dueño.
- (c) Redondeo: no consta en el texto; se devuelve el valor exacto (pendiente del esquema de la 6.4).
- (d) T302.20, «Factores de reducción del sobreancho para anchos de calzada en tangente de 7.20 m», por radio, asociada a
  V < 80 km/h. Para V > 80 «el valor del sobreancho será calculado para cada caso»: fórmula sin factor y aviso.
- Velocidades fuera del rango de la figura 302.18B (30 a 80 km/h): fórmula y aviso.

ESQUEMA PROVISIONAL: las tablas del sobreancho las añade la tarea 6.4 a dg2018.sqlite. Hasta que llegue su esquema, las
consultas (todas en las funciones de «base», abajo) usan el de la base de ejemplo de las pruebas
(tests/base_norma_ejemplo.py): sobreancho_vehiculos y sobreancho_reduccion. Cuando llegue el esquema real solo cambian
esas funciones, TABLAS_SOBREANCHO y la base de ejemplo. Ningún valor de la norma está escrito en este archivo, salvo los
umbrales que cita el texto (0,40 m, 40 m, 7,20 m, 30 y 80 km/h).
"""
import json
import math

import norma as n

# Parámetros que el puente añade a crear_bordes_calzada: nombre → (tipo de Python, descripción).
CAMPOS_PROYECTO = {
    "velocidad_diseno": (float, "Velocidad de diseño en km/h. Con este parámetro y los otros tres datos del proyecto "
                                "(numero_carriles, ancho_calzada, vehiculo_diseno) el puente calcula el sobreancho de "
                                "cada curva con la DG-2018 (302.09)"),
    "numero_carriles": (int, "Número de carriles de la calzada (n de la fórmula del sobreancho, 302.09)"),
    "ancho_calzada": (float, "Ancho de la calzada en tangente, en m (decide el factor de reducción de la T302.20 y, si "
                             "no pasas 'desplazamiento', la media calzada = ancho_calzada / 2)"),
    "vehiculo_diseno": (str, "Vehículo de diseño (da L, la distancia entre el eje delantero y el posterior); si no está "
                             "en la base, la respuesta 'faltan' trae los válidos"),
    "longitud_transicion": (float, "Longitud (m) de la transición lineal del sobreancho, antes del PC y después del PT. "
                                   "Por defecto 40 m (302.09)"),
}
OBLIGATORIOS = ("velocidad_diseno", "numero_carriles", "ancho_calzada", "vehiculo_diseno")
# Lo que el plugin tiene que publicar para que el puente amplíe la herramienta.
PARAMETROS_NECESARIOS = {"eje", "desplazamiento", "ensanchamientos", "simular"}
# Parámetro obligatorio del plugin que con la norma pasa a ser opcional (sale de ancho_calzada / 2).
PARAMETRO_OPCIONAL = "desplazamiento"

DESCRIPCION = (
    "\n\nCON LA NORMA DG-2018 (puente, desde 1.21.0): con 'velocidad_diseno', 'numero_carriles', 'ancho_calzada' y "
    "'vehiculo_diseno' dentro de 'args' (y 'longitud_transicion' opcional), el puente lee las curvas del eje con "
    "listar_pis, calcula el sobreancho de cada curva con la fórmula de la sección 302.09, Sa = n(R − √(R² − L²)) + "
    "V/(10√R), y el factor de reducción de la T302.20 cuando aplica, y lo pone solo en el borde INTERIOR de la curva, "
    "del PC al PT, con una transición lineal de 40 m (o longitud_transicion) antes del PC y después del PT. Un "
    "sobreancho menor de 0,40 m no se aplica. No pases 'ensanchamientos' (los calcula el puente) ni 'desplazamiento' "
    "salvo que el usuario lo pida (por defecto ancho_calzada / 2). Si falta un dato responde 'faltan' sin llamar a "
    "Civil 3D: pregúntalo al usuario, no lo supongas. Devuelve la respuesta del plugin más 'propuesta_norma' (por curva: "
    "R, V, n, L, los términos de la fórmula, el factor, el sobreancho, si se aplica, el borde y las progresivas; qué "
    "manda; y 'pendiente_de_decision'). Simula primero (simular=true). Necesita MEMORIA_FIRMA_DIR."
)
DESCRIPCION_DESPLAZAMIENTO = " Con 'velocidad_diseno' no hace falta: por defecto ancho_calzada / 2."

SECCION = "302.09"
PAGINAS_IMPRESAS = "159 a 162"
REF_REDUCCION = "T302.20"
ANCHO_TABLA_REDUCCION_M = 7.20     # título de la T302.20: «para anchos de calzada en tangente de 7.20 m»
V_MIN_FIGURA_KMH = 30.0            # rango de la figura 302.18B
V_MAX_FIGURA_KMH = 80.0
V_MAX_FACTOR_KMH = 80.0            # por encima, «calculado para cada caso»: sin factor
SOBREANCHO_MINIMO_M = 0.40         # «un valor mínimo de 0.40 m ... para justificar su adopción»
LONGITUD_TRANSICION_M = 40.0       # «Normalmente la longitud para desarrollar el sobreancho será de 40 m»
TABLAS_SOBREANCHO = ("sobreancho_vehiculos", "sobreancho_reduccion")   # esquema provisional (ver arriba)

FORMULA = "Sa = n (R − √(R² − L²)) + V / (10 √R)"
CRITERIO = (
    "Por curva circular: Sa = n (R − √(R² − L²)) + V / (10 √R) (302.09), por el factor de reducción de la T302.20 si la "
    "calzada en tangente es de 7,20 m, V no pasa de 80 km/h y el radio está en la tabla. Sin redondear. Si Sa < 0,40 m "
    "no se aplica. Solo en el borde interior (giro a la izquierda → borde izquierdo), sobreancho pleno del PC al PT y "
    "transición lineal de 40 m (o longitud_transicion) que termina en el PC y empieza en el PT, recortada si no cabe "
    "en el eje. Si las zonas de dos curvas del mismo borde se tocan, se unen con el mayor sobreancho."
)
PENDIENTE_TRANSICION = {
    "tema": "Longitud de la transición del sobreancho en curvas circulares simples",
    "texto_norma": "«linealmente a lo largo del lado interno de la calzada, en la misma longitud utilizada para la "
                   "transición del peralte» (302.09)",
    "se_usa": "40 m terminando en el PC y empezando en el PT («Normalmente la longitud para desarrollar el sobreancho "
              "será de 40 m»), o 'longitud_transicion'",
    "quien_decide": "Jose",
    "nota": "La longitud de transición del peralte la calcula la tarea 6.5.",
}
PENDIENTE_REDONDEO = {
    "tema": "Redondeo del sobreancho",
    "se_usa": "el valor exacto de la fórmula, sin redondear",
    "nota": "El texto de la 302.09 no lo dice; lo confirma el esquema de la tarea 6.4.",
}


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


# ---------------------------------------------------------------------------------------------- base (esquema provisional)
def comprobar_tablas(base: n.BaseNorma):
    """Lanza ErrorNorma si la base aún no tiene las tablas del sobreancho (las añade la tarea 6.4)."""
    existentes = {r["name"] for r in base.filas("SELECT name FROM sqlite_master WHERE type IN ('table', 'view')")}
    faltan = [t for t in TABLAS_SOBREANCHO if t not in existentes]
    if faltan:
        raise n.ErrorNorma(f"La base de la norma {base.ruta} no tiene todavía las tablas del sobreancho: {', '.join(faltan)}. "
                           "Actualiza memoria-firma (git pull): las añade la tarea 6.4. No se llamó a Civil 3D.")


def vehiculos(base: n.BaseNorma) -> list[dict]:
    """Vehículos de diseño con su L (m), en el orden de la base."""
    return base.filas("SELECT vehiculo, l_m, ref FROM sobreancho_vehiculos ORDER BY orden")


def factores_reduccion(base: n.BaseNorma, ancho_calzada: float) -> list[dict]:
    """Filas de la T302.20 para ese ancho de calzada en tangente (radio_m, factor)."""
    return base.filas("SELECT radio_m, factor, ref FROM sobreancho_reduccion WHERE abs(ancho_calzada_m - ?) < 0.001 "
                      "ORDER BY radio_m", float(ancho_calzada))


# ---------------------------------------------------------------------------------------------- datos del proyecto
def _numero(valor):
    if valor is None or isinstance(valor, bool):
        return None
    try:
        v = float(str(valor).replace(",", ".")) if isinstance(valor, str) else float(valor)
    except ValueError:
        return None
    return v if math.isfinite(v) else None


def resolver(base: n.BaseNorma, proyecto: dict) -> tuple[dict | None, list]:
    """Comprueba los datos del proyecto. Devuelve (contexto, faltan); si 'faltan' no está vacío, hay que preguntarlos."""
    faltan = []
    lista_vehiculos = vehiculos(base)
    nombres = [v["vehiculo"] for v in lista_vehiculos]

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
    elegido = n._elegir(proyecto.get("vehiculo_diseno"), nombres)
    if n._vacio(proyecto.get("vehiculo_diseno")):
        faltan.append(n._falta("vehiculo_diseno", "Vehículo de diseño: da L, la distancia entre el eje delantero y el "
                                                  "posterior.", nombres))
    elif elegido is None:
        faltan.append(n._falta("vehiculo_diseno", f"'{proyecto.get('vehiculo_diseno')}' no es un vehículo de la base.", nombres))
    lt = _numero(proyecto.get("longitud_transicion"))
    if proyecto.get("longitud_transicion") is not None and (lt is None or lt <= 0):
        faltan.append(n._falta("longitud_transicion", "Longitud de la transición del sobreancho en m (> 0); sin ella, 40 m "
                                                      "(302.09)."))
    if faltan:
        return None, faltan
    fila = next(x for x in lista_vehiculos if x["vehiculo"] == elegido)
    return {
        "velocidad": v,
        "numero_carriles": int(round(carriles)),
        "ancho_calzada": ancho,
        "vehiculo": elegido,
        "l_m": float(fila["l_m"]),
        "referencia_vehiculo": n.texto_referencia(base.referencia(fila["ref"])) if fila.get("ref") else None,
        "longitud_transicion": lt if lt is not None else LONGITUD_TRANSICION_M,
        "longitud_transicion_origen": "usuario" if lt is not None else "por defecto, 40 m (302.09)",
    }, []


# ---------------------------------------------------------------------------------------------- cálculo
def sobreancho_formula(carriles: int, radio: float, l_m: float, v_kmh: float) -> dict:
    """Sa = n (R − √(R² − L²)) + V / (10 √R), con sus dos términos. Lanza ValueError si R ≤ L."""
    if radio <= l_m:
        raise ValueError(f"el radio ({n._limpio(radio)} m) no es mayor que L ({n._limpio(l_m)} m): la fórmula no vale")
    geometrico = carriles * (radio - math.sqrt(radio * radio - l_m * l_m))
    velocidad = v_kmh / (10.0 * math.sqrt(radio))
    return {"termino_geometrico": geometrico, "termino_velocidad": velocidad, "sobreancho": geometrico + velocidad}


def factor_reduccion(base: n.BaseNorma, ctx: dict, radio: float) -> tuple[dict | None, str | None]:
    """(factor, motivo por el que no se aplica). Solo con la calzada de 7,20 m, V ≤ 80 y el radio en la tabla."""
    if ctx["velocidad"] > V_MAX_FACTOR_KMH + n.TOLERANCIA:
        return None, (f"V = {n._limpio(ctx['velocidad'])} km/h pasa de 80: la norma pide calcular cada caso, así que se usa la "
                      f"fórmula sin el factor de la {REF_REDUCCION}.")
    if abs(ctx["ancho_calzada"] - ANCHO_TABLA_REDUCCION_M) > n.TOLERANCIA:
        return None, (f"La {REF_REDUCCION} es para calzadas de 7,20 m en tangente y la del proyecto es de "
                      f"{n._limpio(ctx['ancho_calzada'])} m: no se aplica factor (si vale para otros anchos lo dirá la base "
                      "de la tarea 6.4).")
    filas = factores_reduccion(base, ctx["ancho_calzada"])
    fila = next((f for f in filas if abs(float(f["radio_m"]) - radio) <= n.TOLERANCIA), None)
    if fila is None:
        return None, (f"El radio {n._limpio(radio)} m no está en la {REF_REDUCCION}: no se aplica factor (si se interpola "
                      "lo dirá la base de la tarea 6.4).")
    return {"valor": float(fila["factor"]), "radio_tabla": float(fila["radio_m"]),
            "referencia": n.texto_referencia(base.referencia(fila.get("ref") or REF_REDUCCION))}, None


def _curvas(tabla_pis: dict) -> tuple[list, list]:
    """(curvas circulares de listar_pis con radio, PC, PT y giro; avisos de las filas no soportadas)."""
    curvas, avisos = [], []
    for p in (tabla_pis or {}).get("pis") or []:
        tipo = p.get("tipo")
        if tipo == "curva" and p.get("radio") and p.get("progresiva_pc") is not None and p.get("progresiva_pt") is not None:
            curvas.append(p)
        elif tipo not in ("curva", "sin_curva"):
            avisos.append(f"PI {p.get('numero')}: tipo '{tipo}' no soportado (solo curvas circulares): no se calcula su "
                          "sobreancho. Con espiral, la norma lo desarrolla en la longitud de la espiral; hazlo con "
                          "'ensanchamientos' a mano.")
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
                      "de la 302.09.")
    filas, zonas = [], []
    for p in curvas:
        radio = float(p["radio"])
        pc, pt = float(p["progresiva_pc"]), float(p["progresiva_pt"])
        borde = "izquierdo" if p.get("giro") == "izquierda" else "derecho" if p.get("giro") == "derecha" else None
        fila = {"pi": p.get("numero"), "radio": n._limpio(radio), "giro": p.get("giro"), "borde": borde,
                "progresiva_pc": n._limpio(pc), "progresiva_pt": n._limpio(pt),
                "datos": {"n": ctx["numero_carriles"], "R": n._limpio(radio), "L": n._limpio(ctx["l_m"]), "V": n._limpio(v)},
                "termino_geometrico": None, "termino_velocidad": None, "sobreancho_formula": None,
                "factor_reduccion": None, "sobreancho": None, "se_aplica": False, "manda": None, "motivo": None,
                "ensanchamiento": None}
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
        sa = f["sobreancho"] * (factor["valor"] if factor else 1.0)
        fila.update({"termino_geometrico": n._limpio(f["termino_geometrico"]), "termino_velocidad": n._limpio(f["termino_velocidad"]),
                     "sobreancho_formula": round(f["sobreancho"], 4), "factor_reduccion": factor,
                     "sobreancho": round(sa, 4),
                     "manda": f"Fórmula de la sección 302.09 ({FORMULA})" + (f" × factor {n._limpio(factor['valor'])} de la "
                                                                             f"{factor['referencia']}" if factor else "")})
        if sin_factor:
            fila["nota_factor"] = sin_factor
        if not n.cumple(sa, SOBREANCHO_MINIMO_M, n.MINIMO):
            fila["motivo"] = (f"Sa = {round(sa, 4)} m es menor de 0,40 m: «los valores muy pequeños de sobreancho no deben "
                              "considerarse» (302.09). No se aplica.")
            continue
        fila["se_aplica"] = True
        lt = ctx["longitud_transicion"]
        cabe = min(lt, pc - inicio, fin - pt)
        if cabe < lt - n.TOLERANCIA:
            avisos.append(f"PI {p.get('numero')}: la transición de {n._limpio(lt)} m no cabe entre el eje y la curva: se "
                          f"recorta a {n._limpio(max(cabe, 0.0))} m.")
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
                ultima["pis"].append(z["pis"][0])
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


def bloque_propuesta(ctx: dict, propuesta: dict, d: float, origen_d: str, lecturas: dict) -> dict:
    return {
        "norma": n.NORMA,
        "seccion": SECCION,
        "referencia": n.texto_referencia(referencia_seccion()),
        "formula": FORMULA,
        "criterio": CRITERIO,
        "proyecto": {"velocidad_diseno": n._limpio(ctx["velocidad"]), "numero_carriles": ctx["numero_carriles"],
                     "ancho_calzada": n._limpio(ctx["ancho_calzada"]), "vehiculo_diseno": ctx["vehiculo"],
                     "l_m": n._limpio(ctx["l_m"]), "referencia_vehiculo": ctx["referencia_vehiculo"]},
        "desplazamiento": n._limpio(d),
        "desplazamiento_origen": origen_d,
        "longitud_transicion": n._limpio(ctx["longitud_transicion"]),
        "longitud_transicion_origen": ctx["longitud_transicion_origen"],
        "curvas": propuesta["curvas"],
        "ensanchamientos": propuesta["ensanchamientos"],
        "pendiente_de_decision": [PENDIENTE_TRANSICION, PENDIENTE_REDONDEO],
        "avisos": propuesta["avisos"],
        "lecturas": lecturas,
    }


def argumentos_plugin(args: dict, d: float, ensanchamientos: list) -> dict:
    final = dict(args)
    final["desplazamiento"] = d
    final["ensanchamientos"] = json.dumps(ensanchamientos, ensure_ascii=False)
    return final

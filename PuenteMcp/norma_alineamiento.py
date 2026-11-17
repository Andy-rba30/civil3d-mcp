# -*- coding: utf-8 -*-
"""crear_alineamiento_desde_polilinea con la norma (1.19.0): propone el radio mínimo de la DG-2018 en cada PI.

El puente amplía la herramienta del plugin con seis parámetros opcionales (velocidad_diseno y los datos del proyecto de
verificar_norma). Con alguno de ellos, el puente:

1. valida los datos del proyecto con la misma lógica de verificar_norma (norma.preparar); si falta algo, responde sin
   llamar al plugin;
2. comprueba que el dibujo está en metros (INSUNITS = 6, con leer_variable);
3. simula la creación una vez (simulación de ensayo) para leer los PI de la polilínea;
4. propone el radio mínimo de la norma en cada PI y comprueba que cabe dejando la tangente mínima entre curvas;
5. llama al plugin con esos radios (radios_por_pi) y adjunta la verificación de verificar_norma.

Este módulo no usa la red ni Civil 3D: son funciones puras sobre lo que devuelve el plugin y la base de la norma. Las
llamadas al plugin las hace main.py. La escritura la sigue haciendo el plugin (Escritura.CrearObjeto): el puente no
escribe nada en el dibujo.

Criterios (plan 4.3, aprobados el 17/11/2026):
- Mínimo de un PI: el mayor de (a) el radio mínimo (T302.02/T302.04, la más exigente; radio_minimo_exigido), (b) el
  radio que permite prescindir de la curva de transición (T302.11A, «igual o mayor»; T302.11B, «superior»: igual no
  basta), porque el plugin solo crea arcos, y (c) con deflexión de 5° o menos, L > 30 (10 − Δ) (estricto). Redondeado
  hacia arriba a un múltiplo de 5 m (como las tablas de la norma); en una desigualdad estricta, si el redondeo cae en la
  igualdad, 5 m más.
- Sin curva (0) si los vértices están alineados o si la deflexión no pasa de la del cuadro de deflexión sin curva.
- 3V/6V solo es una nota; la relación de radios y el radio tras una recta larga de autopista no se imponen (los
  muestra la verificación).
- Cabida en el orden de los PI (como el plugin: la primera curva tiene prioridad), dejando entre dos curvas la
  tangente mínima (T302.01 y, del mismo sentido, también T304.08: la más exigente). Si el mínimo no cabe: aviso y se
  pide el mayor radio que cabe con la tangente mínima, hacia abajo a 5 m; si no llega a 5 m, el mayor que admite el
  plugin (sin la tangente mínima); si tampoco, 0. Siempre anunciado.
- Los radios del usuario ('radio' para los PI que 'radios_por_pi' no nombra, 'radios_por_pi' para los que nombra)
  mandan; se avisa si quedan por debajo del mínimo o si el plugin los dejará sin curva.
"""
import json
import math

import norma as n

# Parámetros que el puente añade a crear_alineamiento_desde_polilinea: nombre → (tipo de Python, descripción).
CAMPOS_PROYECTO = {
    "velocidad_diseno": (float, "Velocidad de diseño en km/h (una celda de la T204.01; la norma no permite interpolar). "
                                "Con este parámetro el puente propone el radio mínimo de la DG-2018 en cada PI"),
    "clasificacion": (str, "Clasificación de la vía según la DG-2018 ('Carretera de tercera clase'...), como en verificar_norma"),
    "orografia": (str, "Orografía: Plano, Ondulado, Accidentado o Escarpado (o 1 a 4), como en verificar_norma"),
    "ubicacion": (str, "Ubicación: rural, urbana o rural con peligro de hielo (decide la fila de la T302.02), como en verificar_norma"),
    "peralte_maximo": (float, "Peralte máximo del proyecto en % (Tercera Clase no pavimentada o terreno accidentado), como en verificar_norma"),
    "pavimentada": (bool, "Tercera Clase: si está pavimentada (se aplican las reglas de Segunda Clase, 101.05), como en verificar_norma"),
}
# Lo que el plugin tiene que publicar para que el puente amplíe la herramienta.
PARAMETROS_NECESARIOS = {"nombre", "radio", "radios_por_pi", "simular"}

DESCRIPCION = (
    "\n\nCON LA NORMA DG-2018 (puente, desde 1.19.0): con 'velocidad_diseno' y los datos del proyecto ('clasificacion', "
    "'orografia', 'ubicacion', 'peralte_maximo', 'pavimentada': los mismos de verificar_norma), dentro de 'args', el "
    "puente propone el radio mínimo de la norma en cada PI: el mayor entre el radio mínimo (T302.02/T302.04), el radio "
    "sin curva de transición (T302.11) y L > 30(10 − Δ) con deflexión pequeña, redondeado hacia arriba a 5 m; 0 (sin "
    "curva) si la deflexión cabe en el cuadro de deflexión sin curva. Comprueba que cada curva cabe dejando la tangente "
    "mínima entre curvas y crea el eje con esos radios. No pases 'radio' ni 'radios_por_pi' salvo que el usuario los "
    "pida: los suyos mandan y se avisa si quedan por debajo del mínimo. Si el mínimo no cabe, avisa ('la polilínea no "
    "permite cumplir la norma en el PI n') y pide el mayor radio que cabe. Si falta un dato del proyecto responde "
    "'faltan' sin llamar a Civil 3D: pregúntalo al usuario, no lo supongas. Lee INSUNITS (debe ser 6, metros). "
    "Devuelve la respuesta del plugin más 'propuesta_norma' (por PI: mínimos con tabla, sección y página, el que manda, "
    "el máximo que cabe, el radio pedido y el aviso) y 'verificacion' (la de verificar_norma; prevista con "
    "simular=true). Simula primero (simular=true). Necesita MEMORIA_FIRMA_DIR."
)
DESCRIPCION_RADIO = " Con 'velocidad_diseno' no hace falta (el puente propone uno por PI); si lo pasas, manda el tuyo."

PASO_REDONDEO_M = 5.0           # las tablas de la norma redondean los radios a 5 m
RADIO_MINIMO_PEDIBLE_M = 5.0    # por debajo de esto no se pide curva cuando el mínimo no cabe
MARGEN_CABIDA_M = 0.001         # margen al comprobar que una tangente cabe (el plugin redondea a 4 decimales)
MARGEN_ESTRICTO_M = 0.01        # margen en L > 30 (10 − Δ), por el redondeo de la deflexión
RADIO_PROVISIONAL = 1.0         # radio de la simulación de ensayo: el plugin lo exige > 0 y aquí no se usa
DEFLEXION_NULA_GRADOS = 1e-4    # vértices alineados
INSUNITS_METROS = "6"
INSUNITS_NOMBRES = {"0": "sin unidades", "1": "pulgadas", "2": "pies", "3": "millas", "4": "milímetros",
                    "5": "centímetros", "6": "metros", "7": "kilómetros", "10": "yardas"}

CRITERIO = (
    "Por PI, el mayor de los mínimos que obligan: radio mínimo (T302.02/T302.04, la más exigente), radio sin curva de "
    "transición (T302.11A, igual o mayor; T302.11B, superior) porque el plugin solo crea arcos, y L > 30 (10 − Δ) con "
    "deflexión de 5° o menos. Redondeado hacia arriba a un múltiplo de 5 m (como las tablas de la norma). Sin curva si "
    "la deflexión cabe en el cuadro de deflexión sin curva. La longitud 3V/6V solo es una nota. Cabida en el orden de "
    "los PI (la primera curva tiene prioridad, como en el plugin), dejando entre dos curvas la tangente mínima (T302.01 "
    "y, del mismo sentido, T304.08: la más exigente) y 1 mm de margen. Si el mínimo no cabe: el mayor radio que cabe "
    "con la tangente mínima, hacia abajo a 5 m; si no llega a 5 m, el mayor que cabe sin ella; si tampoco, sin curva. "
    "Los radios del usuario mandan."
)


# ---------------------------------------------------------------------------------------------- llamada
def campos_que_chocan(parametros: list) -> list:
    """Parámetros del plugin con el mismo nombre que uno de los del puente (entonces no se amplía la herramienta)."""
    return sorted({p.get("name") for p in parametros} & set(CAMPOS_PROYECTO))


def se_puede_ampliar(parametros: list) -> bool:
    nombres = {p.get("name") for p in parametros}
    return PARAMETROS_NECESARIOS <= nombres and not campos_que_chocan(parametros)


def es_con_norma(datos: dict) -> bool:
    """La llamada usa la norma si trae cualquiera de los datos del proyecto (no solo velocidad_diseno)."""
    return any(datos.get(c) is not None for c in CAMPOS_PROYECTO)


def separar(datos: dict) -> tuple[dict, dict]:
    """(argumentos para el plugin, datos del proyecto para la norma)."""
    plugin = {k: v for k, v in datos.items() if k not in CAMPOS_PROYECTO}
    proyecto = {k: datos.get(k) for k in CAMPOS_PROYECTO}
    return plugin, proyecto


def respuesta_faltan(faltan: list) -> dict:
    return {
        "ok": False,
        "error": "Faltan datos del proyecto para proponer los radios de la DG-2018: " + ", ".join(f["dato"] for f in faltan)
                 + ". Pregúntalos al usuario con los valores válidos; no los supongas. No se llamó a Civil 3D: ni copia, "
                   "ni Deshacer, ni línea en mcp_log.jsonl.",
        "faltan": faltan,
    }


def unidades_no_metros(resultado) -> str | None:
    """Motivo para no proponer radios si INSUNITS no es 6 (metros); None si el dibujo está en metros."""
    valor = resultado.get("valor") if isinstance(resultado, dict) else None
    valor = "" if valor is None else str(valor).strip()
    if valor == INSUNITS_METROS:
        return None
    nombre = INSUNITS_NOMBRES.get(valor, "unidades desconocidas")
    return (f"El dibujo no está en metros (INSUNITS = {valor or 'vacío'}, {nombre}): la DG-2018 está en metros y los "
            "radios propuestos estarían mal. No se llamó a crear_alineamiento_desde_polilinea. Si el usuario confirma "
            "que el dibujo está en metros (Configuración del dibujo → Unidades y zona), que ponga INSUNITS en 6 y "
            "repite; o crea el eje sin velocidad_diseno, con sus radios.")


# ---------------------------------------------------------------------------------------------- redondeo
def arriba_5(valor: float) -> float:
    """Hacia arriba al múltiplo de 5 m (125 → 125; 123.2 → 125)."""
    return math.ceil(valor / PASO_REDONDEO_M - 1e-9) * PASO_REDONDEO_M


def abajo_5(valor: float) -> float:
    """Hacia abajo al múltiplo de 5 m (217 → 215; 4.9 → 0)."""
    if valor <= 0:
        return 0.0
    return math.floor(valor / PASO_REDONDEO_M + 1e-9) * PASO_REDONDEO_M


def _tan_mitad(deflexion_grados: float) -> float:
    return math.tan(math.radians(deflexion_grados) / 2.0)


def tangente(radio: float, deflexion_grados: float) -> float:
    """T = R·tan(Δ/2) (F38)."""
    return radio * _tan_mitad(deflexion_grados)


# ---------------------------------------------------------------------------------------------- mínimos de la norma
def _minimo(comprobacion: str, valor_exigido, unidad: str, criterio: str, radio_exacto: float, radio_necesario: float,
            referencia: dict, **detalle) -> dict:
    salida = {
        "comprobacion": comprobacion,
        "valor_exigido": n._limpio(valor_exigido),
        "unidad": unidad,
        "criterio": criterio,
        "radio_exacto": n._limpio(radio_exacto),
        "radio_necesario": n._limpio(radio_necesario),
        "referencia": referencia,
    }
    salida.update({k: (n._limpio(v) if isinstance(v, float) else v) for k, v in detalle.items() if v is not None})
    return salida


def minimos_del_proyecto(base: n.BaseNorma, ctx: dict) -> list[dict]:
    """Los mínimos de radio que no dependen del PI: radio mínimo (T302.02/T302.04) y radio sin transición (T302.11)."""
    minimos = []
    ex = n.radio_minimo_exigido(base, ctx)
    if ex["exigido"] is not None:
        minimos.append(_minimo("Radio mínimo", ex["exigido"], "m", n.MINIMO, ex["exigido"], arriba_5(ex["exigido"]),
                               ex["referencia"], valor_formula=ex["valor_formula"],
                               formula=n._formula(base, ex["formula"], ex["valor_formula"]),
                               otra_tabla=ex["otra_tabla"], aviso=ex["aviso"]))
    ident = "T302.11B" if ctx["tercera_clase"] else "T302.11A"
    r = base.radio_sin_transicion(ident, ctx["velocidad"])
    if r and r["radio_m"] is not None:
        estricto = ident == "T302.11B"
        necesario = arriba_5(r["radio_m"])
        if estricto and necesario - r["radio_m"] <= n.TOLERANCIA:
            necesario += PASO_REDONDEO_M
        minimos.append(_minimo("Radio para prescindir de la curva de transición", r["radio_m"], "m",
                               n.MAYOR_ESTRICTO if estricto else n.MINIMO, r["radio_m"], necesario, base.referencia(ident),
                               nota=("«Superior» al de la tabla: igual no basta." if estricto else None)))
    return minimos


def minimo_deflexion_pequena(base: n.BaseNorma, deflexion_grados: float) -> dict | None:
    """Con Δ de 5° o menos, L > 30 (10 − Δ) (F37, R302.02-1, estricto): R > 30 (10 − Δ) / Δ(rad)."""
    if deflexion_grados is None or deflexion_grados > n.DEFLEXION_PEQUENA_GRADOS + n.TOLERANCIA:
        return None
    longitud = n.longitud_deflexion_pequena(deflexion_grados)
    delta = math.radians(deflexion_grados)
    exacto = longitud / delta
    necesario = arriba_5(exacto)
    while necesario * delta - longitud <= MARGEN_ESTRICTO_M:
        necesario += PASO_REDONDEO_M
    return _minimo("Longitud de curva con deflexión pequeña: L > 30 (10 − Δ)", longitud, "m de curva", n.MAYOR_ESTRICTO,
                   exacto, necesario, base.referencia("R302.02-1"), formula=n._formula(base, "F37", longitud),
                   nota=f"L = R·Δ: con Δ = {n._limpio(deflexion_grados)}° hace falta R > {n._limpio(exacto)} m.")


def deflexion_sin_curva(base: n.BaseNorma, ctx: dict, deflexion_grados: float) -> dict | None:
    """Si la deflexión no pasa de la del cuadro de deflexión sin curva (30 a 80 km/h), el PI puede ir sin curva."""
    r = base.deflexion_sin_curva(ctx["velocidad"])
    if not r or deflexion_grados is None or not n.cumple(deflexion_grados, r["deflexion_max_grados"], n.MAXIMO):
        return None
    return {"deflexion_max_grados": n._limpio(r["deflexion_max_grados"]), "texto": r["deflexion_texto_pdf"],
            "referencia": base.referencia("C302.02-DEFLEX"), "regla": n._regla(base, "R302.02-4")}


def tangente_minima(base: n.BaseNorma, ctx: dict, mismo_sentido: bool) -> dict:
    """Tangente mínima entre dos curvas: T302.01 (L mín.s o L mín.o) y, del mismo sentido, T304.08; la más exigente.
    Sin fila en las tablas, la fórmula (F34 o F35)."""
    v = ctx["velocidad"]
    t = base.longitud_tangente(v)
    if mismo_sentido:
        t2 = base.tangente_mismo_sentido(v)
        candidatas = [(t["l_min_o_m"] if t else None, "T302.01"), (t2["longitud_min_m"] if t2 else None, "T304.08")]
        formula, ident_f = n.tangente_minima_o(v), "F35"
    else:
        candidatas = [(t["l_min_s_m"] if t else None, "T302.01")]
        formula, ident_f = n.tangente_minima_s(v), "F34"
    candidatas = [(valor, ref) for valor, ref in candidatas if valor is not None]
    if not candidatas:
        return {"valor": n._limpio(formula), "referencia": n.texto_referencia(base.referencia(ident_f)),
                "nota": f"La tabla no tiene fila para {v} km/h: se reserva la fórmula."}
    valor, ref = max(candidatas, key=lambda c: c[0])
    salida = {"valor": n._limpio(valor), "referencia": n.texto_referencia(base.referencia(ref))}
    otras = [c for c in candidatas if c[1] != ref]
    if otras:
        salida["otra_tabla"] = {"referencia": n.texto_referencia(base.referencia(otras[0][1])), "valor": n._limpio(otras[0][0])}
    return salida


def _longitud_minima_curva(base: n.BaseNorma, ctx: dict, deflexion_grados: float) -> dict | None:
    lm = base.longitud_minima_curva(ctx["autopista"])
    if not lm or not deflexion_grados:
        return None
    longitud = lm["factor_v"] * ctx["velocidad"]
    return {"longitud": longitud, "expresion": lm["expresion_pdf"],
            "radio": arriba_5(longitud / math.radians(deflexion_grados)),
            "referencia": n.texto_referencia(base.referencia("C302.02-LMIN"))}


# ---------------------------------------------------------------------------------------------- geometría y radios del usuario
def geometria(despues: dict) -> dict:
    """PI y tramos de la polilínea a partir del 'despues' de la simulación del plugin: tramo inicial = progresiva del
    PI 1 − progresiva inicial; intermedios = distancia entre PI; final = progresiva final − (PT o PI) + T. No se usa
    el radio_maximo del plugin (depende de los radios vecinos)."""
    despues = despues or {}
    pis = sorted(despues.get("pis") or [], key=lambda p: p.get("numero") or 0)
    inicio = float(despues.get("progresiva_inicial") or 0.0)
    fin = despues.get("progresiva_final")
    if fin is None:
        raise n.ErrorNorma("La simulación del plugin no devolvió 'progresiva_final': no se pueden medir los tramos. "
                           "No se creó nada.")
    fin = float(fin)
    tramos = []
    if not pis:
        tramos.append(fin - inicio)
    else:
        tramos.append(float(pis[0]["progresiva"]) - inicio)
        for a, b in zip(pis, pis[1:]):
            tramos.append(math.hypot(float(b["x"]) - float(a["x"]), float(b["y"]) - float(a["y"])))
        ultimo = pis[-1]
        if ultimo.get("radio_aplicado") is not None and ultimo.get("progresiva_pt") is not None:
            tramos.append(fin - float(ultimo["progresiva_pt"]) + float(ultimo.get("tangente") or 0.0))
        else:
            tramos.append(fin - float(ultimo["progresiva"]))
    if any(t <= 0 for t in tramos):
        raise n.ErrorNorma(f"La simulación del plugin dio tramos de longitud no positiva ({[n._limpio(t) for t in tramos]}): "
                           "no se puede proponer. No se creó nada.")
    return {"pis": [{"numero": int(p["numero"]), "progresiva": p.get("progresiva"), "x": p.get("x"), "y": p.get("y"),
                     "deflexion_grados": float(p.get("deflexion_grados") or 0.0), "giro": p.get("giro")} for p in pis],
            "tramos": tramos, "progresiva_inicial": inicio, "progresiva_final": fin}


def _radio_de_texto(valor, donde: str) -> float:
    if valor is None:
        return 0.0
    if isinstance(valor, bool):
        raise n.ErrorNorma(f"El radio de {donde} en 'radios_por_pi' debe ser un número (0 o null = sin curva).")
    if isinstance(valor, (int, float)):
        r = float(valor)
    else:
        try:
            r = float(str(valor).replace(",", "."))
        except ValueError:
            raise n.ErrorNorma(f"El radio de {donde} en 'radios_por_pi' debe ser un número (0 o null = sin curva): {valor!r}")
    if math.isnan(r) or math.isinf(r) or r < 0:
        raise n.ErrorNorma(f"El radio de {donde} en 'radios_por_pi' no puede ser negativo (0 o null = sin curva).")
    return r


def radios_del_usuario(radio, radios_por_pi, numero_pi: int) -> dict:
    """{número de PI: radio} de lo que pidió el usuario: 'radio' para todos los PI y 'radios_por_pi' (lista en orden
    o lista de {"pi", "radio"}, como en el plugin) para los que nombra. Vacío si no pidió nada."""
    fijos = {}
    if radio is not None:
        fijos = {k: float(radio) for k in range(1, numero_pi + 1)}
    if radios_por_pi is None or (isinstance(radios_por_pi, str) and not radios_por_pi.strip()):
        return fijos
    lista = radios_por_pi
    if isinstance(radios_por_pi, str):
        try:
            lista = json.loads(radios_por_pi)
        except ValueError:
            raise n.ErrorNorma("'radios_por_pi' no es JSON válido: usa [80, 0, 120] o [{\"pi\": 1, \"radio\": 80}].")
    if lista is None:
        return fijos
    if not isinstance(lista, list):
        raise n.ErrorNorma("'radios_por_pi' debe ser una lista JSON: [80, 0, 120] o [{\"pi\": 1, \"radio\": 80}].")
    for posicion, e in enumerate(lista, start=1):
        if isinstance(e, dict):
            try:
                pi = int(e.get("pi"))
            except (TypeError, ValueError):
                raise n.ErrorNorma(f"El elemento {posicion} de 'radios_por_pi' no tiene un 'pi' entero: {e}")
            r = _radio_de_texto(e.get("radio"), f"el PI {pi}")
        else:
            pi, r = posicion, _radio_de_texto(e, f"la posición {posicion}")
        if pi < 1 or pi > numero_pi:
            raise n.ErrorNorma(f"'radios_por_pi' nombra el PI {pi} y la polilínea tiene {numero_pi} PI intermedios.")
        fijos[pi] = r
    return fijos


# ---------------------------------------------------------------------------------------------- propuesta
def _mismo_sentido(a: dict, b: dict) -> bool:
    return a.get("giro") == b.get("giro") and a.get("giro") in ("izquierda", "derecha")


def _necesidad(base: n.BaseNorma, ctx: dict, pi: dict, comunes: list) -> dict:
    """Primera pasada: si el PI necesita curva según la norma y su mínimo."""
    delta = pi["deflexion_grados"]
    salida = {"necesita_curva": False, "minimos": [], "radio_minimo_norma": None, "manda": None, "nota": None,
              "aviso": None, "sin_curva": None}
    if pi.get("giro") == "ninguno" or delta <= DEFLEXION_NULA_GRADOS:
        salida["nota"] = "Vértices alineados (deflexión 0°): no hace falta curva."
        return salida
    if 180.0 - delta <= DEFLEXION_NULA_GRADOS:
        salida["aviso"] = (f"PI {pi['numero']}: la polilínea vuelve sobre sí misma (deflexión 180°); no cabe ninguna "
                           "curva. Corrige la polilínea.")
        return salida
    sin_curva = deflexion_sin_curva(base, ctx, delta)
    if sin_curva:
        salida["sin_curva"] = sin_curva
        salida["nota"] = (f"La deflexión ({n._limpio(delta)}°) no pasa de la del cuadro de deflexión sin curva "
                          f"({sin_curva['texto']}, {n.texto_referencia(sin_curva['referencia'])}): la norma permite el PI "
                          f"sin curva ({sin_curva['regla']}). Se propone 0.")
        return salida
    minimos = list(comunes)
    pequena = minimo_deflexion_pequena(base, delta)
    if pequena:
        minimos.append(pequena)
    salida["necesita_curva"] = True
    salida["minimos"] = minimos
    if minimos:
        manda = max(minimos, key=lambda m: (m["radio_necesario"], m["radio_exacto"]))
        salida["radio_minimo_norma"] = manda["radio_necesario"]
        redondeo = ""
        if abs(manda["radio_necesario"] - manda["radio_exacto"]) > n.TOLERANCIA:
            redondeo = (f"; {n._limpio(manda['radio_exacto'])} m redondeado hacia arriba a un múltiplo de 5 m"
                        + (" (desigualdad estricta)" if manda["criterio"] == n.MAYOR_ESTRICTO else ""))
        salida["manda"] = {"comprobacion": manda["comprobacion"], "radio": manda["radio_necesario"],
                           "referencia": n.texto_referencia(manda["referencia"]),
                           "explicacion": f"{manda['comprobacion']} ({n.texto_referencia(manda['referencia'])}): "
                                          f"{n._limpio(manda['radio_necesario'])} m{redondeo}."}
    return salida


def proponer(base: n.BaseNorma, ctx: dict, geo: dict, fijos: dict) -> dict:
    """Radio por PI: el mínimo de la norma si cabe (o el del usuario si lo dio), con el criterio de CRITERIO.
    Devuelve {pis, tramos, radios_por_pi, avisos}."""
    pis, tramos = geo["pis"], geo["tramos"]
    comunes = minimos_del_proyecto(base, ctx)
    filas = []
    for pi in pis:
        nec = _necesidad(base, ctx, pi, comunes)
        if pi["numero"] in fijos:
            origen, objetivo = "usuario", fijos[pi["numero"]]
        elif nec["necesita_curva"] and nec["radio_minimo_norma"] is not None:
            origen, objetivo = "norma", nec["radio_minimo_norma"]
        else:
            origen, objetivo = "sin_curva", 0.0
        filas.append({"pi": pi, "nec": nec, "origen": origen, "objetivo": objetivo})

    avisos = []
    tangentes_minimas = []
    radios = []
    tangentes = []
    for i, f in enumerate(filas):
        pi, nec = f["pi"], f["nec"]
        delta = pi["deflexion_grados"]
        tan_mitad = _tan_mitad(delta) if nec["necesita_curva"] or f["objetivo"] > 0 else 0.0
        anterior = filas[i - 1] if i > 0 else None
        siguiente = filas[i + 1] if i + 1 < len(filas) else None
        con_curva = f["objetivo"] > 0
        curva_antes = con_curva and anterior is not None and radios[i - 1] > 0 and tangentes[i - 1] > 0
        curva_despues = con_curva and siguiente is not None and siguiente["objetivo"] > 0
        tmin_antes = tangente_minima(base, ctx, _mismo_sentido(anterior["pi"], pi)) if curva_antes else None
        tmin_despues = tangente_minima(base, ctx, _mismo_sentido(pi, siguiente["pi"])) if curva_despues else None
        libre_antes_geo = tramos[i] - (tangentes[i - 1] if i > 0 else 0.0)
        libre_despues_geo = tramos[i + 1]
        libre_antes = libre_antes_geo - (tmin_antes["valor"] if tmin_antes else 0.0)
        libre_despues = libre_despues_geo - (tmin_despues["valor"] if tmin_despues else 0.0)
        cabe_hasta = min(libre_antes, libre_despues)
        radio_max = max(0.0, cabe_hasta / tan_mitad) if tan_mitad > 0 else None
        radio_max_geo = max(0.0, min(libre_antes_geo, libre_despues_geo) / tan_mitad) if tan_mitad > 0 else None

        salida = {
            "numero": pi["numero"],
            "progresiva": None,   # la del eje con los radios pedidos: la pone completar_con_plugin
            "deflexion_grados": n._limpio(delta),
            "giro": pi.get("giro"),
            "origen": f["origen"],
            "minimos": nec["minimos"],
            "radio_minimo_norma": n._limpio(nec["radio_minimo_norma"]),
            "manda": nec["manda"],
            "radio_maximo_que_cabe": n._limpio(math.floor(radio_max * 1000) / 1000) if radio_max is not None else None,
            "tangente_minima_antes": tmin_antes,
            "tangente_minima_despues": tmin_despues,
            "radio_pedido": None,
            "cumple_minimo": None,
            "nota": nec["nota"],
            "nota_3v": None,
            "aviso": nec["aviso"],
        }
        if nec["aviso"]:
            avisos.append(nec["aviso"])
        radio = 0.0
        efectiva = 0.0   # tangente que de verdad ocupa la curva (0 si el plugin la deja sin curva)
        if f["origen"] == "usuario":
            radio = f["objetivo"]
            minimo = nec["radio_minimo_norma"]
            if not nec["necesita_curva"]:
                salida["cumple_minimo"] = True   # la norma no pide curva: cualquier radio (o ninguno) vale
                if radio > 0 and tan_mitad > 0 and radio * tan_mitad <= min(libre_antes_geo, libre_despues_geo) + 1e-6:
                    efectiva = radio * tan_mitad
            elif radio <= 0:
                salida["aviso"] = (f"PI {pi['numero']}: pediste el PI sin curva y la norma pide curva (deflexión "
                                   f"{n._limpio(delta)}°): la verificación lo marcará «No cumple».")
                salida["cumple_minimo"] = False
            else:
                t = radio * tan_mitad
                if t > libre_antes_geo + 1e-6 or t > libre_despues_geo + 1e-6:
                    salida["aviso"] = (f"PI {pi['numero']}: tu radio {n._limpio(radio)} m no cabe (tangente {n._limpio(t)} m; "
                                       f"cabría hasta {n._limpio(math.floor(radio_max_geo * 1000) / 1000)} m): el plugin "
                                       "dejará el PI sin curva.")
                    salida["cumple_minimo"] = False
                else:
                    efectiva = t
                    bajo = minimo is not None and radio < minimo - n.TOLERANCIA
                    sin_tangente = t > cabe_hasta + MARGEN_CABIDA_M
                    if bajo:
                        salida["aviso"] = (f"PI {pi['numero']}: tu radio {n._limpio(radio)} m queda por debajo del mínimo "
                                           f"de la norma ({n._limpio(minimo)} m, {nec['manda']['referencia']}): la "
                                           "verificación lo marcará «No cumple».")
                    elif sin_tangente:
                        salida["aviso"] = (f"PI {pi['numero']}: tu radio {n._limpio(radio)} m no deja la tangente mínima "
                                           "con la curva vecina: la verificación lo marcará «No cumple».")
                    salida["cumple_minimo"] = None if minimo is None else not (bajo or sin_tangente)
        elif f["origen"] == "norma":
            minimo = f["objetivo"]
            if minimo * tan_mitad <= cabe_hasta - MARGEN_CABIDA_M:
                radio = minimo
                salida["cumple_minimo"] = True
            else:
                radio, nivel = _radio_que_cabe(tan_mitad, cabe_hasta, min(libre_antes_geo, libre_despues_geo))
                salida["cumple_minimo"] = False
                pedido = (f"se pide {n._limpio(radio)} m, el mayor que cabe dejando la tangente mínima, redondeado hacia "
                          "abajo a 5 m" if nivel == 1 else
                          f"se pide {n._limpio(radio)} m, el mayor que cabe sin dejar la tangente mínima con la curva "
                          "anterior (lo que admite el plugin), redondeado hacia abajo a 5 m" if nivel == 2 else
                          "no cabe ninguna curva de 5 m o más: el PI queda sin curva")
                salida["aviso"] = (f"La polilínea no permite cumplir la norma en el PI {pi['numero']} (mínimo "
                                   f"{n._limpio(minimo)} m; cabe hasta {salida['radio_maximo_que_cabe']} m dejando la "
                                   f"tangente mínima): mueve los vértices o baja la velocidad. {pedido[0].upper() + pedido[1:]}; "
                                   "la verificación lo marcará «No cumple».")
            efectiva = radio * tan_mitad
        else:
            salida["cumple_minimo"] = True if not nec["necesita_curva"] else None
            if nec["necesita_curva"] and nec["radio_minimo_norma"] is None:
                salida["aviso"] = (f"PI {pi['numero']}: la norma no tiene radio mínimo para {ctx['velocidad']} km/h en "
                                   "estas tablas: el PI queda sin curva. Pasa 'radios_por_pi' para ese PI.")
        if salida["aviso"] and salida["aviso"] not in avisos:
            avisos.append(salida["aviso"])
        if radio > 0 and tan_mitad > 0:
            lm = _longitud_minima_curva(base, ctx, delta)
            if lm and radio < lm["radio"] - n.TOLERANCIA:
                cabe = lm["radio"] * tan_mitad <= cabe_hasta - MARGEN_CABIDA_M
                salida["nota_3v"] = (f"Solo aviso: con R ≥ {n._limpio(lm['radio'])} m la curva llegaría a la longitud "
                                     f"mínima {lm['expresion']} = {n._limpio(lm['longitud'])} m ({lm['referencia']}); "
                                     + ("ese radio cabe." if cabe else "ese radio no cabe en la polilínea."))
        salida["radio_pedido"] = n._limpio(radio)
        radios.append(radio)
        tangentes.append(efectiva)
        filas[i]["salida"] = salida
        if tmin_antes:
            tangentes_minimas.append({"tramo": f"PI {anterior['pi']['numero']}–PI {pi['numero']}",
                                      "sentido": "mismo sentido" if _mismo_sentido(anterior["pi"], pi) else "sentido contrario",
                                      **tmin_antes})

    return {
        "pis": [f["salida"] for f in filas],
        "tramos": [n._limpio(t) for t in tramos],
        "tangentes_minimas": tangentes_minimas,
        "radios_por_pi": [n._limpio(r) for r in radios],
        "avisos": avisos,
    }


def _radio_que_cabe(tan_mitad: float, cabe_hasta: float, cabe_hasta_geo: float) -> tuple[float, int]:
    """Cuando el mínimo no cabe: (radio, nivel). Nivel 1: el mayor que cabe con la tangente mínima; 2: el mayor que
    cabe sin ella; 3: sin curva (0). Hacia abajo a 5 m y con 1 mm de margen."""
    for nivel, libre in ((1, cabe_hasta), (2, cabe_hasta_geo)):
        if tan_mitad <= 0 or libre <= 0:
            continue
        radio = abajo_5(libre / tan_mitad)
        while radio >= RADIO_MINIMO_PEDIBLE_M and radio * tan_mitad > libre - MARGEN_CABIDA_M:
            radio -= PASO_REDONDEO_M
        if radio >= RADIO_MINIMO_PEDIBLE_M:
            return radio, nivel
    return 0.0, 3


def radio_por_defecto(radio_usuario, radios: list) -> float:
    """'radio' para el plugin (lo exige > 0; no se usa, porque radios_por_pi cubre todos los PI)."""
    if radio_usuario is not None:
        return radio_usuario
    positivos = [r for r in radios if r and r > 0]
    return max(positivos) if positivos else RADIO_PROVISIONAL


# ---------------------------------------------------------------------------------------------- verificación prevista
def tabla_pis_simulada(nombre: str, despues: dict, unidades: str) -> dict:
    """La tabla de PI de la simulación del plugin con el formato de listar_pis, para verificarla sin que el eje exista."""
    despues = despues or {}
    pis = []
    for p in sorted(despues.get("pis") or [], key=lambda x: x.get("numero") or 0):
        curva = p.get("radio_aplicado") is not None
        pis.append({
            "numero": p.get("numero"), "tipo": "curva" if curva else "sin_curva", "tiene_curva": curva,
            "progresiva": p.get("progresiva"), "progresiva_texto": n.progresiva_texto(p.get("progresiva")),
            "x": p.get("x"), "y": p.get("y"), "deflexion_grados": p.get("deflexion_grados"), "giro": p.get("giro"),
            "radio": p.get("radio_aplicado"), "longitud_curva": p.get("longitud_curva") if curva else None,
            "tangente": p.get("tangente") if curva else None,
            "progresiva_pc": p.get("progresiva_pc") if curva else None,
            "progresiva_pt": p.get("progresiva_pt") if curva else None,
        })
    inicio = despues.get("progresiva_inicial") or 0.0
    fin = despues.get("progresiva_final")
    return {
        "alineamiento": nombre, "unidades": unidades, "longitud": despues.get("longitud"),
        "inicio": {"progresiva": inicio, "progresiva_texto": n.progresiva_texto(inicio)},
        "fin": {"progresiva": fin, "progresiva_texto": n.progresiva_texto(fin)},
        "numero_pi": len(pis), "numero_curvas": sum(1 for p in pis if p["tiene_curva"]), "pis": pis, "avisos": [],
    }


def completar_con_plugin(bloque: dict, despues: dict):
    """Con la respuesta final del plugin: la progresiva de cada PI (la del ensayo usaba un radio provisional) y un
    aviso por cada PI con radio pedido que el plugin dejó sin curva (no debería pasar: la propuesta deja margen)."""
    finales = {p.get("numero"): p for p in (despues or {}).get("pis") or []}
    for pi in bloque["pis"]:
        final = finales.get(pi["numero"])
        if final is None:
            continue
        pi["progresiva"] = final.get("progresiva")
        if pi["radio_pedido"] and final.get("radio_aplicado") is None:
            bloque["avisos"].append(f"El plugin dejó el PI {pi['numero']} sin curva aunque se pidió {pi['radio_pedido']} m: "
                                    "mira 'avisos' del plugin.")


def bloque_propuesta(ctx: dict, propuesta: dict, radio: float, insunits: dict, ensayo_id_log) -> dict:
    return {
        "norma": n.NORMA,
        "velocidad_diseno": ctx["velocidad"],
        "proyecto": n._proyecto(ctx),
        "avisos_proyecto": list(ctx["avisos"]),
        "criterio": CRITERIO,
        "unidades": insunits,
        "simulacion_de_ensayo": {"id_log": ensayo_id_log},
        "radio": n._limpio(radio),
        "radios_por_pi": propuesta["radios_por_pi"],
        "tramos": propuesta["tramos"],
        "tangentes_minimas": propuesta["tangentes_minimas"],
        "pis": propuesta["pis"],
        "avisos": propuesta["avisos"],
    }

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
3. por curva circular: peralte de diseño e, bombeo b, longitudes de transición del peralte y del bombeo y su reparto
   entre tangente y curva (de la base de la norma), y las progresivas críticas (progresivas_criticas);
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
- entrada: fin del bombeo normal (FBN) → bombeo nivelado (BN, el exterior en 0 %) → bombeo invertido (BI, el exterior
  en +b; solo si e > b) → inicio del peralte total (IPT, ±e). BN = PC − p·Lp; FBN = BN − Lb; BI = BN + Lp·b/e;
  IPT = BN + Lp. Lp: longitud de transición del peralte (de 0 a e); Lb: la del bombeo (de −b a 0); p: proporción de
  Lp en la tangente;
- salida: simétrica respecto al PT: fin del peralte total (FPT), bombeo invertido, bombeo nivelado e inicio del
  bombeo normal (IBN).
"""
import math

import norma as n
import norma_alineamiento as ne

# ---------------------------------------------------------------------------------------------- parámetros
# Datos del peralte que el puente añade a los de verificar_norma. Los define el esquema de las tablas de peralte de
# dg2018.sqlite (sesión 6.4 de memoria-firma): se completan cuando llegue; nunca se suponen.
CAMPOS_PERALTE: dict = {}
CAMPOS = {**ne.CAMPOS_PROYECTO, **CAMPOS_PERALTE}
# Lo que el plugin tiene que publicar para que el puente amplíe la herramienta.
PARAMETROS_NECESARIOS = {"alineamiento", "curvas", "simular"}
DESCRIPCION_CURVAS = " Con 'velocidad_diseno' no se pasa (el puente calcula las progresivas críticas con la DG-2018)."
PARAMETROS_OPCIONALES = {"curvas": DESCRIPCION_CURVAS}

DESCRIPCION = (
    "\n\nCON LA NORMA DG-2018 (puente, desde 1.20.0): con 'velocidad_diseno' y los datos del proyecto ('clasificacion', "
    "'orografia', 'ubicacion', 'peralte_maximo', 'pavimentada': los de verificar_norma, y los del peralte que pida), "
    "dentro de 'args' y SIN 'curvas', el puente lee las curvas del eje con listar_pis y calcula en cada curva circular el "
    "peralte de diseño, el bombeo, las longitudes de transición y las progresivas críticas (fin del bombeo normal, bombeo "
    "nivelado, bombeo invertido, inicio y fin del peralte total, inicio del bombeo normal), y las aplica. Si las "
    "transiciones no caben (fuera del eje, cruzadas con las de la curva vecina o sin tramo de peralte total) responde el "
    "error sin escribir. Si falta un dato del proyecto responde 'faltan' sin llamar a Civil 3D: pregúntalo al usuario, no "
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
# SuperelevationAttainmentRegionType, SuperelevationCrossSegmentType). VACÍA a propósito: el catálogo de la API no trae
# los valores de los enumerados y no se inventan. Se rellena con los nombres que devuelve listar_peralte → enumerados en
# el paso 1 de herramientas-dev/VALIDACION_120.md (regla 11f: corrección en la misma rama).
CORRESPONDENCIA = {
    "tipo": {c: None for c in TIPOS},
    "region": {c: None for c in REGIONES},
    "segmento": {c: None for c in SEGMENTOS},
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


# ---------------------------------------------------------------------------------------------- valores de la norma
# Tablas de dg2018.sqlite que hacen falta para el peralte (sesión 6.4 de memoria-firma). Se completan con su esquema.
TABLAS_PERALTE: tuple = ()
SIN_TABLAS = ("La base de la norma todavía no tiene las tablas de peralte (peralte según el radio, bombeo y longitud de "
              "transición): actualiza memoria-firma (git pull). No se llamó a aplicar_peralte.")


def preparar_peralte(base: n.BaseNorma, ctx: dict | None, proyecto: dict) -> tuple[dict | None, list]:
    """Datos del peralte del proyecto comprobados contra la base: (datos, faltan). Si `faltan` no está vacío hay que
    preguntar esos datos al usuario con sus valores válidos."""
    raise n.ErrorNorma(SIN_TABLAS)


def valores_de_curva(base: n.BaseNorma, ctx: dict, datos: dict, curva: dict) -> dict:
    """Peralte de diseño e (%), bombeo b (%), longitudes de transición del peralte (lp) y del bombeo (lb) en m y
    proporción de lp en la tangente (p), con la referencia de cada valor y los avisos:
    {"peralte_pct", "bombeo_pct", "longitud_peralte_m", "longitud_bombeo_m", "proporcion_tangente", "valores": [...],
    "avisos": [...]}."""
    raise n.ErrorNorma(SIN_TABLAS)


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
    """Propuesta por curva circular del eje: valores de la norma, progresivas críticas y problemas de cabida.
    Devuelve {curvas, avisos, problemas}."""
    curvas, avisos = curvas_del_eje(tabla_pis)
    salida = []
    for curva in curvas:
        v = valores_de_curva(base, ctx, datos, curva)
        avisos.extend(v.get("avisos") or [])
        estaciones = progresivas_criticas(curva, v["peralte_pct"], v["bombeo_pct"], v["longitud_peralte_m"],
                                          v["longitud_bombeo_m"], v["proporcion_tangente"])
        salida.append({**curva, "peralte_pct": n._limpio(v["peralte_pct"]), "bombeo_pct": n._limpio(v["bombeo_pct"]),
                       "longitud_peralte_m": n._limpio(v["longitud_peralte_m"]),
                       "longitud_bombeo_m": n._limpio(v["longitud_bombeo_m"]),
                       "proporcion_tangente": n._limpio(v["proporcion_tangente"]),
                       "valores": v.get("valores") or [], "estaciones": estaciones})
    inicio = float(((tabla_pis or {}).get("inicio") or {}).get("progresiva") or 0.0)
    fin = ((tabla_pis or {}).get("fin") or {}).get("progresiva")
    fin = float(fin) if fin is not None else math.inf
    return {"curvas": salida, "avisos": avisos, "problemas": comprobar_cabida(salida, inicio, fin)}


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


def progresivas_criticas(curva: dict, e: float, b: float, lp: float, lb: float, p: float) -> list[dict]:
    """Progresivas críticas de una curva circular sin espirales (ver el docstring del módulo). e y b en % (positivos),
    lp y lb en m (> 0), p entre 0 y 1. Lanza ErrorNorma si un dato no tiene sentido. El bombeo invertido solo se pone
    si e > b (si no, el carril exterior no llega a +b)."""
    for nombre, valor in (("peralte", e), ("bombeo", b), ("longitud de transición del peralte", lp),
                          ("longitud de transición del bombeo", lb)):
        if valor is None or not math.isfinite(valor) or valor <= 0:
            raise n.ErrorNorma(f"PI {curva['pi']}: {nombre} no válido ({valor}).")
    if p is None or not 0 <= p <= 1:
        raise n.ErrorNorma(f"PI {curva['pi']}: proporción de la transición en la tangente no válida ({p}).")
    pc, pt, giro = curva["progresiva_pc"], curva["progresiva_pt"], curva["giro"]
    bn_e = pc - p * lp
    bn_s = pt + p * lp
    filas = [
        (FBN, ENTRADA, bn_e - lb, -b, -b),
        (BN, ENTRADA, bn_e, 0.0, -b),
    ]
    if e > b + TOLERANCIA_PCT:
        filas.append((BI, ENTRADA, bn_e + lp * b / e, b, -b))
    filas.append((IPT, ENTRADA, bn_e + lp, e, -e))
    filas.append((FPT, SALIDA, bn_s - lp, e, -e))
    if e > b + TOLERANCIA_PCT:
        filas.append((BI, SALIDA, bn_s - lp * b / e, b, -b))
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
        for cu in curvas]


# ---------------------------------------------------------------------------------------------- verificación
def verificar_lectura(curvas: list, leido: dict, correspondencia: dict | None = None) -> dict:
    """Compara el peralte total pedido de cada curva con el que lee listar_peralte (en la progresiva del inicio del
    peralte total). Devuelve {verificado, filas, avisos}."""
    c = correspondencia or CORRESPONDENCIA
    estaciones = [e for cu in (leido or {}).get("curvas") or [] for e in cu.get("estaciones") or []]
    filas, avisos = [], []
    for cu in curvas:
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

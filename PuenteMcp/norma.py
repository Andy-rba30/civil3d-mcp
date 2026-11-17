# -*- coding: utf-8 -*-
"""verificar_norma (1.18.0): comprueba un eje o una rasante del dibujo contra la DG-2018 (Manual de Carreteras: Diseño
Geométrico, MTC) con la base `normas/tablas/dg2018.sqlite` del repositorio privado memoria-firma.

Este módulo no usa la red ni Civil 3D: recibe lo que devuelven `listar_pis` y `listar_pvis` del plugin y los datos del
proyecto, y devuelve filas de resultado. Cada comprobación es una función aparte para poder reutilizarla (por ejemplo,
`radio_minimo_exigido` para proponer el radio de cada PI al crear un eje).

Reglas de la herramienta (decisiones del dueño, 17/11/2026):
- Manda la tabla. Si la tabla y la fórmula dan valores distintos se informan los dos; si el diseño cumple la tabla pero
  la fórmula pide más, el resultado es «Cumple con aviso».
- Tercera Clase pavimentada se verifica con las reglas de Segunda Clase (101.05).
- La longitud mínima de curva 3V/6V es solo un aviso; L > 30(10 − Δ) con deflexión pequeña es «No cumple» (desigualdad
  estricta).
- Si dos tablas se contradicen, se aplica el valor más exigente, se informan las dos y se avisa.
- Nunca se suponen datos del proyecto ni se interpolan velocidades: si falta algo, se pregunta.
- Tolerancia de coma flotante 0,001: igual cumple, salvo en una desigualdad estricta.
"""
import math
import os
import sqlite3
from pathlib import Path

import buscar_api

NORMA = "DG-2018"
NORMAS = (NORMA,)
TIPOS_ELEMENTO = ("alineamiento", "rasante")
RUTA_BASE = os.path.join("normas", "tablas", "dg2018.sqlite")
UNIDADES_METROS = "Meters"
TOLERANCIA = 0.001

CUMPLE = "Cumple"
NO_CUMPLE = "No cumple"
CUMPLE_AVISO = "Cumple con aviso"
AVISO = "Aviso"
NO_VERIFICABLE = "No verificable"
SIN_VALOR = "Sin valor en la norma"
RESULTADOS = (CUMPLE, NO_CUMPLE, CUMPLE_AVISO, AVISO, NO_VERIFICABLE, SIN_VALOR)

# Criterios de comparación (campo `criterio` de cada fila)
MINIMO = "minimo"                  # cumple si diseño ≥ exigido
MAXIMO = "maximo"                  # cumple si diseño ≤ exigido
MAYOR_ESTRICTO = "mayor_estricto"  # cumple si diseño > exigido (igual no basta)
RANGO = "rango"                    # cumple si mínimo ≤ diseño ≤ máximo
EN_LISTA = "en_lista"              # el valor es una de las celdas permitidas
CONDICION = "condicion"            # una condición (por ejemplo, hace falta curva vertical)

TABLAS_NECESARIAS = (
    "velocidad_diseno", "equivalencias_clasificacion", "radios_minimos", "longitud_tangente",
    "tangente_min_mismo_sentido", "deflexion_sin_curva", "longitud_minima_curva", "radio_sin_transicion",
    "relacion_radios", "indice_k", "pendiente_maxima", "visibilidad_parada", "peralte_maximo", "referencias",
    "reglas", "formulas", "metadatos",
)

# Umbrales de las reglas del texto de la norma (sección y página en la tabla `reglas` de la base). El script
# casos_norma_reales.py los compara con `reglas.valor_num` de la base real.
UMBRALES_REGLAS = {
    "R302.02-2": 59,         # minutos: deflexión mínima (aviso)
    "R302.02-7": 800,        # m: longitud de curva preferiblemente no mayor (aviso)
    "R302.02-8": 500,        # m: tangente entre curvas del mismo sentido (aviso, pendiente del dueño)
    "R302.04.05-1": 200,     # m: tangente hasta la que se exige la relación de radios
    "R302.04.05-3": 700,     # m: radio de salida tras una recta larga en autopista
    "R302.06.02-1": 1.5,     # veces: relación de radios de una curva compuesta
    "R302.06.03-1": 400,     # m: tangente entre curvas del mismo sentido (aviso)
    "R303.03.01-1": 0.5,     # %: pendiente mínima (aviso)
    "R303.03.02-1": 1,       # %: rebaja de la pendiente máxima por encima de 3000 msnm
    "R303.03.03-1": 1,       # %: aumento excepcional de la pendiente máxima
    "R303.03.03-2": 500,     # m: descanso en subidas continuas (Tercera Clase, aviso)
    "R303.03.03-3": 180,     # m: longitud máxima de tramos de más del 10 % (Tercera Clase)
    "R303.03.03-4": 6,       # %: pendiente media en más de 2000 m (Tercera Clase)
    "R303.03.03-5": 8,       # %: pendiente a evitar en curvas de radio menor de 50 m (Tercera Clase, aviso)
    "R303.04.01-1": 1,       # %: A a partir de la cual hace falta curva vertical (pavimentada)
    "R303.04.01-2": 2,       # %: lo mismo sin pavimentar
}
DEFLEXION_MINIMA_GRADOS = UMBRALES_REGLAS["R302.02-2"] / 60.0
DEFLEXION_PEQUENA_GRADOS = 5.0
ALTITUD_REBAJA_MSNM = 3000.0
PENDIENTE_TRAMO_LARGO_PCT = 10.0
PENDIENTE_SUBIDA_CONTINUA_PCT = 5.0
PENDIENTE_DESCANSO_PCT = 2.0
LONGITUD_SIN_DESCANSO_M = 3000.0
VENTANA_PENDIENTE_MEDIA_M = 2000.0
RECTA_LARGA_AUTOPISTA_M = 400.0
RADIO_CURVA_CERRADA_M = 50.0
ALTURA_OJO_M = 1.07       # h1 de F26/F27
ALTURA_OBJETO_M = 0.15    # h2 de F26/F27

# Todo lo que el código cita por id (tablas y cuadros de `referencias`, reglas de `reglas` y fórmulas de `formulas`).
# casos_norma_reales.py comprueba que existen en la base real.
REFERENCIAS_USADAS = (
    "T204.01", "T205.01", "T302.01", "T302.02", "T302.04", "T302.07", "T302.08", "T302.11A", "T302.11B", "T303.01",
    "T303.02", "T303.03", "T304.08", "C302.02-DEFLEX", "C302.02-LMIN",
    "R101.05-1", "R302.02-1", "R302.02-2", "R302.02-3", "R302.02-4", "R302.02-7", "R302.02-8", "R302.04.05-1",
    "R302.04.05-3", "R302.06.02-1", "R302.06.03-1", "R303.03.01-1", "R303.03.02-1", "R303.03.02-2", "R303.03.03-1",
    "R303.03.03-2", "R303.03.03-3", "R303.03.03-4", "R303.03.03-5", "R303.04.01-1", "R303.04.01-2",
    "F08", "F09", "F26", "F27", "F30", "F31", "F34", "F35", "F36", "F37",
)

# Lo que el lomo quebrado cita: no está en `reglas` porque la norma no da umbral.
SECCION_LOMO_QUEBRADO = {"seccion": "303.02", "pagina_pdf": "170", "pagina_impresa": "169"}

NO_COMPROBADO_ALINEAMIENTO = [
    {"que": "Peralte de diseño de cada curva", "motivo": "listar_pis no lee el peralte del eje"},
    {"que": "Transición de peralte entre curvas de sentido contrario (R302.02-9)", "motivo": "necesita el peralte de diseño"},
    {"que": "Contraperalte (R302.04.04-1)", "motivo": "necesita el peralte de diseño"},
    {"que": "Visibilidad disponible en las curvas", "motivo": "depende de los obstáculos del entorno, que no se leen del dibujo"},
    {"que": "Sobreancho en curvas", "motivo": "listar_pis no lee anchos de calzada"},
    {"que": "Velocidad específica de cada curva (R204.04.01-1)", "motivo": "no se calcula"},
    {"que": "Coordinación entre planta y perfil (R601.02)", "motivo": "no se calcula"},
]
NO_COMPROBADO_RASANTE = [
    {"que": "Visibilidad de paso (control «paso» de la T303.02)", "motivo": "solo si el tramo debe permitir adelantar; no se verifica por defecto"},
    {"que": "Pendiente mínima donde se anula la pendiente transversal (R303.03.01-6)", "motivo": "necesita la transición de peralte"},
    {"que": "Excepciones de la pendiente mínima (bombeo, bermas, cunetas: R303.03.01-2 a -5)", "motivo": "dependen de la sección transversal, que no se lee"},
    {"que": "Visibilidad disponible", "motivo": "depende de los obstáculos del entorno, que no se leen del dibujo"},
    {"que": "Coordinación entre planta y perfil (R601.02)", "motivo": "no se calcula"},
]


class ErrorNorma(Exception):
    """Algo impide verificar (norma, tipo de elemento, memoria o base). El mensaje ya dice qué pasa y cómo arreglarlo."""


# ---------------------------------------------------------------------------------------------- utilidades
def normalizar(texto) -> str:
    return buscar_api.normalizar("" if texto is None else str(texto))


def _vacio(valor) -> bool:
    return valor is None or (isinstance(valor, str) and not valor.strip())


def _limpio(valor):
    """8.0 → 8; 50.3937 → 50.394 (para mostrar). None queda None."""
    if valor is None:
        return None
    if isinstance(valor, bool):
        return valor
    if isinstance(valor, (int, float)):
        r = round(float(valor), 3)
        return int(r) if r == int(r) else r
    return valor


def progresiva_texto(pk) -> str | None:
    """300 → '0+300.000'; 1234.5 → '1+234.500' (el mismo formato que listar_pis)."""
    if pk is None:
        return None
    a = round(abs(float(pk)), 3)
    km = int(a // 1000)
    resto = round(a - km * 1000, 3)
    if resto >= 1000:
        km += 1
        resto -= 1000
    return ("-" if pk < 0 and a > 0 else "") + f"{km}+{resto:07.3f}"


def cumple(diseno: float, exigido: float, criterio: str) -> bool:
    """Compara con la tolerancia 0,001: igual cumple en mínimo y máximo; en mayor_estricto, igual no cumple."""
    if criterio == MINIMO:
        return diseno >= exigido - TOLERANCIA
    if criterio == MAXIMO:
        return diseno <= exigido + TOLERANCIA
    if criterio == MAYOR_ESTRICTO:
        return diseno - exigido > TOLERANCIA
    raise ValueError(f"criterio desconocido: {criterio}")


def resultado_tabla_formula(diseno: float, valor_tabla, valor_formula, criterio: str) -> str:
    """Decisión 1: manda la tabla. Sin tabla, decide la fórmula. Cumple la tabla pero no la fórmula: Cumple con aviso."""
    if valor_tabla is None:
        if valor_formula is None:
            return SIN_VALOR
        return CUMPLE if cumple(diseno, valor_formula, criterio) else NO_CUMPLE
    if not cumple(diseno, valor_tabla, criterio):
        return NO_CUMPLE
    if valor_formula is not None and not cumple(diseno, valor_formula, criterio):
        return CUMPLE_AVISO
    return CUMPLE


def _con_aviso(resultado: str, hay_aviso: bool) -> str:
    return CUMPLE_AVISO if hay_aviso and resultado == CUMPLE else resultado


def fila(elemento, comprobacion, valor_diseno, valor_exigido, unidad, criterio, resultado, referencia,
         progresiva=None, **detalle) -> dict:
    """Una fila del resultado. `detalle` solo lleva las claves con valor."""
    if isinstance(valor_exigido, dict):
        valor_exigido = {k: _limpio(v) for k, v in valor_exigido.items()}
    return {
        "elemento": elemento,
        "progresiva": progresiva,
        "comprobacion": comprobacion,
        "valor_diseno": _limpio(valor_diseno),
        "valor_exigido": _limpio(valor_exigido) if not isinstance(valor_exigido, dict) else valor_exigido,
        "unidad": unidad,
        "criterio": criterio,
        "resultado": resultado,
        "referencia": referencia,
        "detalle": {k: (_limpio(v) if isinstance(v, (int, float)) and not isinstance(v, bool) else v)
                    for k, v in detalle.items() if v is not None},
    }


# ---------------------------------------------------------------------------------------------- fórmulas (en código)
def radio_minimo_formula(v_kmh: float, peralte_pct: float, f_max: float) -> float:
    """F08 / F09: R = V² / (127 (e + f)), con el peralte en tanto por uno."""
    return v_kmh ** 2 / (127.0 * (peralte_pct / 100.0 + f_max))


def longitud_deflexion_pequena(deflexion_grados: float) -> float:
    """F37: 30 (10 − Δ), con Δ en grados."""
    return 30.0 * (10.0 - deflexion_grados)


def tangente_minima_s(v_kmh: float) -> float:
    """F34: L mín.s = 1,39 V (curvas de sentido contrario)."""
    return 1.39 * v_kmh


def tangente_minima_o(v_kmh: float) -> float:
    """F35: L mín.o = 2,78 V (curvas del mismo sentido)."""
    return 2.78 * v_kmh


def tangente_maxima(v_kmh: float) -> float:
    """F36: L máx = 16,70 V."""
    return 16.70 * v_kmh


def convexa_f26(a_pct: float, distancia_m: float, h1: float = ALTURA_OJO_M, h2: float = ALTURA_OBJETO_M) -> float:
    """F26 (convexa, parada, D < L): L = A D² / (100 (√(2 h1) + √(2 h2))²)."""
    return a_pct * distancia_m ** 2 / (100.0 * ((2 * h1) ** 0.5 + (2 * h2) ** 0.5) ** 2)


def convexa_f27(a_pct: float, distancia_m: float, h1: float = ALTURA_OJO_M, h2: float = ALTURA_OBJETO_M) -> float:
    """F27 (convexa, parada, D > L): L = 2 D − 200 (√h1 + √h2)² / A."""
    return 2 * distancia_m - 200.0 * (h1 ** 0.5 + h2 ** 0.5) ** 2 / a_pct


def concava_f30(a_pct: float, distancia_m: float) -> float:
    """F30 (cóncava, D < L): L = A D² / (120 + 3,5 D)."""
    return a_pct * distancia_m ** 2 / (120.0 + 3.5 * distancia_m)


def concava_f31(a_pct: float, distancia_m: float) -> float:
    """F31 (cóncava, D > L): L = 2 D − (120 + 3,5 D) / A."""
    return 2 * distancia_m - (120.0 + 3.5 * distancia_m) / a_pct


def longitud_convexa(a_pct: float, distancia_m: float):
    """Curva convexa con visibilidad de parada: F26 si D < L, F27 si D > L. Devuelve (L, id de la fórmula)."""
    if a_pct <= 0:
        return None, None
    l26 = convexa_f26(a_pct, distancia_m)
    if l26 >= distancia_m:
        return l26, "F26"
    return max(0.0, convexa_f27(a_pct, distancia_m)), "F27"


def longitud_concava(a_pct: float, distancia_m: float):
    """Curva cóncava: F30 si D < L, F31 si D > L. Devuelve (L, id de la fórmula)."""
    if a_pct <= 0:
        return None, None
    l30 = concava_f30(a_pct, distancia_m)
    if l30 >= distancia_m:
        return l30, "F30"
    return max(0.0, concava_f31(a_pct, distancia_m)), "F31"


# ---------------------------------------------------------------------------------------------- base de la norma
def localizar_base(carpeta: str | None = None) -> str:
    """Ruta de dg2018.sqlite dentro de memoria-firma (argumento o MEMORIA_FIRMA_DIR). Lanza ErrorNorma si falta."""
    variable = buscar_api.VARIABLE_ENTORNO
    carpeta = carpeta or os.environ.get(variable) or ""
    if not carpeta.strip():
        raise ErrorNorma(
            f"Falta la variable de entorno {variable}: debe apuntar a la carpeta del repositorio privado memoria-firma "
            f"(la que contiene normas\\tablas\\dg2018.sqlite). {buscar_api.COMO_PONER_VARIABLE}"
        )
    carpeta = os.path.abspath(os.path.expanduser(os.path.expandvars(carpeta.strip())))
    if not os.path.isdir(carpeta):
        raise ErrorNorma(
            f"La carpeta de {variable} no existe: {carpeta}. Clona memoria-firma ahí o corrige la variable. "
            f"{buscar_api.COMO_PONER_VARIABLE}"
        )
    ruta = os.path.join(carpeta, RUTA_BASE)
    if not os.path.isfile(ruta):
        raise ErrorNorma(
            f"Falta la base de la norma {ruta}. Actualiza memoria-firma (git pull): la base de la DG-2018 va en "
            "normas\\tablas\\dg2018.sqlite. No se verificó nada."
        )
    return ruta


class BaseNorma:
    """Acceso de solo lectura a dg2018.sqlite. Cada método es una consulta; las filas salen como diccionarios."""

    def __init__(self, ruta: str):
        self.ruta = ruta
        self._referencias = {}
        try:
            self.con = sqlite3.connect(Path(ruta).resolve().as_uri() + "?mode=ro", uri=True, check_same_thread=False)
            self.con.row_factory = sqlite3.Row
            existentes = {r[0] for r in self.con.execute("SELECT name FROM sqlite_master WHERE type IN ('table', 'view')")}
        except sqlite3.Error as e:
            raise ErrorNorma(f"No se pudo abrir la base de la norma {ruta}: {e}. Actualiza memoria-firma (git pull).")
        faltan = [t for t in TABLAS_NECESARIAS if t not in existentes]
        if faltan:
            self.cerrar()
            raise ErrorNorma(
                f"La base de la norma {ruta} no tiene las tablas: {', '.join(faltan)}. Actualiza memoria-firma (git pull). "
                "No se verificó nada."
            )

    def cerrar(self):
        try:
            self.con.close()
        except Exception:
            pass

    def __enter__(self):
        return self

    def __exit__(self, *a):
        self.cerrar()

    def filas(self, sql: str, *params) -> list[dict]:
        try:
            return [dict(r) for r in self.con.execute(sql, params)]
        except sqlite3.Error as e:
            raise ErrorNorma(f"La base de la norma {self.ruta} no responde a una consulta ({e}). Actualiza memoria-firma (git pull).")

    def fila(self, sql: str, *params) -> dict | None:
        filas = self.filas(sql, *params)
        return filas[0] if filas else None

    # -------------------------------------------------- clasificación del proyecto
    def clasificaciones(self) -> list[str]:
        return [r["clasificacion"] for r in self.filas(
            "SELECT clasificacion FROM velocidad_diseno GROUP BY clasificacion ORDER BY MIN(orden)")]

    def orografias(self) -> list[tuple[str, int]]:
        return [(r["orografia"], r["tipo"]) for r in self.filas(
            "SELECT orografia, MIN(orografia_tipo) AS tipo FROM equivalencias_clasificacion GROUP BY orografia ORDER BY tipo")]

    def zonas(self) -> list[str]:
        return [r["zona"] for r in self.filas(
            "SELECT zona FROM equivalencias_clasificacion GROUP BY zona ORDER BY MIN(orden)")]

    def velocidades(self, clasificacion: str, orografia: str) -> list[int]:
        return [int(r["velocidad_kmh"]) for r in self.filas(
            "SELECT velocidad_kmh FROM velocidad_diseno WHERE clasificacion = ? AND orografia = ? ORDER BY velocidad_kmh",
            clasificacion, orografia)]

    def equivalencias(self, clasificacion: str, orografia: str, zona: str | None = None) -> list[dict]:
        if zona is None:
            return self.filas("SELECT * FROM equivalencias_clasificacion WHERE clasificacion = ? AND orografia = ? "
                              "ORDER BY orden", clasificacion, orografia)
        return self.filas("SELECT * FROM equivalencias_clasificacion WHERE clasificacion = ? AND orografia = ? AND zona = ? "
                          "ORDER BY orden", clasificacion, orografia, zona)

    def ubicaciones_de_zona(self, zona: str) -> list[str]:
        return [r["ubicacion_radios"] for r in self.filas(
            "SELECT ubicacion_radios FROM equivalencias_clasificacion WHERE zona = ? GROUP BY ubicacion_radios "
            "ORDER BY MIN(orden)", zona)]

    def peralte_de_condicion(self, condicion: str | None):
        if not condicion:
            return None
        r = self.fila("SELECT absoluto_pct FROM peralte_maximo WHERE condicion = ? ORDER BY orden", condicion)
        return r["absoluto_pct"] if r else None

    # -------------------------------------------------- planta
    def radio_t30202(self, ubicacion: str, v: int) -> dict | None:
        return self.fila("SELECT * FROM radios_minimos WHERE ref = 'T302.02' AND ubicacion = ? AND velocidad_kmh = ? "
                         "ORDER BY orden", ubicacion, v)

    def peralte_t30202(self, ubicacion: str):
        r = self.fila("SELECT peralte_max_pct FROM radios_minimos WHERE ref = 'T302.02' AND ubicacion = ? ORDER BY orden",
                      ubicacion)
        return r["peralte_max_pct"] if r else None

    def radio_t30204(self, peralte_pct: float, v: int) -> dict | None:
        return self.fila("SELECT * FROM radios_minimos WHERE ref = 'T302.04' AND abs(peralte_max_pct - ?) < 0.001 "
                         "AND velocidad_kmh = ? ORDER BY orden", float(peralte_pct), v)

    def peraltes_t30204(self) -> list[float]:
        return [r["peralte_max_pct"] for r in self.filas(
            "SELECT DISTINCT peralte_max_pct FROM radios_minimos WHERE ref = 'T302.04' ORDER BY peralte_max_pct")]

    def longitud_tangente(self, v: int) -> dict | None:
        return self.fila("SELECT * FROM longitud_tangente WHERE velocidad_kmh = ? ORDER BY orden", v)

    def tangente_mismo_sentido(self, v: int) -> dict | None:
        return self.fila("SELECT * FROM tangente_min_mismo_sentido WHERE velocidad_kmh = ? ORDER BY orden", v)

    def deflexion_sin_curva(self, v: int) -> dict | None:
        return self.fila("SELECT * FROM deflexion_sin_curva WHERE velocidad_kmh = ? ORDER BY orden", v)

    def longitud_minima_curva(self, autopista: bool) -> dict | None:
        for r in self.filas("SELECT * FROM longitud_minima_curva ORDER BY orden"):
            if normalizar(r["tipo_carretera"]).startswith("autopista") == autopista:
                return r
        return None

    def radio_sin_transicion(self, ref: str, v: int) -> dict | None:
        return self.fila("SELECT * FROM radio_sin_transicion WHERE ref = ? AND velocidad_kmh = ? ORDER BY orden", ref, v)

    def relacion_radios(self, grupo: int) -> list[dict]:
        return self.filas("SELECT * FROM relacion_radios WHERE grupo = ? ORDER BY radio_entrada_m, orden", grupo)

    # -------------------------------------------------- perfil
    def indice_k(self, tipo_curva: str, v: int, control: str = "parada") -> dict | None:
        return self.fila("SELECT * FROM indice_k WHERE tipo_curva = ? AND control = ? AND velocidad_kmh = ? ORDER BY orden",
                         tipo_curva, control, v)

    def pendiente_maxima(self, tipo_via: str, clase: str, demanda: str, orografia_tipo: int, v: int) -> dict | None:
        return self.fila("SELECT * FROM pendiente_maxima WHERE tipo_via = ? AND clase = ? AND demanda_veh_dia = ? "
                         "AND orografia_tipo = ? AND velocidad_kmh = ? ORDER BY orden",
                         tipo_via, clase, demanda, orografia_tipo, v)

    def visibilidad_parada(self, v: int) -> dict | None:
        return self.fila("SELECT * FROM visibilidad_parada WHERE velocidad_kmh = ? ORDER BY orden", v)

    # -------------------------------------------------- citas
    def referencia(self, ident: str) -> dict:
        """Tabla, cuadro o figura (referencias), regla (reglas) o fórmula (formulas) por su id, con sección y páginas."""
        if ident in self._referencias:
            return dict(self._referencias[ident])
        ref = None
        r = self.fila("SELECT id, tipo, titulo, seccion, paginas_pdf, paginas_impresas FROM referencias WHERE id = ?", ident)
        if r:
            ref = {"id": r["id"], "tipo": r["tipo"], "titulo": r["titulo"], "seccion": r["seccion"],
                   "pagina_impresa": _texto(r["paginas_impresas"]), "pagina_pdf": _texto(r["paginas_pdf"])}
        if ref is None:
            r = self.fila("SELECT id, seccion, pagina_pdf, pagina_impresa, tipo FROM reglas WHERE id = ?", ident)
            if r:
                ref = {"id": r["id"], "tipo": "regla", "caracter": r["tipo"], "seccion": r["seccion"],
                       "pagina_impresa": _texto(r["pagina_impresa"]), "pagina_pdf": _texto(r["pagina_pdf"])}
        if ref is None:
            r = self.fila("SELECT id, nombre, seccion, pagina_pdf, pagina_impresa FROM formulas WHERE id = ?", ident)
            if r:
                ref = {"id": r["id"], "tipo": "fórmula", "titulo": r["nombre"], "seccion": r["seccion"],
                       "pagina_impresa": _texto(r["pagina_impresa"]), "pagina_pdf": _texto(r["pagina_pdf"])}
        if ref is None:
            ref = {"id": ident, "tipo": None, "nota": "no está en la base"}
        self._referencias[ident] = ref
        return dict(ref)

    def metadatos(self) -> dict:
        return {r["clave"]: r["valor"] for r in self.filas("SELECT clave, valor FROM metadatos")}


def _texto(valor):
    return None if valor is None else str(valor)


def referencia_seccion(seccion: str, pagina_pdf: str, pagina_impresa: str) -> dict:
    """Cita de una sección del texto que no está en `reglas` (por ejemplo, el lomo quebrado)."""
    return {"id": seccion, "tipo": "sección", "seccion": seccion, "pagina_impresa": pagina_impresa, "pagina_pdf": pagina_pdf}


def texto_referencia(ref: dict | None) -> str:
    """'T302.02 (302.04.02, p. 129; PDF 130)' para la tabla en markdown."""
    if not ref:
        return ""
    partes = []
    if ref.get("seccion") and ref.get("seccion") != ref.get("id"):
        partes.append(str(ref["seccion"]))
    paginas = []
    if ref.get("pagina_impresa"):
        paginas.append(f"p. {ref['pagina_impresa']}")
    if ref.get("pagina_pdf"):
        paginas.append(f"PDF {ref['pagina_pdf']}")
    if paginas:
        partes.append("; ".join(paginas))
    return f"{ref.get('id')} ({', '.join(partes)})" if partes else str(ref.get("id"))


def _formula(base: BaseNorma, ident: str, valor) -> dict | None:
    if valor is None:
        return None
    ref = base.referencia(ident)
    salida = {"id": ident, "valor": _limpio(valor)}
    for clave in ("seccion", "pagina_impresa", "pagina_pdf"):
        if ref.get(clave):
            salida[clave] = ref[clave]
    return salida


def _regla(base: BaseNorma, ident: str) -> str:
    return texto_referencia(base.referencia(ident))


# ---------------------------------------------------------------------------------------------- datos del proyecto
def _elegir(valor, validos: list[str]) -> str | None:
    """El valor válido que coincide sin mayúsculas ni tildes, o None."""
    n = normalizar(valor)
    for v in validos:
        if normalizar(v) == n:
            return v
    return None


def _falta(dato: str, motivo: str, valores_validos=None) -> dict:
    salida = {"dato": dato, "motivo": motivo}
    if valores_validos is not None:
        salida["valores_validos"] = valores_validos
    return salida


def _es_tercera(clase) -> bool:
    return normalizar(clase) == "tercera clase"


def _numero(valor):
    if valor is None or isinstance(valor, bool):
        return None
    try:
        return float(valor)
    except (TypeError, ValueError):
        return None


def resolver_proyecto(base: BaseNorma, tipo_elemento: str, datos: dict):
    """Comprueba los datos del proyecto contra la base, sin leer el dibujo. Devuelve (contexto, faltan): si `faltan`
    no está vacío, el contexto es None y hay que preguntar esos datos al usuario (con sus valores válidos)."""
    faltan = []
    alineamiento = tipo_elemento == "alineamiento"

    clasificaciones = base.clasificaciones()
    clasificacion = _elegir(datos.get("clasificacion"), clasificaciones)
    if _vacio(datos.get("clasificacion")):
        faltan.append(_falta("clasificacion", "Clasificación de la vía según la DG-2018 (T204.01).", clasificaciones))
    elif clasificacion is None:
        faltan.append(_falta("clasificacion", f"'{datos.get('clasificacion')}' no es una clasificación de la DG-2018 (T204.01).",
                             clasificaciones))

    orografias = base.orografias()
    nombres_orografia = [n for n, _ in orografias]
    orografia = _elegir(datos.get("orografia"), nombres_orografia)
    if orografia is None and normalizar(datos.get("orografia")) in {str(t) for _, t in orografias}:
        orografia = next(n for n, t in orografias if str(t) == normalizar(datos.get("orografia")))
    if _vacio(datos.get("orografia")):
        faltan.append(_falta("orografia", "Tipo de orografía del terreno (sección 102 de la DG-2018).", nombres_orografia))
    elif orografia is None:
        faltan.append(_falta("orografia", f"'{datos.get('orografia')}' no es un tipo de orografía de la DG-2018.",
                             nombres_orografia))

    zonas = base.zonas()
    zona = _elegir(datos.get("ubicacion"), zonas)
    if not _vacio(datos.get("ubicacion")) and zona is None:
        faltan.append(_falta("ubicacion", f"'{datos.get('ubicacion')}' no es una ubicación de la base.", zonas))

    v = _numero(datos.get("velocidad_diseno"))
    velocidades = []
    if clasificacion and orografia:
        velocidades = base.velocidades(clasificacion, orografia)
        if not velocidades:
            faltan.append(_falta("velocidad_diseno", f"La T204.01 no tiene velocidades para '{clasificacion}' en terreno "
                                 f"'{orografia}': revisa la clasificación o la orografía.", []))
        elif v is None:
            faltan.append(_falta("velocidad_diseno", f"Velocidad de diseño en km/h: una de las celdas de la T204.01 para "
                                 f"'{clasificacion}' en terreno '{orografia}' (la norma no permite interpolar).", velocidades))
        elif abs(v - round(v)) > 1e-9 or int(round(v)) not in velocidades:
            faltan.append(_falta("velocidad_diseno", f"{_limpio(v)} km/h no es una velocidad de la T204.01 para "
                                 f"'{clasificacion}' en terreno '{orografia}' (la norma no permite interpolar).", velocidades))
    elif v is None:
        faltan.append(_falta("velocidad_diseno", "Velocidad de diseño en km/h (sus valores dependen de la clasificación y "
                             "la orografía, T204.01)."))

    eq = None
    preguntar = ""
    if clasificacion and orografia:
        filas = base.equivalencias(clasificacion, orografia, zona)
        if not filas:
            raise ErrorNorma(f"La base no tiene la fila de equivalencias_clasificacion para '{clasificacion}', '{orografia}'"
                             f"{', ' + zona if zona else ''}. Actualiza memoria-firma (git pull).")
        if zona is None and _vacio(datos.get("ubicacion")):
            claves = {(f["tipo_via"], f["clase"], f["demanda_veh_dia"], f["orografia_tipo"], f["grupo_relacion_radios"])
                      for f in filas}
            if alineamiento or len(claves) > 1:
                faltan.append(_falta("ubicacion", "Ubicación de la vía: decide la fila de la T302.02 (radios mínimos) y "
                                     "la de la T304.05 (peralte máximo).", zonas))
        eq = filas[0]
        preguntar = " ".join(normalizar(f["preguntar"]) for f in filas if f["preguntar"])
        tercera = _es_tercera(eq["clase"])
        pavimentada = datos.get("pavimentada")
        if (tercera or "paviment" in preguntar) and pavimentada is None:
            faltan.append(_falta("pavimentada", "Tercera Clase: si está pavimentada se verifica con las reglas de Segunda "
                                 "Clase (101.05); además cambia cuándo hace falta curva vertical (303.04.01).", [True, False]))
        if alineamiento:
            pide_peralte = "peralte" in preguntar and zona is not None
            necesita = pide_peralte or (tercera and pavimentada is not True)
            if necesita:
                validos = _peraltes_validos(base, eq, tercera and pavimentada is not True, pide_peralte)
                peralte = _numero(datos.get("peralte_maximo"))
                razones = []
                if pide_peralte:
                    razones.append("en terreno accidentado la T304.05 admite dos valores y el elegido decide la fila de "
                                   "la T302.02")
                if tercera and pavimentada is not True:
                    razones.append("en Tercera Clase decide la fila de la T302.04"
                                   + (" (solo si la vía no está pavimentada)" if pavimentada is None else ""))
                motivo = "Peralte máximo del proyecto en %: " + "; ".join(razones) + "."
                if peralte is None:
                    faltan.append(_falta("peralte_maximo", motivo, validos))
                elif not any(abs(peralte - p) <= TOLERANCIA for p in validos):
                    faltan.append(_falta("peralte_maximo", f"{_limpio(peralte)} % no es un valor de la norma aquí. " + motivo,
                                         validos))
        elif eq["orografia_tipo"] in (3, 4) and _numero(datos.get("altitud_msnm")) is None and not datos.get("cotas_msnm"):
            faltan.append(_falta("altitud_msnm", "Terreno accidentado o escarpado: por encima de 3000 msnm la pendiente "
                                 "máxima baja 1 % (303.03.02). Pasa la altitud en msnm, o cotas_msnm=true si las cotas del "
                                 "perfil ya están en msnm.", ["número en msnm", "cotas_msnm=true"]))

    if faltan:
        return None, faltan
    return _contexto(base, tipo_elemento, datos, clasificacion, orografia, zona, int(round(v)), velocidades, eq,
                     preguntar), []


def _peraltes_validos(base: BaseNorma, eq: dict, usa_t30204: bool, pide_peralte: bool) -> list:
    validos = None
    if pide_peralte:
        validos = [p for p in (base.peralte_de_condicion(eq["condicion_peralte"]),
                               base.peralte_de_condicion(eq["condicion_peralte_otra"])) if p is not None]
    if usa_t30204:
        t30204 = base.peraltes_t30204()
        validos = t30204 if validos is None else [p for p in validos if any(abs(p - q) <= TOLERANCIA for q in t30204)]
    return [_limpio(p) for p in sorted(set(validos or []))]


def _contexto(base, tipo_elemento, datos, clasificacion, orografia, zona, v, velocidades, eq, preguntar) -> dict:
    avisos = []
    tercera_declarada = _es_tercera(eq["clase"])
    pavimentada = datos.get("pavimentada")
    aplica_101_05 = tercera_declarada and pavimentada is True
    efectiva = eq
    if aplica_101_05:
        segunda = next((c for c in base.clasificaciones() if normalizar(c) == "carretera de segunda clase"), None)
        filas = base.equivalencias(segunda, orografia, zona) if segunda else []
        if not filas:
            raise ErrorNorma("La base no tiene la fila de Segunda Clase que exige el artículo 101.05. Actualiza memoria-firma.")
        efectiva = filas[0]
        avisos.append(f"Tercera Clase pavimentada: se verifica con las reglas de Segunda Clase ({_regla(base, 'R101.05-1')}).")
        if v not in base.velocidades(segunda, orografia):
            avisos.append(f"{v} km/h no es una velocidad de la T204.01 para '{segunda}' en terreno '{orografia}': las tablas "
                          "de Segunda Clase sin fila para esa velocidad salen «Sin valor en la norma».")
    elif not tercera_declarada and pavimentada is False:
        avisos.append("La DG-2018 exige pavimento en esta clasificación (sección 101): se verifica como pavimentada.")

    peralte = _numero(datos.get("peralte_maximo"))
    ubicacion = efectiva["ubicacion_radios"]
    if tipo_elemento == "alineamiento" and peralte is not None:
        if "peralte" in preguntar and zona is not None:
            for u in base.ubicaciones_de_zona(zona):
                p = base.peralte_t30202(u)
                if p is not None and abs(p - peralte) <= TOLERANCIA:
                    ubicacion = u
                    break
        p_tabla = base.peralte_t30202(ubicacion)
        if p_tabla is not None and abs(p_tabla - peralte) > TOLERANCIA and not (_es_tercera(efectiva["clase"])):
            avisos.append(f"peralte_maximo {_limpio(peralte)} % no coincide con el de la T302.02 para '{ubicacion}' "
                          f"({_limpio(p_tabla)} %): el radio mínimo se verifica con esa fila de la tabla.")

    cotas = datos.get("cotas_msnm")
    return {
        "norma": NORMA,
        "tipo_elemento": tipo_elemento,
        "velocidad": v,
        "velocidades_permitidas": velocidades,
        "clasificacion": clasificacion,
        "orografia": orografia,
        "orografia_tipo": eq["orografia_tipo"],
        "zona": zona,
        "pavimentada": True if not tercera_declarada else pavimentada,
        "peralte_maximo": peralte,
        "altitud_msnm": _numero(datos.get("altitud_msnm")),
        "cotas_msnm": bool(cotas) if cotas is not None else None,
        "clase_declarada": eq["clase"],
        "clase": efectiva["clase"],
        "tipo_via": efectiva["tipo_via"],
        "demanda_veh_dia": efectiva["demanda_veh_dia"],
        "grupo_relacion_radios": efectiva["grupo_relacion_radios"],
        "ubicacion_radios": ubicacion,
        "condicion_peralte": efectiva["condicion_peralte"],
        "aplica_101_05": aplica_101_05,
        "tercera_clase": _es_tercera(efectiva["clase"]),
        "autopista": normalizar(efectiva["tipo_via"]) == "autopista",
        "avisos": avisos,
    }


# ---------------------------------------------------------------------------------------------- E0: velocidad
def comprobar_velocidad(base: BaseNorma, ctx: dict) -> dict:
    return fila("Proyecto", "Velocidad de diseño admisible", ctx["velocidad"],
                ", ".join(str(x) for x in ctx["velocidades_permitidas"]), "km/h", EN_LISTA, CUMPLE,
                base.referencia("T204.01"), nota=f"{ctx['clasificacion']}, terreno {ctx['orografia']}")


# ---------------------------------------------------------------------------------------------- E1: radio mínimo
def radio_minimo_exigido(base: BaseNorma, ctx: dict) -> dict:
    """Radio mínimo de la norma para el proyecto: T302.02 (por ubicación) y, en Tercera Clase, T302.04 (por peralte);
    si las dos tienen fila y difieren, la mayor (decisión 4). Devuelve {exigido, referencia, valor_tabla,
    valor_calculado_en_tabla, valor_formula, formula, peralte_pct, f_max, otra_tabla, aviso}; exigido None si ninguna
    tabla tiene fila para esa velocidad."""
    v = ctx["velocidad"]
    candidatas = []
    r = base.radio_t30202(ctx["ubicacion_radios"], v)
    if r:
        candidatas.append({"ref": "T302.02", "formula_id": "F08", "fila": r})
    if ctx["tercera_clase"] and ctx.get("peralte_maximo") is not None:
        r4 = base.radio_t30204(ctx["peralte_maximo"], v)
        if r4:
            candidatas.append({"ref": "T302.04", "formula_id": "F09", "fila": r4})
    if not candidatas:
        return {"exigido": None, "referencia": base.referencia("T302.04" if ctx["tercera_clase"] else "T302.02")}
    for c in candidatas:
        f = c["fila"]
        c["valor"] = f["radio_redondeado_m"]
        c["formula"] = radio_minimo_formula(v, f["peralte_max_pct"], f["f_max"])
    elegida = max(candidatas, key=lambda c: c["valor"])
    otras = [c for c in candidatas if c is not elegida]
    salida = {
        "exigido": elegida["valor"],
        "referencia": base.referencia(elegida["ref"]),
        "valor_tabla": elegida["valor"],
        "valor_calculado_en_tabla": elegida["fila"]["radio_calculado_m"],
        "valor_formula": max(c["formula"] for c in candidatas if abs(c["valor"] - elegida["valor"]) <= TOLERANCIA),
        "formula": elegida["formula_id"],
        "peralte_pct": elegida["fila"]["peralte_max_pct"],
        "f_max": elegida["fila"]["f_max"],
        "otra_tabla": None,
        "aviso": None,
    }
    if otras:
        o = otras[0]
        salida["otra_tabla"] = {"referencia": texto_referencia(base.referencia(o["ref"])), "valor": _limpio(o["valor"])}
        if abs(o["valor"] - elegida["valor"]) > TOLERANCIA:
            salida["aviso"] = (f"{texto_referencia(base.referencia('T302.02'))} y {texto_referencia(base.referencia('T302.04'))} "
                               f"dan valores distintos ({_limpio(next(c['valor'] for c in candidatas if c['ref'] == 'T302.02'))} m y "
                               f"{_limpio(next(c['valor'] for c in candidatas if c['ref'] == 'T302.04'))} m): se aplica el más exigente.")
    return salida


def comprobar_radio_minimo(base: BaseNorma, ctx: dict, radio: float, elemento: str = "Curva", progresiva=None) -> dict:
    ex = radio_minimo_exigido(base, ctx)
    comprobacion = "Radio mínimo"
    if ex["exigido"] is None:
        return fila(elemento, comprobacion, radio, None, "m", MINIMO, SIN_VALOR, ex["referencia"], progresiva,
                    nota=f"La tabla no tiene fila para {ctx['velocidad']} km/h (la norma no permite interpolar).")
    resultado = _con_aviso(resultado_tabla_formula(radio, ex["exigido"], ex["valor_formula"], MINIMO), bool(ex["aviso"]))
    nota = None
    if resultado == CUMPLE_AVISO and not ex["aviso"]:
        nota = "Cumple la tabla, pero la fórmula pide más (manda la tabla)."
    return fila(elemento, comprobacion, radio, ex["exigido"], "m", MINIMO, resultado, ex["referencia"], progresiva,
                valor_tabla=ex["valor_tabla"], valor_calculado_en_tabla=ex["valor_calculado_en_tabla"],
                valor_formula=ex["valor_formula"], formula=_formula(base, ex["formula"], ex["valor_formula"]),
                peralte_pct=ex["peralte_pct"], f_max=ex["f_max"], otra_tabla=ex["otra_tabla"], aviso=ex["aviso"], nota=nota)


# ---------------------------------------------------------------------------------------------- E2: longitud de curva
def comprobar_longitud_curva(base: BaseNorma, ctx: dict, deflexion_grados: float, longitud: float,
                             elemento: str = "Curva", progresiva=None) -> list[dict]:
    filas = []
    v = ctx["velocidad"]
    if deflexion_grados is not None and deflexion_grados <= DEFLEXION_PEQUENA_GRADOS + TOLERANCIA:
        exigido = longitud_deflexion_pequena(deflexion_grados)
        resultado = CUMPLE if cumple(longitud, exigido, MAYOR_ESTRICTO) else NO_CUMPLE
        nota = None
        if abs(deflexion_grados - DEFLEXION_PEQUENA_GRADOS) <= TOLERANCIA:
            nota = "Δ = 5°: el texto dice «≤ 5°» y la fórmula «Δ < 5°»; se aplica la regla (lo más exigente)."
        filas.append(fila(elemento, "Longitud de curva con deflexión pequeña: L > 30 (10 − Δ)", longitud, exigido, "m",
                          MAYOR_ESTRICTO, resultado, base.referencia("R302.02-1"), progresiva,
                          deflexion_grados=deflexion_grados, formula=_formula(base, "F37", exigido), nota=nota))
    lm = base.longitud_minima_curva(ctx["autopista"])
    if lm and longitud is not None:
        exigido = lm["factor_v"] * v
        ok = cumple(longitud, exigido, MINIMO)
        filas.append(fila(elemento, f"Longitud mínima de curva ({lm['expresion_pdf']})", longitud, exigido, "m", MINIMO,
                          CUMPLE if ok else AVISO, base.referencia("C302.02-LMIN"), progresiva,
                          regla=_regla(base, "R302.02-3"),
                          nota=None if ok else "Solo aviso (decisión del dueño): la norma lo pide del orden de esa longitud."))
    if longitud is not None and longitud > UMBRALES_REGLAS["R302.02-7"] + TOLERANCIA:
        filas.append(fila(elemento, "Longitud de curva preferiblemente no mayor de 800 m", longitud,
                          UMBRALES_REGLAS["R302.02-7"], "m", MAXIMO, AVISO, base.referencia("R302.02-7"), progresiva,
                          nota="Recomendación de la norma: solo aviso."))
    return filas


# ---------------------------------------------------------------------------------------------- E3: PI sin curva
def comprobar_pi_sin_curva(base: BaseNorma, ctx: dict, deflexion_grados: float, elemento: str = "PI",
                           progresiva=None) -> dict:
    r = base.deflexion_sin_curva(ctx["velocidad"])
    ref = base.referencia("C302.02-DEFLEX")
    if not r:
        return fila(elemento, "Deflexión máxima sin curva", deflexion_grados, None, "°", MAXIMO, SIN_VALOR, ref, progresiva,
                    nota=f"El cuadro solo cubre ciertas velocidades (de 30 a 80 km/h); no tiene fila para {ctx['velocidad']} km/h.")
    maximo = r["deflexion_max_grados"]
    resultado = CUMPLE if cumple(deflexion_grados, maximo, MAXIMO) else NO_CUMPLE
    return fila(elemento, "Deflexión máxima sin curva", deflexion_grados, maximo, "°", MAXIMO, resultado, ref, progresiva,
                valor_tabla_texto=r["deflexion_texto_pdf"], regla=_regla(base, "R302.02-4"),
                nota=None if resultado == CUMPLE else "El PI necesita curva circular.")


def comprobar_deflexion_minima(base: BaseNorma, ctx: dict, deflexion_grados: float, elemento: str = "PI",
                               progresiva=None) -> dict | None:
    """R302.02-2: deflexiones menores de 0°59′ (solo aviso: está pendiente del dueño si es «No cumple»)."""
    if deflexion_grados is None or not deflexion_grados < DEFLEXION_MINIMA_GRADOS - TOLERANCIA:
        return None
    return fila(elemento, "Deflexión no menor de 0°59′", deflexion_grados, DEFLEXION_MINIMA_GRADOS, "°", MINIMO, AVISO,
                base.referencia("R302.02-2"), progresiva,
                nota="Solo aviso: la norma no aclara si es la deflexión del PI o de la curva (pendiente del dueño).")


# ---------------------------------------------------------------------------------------------- E4/E5: tangentes
def comprobar_tangente_minima(base: BaseNorma, ctx: dict, longitud: float, mismo_sentido: bool,
                              elemento: str = "Tangente", progresiva=None) -> list[dict]:
    v = ctx["velocidad"]
    t = base.longitud_tangente(v)
    filas = []
    if mismo_sentido:
        comprobacion = "Tangente mínima entre curvas del mismo sentido (L mín.o)"
        tabla = t["l_min_o_m"] if t else None
        valor_formula, formula_id = tangente_minima_o(v), "F35"
        t2 = base.tangente_mismo_sentido(v)
        candidatas = [(tabla, "T302.01"), (t2["longitud_min_m"] if t2 else None, "T304.08")]
    else:
        comprobacion = "Tangente mínima entre curvas de sentido contrario (L mín.s)"
        tabla = t["l_min_s_m"] if t else None
        valor_formula, formula_id = tangente_minima_s(v), "F34"
        candidatas = [(tabla, "T302.01")]
    candidatas = [(valor, ref) for valor, ref in candidatas if valor is not None]
    if not candidatas:
        filas.append(fila(elemento, comprobacion, longitud, None, "m", MINIMO, SIN_VALOR, base.referencia("T302.01"),
                          progresiva, nota=f"La tabla no tiene fila para {v} km/h."))
    else:
        exigido, ref = max(candidatas, key=lambda c: c[0])
        aviso = None
        otra = None
        if len(candidatas) > 1:
            otro_valor, otra_ref = next(c for c in candidatas if c[1] != ref)
            otra = {"referencia": texto_referencia(base.referencia(otra_ref)), "valor": _limpio(otro_valor)}
            if abs(otro_valor - exigido) > TOLERANCIA:
                aviso = (f"{texto_referencia(base.referencia('T302.01'))} da {_limpio(tabla)} m y "
                         f"{texto_referencia(base.referencia('T304.08'))} da {_limpio(t2['longitud_min_m'])} m: "
                         "se aplica el más exigente.")
        formula = valor_formula if ref == "T302.01" else None
        resultado = _con_aviso(resultado_tabla_formula(longitud, exigido, formula, MINIMO), bool(aviso))
        filas.append(fila(elemento, comprobacion, longitud, exigido, "m", MINIMO, resultado, base.referencia(ref), progresiva,
                          valor_tabla=tabla, valor_formula=valor_formula, formula=_formula(base, formula_id, valor_formula),
                          otra_tabla=otra, aviso=aviso))
    if mismo_sentido:
        for ident, texto in (("R302.02-8", "Tangente entre curvas del mismo sentido mayor de 500 m"),
                             ("R302.06.03-1", "Evitar menos de 400 m entre curvas del mismo sentido")):
            umbral = UMBRALES_REGLAS[ident]
            if longitud < umbral - TOLERANCIA:
                filas.append(fila(elemento, texto, longitud, umbral, "m", MINIMO, AVISO, base.referencia(ident), progresiva,
                                  nota="Solo aviso: pendiente del dueño si entra en «el más exigente»"
                                       + (" (en Tercera Clase puede ser menor)." if ident == "R302.02-8" else ".")))
    return filas


def comprobar_tangente_maxima(base: BaseNorma, ctx: dict, longitud: float, elemento: str = "Tangente",
                              progresiva=None) -> dict:
    v = ctx["velocidad"]
    t = base.longitud_tangente(v)
    ref = base.referencia("T302.01")
    if not t or t["l_max_m"] is None:
        return fila(elemento, "Tangente máxima deseable (L máx)", longitud, None, "m", MAXIMO, SIN_VALOR, ref, progresiva,
                    nota=f"La tabla no tiene fila para {v} km/h.")
    valor_formula = tangente_maxima(v)
    resultado = resultado_tabla_formula(longitud, t["l_max_m"], valor_formula, MAXIMO)
    if resultado == NO_CUMPLE:
        resultado = AVISO
    return fila(elemento, "Tangente máxima deseable (L máx)", longitud, t["l_max_m"], "m", MAXIMO, resultado, ref, progresiva,
                valor_formula=valor_formula, formula=_formula(base, "F36", valor_formula),
                nota="Solo aviso: es una longitud deseable." if resultado != CUMPLE else None)


# ---------------------------------------------------------------------------------------------- E6: relación de radios
def comprobar_relacion_radios(base: BaseNorma, ctx: dict, radio_entrada: float, radio_salida: float,
                              elemento: str = "Curvas", progresiva=None) -> dict:
    grupo = ctx["grupo_relacion_radios"]
    ref = base.referencia("T302.07" if grupo == 1 else "T302.08")
    comprobacion = "Relación entre radios consecutivos"
    filas = base.relacion_radios(grupo)
    if not filas or radio_entrada < filas[0]["radio_entrada_m"] - TOLERANCIA or radio_entrada > filas[-1]["radio_entrada_m"] + TOLERANCIA:
        rango = f"{_limpio(filas[0]['radio_entrada_m'])} a {_limpio(filas[-1]['radio_entrada_m'])} m" if filas else "ninguno"
        return fila(elemento, comprobacion, radio_salida, None, "m", RANGO, SIN_VALOR, ref, progresiva,
                    radio_entrada=radio_entrada, nota=f"La tabla del grupo {grupo} cubre radios de entrada de {rango}.")
    exactas = [f for f in filas if abs(f["radio_entrada_m"] - radio_entrada) <= TOLERANCIA]
    aviso = None
    if exactas:
        usadas = exactas
        if len(exactas) > 1:
            aviso = (f"La tabla tiene {len(exactas)} filas con radio de entrada {_limpio(radio_entrada)} m (posible errata): "
                     "se aplica la más exigente.")
    else:
        debajo = [f for f in filas if f["radio_entrada_m"] < radio_entrada][-1]
        encima = [f for f in filas if f["radio_entrada_m"] > radio_entrada][0]
        usadas = [debajo, encima]
        aviso = (f"El radio de entrada {_limpio(radio_entrada)} m está entre dos filas ({_limpio(debajo['radio_entrada_m'])} y "
                 f"{_limpio(encima['radio_entrada_m'])} m): se aplica la más exigente.")
    minimo = max(f["salida_min_m"] for f in usadas if f["salida_min_m"] is not None)
    topes = [f["salida_max_m"] for f in usadas if f["salida_max_m"] is not None and not _sin_tope(f)]
    maximo = min(topes) if topes else None
    ok = radio_salida >= minimo - TOLERANCIA and (maximo is None or radio_salida <= maximo + TOLERANCIA)
    resultado = _con_aviso(CUMPLE if ok else NO_CUMPLE, bool(aviso))
    return fila(elemento, comprobacion, radio_salida, {"minimo": minimo, "maximo": maximo}, "m", RANGO, resultado, ref,
                progresiva, radio_entrada=radio_entrada, grupo=grupo, regla=_regla(base, "R302.04.05-1"), aviso=aviso,
                nota=None if maximo is not None else "Sin tope superior en la tabla.")


def _sin_tope(f: dict) -> bool:
    operador = (f.get("salida_max_operador") or "").strip()
    return operador in (">", "&gt;", "≥")


def comprobar_radio_tras_recta_larga(base: BaseNorma, ctx: dict, longitud_recta: float, radio: float,
                                     elemento: str = "Curva", progresiva=None) -> dict | None:
    """R302.04.05-3: en autopistas, tras una recta de más de 400 m el radio de salida es de 700 m o más."""
    if not ctx["autopista"] or longitud_recta is None or not longitud_recta > RECTA_LARGA_AUTOPISTA_M + TOLERANCIA:
        return None
    umbral = UMBRALES_REGLAS["R302.04.05-3"]
    return fila(elemento, "Radio tras una recta de más de 400 m (autopista)", radio, umbral, "m", MINIMO,
                CUMPLE if cumple(radio, umbral, MINIMO) else NO_CUMPLE, base.referencia("R302.04.05-3"), progresiva,
                longitud_recta=longitud_recta)


# ---------------------------------------------------------------------------------------------- E7: transición
def comprobar_transicion(base: BaseNorma, ctx: dict, radio: float, elemento: str = "Curva", progresiva=None) -> dict:
    """T302.11: se prescinde de la curva de transición con radio igual o mayor que el de la tabla (en la B, de Tercera
    Clase, «superior»: igual no basta). El plugin solo crea arcos, así que si hace falta es «No cumple»."""
    ident = "T302.11B" if ctx["tercera_clase"] else "T302.11A"
    ref = base.referencia(ident)
    r = base.radio_sin_transicion(ident, ctx["velocidad"])
    comprobacion = "Radio para prescindir de la curva de transición"
    if not r:
        return fila(elemento, comprobacion, radio, None, "m", MINIMO, SIN_VALOR, ref, progresiva,
                    nota=f"La tabla no tiene fila para {ctx['velocidad']} km/h.")
    criterio = MAYOR_ESTRICTO if ident == "T302.11B" else MINIMO
    ok = cumple(radio, r["radio_m"], criterio)
    return fila(elemento, comprobacion, radio, r["radio_m"], "m", criterio, CUMPLE if ok else NO_CUMPLE, ref, progresiva,
                nota=None if ok else "Requiere curva de transición; el plugin solo crea arcos (pendiente de confirmar con el dueño).")


# ---------------------------------------------------------------------------------------------- E8: compuestas
def comprobar_curva_compuesta(base: BaseNorma, ctx: dict, radio_1: float, radio_2: float, elemento: str = "Curvas",
                              progresiva=None) -> dict:
    """Dos curvas del mismo sentido sin tangente: aviso de curva compuesta y R mayor ≤ 1,5 R menor (302.06.02)."""
    mayor, menor = max(radio_1, radio_2), min(radio_1, radio_2)
    relacion = mayor / menor if menor > 0 else math.inf
    umbral = UMBRALES_REGLAS["R302.06.02-1"]
    ok = cumple(relacion, umbral, MAXIMO)
    return fila(elemento, "Curva compuesta: R mayor ≤ 1,5 R menor", relacion, umbral, "veces", MAXIMO,
                CUMPLE_AVISO if ok else NO_CUMPLE, base.referencia("R302.06.02-1"), progresiva,
                radio_mayor=mayor, radio_menor=menor,
                aviso="Curva compuesta: dos curvas del mismo sentido sin tangente entre ellas.")


# ---------------------------------------------------------------------------------------------- R1/R2: pendientes
def pendiente_maxima_exigida(base: BaseNorma, ctx: dict, altitud_msnm=None) -> dict:
    """T303.01 para el proyecto, con −1 % por encima de 3000 msnm en terreno accidentado o escarpado (3000 justos no)
    y el máximo excepcional +1 % (en ese orden; pendiente del dueño). Devuelve {exigido, valor_tabla, rebaja_altitud,
    excepcional, referencia}; exigido None si la celda está vacía."""
    r = base.pendiente_maxima(ctx["tipo_via"], ctx["clase"], ctx["demanda_veh_dia"], ctx["orografia_tipo"], ctx["velocidad"])
    salida = {"referencia": base.referencia("T303.01"), "exigido": None, "valor_tabla": None, "rebaja_altitud": False,
              "excepcional": None}
    if not r or r["pendiente_max_pct"] is None:
        return salida
    maximo = r["pendiente_max_pct"]
    salida["valor_tabla"] = maximo
    if ctx["orografia_tipo"] in (3, 4) and altitud_msnm is not None and altitud_msnm - ALTITUD_REBAJA_MSNM > TOLERANCIA:
        maximo -= UMBRALES_REGLAS["R303.03.02-1"]
        salida["rebaja_altitud"] = True
    salida["exigido"] = maximo
    salida["excepcional"] = maximo + UMBRALES_REGLAS["R303.03.03-1"]
    return salida


def comprobar_pendiente_maxima(base: BaseNorma, ctx: dict, pendiente_pct: float, altitud_msnm=None,
                               elemento: str = "Tramo", progresiva=None) -> dict:
    ex = pendiente_maxima_exigida(base, ctx, altitud_msnm)
    p = abs(pendiente_pct)
    comprobacion = "Pendiente máxima"
    if ex["exigido"] is None:
        return fila(elemento, comprobacion, p, None, "%", MAXIMO, SIN_VALOR, ex["referencia"], progresiva,
                    nota=f"Celda vacía de la T303.01 para {ctx['clase']} ({ctx['demanda_veh_dia']} veh/día), orografía "
                         f"tipo {ctx['orografia_tipo']} y {ctx['velocidad']} km/h: no aplica.")
    ok = cumple(p, ex["exigido"], MAXIMO)
    notas = []
    if ex["rebaja_altitud"]:
        notas.append(f"−1 % por estar por encima de 3000 msnm ({_regla(base, 'R303.03.02-1')}); el orden −1 % y +1 % "
                     "excepcional está pendiente del dueño.")
    if not ok and cumple(p, ex["excepcional"], MAXIMO):
        notas.append(f"Solo como excepción con justificación técnica y económica, hasta {_limpio(ex['excepcional'])} % "
                     f"({_regla(base, 'R303.03.03-1')}).")
    if not ok and ctx["autopista"]:
        notas.append(f"En autopistas la norma permite más en bajadas ({_regla(base, 'R303.03.02-2')}): compruébalo con el "
                     "sentido de la calzada.")
    return fila(elemento, comprobacion, p, ex["exigido"], "%", MAXIMO, CUMPLE if ok else NO_CUMPLE, ex["referencia"],
                progresiva, valor_tabla=ex["valor_tabla"], maximo_excepcional=ex["excepcional"],
                altitud_msnm=altitud_msnm, nota=" ".join(notas) or None)


def comprobar_pendiente_minima(base: BaseNorma, ctx: dict, pendiente_pct: float, elemento: str = "Tramo",
                               progresiva=None) -> dict | None:
    p = abs(pendiente_pct)
    umbral = UMBRALES_REGLAS["R303.03.01-1"]
    if p >= umbral - TOLERANCIA:
        return None
    return fila(elemento, "Pendiente mínima del orden de 0,5 %", p, umbral, "%", MINIMO, AVISO,
                base.referencia("R303.03.01-1"), progresiva,
                nota="Solo aviso: la norma admite menos según el bombeo, las bermas y las cunetas (R303.03.01-2 a -5).")


# ---------------------------------------------------------------------------------------------- R3-R5: curvas verticales
def comprobar_curva_obligatoria(base: BaseNorma, ctx: dict, a_pct: float, tiene_curva: bool, elemento: str = "PVI",
                                progresiva=None) -> dict:
    pavimentada = ctx["pavimentada"] is not False
    ident = "R303.04.01-1" if pavimentada else "R303.04.01-2"
    umbral = UMBRALES_REGLAS[ident]
    necesita = a_pct - umbral > TOLERANCIA
    resultado = CUMPLE if (not necesita or tiene_curva) else NO_CUMPLE
    nota = ("No hace falta curva vertical." if not necesita else
            "Tiene curva vertical." if tiene_curva else "Falta la curva vertical.")
    return fila(elemento, f"Curva vertical obligatoria si A > {_limpio(umbral)} %", a_pct, umbral, "%", CONDICION,
                resultado, base.referencia(ident), progresiva, tiene_curva=tiene_curva,
                pavimentada=pavimentada, nota=nota)


def longitud_curva_vertical_exigida(base: BaseNorma, ctx: dict, pe_pct: float, ps_pct: float) -> dict:
    """Longitud mínima de una curva vertical con visibilidad de parada. Tercera Clase: K de la T303.02 (convexa) o
    T303.03 (cóncava) por A, y la fórmula con la distancia de esa fila; demás clases: solo la fórmula, con Dp de la
    T205.01. Devuelve {convexa, a_pct, exigido, valor_tabla, k_tabla, valor_formula, formula, distancia, referencia,
    referencia_distancia}."""
    a = abs(ps_pct - pe_pct)
    convexa = pe_pct > ps_pct
    salida = {"convexa": convexa, "a_pct": a, "exigido": None, "valor_tabla": None, "k_tabla": None,
              "valor_formula": None, "formula": None, "distancia": None, "referencia": None, "referencia_distancia": None}
    v = ctx["velocidad"]
    if ctx["tercera_clase"]:
        ident = "T303.02" if convexa else "T303.03"
        salida["referencia"] = base.referencia(ident)
        k = base.indice_k("convexa" if convexa else "concava", v, "parada")
        if not k:
            return salida
        salida["k_tabla"] = k["k"]
        salida["valor_tabla"] = k["k"] * a
        salida["distancia"] = k["distancia_visibilidad_m"]
        salida["referencia_distancia"] = texto_referencia(base.referencia(ident))
    else:
        vp = base.visibilidad_parada(v)
        if not vp:
            salida["referencia"] = base.referencia("T205.01")
            return salida
        salida["distancia"] = vp["dp_redondeada_m"]
        salida["referencia_distancia"] = texto_referencia(base.referencia("T205.01"))
    if salida["distancia"] is not None:
        valor, ident_f = (longitud_convexa if convexa else longitud_concava)(a, salida["distancia"])
        salida["valor_formula"], salida["formula"] = valor, ident_f
        if salida["referencia"] is None and ident_f:
            salida["referencia"] = base.referencia(ident_f)
    salida["exigido"] = salida["valor_tabla"] if salida["valor_tabla"] is not None else salida["valor_formula"]
    return salida


def comprobar_curva_vertical(base: BaseNorma, ctx: dict, pe_pct: float, ps_pct: float, longitud: float,
                             elemento: str = "PVI", progresiva=None) -> dict:
    ex = longitud_curva_vertical_exigida(base, ctx, pe_pct, ps_pct)
    tipo = "convexa" if ex["convexa"] else "cóncava"
    comprobacion = f"Longitud de curva vertical {tipo} (visibilidad de parada)"
    if ex["exigido"] is None:
        return fila(elemento, comprobacion, longitud, None, "m", MINIMO, SIN_VALOR, ex["referencia"], progresiva,
                    a_pct=ex["a_pct"], nota=f"La tabla no tiene fila para {ctx['velocidad']} km/h.")
    resultado = resultado_tabla_formula(longitud, ex["valor_tabla"], ex["valor_formula"], MINIMO)
    nota = None
    if resultado == CUMPLE_AVISO:
        nota = "Cumple la tabla (K × A), pero la fórmula pide más (manda la tabla)."
    return fila(elemento, comprobacion, longitud, ex["exigido"], "m", MINIMO, resultado, ex["referencia"], progresiva,
                a_pct=ex["a_pct"], k_tabla=ex["k_tabla"], k_diseno=(longitud / ex["a_pct"] if ex["a_pct"] else None),
                valor_tabla=ex["valor_tabla"], valor_formula=ex["valor_formula"],
                formula=_formula(base, ex["formula"], ex["valor_formula"]) if ex["formula"] else None,
                distancia_visibilidad_m=ex["distancia"], distancia_de=ex["referencia_distancia"], nota=nota)


# ---------------------------------------------------------------------------------------------- R6-R9: perfil
def _tramos(pvis: list) -> list[dict]:
    salida = []
    for a, b in zip(pvis, pvis[1:]):
        s0, s1 = float(a["progresiva"]), float(b["progresiva"])
        if s1 - s0 <= 1e-6:
            continue
        p = a.get("ps_pct")
        if p is None:
            p = (float(b["cota"]) - float(a["cota"])) / (s1 - s0) * 100.0
        salida.append({"desde": a, "hasta": b, "inicio": s0, "fin": s1, "longitud": s1 - s0, "pendiente": float(p),
                       "cota_max": max(float(a["cota"]), float(b["cota"])),
                       "elemento": f"Tramo PVI {a['n']}–PVI {b['n']}",
                       "progresiva": f"{progresiva_texto(s0)} a {progresiva_texto(s1)}"})
    return salida


def _rachas(tramos: list, condicion) -> list[list]:
    """Grupos de tramos seguidos que cumplen `condicion` y tienen el mismo signo de pendiente."""
    grupos, actual = [], []
    for t in tramos:
        if condicion(t) and (not actual or (t["pendiente"] > 0) == (actual[-1]["pendiente"] > 0)):
            actual.append(t)
            continue
        if actual:
            grupos.append(actual)
        actual = [t] if condicion(t) else []
    if actual:
        grupos.append(actual)
    return grupos


def _elemento_racha(racha: list) -> tuple[str, str]:
    a, b = racha[0], racha[-1]
    return (f"Tramos PVI {a['desde']['n']}–PVI {b['hasta']['n']}",
            f"{progresiva_texto(a['inicio'])} a {progresiva_texto(b['fin'])}")


def comprobar_tramos_mas_de_10(base: BaseNorma, ctx: dict, tramos: list) -> list[dict]:
    """R303.03.03-3 (Tercera Clase): los tramos de más del 10 % no pasan de 180 m. Tramos seguidos del mismo signo
    cuentan juntos (la norma no define «tramo»: es lo más exigente)."""
    umbral = UMBRALES_REGLAS["R303.03.03-3"]
    filas = []
    for racha in _rachas(tramos, lambda t: abs(t["pendiente"]) - PENDIENTE_TRAMO_LARGO_PCT > TOLERANCIA):
        longitud = sum(t["longitud"] for t in racha)
        elemento, progresiva = _elemento_racha(racha)
        filas.append(fila(elemento, "Longitud de tramo con pendiente mayor del 10 %", longitud, umbral, "m", MAXIMO,
                          CUMPLE if cumple(longitud, umbral, MAXIMO) else NO_CUMPLE, base.referencia("R303.03.03-3"),
                          progresiva, pendiente_max_pct=max(abs(t["pendiente"]) for t in racha),
                          nota="Tramos seguidos de más del 10 % y del mismo sentido cuentan juntos."))
    return filas


def _cota_en(pvis: list, s: float) -> float:
    for a, b in zip(pvis, pvis[1:]):
        s0, s1 = float(a["progresiva"]), float(b["progresiva"])
        if s0 - 1e-9 <= s <= s1 + 1e-9 and s1 > s0:
            return float(a["cota"]) + (float(b["cota"]) - float(a["cota"])) * (s - s0) / (s1 - s0)
    return float(pvis[-1]["cota"])


def comprobar_pendiente_media(base: BaseNorma, ctx: dict, pvis: list) -> dict | None:
    """R303.03.03-4 (Tercera Clase): pendiente media no mayor del 6 % en tramos de más de 2000 m. Se mira cada
    ventana de 2000 m de la rasante entre PVI (sin curvas verticales) y se informa la peor."""
    if len(pvis) < 2:
        return None
    inicio, fin = float(pvis[0]["progresiva"]), float(pvis[-1]["progresiva"])
    if fin - inicio <= VENTANA_PENDIENTE_MEDIA_M + TOLERANCIA:
        return None
    candidatos = set()
    for p in pvis:
        s = float(p["progresiva"])
        for c in (s, s - VENTANA_PENDIENTE_MEDIA_M):
            if inicio - 1e-9 <= c <= fin - VENTANA_PENDIENTE_MEDIA_M + 1e-9:
                candidatos.add(c)
    peor, desde = -1.0, inicio
    for c in sorted(candidatos):
        media = abs(_cota_en(pvis, c + VENTANA_PENDIENTE_MEDIA_M) - _cota_en(pvis, c)) / VENTANA_PENDIENTE_MEDIA_M * 100.0
        if media > peor:
            peor, desde = media, c
    umbral = UMBRALES_REGLAS["R303.03.03-4"]
    return fila("Rasante (ventana de 2000 m)", "Pendiente media en más de 2000 m", peor, umbral, "%", MAXIMO,
                CUMPLE if cumple(peor, umbral, MAXIMO) else NO_CUMPLE, base.referencia("R303.03.03-4"),
                f"{progresiva_texto(desde)} a {progresiva_texto(desde + VENTANA_PENDIENTE_MEDIA_M)}",
                nota="La peor ventana de 2000 m, entre PVI y sin curvas verticales.")


def comprobar_descansos(base: BaseNorma, ctx: dict, tramos: list) -> list[dict]:
    """R303.03.03-2 (Tercera Clase): en subidas continuas de más del 5 %, un descanso cada 3 km o así (solo aviso).
    Una subida continua son tramos seguidos del mismo sentido de más del 2 % con al menos uno de más del 5 %."""
    filas = []
    for racha in _rachas(tramos, lambda t: abs(t["pendiente"]) - PENDIENTE_DESCANSO_PCT > TOLERANCIA):
        longitud = sum(t["longitud"] for t in racha)
        if longitud > LONGITUD_SIN_DESCANSO_M + TOLERANCIA and any(
                abs(t["pendiente"]) - PENDIENTE_SUBIDA_CONTINUA_PCT > TOLERANCIA for t in racha):
            elemento, progresiva = _elemento_racha(racha)
            filas.append(fila(elemento, "Descanso en subidas continuas de más del 5 %", longitud, LONGITUD_SIN_DESCANSO_M,
                              "m", MAXIMO, AVISO, base.referencia("R303.03.03-2"), progresiva,
                              nota="Solo aviso: más de 3 km sin un tramo de al menos 500 m con 2 % o menos; revísalo a mano."))
    return filas


def comprobar_pendiente_en_curvas_cerradas(base: BaseNorma, ctx: dict, tramos: list, pis: list) -> list[dict]:
    """R303.03.03-5 (Tercera Clase): en curvas de radio menor de 50 m, evitar pendientes de más del 8 % (aviso)."""
    umbral = UMBRALES_REGLAS["R303.03.03-5"]
    filas = []
    for pi in pis or []:
        radio, pc, pt = pi.get("radio"), pi.get("progresiva_pc"), pi.get("progresiva_pt")
        if pi.get("tipo") != "curva" or radio is None or pc is None or pt is None or radio >= RADIO_CURVA_CERRADA_M:
            continue
        for t in tramos:
            if t["inicio"] < pt and t["fin"] > pc and abs(t["pendiente"]) - umbral > TOLERANCIA:
                filas.append(fila(t["elemento"], "Pendiente en curva de radio menor de 50 m", abs(t["pendiente"]), umbral,
                                  "%", MAXIMO, AVISO, base.referencia("R303.03.03-5"), t["progresiva"],
                                  curva=f"PI {pi.get('numero')} (R {_limpio(radio)} m)", nota="Recomendación: solo aviso."))
    return filas


def comprobar_lomo_quebrado(base: BaseNorma, ctx: dict, pvis: list) -> list[dict]:
    """303.02: evitar dos curvas verticales del mismo sentido unidas por una tangente corta. La norma no da longitud:
    aviso para cada par de PVI seguidos del mismo sentido."""
    ref = referencia_seccion(**SECCION_LOMO_QUEBRADO)
    interiores = [p for p in pvis if p.get("pe_pct") is not None and p.get("ps_pct") is not None
                  and abs(float(p["ps_pct"]) - float(p["pe_pct"])) > TOLERANCIA]
    filas = []
    for a, b in zip(interiores, interiores[1:]):
        if b["n"] != a["n"] + 1:
            continue
        convexa_a, convexa_b = float(a["pe_pct"]) > float(a["ps_pct"]), float(b["pe_pct"]) > float(b["ps_pct"])
        if convexa_a == convexa_b:
            tangente = float(b["progresiva"]) - float(a["progresiva"])
            filas.append(fila(f"PVI {a['n']} y PVI {b['n']}", "Lomo quebrado (dos curvas del mismo sentido seguidas)",
                              tangente, None, "m", CONDICION, AVISO, ref,
                              f"{progresiva_texto(float(a['progresiva']))} a {progresiva_texto(float(b['progresiva']))}",
                              sentido="convexas" if convexa_a else "cóncavas",
                              nota="La norma no da longitud mínima: revisa si la tangente entre las dos curvas es corta."))
    return filas


# ---------------------------------------------------------------------------------------------- eje completo
def unidades_no_validas(tabla_pis: dict) -> str | None:
    """Motivo para no verificar si el dibujo no está en metros (None si está en metros)."""
    unidades = (tabla_pis or {}).get("unidades")
    if not unidades:
        return "No se pudieron leer las unidades del dibujo (listar_pis devolvió 'unidades' vacío): no se verifica."
    if str(unidades) != UNIDADES_METROS:
        return (f"El dibujo está en '{unidades}', no en metros (Meters): la DG-2018 está en metros y no se verifica. "
                "Cambia las unidades del dibujo o verifica a mano.")
    return None


def _etiqueta(pi: dict) -> str:
    return f"PI {pi.get('numero')}"


def verificar_alineamiento(base: BaseNorma, ctx: dict, tabla_pis: dict) -> tuple[list, list]:
    """Filas (en el orden del eje) y lista de lo no comprobado para un eje leído con listar_pis."""
    no_comprobado = [dict(x) for x in NO_COMPROBADO_ALINEAMIENTO]
    pis = tabla_pis.get("pis") or []

    # Puntos del eje: inicio → (PC, PT) de cada curva / PI sin curva → fin. Una tangente junto a un tipo no soportado
    # no tiene longitud conocida.
    tangentes = []
    previo = ("inicio", (tabla_pis.get("inicio") or {}).get("progresiva"), None)
    for pi in pis:
        tipo = pi.get("tipo")
        if tipo == "curva":
            entrada, salida = pi.get("progresiva_pc"), pi.get("progresiva_pt")
        elif tipo == "sin_curva":
            entrada = salida = pi.get("progresiva")
        else:
            entrada = salida = None
        tangentes.append({"desde": previo[0], "hasta": _etiqueta(pi), "inicio": previo[1], "fin": entrada,
                          "pi_antes": previo[2], "pi_despues": pi})
        previo = (_etiqueta(pi), salida, pi)
    tangentes.append({"desde": previo[0], "hasta": "fin", "inicio": previo[1],
                      "fin": (tabla_pis.get("fin") or {}).get("progresiva"), "pi_antes": previo[2], "pi_despues": None})

    pares = []   # (orden en el eje, fila): la tangente i va antes del PI i
    for i, pi in enumerate(pis):
        filas = []
        tipo = pi.get("tipo")
        prog = pi.get("progresiva_texto") or progresiva_texto(pi.get("progresiva"))
        deflexion = pi.get("deflexion_grados")
        if tipo == "curva":
            elemento = f"Curva {_etiqueta(pi)}"
            radio, longitud = pi.get("radio"), pi.get("longitud_curva")
            filas.append(comprobar_radio_minimo(base, ctx, radio, elemento, prog))
            filas.extend(comprobar_longitud_curva(base, ctx, deflexion, longitud, elemento, prog))
            filas.append(comprobar_transicion(base, ctx, radio, elemento, prog))
            aviso = comprobar_deflexion_minima(base, ctx, deflexion, elemento, prog)
            if aviso:
                filas.append(aviso)
        elif tipo == "sin_curva":
            elemento = f"{_etiqueta(pi)} (sin curva)"
            filas.append(comprobar_pi_sin_curva(base, ctx, deflexion, elemento, prog))
            aviso = comprobar_deflexion_minima(base, ctx, deflexion, elemento, prog)
            if aviso:
                filas.append(aviso)
        else:
            elemento = f"{_etiqueta(pi)} ({tipo})"
            filas.append(fila(elemento, "Tipo de entidad", None, None, None, CONDICION, NO_VERIFICABLE, None, prog,
                              nota=f"Tipo no soportado todavía ({tipo}): espirales y otros tipos no se verifican."))
            no_comprobado.append({"que": elemento, "motivo": "tipo no soportado todavía"})
        pares.extend((2 * i + 1, f) for f in filas)

    for i, t in enumerate(tangentes):
        filas = []
        elemento = f"Tangente {t['desde']}–{t['hasta']}"
        if t["inicio"] is None or t["fin"] is None:
            filas.append(fila(elemento, "Tangente", None, None, "m", CONDICION, NO_VERIFICABLE, None, None,
                              nota="Junto a un tipo no soportado: no se conoce su longitud."))
            pares.extend((2 * i, f) for f in filas)
            continue
        longitud = float(t["fin"]) - float(t["inicio"])
        prog = f"{progresiva_texto(t['inicio'])} a {progresiva_texto(t['fin'])}"
        if longitud < -TOLERANCIA:
            filas.append(fila(elemento, "Tangente", longitud, None, "m", CONDICION, NO_VERIFICABLE, None, prog,
                              nota="Longitud negativa: las curvas se solapan."))
            pares.extend((2 * i, f) for f in filas)
            continue
        longitud = max(longitud, 0.0)
        antes, despues = t["pi_antes"], t["pi_despues"]
        entre_curvas = (antes is not None and despues is not None and antes.get("tipo") == "curva"
                        and despues.get("tipo") == "curva")
        if longitud > TOLERANCIA:
            filas.append(comprobar_tangente_maxima(base, ctx, longitud, elemento, prog))
        if entre_curvas:
            giros = (antes.get("giro"), despues.get("giro"))
            mismo_sentido = giros[0] == giros[1] and giros[0] in ("izquierda", "derecha")
            pareja = f"Curvas {_etiqueta(antes)} y {_etiqueta(despues)}"
            if longitud <= TOLERANCIA and mismo_sentido:
                filas.append(comprobar_curva_compuesta(base, ctx, antes["radio"], despues["radio"], pareja, prog))
            else:
                filas.extend(comprobar_tangente_minima(base, ctx, longitud, mismo_sentido, elemento, prog))
            if longitud <= UMBRALES_REGLAS["R302.04.05-1"] + TOLERANCIA:
                filas.append(comprobar_relacion_radios(base, ctx, antes["radio"], despues["radio"],
                                                       f"{_etiqueta(antes)} → {_etiqueta(despues)}", prog))
                filas.append(comprobar_relacion_radios(base, ctx, despues["radio"], antes["radio"],
                                                       f"{_etiqueta(despues)} → {_etiqueta(antes)}", prog))
        for pi in (antes, despues):
            if pi is not None and pi.get("tipo") == "curva":
                r = comprobar_radio_tras_recta_larga(base, ctx, longitud, pi["radio"], f"Curva {_etiqueta(pi)}", prog)
                if r:
                    filas.append(r)
        pares.extend((2 * i, f) for f in filas)
    pares.sort(key=lambda par: par[0])
    return [comprobar_velocidad(base, ctx)] + [f for _, f in pares], no_comprobado


# ---------------------------------------------------------------------------------------------- rasante completa
def verificar_rasante(base: BaseNorma, ctx: dict, pvis: list, tabla_pis: dict | None = None) -> tuple[list, list]:
    """Filas (en el orden del perfil) y lista de lo no comprobado para una rasante leída con listar_pvis (y el eje con
    listar_pis)."""
    no_comprobado = [dict(x) for x in NO_COMPROBADO_RASANTE]
    pvis = sorted(pvis or [], key=lambda p: float(p["progresiva"]))
    tramos = _tramos(pvis)
    pares = []   # (progresiva, orden, fila): cada PVI va antes del tramo que sale de él
    for t in tramos:
        filas = []
        altitud = None
        if ctx.get("altitud_msnm") is not None:
            altitud = ctx["altitud_msnm"]
        if ctx.get("cotas_msnm"):
            altitud = t["cota_max"] if altitud is None else max(altitud, t["cota_max"])
        filas.append(comprobar_pendiente_maxima(base, ctx, t["pendiente"], altitud, t["elemento"], t["progresiva"]))
        aviso = comprobar_pendiente_minima(base, ctx, t["pendiente"], t["elemento"], t["progresiva"])
        if aviso:
            filas.append(aviso)
        pares.extend((t["inicio"], 1, f) for f in filas)
    for p in pvis:
        pe, ps = p.get("pe_pct"), p.get("ps_pct")
        if pe is None or ps is None:
            continue
        pe, ps = float(pe), float(ps)
        a = p.get("a_pct")
        a = abs(ps - pe) if a is None else float(a)
        elemento = f"PVI {p['n']}"
        prog = progresiva_texto(float(p["progresiva"]))
        tiene = bool(p.get("tiene_curva"))
        pares.append((float(p["progresiva"]), 0, comprobar_curva_obligatoria(base, ctx, a, tiene, elemento, prog)))
        if tiene and a > TOLERANCIA and p.get("longitud_curva") is not None:
            pares.append((float(p["progresiva"]), 0, comprobar_curva_vertical(base, ctx, pe, ps, float(p["longitud_curva"]),
                                                                                elemento, prog)))
    pares.sort(key=lambda par: (par[0], par[1]))
    filas = [comprobar_velocidad(base, ctx)] + [f for _, _, f in pares]
    if ctx["tercera_clase"]:
        filas.extend(comprobar_tramos_mas_de_10(base, ctx, tramos))
        media = comprobar_pendiente_media(base, ctx, pvis)
        if media:
            filas.append(media)
        filas.extend(comprobar_descansos(base, ctx, tramos))
        filas.extend(comprobar_pendiente_en_curvas_cerradas(base, ctx, tramos, (tabla_pis or {}).get("pis")))
    filas.extend(comprobar_lomo_quebrado(base, ctx, pvis))
    if ctx["autopista"]:
        no_comprobado.append({"que": "Pendiente mayor en bajadas de autopista (R303.03.02-2)",
                              "motivo": "depende del sentido de circulación de cada calzada"})
    return filas, no_comprobado


# ---------------------------------------------------------------------------------------------- resultado
def resumir(filas: list) -> dict:
    resumen = {r: 0 for r in RESULTADOS}
    for f in filas:
        resumen[f["resultado"]] = resumen.get(f["resultado"], 0) + 1
    return resumen


def _celda(valor) -> str:
    if valor is None:
        return "—"
    if isinstance(valor, dict):
        minimo, maximo = valor.get("minimo"), valor.get("maximo")
        return f"{_celda(minimo)} a {_celda(maximo)}" if maximo is not None else f"≥ {_celda(minimo)}"
    if isinstance(valor, float):
        return f"{_limpio(valor)}"
    return str(valor).replace("|", "/")


def tabla_markdown(filas: list) -> str:
    lineas = ["| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |",
              "|---|---|---|---|---|---|---|"]
    for f in filas:
        unidad = f" {f['unidad']}" if f.get("unidad") and f.get("valor_diseno") is not None else ""
        unidad_ex = f" {f['unidad']}" if f.get("unidad") and f.get("valor_exigido") is not None else ""
        lineas.append(f"| {_celda(f['elemento'])} | {_celda(f['progresiva'])} | {_celda(f['comprobacion'])} | "
                      f"{_celda(f['valor_diseno'])}{unidad} | {_celda(f['valor_exigido'])}{unidad_ex} | {f['resultado']} | "
                      f"{_celda(texto_referencia(f['referencia']))} |")
    return "\n".join(lineas)


def _proyecto(ctx: dict) -> dict:
    claves = ("velocidad", "clasificacion", "orografia", "orografia_tipo", "zona", "pavimentada", "peralte_maximo",
              "altitud_msnm", "cotas_msnm", "clase_declarada", "clase", "tipo_via", "demanda_veh_dia",
              "grupo_relacion_radios", "ubicacion_radios", "condicion_peralte", "aplica_101_05")
    return {k: _limpio(ctx.get(k)) if isinstance(ctx.get(k), float) else ctx.get(k) for k in claves}


# ---------------------------------------------------------------------------------------------- entrada de la herramienta
def preparar(norma: str, tipo_elemento: str, nombre: str, alineamiento: str | None, datos: dict,
             carpeta: str | None = None) -> dict:
    """Valida la llamada y los datos del proyecto, sin leer el dibujo. Devuelve {base, contexto, faltan, eje, perfil,
    tipo_elemento}; el llamante cierra la base (base.cerrar()). Lanza ErrorNorma con un mensaje claro."""
    if normalizar(norma).replace(" ", "") != normalizar(NORMA):
        raise ErrorNorma(f"Norma '{norma}' no soportada. Normas disponibles: {', '.join(NORMAS)}.")
    tipo = normalizar(tipo_elemento)
    if tipo not in TIPOS_ELEMENTO:
        raise ErrorNorma(f"tipo_elemento '{tipo_elemento}' no vale: usa 'alineamiento' (un eje) o 'rasante' (un perfil "
                         "de diseño, con su 'alineamiento').")
    if _vacio(nombre):
        raise ErrorNorma("Falta 'nombre': el eje (tipo_elemento=alineamiento) o el perfil (tipo_elemento=rasante). "
                         "Usa listar_alineamientos o listar_perfiles.")
    nombre = nombre.strip()
    if tipo == "alineamiento":
        if not _vacio(alineamiento) and alineamiento.strip() != nombre:
            raise ErrorNorma("Para un alineamiento, 'nombre' es el eje: no pases un 'alineamiento' distinto.")
        eje, perfil = nombre, None
    else:
        if _vacio(alineamiento):
            raise ErrorNorma("Para una rasante falta 'alineamiento': el eje al que pertenece el perfil. Usa "
                             "listar_alineamientos y listar_perfiles.")
        eje, perfil = alineamiento.strip(), nombre
    base = BaseNorma(localizar_base(carpeta))
    try:
        contexto, faltan = resolver_proyecto(base, tipo, datos)
    except Exception:
        base.cerrar()
        raise
    return {"base": base, "contexto": contexto, "faltan": faltan, "eje": eje, "perfil": perfil, "tipo_elemento": tipo}


def respuesta_faltan(preparada: dict) -> dict:
    faltan = preparada["faltan"]
    return {
        "ok": False,
        "verificado": False,
        "error": "Faltan datos del proyecto: " + ", ".join(f["dato"] for f in faltan) +
                 ". Pregúntalos al usuario con los valores válidos; no los supongas. No se leyó el dibujo.",
        "faltan": faltan,
    }


def verificar(preparada: dict, tabla_pis: dict, pvis: list | None = None, lecturas: dict | None = None) -> dict:
    """Respuesta completa de verificar_norma a partir de lo leído del dibujo."""
    base, ctx = preparada["base"], preparada["contexto"]
    cabecera = {
        "ok": True,
        "norma": NORMA,
        "tipo_elemento": preparada["tipo_elemento"],
        "nombre": preparada["perfil"] or preparada["eje"],
        "alineamiento": preparada["eje"],
    }
    motivo = unidades_no_validas(tabla_pis)
    if motivo:
        return {**cabecera, "verificado": False, "motivo": motivo, "lecturas": lecturas or {}}
    if preparada["tipo_elemento"] == "alineamiento":
        filas, no_comprobado = verificar_alineamiento(base, ctx, tabla_pis)
    else:
        filas, no_comprobado = verificar_rasante(base, ctx, pvis or [], tabla_pis)
    avisos = list(ctx["avisos"])
    avisos.extend(f"listar_pis: {a}" for a in (tabla_pis.get("avisos") or []))
    return {
        **cabecera,
        "verificado": True,
        "proyecto": _proyecto(ctx),
        "base": {"ruta": base.ruta, "metadatos": base.metadatos()},
        "resumen": resumir(filas),
        "filas": filas,
        "no_comprobado": no_comprobado,
        "avisos": avisos,
        "lecturas": lecturas or {},
        "tabla": tabla_markdown(filas),
    }

# -*- coding: utf-8 -*-
"""Base pequeña de ejemplo con el mismo esquema que normas/tablas/dg2018.sqlite de memoria-firma, para probar
verificar_norma sin la base real.

Los valores con el comentario «DG-2018» son de la norma pública del MTC (Manual de Carreteras: Diseño Geométrico
DG-2018) y los marcados «ejemplo» son inventados solo para probar la lógica: no los uses como datos de la norma.
"""
import os
import sqlite3

ESQUEMA = """
CREATE TABLE deflexion_sin_curva (ref TEXT, pagina_pdf INTEGER, orden INTEGER, velocidad_kmh INTEGER,
    deflexion_texto_pdf TEXT, grados INTEGER, minutos INTEGER, deflexion_max_grados REAL);
CREATE TABLE equivalencias_clasificacion (orden INTEGER, zona TEXT, clasificacion TEXT, orografia TEXT,
    orografia_tipo INTEGER, tipo_via TEXT, clase TEXT, demanda_veh_dia TEXT, grupo_relacion_radios INTEGER,
    ubicacion_radios TEXT, condicion_peralte TEXT, condicion_peralte_otra TEXT, preguntar TEXT, fuente TEXT);
CREATE TABLE formulas (id TEXT PRIMARY KEY, nombre TEXT, expresion TEXT, expresion_python TEXT, variables_json TEXT,
    seccion TEXT, pagina_pdf INTEGER, pagina_impresa INTEGER, nota TEXT);
CREATE TABLE friccion_maxima (ref TEXT, pagina_pdf INTEGER, orden INTEGER, velocidad_kmh INTEGER, velocidad_texto TEXT,
    f_max REAL);
CREATE TABLE indice_k (ref TEXT, pagina_pdf INTEGER, orden INTEGER, tipo_curva TEXT, control TEXT, velocidad_kmh INTEGER,
    distancia_visibilidad_m REAL, k REAL);
CREATE TABLE longitud_minima_curva (ref TEXT, pagina_pdf INTEGER, orden INTEGER, tipo_carretera TEXT, expresion_pdf TEXT,
    factor_v REAL);
CREATE TABLE longitud_tangente (ref TEXT, pagina_pdf INTEGER, orden INTEGER, velocidad_kmh INTEGER, l_min_s_m REAL,
    l_min_o_m REAL, l_max_m REAL);
CREATE TABLE metadatos (clave TEXT PRIMARY KEY, valor TEXT);
CREATE TABLE pendiente_maxima (ref TEXT, pagina_pdf INTEGER, orden INTEGER, tipo_via TEXT, clase TEXT,
    demanda_veh_dia TEXT, orografia_tipo INTEGER, velocidad_kmh INTEGER, pendiente_max_pct REAL);
CREATE TABLE peralte_maximo (ref TEXT, pagina_pdf INTEGER, orden INTEGER, condicion TEXT, absoluto_pct REAL,
    normal_pct REAL, absoluto_texto_pdf TEXT, normal_texto_pdf TEXT, figura TEXT);
CREATE TABLE radio_sin_transicion (ref TEXT, pagina_pdf INTEGER, orden INTEGER, velocidad_kmh INTEGER, radio_m REAL);
CREATE TABLE radios_minimos (ref TEXT, pagina_pdf INTEGER, orden INTEGER, ubicacion TEXT, velocidad_kmh INTEGER,
    peralte_max_pct REAL, f_max REAL, radio_calculado_m REAL, radio_redondeado_m REAL, texto_pdf TEXT);
CREATE TABLE referencias (id TEXT PRIMARY KEY, tipo TEXT, numero TEXT, titulo TEXT, seccion TEXT, titulo_seccion TEXT,
    paginas_pdf TEXT, paginas_impresas TEXT, tabla_sqlite TEXT, filas INTEGER, revisar INTEGER DEFAULT 0, imagen TEXT);
CREATE TABLE reglas (id TEXT PRIMARY KEY, seccion TEXT, pagina_pdf INTEGER, pagina_impresa INTEGER, elemento TEXT,
    aplica_a TEXT, condicion TEXT, operador TEXT, valor TEXT, valor_num REAL, unidad TEXT, tipo TEXT, verbo_pdf TEXT,
    regla TEXT, cita_pdf TEXT, referencia TEXT, nota TEXT, imagen TEXT);
CREATE TABLE relacion_radios (ref TEXT, pagina_pdf INTEGER, orden INTEGER, grupo INTEGER, lado_pdf TEXT,
    radio_entrada_m REAL, salida_max_texto TEXT, salida_max_operador TEXT, salida_max_m REAL, salida_min_m REAL);
CREATE TABLE tangente_min_mismo_sentido (ref TEXT, pagina_pdf INTEGER, orden INTEGER, velocidad_kmh INTEGER,
    longitud_min_m REAL);
CREATE TABLE velocidad_diseno (ref TEXT, pagina_pdf INTEGER, orden INTEGER, clasificacion TEXT, orografia TEXT,
    velocidad_kmh INTEGER);
CREATE VIEW velocidad_diseno_rango AS
    SELECT clasificacion, orografia, MIN(velocidad_kmh) AS v_min_kmh, MAX(velocidad_kmh) AS v_max_kmh,
           GROUP_CONCAT(velocidad_kmh, ',') AS velocidades, ref
    FROM velocidad_diseno GROUP BY clasificacion, orografia ORDER BY MIN(orden);
CREATE TABLE visibilidad_parada (ref TEXT, pagina_pdf INTEGER, orden INTEGER, velocidad_kmh INTEGER,
    dist_percepcion_reaccion_m REAL, dist_frenado_m REAL, dp_calculada_m REAL, dp_redondeada_m REAL);
"""

RURAL_PLANO = "Área rural (plano u ondulada)"
RURAL_ACCIDENTADO = "Área rural (accidentada o escarpada)"
URBANA = "Área urbana"
P_RURAL_POA = "Zona rural (T. Plano, Ondulado o Accidentado)"
P_RURAL_AE = "Zona rural (T. Accidentado o Escarpado)"
P_URBANA = "Atravesamiento de zonas urbanas"
TERCERA = "Carretera de tercera clase"
SEGUNDA = "Carretera de segunda clase"
AUTOPISTA = "Autopista de primera clase"


def crear_base(carpeta_memoria: str, sin_tablas: tuple = ()) -> str:
    """Crea <carpeta_memoria>/normas/tablas/dg2018.sqlite con la tabla pequeña de ejemplo. Devuelve su ruta."""
    ruta = os.path.join(carpeta_memoria, "normas", "tablas", "dg2018.sqlite")
    os.makedirs(os.path.dirname(ruta), exist_ok=True)
    if os.path.exists(ruta):
        os.remove(ruta)
    con = sqlite3.connect(ruta)
    con.executescript(ESQUEMA)
    ins = lambda tabla, filas: con.executemany(  # noqa: E731
        f"INSERT INTO {tabla} VALUES ({','.join('?' * len(filas[0]))})", filas)

    # T204.01 (DG-2018, subconjunto de celdas)
    vel = []
    for clas, oro, velocidades in ((TERCERA, "Ondulado", (40, 50, 60, 70, 80)), (TERCERA, "Accidentado", (30, 40, 50, 60)),
                                   (SEGUNDA, "Ondulado", (60, 70, 80)), (SEGUNDA, "Accidentado", (50, 60, 70)),
                                   (AUTOPISTA, "Plano", (100,))):
        for v in velocidades:
            vel.append(("T204.01", 98, len(vel) + 1, clas, oro, v))
    ins("velocidad_diseno", vel)

    # Equivalencias (patrón de la base real; el texto de 'preguntar' es de ejemplo)
    ins("equivalencias_clasificacion", [
        (1, "rural", TERCERA, "Ondulado", 2, "Carretera", "Tercera clase", "< 400", 2, RURAL_PLANO, P_RURAL_POA, None,
         "Pregunta si la vía está pavimentada", "ejemplo"),
        (2, "urbana", TERCERA, "Ondulado", 2, "Carretera", "Tercera clase", "< 400", 2, URBANA, P_URBANA, None,
         "Pregunta si la vía está pavimentada", "ejemplo"),
        (3, "rural", TERCERA, "Accidentado", 3, "Carretera", "Tercera clase", "< 400", 2, RURAL_ACCIDENTADO, P_RURAL_POA,
         P_RURAL_AE, "Pregunta el peralte (8 o 12) y si la vía está pavimentada", "ejemplo"),
        (4, "rural", SEGUNDA, "Ondulado", 2, "Carretera", "Segunda clase", "2.000-400", 2, RURAL_PLANO, P_RURAL_POA, None,
         None, "ejemplo"),
        (5, "rural", SEGUNDA, "Accidentado", 3, "Carretera", "Segunda clase", "2.000-400", 2, RURAL_ACCIDENTADO,
         P_RURAL_POA, P_RURAL_AE, "Pregunta el peralte (8 o 12)", "ejemplo"),
        (6, "urbana", SEGUNDA, "Accidentado", 3, "Carretera", "Segunda clase", "2.000-400", 2, URBANA, P_URBANA, None,
         None, "ejemplo"),
        (7, "urbana", SEGUNDA, "Ondulado", 2, "Carretera", "Segunda clase", "2.000-400", 2, URBANA, P_URBANA, None,
         None, "ejemplo"),
        (8, "rural", AUTOPISTA, "Plano", 1, "Autopista", "Primera clase", "> 6.000", 1, RURAL_PLANO, P_RURAL_POA, None,
         None, "ejemplo"),
    ])

    # T304.05 (DG-2018)
    ins("peralte_maximo", [
        ("T304.05", 197, 1, P_URBANA, 6.0, 4.0, "6,0 %", "4,0 %", None),
        ("T304.05", 197, 2, P_RURAL_POA, 8.0, 6.0, "8,0 %", "6,0 %", None),
        ("T304.05", 197, 3, P_RURAL_AE, 12.0, 8.0, "12,0 %", "8,0 %", None),
    ])

    # T302.02 y T302.04 (DG-2018; las filas de 50 km/h muestran el choque entre las dos tablas)
    ins("radios_minimos", [
        ("T302.02", 130, 1, URBANA, 60, 4.0, 0.15, 149.2, 150.0, None),
        ("T302.02", 130, 2, RURAL_PLANO, 40, 8.0, 0.17, 50.4, 50.0, None),
        ("T302.02", 130, 3, RURAL_PLANO, 50, 8.0, 0.16, 82.0, 85.0, None),
        ("T302.02", 130, 4, RURAL_PLANO, 60, 8.0, 0.15, 123.2, 125.0, None),
        ("T302.02", 130, 5, RURAL_PLANO, 80, 8.0, 0.14, 229.1, 230.0, None),
        ("T302.02", 130, 6, RURAL_PLANO, 100, 8.0, 0.12, 393.7, 395.0, None),
        ("T302.02", 130, 7, RURAL_ACCIDENTADO, 50, 12.0, 0.16, 70.3, 70.0, None),
        ("T302.02", 130, 8, RURAL_ACCIDENTADO, 60, 12.0, 0.15, 105.0, 105.0, None),
        ("T302.04", 133, 1, None, 40, 8.0, 0.17, 50.4, 50.0, None),
        ("T302.04", 133, 2, None, 50, 8.0, 0.16, 82.0, 80.0, None),
        ("T302.04", 133, 3, None, 60, 8.0, 0.15, 123.2, 125.0, None),
        ("T302.04", 133, 4, None, 60, 12.0, 0.15, 105.0, 105.0, None),
        ("T302.04", 133, 5, None, 60, 6.0, 0.15, 135.0, 135.0, None),
    ])

    # T302.01 (DG-2018: 1,39 V, 2,78 V y 16,70 V redondeados)
    ins("longitud_tangente", [
        ("T302.01", 128, 1, 40, 56.0, 111.0, 668.0),
        ("T302.01", 128, 2, 60, 83.0, 167.0, 1002.0),
        ("T302.01", 128, 3, 80, 111.0, 222.0, 1336.0),
        ("T302.01", 128, 4, 100, 139.0, 278.0, 1670.0),
    ])
    ins("tangente_min_mismo_sentido", [("T304.08", 199, 1, 60, 85.0)])  # DG-2018

    # Cuadro de deflexión máxima sin curva (DG-2018)
    ins("deflexion_sin_curva", [
        ("C302.02-DEFLEX", 127, 1, 30, "2º 30´", 2, 30, 2.5),
        ("C302.02-DEFLEX", 127, 2, 40, "2º 15´", 2, 15, 2.25),
        ("C302.02-DEFLEX", 127, 3, 50, "1º 50´", 1, 50, 1 + 50 / 60),
        ("C302.02-DEFLEX", 127, 4, 60, "1º 30´", 1, 30, 1.5),
        ("C302.02-DEFLEX", 127, 5, 70, "1º 20´", 1, 20, 1 + 20 / 60),
        ("C302.02-DEFLEX", 127, 6, 80, "1º 10´", 1, 10, 1 + 10 / 60),
    ])
    ins("longitud_minima_curva", [  # DG-2018
        ("C302.02-LMIN", 126, 1, "Autopistas", "6 V", 6.0),
        ("C302.02-LMIN", 126, 2, "Carreteras de dos carriles", "3 V", 3.0),
    ])
    ins("radio_sin_transicion", [
        ("T302.11B", 147, 1, 60, 210.0),   # DG-2018
        ("T302.11B", 147, 2, 50, 150.0),   # DG-2018
        ("T302.11A", 147, 1, 60, 250.0),   # ejemplo
        ("T302.11A", 147, 2, 100, 900.0),  # ejemplo
    ])

    # T302.08 grupo 2 (DG-2018, unas pocas filas) y T302.07 grupo 1 (ejemplo, con la fila repetida de la errata)
    ins("relacion_radios", [
        ("T302.08", 138, 1, 2, "izquierda", 40.0, "60", None, 60.0, 50.0),
        ("T302.08", 138, 2, 2, "izquierda", 50.0, "75", None, 75.0, 50.0),
        ("T302.08", 138, 3, 2, "izquierda", 60.0, "90", None, 90.0, 50.0),
        ("T302.08", 138, 4, 2, "izquierda", 70.0, "105", None, 105.0, 50.0),
        ("T302.08", 138, 5, 2, "izquierda", 80.0, "120", None, 120.0, 53.0),
        ("T302.08", 138, 6, 2, "izquierda", 90.0, "135", None, 135.0, 60.0),
        ("T302.08", 139, 7, 2, "derecha", 660.0, "> 670", ">", 670.0, 298.0),
        ("T302.08", 139, 8, 2, "derecha", 680.0, "> 670", ">", 670.0, 302.0),
        ("T302.08", 139, 9, 2, "derecha", 700.0, "> 670", ">", 670.0, 306.0),
        ("T302.07", 137, 1, 1, "izquierda", 860.0, "> 1720", ">", 1720.0, 500.0),
        ("T302.07", 137, 2, 1, "izquierda", 880.0, "> 1720", ">", 1720.0, 510.0),
        ("T302.07", 137, 3, 1, "izquierda", 880.0, "> 1720", ">", 1720.0, 517.0),
    ])

    # T303.02 y T303.03 (DG-2018, 60 km/h)
    ins("indice_k", [
        ("T303.02", 181, 1, "convexa", "parada", 60, 85.0, 11.0),
        ("T303.03", 183, 1, "concava", "parada", 60, 85.0, 18.0),
    ])
    # T303.01: Segunda clase accidentado 60 km/h es de la DG-2018; las demás celdas son de ejemplo
    ins("pendiente_maxima", [
        ("T303.01", 172, 1, "Carretera", "Segunda clase", "2.000-400", 3, 60, 8.0),
        ("T303.01", 172, 2, "Carretera", "Segunda clase", "2.000-400", 2, 60, 6.0),
        ("T303.01", 172, 3, "Carretera", "Tercera clase", "< 400", 2, 60, 8.0),
        ("T303.01", 172, 4, "Carretera", "Tercera clase", "< 400", 2, 40, 9.0),
    ])
    ins("visibilidad_parada", [  # T205.01 (DG-2018, pendiente 0 %)
        ("T205.01", 105, 1, 60, 41.7, 42.5, 84.2, 85.0),
        ("T205.01", 105, 2, 100, 69.4, 115.0, 184.4, 185.0),
    ])

    # Referencias: id, sección y páginas de la DG-2018 (títulos abreviados)
    refs = [
        ("T204.01", "tabla", "Rangos de la velocidad de diseño", "204.02", "98", "97"),
        ("T205.01", "tabla", "Distancia de visibilidad de parada", "205.02", "105", "104"),
        ("T302.01", "tabla", "Longitudes de tramos en tangente", "302.03", "128", "127"),
        ("T302.02", "tabla", "Radios mínimos y peraltes máximos", "302.04.02", "130", "129"),
        ("T302.04", "tabla", "Radios mínimos y peraltes máximos (Tercera Clase)", "302.04.03", "133", "132"),
        ("T302.07", "tabla", "Relación de radios (grupo 1)", "302.04.05", "136,137,138", "135,136,137"),
        ("T302.08", "tabla", "Relación de radios (grupo 2)", "302.04.05", "138,139", "137,138"),
        ("T302.11A", "tabla", "Radios que permiten prescindir de la transición", "302.05.07", "147", "146"),
        ("T302.11B", "tabla", "Radios que permiten prescindir de la transición (Tercera Clase)", "302.05.07", "147", "146"),
        ("T303.01", "tabla", "Pendientes máximas", "303.03.02", "172", "171"),
        ("T303.02", "tabla", "Índice K para curvas convexas", "303.04.03", "181", "180"),
        ("T303.03", "tabla", "Índice K para curvas cóncavas", "303.04.04", "183", "182"),
        ("T304.05", "tabla", "Peralte máximo", "304.06.01", "197", "196"),
        ("T304.08", "tabla", "Tangente mínima entre curvas del mismo sentido", "304.06.03", "199", "198"),
        ("C302.02-DEFLEX", "cuadro", "Deflexión máxima sin curva", "302.02", "127", "126"),
        ("C302.02-LMIN", "cuadro", "Longitud mínima de curva", "302.02", "126", "125"),
    ]
    ins("referencias", [(i, t, None, ti, s, None, pp, pi, None, None, 0, None) for i, t, ti, s, pp, pi in refs])

    # Reglas: id, sección, páginas, tipo y valor (texto de ejemplo, no citas de la norma)
    reglas = [
        ("R101.05-1", "101.05", 13, 12, "=", None, "obligatoria"),
        ("R302.02-1", "302.02", 126, 125, ">", None, "obligatoria"),
        ("R302.02-2", "302.02", 126, 125, "≥", 59, "obligatoria"),
        ("R302.02-3", "302.02", 126, 125, "≥", None, "obligatoria"),
        ("R302.02-4", "302.02", 127, 126, "≤", None, "permiso"),
        ("R302.02-7", "302.02", 127, 126, "≤", 800, "recomendación"),
        ("R302.02-8", "302.02", 127, 126, ">", 500, "obligatoria"),
        ("R302.04.05-1", "302.04.05", 135, 134, "≤", 200, "obligatoria"),
        ("R302.04.05-3", "302.04.05", 139, 138, "≥", 700, "obligatoria"),
        ("R302.06.02-1", "302.06.02", 148, 147, "≤", 1.5, "obligatoria"),
        ("R302.06.03-1", "302.06.03", 148, 147, "≥", 400, "recomendación"),
        ("R303.03.01-1", "303.03.01", 171, 170, "≥", 0.5, "recomendación"),
        ("R303.03.02-1", "303.03.02", 171, 170, "=", -1, "obligatoria"),
        ("R303.03.02-2", "303.03.02", 171, 170, "=", 2, "permiso"),
        ("R303.03.03-1", "303.03.03", 173, 172, "=", 1, "excepción"),
        ("R303.03.03-2", "303.03.03", 173, 172, "≥", 500, "obligatoria"),
        ("R303.03.03-3", "303.03.03", 173, 172, "≤", 180, "obligatoria"),
        ("R303.03.03-4", "303.03.03", 173, 172, "≤", 6, "obligatoria"),
        ("R303.03.03-5", "303.03.03", 173, 172, "≤", 8, "recomendación"),
        ("R303.04.01-1", "303.04.01", 175, 174, ">", 1, "obligatoria"),
        ("R303.04.01-2", "303.04.01", 175, 174, ">", 2, "obligatoria"),
    ]
    ins("reglas", [(i, s, pp, pi, None, None, None, op, None, num, None, tipo, None, "ejemplo", None, None, None, None)
                   for i, s, pp, pi, op, num, tipo in reglas])

    formulas = [
        ("F08", "Radio mínimo", "V**2/(127*(Pmax + fmax))", "302.04.02", 129, 128),
        ("F09", "Radio mínimo (Tercera Clase)", "V**2/(127*(0.01*emax + fmax))", "302.04.03", 132, 131),
        ("F26", "Convexa, parada, D < L", "A*Dp**2/(100*((2*h1)**0.5 + (2*h2)**0.5)**2)", "303.04.03", 179, 178),
        ("F27", "Convexa, parada, D > L", "2*Dp - 200*(h1**0.5 + h2**0.5)**2/A", "303.04.03", 179, 178),
        ("F30", "Cóncava, D < L", "A*D**2/(120 + 3.5*D)", "303.04.04", 181, 180),
        ("F31", "Cóncava, D > L", "2*D - (120 + 3.5*D)/A", "303.04.04", 181, 180),
        ("F34", "L mín.s", "1.39*V", "302.03", 128, 127),
        ("F35", "L mín.o", "2.78*V", "302.03", 128, 127),
        ("F36", "L máx", "16.70*V", "302.03", 128, 127),
        ("F37", "Longitud con deflexión pequeña", "30*(10 - Delta)", "302.02", 126, 125),
    ]
    ins("formulas", [(i, n, None, e, None, s, pp, pi, None) for i, n, e, s, pp, pi in formulas])

    ins("metadatos", [("documento", "DG-2018 (tabla pequeña de ejemplo para pruebas)"), ("script_version", "ejemplo")])
    for tabla in sin_tablas:
        con.execute(f"DROP TABLE {tabla}")
    con.commit()
    con.close()
    return ruta

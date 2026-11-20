# -*- coding: utf-8 -*-
"""crear_bordes_calzada con velocidad_diseno (1.21.0): el puente calcula el sobreancho de cada curva con la DG-2018
(302.09) y llama al plugin con esos ensanchamientos. Con la base de ejemplo (base_norma_ejemplo.py, esquema de la base
1.2 de la tarea 6.4: L de los vehículos de la T202.01 y algunas filas reales de la T302.20; el resto de EJEMPLO) y un
plugin falso con listar_pis y la herramienta.
"""
import json
import math

import pytest

import norma as n
import norma_sobreancho as ns
from base_norma_ejemplo import crear_base
from conftest import ejecutar

HERRAMIENTA = "crear_bordes_calzada"
PARAMETROS = [
    {"name": "eje", "type": "string", "description": "Eje", "required": True},
    {"name": "desplazamiento", "type": "number", "description": "Media calzada (> 0)", "required": True},
    {"name": "nombre_izquierdo", "type": "string", "description": "Nombre del borde izquierdo"},
    {"name": "nombre_derecho", "type": "string", "description": "Nombre del borde derecho"},
    {"name": "estilo", "type": "string", "description": "Estilo"},
    {"name": "ensanchamientos", "type": "json", "description": "Ensanchamientos"},
    {"name": "simular", "type": "boolean", "description": "Simular"},
    {"name": "pedido", "type": "string", "description": "Pedido original del usuario"},
]
PROYECTO = {"velocidad_diseno": 60, "numero_carriles": 2, "ancho_calzada": 7.2, "vehiculo_diseno": "B2"}
L_B2, L_VL = 10.55, 4.30   # T202.01 (DG-2018), l_frontal_eje_trasero_m
CON_L = ["VL", "B2", "B3-1", "B4-1"]   # los de la base de ejemplo con L (el articulado T3S3 no la tiene)


def curva(numero, giro, radio, pc, pt):
    return {"numero": numero, "tipo": "curva", "tiene_curva": True, "giro": giro, "radio": radio,
            "progresiva_pc": pc, "progresiva_pt": pt, "progresiva": (pc + pt) / 2}


# Ejes de prueba (progresivas genéricas): el Eje 1 es el de las validaciones (dos curvas de R = 80).
EJES = {
    "Eje 1": {"fin": 831.3274, "pis": [curva(1, "izquierda", 80, 220, 345.6637), curva(2, "derecha", 80, 485.6637, 611.3274)]},
    "Eje grande": {"fin": 1000, "pis": [curva(1, "derecha", 300, 300, 500)]},
    "Eje juntas": {"fin": 1000, "pis": [curva(1, "izquierda", 80, 200, 300), curva(2, "izquierda", 100, 350, 450)]},
    "Eje inicio": {"fin": 500, "pis": [curva(1, "izquierda", 80, 20, 120)]},
    # Vía de baja velocidad (30 y 40 km/h) con curvas cerradas de R = 25 y 30 m (requisito de la orquestadora: el XML de
    # criterios que usa hoy el dueño no funciona a 30 km/h).
    "Eje cerrado": {"fin": 600, "pis": [curva(1, "izquierda", 25, 200, 239.2699), curva(2, "derecha", 30, 400, 447.1239)]},
    # Casos de control de la 6.4 (caso 8, B2) a 60 km/h: R = 400 (fila real de la T302.20) y R = 700 (Sa < 0,40).
    "Eje control": {"fin": 3000, "pis": [curva(1, "izquierda", 400, 500, 900), curva(2, "derecha", 700, 1500, 2100)]},
    "Eje espiral": {"fin": 800, "pis": [{"numero": 1, "tipo": "AlignmentSCS", "tiene_curva": False, "giro": "izquierda",
                                         "aviso": "tipo no soportado todavía"}, curva(2, "derecha", 80, 400, 500)]},
}


class PluginFalso:
    def __init__(self, servidor):
        self.servidor = servidor
        self.unidades = "Meters"
        servidor.herramienta(HERRAMIENTA, PARAMETROS, "Crea los bordes de calzada de un eje.")
        servidor.respuestas[HERRAMIENTA] = self.crear
        servidor.respuestas["listar_pis"] = self.listar_pis

    def crear(self, cuerpo):
        a = cuerpo["args"]
        ens = json.loads(a["ensanchamientos"]) if a.get("ensanchamientos") else []
        simulado = bool(a.get("simular"))
        indice = len(self.servidor.llamadas) - 1
        despues = {"eje": a["eje"], "existe_izquierdo": True, "existe_derecho": True,
                   "izquierdo": {"nombre": a["eje"] + " - Borde izq", "desplazamiento_nominal": -a["desplazamiento"],
                                 "ensanchamientos": [e for e in ens if e["borde"] == "izquierdo"]},
                   "derecho": {"nombre": a["eje"] + " - Borde der", "desplazamiento_nominal": a["desplazamiento"],
                               "ensanchamientos": [e for e in ens if e["borde"] == "derecho"]}}
        resultado = {"simulado": simulado, "herramienta": HERRAMIENTA, "cambios": ["existe_izquierdo", "existe_derecho"],
                     "antes": {"existe_izquierdo": False, "existe_derecho": False}, "despues": despues}
        if not simulado:
            resultado["copia"] = {"ruta": "C:\\Proyectos\\prueba\\backups\\prueba.dwg"}
        return {"ok": True, "tool": HERRAMIENTA, "id_log": f"id-bordes-{indice}", "ms": 9, "result": resultado}

    def listar_pis(self, cuerpo):
        nombre = cuerpo["args"]["alineamiento"]
        eje = EJES.get(nombre)
        if eje is None:
            return {"ok": False, "error": f"No existe el alineamiento '{nombre}'. Usa listar_alineamientos.", "id_log": "id-no"}
        tabla = {"alineamiento": nombre, "unidades": self.unidades, "longitud": eje["fin"],
                 "inicio": {"progresiva": 0.0}, "fin": {"progresiva": eje["fin"]}, "numero_pi": len(eje["pis"]),
                 "pis": eje["pis"], "avisos": []}
        return {"ok": True, "tool": "listar_pis", "id_log": "id-listar-pis", "result": tabla}


@pytest.fixture
def memoria(tmp_path, monkeypatch):
    carpeta = tmp_path / "memoria-firma"
    crear_base(str(carpeta))
    monkeypatch.setenv("MEMORIA_FIRMA_DIR", str(carpeta))
    return carpeta


@pytest.fixture
def plugin(servidor, puente, memoria):
    p = PluginFalso(servidor)
    ejecutar(puente.sincronizar_herramientas())
    return p


def crear(puente, **args):
    texto = ejecutar(puente.mcp.call_tool(HERRAMIENTA, {"args": args})).content[0].text
    return json.loads(texto)


def llamadas(servidor):
    return [c["tool"] for c in servidor.llamadas]


def enviados(servidor):
    """Los ensanchamientos que el puente pasó al plugin en la última llamada a crear_bordes_calzada."""
    ultima = [c for c in servidor.llamadas if c["tool"] == HERRAMIENTA][-1]
    return ultima["args"], json.loads(ultima["args"]["ensanchamientos"])


def sa(carriles, radio, l, v):
    return carriles * (radio - math.sqrt(radio * radio - l * l)) + v / (10 * math.sqrt(radio))


# ---------------------------------------------------------------------------------------------- sin la norma
def test_sin_velocidad_todo_igual_que_antes(servidor, puente, plugin):
    args = {"eje": "Eje 1", "desplazamiento": 3.6, "simular": True, "pedido": "crea los bordes"}
    r = crear(puente, **args)
    assert servidor.llamadas == [{"tool": HERRAMIENTA, "args": args, "timeout_s": 120}]
    assert r["simulado"] is True and "propuesta_norma" not in r


def modelo(puente):
    herramienta = puente.mcp._tool_manager.get_tool(HERRAMIENTA)
    return herramienta, herramienta.parameters["$defs"][f"{HERRAMIENTA}_Model"]


def test_esquema_ampliado(servidor, puente, plugin):
    herramienta, m = modelo(puente)
    assert set(ns.CAMPOS_PROYECTO) <= set(m["properties"])
    assert m["required"] == ["eje"], "desplazamiento deja de ser obligatorio"
    assert "Con 'velocidad_diseno' no hace falta" in m["properties"]["desplazamiento"]["description"]
    assert "CON LA NORMA DG-2018" in herramienta.description and "302.09" in herramienta.description


def test_no_se_amplia_si_el_plugin_cambia_sus_parametros(servidor, puente):
    servidor.herramienta(HERRAMIENTA, [p for p in PARAMETROS if p["name"] != "ensanchamientos"], "Bordes")
    ejecutar(puente.sincronizar_herramientas())
    _, m = modelo(puente)
    assert "velocidad_diseno" not in m["properties"] and "desplazamiento" in m["required"]
    assert ns.campos_que_chocan([{"name": "vehiculo_diseno"}]) == ["vehiculo_diseno"]
    assert not ns.se_puede_ampliar(PARAMETROS + [{"name": "numero_carriles"}])


def test_las_tres_ampliaciones_con_la_forma_comun(servidor, puente, memoria):
    """AMPLIACIONES_NORMA tiene una sola forma (módulo, manejador) para el eje (1.19.0), el peralte (1.20.0) y los bordes
    (1.21.0); cada módulo publica CAMPOS y PARAMETROS_OPCIONALES."""
    import norma_alineamiento as ne
    import norma_peralte as np_
    esperado = {"crear_alineamiento_desde_polilinea": (ne, puente._crear_eje_con_norma, "radio"),
                "aplicar_peralte": (np_, puente._aplicar_peralte_con_norma, "curvas"),
                HERRAMIENTA: (ns, puente._crear_bordes_con_norma, "desplazamiento")}
    assert set(puente.AMPLIACIONES_NORMA) == set(esperado)
    for nombre, (modulo, manejador, opcional) in esperado.items():
        assert puente.AMPLIACIONES_NORMA[nombre] == (modulo, manejador)
        assert set(modulo.PARAMETROS_OPCIONALES) == {opcional} and modulo.CAMPOS


# ---------------------------------------------------------------------------------------------- cálculo
def test_formula_y_sus_terminos():
    f = ns.sobreancho_formula(2, 80, L_B2, 60)
    assert f["termino_geometrico"] == pytest.approx(2 * (80 - math.sqrt(6400 - L_B2 ** 2)))
    assert f["termino_velocidad"] == pytest.approx(60 / (10 * math.sqrt(80)))
    assert f["sobreancho"] == pytest.approx(sa(2, 80, L_B2, 60))
    with pytest.raises(ValueError, match="no es mayor que L"):
        ns.sobreancho_formula(2, 10, L_B2, 60)


def test_control_de_la_f46():
    """El control de la F46 de la base 1.2: n = 2, R = 60, L = 10,55, V = 40 → 2,386."""
    assert round(ns.sobreancho_formula(2, 60, L_B2, 40)["sobreancho"], 3) == 2.386


@pytest.mark.parametrize("velocidad, radio, esperado, se_aplica", [
    (30, 25, 5.27, True), (30, 30, 4.38, True), (40, 60, 2.39, True), (60, 400, 0.58, True), (60, 700, 0.39, False)])
def test_casos_de_control_de_la_6_4(velocidad, radio, esperado, se_aplica):
    """normas/casos_prueba, caso 8 (B2, n = 2): Sa de la F46 sin factor y si pasa del mínimo de 0,40 m."""
    f = ns.sobreancho_formula(2, radio, L_B2, velocidad)
    assert round(f["sobreancho"], 2) == esperado
    assert n.cumple(f["sobreancho"], ns.SOBREANCHO_MINIMO_M, n.MINIMO) is se_aplica


def test_el_termino_de_velocidad_es_el_de_la_norma():
    """V / (10 √R) de la DG-2018 (302.09), no 0,05·V/√R: a 30 km/h y R = 25 da 0,6 m, no 0,3."""
    f = ns.sobreancho_formula(1, 25, L_VL, 30)
    assert f["termino_velocidad"] == pytest.approx(0.6)
    assert f["termino_velocidad"] != pytest.approx(0.05 * 30 / math.sqrt(25))


def test_dos_curvas_en_el_borde_interior_sin_factor_por_defecto(servidor, puente, plugin):
    """P5, opción F (decidida por el dueño el 20/11/2026): sin reduccion_t30220 se aplica Sa entero, aunque la calzada
    sea de 7,20 m y el radio esté en la T302.20. Antes (opción A) se aplicaba el factor por defecto: cambio pedido."""
    r = crear(puente, eje="Eje 1", simular=True, pedido="bordes con sobreancho", **PROYECTO)
    assert llamadas(servidor) == ["listar_pis", HERRAMIENTA]
    args, ens = enviados(servidor)
    assert args["desplazamiento"] == pytest.approx(3.6)
    assert args["simular"] is True and args["pedido"] == "bordes con sobreancho"
    for campo in ns.CAMPOS_PROYECTO:
        assert campo not in args
    entero = round(sa(2, 80, L_B2, 60), 4)
    assert ens == [
        {"borde": "izquierdo", "progresiva_inicial": 220, "progresiva_final": 345.6637, "sobreancho": entero,
         "longitud_transicion": 40},
        {"borde": "derecho", "progresiva_inicial": 485.6637, "progresiva_final": 611.3274, "sobreancho": entero,
         "longitud_transicion": 40},
    ]
    p = r["propuesta_norma"]
    assert r["simulado"] is True and r["id_log"].startswith("id-bordes")
    assert p["seccion"] == "302.09" and p["formula"] == ns.FORMULA and p["formula_referencia"].startswith("F46")
    assert p["desplazamiento"] == 3.6 and p["desplazamiento_origen"] == "ancho_calzada / 2"
    assert p["longitud_transicion"] == 40 and p["longitud_transicion_origen"].startswith("por defecto")
    assert p["proyecto"]["l_m"] == L_B2 and p["proyecto"]["vehiculo_diseno"] == "B2"
    assert p["proyecto"]["vehiculo_nombre"] == "Ómnibus de dos ejes (B2)"
    assert p["proyecto"]["referencia_vehiculo"].startswith("T202.01")
    assert p["proyecto"]["reduccion_t30220"] is False
    c1 = p["curvas"][0]
    assert c1["borde"] == "izquierdo" and c1["se_aplica"] is True
    assert c1["sobreancho_sin_factor"] == c1["sobreancho"] == entero
    # El factor se informa (calzada de 7,20 m y V ≤ 80), pero no se aplica: la respuesta da Sa con factor para comparar.
    assert c1["factor_reduccion"]["valor"] == 0.5 and c1["factor_reduccion"]["referencia"].startswith("T302.20")
    assert c1["factor_reduccion"]["como"] == "fila de 80 m"
    assert c1["sobreancho_con_factor"] == round(sa(2, 80, L_B2, 60) * 0.5, 4)   # factor de EJEMPLO 0,5 para R = 80
    assert c1["reduccion_aplicada"] is False and c1["piso_0_40"] is False
    assert "Sa entero, sin factor: P5, opción F" in c1["manda"] and "× factor" not in c1["manda"]
    assert "Sin reducción (por defecto, P5 opción F)" in c1["nota_factor"]
    assert c1["ensanchamiento"]["pis"] == [1]
    assert p["curvas"][1]["borde"] == "derecho"
    pendientes = p["pendiente_de_decision"]
    assert [x["id"] for x in pendientes] == ["P4", "P5", "P6", "P7", "P8"]
    assert all(x["opcion_usada"] for x in pendientes)
    assert all(x["a_confirmar"] is True for x in pendientes if x["id"] != "P5")
    p5 = pendientes[1]
    assert p5["a_confirmar"] is False and p5["opcion"] == "F" and p5["decidida"] == "por el dueño el 20/11/2026"
    assert [(x["punto"], x["a_confirmar"]) for x in p5["puntos_a_confirmar"]] == [("a", True), ("b", True)]
    assert "opcional" in p5["puntos_a_confirmar"][0]["texto"] and "0,40 m" in p5["puntos_a_confirmar"][1]["texto"]
    assert p5["referencia"].startswith("T302.20") and pendientes[3]["referencia"].startswith("R302.09.03-2")
    assert "Sin redondear" in p["nota_redondeo"]
    assert p["lecturas"] == {"listar_pis": "id-listar-pis"}


def test_dos_curvas_con_la_reduccion_pedida(servidor, puente, plugin):
    """Con reduccion_t30220 = true, calzada de 7,20 m y V ≤ 80: Sa × factor de la T302.20 (fila exacta de 80 m)."""
    r = crear(puente, eje="Eje 1", simular=True, reduccion_t30220=True, **PROYECTO)
    args, ens = enviados(servidor)
    assert "reduccion_t30220" not in args
    reducido = round(sa(2, 80, L_B2, 60) * 0.5, 4)
    assert [e["sobreancho"] for e in ens] == [reducido, reducido]
    p = r["propuesta_norma"]
    assert p["proyecto"]["reduccion_t30220"] is True
    c1 = p["curvas"][0]
    assert c1["sobreancho_sin_factor"] == round(sa(2, 80, L_B2, 60), 4)
    assert c1["sobreancho_con_factor"] == c1["sobreancho"] == reducido
    assert c1["reduccion_aplicada"] is True and c1["piso_0_40"] is False and "nota_factor" not in c1
    assert "× factor 0.5 de la T302.20" in c1["manda"] and "(reduccion_t30220, P5 opción F)" in c1["manda"]
    assert not any("Pediste reduccion_t30220" in a for a in p["avisos"])


@pytest.mark.parametrize("reduccion", [False, True])
@pytest.mark.parametrize("velocidad", [30, 40])
def test_baja_velocidad_con_curvas_cerradas(servidor, puente, plugin, velocidad, reduccion):
    """30 y 40 km/h con R = 25 y 30 m (B2): la F46 con V / (10 √R), dentro del rango de la figura 302.18B (sin aviso
    de velocidad). Por defecto Sa entero (P5, opción F); con la reducción, R = 25 usa su fila (EJEMPLO 0,75) y R = 30,
    entre las filas de 25 y 80 m, el factor mayor de las dos (0,75), sin interpolar."""
    r = crear(puente, eje="Eje cerrado", simular=True, reduccion_t30220=reduccion,
              **{**PROYECTO, "velocidad_diseno": velocidad})
    _, ens = enviados(servidor)
    p = r["propuesta_norma"]
    assert not any("figura 302.18B" in a for a in p["avisos"])
    c25, c30 = p["curvas"]
    assert c25["termino_velocidad"] == round(velocidad / (10 * math.sqrt(25)), 4)
    assert c25["termino_geometrico"] == round(2 * (25 - math.sqrt(25 * 25 - L_B2 ** 2)), 4)
    assert c25["sobreancho_sin_factor"] == round(sa(2, 25, L_B2, velocidad), 4)
    assert c25["factor_reduccion"]["valor"] == 0.75 and c25["factor_reduccion"]["como"] == "fila de 25 m"
    assert c30["factor_reduccion"]["valor"] == 0.75 and c30["factor_reduccion"]["radio_tabla"] == 25
    assert c30["factor_reduccion"]["como"].startswith("entre las filas de 25 y 80 m: el factor mayor")
    for c, radio in ((c25, 25), (c30, 30)):
        assert c["sobreancho_con_factor"] == round(sa(2, radio, L_B2, velocidad) * 0.75, 4)
        assert c["reduccion_aplicada"] is reduccion
        assert c["sobreancho"] == round(sa(2, radio, L_B2, velocidad) * (0.75 if reduccion else 1), 4)
    if velocidad == 30:
        assert round(c25["sobreancho_sin_factor"], 2) == 5.27 and round(c30["sobreancho_sin_factor"], 2) == 4.38   # 6.4
    assert [(e["borde"], e["progresiva_inicial"], e["progresiva_final"], e["sobreancho"]) for e in ens] == [
        ("izquierdo", 200, 239.2699, c25["sobreancho"]), ("derecho", 400, 447.1239, c30["sobreancho"])]
    assert all(e["longitud_transicion"] == 40 for e in ens)


def test_control_a_60_kmh_con_r_400_y_700(servidor, puente, plugin):
    """Por defecto (P5, opción F): R = 400 da Sa = 0,58 entero y se aplica; el factor 0,07 de la T302.20 (fila real,
    posible errata) se informa: Sa × 0,07 = 0,04, que con el piso queda en 0,40. R = 700: Sa = 0,39 < 0,40, no se
    aplica y se avisa (P7). Antes (opción A) R = 400 se reducía por defecto y no se aplicaba: cambio pedido."""
    r = crear(puente, eje="Eje control", simular=True, **PROYECTO)
    _, ens = enviados(servidor)
    assert [(e["borde"], round(e["sobreancho"], 2)) for e in ens] == [("izquierdo", 0.58)]
    c400, c700 = r["propuesta_norma"]["curvas"]
    assert round(c400["sobreancho_sin_factor"], 2) == 0.58 and c400["sobreancho"] == round(sa(2, 400, L_B2, 60), 4)
    assert c400["factor_reduccion"]["valor"] == 0.07 and "Posible errata" in c400["factor_reduccion"]["nota"]
    assert c400["factor_reduccion"]["producto"] == round(sa(2, 400, L_B2, 60) * 0.07, 4)
    assert c400["sobreancho_con_factor"] == 0.4 and c400["se_aplica"] is True and c400["reduccion_aplicada"] is False
    assert round(c700["sobreancho"], 2) == 0.39 and c700["se_aplica"] is False
    # 700 m pasa de la última fila de la tabla de ejemplo (450 m, real): su factor, 0,08, con el aviso de posible errata
    assert c700["factor_reduccion"]["valor"] == 0.08 and "por encima de la última fila" in c700["factor_reduccion"]["como"]
    assert "Posible errata" in c700["factor_reduccion"]["nota"] and "nota_factor" not in c700
    assert "menor de 0,40 m" in c700["motivo"] and "a confirmar, P7" in c700["motivo"]
    assert any("PI 2: Sa = " in a and "menor de 0,40 m" in a for a in r["propuesta_norma"]["avisos"])


def test_control_a_60_kmh_con_la_reduccion_y_el_piso(servidor, puente, plugin):
    """Con reduccion_t30220: R = 400 da Sa × 0,07 = 0,04 y sube al piso de 0,40 m (Sa ≥ 0,40; punto b a confirmar).
    R = 700 sigue sin aplicarse: Sa = 0,39 < 0,40, el piso no lo sube (P7)."""
    r = crear(puente, eje="Eje control", simular=True, reduccion_t30220=True, **PROYECTO)
    _, ens = enviados(servidor)
    assert [(e["borde"], e["sobreancho"]) for e in ens] == [("izquierdo", 0.4)]
    c400, c700 = r["propuesta_norma"]["curvas"]
    assert c400["sobreancho"] == c400["sobreancho_con_factor"] == 0.4
    assert c400["reduccion_aplicada"] is True and c400["piso_0_40"] is True
    assert "con el piso de 0,40 m" in c400["manda"]
    assert c700["se_aplica"] is False and c700["reduccion_aplicada"] is False and c700["piso_0_40"] is False
    assert c700["sobreancho"] == round(sa(2, 700, L_B2, 60), 4) < 0.4


@pytest.mark.parametrize("cambio, motivo", [({"velocidad_diseno": 90}, "V = 90 km/h pasa de 80"),
                                            ({"ancho_calzada": 6.6}, "la calzada en tangente es de 6.6 m")])
def test_reduccion_pedida_que_no_vale_aplica_sa_entero_y_avisa(servidor, puente, plugin, cambio, motivo):
    """P5, opción F, punto 3: con la reducción pedida a más de 80 km/h o con una calzada distinta de 7,20 m no se reduce:
    se aplica Sa entero y se avisa por qué."""
    r = crear(puente, eje="Eje 1", simular=True, reduccion_t30220=True, **{**PROYECTO, **cambio})
    _, ens = enviados(servidor)
    v = cambio.get("velocidad_diseno", 60)
    assert [e["sobreancho"] for e in ens] == [round(sa(2, 80, L_B2, v), 4)] * 2
    p = r["propuesta_norma"]
    assert any(a.startswith("Pediste reduccion_t30220, pero " + motivo) and "se aplica el sobreancho entero" in a
               for a in p["avisos"])
    for c in p["curvas"]:
        assert c["factor_reduccion"] is None and c["sobreancho_con_factor"] is None and c["reduccion_aplicada"] is False
        assert c["sobreancho"] == c["sobreancho_sin_factor"] and c["nota_factor"]


# Filas de la T302.20 (DG-2018, pág. 162) que necesitan los valores de control del dueño (tarea 7.5); la tabla entera
# está en la base de memoria-firma y casos_norma_reales.py repite estos controles contra ella.
FILAS_CONTROL = [{"radio_m": r, "factor_reduccion": f, "ref": "T302.20"} for r, f in (
    (25, 0.86), (30, 0.83), (120, 0.54), (130, 0.52), (150, 0.47), (200, 0.38), (400, 0.07), (450, 0.08), (500, 0.05))]


def aplicado(velocidad, radio, reduccion, filas=FILAS_CONTROL):
    """El sobreancho que se aplica (0 si no se aplica) con B2, n = 2 y calzada de 7,20 m."""
    s = ns.sobreancho_formula(2, radio, L_B2, velocidad)["sobreancho"]
    factor = ns.factor_de_tabla(filas, radio)["valor"] if velocidad <= 80 else None
    return ns.decidir(s, factor, reduccion)["sobreancho"]


@pytest.mark.parametrize("velocidad, radio, sin_reduccion, con_reduccion", [
    (30, 25, 5.27, 4.53), (30, 30, 4.38, 3.64), (60, 125, 1.43, 0.77), (60, 150, 1.23, 0.58), (60, 200, 0.98, 0.40),
    (60, 400, 0.58, 0.40), (60, 500, 0.49, 0.40), (60, 600, 0.43, 0.40), (80, 600, 0.51, 0.40), (80, 700, 0.46, 0.40),
    (30, 500, 0, 0)])
def test_valores_de_control_de_la_opcion_f(velocidad, radio, sin_reduccion, con_reduccion):
    """Valores de control del dueño (P5, opción F; B2, n = 2, calzada de 7,20 m), en m."""
    assert round(aplicado(velocidad, radio, False), 2) == sin_reduccion
    assert round(aplicado(velocidad, radio, True), 2) == con_reduccion


def test_factor_de_la_tabla_sin_interpolar():
    """Fila exacta; entre dos filas, el factor mayor de las dos; por debajo de la primera, la primera; por encima de la
    última (500 m), la última; la fila de 450 m tal como está (0,08)."""
    def factor(radio):
        return ns.factor_de_tabla(FILAS_CONTROL, radio)["valor"]
    assert factor(25) == 0.86 and factor(30) == 0.83 and factor(27) == 0.86
    assert factor(125) == 0.54 and factor(130) == 0.52 and factor(175) == 0.47
    assert factor(20) == 0.86 and factor(500) == 0.05 and factor(600) == 0.05 and factor(5000) == 0.05
    assert factor(400) == 0.07 and factor(425) == 0.08 and factor(450) == 0.08 and factor(475) == 0.08
    assert ns.factor_de_tabla([], 100) is None
    assert ns.factor_de_tabla(FILAS_CONTROL, 125)["filas_usadas"] == [120, 130]


def test_piso_de_0_40_solo_si_sa_llega():
    assert ns.reducir(0.58, 0.07) == {"producto": pytest.approx(0.0406), "con_factor": 0.4, "piso": True}
    assert ns.reducir(2.0, 0.5) == {"producto": 1.0, "con_factor": 1.0, "piso": False}
    assert ns.reducir(0.39, 0.5)["con_factor"] == pytest.approx(0.195)   # Sa < 0,40: sin piso (y no se aplica, P7)
    assert ns.decidir(0.39, 0.5, True) == {"se_aplica": False, "reducido": ns.reducir(0.39, 0.5),
                                           "reduccion_aplicada": False, "sobreancho": 0.0}
    assert ns.decidir(0.58, None, True)["sobreancho"] == 0.58 and ns.decidir(0.58, 0.07, False)["sobreancho"] == 0.58


@pytest.mark.parametrize("reduccion", [False, True])
@pytest.mark.parametrize("velocidad", [30, 50, 60, 80])
def test_el_sobreancho_aplicado_nunca_sube_al_crecer_el_radio(velocidad, reduccion):
    """Barrido de radios de 11 a 1000 m cada 0,5 m (y los radios de la tabla): el sobreancho aplicado no sube."""
    radios = sorted({11 + 0.5 * i for i in range(1979)} | {f["radio_m"] for f in FILAS_CONTROL})
    valores = [aplicado(velocidad, r, reduccion) for r in radios]
    subidas = [(radios[i], valores[i], radios[i + 1], valores[i + 1]) for i in range(len(radios) - 1)
               if valores[i + 1] > valores[i] + 1e-9]
    assert subidas == []
    assert valores[-1] == 0.0 and valores[0] > 0.40


def test_resolver_lee_la_reduccion(memoria):
    with n.BaseNorma(n.localizar_base(str(memoria))) as base:
        sin, _ = ns.resolver(base, PROYECTO)
        con, _ = ns.resolver(base, {**PROYECTO, "reduccion_t30220": "sí"})
        _, faltan = ns.resolver(base, {**PROYECTO, "reduccion_t30220": "quizá"})
    assert sin["reduccion_t30220"] is False and con["reduccion_t30220"] is True
    assert [f["dato"] for f in faltan] == ["reduccion_t30220"] and faltan[0]["valores_validos"] == [True, False]


def test_control_a_60_kmh_sin_factor_con_otro_ancho(servidor, puente, plugin):
    """Con una calzada que no es de 7,20 m no hay factor (P5): R = 400 da 0,58 y se aplica."""
    r = crear(puente, eje="Eje control", simular=True, **{**PROYECTO, "ancho_calzada": 6.6})
    _, ens = enviados(servidor)
    assert [(e["borde"], round(e["sobreancho"], 2)) for e in ens] == [("izquierdo", 0.58)]
    assert "es para calzadas de 7,20 m" in r["propuesta_norma"]["curvas"][0]["nota_factor"]


def test_menos_de_40_cm_no_se_aplica_y_se_crean_los_bordes(servidor, puente, plugin):
    r = crear(puente, eje="Eje grande", **{**PROYECTO, "vehiculo_diseno": "VL", "velocidad_diseno": 30})
    _, ens = enviados(servidor)
    assert ens == []
    c = r["propuesta_norma"]["curvas"][0]
    assert c["se_aplica"] is False and c["sobreancho"] == round(sa(2, 300, L_VL, 30), 4)
    assert "menor de 0,40 m" in c["motivo"] and c["ensanchamiento"] is None
    # P5, opción F: 300 m está entre las filas de 150 y 400 m (EJEMPLO 0,25 y real 0,07): el mayor, sin interpolar. Antes
    # (opción A) solo valía el radio exacto y la nota decía «no está en la T302.20»: cambio pedido.
    assert c["factor_reduccion"]["valor"] == 0.25 and c["factor_reduccion"]["como"].startswith("entre las filas de 150 y 400")
    assert c["reduccion_aplicada"] is False and "nota_factor" not in c


def test_velocidad_mayor_de_80_sin_factor(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", simular=True, **{**PROYECTO, "velocidad_diseno": 90})
    _, ens = enviados(servidor)
    assert ens[0]["sobreancho"] == round(sa(2, 80, L_B2, 90), 4)
    p = r["propuesta_norma"]
    c = p["curvas"][0]
    assert c["factor_reduccion"] is None and "pasa de 80" in c["nota_factor"] and "R302.09.03-4" in c["nota_factor"]
    assert any("fuera del rango de la figura 302.18B" in a for a in p["avisos"])
    assert any("calculado para cada caso" in a for a in p["avisos"])


def test_otro_ancho_de_calzada_sin_factor_y_desplazamiento_del_usuario(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", simular=True, desplazamiento=3.5, **{**PROYECTO, "ancho_calzada": 6.6})
    args, ens = enviados(servidor)
    assert args["desplazamiento"] == 3.5
    assert ens[0]["sobreancho"] == round(sa(2, 80, L_B2, 60), 4)
    p = r["propuesta_norma"]
    assert p["desplazamiento_origen"] == "usuario"
    assert "no es la mitad de 'ancho_calzada'" in p["avisos"][0]
    assert "es para calzadas de 7,20 m" in p["curvas"][0]["nota_factor"]


def test_zonas_del_mismo_borde_que_se_tocan_se_unen(servidor, puente, plugin):
    r = crear(puente, eje="Eje juntas", simular=True, **PROYECTO)
    _, ens = enviados(servidor)
    assert len(ens) == 1
    e = ens[0]
    assert (e["borde"], e["progresiva_inicial"], e["progresiva_final"]) == ("izquierdo", 200, 450)
    # el mayor de los dos, ambos sin factor (P5, opción F; antes R = 80 se reducía con el factor de ejemplo 0,5): R = 80
    assert e["sobreancho"] == round(sa(2, 80, L_B2, 60), 4) > round(sa(2, 100, L_B2, 60), 4)
    assert any("se tocan: se unen" in a for a in r["propuesta_norma"]["avisos"])
    assert r["propuesta_norma"]["curvas"][1]["ensanchamiento"]["pis"] == [1, 2]


def test_transicion_recortada_al_inicio_del_eje_y_longitud_del_usuario(servidor, puente, plugin):
    r = crear(puente, eje="Eje inicio", simular=True, longitud_transicion=30, **PROYECTO)
    _, ens = enviados(servidor)
    assert ens[0]["longitud_transicion"] == 20
    p = r["propuesta_norma"]
    assert p["longitud_transicion"] == 30 and p["longitud_transicion_origen"] == "usuario"
    assert any("se recorta a 20 m" in a for a in p["avisos"])


def test_espiral_no_soportada_avisa(servidor, puente, plugin):
    r = crear(puente, eje="Eje espiral", simular=True, **PROYECTO)
    _, ens = enviados(servidor)
    assert [e["progresiva_inicial"] for e in ens] == [400]
    assert any("tipo 'AlignmentSCS' no soportado" in a for a in r["propuesta_norma"]["avisos"])


def test_vehiculo_por_su_nombre(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", simular=True, **{**PROYECTO, "vehiculo_diseno": "omnibus de dos ejes (b2)"})
    assert r["propuesta_norma"]["proyecto"]["vehiculo_diseno"] == "B2"


# ---------------------------------------------------------------------------------------------- sin llamar a Civil 3D
def test_faltan_datos_no_llama_al_plugin(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", velocidad_diseno=60, numero_carriles=0)
    assert servidor.llamadas == []
    assert r["ok"] is False and "No se llamó a Civil 3D" in r["error"]
    faltan = {f["dato"]: f for f in r["faltan"]}
    assert set(faltan) == {"numero_carriles", "ancho_calzada", "vehiculo_diseno"}
    assert faltan["vehiculo_diseno"]["valores_validos"] == CON_L
    assert "B2 (Ómnibus de dos ejes (B2), L = 10.55 m)" in faltan["vehiculo_diseno"]["motivo"]


def test_vehiculo_articulado_sin_l_no_se_inventa(servidor, puente, plugin):
    """P6: los articulados no tienen L en la T202.01; 'faltan' con los que sí la tienen, sin llamar a Civil 3D."""
    r = crear(puente, eje="Eje 1", **{**PROYECTO, "vehiculo_diseno": "t3s3"})
    assert servidor.llamadas == []
    f = r["faltan"][0]
    assert f["dato"] == "vehiculo_diseno" and "articulado" in f["motivo"] and "P6" in f["motivo"]
    assert f["valores_validos"] == CON_L


def test_vehiculo_que_no_esta_en_la_base(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", **{**PROYECTO, "vehiculo_diseno": "camión inventado"})
    assert servidor.llamadas == []
    assert r["faltan"][0]["dato"] == "vehiculo_diseno" and "no es un vehículo de la T202.01" in r["faltan"][0]["motivo"]


def test_longitud_de_transicion_no_valida(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", longitud_transicion=0, **PROYECTO)
    assert servidor.llamadas == []
    assert [f["dato"] for f in r["faltan"]] == ["longitud_transicion"]


def test_ensanchamientos_con_la_norma_es_error(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", ensanchamientos="[]", **PROYECTO)
    assert servidor.llamadas == []
    assert r["ok"] is False and "no pases 'ensanchamientos'" in r["error"]


@pytest.mark.parametrize("tabla", ["sobreancho_reduccion", "vehiculos_diseno"])
def test_base_sin_tablas_del_sobreancho(servidor, puente, plugin, memoria, tabla):
    crear_base(str(memoria), sin_tablas=(tabla,))
    r = crear(puente, eje="Eje 1", **PROYECTO)
    assert servidor.llamadas == []
    assert f"no tiene todavía las tablas del sobreancho: {tabla}" in r["error"]


def test_sin_memoria_firma(servidor, puente, plugin, monkeypatch):
    monkeypatch.delenv("MEMORIA_FIRMA_DIR")
    r = crear(puente, eje="Eje 1", **PROYECTO)
    assert servidor.llamadas == []
    assert r["ok"] is False and "MEMORIA_FIRMA_DIR" in r["error"]


# ---------------------------------------------------------------------------------------------- errores de Civil 3D
def test_dibujo_que_no_esta_en_metros(servidor, puente, plugin):
    plugin.unidades = "Feet"
    r = crear(puente, eje="Eje 1", **PROYECTO)
    assert llamadas(servidor) == ["listar_pis"]
    assert r["ok"] is False and "no en metros" in r["error"] and "No se llamó a crear_bordes_calzada" in r["error"]


def test_eje_que_no_existe(servidor, puente, plugin):
    r = crear(puente, eje="Eje 9", **PROYECTO)
    assert llamadas(servidor) == ["listar_pis"]
    assert r["ok"] is False and "No existe el alineamiento 'Eje 9'" in r["error"] and r["id_log"] == "id-no"


def test_error_del_plugin_lleva_la_propuesta(servidor, puente, plugin):
    servidor.respuestas[HERRAMIENTA] = {"ok": False, "error": "Ya existe el alineamiento 'Eje 1 - Borde izq'.", "id_log": "id-x"}
    r = crear(puente, eje="Eje 1", **PROYECTO)
    assert r["ok"] is False and "Ya existe" in r["error"] and r["id_log"] == "id-x"
    assert "propuesta_norma" in r


def test_resolver_sin_civil3d(memoria):
    with n.BaseNorma(n.localizar_base(str(memoria))) as base:
        ctx, faltan = ns.resolver(base, {"velocidad_diseno": "60", "numero_carriles": 2.0, "ancho_calzada": "7,2",
                                         "vehiculo_diseno": "b2"})
        citas = {i: base.referencia(i).get("tipo") for i in ns.REFERENCIAS_USADAS}
    assert faltan == []
    assert ctx["velocidad"] == 60 and ctx["numero_carriles"] == 2 and ctx["ancho_calzada"] == 7.2
    assert ctx["vehiculo"] == "B2" and ctx["l_m"] == L_B2 and ctx["longitud_transicion"] == 40
    assert all(citas.values()), citas   # todo lo que cita el módulo está en la base de ejemplo


# ---------------------------------------------------------------------------------------------- casos_norma_reales.py
def test_casos_norma_reales_comprueba_el_sobreancho(memoria):
    """El script de la base real compara la F46 del código con la de la base, el umbral 0,40 de la R302.09.03-2 y las
    referencias del sobreancho (con la base de ejemplo, todo coincide)."""
    import io
    import casos_norma_reales
    carpeta = memoria / "normas" / "casos_prueba"
    carpeta.mkdir(parents=True)
    (carpeta / "casos_dg2018.yaml").write_text("version: ejemplo\ncasos: []\n", encoding="utf-8")
    salida = io.StringIO()
    codigo = casos_norma_reales.main(["--memoria", str(memoria)], salida)
    texto = salida.getvalue()
    assert codigo == 0, texto
    assert "[OK   ] F46" in texto and "[OK   ] R302.09.03-2" in texto
    assert f"[OK   ] Referencias que cita el sobreancho ({len(ns.REFERENCIAS_USADAS)}) presentes en la base" in texto
    # Sin la F47 en la base, la comprobación de referencias del sobreancho falla
    import sqlite3
    con = sqlite3.connect(str(memoria / "normas" / "tablas" / "dg2018.sqlite"))
    con.execute("DELETE FROM formulas WHERE id = 'F47'")
    con.commit()
    con.close()
    salida = io.StringIO()
    assert casos_norma_reales.main(["--memoria", str(memoria)], salida) == 1
    assert "faltan F47" in salida.getvalue()

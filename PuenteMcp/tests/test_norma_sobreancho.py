# -*- coding: utf-8 -*-
"""crear_bordes_calzada con velocidad_diseno (1.21.0): el puente calcula el sobreancho de cada curva con la DG-2018
(302.09) y llama al plugin con esos ensanchamientos. Con la base de ejemplo (base_norma_ejemplo.py: vehículos y
factores de EJEMPLO, esquema provisional hasta el de la tarea 6.4) y un plugin falso con listar_pis y la herramienta.
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
PROYECTO = {"velocidad_diseno": 60, "numero_carriles": 2, "ancho_calzada": 7.2, "vehiculo_diseno": "ejemplo_largo"}


def curva(numero, giro, radio, pc, pt):
    return {"numero": numero, "tipo": "curva", "tiene_curva": True, "giro": giro, "radio": radio,
            "progresiva_pc": pc, "progresiva_pt": pt, "progresiva": (pc + pt) / 2}


# Ejes de prueba (progresivas genéricas): el Eje 1 es el de las validaciones (dos curvas de R = 80).
EJES = {
    "Eje 1": {"fin": 831.3274, "pis": [curva(1, "izquierda", 80, 220, 345.6637), curva(2, "derecha", 80, 485.6637, 611.3274)]},
    "Eje grande": {"fin": 1000, "pis": [curva(1, "derecha", 300, 300, 500)]},
    "Eje juntas": {"fin": 1000, "pis": [curva(1, "izquierda", 80, 200, 300), curva(2, "izquierda", 100, 350, 450)]},
    "Eje inicio": {"fin": 500, "pis": [curva(1, "izquierda", 80, 20, 120)]},
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


def test_la_ampliacion_del_eje_sigue_igual(servidor, puente, memoria):
    """La generalización de AMPLIACIONES_NORMA (1.21.0) no cambia crear_alineamiento_desde_polilinea."""
    import norma_alineamiento as ne
    modulo, opcional, _ = puente.AMPLIACIONES_NORMA["crear_alineamiento_desde_polilinea"]
    assert modulo is ne and opcional == "radio"
    assert puente.EJECUTAR_CON_NORMA["crear_alineamiento_desde_polilinea"] is puente._crear_eje_con_norma


# ---------------------------------------------------------------------------------------------- cálculo
def test_formula_y_sus_terminos():
    f = ns.sobreancho_formula(2, 80, 10, 60)
    assert f["termino_geometrico"] == pytest.approx(2 * (80 - math.sqrt(6300)))
    assert f["termino_velocidad"] == pytest.approx(60 / (10 * math.sqrt(80)))
    assert f["sobreancho"] == pytest.approx(sa(2, 80, 10, 60))
    with pytest.raises(ValueError, match="no es mayor que L"):
        ns.sobreancho_formula(2, 10, 10, 60)


def test_dos_curvas_en_el_borde_interior_con_factor(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", simular=True, pedido="bordes con sobreancho", **PROYECTO)
    assert llamadas(servidor) == ["listar_pis", HERRAMIENTA]
    args, ens = enviados(servidor)
    assert args["desplazamiento"] == pytest.approx(3.6)
    assert args["simular"] is True and args["pedido"] == "bordes con sobreancho"
    for campo in ns.CAMPOS_PROYECTO:
        assert campo not in args
    esperado = round(sa(2, 80, 10, 60) * 0.5, 4)   # factor de EJEMPLO 0,5 para R = 80 en 7,20 m
    assert ens == [
        {"borde": "izquierdo", "progresiva_inicial": 220, "progresiva_final": 345.6637, "sobreancho": esperado,
         "longitud_transicion": 40},
        {"borde": "derecho", "progresiva_inicial": 485.6637, "progresiva_final": 611.3274, "sobreancho": esperado,
         "longitud_transicion": 40},
    ]
    p = r["propuesta_norma"]
    assert r["simulado"] is True and r["id_log"].startswith("id-bordes")
    assert p["seccion"] == "302.09" and p["formula"] == ns.FORMULA
    assert p["desplazamiento"] == 3.6 and p["desplazamiento_origen"] == "ancho_calzada / 2"
    assert p["longitud_transicion"] == 40 and p["longitud_transicion_origen"].startswith("por defecto")
    assert p["proyecto"]["l_m"] == 10 and p["proyecto"]["vehiculo_diseno"] == "ejemplo_largo"
    c1 = p["curvas"][0]
    assert c1["borde"] == "izquierdo" and c1["se_aplica"] is True
    assert c1["sobreancho_formula"] == round(sa(2, 80, 10, 60), 4)
    assert c1["factor_reduccion"]["valor"] == 0.5 and c1["factor_reduccion"]["referencia"].startswith("T302.20")
    assert "× factor 0.5 de la T302.20" in c1["manda"]
    assert c1["ensanchamiento"]["pis"] == [1]
    assert p["curvas"][1]["borde"] == "derecho"
    assert [x["tema"] for x in p["pendiente_de_decision"]] == [ns.PENDIENTE_TRANSICION["tema"], ns.PENDIENTE_REDONDEO["tema"]]
    assert p["pendiente_de_decision"][0]["quien_decide"] == "Jose"
    assert p["lecturas"] == {"listar_pis": "id-listar-pis"}


def test_menos_de_40_cm_no_se_aplica_y_se_crean_los_bordes(servidor, puente, plugin):
    r = crear(puente, eje="Eje grande", **{**PROYECTO, "vehiculo_diseno": "ejemplo_corto", "velocidad_diseno": 30})
    _, ens = enviados(servidor)
    assert ens == []
    c = r["propuesta_norma"]["curvas"][0]
    assert c["se_aplica"] is False and c["sobreancho"] == round(sa(2, 300, 5, 30), 4)
    assert "menor de 0,40 m" in c["motivo"] and c["ensanchamiento"] is None
    assert "no está en la T302.20" in c["nota_factor"]


def test_velocidad_mayor_de_80_sin_factor(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", simular=True, **{**PROYECTO, "velocidad_diseno": 90})
    _, ens = enviados(servidor)
    assert ens[0]["sobreancho"] == round(sa(2, 80, 10, 90), 4)
    c = r["propuesta_norma"]["curvas"][0]
    assert c["factor_reduccion"] is None and "pasa de 80" in c["nota_factor"]
    assert any("fuera del rango de la figura 302.18B" in a for a in r["propuesta_norma"]["avisos"])


def test_otro_ancho_de_calzada_sin_factor_y_desplazamiento_del_usuario(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", simular=True, desplazamiento=3.5, **{**PROYECTO, "ancho_calzada": 6.6})
    args, ens = enviados(servidor)
    assert args["desplazamiento"] == 3.5
    assert ens[0]["sobreancho"] == round(sa(2, 80, 10, 60), 4)
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
    # el mayor de los dos: R = 80 con el factor de ejemplo 0,5 y R = 100 sin factor (no está en la tabla de ejemplo)
    assert e["sobreancho"] == round(sa(2, 100, 10, 60), 4) > round(sa(2, 80, 10, 60) * 0.5, 4)
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


# ---------------------------------------------------------------------------------------------- sin llamar a Civil 3D
def test_faltan_datos_no_llama_al_plugin(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", velocidad_diseno=60, numero_carriles=0)
    assert servidor.llamadas == []
    assert r["ok"] is False and "No se llamó a Civil 3D" in r["error"]
    faltan = {f["dato"]: f for f in r["faltan"]}
    assert set(faltan) == {"numero_carriles", "ancho_calzada", "vehiculo_diseno"}
    assert faltan["vehiculo_diseno"]["valores_validos"] == ["ejemplo_corto", "ejemplo_largo"]


def test_vehiculo_que_no_esta_en_la_base(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", **{**PROYECTO, "vehiculo_diseno": "camión inventado"})
    assert servidor.llamadas == []
    assert r["faltan"][0]["dato"] == "vehiculo_diseno" and "no es un vehículo de la base" in r["faltan"][0]["motivo"]


def test_longitud_de_transicion_no_valida(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", longitud_transicion=0, **PROYECTO)
    assert servidor.llamadas == []
    assert [f["dato"] for f in r["faltan"]] == ["longitud_transicion"]


def test_ensanchamientos_con_la_norma_es_error(servidor, puente, plugin):
    r = crear(puente, eje="Eje 1", ensanchamientos="[]", **PROYECTO)
    assert servidor.llamadas == []
    assert r["ok"] is False and "no pases 'ensanchamientos'" in r["error"]


def test_base_sin_tablas_del_sobreancho(servidor, puente, plugin, memoria):
    crear_base(str(memoria), sin_tablas=("sobreancho_reduccion",))
    r = crear(puente, eje="Eje 1", **PROYECTO)
    assert servidor.llamadas == []
    assert "no tiene todavía las tablas del sobreancho: sobreancho_reduccion" in r["error"]


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


def test_resolver_sin_base_de_civil3d(memoria):
    with n.BaseNorma(n.localizar_base(str(memoria))) as base:
        ctx, faltan = ns.resolver(base, {"velocidad_diseno": "60", "numero_carriles": 2.0, "ancho_calzada": "7,2",
                                         "vehiculo_diseno": "EJEMPLO_LARGO"})
    assert faltan == []
    assert ctx["velocidad"] == 60 and ctx["numero_carriles"] == 2 and ctx["ancho_calzada"] == 7.2
    assert ctx["vehiculo"] == "ejemplo_largo" and ctx["l_m"] == 10 and ctx["longitud_transicion"] == 40

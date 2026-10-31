# -*- coding: utf-8 -*-
"""Pruebas de exportar_dataset (1.6.0) con registros de ejemplo. Todas las rutas y nombres de proyecto son FALSOS
(C:\\Obras\\Carretera Ficticia\\prueba.dwg, "Proyecto Ejemplo") y deben desaparecer de la salida."""
import json
import os
import sys

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import exportar_dataset as ed  # noqa: E402

RUTA_FALSA = "C:\\Obras\\Carretera Ficticia\\prueba.dwg"
PROYECTO_FALSO = "Proyecto Ejemplo"


def llamada(id_, herramienta, pedido=None, args=None, ok=True, error=None, hora="2026-10-31 14:35:01", version="1.6.0"):
    args = dict(args or {})
    if pedido is not None:
        args["pedido"] = pedido
    return {"tipo": "llamada", "id": id_, "hora": hora, "herramienta": herramienta, "pedido": pedido,
            "args": args, "ok": ok, "ms": 3, "error": error, "version": version}


def aprobacion(id_, id_log, aprobado=True, comentario=None):
    return {"tipo": "aprobacion", "id": id_, "hora": "2026-10-31 14:40:00", "id_log": id_log, "aprobado": aprobado, "comentario": comentario}


def escribir_log(ruta, lineas):
    os.makedirs(os.path.dirname(ruta), exist_ok=True)
    with open(ruta, "w", encoding="utf-8") as f:
        for l in lineas:
            f.write(l if isinstance(l, str) else json.dumps(l, ensure_ascii=False))
            f.write("\n")


def leer_pares(ruta):
    with open(ruta, "r", encoding="utf-8") as f:
        return [json.loads(l) for l in f if l.strip()]


@pytest.fixture
def registros(tmp_path):
    """Dos dibujos de prueba en carpetas distintas, cada uno con su mcp_log.jsonl, más un archivo que no es un log."""
    log1 = tmp_path / "dibujos" / "obra1" / "mcp_log.jsonl"
    escribir_log(str(log1), [
        "",                                                        # línea vacía
        "esto no es JSON",                                         # línea rota
        {"hora": "2026-09-28 10:05:03", "herramienta": "antigua", "ok": True},   # anterior a 1.6.0: sin id, nunca aprobable
        llamada("id-1", "listar_superficies", "lista las superficies"),
        llamada("id-2", "listar_corredores", "lista las superficies"),           # mismo pedido, consecutiva → mismo par
        llamada("id-3", "asignar_objetivo", f"asigna el talud al terreno en {PROYECTO_FALSO}",
                {"corredor": "Corredor 1", "objetivo": "Terreno"}),
        llamada("id-4", "guardar_dibujo", f"guarda como {RUTA_FALSA}", {"como": RUTA_FALSA}),
        llamada("id-5", "listar_perfiles", "lista los perfiles", ok=False, error=f"No existe el alineamiento 'Eje {PROYECTO_FALSO}'. Usa listar_alineamientos."),
        llamada("id-6", "listar_alineamientos", "lista los perfiles"),
        llamada("id-7", "ping", "no aprobada"),
        llamada("id-8", "ping", "rechazada"),
        llamada("id-9", "ping", None),                              # sin pedido
        llamada("id-10", "ping", None),                             # sin pedido: no se agrupa con la anterior
        aprobacion("ap-1", "id-1", True, "correcto"),
        aprobacion("ap-2", "id-2", True),
        aprobacion("ap-3", "id-3", True, f"perfecto, era {PROYECTO_FALSO}"),
        aprobacion("ap-4", "id-4", True),
        aprobacion("ap-5", "id-5", True),
        aprobacion("ap-6", "id-6", True),
        aprobacion("ap-8", "id-8", False, "estaba mal"),
        aprobacion("ap-9", "id-9", True),
        aprobacion("ap-10", "id-10", True),
        aprobacion("ap-x", "id-no-existe", True),
    ])
    log2 = tmp_path / "dibujos" / "obra2" / "sub" / "mcp_log.jsonl"
    escribir_log(str(log2), [
        llamada("id-21", "listar_superficies", "lista las superficies", hora="2026-11-02 09:00:00", version="1.6.1"),
        aprobacion("ap-21", "id-21", True),
    ])
    escribir_log(str(tmp_path / "dibujos" / "otro.jsonl"), [llamada("id-99", "ping", "no es mcp_log"), aprobacion("ap-99", "id-99")])
    prohibidos = tmp_path / "PROHIBIDOS.txt"
    prohibidos.write_text(f"# nombres de obras reales\n\n{PROYECTO_FALSO}\nCarretera Ficticia\n", encoding="utf-8")
    return {"carpeta": str(tmp_path / "dibujos"), "log1": str(log1), "prohibidos": str(prohibidos), "tmp": tmp_path}


# ---------------------------------------------------------------------------------------------- exportación completa
def test_solo_entran_las_aprobadas_y_se_agrupan_por_pedido(registros):
    salida = registros["tmp"] / "salida" / "pares.jsonl"
    r = ed.exportar(registros["carpeta"], str(salida), registros["prohibidos"])
    assert r["archivos"] == 2 and r["aprobadas"] == 9 and r["pares"] == 7 and r["prohibidos"] == 2
    pares = leer_pares(str(salida))
    assert len(pares) == 7
    assert [p["pedido"] for p in pares] == [
        "lista las superficies", "asigna el talud al terreno en <proyecto>", "guarda como <ruta>",
        "lista los perfiles", "", "", "lista las superficies"]
    # el primer par agrupa las dos llamadas consecutivas con el mismo pedido, en orden
    assert [h["nombre"] for h in pares[0]["herramientas"]] == ["listar_superficies", "listar_corredores"]
    assert pares[0]["resultado"] == {"ok": True, "resumen": "2 llamadas correctas"}
    assert pares[0]["fecha"] == "2026-10-31" and pares[0]["version"] == "1.6.0"
    # 'pedido' no se repite dentro de args
    assert all("pedido" not in h["args"] for p in pares for h in p["herramientas"])
    # un par con un error conserva ok=false y un resumen corto
    assert pares[3]["resultado"]["ok"] is False
    assert pares[3]["resultado"]["resumen"].startswith("1 de 2 con error: No existe el alineamiento 'Eje <proyecto>'")
    # las llamadas sin pedido no se agrupan entre sí
    assert pares[4]["herramientas"][0]["nombre"] == "ping" and pares[5]["herramientas"][0]["nombre"] == "ping"
    # el segundo archivo (carpeta anidada) también entra, con su fecha y versión
    assert pares[6]["fecha"] == "2026-11-02" and pares[6]["version"] == "1.6.1"
    # ni la rechazada, ni la no aprobada, ni la antigua, ni la de otro.jsonl
    nombres = [h["nombre"] for p in pares for h in p["herramientas"]]
    assert "antigua" not in nombres and nombres.count("ping") == 2
    assert not any(p["pedido"] in ("no aprobada", "rechazada", "no es mcp_log") for p in pares)


def test_la_salida_no_contiene_rutas_ni_nombres_prohibidos(registros):
    salida = registros["tmp"] / "pares.jsonl"
    ed.exportar(registros["carpeta"], str(salida), registros["prohibidos"])
    texto = salida.read_text(encoding="utf-8")
    assert "Carretera Ficticia" not in texto and "Proyecto Ejemplo" not in texto
    assert "C:\\\\" not in texto and "prueba.dwg" not in texto and ".dwg" not in texto
    assert "<proyecto>" in texto and "<ruta>" in texto
    pares = leer_pares(str(salida))
    assert pares[2]["herramientas"][0]["args"] == {"como": "<ruta>"}


def test_un_solo_archivo_y_sin_prohibidos(registros):
    salida = registros["tmp"] / "uno.jsonl"
    r = ed.exportar(registros["log1"], str(salida), None)
    assert r["archivos"] == 1 and r["pares"] == 6 and r["prohibidos"] == 0
    texto = salida.read_text(encoding="utf-8")
    assert "prueba.dwg" not in texto and "Carretera Ficticia" not in texto, "la ruta completa desaparece aunque no haya lista"
    assert "Proyecto Ejemplo" in texto, "sin lista de prohibidos el nombre de proyecto no se conoce"


def test_sin_aprobaciones_el_dataset_queda_vacio(tmp_path):
    log = tmp_path / "mcp_log.jsonl"
    escribir_log(str(log), [llamada("id-1", "ping", "hola")])
    salida = tmp_path / "pares.jsonl"
    r = ed.exportar(str(log), str(salida))
    assert r == {"archivos": 1, "lineas": 1, "aprobadas": 0, "pares": 0, "prohibidos": 0, "salida": str(salida)}
    assert salida.read_text(encoding="utf-8") == ""


def test_la_ultima_aprobacion_manda(tmp_path):
    log = tmp_path / "mcp_log.jsonl"
    escribir_log(str(log), [llamada("id-1", "ping", "hola"), aprobacion("a", "id-1", True), aprobacion("b", "id-1", False),
                            llamada("id-2", "ping", "adiós"), aprobacion("c", "id-2", False), aprobacion("d", "id-2", True)])
    salida = tmp_path / "pares.jsonl"
    ed.exportar(str(log), str(salida))
    assert [p["pedido"] for p in leer_pares(str(salida))] == ["adiós"]


# ---------------------------------------------------------------------------------------------- anonimización
@pytest.mark.parametrize("texto, esperado", [
    ("guarda como C:\\Obras\\Carretera Ficticia\\prueba.dwg", "guarda como <ruta>"),
    ("abre C:/Obras/prueba.dwg y luego lista", "abre <ruta> y luego lista"),
    ("copia en \\\\servidor\\obras\\prueba.dwg ahora", "copia en <ruta> ahora"),
    ("el log está en /home/user/obras/mcp_log.jsonl", "el log está en <ruta>"),
    ("exporta a D:\\salida\\x.xml", "exporta a <ruta>"),
    ("abre prueba.dwg", "abre <dibujo>"),
    ("abre PRUEBA_V2.DWG ya", "abre <dibujo> ya"),
    ("dos: C:\\a\\b.dwg y C:\\c\\d.dwg", "dos: <ruta> y <ruta>"),
    ("la mitad es 1/2 y la fecha 31/10/2026", "la mitad es 1/2 y la fecha 31/10/2026"),
    ("lista las superficies", "lista las superficies"),
    ("", ""),
])
def test_anonimizar_rutas_y_dwg(texto, esperado):
    assert ed.anonimizar_texto(texto, []) == esperado


def test_anonimizar_prohibidos_sin_distinguir_mayusculas_y_los_largos_primero():
    lista = sorted({"Carretera Norte", "Carretera Norte Tramo 2", "Proyecto Ejemplo"}, key=len, reverse=True)
    assert ed.anonimizar_texto("obra CARRETERA NORTE TRAMO 2 de proyecto ejemplo", lista) == "obra <proyecto> de <proyecto>"
    assert ed.anonimizar("Carretera Norte", lista) == "<proyecto>"
    assert ed.anonimizar({"a": ["Proyecto Ejemplo", 3, None, {"b": "C:\\x\\y.dwg"}]}, lista) == {"a": ["<proyecto>", 3, None, {"b": "<ruta>"}]}


def test_leer_prohibidos(tmp_path):
    archivo = tmp_path / "PROHIBIDOS.txt"
    archivo.write_text("# comentario\n\nObra Corta\n  Obra Mucho Más Larga  \nObra Corta\n", encoding="utf-8")
    assert ed.leer_prohibidos(str(archivo)) == ["Obra Mucho Más Larga", "Obra Corta"]
    assert ed.leer_prohibidos(None) == [] and ed.leer_prohibidos(str(tmp_path / "no_existe.txt")) == []


# ---------------------------------------------------------------------------------------------- línea de comandos
def test_main_sin_variable_ni_salida_da_error_claro(registros, monkeypatch, capsys):
    monkeypatch.delenv(ed.VARIABLE_ENTORNO, raising=False)
    assert ed.main(["--logs", registros["carpeta"]]) == 2
    texto = capsys.readouterr().out
    assert texto.startswith("Error:") and "MEMORIA_FIRMA_DIR" in texto and "--salida" in texto


def test_main_usa_memoria_firma_por_defecto(registros, monkeypatch, capsys):
    memoria = registros["tmp"] / "memoria-firma"
    (memoria / "casos").mkdir(parents=True)
    (memoria / "casos" / "PROHIBIDOS.txt").write_text(f"{PROYECTO_FALSO}\n", encoding="utf-8")
    monkeypatch.setenv(ed.VARIABLE_ENTORNO, str(memoria))
    assert ed.main(["--logs", registros["carpeta"]]) == 0
    salida = memoria / "dataset" / "pares.jsonl"
    assert salida.is_file()
    texto = capsys.readouterr().out
    assert "9 llamadas aprobadas, 7 pares" in texto and str(salida) in texto and "sin lista de prohibidos" not in texto
    assert "Proyecto Ejemplo" not in salida.read_text(encoding="utf-8")


def test_main_con_salida_y_prohibidos_explicitos(registros, monkeypatch, capsys):
    monkeypatch.delenv(ed.VARIABLE_ENTORNO, raising=False)
    salida = registros["tmp"] / "x" / "pares.jsonl"
    assert ed.main(["--logs", registros["log1"], "--salida", str(salida), "--prohibidos", registros["prohibidos"]]) == 0
    assert "8 llamadas aprobadas, 6 pares" in capsys.readouterr().out
    assert ed.main(["--logs", registros["log1"], "--salida", str(salida), "--prohibidos", str(registros["tmp"] / "nada.txt")]) == 2
    assert ed.main(["--logs", str(registros["tmp"] / "no_existe"), "--salida", str(salida)]) == 2
    assert capsys.readouterr().out.count("Error:") == 2


def test_sin_lista_de_prohibidos_avisa(registros, monkeypatch, capsys):
    monkeypatch.delenv(ed.VARIABLE_ENTORNO, raising=False)
    assert ed.main(["--logs", registros["log1"], "--salida", str(registros["tmp"] / "p.jsonl")]) == 0
    assert "sin lista de prohibidos" in capsys.readouterr().out


def test_se_puede_ejecutar_como_modulo(registros, tmp_path):
    import subprocess
    salida = tmp_path / "m.jsonl"
    r = subprocess.run([sys.executable, "-m", "exportar_dataset", "--logs", registros["log1"], "--salida", str(salida)],
                       cwd=os.path.dirname(ed.__file__), capture_output=True, text=True, encoding="utf-8")
    assert r.returncode == 0, r.stdout + r.stderr
    assert "6 pares" in r.stdout and salida.is_file()

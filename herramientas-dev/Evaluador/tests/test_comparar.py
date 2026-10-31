"""Pruebas del comando `comparar`: validación, guardado con fecha y comparación con el informe anterior."""
from conftest import DATOS, correr


def informes_guardados(memoria):
    return sorted(p.name for p in (memoria / "evaluacion" / "informes").iterdir() if p.suffix == ".yaml")


def test_primer_informe_solo_se_guarda(memoria, capsys):
    codigo, out, err = correr(capsys, "comparar", str(DATOS / "informe_1.yaml"))
    assert codigo == 0, err
    assert informes_guardados(memoria) == ["informe_evaluacion_20261102_1000.yaml"]
    assert "No hay informe anterior" in out
    assert "Tiempo total del agente: 150 s" in out
    # Frente al manual: 2 + 5 + 3 = 10 min = 600 s; 150/600 = 25 %
    assert "150 s frente a 10 min (600 s)" in out and "25 % del tiempo manual" in out
    guardado = (memoria / "evaluacion" / "informes" / "informe_evaluacion_20261102_1000.yaml").read_text("utf-8")
    assert "version_plugin: 1.6.0" in guardado and "- id: 2" in guardado and "resultado: FALLO" in guardado


def test_segundo_informe_detecta_cambios_y_tiempos(memoria, capsys):
    correr(capsys, "comparar", str(DATOS / "informe_1.yaml"))
    codigo, out, err = correr(capsys, "comparar", str(DATOS / "informe_2.yaml"))
    assert codigo == 0, err
    assert informes_guardados(memoria) == ["informe_evaluacion_20261102_1000.yaml",
                                           "informe_evaluacion_20261109_1130.yaml"]
    assert "Informe anterior: 2026-11-02 10:00" in out
    assert "Pasaron de FALLO a OK: 1 (ids: 2)" in out
    assert "Pasaron de OK a FALLO: 1 (ids: 3)" in out
    assert "Siguen igual: 1 (1 siguen OK, 0 siguen FALLO)" in out
    # Tiempos por tarea y total: 150 s → 145 s
    assert "| 1 | OK | OK | sigue OK | 20 s | 15 s | -5 s | 2 |" in out
    assert "| 2 | FALLO | OK | FALLO → OK | 90 s | 60 s | -30 s | 5 |" in out
    assert "| 3 | OK | FALLO | OK → FALLO | 40 s | 70 s | +30 s | 3 | el corredor siguió desactualizado" in out
    assert "Tiempo total: antes 150 s, ahora 145 s (-5 s)" in out
    assert "145 s frente a 10 min (600 s)" in out


def test_compara_con_el_anterior_mas_reciente(memoria, capsys):
    # Se guardan dos informes y se vuelve a pasar el primero: debe compararse con el segundo (el más reciente)
    correr(capsys, "comparar", str(DATOS / "informe_1.yaml"))
    correr(capsys, "comparar", str(DATOS / "informe_2.yaml"))
    codigo, out, err = correr(capsys, "comparar", str(DATOS / "informe_1.yaml"))
    assert codigo == 0, err
    assert "Informe anterior: 2026-11-09 11:30" in out
    assert "Pasaron de OK a FALLO: 1 (ids: 2)" in out
    # El nombre ya existía: se añade _2 en vez de sobrescribir
    assert "informe_evaluacion_20261102_1000_2.yaml" in informes_guardados(memoria)


def test_acepta_bloque_yaml_dentro_de_markdown(memoria, tmp_path, capsys):
    md = tmp_path / "respuesta_agente.md"
    md.write_text("Informe de la corrida:\n\n```yaml\n" + (DATOS / "informe_1.yaml").read_text("utf-8")
                  + "```\n\nFin.\n", encoding="utf-8")
    codigo, out, err = correr(capsys, "comparar", str(md))
    assert codigo == 0, err
    assert informes_guardados(memoria) == ["informe_evaluacion_20261102_1000.yaml"]


def test_carpeta_explicita_sin_variable(sin_memoria, tmp_path, capsys):
    carpeta = tmp_path / "mis_informes"
    codigo, out, err = correr(capsys, "comparar", str(DATOS / "informe_1.yaml"), "--informes", str(carpeta),
                              "--tareas", str(DATOS / "tareas_ejemplo.yaml"))
    assert codigo == 0, err
    assert (carpeta / "informe_evaluacion_20261102_1000.yaml").is_file()
    assert "600 s" in out


def test_sin_banco_de_tareas_avisa_y_sigue(sin_memoria, tmp_path, capsys):
    codigo, out, err = correr(capsys, "comparar", str(DATOS / "informe_1.yaml"), "--informes", str(tmp_path))
    assert codigo == 0, err
    assert "no se compara con el tiempo manual" in out


def test_sin_variable_ni_carpeta_da_error_claro(sin_memoria, capsys):
    codigo, out, err = correr(capsys, "comparar", str(DATOS / "informe_1.yaml"))
    assert codigo == 2
    assert "MEMORIA_FIRMA_DIR" in err and "--informes" in err


def test_informe_inexistente_da_error_claro(memoria, capsys):
    codigo, out, err = correr(capsys, "comparar", str(memoria / "no_existe.yaml"))
    assert codigo == 2
    assert "No existe el informe" in err
    assert informes_guardados(memoria) == []


def escribir_informe_modificado(tmp_path, reemplazos):
    texto = (DATOS / "informe_1.yaml").read_text("utf-8")
    for viejo, nuevo in reemplazos:
        assert viejo in texto, viejo
        texto = texto.replace(viejo, nuevo)
    ruta = tmp_path / "informe_malo.yaml"
    ruta.write_text(texto, encoding="utf-8")
    return ruta


def test_rechaza_resultado_que_no_es_ok_ni_fallo(memoria, tmp_path, capsys):
    ruta = escribir_informe_modificado(tmp_path, [("resultado: OK\n    tiempo_s: 20", "resultado: BIEN\n    tiempo_s: 20")])
    codigo, out, err = correr(capsys, "comparar", str(ruta))
    assert codigo == 2
    assert "solo vale OK o FALLO" in err and "tarea 1" in err
    assert informes_guardados(memoria) == []


def test_rechaza_fallo_sin_explicacion(memoria, tmp_path, capsys):
    ruta = escribir_informe_modificado(tmp_path, [('fallo: "la herramienta no encontró el Corredor 1"', 'fallo: ""')])
    codigo, out, err = correr(capsys, "comparar", str(ruta))
    assert codigo == 2
    assert "es FALLO pero 'fallo' está vacío" in err


def test_rechaza_campo_que_falta(memoria, tmp_path, capsys):
    ruta = escribir_informe_modificado(tmp_path, [('version_puente: "1.6.0"\n', "")])
    codigo, out, err = correr(capsys, "comparar", str(ruta))
    assert codigo == 2
    assert "faltan los campos: version_puente" in err


def test_rechaza_fecha_mal_escrita(memoria, tmp_path, capsys):
    ruta = escribir_informe_modificado(tmp_path, [('fecha: "2026-11-02 10:00"', 'fecha: "2 de noviembre"')])
    codigo, out, err = correr(capsys, "comparar", str(ruta))
    assert codigo == 2
    assert "AAAA-MM-DD HH:MM" in err


def test_rechaza_yaml_roto(memoria, tmp_path, capsys):
    ruta = tmp_path / "roto.yaml"
    ruta.write_text("tareas: [\n", encoding="utf-8")
    codigo, out, err = correr(capsys, "comparar", str(ruta))
    assert codigo == 2
    assert "no es YAML válido" in err


def test_avisa_si_los_ids_no_coinciden_con_el_banco(memoria, tmp_path, capsys):
    ruta = escribir_informe_modificado(tmp_path, [("  - id: 3\n", "  - id: 7\n")])
    codigo, out, err = correr(capsys, "comparar", str(ruta))
    assert codigo == 0, err
    assert "no coinciden con los del banco" in out
    assert "Sin tiempo manual en el banco para las tareas: 7" in out


def test_respuesta_literal_se_recorta_al_guardar(memoria, tmp_path, capsys):
    larga = "x" * 2000
    ruta = escribir_informe_modificado(
        tmp_path, [("""respuesta_literal: '{"ok": true, "superficies": [{"nombre": "Terreno"}]}'""",
                    f"respuesta_literal: '{larga}'")])
    codigo, out, err = correr(capsys, "comparar", str(ruta))
    assert codigo == 0, err
    guardado = (memoria / "evaluacion" / "informes" / "informe_evaluacion_20261102_1000.yaml").read_text("utf-8")
    assert "x" * 600 in guardado and "x" * 601 not in guardado and "[…]" in guardado


def test_datos_de_prueba_no_llevan_tareas_reales():
    # Los ejemplos usan solo nombres genéricos (regla 7 de CLAUDE.md)
    texto = (DATOS / "tareas_ejemplo.yaml").read_text("utf-8")
    assert "Corredor 1" in texto and "Terreno" in texto and r"C:\Proyectos\prueba.dwg" in texto

"""Pruebas del comando `generar`: el prompt lleva las tareas, las reglas y el formato del informe."""
from conftest import DATOS, correr


def test_generar_escribe_prompt_con_tareas_y_formato(memoria, capsys):
    salida = memoria / "evaluacion" / "PROMPT_EVALUACION.md"
    codigo, out, err = correr(capsys, "generar")
    assert codigo == 0, err
    assert salida.is_file()
    texto = salida.read_text(encoding="utf-8")
    # Las tres tareas, con sus campos y el tiempo manual
    for frase in ("### Tarea 1: Lista las superficies del dibujo",
                  "### Tarea 2: Cambia la frecuencia del Corredor 1 a 10 m",
                  "### Tarea 3: Reconstruye el Corredor 1",
                  "**Archivo de partida**: resultado de la tarea 1",
                  "**Resultado esperado**: Aparece la superficie Terreno",
                  "**Cómo se comprueba**: listar_regiones muestra frecuencias 10",
                  "**Tiempo manual de referencia**: 5 min (300 s)",
                  "| **Total** | | | **10** |"):
        assert frase in texto, frase
    # Las reglas que pide la tarea
    for frase in ("Solo herramientas del MCP `civil3d`", "`8765`", "COPIA del dibujo de prueba",
                  "`simular=true`", "`como_se_comprueba`", "No arregles código"):
        assert frase in texto, frase
    # El formato fijo del informe, con una entrada por tarea
    assert "informe_evaluacion: 1" in texto
    for campo in ("fecha:", "version_plugin:", "version_puente:", "dibujo_de_partida:", "tareas:"):
        assert campo in texto, campo
    assert texto.count("  - id: ") == 3
    for campo in ("resultado: FALLO", "tiempo_s: 0", "herramientas: []", "respuesta_literal: \"\"", "fallo: \"\""):
        assert texto.count(campo) == 3, campo
    assert "Prompt de evaluación escrito en" in out and "3 tareas" in out


def test_generar_con_rutas_explicitas_sin_variable(sin_memoria, tmp_path, capsys):
    salida = tmp_path / "prompt.md"
    codigo, out, err = correr(capsys, "generar", "--tareas", str(DATOS / "tareas_ejemplo.yaml"),
                              "--salida", str(salida))
    assert codigo == 0, err
    assert "### Tarea 3" in salida.read_text(encoding="utf-8")


def test_generar_sin_variable_ni_rutas_da_error_claro(sin_memoria, capsys):
    codigo, out, err = correr(capsys, "generar")
    assert codigo == 2
    assert "MEMORIA_FIRMA_DIR" in err and "--tareas" in err


def test_generar_sin_archivo_de_tareas_da_error_claro(memoria, capsys):
    (memoria / "evaluacion" / "tareas.yaml").unlink()
    codigo, out, err = correr(capsys, "generar")
    assert codigo == 2
    assert "No existe el banco de tareas" in err


def test_generar_rechaza_tarea_sin_campos(memoria, capsys):
    (memoria / "evaluacion" / "tareas.yaml").write_text(
        "tareas:\n  - id: 1\n    pedido: algo\n", encoding="utf-8")
    codigo, out, err = correr(capsys, "generar")
    assert codigo == 2
    assert "no tiene los campos" in err and "resultado_esperado" in err

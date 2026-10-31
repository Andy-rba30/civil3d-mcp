"""Datos y ayudantes comunes de las pruebas del evaluador."""
import os
import shutil
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import evaluador  # noqa: E402

DATOS = Path(__file__).parent / "datos"


@pytest.fixture
def memoria(tmp_path, monkeypatch):
    """Una memoria-firma falsa en una carpeta temporal, apuntada por MEMORIA_FIRMA_DIR."""
    raiz = tmp_path / "memoria-firma"
    (raiz / "evaluacion" / "informes").mkdir(parents=True)
    shutil.copy(DATOS / "tareas_ejemplo.yaml", raiz / "evaluacion" / "tareas.yaml")
    monkeypatch.setenv("MEMORIA_FIRMA_DIR", str(raiz))
    return raiz


@pytest.fixture
def sin_memoria(monkeypatch):
    monkeypatch.delenv("MEMORIA_FIRMA_DIR", raising=False)


def correr(capsys, *argv):
    """Ejecuta evaluador.main con los argumentos dados y devuelve (código de salida, salida, errores)."""
    codigo = evaluador.main(list(argv))
    cap = capsys.readouterr()
    return codigo, cap.out, cap.err

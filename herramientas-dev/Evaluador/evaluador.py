#!/usr/bin/env python3
"""Evaluador automático (línea base) del agente sobre Civil 3D 2027 con el conector civil3d-mcp.

Dos comandos, pensados para correrse desde esta carpeta con `uv run --frozen python evaluador.py <comando>`:

  generar   lee el banco de tareas (evaluacion/tareas.yaml de memoria-firma) y escribe el prompt de evaluación en
            Markdown que el dueño pega al agente instalador de su PC (el que trabaja con el MCP civil3d).
  comparar  valida el informe YAML que devuelve ese agente, lo guarda con fecha en evaluacion/informes/ y lo compara
            con el informe anterior más reciente: qué tareas pasaron de FALLO a OK y al revés, cuáles siguen igual,
            cómo cambió el tiempo por tarea y el total, y el total frente al tiempo manual de la firma.

El programa no toca Civil 3D ni el MCP: solo lee y escribe archivos de texto. La ruta de memoria-firma sale de la
variable de entorno MEMORIA_FIRMA_DIR o de las opciones --tareas / --informes / --salida.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
from datetime import datetime
from pathlib import Path

import yaml

__version__ = "0.1.0"

FORMATO_INFORME = 1                      # número del formato del bloque YAML que devuelve el agente
RESULTADOS_VALIDOS = ("OK", "FALLO")
CAMPOS_TAREA_BANCO = ("id", "pedido", "archivo_de_partida", "resultado_esperado", "como_se_comprueba",
                      "tiempo_manual_min")
CAMPOS_INFORME = ("informe_evaluacion", "fecha", "version_plugin", "version_puente", "dibujo_de_partida", "tareas")
CAMPOS_TAREA_INFORME = ("id", "resultado", "tiempo_s", "herramientas", "respuesta_literal", "fallo")
MAX_RESPUESTA_LITERAL = 600              # caracteres que se conservan de respuesta_literal al guardar
PREFIJO_INFORME = "informe_evaluacion_"


class ErrorEvaluador(Exception):
    """Error que se muestra al usuario tal cual, sin traza."""


# ----------------------------------------------------------------------------------------------------------------
# Rutas
# ----------------------------------------------------------------------------------------------------------------

def carpeta_memoria(opcion: str | None, nombre_opcion: str, subruta: str) -> Path:
    """Devuelve la ruta pedida: la opción explícita o MEMORIA_FIRMA_DIR/subruta; si no hay ninguna, error claro."""
    if opcion:
        return Path(opcion)
    base = os.environ.get("MEMORIA_FIRMA_DIR", "").strip()
    if not base:
        raise ErrorEvaluador(
            f"No sé dónde está memoria-firma. Define la variable de entorno MEMORIA_FIRMA_DIR con la carpeta del "
            f"repositorio memoria-firma o pasa la opción {nombre_opcion} con la ruta explícita.")
    ruta = Path(base)
    if not ruta.is_dir():
        raise ErrorEvaluador(f"MEMORIA_FIRMA_DIR apunta a '{base}', que no es una carpeta.")
    return ruta / subruta


# ----------------------------------------------------------------------------------------------------------------
# Banco de tareas
# ----------------------------------------------------------------------------------------------------------------

def cargar_tareas(ruta: Path) -> dict:
    """Lee tareas.yaml y comprueba que cada tarea trae los seis campos. Devuelve el documento completo."""
    if not ruta.is_file():
        raise ErrorEvaluador(f"No existe el banco de tareas '{ruta}'. Revisa MEMORIA_FIRMA_DIR o la opción --tareas.")
    try:
        datos = yaml.safe_load(ruta.read_text(encoding="utf-8"))
    except yaml.YAMLError as e:
        raise ErrorEvaluador(f"El banco de tareas '{ruta}' no es YAML válido: {e}") from e
    if not isinstance(datos, dict) or not isinstance(datos.get("tareas"), list) or not datos["tareas"]:
        raise ErrorEvaluador(f"El banco de tareas '{ruta}' debe tener una lista 'tareas' con al menos una tarea.")
    ids = set()
    for n, tarea in enumerate(datos["tareas"], 1):
        if not isinstance(tarea, dict):
            raise ErrorEvaluador(f"La tarea número {n} de '{ruta}' no es un mapa de campos.")
        faltan = [c for c in CAMPOS_TAREA_BANCO if c not in tarea]
        if faltan:
            raise ErrorEvaluador(f"La tarea número {n} de '{ruta}' no tiene los campos: {', '.join(faltan)}.")
        if not isinstance(tarea["id"], int):
            raise ErrorEvaluador(f"La tarea número {n} de '{ruta}' tiene un id que no es un número entero.")
        if tarea["id"] in ids:
            raise ErrorEvaluador(f"El id {tarea['id']} está repetido en '{ruta}'.")
        ids.add(tarea["id"])
        if not isinstance(tarea["tiempo_manual_min"], (int, float)):
            raise ErrorEvaluador(f"La tarea {tarea['id']} de '{ruta}' tiene tiempo_manual_min que no es un número.")
    return datos


def intentar_cargar_tareas(opcion: str | None) -> dict | None:
    """Para `comparar`: el banco de tareas es opcional. Se usa si se puede encontrar; si no, se sigue sin él."""
    try:
        ruta = carpeta_memoria(opcion, "--tareas", "evaluacion/tareas.yaml")
    except ErrorEvaluador:
        return None
    if not ruta.is_file():
        return None
    return cargar_tareas(ruta)


# ----------------------------------------------------------------------------------------------------------------
# generar: el prompt de evaluación
# ----------------------------------------------------------------------------------------------------------------

def texto_plano(valor) -> str:
    """Convierte un campo a una línea de texto sin saltos (para las tablas y listas del prompt)."""
    return " ".join(str(valor).split())


def bloque_yaml_plantilla(tareas: list[dict]) -> str:
    """Plantilla del bloque YAML del informe con una entrada por tarea, lista para rellenar."""
    lineas = [
        "```yaml",
        f"informe_evaluacion: {FORMATO_INFORME}",
        'fecha: "AAAA-MM-DD HH:MM"',
        'version_plugin: "x.y.z"',
        'version_puente: "x.y.z"',
        'dibujo_de_partida: "C:\\\\Proyectos\\\\copia_del_dibujo_de_prueba.dwg"',
        "tareas:",
    ]
    for t in tareas:
        lineas += [
            f"  - id: {t['id']}",
            "    resultado: FALLO            # OK o FALLO",
            "    tiempo_s: 0                 # segundos desde que empezaste la tarea hasta que la comprobaste",
            "    herramientas: []            # herramientas del MCP usadas, en orden, por ejemplo [ping, listar_superficies]",
            '    respuesta_literal: ""       # lo que devolvió la última herramienta decisiva, recortado a 600 caracteres',
            '    fallo: ""                   # si FALLO: qué faltó o qué salió distinto; si OK: vacío',
        ]
    lineas.append("```")
    return "\n".join(lineas)


def generar_prompt(banco: dict, fecha_generacion: datetime | None = None) -> str:
    """Construye el prompt de evaluación en Markdown a partir del banco de tareas."""
    fecha_generacion = fecha_generacion or datetime.now()
    tareas = banco["tareas"]
    total_manual = sum(t["tiempo_manual_min"] for t in tareas)
    p: list[str] = []
    a = p.append

    a("# Prompt de evaluación del agente sobre Civil 3D (banco de tareas de la firma)")
    a("")
    a(f"Generado el {fecha_generacion.strftime('%Y-%m-%d %H:%M')} con `herramientas-dev/Evaluador` de civil3d-mcp "
      f"(evaluador {__version__}) a partir de `evaluacion/tareas.yaml` "
      f"(versión {banco.get('version', '?')}, fecha {banco.get('fecha', '?')}) de memoria-firma.")
    a("")
    a("Eres el agente instalador en la PC del dueño. Tu trabajo en esta sesión es **medir**, no arreglar: ejecutar "
      "cada tarea del banco con el MCP `civil3d`, comprobar el resultado y entregar un informe. El informe dice qué "
      "sabe hacer hoy el agente con el conector y cuánto tarda frente al trabajo manual.")
    a("")
    a("## Reglas (de obligado cumplimiento)")
    a("")
    a("1. **Solo herramientas del MCP `civil3d`.** Nunca `curl`, nunca scripts (PowerShell, Python u otros) y nunca "
      "el puerto `8765` directamente. Si algo no se puede hacer con las herramientas del MCP, la tarea es **FALLO** y "
      "lo anotas en `fallo` (por ejemplo: \"no existe herramienta para crear superficies\").")
    a("2. **Trabaja sobre una COPIA del dibujo de prueba.** Antes de empezar, el dueño abre en Civil 3D una copia "
      "del dibujo de partida (nunca el original). Si ves que el dibujo abierto no es una copia, detente y avísalo.")
    a("3. **Primero `simular=true`, después de verdad.** Para cada herramienta de escritura, llama primero con "
      "`simular=true`, lee `antes`/`despues`, y solo entonces repite la llamada sin simular. Las lecturas no se simulan.")
    a("4. **Comprueba cada tarea como dice `como_se_comprueba`** y compara con `resultado_esperado`. Si no coincide, "
      "es FALLO aunque la herramienta haya respondido `ok: true`. No des por bueno lo que no hayas leído del dibujo.")
    a("5. **No arregles código.** No edites el plugin ni el puente, no reinstales ni cambies versiones. Si algo falla, "
      "se anota en el informe y se corrige en la nube.")
    a("6. **Mide el tiempo de cada tarea** en segundos: desde que empiezas la tarea hasta que terminas de comprobarla. "
      "Si una tarea es FALLO, anota el tiempo que invertiste hasta decidirlo.")
    a("7. **Orden y encadenado.** Las tareas se hacen en el orden listado. Cuando `archivo_de_partida` dice "
      "\"resultado de la tarea N\", continúas sobre el dibujo que dejó esa tarea; si esa tarea fue FALLO, la "
      "dependiente también es FALLO con `fallo: \"depende de la tarea N, que falló\"`, salvo que el dueño te dé a "
      "mano el estado de partida.")
    a("8. **El token del plugin nunca aparece en el informe**: si saliera en una respuesta, escribe `<oculto>`.")
    a("9. **Sin datos de clientes en el informe.** Las rutas y nombres que anotes son los del dibujo de prueba.")
    a("")
    a("## Antes de empezar")
    a("")
    a("1. Llama a `ping` y anota `version` (es `version_plugin`) y la ruta de `dibujo` (es `dibujo_de_partida`).")
    a("2. Anota la versión del puente que muestra la conexión MCP (`version_puente`).")
    a("3. Llama a `listar_superficies`, `listar_alineamientos` y `listar_corredores` para conocer el estado inicial.")
    a(f"4. Dibujo base previsto por el banco: {texto_plano(banco.get('dibujo_base', 'no indicado'))}.")
    a("")
    a("## Tareas")
    a("")
    a("Para cada tarea: lee `pedido` como si te lo dijera el dueño, decide las herramientas, aplica la regla 3 y "
      "comprueba según `como_se_comprueba`. Al terminar cada tarea, anota su entrada del informe antes de pasar a la "
      "siguiente (si la sesión se corta, el informe parcial sigue sirviendo).")
    a("")
    for t in tareas:
        a(f"### Tarea {t['id']}: {texto_plano(t['pedido'])}")
        a("")
        a(f"- **Pedido**: {texto_plano(t['pedido'])}")
        a(f"- **Archivo de partida**: {texto_plano(t['archivo_de_partida'])}")
        a(f"- **Resultado esperado**: {texto_plano(t['resultado_esperado'])}")
        a(f"- **Cómo se comprueba**: {texto_plano(t['como_se_comprueba'])}")
        a(f"- **Tiempo manual de referencia**: {t['tiempo_manual_min']} min "
          f"({int(round(t['tiempo_manual_min'] * 60))} s). Tu `tiempo_s` se compara con este valor.")
        a("")
    a("## Resumen del banco")
    a("")
    a("| id | Pedido | Archivo de partida | Tiempo manual (min) |")
    a("|---|---|---|---|")
    for t in tareas:
        a(f"| {t['id']} | {texto_plano(t['pedido'])} | {texto_plano(t['archivo_de_partida'])} | "
          f"{t['tiempo_manual_min']} |")
    a(f"| **Total** | | | **{total_manual}** |")
    a("")
    a("## Informe final (formato fijo)")
    a("")
    a("Termina tu respuesta con **un único bloque YAML** exactamente con estos campos y en este orden. El dueño lo "
      "guardará en un archivo y lo pasará a `evaluador.py comparar`, que lo valida; si falta un campo o `resultado` no "
      "es `OK` ni `FALLO`, el comando lo rechaza y habrá que corregirlo a mano.")
    a("")
    a("Significado de los campos:")
    a("")
    a(f"- `informe_evaluacion`: número del formato; siempre `{FORMATO_INFORME}`.")
    a("- `fecha`: día y hora de la corrida, `AAAA-MM-DD HH:MM`.")
    a("- `version_plugin` y `version_puente`: las que anotaste antes de empezar.")
    a("- `dibujo_de_partida`: ruta de la copia del dibujo con la que empezaste.")
    a("- `tareas`: una entrada por tarea del banco, con el mismo `id`, en el mismo orden, y ninguna de más:")
    a("  - `resultado`: `OK` si la comprobación coincide con `resultado_esperado`; si no, `FALLO`.")
    a("  - `tiempo_s`: segundos de la tarea (número entero).")
    a("  - `herramientas`: lista con las herramientas del MCP que usaste, en orden y con repeticiones "
      "(las llamadas con `simular=true` se escriben como `nombre(simular)`).")
    a("  - `respuesta_literal`: la respuesta de la herramienta decisiva (la que sirvió para comprobar), recortada a "
      f"{MAX_RESPUESTA_LITERAL} caracteres, con el token sustituido por `<oculto>`.")
    a("  - `fallo`: texto vacío si OK; si FALLO, qué faltó o qué salió distinto, en una o dos frases.")
    a("")
    a(bloque_yaml_plantilla(tareas))
    a("")
    a("Después del bloque YAML no escribas nada más.")
    a("")
    return "\n".join(p)


def comando_generar(args: argparse.Namespace) -> int:
    ruta_tareas = carpeta_memoria(args.tareas, "--tareas", "evaluacion/tareas.yaml")
    banco = cargar_tareas(ruta_tareas)
    prompt = generar_prompt(banco)
    if args.salida:
        salida = Path(args.salida)
    elif os.environ.get("MEMORIA_FIRMA_DIR", "").strip():
        salida = carpeta_memoria(None, "--salida", "evaluacion/PROMPT_EVALUACION.md")
    else:
        sys.stdout.write(prompt)
        return 0
    salida.parent.mkdir(parents=True, exist_ok=True)
    salida.write_text(prompt, encoding="utf-8", newline="\n")
    print(f"Prompt de evaluación escrito en {salida} ({len(banco['tareas'])} tareas, "
          f"{sum(t['tiempo_manual_min'] for t in banco['tareas'])} min manuales en total).")
    return 0


# ----------------------------------------------------------------------------------------------------------------
# comparar: validar, guardar y comparar informes
# ----------------------------------------------------------------------------------------------------------------

def extraer_yaml(texto: str) -> str:
    """Si el texto trae un bloque ```yaml ... ``` (por ejemplo, la respuesta completa del agente pegada en un .md),
    devuelve solo el contenido del primer bloque; si no, devuelve el texto tal cual."""
    m = re.search(r"```ya?ml\s*\n(.*?)```", texto, re.DOTALL | re.IGNORECASE)
    return m.group(1) if m else texto


def validar_informe(datos, origen: str) -> dict:
    """Comprueba el formato fijo del informe. Devuelve el informe normalizado o lanza ErrorEvaluador."""
    if not isinstance(datos, dict):
        raise ErrorEvaluador(f"El informe '{origen}' no es un mapa YAML con los campos del formato.")
    faltan = [c for c in CAMPOS_INFORME if c not in datos]
    if faltan:
        raise ErrorEvaluador(f"Al informe '{origen}' le faltan los campos: {', '.join(faltan)}.")
    if datos["informe_evaluacion"] != FORMATO_INFORME:
        raise ErrorEvaluador(f"El informe '{origen}' tiene informe_evaluacion={datos['informe_evaluacion']!r}; "
                             f"este evaluador entiende el formato {FORMATO_INFORME}.")
    fecha = interpretar_fecha(datos["fecha"], origen)
    for campo in ("version_plugin", "version_puente", "dibujo_de_partida"):
        if not str(datos[campo]).strip():
            raise ErrorEvaluador(f"El campo '{campo}' del informe '{origen}' está vacío.")
    tareas = datos["tareas"]
    if not isinstance(tareas, list) or not tareas:
        raise ErrorEvaluador(f"El informe '{origen}' debe tener una lista 'tareas' con al menos una entrada.")
    normalizadas = []
    ids = set()
    for n, t in enumerate(tareas, 1):
        if not isinstance(t, dict):
            raise ErrorEvaluador(f"La entrada número {n} de 'tareas' en '{origen}' no es un mapa de campos.")
        faltan = [c for c in CAMPOS_TAREA_INFORME if c not in t]
        if faltan:
            raise ErrorEvaluador(f"A la entrada número {n} de 'tareas' en '{origen}' le faltan: {', '.join(faltan)}.")
        if not isinstance(t["id"], int):
            raise ErrorEvaluador(f"La entrada número {n} de 'tareas' en '{origen}' tiene un id que no es entero.")
        if t["id"] in ids:
            raise ErrorEvaluador(f"El id {t['id']} está repetido en las tareas del informe '{origen}'.")
        ids.add(t["id"])
        resultado = str(t["resultado"]).strip().upper()
        if resultado not in RESULTADOS_VALIDOS:
            raise ErrorEvaluador(f"La tarea {t['id']} del informe '{origen}' tiene resultado={t['resultado']!r}; "
                                 f"solo vale OK o FALLO.")
        if isinstance(t["tiempo_s"], bool) or not isinstance(t["tiempo_s"], (int, float)) or t["tiempo_s"] < 0:
            raise ErrorEvaluador(f"La tarea {t['id']} del informe '{origen}' tiene tiempo_s={t['tiempo_s']!r}; "
                                 f"debe ser un número de segundos mayor o igual que cero.")
        herramientas = t["herramientas"] if t["herramientas"] is not None else []
        if isinstance(herramientas, str):
            herramientas = [h.strip() for h in herramientas.split(",") if h.strip()]
        if not isinstance(herramientas, list) or not all(isinstance(h, str) for h in herramientas):
            raise ErrorEvaluador(f"La tarea {t['id']} del informe '{origen}' tiene 'herramientas' que no es una "
                                 f"lista de nombres.")
        fallo = "" if t["fallo"] is None else str(t["fallo"]).strip()
        if resultado == "FALLO" and not fallo:
            raise ErrorEvaluador(f"La tarea {t['id']} del informe '{origen}' es FALLO pero 'fallo' está vacío: "
                                 f"escribe qué faltó o qué salió distinto.")
        respuesta = "" if t["respuesta_literal"] is None else str(t["respuesta_literal"])
        if len(respuesta) > MAX_RESPUESTA_LITERAL:
            respuesta = respuesta[:MAX_RESPUESTA_LITERAL] + " […]"
        normalizadas.append({
            "id": t["id"],
            "resultado": resultado,
            "tiempo_s": t["tiempo_s"],
            "herramientas": herramientas,
            "respuesta_literal": respuesta,
            "fallo": fallo,
        })
    return {
        "informe_evaluacion": FORMATO_INFORME,
        "fecha": fecha.strftime("%Y-%m-%d %H:%M"),
        "version_plugin": str(datos["version_plugin"]).strip(),
        "version_puente": str(datos["version_puente"]).strip(),
        "dibujo_de_partida": str(datos["dibujo_de_partida"]).strip(),
        "tareas": normalizadas,
    }


def interpretar_fecha(valor, origen: str) -> datetime:
    """Acepta 'AAAA-MM-DD HH:MM', 'AAAA-MM-DDTHH:MM', 'AAAA-MM-DD' o los tipos fecha que YAML ya convierte."""
    if isinstance(valor, datetime):
        return valor
    if hasattr(valor, "year") and hasattr(valor, "month"):      # datetime.date
        return datetime(valor.year, valor.month, valor.day)
    texto = str(valor).strip()
    for formato in ("%Y-%m-%d %H:%M", "%Y-%m-%dT%H:%M", "%Y-%m-%d %H:%M:%S", "%Y-%m-%dT%H:%M:%S", "%Y-%m-%d"):
        try:
            return datetime.strptime(texto, formato)
        except ValueError:
            continue
    raise ErrorEvaluador(f"La fecha del informe '{origen}' es {valor!r}; escríbela como AAAA-MM-DD HH:MM.")


def cargar_informe(ruta: Path) -> dict:
    if not ruta.is_file():
        raise ErrorEvaluador(f"No existe el informe '{ruta}'.")
    texto = ruta.read_text(encoding="utf-8")
    try:
        datos = yaml.safe_load(extraer_yaml(texto))
    except yaml.YAMLError as e:
        raise ErrorEvaluador(f"El informe '{ruta}' no es YAML válido: {e}") from e
    return validar_informe(datos, str(ruta))


def informes_anteriores(carpeta: Path) -> list[Path]:
    """Informes ya guardados en la carpeta, ordenados del más antiguo al más reciente por el nombre (lleva la fecha)."""
    patron = re.compile(rf"^{PREFIJO_INFORME}\d{{8}}_\d{{4}}(_\d+)?\.yaml$")
    return sorted(p for p in carpeta.iterdir() if p.is_file() and patron.match(p.name))


def guardar_informe(informe: dict, carpeta: Path) -> Path:
    """Guarda el informe como informe_evaluacion_<AAAAMMDD_HHMM>.yaml; si ya existe, añade _2, _3…"""
    carpeta.mkdir(parents=True, exist_ok=True)
    sello = datetime.strptime(informe["fecha"], "%Y-%m-%d %H:%M").strftime("%Y%m%d_%H%M")
    destino = carpeta / f"{PREFIJO_INFORME}{sello}.yaml"
    n = 2
    while destino.exists():
        destino = carpeta / f"{PREFIJO_INFORME}{sello}_{n}.yaml"
        n += 1
    cabecera = (f"# Informe de evaluación del agente (formato {FORMATO_INFORME}). Guardado por evaluador.py "
                f"{__version__} el {datetime.now().strftime('%Y-%m-%d %H:%M')}.\n")
    cuerpo = yaml.safe_dump(informe, allow_unicode=True, sort_keys=False, width=120)
    destino.write_text(cabecera + cuerpo, encoding="utf-8", newline="\n")
    return destino


def comparar_informes(actual: dict, anterior: dict | None, banco: dict | None) -> dict:
    """Calcula la comparación. Devuelve un mapa con los totales y una fila por tarea (sin imprimir nada)."""
    manual_por_id = {t["id"]: t["tiempo_manual_min"] for t in banco["tareas"]} if banco else {}
    ant_por_id = {t["id"]: t for t in anterior["tareas"]} if anterior else {}
    filas = []
    fallo_a_ok, ok_a_fallo, siguen_ok, siguen_fallo, nuevas = [], [], [], [], []
    for t in actual["tareas"]:
        a = ant_por_id.get(t["id"])
        fila = {
            "id": t["id"],
            "antes": a["resultado"] if a else None,
            "ahora": t["resultado"],
            "tiempo_antes_s": a["tiempo_s"] if a else None,
            "tiempo_ahora_s": t["tiempo_s"],
            "diferencia_s": (t["tiempo_s"] - a["tiempo_s"]) if a else None,
            "manual_min": manual_por_id.get(t["id"]),
            "fallo": t["fallo"],
        }
        if a is None:
            fila["cambio"] = "nueva"
            nuevas.append(t["id"])
        elif a["resultado"] == "FALLO" and t["resultado"] == "OK":
            fila["cambio"] = "FALLO → OK"
            fallo_a_ok.append(t["id"])
        elif a["resultado"] == "OK" and t["resultado"] == "FALLO":
            fila["cambio"] = "OK → FALLO"
            ok_a_fallo.append(t["id"])
        elif t["resultado"] == "OK":
            fila["cambio"] = "sigue OK"
            siguen_ok.append(t["id"])
        else:
            fila["cambio"] = "sigue FALLO"
            siguen_fallo.append(t["id"])
        filas.append(fila)
    ids_actual = {t["id"] for t in actual["tareas"]}
    desaparecidas = [i for i in ant_por_id if i not in ids_actual]
    total_ahora = sum(t["tiempo_s"] for t in actual["tareas"])
    total_antes = sum(t["tiempo_s"] for t in anterior["tareas"]) if anterior else None
    manual_ids = [t["id"] for t in actual["tareas"] if t["id"] in manual_por_id]
    total_manual_s = sum(manual_por_id[i] * 60 for i in manual_ids) if manual_ids else None
    ids_sin_manual = [t["id"] for t in actual["tareas"] if t["id"] not in manual_por_id] if banco else []
    return {
        "filas": filas,
        "fallo_a_ok": fallo_a_ok,
        "ok_a_fallo": ok_a_fallo,
        "siguen_ok": siguen_ok,
        "siguen_fallo": siguen_fallo,
        "nuevas": nuevas,
        "desaparecidas": desaparecidas,
        "ok_ahora": sum(1 for t in actual["tareas"] if t["resultado"] == "OK"),
        "ok_antes": (sum(1 for t in anterior["tareas"] if t["resultado"] == "OK") if anterior else None),
        "n_tareas": len(actual["tareas"]),
        "total_ahora_s": total_ahora,
        "total_antes_s": total_antes,
        "diferencia_total_s": (total_ahora - total_antes) if anterior else None,
        "total_manual_s": total_manual_s,
        "ids_sin_manual": ids_sin_manual,
    }


def fmt_s(valor) -> str:
    if valor is None:
        return "—"
    if isinstance(valor, float) and not valor.is_integer():
        return f"{valor:.1f} s"
    return f"{int(valor)} s"


def fmt_dif(valor) -> str:
    if valor is None:
        return "—"
    signo = "+" if valor > 0 else ""
    if isinstance(valor, float) and not valor.is_integer():
        return f"{signo}{valor:.1f} s"
    return f"{signo}{int(valor)} s"


def lista_ids(ids: list[int]) -> str:
    return ", ".join(str(i) for i in ids) if ids else "ninguna"


def texto_comparacion(actual: dict, anterior: dict | None, ruta_anterior: Path | None, comp: dict) -> str:
    """Tabla legible con la comparación; es lo que imprime `comparar`."""
    L: list[str] = []
    a = L.append
    a(f"Informe actual: {actual['fecha']}, plugin {actual['version_plugin']}, puente {actual['version_puente']}, "
      f"{comp['n_tareas']} tareas, {comp['ok_ahora']} OK.")
    if anterior is None:
        a("No hay informe anterior en la carpeta: este es el primero (línea base). Solo se guarda; no hay con qué "
          "comparar todavía.")
    else:
        a(f"Informe anterior: {anterior['fecha']}, plugin {anterior['version_plugin']}, puente "
          f"{anterior['version_puente']}, {len(anterior['tareas'])} tareas, {comp['ok_antes']} OK "
          f"({ruta_anterior.name if ruta_anterior else ''}).")
    a("")
    a("| id | Antes | Ahora | Cambio | Tiempo antes | Tiempo ahora | Diferencia | Manual (min) | Fallo |")
    a("|---|---|---|---|---|---|---|---|---|")
    for f in comp["filas"]:
        manual = "—" if f["manual_min"] is None else str(f["manual_min"])
        fallo = f["fallo"] if f["ahora"] == "FALLO" else ""
        a(f"| {f['id']} | {f['antes'] or '—'} | {f['ahora']} | {f['cambio']} | {fmt_s(f['tiempo_antes_s'])} | "
          f"{fmt_s(f['tiempo_ahora_s'])} | {fmt_dif(f['diferencia_s'])} | {manual} | {fallo} |")
    a("")
    if anterior is not None:
        a(f"Pasaron de FALLO a OK: {len(comp['fallo_a_ok'])} (ids: {lista_ids(comp['fallo_a_ok'])}).")
        a(f"Pasaron de OK a FALLO: {len(comp['ok_a_fallo'])} (ids: {lista_ids(comp['ok_a_fallo'])}).")
        a(f"Siguen igual: {len(comp['siguen_ok']) + len(comp['siguen_fallo'])} "
          f"({len(comp['siguen_ok'])} siguen OK, {len(comp['siguen_fallo'])} siguen FALLO).")
        if comp["nuevas"]:
            a(f"Tareas nuevas (no estaban en el anterior): {lista_ids(comp['nuevas'])}.")
        if comp["desaparecidas"]:
            a(f"Tareas del informe anterior que ya no aparecen: {lista_ids(comp['desaparecidas'])}.")
        a(f"Tiempo total: antes {fmt_s(comp['total_antes_s'])}, ahora {fmt_s(comp['total_ahora_s'])} "
          f"({fmt_dif(comp['diferencia_total_s'])}).")
    else:
        a(f"Tiempo total del agente: {fmt_s(comp['total_ahora_s'])}.")
    if comp["total_manual_s"] is not None:
        manual_min = comp["total_manual_s"] / 60
        relacion = comp["total_ahora_s"] / comp["total_manual_s"] if comp["total_manual_s"] else None
        frase = (f"Tiempo total del agente frente al manual de la firma: {fmt_s(comp['total_ahora_s'])} frente a "
                 f"{manual_min:g} min ({fmt_s(comp['total_manual_s'])})")
        if relacion is not None:
            frase += f", es decir, el {relacion * 100:.0f} % del tiempo manual"
        a(frase + ".")
        if comp["ids_sin_manual"]:
            a(f"Sin tiempo manual en el banco para las tareas: {lista_ids(comp['ids_sin_manual'])} "
              f"(no cuentan en la suma manual).")
    else:
        a("No se encontró el banco de tareas (tareas.yaml): no se compara con el tiempo manual. Define "
          "MEMORIA_FIRMA_DIR o pasa --tareas para tenerlo.")
    return "\n".join(L)


def comando_comparar(args: argparse.Namespace) -> int:
    carpeta = carpeta_memoria(args.informes, "--informes", "evaluacion/informes")
    actual = cargar_informe(Path(args.informe))
    banco = intentar_cargar_tareas(args.tareas)
    if banco:
        ids_banco = {t["id"] for t in banco["tareas"]}
        ids_informe = {t["id"] for t in actual["tareas"]}
        if ids_banco != ids_informe:
            print(f"Aviso: los ids del informe ({lista_ids(sorted(ids_informe))}) no coinciden con los del banco "
                  f"({lista_ids(sorted(ids_banco))}). Se compara igualmente.")
    anteriores = informes_anteriores(carpeta) if carpeta.is_dir() else []
    ruta_anterior = anteriores[-1] if anteriores else None
    anterior = cargar_informe(ruta_anterior) if ruta_anterior else None
    destino = guardar_informe(actual, carpeta)
    comp = comparar_informes(actual, anterior, banco)
    print(f"Informe guardado en {destino}")
    print()
    print(texto_comparacion(actual, anterior, ruta_anterior, comp))
    return 0


# ----------------------------------------------------------------------------------------------------------------
# Línea de comandos
# ----------------------------------------------------------------------------------------------------------------

def construir_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="evaluador.py",
        description="Evaluador automático del agente sobre Civil 3D: genera el prompt de evaluación y compara "
                    "informes. Lee memoria-firma desde MEMORIA_FIRMA_DIR o desde las rutas que se le pasen.")
    parser.add_argument("--version", action="version", version=f"evaluador {__version__}")
    sub = parser.add_subparsers(dest="comando", required=True)

    g = sub.add_parser("generar", help="escribe el prompt de evaluación en Markdown a partir de tareas.yaml")
    g.add_argument("--tareas", help="ruta de tareas.yaml (por defecto MEMORIA_FIRMA_DIR/evaluacion/tareas.yaml)")
    g.add_argument("--salida", help="archivo Markdown de salida (por defecto "
                                    "MEMORIA_FIRMA_DIR/evaluacion/PROMPT_EVALUACION.md; sin MEMORIA_FIRMA_DIR, "
                                    "se imprime en pantalla)")
    g.set_defaults(funcion=comando_generar)

    c = sub.add_parser("comparar", help="valida un informe YAML, lo guarda con fecha y lo compara con el anterior")
    c.add_argument("informe", help="archivo con el bloque YAML que devolvió el agente (.yaml, o .md con el bloque)")
    c.add_argument("--informes", help="carpeta donde se guardan los informes (por defecto "
                                      "MEMORIA_FIRMA_DIR/evaluacion/informes)")
    c.add_argument("--tareas", help="ruta de tareas.yaml para comparar con el tiempo manual (por defecto "
                                    "MEMORIA_FIRMA_DIR/evaluacion/tareas.yaml; si no existe, se omite)")
    c.set_defaults(funcion=comando_comparar)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = construir_parser()
    args = parser.parse_args(argv)
    try:
        return args.funcion(args)
    except ErrorEvaluador as e:
        print(f"Error: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())

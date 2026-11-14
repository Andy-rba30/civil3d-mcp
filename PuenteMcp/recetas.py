# -*- coding: utf-8 -*-
"""recetas (1.17.0): recetas de código de la memoria técnica de la firma (repositorio privado `memoria-firma`).

Una **receta de código** es un script de `ejecutar_codigo` (la herramienta del plugin que compila y ejecuta C#) que ya
resolvió un caso y que el dueño aprobó guardar. Vive en `MEMORIA_FIRMA_DIR/recetas/` como dos archivos:

- `<nombre>.cs`: el script, tal cual se pasa a `ejecutar_codigo` en `codigo_csharp`.
- `<nombre>.yaml`: sus datos: nombre, pedido de ejemplo en español, parámetros (nombre, tipo, descripción,
  obligatorio, por_defecto), notas, versión de Civil 3D, versión del conector, fecha, veces_usada, ultima_vez y la
  aprobación del dueño (sus palabras literales y la fecha).

Las dos herramientas del puente que las usan (registradas en main.py):

- `guardar_receta`: sin `aprobada_por_el_dueno=true` y las palabras del dueño en `aprobacion_del_dueno` **no escribe
  nada** y devuelve la vista previa; con las dos, escribe los dos archivos. Rechaza nombres que no sirven como nombre
  de archivo (rutas, `..`, nombres reservados de Windows), no sobrescribe una receta existente salvo `reemplazar=true`
  y nunca guarda el token del plugin ni datos de obra (rutas absolutas, nombres `.dwg`, nombres de
  `casos/PROHIBIDOS.txt`).
- `ejecutar_receta`: lee la receta, comprueba los parámetros contra el `.yaml` y prepara la llamada a
  `ejecutar_codigo`; main.py la envía al plugin y `cerrar_ejecucion` suma 1 a `veces_usada` solo si la ejecución fue
  real (`simular=false`) y salió bien.

Este módulo no habla con Civil 3D: solo lee y escribe archivos. Así se prueba sin plugin (tests/test_recetas.py).
"""
from __future__ import annotations

import datetime
import json
import math
import os
import re
import tempfile
import threading

import buscar_api
import exportar_dataset

VERSION_CIVIL3D = "2027"
TIPOS_PARAMETRO = ("string", "number", "boolean", "json")
CLAVES_PARAMETRO = ("nombre", "tipo", "descripcion", "obligatorio", "por_defecto")
EXTENSION_CODIGO = ".cs"
EXTENSION_DATOS = ".yaml"
MAXIMO_CODIGO = 200_000
MAXIMO_TEXTO = 10_000
MAXIMO_PARAMETROS = 50

_RE_NOMBRE = re.compile(r"^[a-z0-9][a-z0-9_]{2,63}$")
_RE_NOMBRE_PARAMETRO = re.compile(r"^[a-z_][a-z0-9_]{0,63}$")
_RESERVADOS_WINDOWS = {"con", "prn", "aux", "nul"} | {f"com{i}" for i in range(1, 10)} | {f"lpt{i}" for i in range(1, 10)}

# Lo que nunca entra en una receta (regla 7 de CLAUDE.md): el token del plugin (32 bytes en hexadecimal = 64
# caracteres), su archivo o su cabecera, rutas absolutas de Windows o UNC y nombres de dibujo.
_RE_HEX64 = re.compile(r"(?<![0-9A-Fa-f])[0-9A-Fa-f]{64}(?![0-9A-Fa-f])")
_RE_ARCHIVO_TOKEN = re.compile(r"arbamcp[\\/]+token|x-arba-token", re.IGNORECASE)
_RE_RUTA = re.compile(r"(?<![A-Za-z0-9])[A-Za-z]:[\\/]|\\\\[A-Za-z0-9_.$-]+[\\/]")
_RE_DWG = re.compile(r"[^\s\\/\"'<>|:()]+\.dwg\b", re.IGNORECASE)

_CERROJO = threading.Lock()   # dos ejecuciones a la vez no deben pisarse el contador de veces_usada

CABECERA_YAML = (
    "# Receta de código de civil3d-mcp, escrita por guardar_receta con la aprobación del dueño. El código está en\n"
    "# {codigo}. ejecutar_receta suma veces_usada en cada ejecución real que sale bien: no lo cambies a mano.\n"
)


class ErrorReceta(Exception):
    """Algo impide guardar o ejecutar la receta. El mensaje ya dice qué pasa y qué hacer."""


# ---------------------------------------------------------------------------------------------- carpetas y nombres
def carpeta_memoria(memoria: str | None = None) -> str:
    """Carpeta de memoria-firma (argumento o MEMORIA_FIRMA_DIR). No exige el catálogo: guardar una receta no lo usa."""
    carpeta = memoria or os.environ.get(buscar_api.VARIABLE_ENTORNO) or ""
    if not carpeta.strip():
        raise ErrorReceta(
            f"Falta la variable de entorno {buscar_api.VARIABLE_ENTORNO}: debe apuntar a la carpeta del repositorio "
            f"privado memoria-firma (la que contiene recetas\\). {buscar_api.COMO_PONER_VARIABLE}"
        )
    carpeta = os.path.abspath(os.path.expanduser(os.path.expandvars(carpeta.strip())))
    if not os.path.isdir(carpeta):
        raise ErrorReceta(
            f"La carpeta de {buscar_api.VARIABLE_ENTORNO} no existe: {carpeta}. Clona memoria-firma ahí o corrige la "
            f"variable. {buscar_api.COMO_PONER_VARIABLE}"
        )
    return carpeta


def sugerir_nombre(texto: str) -> str:
    """Un nombre válido parecido al pedido: sin tildes, en minúsculas y con guiones bajos ('Crear Capa' → 'crear_capa')."""
    base = re.sub(r"[^a-z0-9]+", "_", buscar_api.normalizar(texto or "")).strip("_")[:64].strip("_")
    if len(base) < 3 or base in _RESERVADOS_WINDOWS:
        base = ("receta_" + base).strip("_")[:64]
    return base


def validar_nombre(nombre) -> str:
    """El nombre de la receta es el nombre de sus dos archivos: de 3 a 64 caracteres en minúsculas, números y '_',
    sin rutas, puntos ni nombres reservados de Windows. Devuelve el nombre limpio o lanza ErrorReceta con una sugerencia."""
    limpio = (nombre or "").strip() if isinstance(nombre, str) else ""
    if _RE_NOMBRE.match(limpio) and limpio not in _RESERVADOS_WINDOWS:
        return limpio
    raise ErrorReceta(
        f"El nombre de receta '{nombre}' no vale: usa de 3 a 64 caracteres en minúsculas, números y guiones bajos, sin "
        f"rutas, puntos ni espacios (por ejemplo '{sugerir_nombre(str(nombre or ''))}')."
    )


def rutas_receta(carpeta_recetas: str, nombre: str) -> tuple[str, str]:
    """Rutas del .cs y del .yaml. Comprueba que quedan dentro de la carpeta de recetas (defensa extra al nombre)."""
    base = os.path.abspath(carpeta_recetas)
    ruta_cs = os.path.abspath(os.path.join(base, nombre + EXTENSION_CODIGO))
    ruta_yaml = os.path.abspath(os.path.join(base, nombre + EXTENSION_DATOS))
    for ruta in (ruta_cs, ruta_yaml):
        if os.path.dirname(ruta) != base:
            raise ErrorReceta(f"El nombre de receta '{nombre}' sale de la carpeta de recetas; usa un nombre sin rutas.")
    return ruta_cs, ruta_yaml


# ---------------------------------------------------------------------------------------------- parámetros
def _es_numero(valor) -> bool:
    return isinstance(valor, (int, float)) and not isinstance(valor, bool) and math.isfinite(valor)


def tipo_correcto(valor, tipo: str) -> bool:
    """Tipos del contrato: string, number (entero o decimal, no booleano), boolean y json (cualquier valor JSON)."""
    if tipo == "string":
        return isinstance(valor, str)
    if tipo == "number":
        return _es_numero(valor)
    if tipo == "boolean":
        return isinstance(valor, bool)
    if tipo == "json":
        try:
            json.dumps(valor)
            return True
        except (TypeError, ValueError):
            return False
    return False


def _nombre_tipo(valor) -> str:
    if isinstance(valor, bool):
        return "boolean"
    if _es_numero(valor):
        return "number"
    if isinstance(valor, str):
        return "string"
    if isinstance(valor, list):
        return "lista JSON"
    if isinstance(valor, dict):
        return "objeto JSON"
    return type(valor).__name__


EJEMPLO_PARAMETROS = ('[{"nombre": "nombre_capa", "tipo": "string", "descripcion": "Nombre de la capa", '
                      '"obligatorio": true}, {"nombre": "color", "tipo": "number", "por_defecto": 7}]')


def normalizar_parametros(parametros, origen: str = "parametros") -> list[dict]:
    """Lista de parámetros declarados de una receta, comprobada y en un formato fijo: nombre, tipo, descripcion,
    obligatorio y, si lo hay, por_defecto. Acepta la lista o un texto con la lista JSON. Lanza ErrorReceta."""
    if parametros is None or (isinstance(parametros, str) and not parametros.strip()):
        return []
    if isinstance(parametros, str):
        try:
            parametros = json.loads(parametros)
        except ValueError as e:
            raise ErrorReceta(f"'{origen}' no es JSON válido ({e}). Ejemplo: {EJEMPLO_PARAMETROS}") from e
    if not isinstance(parametros, list):
        raise ErrorReceta(f"'{origen}' debe ser una lista JSON de parámetros. Ejemplo: {EJEMPLO_PARAMETROS}")
    if len(parametros) > MAXIMO_PARAMETROS:
        raise ErrorReceta(f"'{origen}' tiene {len(parametros)} parámetros; el máximo es {MAXIMO_PARAMETROS}.")
    salida, vistos = [], set()
    for i, p in enumerate(parametros):
        if not isinstance(p, dict):
            raise ErrorReceta(f"El parámetro {i} de '{origen}' debe ser un objeto con nombre y tipo. Ejemplo: {EJEMPLO_PARAMETROS}")
        sobrantes = [k for k in p if k not in CLAVES_PARAMETRO]
        if sobrantes:
            raise ErrorReceta(f"El parámetro {i} de '{origen}' tiene claves desconocidas: {', '.join(map(str, sobrantes))}. "
                              f"Se admiten: {', '.join(CLAVES_PARAMETRO)}.")
        nombre = p.get("nombre")
        if not isinstance(nombre, str) or not _RE_NOMBRE_PARAMETRO.match(nombre):
            raise ErrorReceta(f"El parámetro {i} de '{origen}' necesita un 'nombre' en minúsculas, números y guiones bajos "
                              f"(llegó {nombre!r}).")
        if nombre in vistos:
            raise ErrorReceta(f"El parámetro '{nombre}' está repetido en '{origen}'.")
        vistos.add(nombre)
        tipo = p.get("tipo")
        if tipo not in TIPOS_PARAMETRO:
            raise ErrorReceta(f"El parámetro '{nombre}' tiene el tipo {tipo!r}; usa uno de: {', '.join(TIPOS_PARAMETRO)}.")
        descripcion = p.get("descripcion") or ""
        if not isinstance(descripcion, str):
            raise ErrorReceta(f"La 'descripcion' del parámetro '{nombre}' debe ser un texto.")
        tiene_defecto = p.get("por_defecto") is not None
        obligatorio = p.get("obligatorio")
        if obligatorio is None:
            obligatorio = not tiene_defecto
        if not isinstance(obligatorio, bool):
            raise ErrorReceta(f"'obligatorio' del parámetro '{nombre}' debe ser true o false.")
        if obligatorio and tiene_defecto:
            raise ErrorReceta(f"El parámetro '{nombre}' es obligatorio y tiene por_defecto; quita uno de los dos.")
        fila = {"nombre": nombre, "tipo": tipo, "descripcion": descripcion.strip(), "obligatorio": obligatorio}
        if tiene_defecto:
            if not tipo_correcto(p["por_defecto"], tipo):
                raise ErrorReceta(f"El por_defecto del parámetro '{nombre}' debe ser {tipo} (llegó {_nombre_tipo(p['por_defecto'])}).")
            fila["por_defecto"] = p["por_defecto"]
        salida.append(fila)
    return salida


def preparar_parametros(declarados: list[dict], recibidos) -> dict:
    """Valores para el script: comprueba los recibidos (objeto JSON o texto con el objeto) contra los declarados en el
    .yaml, completa los que faltan con su por_defecto y los devuelve en el orden declarado. Lanza ErrorReceta."""
    if recibidos is None or (isinstance(recibidos, str) and not recibidos.strip()):
        recibidos = {}
    if isinstance(recibidos, str):
        try:
            recibidos = json.loads(recibidos)
        except ValueError as e:
            raise ErrorReceta(f"'parametros' no es JSON válido ({e}). Debe ser un objeto, por ejemplo {{\"nombre_capa\": \"Capa 1\"}}.") from e
    if not isinstance(recibidos, dict):
        raise ErrorReceta("'parametros' debe ser un objeto JSON con un valor por parámetro, por ejemplo {\"nombre_capa\": \"Capa 1\"}.")
    nombres = [d["nombre"] for d in declarados]
    lista = ", ".join(_describir_parametro(d) for d in declarados) or "ninguno"
    desconocidos = [k for k in recibidos if k not in nombres]
    if desconocidos:
        raise ErrorReceta(f"La receta no tiene los parámetros: {', '.join(map(str, desconocidos))}. Parámetros de la receta: {lista}.")
    valores = {}
    for d in declarados:
        valor = recibidos.get(d["nombre"])
        if valor is None:
            if d["obligatorio"]:
                raise ErrorReceta(f"Falta el parámetro obligatorio '{d['nombre']}'. Parámetros de la receta: {lista}.")
            if "por_defecto" in d:
                valores[d["nombre"]] = d["por_defecto"]
            continue
        if not tipo_correcto(valor, d["tipo"]):
            raise ErrorReceta(f"El parámetro '{d['nombre']}' debe ser {d['tipo']} (llegó {_nombre_tipo(valor)}). "
                              f"Parámetros de la receta: {lista}.")
        valores[d["nombre"]] = valor
    return valores


def _describir_parametro(d: dict) -> str:
    if d.get("obligatorio"):
        return f"{d['nombre']} ({d['tipo']}, obligatorio)"
    if "por_defecto" in d:
        return f"{d['nombre']} ({d['tipo']}, por defecto {json.dumps(d['por_defecto'], ensure_ascii=False)})"
    return f"{d['nombre']} ({d['tipo']}, opcional)"


# ---------------------------------------------------------------------------------------------- datos prohibidos
def comprobar_datos_prohibidos(textos: dict, token: str = "", prohibidos: list | None = None) -> None:
    """Lanza ErrorReceta si algún texto que se va a guardar lleva el token, su archivo, una ruta absoluta, un nombre
    .dwg o un nombre de la lista de prohibidos. El mensaje nunca repite el token ni el nombre prohibido."""
    prohibidos = prohibidos or []
    for campo, texto in textos.items():
        if not texto:
            continue
        if token and token in texto:
            raise ErrorReceta(f"'{campo}' contiene el token del plugin (<oculto>). Una receta nunca guarda el token: quítalo.")
        if _RE_HEX64.search(texto):
            raise ErrorReceta(f"'{campo}' contiene un texto de 64 caracteres hexadecimales que parece un token (<oculto>). "
                              "Una receta nunca guarda el token: quítalo.")
        if _RE_ARCHIVO_TOKEN.search(texto):
            raise ErrorReceta(f"'{campo}' nombra el archivo o la cabecera del token del plugin. Una receta no puede leer ni "
                              "enviar el token: quita esa parte.")
        if _RE_RUTA.search(texto):
            raise ErrorReceta(f"'{campo}' lleva una ruta absoluta (C:\\..., \\\\servidor\\...). Las rutas son datos de obra: "
                              "pásala como parámetro de la receta y no la escribas en ella.")
        if _RE_DWG.search(texto):
            raise ErrorReceta(f"'{campo}' lleva el nombre de un dibujo (.dwg). Es un dato de obra: pásalo como parámetro "
                              "de la receta y no lo escribas en ella.")
        normal = buscar_api.normalizar(texto)
        for n, nombre in enumerate(prohibidos, 1):
            if buscar_api._contiene_palabra(normal, buscar_api.normalizar(nombre)):
                raise ErrorReceta(f"'{campo}' contiene un nombre de la lista casos\\PROHIBIDOS.txt de memoria-firma (dato de "
                                  "un cliente o de una obra). Quítalo o pásalo como parámetro; la receta debe ser genérica.")


def _prohibidos(carpeta: str) -> list:
    return exportar_dataset.leer_prohibidos(os.path.join(carpeta, exportar_dataset.PROHIBIDOS_RELATIVO))


# ---------------------------------------------------------------------------------------------- YAML
def _yaml():
    try:
        import yaml  # dependencia del puente (pyyaml); se importa aquí, como en buscar_api
    except ImportError as e:
        raise ErrorReceta("Falta la biblioteca PyYAML para leer y escribir recetas. Instálala desde PuenteMcp con "
                          "'uv sync' (o 'pip install pyyaml' en el .venv) y reinicia el puente.") from e
    return yaml


def _texto_yaml(datos: dict) -> str:
    """El .yaml de la receta: cabecera de comentario y los datos en el orden de `datos`; textos de varias líneas en
    bloque literal (|) para que se lean bien en GitHub."""
    yaml = _yaml()

    class _Volcador(yaml.SafeDumper):
        pass

    def _texto(dumper, valor):
        estilo = "|" if "\n" in valor else None
        return dumper.represent_scalar("tag:yaml.org,2002:str", valor, style=estilo)

    _Volcador.add_representer(str, _texto)
    cuerpo = yaml.dump(datos, Dumper=_Volcador, allow_unicode=True, sort_keys=False, default_flow_style=False, width=120)
    return CABECERA_YAML.format(codigo=datos.get("codigo", "")) + cuerpo


def _escribir_atomico(ruta: str, texto: str) -> None:
    """Escribe en un temporal de la misma carpeta y lo cambia por el archivo de una vez: nunca queda medio escrito."""
    carpeta = os.path.dirname(ruta)
    fd, temporal = tempfile.mkstemp(prefix=".receta_", suffix=".tmp", dir=carpeta)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as f:
            f.write(texto)
        os.replace(temporal, ruta)
    except BaseException:
        try:
            os.remove(temporal)
        except OSError:
            pass
        raise


def _leer_yaml(ruta: str) -> dict:
    yaml = _yaml()
    try:
        with open(ruta, "r", encoding="utf-8-sig") as f:
            datos = yaml.safe_load(f)
    except Exception as e:
        raise ErrorReceta(f"No se pudo leer el archivo de la receta {ruta}: {e}") from e
    if not isinstance(datos, dict):
        raise ErrorReceta(f"El archivo de la receta {ruta} no tiene el formato esperado (nombre, pedido, parametros...).")
    return datos


def _hoy(hoy=None) -> str:
    return (hoy or datetime.date.today()).isoformat()


def _veces(datos: dict) -> int:
    try:
        return max(0, int(datos.get("veces_usada") or 0))
    except (TypeError, ValueError):
        return 0


# ---------------------------------------------------------------------------------------------- guardar_receta
def guardar(nombre, pedido, codigo, parametros=None, notas="", aprobada_por_el_dueno=False, aprobacion_del_dueno="",
            reemplazar=False, memoria: str | None = None, token: str = "", version_conector: str = "", hoy=None) -> dict:
    """La herramienta guardar_receta. Devuelve siempre un dict: {"ok": false, "error": ...} o la vista previa
    (guardada=false) o lo guardado (guardada=true, archivos, recordatorio de L.2). Nunca lanza."""
    try:
        return _guardar(nombre, pedido, codigo, parametros, notas, aprobada_por_el_dueno, aprobacion_del_dueno,
                        reemplazar, memoria, token, version_conector, hoy)
    except ErrorReceta as e:
        return {"ok": False, "error": str(e), "guardada": False}
    except OSError as e:
        return {"ok": False, "error": f"No se pudo escribir la receta: {e}", "guardada": False}


def _texto_obligatorio(valor, campo: str, explicacion: str, maximo: int) -> str:
    texto = valor.strip() if isinstance(valor, str) else ""
    if not texto:
        raise ErrorReceta(f"Falta '{campo}': {explicacion}")
    if len(texto) > maximo:
        raise ErrorReceta(f"'{campo}' tiene {len(texto)} caracteres; el máximo es {maximo}.")
    return texto


def _guardar(nombre, pedido, codigo, parametros, notas, aprobada, aprobacion, reemplazar, memoria, token,
             version_conector, hoy) -> dict:
    nombre = validar_nombre(nombre)
    pedido = _texto_obligatorio(pedido, "pedido", "el pedido de ejemplo en español, con las palabras del usuario "
                                "(por ejemplo 'Crea la capa Bordes de color 3').", MAXIMO_TEXTO)
    if not isinstance(codigo, str) or not codigo.strip():
        raise ErrorReceta("Falta 'codigo': el script C# que funcionó con ejecutar_codigo, tal cual.")
    if len(codigo) > MAXIMO_CODIGO:
        raise ErrorReceta(f"'codigo' tiene {len(codigo)} caracteres; el máximo es {MAXIMO_CODIGO}.")
    notas = notas.strip() if isinstance(notas, str) else ""
    if len(notas) > MAXIMO_TEXTO:
        raise ErrorReceta(f"'notas' tiene {len(notas)} caracteres; el máximo es {MAXIMO_TEXTO}.")
    aprobacion = aprobacion.strip() if isinstance(aprobacion, str) else ""
    declarados = normalizar_parametros(parametros)

    carpeta = carpeta_memoria(memoria)
    textos = {"nombre": nombre, "pedido": pedido, "codigo": codigo, "notas": notas, "aprobacion_del_dueno": aprobacion}
    for d in declarados:
        textos[f"parametros.{d['nombre']}"] = d["descripcion"] + "\n" + json.dumps(d.get("por_defecto"), ensure_ascii=False)
    comprobar_datos_prohibidos(textos, token, _prohibidos(carpeta))

    carpeta_recetas = os.path.join(carpeta, buscar_api.CARPETA_RECETAS)
    ruta_cs, ruta_yaml = rutas_receta(carpeta_recetas, nombre)
    existe = os.path.exists(ruta_cs) or os.path.exists(ruta_yaml)
    anterior = {}
    if existe:
        if os.path.exists(ruta_yaml):
            try:
                anterior = _leer_yaml(ruta_yaml)
            except ErrorReceta:
                anterior = {}
        if not reemplazar:
            raise ErrorReceta(
                f"Ya existe la receta '{nombre}' ({ruta_yaml}, veces_usada {_veces(anterior)}). No se sobrescribe sin "
                "decirlo: elige otro nombre o, si el dueño pidió cambiarla, repite con reemplazar=true."
            )

    avisos = [f"El parámetro '{d['nombre']}' no aparece en el código: el script no lo leerá de Parametros."
              for d in declarados if not re.search(r"(?<![A-Za-z0-9_])" + re.escape(d["nombre"]) + r"(?![A-Za-z0-9_])", codigo)]
    fecha = _hoy(hoy)
    datos = {
        "nombre": nombre,
        "pedido": pedido,
        "parametros": declarados,
        "notas": notas,
        "codigo": nombre + EXTENSION_CODIGO,
        "version_civil3d": VERSION_CIVIL3D,
        "version_conector": version_conector,
        "fecha": str(anterior.get("fecha") or fecha) if existe else fecha,
        "veces_usada": _veces(anterior),
        "ultima_vez": anterior.get("ultima_vez") if existe else None,
    }
    if existe:
        datos["actualizada"] = fecha
    archivos = {"codigo": ruta_cs, "datos": ruta_yaml}

    if aprobada is not True or not aprobacion:
        if aprobada is True and not aprobacion:
            raise ErrorReceta(
                "Falta 'aprobacion_del_dueno': escribe las palabras literales con las que el dueño aprobó guardar la "
                "receta. Si no te lo ha dicho, pregúntale y espera su respuesta; no guardes nada sin su sí."
            )
        datos["aprobacion"] = {"palabras_del_dueno": "(pendiente: pregúntale al dueño)", "fecha": fecha}
        return {
            "ok": True,
            "guardada": False,
            "nombre": nombre,
            "reemplazaria": existe,
            "archivos_previstos": archivos,
            "yaml": _texto_yaml(datos),
            "lineas_codigo": len(codigo.splitlines()),
            "avisos": avisos,
            "siguiente_paso": (
                f"No se guardó nada. Enséñale al dueño esta vista previa y pregúntale si quiere guardar el script como "
                f"la receta '{nombre}'. Solo si responde que sí, repite la llamada con aprobada_por_el_dueno=true y sus "
                "palabras literales en aprobacion_del_dueno. Nunca lo decidas tú."
            ),
        }

    datos["aprobacion"] = {"palabras_del_dueno": aprobacion, "fecha": fecha}
    os.makedirs(carpeta_recetas, exist_ok=True)
    with _CERROJO:
        _escribir_atomico(ruta_cs, codigo)
        _escribir_atomico(ruta_yaml, _texto_yaml(datos))
    return {
        "ok": True,
        "guardada": True,
        "reemplazada": existe,
        "nombre": nombre,
        "archivos": archivos,
        "veces_usada": datos["veces_usada"],
        "avisos": avisos,
        "recordatorio": (
            f"Recuerda correr L.2 para subir la receta a GitHub: en {carpeta} ejecuta sincronizar_memoria.ps1 (Prompt "
            "L.2 del agente instalador). Hasta entonces la receta solo está en esta PC."
        ),
    }


# ---------------------------------------------------------------------------------------------- ejecutar_receta
def leer_receta(nombre, memoria: str | None = None) -> dict:
    """Datos, parámetros declarados, código y rutas de una receta de código. Lanza ErrorReceta con qué hacer."""
    nombre = validar_nombre(nombre)
    carpeta_recetas = os.path.join(carpeta_memoria(memoria), buscar_api.CARPETA_RECETAS)
    ruta_cs, ruta_yaml = rutas_receta(carpeta_recetas, nombre)
    if not os.path.isfile(ruta_yaml):
        if os.path.isfile(ruta_cs):
            raise ErrorReceta(f"La receta '{nombre}' no tiene sus datos (falta {nombre}{EXTENSION_DATOS} junto a "
                              f"{nombre}{EXTENSION_CODIGO}). Actualiza memoria-firma (git pull) o vuelve a guardarla.")
        if os.path.isfile(os.path.join(carpeta_recetas, nombre + ".md")):
            raise ErrorReceta(f"'{nombre}' es una receta de procedimiento ({nombre}.md), no de código: léela con buscar_api "
                              "y sigue sus pasos; ejecutar_receta solo reutiliza recetas .cs + .yaml.")
        existentes = []
        if os.path.isdir(carpeta_recetas):
            existentes = sorted(a[:-len(EXTENSION_DATOS)] for a in os.listdir(carpeta_recetas) if a.endswith(EXTENSION_DATOS))
        lista = ", ".join(existentes[:20]) or "ninguna"
        raise ErrorReceta(f"No existe la receta '{nombre}' en {carpeta_recetas}. Usa buscar_api para encontrarla. "
                          f"Recetas de código de la carpeta: {lista}.")
    if not os.path.isfile(ruta_cs):
        raise ErrorReceta(f"La receta '{nombre}' no tiene su código (falta {nombre}{EXTENSION_CODIGO} junto a "
                          f"{nombre}{EXTENSION_DATOS}). Actualiza memoria-firma (git pull) o vuelve a guardarla.")
    datos = _leer_yaml(ruta_yaml)
    try:
        declarados = normalizar_parametros(datos.get("parametros"), f"parametros de {nombre}{EXTENSION_DATOS}")
    except ErrorReceta as e:
        raise ErrorReceta(f"El archivo de la receta '{nombre}' tiene parámetros inválidos: {e}") from e
    with open(ruta_cs, "r", encoding="utf-8-sig", newline="") as f:
        codigo = f.read()
    if not codigo.strip():
        raise ErrorReceta(f"El código de la receta '{nombre}' ({ruta_cs}) está vacío.")
    return {"nombre": nombre, "datos": datos, "declarados": declarados, "codigo": codigo,
            "ruta_cs": ruta_cs, "ruta_yaml": ruta_yaml}


def preparar_ejecucion(nombre, parametros=None, simular=True, pedido=None, memoria: str | None = None) -> dict:
    """Lee la receta y prepara los argumentos de ejecutar_codigo. `parametros` viaja como texto JSON, igual que
    cualquier parámetro de tipo json que el puente pasa al plugin (el plugin lo convierte con Argumentos.Json)."""
    receta = leer_receta(nombre, memoria)
    valores = preparar_parametros(receta["declarados"], parametros)
    simular = simular is not False
    args = {"codigo_csharp": receta["codigo"], "simular": simular,
            "parametros": json.dumps(valores, ensure_ascii=False)}
    if isinstance(pedido, str) and pedido.strip():
        args["pedido"] = pedido
    return {**receta, "valores": valores, "simular": simular, "args": args}


def traducir_error(respuesta: dict) -> dict:
    """Si el plugin no conoce ejecutar_codigo (versión anterior a la 1.16.0), lo dice claro."""
    error = respuesta.get("error") if isinstance(respuesta, dict) else None
    if isinstance(error, str) and "Herramienta desconocida" in error and "ejecutar_codigo" in error:
        respuesta = dict(respuesta)
        respuesta["error"] = ("ejecutar_receta necesita la herramienta ejecutar_codigo del plugin (1.16.0 o posterior) y "
                              f"el plugin instalado no la tiene. Instala una versión del plugin que la traiga. ({error})")
    return respuesta


def anotar_uso(ruta_yaml: str, hoy=None) -> int:
    """Suma 1 a veces_usada y anota ultima_vez en el .yaml (escritura atómica). Devuelve el valor nuevo."""
    with _CERROJO:
        datos = _leer_yaml(ruta_yaml)
        datos["veces_usada"] = _veces(datos) + 1
        datos["ultima_vez"] = _hoy(hoy)
        _escribir_atomico(ruta_yaml, _texto_yaml(datos))
        return datos["veces_usada"]


def cerrar_ejecucion(preparada: dict, respuesta: dict, hoy=None) -> dict:
    """Bloque `receta` de la respuesta. Solo una ejecución real (simular=false) que salió bien suma 1 a veces_usada."""
    veces = _veces(preparada["datos"])
    bloque = {"nombre": preparada["nombre"], "parametros": preparada["valores"], "veces_usada": veces, "sumada": False}
    resultado = respuesta.get("resultado")
    simulado_por_plugin = isinstance(resultado, dict) and resultado.get("simulado") is True
    if preparada["simular"]:
        bloque["nota"] = ("Simulación: veces_usada no cambia; solo suma una ejecución real (simular=false) que sale bien. "
                          "Si el resultado es el esperado, confírmalo con el usuario y ejecútala con simular=false.")
    elif "error" in respuesta:
        bloque["nota"] = "La ejecución falló: veces_usada no cambia."
    elif simulado_por_plugin:
        bloque["nota"] = "El plugin respondió simulado=true: veces_usada no cambia."
    else:
        try:
            bloque["veces_usada"] = anotar_uso(preparada["ruta_yaml"], hoy)
            bloque["sumada"] = True
            bloque["nota"] = (f"Ejecución real correcta: veces_usada sube a {bloque['veces_usada']}. El contador está en "
                              "esta PC; L.2 lo sube a GitHub.")
        except (ErrorReceta, OSError) as e:
            bloque["aviso"] = f"La receta se ejecutó, pero no se pudo anotar el uso en {preparada['ruta_yaml']}: {e}"
    return bloque

# -*- coding: utf-8 -*-
"""buscar_api: busca en la memoria técnica de la firma (repositorio privado `memoria-firma`) antes de escribir código
contra la API de Civil 3D 2027. Es una herramienta del puente (Python), no del plugin: funciona con Civil 3D cerrado.

Qué consulta, y en este orden:
  1. Recetas: `recetas/*.md` (procedimientos paso a paso).
  2. Glosario: `catalogo/glosario.yaml` (español → clase o miembro de la API, con estado `confirmado` o
     `por confirmar`). Si el YAML no existe pero la tabla `glosario` del catálogo tiene filas, se usa la tabla.
  3. Catálogo: `catalogo/catalogo_c3d2027.sqlite` (tipos, métodos, propiedades y parámetros públicos de las DLL de
     Civil 3D 2027, con un índice de texto FTS5 en la tabla `busqueda`). Primero los conceptos que el glosario señaló
     para la consulta; después lo que encuentre el índice de texto con la consulta ampliada.

La consulta se amplía ANTES de buscar: si contiene un término del glosario o uno de sus sinónimos (sin tildes ni
mayúsculas), se añaden los nombres de sus clases y miembros a la búsqueda del catálogo.

La carpeta de `memoria-firma` llega por la variable de entorno MEMORIA_FIRMA_DIR. Si falta la variable o un archivo,
la herramienta responde un texto de error que dice qué falta y cómo ponerlo; nunca lanza una excepción.

Uso como comando, sin MCP (desde la carpeta PuenteMcp):
    uv run --frozen python buscar_api.py "rasante con PVI" [--limite N] [--memoria RUTA]
    python -m buscar_api "rasante con PVI"
Imprime el mismo texto que devuelve la herramienta MCP. Código de salida 0 si pudo buscar (aunque no haya
resultados) y 2 si falta la memoria (variable o archivo).
"""
from __future__ import annotations

import argparse
import os
import re
import sqlite3
import sys
import unicodedata

VARIABLE_ENTORNO = "MEMORIA_FIRMA_DIR"
RUTA_CATALOGO = os.path.join("catalogo", "catalogo_c3d2027.sqlite")
RUTA_GLOSARIO = os.path.join("catalogo", "glosario.yaml")
CARPETA_RECETAS = "recetas"
LIMITE_POR_DEFECTO = 10
LIMITE_MAXIMO = 100

COMO_PONER_VARIABLE = (
    "Ponla con PowerShell (y reinicia el puente): "
    "[Environment]::SetEnvironmentVariable('MEMORIA_FIRMA_DIR','C:\\Proyectos\\memoria-firma','User')"
)

# Palabras de la consulta que no aportan nada a la búsqueda de recetas ni del catálogo.
PALABRAS_VACIAS = {
    "a", "al", "como", "con", "de", "del", "el", "en", "es", "la", "las", "lo", "los", "o", "para", "por", "que",
    "se", "sin", "su", "sus", "un", "una", "unas", "unos", "y", "the", "of", "and", "to", "in", "for", "with",
    "quiero", "crear", "hacer", "usar", "puedo", "como",
}


# ---------------------------------------------------------------------------------------------- texto
def normalizar(texto: str) -> str:
    """Minúsculas, sin tildes y con un solo espacio entre palabras: así 'Rasante' y 'rasánte' coinciden."""
    if not texto:
        return ""
    sin_tildes = "".join(c for c in unicodedata.normalize("NFKD", str(texto)) if not unicodedata.combining(c))
    return re.sub(r"\s+", " ", sin_tildes.lower()).strip()


def _contiene_palabra(texto_normalizado: str, termino: str) -> bool:
    """True si el término (ya normalizado) aparece como palabra completa en el texto normalizado."""
    if not termino:
        return False
    return re.search(r"(?<![0-9a-záéíóúñü])" + re.escape(termino) + r"(?![0-9a-záéíóúñü])", texto_normalizado) is not None


def _palabras_utiles(consulta: str) -> list[str]:
    """Palabras de la consulta que vale la pena buscar: sin vacías y con al menos 3 letras (o 2 en mayúsculas, FG)."""
    salida = []
    for original in re.findall(r"[0-9A-Za-zÁÉÍÓÚÑÜáéíóúñü_]+", consulta):
        p = normalizar(original)
        if p in PALABRAS_VACIAS or p in salida:
            continue
        if len(p) >= 3 or (len(p) == 2 and original.isupper()):
            salida.append(p)
    return salida


def _nombre_corto(tipo: str) -> str:
    """'Autodesk.Civil.DatabaseServices.Profile' → 'Profile' (para leer firmas sin el espacio de nombres)."""
    if not tipo:
        return ""
    return re.sub(r"[A-Za-z_][A-Za-z0-9_]*\.", "", tipo)


# ---------------------------------------------------------------------------------------------- memoria (rutas)
class ErrorMemoria(Exception):
    """Falta la variable de entorno o un archivo de la memoria. El mensaje ya dice qué falta y cómo ponerlo."""


def localizar_memoria(carpeta: str | None = None) -> dict:
    """Comprueba la carpeta de memoria-firma y devuelve las rutas. Lanza ErrorMemoria con el texto para el agente."""
    carpeta = carpeta or os.environ.get(VARIABLE_ENTORNO) or ""
    if not carpeta.strip():
        raise ErrorMemoria(
            f"Falta la variable de entorno {VARIABLE_ENTORNO}: debe apuntar a la carpeta del repositorio privado "
            f"memoria-firma (la que contiene catalogo\\ y recetas\\). {COMO_PONER_VARIABLE}"
        )
    carpeta = os.path.abspath(os.path.expanduser(os.path.expandvars(carpeta.strip())))
    if not os.path.isdir(carpeta):
        raise ErrorMemoria(
            f"La carpeta de {VARIABLE_ENTORNO} no existe: {carpeta}. Clona memoria-firma ahí "
            f"(git clone <url privada> \"{carpeta}\") o corrige la variable. {COMO_PONER_VARIABLE}"
        )
    catalogo = os.path.join(carpeta, RUTA_CATALOGO)
    if not os.path.isfile(catalogo):
        raise ErrorMemoria(
            f"Falta el catálogo {catalogo}. Actualiza memoria-firma (git pull) o genéralo con "
            "herramientas-dev/ExtractorCatalogo de civil3d-mcp y cópialo a catalogo\\catalogo_c3d2027.sqlite."
        )
    glosario = os.path.join(carpeta, RUTA_GLOSARIO)
    recetas = os.path.join(carpeta, CARPETA_RECETAS)
    return {
        "carpeta": carpeta,
        "catalogo": catalogo,
        "glosario": glosario if os.path.isfile(glosario) else None,
        "recetas": recetas if os.path.isdir(recetas) else None,
    }


# ---------------------------------------------------------------------------------------------- glosario
def _leer_glosario_yaml(ruta: str) -> list[dict]:
    """Entradas del YAML: termino, sinonimos[], paso, conceptos[{concepto_api, estado}], nota. Lanza ErrorMemoria si
    falta PyYAML o el archivo no tiene el formato esperado."""
    try:
        import yaml  # dependencia del puente (pyyaml); se importa aquí para que el puente arranque aunque falte
    except ImportError as e:
        raise ErrorMemoria(
            f"Falta la biblioteca PyYAML para leer {ruta}. Instálala desde PuenteMcp con 'uv sync' "
            "(o 'pip install pyyaml' en el .venv) y reinicia el puente."
        ) from e
    try:
        with open(ruta, "r", encoding="utf-8") as f:
            datos = yaml.safe_load(f)
    except Exception as e:
        raise ErrorMemoria(f"No se pudo leer el glosario {ruta}: {e}") from e
    if not isinstance(datos, dict) or not isinstance(datos.get("terminos"), list):
        raise ErrorMemoria(f"El glosario {ruta} no tiene la clave 'terminos' con la lista de entradas.")
    entradas = []
    for t in datos["terminos"]:
        if not isinstance(t, dict) or not t.get("termino"):
            continue
        conceptos = []
        for c in t.get("conceptos") or []:
            if isinstance(c, dict) and c.get("concepto_api"):
                conceptos.append({"concepto_api": str(c["concepto_api"]), "estado": str(c.get("estado") or "por confirmar")})
            elif isinstance(c, str):
                conceptos.append({"concepto_api": c, "estado": "por confirmar"})
        entradas.append({
            "termino": str(t["termino"]),
            "sinonimos": [str(s) for s in (t.get("sinonimos") or [])],
            "paso": str(t.get("paso", "")),
            "conceptos": conceptos,
            "nota": str(t.get("nota") or ""),
        })
    return entradas


_RE_CONCEPTO_TABLA = re.compile(r"^(?P<api>[^\[\]]+?)\s*(?:\[(?P<estado>[^\]]*)\])?$")


def _leer_glosario_tabla(con: sqlite3.Connection) -> list[dict]:
    """La tabla `glosario` del catálogo (la llena catalogo/cargar_glosario.py de memoria-firma): sinonimos separados
    por '; ', concepto_api como 'Clase.Miembro [estado]; ...' y nota 'paso N · nota'. Vacía si la tabla no existe."""
    try:
        filas = con.execute("SELECT termino, sinonimos, concepto_api, nota FROM glosario").fetchall()
    except sqlite3.Error:
        return []
    entradas = []
    for termino, sinonimos, concepto_api, nota in filas:
        conceptos = []
        for trozo in (concepto_api or "").split(";"):
            trozo = trozo.strip()
            if not trozo:
                continue
            m = _RE_CONCEPTO_TABLA.match(trozo)
            if m:
                conceptos.append({"concepto_api": m.group("api").strip(), "estado": (m.group("estado") or "por confirmar").strip()})
        paso = ""
        nota = nota or ""
        m = re.match(r"^paso\s+(\S+)\s*·\s*(.*)$", nota, re.S)
        if m:
            paso, nota = m.group(1), m.group(2)
        entradas.append({
            "termino": termino,
            "sinonimos": [s.strip() for s in (sinonimos or "").split(";") if s.strip()],
            "paso": paso,
            "conceptos": conceptos,
            "nota": nota,
        })
    return entradas


def cargar_glosario(rutas: dict, con: sqlite3.Connection) -> tuple[list[dict], str]:
    """Devuelve (entradas, origen). El YAML manda; si no existe, la tabla del catálogo; si ninguno, ErrorMemoria."""
    if rutas.get("glosario"):
        return _leer_glosario_yaml(rutas["glosario"]), "glosario.yaml"
    tabla = _leer_glosario_tabla(con)
    if tabla:
        return tabla, "tabla glosario del catálogo"
    raise ErrorMemoria(
        f"Falta el glosario {os.path.join(rutas['carpeta'], RUTA_GLOSARIO)} y la tabla 'glosario' del catálogo está "
        "vacía. Actualiza memoria-firma (git pull) para tener catalogo\\glosario.yaml."
    )


def _segmentos_de_espacios(con: sqlite3.Connection) -> set[str]:
    """Trozos de los espacios de nombres del catálogo ('Autodesk', 'Civil', 'DatabaseServices'...): no se usan para
    ampliar la consulta porque aparecen en casi todas las filas."""
    try:
        filas = con.execute("SELECT nombre FROM espacios").fetchall()
    except sqlite3.Error:
        return set()
    return {parte for (nombre,) in filas for parte in (nombre or "").split(".") if parte}


def expandir_consulta(consulta: str, glosario: list[dict], excluir: set[str] | None = None) -> tuple[list[dict], list[str]]:
    """Entradas del glosario cuyo término o sinónimo aparece en la consulta, y los nombres (clase y miembro) de sus
    conceptos para ampliar la búsqueda en el catálogo (sin los trozos de espacio de nombres de `excluir`)."""
    texto = normalizar(consulta)
    excluir = excluir or set()
    coincidencias = []
    terminos_extra: list[str] = []
    for entrada in glosario:
        candidatos = [entrada["termino"]] + list(entrada["sinonimos"])
        por = next((c for c in candidatos if _contiene_palabra(texto, normalizar(c))), None)
        if por is None:
            continue
        coincidencias.append({**entrada, "coincide_por": por})
        for c in entrada["conceptos"]:
            partes = [p for p in c["concepto_api"].split(".") if p]
            # el nombre de la clase y el del miembro (los dos últimos trozos) son lo que indexa el catálogo
            for nombre in partes[-2:]:
                if nombre in excluir or nombre in terminos_extra or nombre.lower() in PALABRAS_VACIAS:
                    continue
                terminos_extra.append(nombre)
    return coincidencias, terminos_extra


# ---------------------------------------------------------------------------------------------- recetas
def buscar_recetas(carpeta: str | None, consulta: str, coincidencias: list[dict], limite: int) -> list[dict]:
    """Recetas (.md) que contienen alguna palabra útil de la consulta o un término del glosario que coincidió.
    Ordenadas por número de palabras distintas que coinciden; cada una con nombre, título y primeras líneas."""
    if not carpeta or limite <= 0:
        return []
    buscados = list(_palabras_utiles(consulta))
    for c in coincidencias:
        for t in [c["termino"]] + list(c["sinonimos"]):
            t = normalizar(t)
            if t and t not in buscados:
                buscados.append(t)
    if not buscados:
        return []
    encontradas = []
    for nombre in sorted(os.listdir(carpeta)):
        if not nombre.lower().endswith(".md"):
            continue
        ruta = os.path.join(carpeta, nombre)
        try:
            with open(ruta, "r", encoding="utf-8", errors="replace") as f:
                contenido = f.read()
        except OSError:
            continue
        texto = normalizar(nombre + "\n" + contenido)
        aciertos = sum(1 for b in buscados if _contiene_palabra(texto, b))
        if aciertos == 0:
            continue
        lineas = [l.strip() for l in contenido.splitlines() if l.strip()]
        titulo = next((l.lstrip("# ").strip() for l in lineas if l.startswith("#")), lineas[0] if lineas else nombre)
        cuerpo = [l for l in lineas if l.lstrip("# ").strip() != titulo][:3]
        encontradas.append({"archivo": nombre, "titulo": titulo, "lineas": cuerpo, "aciertos": aciertos})
    encontradas.sort(key=lambda r: (-r["aciertos"], r["archivo"]))
    return encontradas[:limite]


# ---------------------------------------------------------------------------------------------- catálogo
def _fila_tipo(con: sqlite3.Connection, tipo_id: int) -> dict | None:
    f = con.execute("SELECT id, nombre, nombre_completo, clase, tipo_base, obsoleto, mensaje_obsoleto, descripcion "
                    "FROM tipos WHERE id = ?", (tipo_id,)).fetchone()
    if f is None:
        return None
    return {"clase": "tipo", "id": f[0], "nombre": f[1], "nombre_completo": f[2], "clase_tipo": f[3], "tipo_base": f[4],
            "obsoleto": bool(f[5]), "mensaje_obsoleto": f[6] or "", "descripcion": f[7] or ""}


def _fila_metodo(con: sqlite3.Connection, metodo_id: int) -> dict | None:
    f = con.execute("SELECT m.id, m.nombre, t.nombre_completo, m.estatico, m.tipo_retorno, m.es_constructor, m.firma, "
                    "m.obsoleto, m.mensaje_obsoleto, m.descripcion FROM metodos m JOIN tipos t ON t.id = m.tipo_id "
                    "WHERE m.id = ?", (metodo_id,)).fetchone()
    if f is None:
        return None
    parametros = con.execute("SELECT nombre, tipo FROM parametros WHERE metodo_id = ? ORDER BY posicion", (metodo_id,)).fetchall()
    return {"clase": "metodo", "id": f[0], "nombre": f[1], "nombre_completo": f[2] + "." + f[1], "estatico": bool(f[3]),
            "tipo_retorno": f[4] or "", "es_constructor": bool(f[5]), "firma": f[6] or "", "obsoleto": bool(f[7]),
            "mensaje_obsoleto": f[8] or "", "descripcion": f[9] or "",
            "parametros": [{"nombre": p[0], "tipo": p[1]} for p in parametros]}


def _fila_propiedad(con: sqlite3.Connection, propiedad_id: int) -> dict | None:
    f = con.execute("SELECT p.id, p.nombre, t.nombre_completo, p.tipo_dato, p.estatico, p.lectura, p.escritura, "
                    "p.obsoleto, p.mensaje_obsoleto, p.descripcion FROM propiedades p JOIN tipos t ON t.id = p.tipo_id "
                    "WHERE p.id = ?", (propiedad_id,)).fetchone()
    if f is None:
        return None
    return {"clase": "propiedad", "id": f[0], "nombre": f[1], "nombre_completo": f[2] + "." + f[1], "tipo_dato": f[3] or "",
            "estatico": bool(f[4]), "lectura": bool(f[5]), "escritura": bool(f[6]), "obsoleto": bool(f[7]),
            "mensaje_obsoleto": f[8] or "", "descripcion": f[9] or ""}


def _fila(con: sqlite3.Connection, clase: str, ref_id: int) -> dict | None:
    if clase == "tipo":
        return _fila_tipo(con, ref_id)
    if clase == "metodo":
        return _fila_metodo(con, ref_id)
    if clase == "propiedad":
        return _fila_propiedad(con, ref_id)
    return None


def _buscar_concepto_exacto(con: sqlite3.Connection, concepto_api: str) -> list[dict]:
    """Filas del catálogo cuyo nombre completo es exactamente el concepto del glosario (clase o clase.miembro)."""
    filas = []
    for (tid,) in con.execute("SELECT id FROM tipos WHERE nombre_completo = ?", (concepto_api,)).fetchall():
        filas.append(_fila_tipo(con, tid))
    if "." in concepto_api:
        clase, miembro = concepto_api.rsplit(".", 1)
        for (mid,) in con.execute("SELECT m.id FROM metodos m JOIN tipos t ON t.id = m.tipo_id "
                                  "WHERE t.nombre_completo = ? AND m.nombre = ? ORDER BY m.id", (clase, miembro)).fetchall():
            filas.append(_fila_metodo(con, mid))
        for (pid,) in con.execute("SELECT p.id FROM propiedades p JOIN tipos t ON t.id = p.tipo_id "
                                  "WHERE t.nombre_completo = ? AND p.nombre = ? ORDER BY p.id", (clase, miembro)).fetchall():
            filas.append(_fila_propiedad(con, pid))
    return [f for f in filas if f is not None]


def _buscar_nombres_exactos(con: sqlite3.Connection, palabras: list[str]) -> list[dict]:
    """Lo que el agente nombró tal cual: tipos cuyo nombre es una palabra de la consulta (ProfilePVI) y, si además
    nombró el miembro (ProfilePVI Station o ProfilePVI.Station), ese método o propiedad. Van antes que el glosario."""
    if not palabras:
        return []
    filas = []
    tipos = {}
    for p in palabras:
        for tid, nombre in con.execute("SELECT id, nombre FROM tipos WHERE lower(nombre) = ? ORDER BY id", (p,)).fetchall():
            tipos[tid] = nombre
            filas.append(_fila_tipo(con, tid))
    for tid in tipos:
        for p in palabras:
            for (mid,) in con.execute("SELECT id FROM metodos WHERE tipo_id = ? AND lower(nombre) = ? ORDER BY id", (tid, p)).fetchall():
                filas.append(_fila_metodo(con, mid))
            for (pid,) in con.execute("SELECT id FROM propiedades WHERE tipo_id = ? AND lower(nombre) = ? ORDER BY id", (tid, p)).fetchall():
                filas.append(_fila_propiedad(con, pid))
    return [f for f in filas if f is not None]


def _consulta_fts(palabras: list[str], terminos_extra: list[str]) -> str:
    """Expresión FTS5: cada palabra entre comillas (así 'con', 'NOT' o los puntos no rompen la sintaxis), unidas con
    OR; las palabras de la consulta llevan prefijo (*) para que 'PVI' encuentre 'PVIs' o 'PVIType'."""
    trozos = []
    for p in palabras:
        limpio = p.replace('"', "")
        if limpio:
            trozos.append(f'"{limpio}"*')
    for t in terminos_extra:
        limpio = t.replace('"', "")
        if limpio:
            trozos.append(f'"{limpio}"')
    return " OR ".join(trozos)


def buscar_catalogo(con: sqlite3.Connection, consulta: str, coincidencias: list[dict], terminos_extra: list[str],
                    limite: int) -> list[dict]:
    """Primero los conceptos exactos que señaló el glosario (en su orden), luego el índice de texto con la consulta
    ampliada, sin repetir. Devuelve como mucho `limite` filas."""
    if limite <= 0:
        return []
    vistos: set[tuple[str, int]] = set()
    salida: list[dict] = []
    palabras = _palabras_utiles(consulta)
    for fila in _buscar_nombres_exactos(con, palabras):
        clave = (fila["clase"], fila["id"])
        if clave in vistos:
            continue
        vistos.add(clave)
        fila["via"] = "nombre exacto"
        salida.append(fila)
        if len(salida) >= limite:
            return salida
    for c in coincidencias:
        for concepto in c["conceptos"]:
            for fila in _buscar_concepto_exacto(con, concepto["concepto_api"]):
                clave = (fila["clase"], fila["id"])
                if clave in vistos:
                    continue
                vistos.add(clave)
                fila["via"] = f"glosario: {c['termino']}"
                salida.append(fila)
                if len(salida) >= limite:
                    return salida
    expresion = _consulta_fts(palabras, terminos_extra)
    if not expresion:
        return salida
    try:
        filas = con.execute("SELECT clase, ref_id FROM busqueda WHERE busqueda MATCH ? ORDER BY rank LIMIT ?",
                            (expresion, limite + len(vistos))).fetchall()
    except sqlite3.Error:
        return salida
    for clase, ref_id in filas:
        clave = (clase, int(ref_id))
        if clave in vistos:
            continue
        fila = _fila(con, clase, int(ref_id))
        if fila is None:
            continue
        vistos.add(clave)
        fila["via"] = "índice de texto"
        salida.append(fila)
        if len(salida) >= limite:
            break
    return salida


# ---------------------------------------------------------------------------------------------- texto de salida
def _formatear_fila(fila: dict) -> list[str]:
    lineas = []
    if fila["clase"] == "tipo":
        extra = f" (base: {_nombre_corto(fila['tipo_base'])})" if fila.get("tipo_base") else ""
        lineas.append(f"- {fila['clase_tipo']} {fila['nombre_completo']}{extra}")
    elif fila["clase"] == "metodo":
        marcas = []
        if fila["estatico"]:
            marcas.append("estático")
        if fila["es_constructor"]:
            marcas.append("constructor")
        retorno = f" → {_nombre_corto(fila['tipo_retorno'])}" if fila.get("tipo_retorno") else ""
        sufijo = f" [{', '.join(marcas)}]" if marcas else ""
        lineas.append(f"- método {fila['nombre_completo']}{retorno}{sufijo}")
        if fila.get("firma"):
            lineas.append(f"    firma: {fila['firma']}")
        if fila.get("parametros"):
            lineas.append("    parámetros: " + ", ".join(f"{p['nombre']}: {_nombre_corto(p['tipo'])}" for p in fila["parametros"]))
        else:
            lineas.append("    parámetros: ninguno")
    else:
        acceso = "/".join(a for a, si in (("lectura", fila["lectura"]), ("escritura", fila["escritura"])) if si) or "sin acceso"
        estatico = ", estática" if fila["estatico"] else ""
        lineas.append(f"- propiedad {fila['nombre_completo']}: {_nombre_corto(fila['tipo_dato'])} ({acceso}{estatico})")
    if fila.get("descripcion"):
        lineas.append(f"    descripción: {fila['descripcion'].strip().splitlines()[0]}")
    if fila.get("obsoleto"):
        mensaje = f": {fila['mensaje_obsoleto']}" if fila.get("mensaje_obsoleto") else ""
        lineas.append(f"    OBSOLETO{mensaje}")
    return lineas


def formatear(consulta: str, limite: int, rutas: dict, origen_glosario: str, recetas: list[dict],
              coincidencias: list[dict], terminos_extra: list[str], catalogo: list[dict]) -> str:
    total = len(recetas) + len(coincidencias) + len(catalogo)
    lineas = [f"buscar_api \"{consulta}\" → {len(recetas)} recetas, {len(coincidencias)} entradas del glosario, "
              f"{len(catalogo)} del catálogo (límite {limite}). Memoria: {rutas['carpeta']}"]
    if terminos_extra:
        lineas.append("Consulta ampliada con el glosario: " + ", ".join(terminos_extra))
    if total == 0:
        lineas.append(f"Sin resultados para \"{consulta}\". Prueba con el nombre inglés de la clase (por ejemplo "
                      "TinSurface, Profile, Corridor) o con una palabra del glosario; con más de una palabra se "
                      "busca cualquiera de ellas.")
        return "\n".join(lineas)
    if recetas:
        lineas.append("")
        lineas.append(f"RECETAS ({len(recetas)}), en {os.path.join(rutas['carpeta'], CARPETA_RECETAS)}:")
        for r in recetas:
            lineas.append(f"- {r['archivo']}: {r['titulo']}")
            for l in r["lineas"]:
                lineas.append(f"    {l[:160]}")
    if coincidencias:
        lineas.append("")
        lineas.append(f"GLOSARIO ({len(coincidencias)}, de {origen_glosario}):")
        for c in coincidencias:
            paso = f", paso {c['paso']}" if c.get("paso") else ""
            sinonimos = ", ".join(c["sinonimos"]) if c["sinonimos"] else "—"
            lineas.append(f"- {c['termino']} (coincide por \"{c['coincide_por']}\"{paso}; sinónimos: {sinonimos})")
            for concepto in c["conceptos"]:
                lineas.append(f"    {concepto['concepto_api']} [{concepto['estado']}]")
            if c.get("nota"):
                lineas.append(f"    nota: {c['nota']}")
    if catalogo:
        lineas.append("")
        lineas.append(f"CATÁLOGO ({len(catalogo)}; primero los nombres exactos de la consulta, luego los conceptos del "
                      "glosario y después el índice de texto):")
        for fila in catalogo:
            lineas.extend(_formatear_fila(fila))
    elif total < limite and not coincidencias:
        lineas.append("")
        lineas.append("CATÁLOGO: nada con esas palabras. Prueba con el nombre inglés de la clase (TinSurface, Profile, Corridor).")
    return "\n".join(lineas)


# ---------------------------------------------------------------------------------------------- entrada principal
def _limite_valido(limite) -> int:
    try:
        n = int(limite)
    except (TypeError, ValueError):
        n = LIMITE_POR_DEFECTO
    return max(1, min(n, LIMITE_MAXIMO))


def buscar(consulta: str, limite: int = LIMITE_POR_DEFECTO, memoria: str | None = None) -> str:
    """La herramienta: devuelve siempre un texto (resultados o un error claro), nunca lanza."""
    consulta = (consulta or "").strip()
    limite = _limite_valido(limite)
    if not consulta:
        return "Error: la consulta está vacía. Escribe qué buscas, por ejemplo \"rasante con PVI\" o \"TinSurface\"."
    try:
        rutas = localizar_memoria(memoria)
    except ErrorMemoria as e:
        return f"Error: {e}"
    try:
        con = sqlite3.connect(f"file:{rutas['catalogo']}?mode=ro", uri=True)
    except sqlite3.Error as e:
        return f"Error: no se pudo abrir el catálogo {rutas['catalogo']}: {e}"
    try:
        try:
            glosario, origen_glosario = cargar_glosario(rutas, con)
        except ErrorMemoria as e:
            return f"Error: {e}"
        coincidencias, terminos_extra = expandir_consulta(consulta, glosario, _segmentos_de_espacios(con))
        recetas = buscar_recetas(rutas["recetas"], consulta, coincidencias, limite)
        restante = limite - len(recetas)
        coincidencias = coincidencias[:max(0, restante)]
        restante -= len(coincidencias)
        try:
            catalogo = buscar_catalogo(con, consulta, coincidencias, terminos_extra, restante)
        except sqlite3.Error as e:
            return f"Error: el catálogo {rutas['catalogo']} no tiene el esquema esperado (tipos, metodos, propiedades, busqueda): {e}"
        return formatear(consulta, limite, rutas, origen_glosario, recetas, coincidencias, terminos_extra, catalogo)
    except Exception as e:  # último recurso: nunca una excepción hacia el agente
        return f"Error inesperado en buscar_api: {type(e).__name__}: {e}"
    finally:
        con.close()


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="buscar_api",
                                description="Busca en la memoria técnica (recetas, glosario y catálogo de la API de Civil 3D 2027).")
    p.add_argument("consulta", help='qué buscar, por ejemplo "rasante con PVI"')
    p.add_argument("--limite", type=int, default=LIMITE_POR_DEFECTO, help=f"máximo de resultados (por defecto {LIMITE_POR_DEFECTO})")
    p.add_argument("--memoria", default=None, help=f"carpeta de memoria-firma (si no, la variable {VARIABLE_ENTORNO})")
    args = p.parse_args(argv)
    texto = buscar(args.consulta, args.limite, args.memoria)
    print(texto)
    return 2 if texto.startswith("Error:") else 0


if __name__ == "__main__":
    sys.exit(main())

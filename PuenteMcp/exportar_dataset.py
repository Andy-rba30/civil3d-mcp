# -*- coding: utf-8 -*-
"""exportar_dataset (1.6.0): convierte los registros mcp_log.jsonl del plugin en un dataset de pares pedido → herramientas.

Solo entran las llamadas que el usuario **aprobó** (líneas de tipo "aprobacion" con aprobado=true, enlazadas por
id_log con la línea de la llamada). Las llamadas aprobadas consecutivas con el mismo `pedido` se agrupan en un solo
par. Cada línea de salida (pares.jsonl) es un JSON con: pedido, herramientas (lista ordenada de {nombre, args}),
resultado ({ok, resumen}), fecha y version.

Anonimiza antes de escribir: rutas de Windows, UNC y Linux → "<ruta>"; nombres de archivo .dwg → "<dibujo>"; y cada
nombre de la lista de prohibidos (por defecto MEMORIA_FIRMA_DIR/casos/PROHIBIDOS.txt, una entrada por línea) →
"<proyecto>". Salida por defecto: MEMORIA_FIRMA_DIR/dataset/pares.jsonl.

Uso, desde PuenteMcp:
    uv run --frozen python exportar_dataset.py --logs <carpeta o archivo> [--salida <archivo>] [--prohibidos <archivo>]
    python -m exportar_dataset --logs C:\\Proyectos\\dibujos_prueba
Código de salida 0 si exportó (aunque no haya pares), 2 si falta algo (carpeta, salida o variable de entorno).
"""
import argparse
import json
import os
import re
import sys

VARIABLE_ENTORNO = "MEMORIA_FIRMA_DIR"
NOMBRE_LOG = "mcp_log.jsonl"
SALIDA_RELATIVA = os.path.join("dataset", "pares.jsonl")
PROHIBIDOS_RELATIVO = os.path.join("casos", "PROHIBIDOS.txt")
TIPO_LLAMADA = "llamada"
TIPO_APROBACION = "aprobacion"
MARCA_RUTA = "<ruta>"
MARCA_DIBUJO = "<dibujo>"
MARCA_PROYECTO = "<proyecto>"
LARGO_RESUMEN = 160

# Rutas: Windows (C:\a b\c.dwg o C:/a/b), UNC (\\servidor\carpeta\x) y Linux (/home/user/x). Los directorios
# intermedios admiten espacios; el último tramo termina en el primer espacio para no comerse la frase que sigue.
_SEG = r'[^\\/:*?"<>|\r\n]*?'
_FIN = r'[^\\/:*?"<>|\s]*'
_RE_RUTAS = re.compile(
    r"(?:[A-Za-z]:[\\/](?:" + _SEG + r"[\\/])*" + _FIN + r")"      # C:\... o C:/...
    r"|(?:\\\\(?:" + _SEG + r"\\)+" + _FIN + r")"                   # \\servidor\carpeta\...
    r"|(?:(?<![\w:./])/(?:[^/\s\"']+/)+[^/\s\"']*)"                 # /home/user/... (al menos dos tramos)
)
_RE_DWG = re.compile(r"[^\s\\/\"'<>|:]+\.dwg\b", re.IGNORECASE)


# ---------------------------------------------------------------------------------------------- lectura
def buscar_logs(ruta: str) -> list:
    """Archivos mcp_log.jsonl: el propio archivo si `ruta` es un archivo, o todos los de la carpeta (recursivo), ordenados."""
    if os.path.isfile(ruta):
        return [ruta]
    if not os.path.isdir(ruta):
        raise FileNotFoundError(f"No existe la carpeta ni el archivo de registros: {ruta}")
    encontrados = []
    for carpeta, _subcarpetas, archivos in os.walk(ruta):
        for nombre in archivos:
            if nombre.lower() == NOMBRE_LOG:
                encontrados.append(os.path.join(carpeta, nombre))
    return sorted(encontrados)


def leer_lineas(ruta: str) -> list:
    """Líneas JSON de un mcp_log.jsonl como diccionarios, en orden; las vacías o rotas se saltan."""
    lineas = []
    with open(ruta, "r", encoding="utf-8-sig") as f:
        for texto in f:
            texto = texto.strip()
            if not texto:
                continue
            try:
                dato = json.loads(texto)
            except ValueError:
                continue
            if isinstance(dato, dict):
                lineas.append(dato)
    return lineas


def leer_prohibidos(ruta) -> list:
    """Nombres a sustituir por <proyecto>: una entrada por línea; se ignoran las vacías y las que empiezan por #."""
    if not ruta or not os.path.isfile(ruta):
        return []
    nombres = []
    with open(ruta, "r", encoding="utf-8-sig") as f:
        for linea in f:
            linea = linea.strip()
            if linea and not linea.startswith("#"):
                nombres.append(linea)
    # Los más largos primero: "Carretera Norte Tramo 2" antes que "Carretera Norte"
    return sorted(set(nombres), key=len, reverse=True)


# ---------------------------------------------------------------------------------------------- anonimización
def anonimizar_texto(texto: str, prohibidos: list) -> str:
    """Sustituye rutas, luego nombres .dwg y por último los nombres prohibidos (sin distinguir mayúsculas). Las rutas
    van primero para que una carpeta con nombre de obra desaparezca entera como <ruta>."""
    if not isinstance(texto, str) or not texto:
        return texto
    texto = _RE_RUTAS.sub(MARCA_RUTA, texto)
    texto = _RE_DWG.sub(MARCA_DIBUJO, texto)
    for nombre in prohibidos:
        texto = re.sub(re.escape(nombre), MARCA_PROYECTO, texto, flags=re.IGNORECASE)
    return texto


def anonimizar(valor, prohibidos: list):
    """Aplica anonimizar_texto a todos los textos de un valor JSON (recursivo; las claves se conservan)."""
    if isinstance(valor, str):
        return anonimizar_texto(valor, prohibidos)
    if isinstance(valor, list):
        return [anonimizar(v, prohibidos) for v in valor]
    if isinstance(valor, dict):
        return {k: anonimizar(v, prohibidos) for k, v in valor.items()}
    return valor


# ---------------------------------------------------------------------------------------------- pares
def _es_llamada(linea: dict) -> bool:
    tipo = linea.get("tipo")
    return (tipo == TIPO_LLAMADA or tipo is None) and bool(linea.get("id")) and bool(linea.get("herramienta"))


def aprobaciones(lineas_por_archivo: list) -> dict:
    """id_log → aprobado (true/false) según la última aprobación registrada para cada llamada."""
    estado = {}
    for _ruta, lineas in lineas_por_archivo:
        for linea in lineas:
            if linea.get("tipo") == TIPO_APROBACION and linea.get("id_log"):
                estado[linea["id_log"]] = bool(linea.get("aprobado"))
    return estado


def llamadas_aprobadas(lineas: list, aprobadas: dict) -> list:
    """Las llamadas de un archivo cuyo id tiene aprobado=true, en el orden del archivo y sin repetir ids."""
    vistos = set()
    salida = []
    for linea in lineas:
        if not _es_llamada(linea):
            continue
        id_ = linea["id"]
        if id_ in vistos or not aprobadas.get(id_):
            continue
        vistos.add(id_)
        salida.append(linea)
    return salida


def _args_sin_pedido(args) -> dict:
    if not isinstance(args, dict):
        return {}
    return {k: v for k, v in args.items() if k != "pedido"}


def _pedido(linea: dict) -> str:
    pedido = linea.get("pedido")
    if not pedido and isinstance(linea.get("args"), dict):
        pedido = linea["args"].get("pedido")
    return (pedido or "").strip()


def agrupar(llamadas: list) -> list:
    """Grupos de llamadas consecutivas con el mismo pedido (no vacío). Sin pedido, cada llamada es su propio grupo."""
    grupos = []
    for llamada in llamadas:
        pedido = _pedido(llamada)
        if grupos and pedido and _pedido(grupos[-1][0]) == pedido:
            grupos[-1].append(llamada)
        else:
            grupos.append([llamada])
    return grupos


def resumen(grupo: list) -> dict:
    """ok si todas las llamadas del grupo fueron ok; resumen corto con el primer error si lo hubo."""
    n = len(grupo)
    errores = [l for l in grupo if not l.get("ok")]
    if not errores:
        texto = "1 llamada correcta" if n == 1 else f"{n} llamadas correctas"
        return {"ok": True, "resumen": texto}
    primero = (errores[0].get("error") or "sin detalle").replace("\r", " ").replace("\n", " ")
    if len(primero) > LARGO_RESUMEN:
        primero = primero[: LARGO_RESUMEN - 1] + "…"
    texto = f"{len(errores)} de {n} con error: {primero}"
    return {"ok": False, "resumen": texto}


def par(grupo: list, prohibidos: list) -> dict:
    """Un par del dataset (ya anonimizado) a partir de un grupo de llamadas aprobadas."""
    primera = grupo[0]
    hora = primera.get("hora") or ""
    version = next((l.get("version") for l in grupo if l.get("version")), None)
    crudo = {
        "pedido": _pedido(primera),
        "herramientas": [{"nombre": l.get("herramienta"), "args": _args_sin_pedido(l.get("args"))} for l in grupo],
        "resultado": resumen(grupo),
        "fecha": hora[:10] if hora else None,
        "version": version,
    }
    return anonimizar(crudo, prohibidos)


def exportar(ruta_logs: str, ruta_salida: str, ruta_prohibidos=None) -> dict:
    """Lee los registros, filtra, agrupa, anonimiza y escribe pares.jsonl. Devuelve los recuentos."""
    archivos = buscar_logs(ruta_logs)
    prohibidos = leer_prohibidos(ruta_prohibidos)
    lineas_por_archivo = [(ruta, leer_lineas(ruta)) for ruta in archivos]
    aprobadas = aprobaciones(lineas_por_archivo)
    pares = []
    n_lineas = 0
    n_aprobadas = 0
    for _ruta, lineas in lineas_por_archivo:
        n_lineas += len(lineas)
        llamadas = llamadas_aprobadas(lineas, aprobadas)
        n_aprobadas += len(llamadas)
        for grupo in agrupar(llamadas):
            pares.append(par(grupo, prohibidos))
    carpeta = os.path.dirname(os.path.abspath(ruta_salida))
    os.makedirs(carpeta, exist_ok=True)
    with open(ruta_salida, "w", encoding="utf-8", newline="\n") as f:
        for p in pares:
            f.write(json.dumps(p, ensure_ascii=False) + "\n")
    return {
        "archivos": len(archivos),
        "lineas": n_lineas,
        "aprobadas": n_aprobadas,
        "pares": len(pares),
        "prohibidos": len(prohibidos),
        "salida": os.path.abspath(ruta_salida),
    }


# ---------------------------------------------------------------------------------------------- línea de comandos
def _ruta_por_defecto(relativa: str, entorno=None):
    base = (entorno if entorno is not None else os.environ).get(VARIABLE_ENTORNO)
    if not base:
        return None
    return os.path.join(base, relativa)


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(prog="exportar_dataset", description="Exporta las llamadas aprobadas de mcp_log.jsonl como pares pedido → herramientas (pares.jsonl), anonimizados.")
    parser.add_argument("--logs", required=True, help="Carpeta (se buscan mcp_log.jsonl de forma recursiva) o un archivo mcp_log.jsonl")
    parser.add_argument("--salida", help=f"Archivo de salida (por defecto %%{VARIABLE_ENTORNO}%%\\dataset\\pares.jsonl)")
    parser.add_argument("--prohibidos", help=f"Lista de nombres a sustituir por {MARCA_PROYECTO}, uno por línea (por defecto %%{VARIABLE_ENTORNO}%%\\casos\\PROHIBIDOS.txt si existe)")
    args = parser.parse_args(argv)

    salida = args.salida or _ruta_por_defecto(SALIDA_RELATIVA)
    if not salida:
        print(f"Error: no se indicó --salida y la variable de entorno {VARIABLE_ENTORNO} no está puesta. "
              f"Pon {VARIABLE_ENTORNO} apuntando a la carpeta de memoria-firma o pasa --salida <archivo>.")
        return 2
    prohibidos = args.prohibidos or _ruta_por_defecto(PROHIBIDOS_RELATIVO)
    if args.prohibidos and not os.path.isfile(args.prohibidos):
        print(f"Error: no existe el archivo de prohibidos {args.prohibidos}.")
        return 2
    try:
        r = exportar(args.logs, salida, prohibidos)
    except FileNotFoundError as e:
        print(f"Error: {e}")
        return 2
    aviso = "" if r["prohibidos"] else " (sin lista de prohibidos: solo se anonimizan rutas y nombres .dwg)"
    print(f"exportar_dataset: {r['lineas']} líneas de {r['archivos']} archivo(s) mcp_log.jsonl, "
          f"{r['aprobadas']} llamadas aprobadas, {r['pares']} pares → {r['salida']}{aviso}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

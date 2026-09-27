"""
Pruebas del servidor local de ArbaMcp (con Civil 3D abierto y el plugin cargado).

  python probar_servidor.py                 -> seguridad HTTP, ejecutar_comando (como en la versión 1.1) y
                                               "Civil 3D ocupado" (1.2.2: con _.LINE activo, ping responde y las
                                               herramientas de dibujo esperan y se descartan sin ejecutarse)
  python probar_servidor.py --dwg RUTA.dwg  -> además abre ese dibujo (debe tener al menos un corredor) y comprueba:
      1. todas las herramientas de lectura responden ok=true en menos de 5 s
      2. asignar_objetivo con simular=true no cambia nada (listar_objetivos idéntico antes y después)
      3. asignar_objetivo real se refleja en listar_objetivos y crea un archivo en backups\\
      4. ejecutar_comando sin undo no envía _.UNDO
      5. mcp_log.jsonl recibe una línea por cada llamada de escritura
  --sin-escritura  omite las pruebas 3 y 5 (no toca el dibujo).
"""
import argparse
import json
import os
import sys
import threading
import time

import httpx

# La consola de Windows (cp1252) no puede imprimir algunos caracteres del historial del plugin
for _flujo in (sys.stdout, sys.stderr):
    try:
        _flujo.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass

PUERTO = int(os.environ.get("ARBA_MCP_PORT", 8765))
BASE_URL = f"http://127.0.0.1:{PUERTO}"
LIMITE_LECTURA_S = 5.0

token_path = os.path.expandvars(r"%LOCALAPPDATA%\ArbaMcp\token")
try:
    with open(token_path, "r", encoding="utf-8") as f:
        TOKEN = f.read().strip()
except Exception as e:
    print(f"Error reading token: {e}")
    sys.exit(1)

CABECERAS = {"X-Arba-Token": TOKEN, "Content-Type": "application/json"}
fallos = []


def resultado(nombre, ok, detalle=""):
    print(f"  [{'OK ' if ok else 'FAL'}] {nombre}" + (f"  -> {detalle}" if detalle else ""))
    if not ok:
        fallos.append(nombre)


def llamar(tool, args=None, timeout_s=120):
    """Devuelve (ok, result_o_error, segundos)."""
    t0 = time.perf_counter()
    try:
        res = httpx.post(f"{BASE_URL}/execute", headers=CABECERAS,
                         json={"tool": tool, "args": args or {}, "timeout_s": timeout_s}, timeout=timeout_s + 10)
        datos = res.json()
    except Exception as e:
        return False, f"excepción: {e}", time.perf_counter() - t0
    seg = time.perf_counter() - t0
    if datos.get("ok"):
        return True, datos.get("result"), seg
    return False, datos.get("error"), seg


def test_req(name, method, endpoint, headers, json=None, timeout=120):
    try:
        print(f"\n[TEST] {name}")
        res = httpx.request(method, f"{BASE_URL}{endpoint}", headers=headers, json=json, timeout=timeout)
        print(f"Status: {res.status_code}")
        print(f"Body: {res.text[:400]}")
        return res
    except Exception as e:
        print(f"Exception: {e}")
        return None


# ---------------------------------------------------------------- seguridad HTTP (versión 1.1)
def pruebas_seguridad():
    print(f"--- Token actual: {TOKEN[:8]}... ---")
    r = test_req("Sin token (401)", "GET", "/ping", {})
    resultado("sin token responde 401", r is not None and r.status_code == 401)
    r = test_req("Con token (200)", "GET", "/ping", {"X-Arba-Token": TOKEN})
    resultado("con token responde 200", r is not None and r.status_code == 200)
    r = test_req("Con Origin (403)", "GET", "/ping", {"X-Arba-Token": TOKEN, "Origin": "http://localhost:3000"})
    resultado("con Origin responde 403", r is not None and r.status_code == 403)
    try:
        print("\n[TEST] POST sin Content-Type (415)")
        res = httpx.post(f"{BASE_URL}/execute", content="{}", headers={"X-Arba-Token": TOKEN, "Content-Type": "text/plain"}, timeout=5)
        print(f"Status: {res.status_code}")
        resultado("POST sin JSON responde 415", res.status_code == 415)
    except Exception as e:
        resultado("POST sin JSON responde 415", False, str(e))


def pruebas_comando():
    ok, r, _ = llamar("ejecutar_comando", {"comando": "REGEN"})
    resultado("ejecutar_comando REGEN terminado", ok and r == "terminado", str(r))
    ok, r, _ = llamar("ejecutar_comando", {"comando": "COMANDOINEXISTENTE123", "timeout_s": 5}, timeout_s=10)
    resultado("comando inexistente: no se inició", ok and "no se inició" in str(r), str(r))
    ok, r, _ = llamar("ejecutar_comando", {"comando": "_.LINE", "timeout_s": 5}, timeout_s=10)
    resultado("_.LINE: timeout con ESC", ok and r == "timeout con ESC", str(r))


def pruebas_ocupado():
    """Con un comando activo (_.LINE esperando un punto): ping y leer_variable responden igualmente (contexto
    Inmediato), una herramienta de dibujo espera y se descarta al agotar su tiempo sin ejecutarse, y el comando
    termina con 'timeout con ESC' (los ESC llegan al hilo principal aunque el comando esté esperando entrada)."""
    print("\n--- Civil 3D ocupado (comando _.LINE activo) ---")
    respuesta_linea = {}

    def linea():
        respuesta_linea["r"] = llamar("ejecutar_comando", {"comando": "_.LINE", "timeout_s": 12}, timeout_s=20)

    hilo = threading.Thread(target=linea, daemon=True)
    hilo.start()
    time.sleep(3)  # tiempo de sobra para que _.LINE arranque y quede esperando un punto

    ok, r, seg = llamar("ping", timeout_s=5)
    resultado(f"ping responde con comando activo ({seg:.2f} s)", ok and seg < 3, str(r)[:120])
    ok, r, seg = llamar("leer_variable", {"nombre": "CMDACTIVE"}, timeout_s=5)
    valor = r.get("valor") if isinstance(r, dict) else None
    resultado("leer_variable CMDACTIVE > 0 durante el comando", ok and valor not in (None, "0"), str(r)[:120])
    ok, r, seg = llamar("listar_alineamientos", timeout_s=3)
    resultado(f"listar_alineamientos espera y se descarta al agotar el tiempo ({seg:.1f} s)", (not ok) and "descartado" in str(r), str(r)[:160])

    hilo.join(40)
    r = respuesta_linea.get("r")
    resultado("_.LINE termina con 'timeout con ESC'", r is not None and r[0] and r[1] == "timeout con ESC", str(r)[:120])
    ok, r, seg = llamar("listar_alineamientos", timeout_s=30)
    resultado(f"listar_alineamientos vuelve a responder tras el ESC ({seg:.2f} s)", ok, str(r)[:120])
    ok, hist, _ = llamar("leer_historial", {"ultimas_n": 60})
    resultado("el historial anota la espera de listar_alineamientos (linea MCP espera)", any("listar_alineamientos espera:" in l for l in (hist or [])))


# ---------------------------------------------------------------- pruebas con dibujo
def lectura(nombre, args=None, acepta_error=None):
    """Comprueba ok=true en menos de 5 s. acepta_error: texto de error que se considera respuesta válida."""
    ok, r, seg = llamar(nombre, args, timeout_s=30)
    if not ok and acepta_error and acepta_error in str(r):
        ok = True
    resultado(f"lectura {nombre} ({seg:.2f} s)", ok and seg < LIMITE_LECTURA_S, "" if ok else str(r))
    return r if ok else None


def ruta_log(dwg):
    return os.path.join(os.path.dirname(os.path.abspath(dwg)), "mcp_log.jsonl")


def carpeta_backups(dwg):
    return os.path.join(os.path.dirname(os.path.abspath(dwg)), "backups")


def lineas_log(dwg):
    try:
        with open(ruta_log(dwg), "r", encoding="utf-8") as f:
            return [l for l in f.read().splitlines() if l.strip()]
    except FileNotFoundError:
        return []


def archivos_backups(dwg):
    try:
        return set(os.listdir(carpeta_backups(dwg)))
    except FileNotFoundError:
        return set()


def pruebas_dibujo(dwg, con_escritura):
    print(f"\n=== Dibujo de prueba: {dwg} ===")
    ok, r, _ = llamar("abrir_dibujo", {"ruta": dwg})
    resultado("abrir_dibujo", ok, str(r))
    if not ok:
        return

    # ---- 1. herramientas de lectura
    print("\n--- 1. Herramientas de lectura (< 5 s, ok=true) ---")
    lectura("ping")
    alineamientos = lectura("listar_alineamientos") or []
    superficies = lectura("listar_superficies") or []
    corredores = lectura("listar_corredores") or []
    lectura("listar_ensamblajes")
    lectura("listar_intersecciones")
    lectura("listar_lineas_muestreo")
    lectura("leer_historial", {"ultimas_n": 5})
    lectura("leer_log", {"ultimas_n": 5})
    lectura("leer_variable", {"nombre": "DWGNAME"})
    resultado("el dibujo tiene al menos un corredor", len(corredores) > 0)
    if not corredores:
        return

    cor = corredores[0]
    lectura("estado_corredor", {"corredor": cor["nombre"]})
    regiones = lectura("listar_regiones", {"corredor": cor["nombre"]}) or []
    resultado("el corredor tiene regiones", len(regiones) > 0)
    objetivos = []
    if regiones:
        reg = regiones[0]
        objetivos = lectura("listar_objetivos", {"corredor": cor["nombre"], "linea_base": reg["linea_base"], "region": reg["nombre"]}) or []

    if alineamientos:
        al = alineamientos[0]
        pk = (al["inicio"] + al["fin"]) / 2
        punto = lectura("pk_a_punto", {"alineamiento": al["nombre"], "pk": pk, "perfil": (al.get("perfiles") or [None])[0]}) or {}
        if punto:
            lectura("punto_a_pk", {"alineamiento": al["nombre"], "x": punto["x"], "y": punto["y"]})
            if superficies:
                lectura("cota_superficie", {"superficie": superficies[0]["nombre"], "x": punto["x"], "y": punto["y"]}, acepta_error="fuera de la superficie")
            lectura("listar_perfiles", {"alineamiento": al["nombre"]})
            if al.get("perfiles"):
                lectura("listar_pvis", {"alineamiento": al["nombre"], "perfil": al["perfiles"][0]})
    if len(alineamientos) >= 2:
        lectura("interseccion_ejes", {"alineamiento_a": alineamientos[0]["nombre"], "alineamiento_b": alineamientos[1]["nombre"]})

    # ---- 2. simular no cambia nada
    print("\n--- 2. asignar_objetivo con simular=true ---")
    obj_sup = next((o for o in objetivos if o.get("tipo") == "superficie"), None)
    resultado("hay un objetivo de superficie en la primera región", obj_sup is not None)
    if obj_sup is None or not superficies:
        return
    reg = regiones[0]
    base = {"corredor": cor["nombre"], "linea_base": reg["linea_base"], "region": reg["nombre"], "subensamblaje": obj_sup["subensamblaje"], "tipo": "superficie"}
    if obj_sup.get("grupo"):
        base["grupo"] = obj_sup["grupo"]
    if obj_sup.get("parametro"):
        base["parametro"] = obj_sup["parametro"]
    actual = (obj_sup.get("objetivos") or [{}])
    nombre_actual = actual[0].get("nombre") if actual else None
    otra = next((s["nombre"] for s in superficies if s["nombre"] != nombre_actual), None)
    destino_sim = otra or superficies[0]["nombre"]

    log_antes = len(lineas_log(dwg))
    antes = json.dumps(objetivos, sort_keys=True)
    ok, r, _ = llamar("asignar_objetivo", dict(base, objetivo=destino_sim, simular=True))
    resultado("asignar_objetivo simular=true responde ok", ok and isinstance(r, dict) and r.get("simulado") is True, str(r)[:300])
    despues = json.dumps(llamar("listar_objetivos", {"corredor": cor["nombre"], "linea_base": reg["linea_base"], "region": reg["nombre"]})[1], sort_keys=True)
    resultado("listar_objetivos idéntico tras simular", antes == despues)
    resultado("mcp_log.jsonl registra la simulación", len(lineas_log(dwg)) == log_antes + 1, f"{log_antes} -> {len(lineas_log(dwg))}")

    if not con_escritura:
        print("\n(--sin-escritura: se omiten las pruebas 3 y 5)")
        pruebas_undo()
        return

    # ---- 3. escritura real + copia de seguridad
    print("\n--- 3. asignar_objetivo real ---")
    destino_real = otra if otra else "ninguno"
    backups_antes = archivos_backups(dwg)
    log_antes = len(lineas_log(dwg))
    ok, r, _ = llamar("asignar_objetivo", dict(base, objetivo=destino_real))
    resultado("asignar_objetivo real responde ok", ok, str(r)[:300])
    nuevos = list(llamar("listar_objetivos", {"corredor": cor["nombre"], "linea_base": reg["linea_base"], "region": reg["nombre"]})[1] or [])
    obj_nuevo = next((o for o in nuevos if o.get("subensamblaje") == obj_sup["subensamblaje"] and o.get("tipo") == "superficie" and o.get("parametro") == obj_sup.get("parametro")), None)
    nombres_nuevos = [o.get("nombre") for o in (obj_nuevo or {}).get("objetivos", [])]
    esperado = [] if destino_real == "ninguno" else [destino_real]
    resultado("listar_objetivos refleja el cambio", nombres_nuevos == esperado, f"{nombres_nuevos} (esperado {esperado})")
    nuevos_backups = archivos_backups(dwg) - backups_antes
    resultado("se creó un archivo en backups\\", len(nuevos_backups) >= 1, ", ".join(sorted(nuevos_backups)))

    # ---- 5. una línea de log por escritura
    n_escrituras = 1
    # restaurar el objetivo original (otra escritura)
    ok2, r2, _ = llamar("asignar_objetivo", dict(base, objetivo=nombre_actual or "ninguno"))
    resultado("restaurar el objetivo original", ok2, str(r2)[:300])
    n_escrituras += 1
    lineas = lineas_log(dwg)
    resultado("mcp_log.jsonl: una línea por escritura", len(lineas) == log_antes + n_escrituras, f"{log_antes} -> {len(lineas)} (esperado +{n_escrituras})")
    try:
        ultima = json.loads(lineas[-1])
        resultado("última línea del log tiene hora/herramienta/args/ok/ms", all(k in ultima for k in ("hora", "herramienta", "args", "ok", "ms")), str(ultima)[:200])
    except Exception as e:
        resultado("última línea del log es JSON", False, str(e))

    pruebas_undo()


def pruebas_undo():
    # ---- 4. ejecutar_comando sin undo no envía _.UNDO
    print("\n--- 4. ejecutar_comando sin undo ---")
    marca = f"MCP → ejecutar_comando"
    ok, r, _ = llamar("ejecutar_comando", {"comando": "REGEN"})
    resultado("REGEN terminado", ok and r == "terminado", str(r))
    ok, hist, _ = llamar("leer_historial", {"ultimas_n": 40})
    hist = hist or []
    idx = max((i for i, l in enumerate(hist) if marca in l), default=-1)
    tras = hist[idx:] if idx >= 0 else hist
    sin_undo = not any("Comando inicia: UNDO" in l or "Comando termina: UNDO" in l for l in tras)
    resultado("sin undo: el historial no muestra el comando UNDO", sin_undo, " | ".join(l for l in tras if "UNDO" in l)[:300])
    ok, r, _ = llamar("ejecutar_comando", {"comando": "REGEN", "undo": True})
    ok, hist, _ = llamar("leer_historial", {"ultimas_n": 40})
    hist = hist or []
    idx = max((i for i, l in enumerate(hist) if marca in l), default=-1)
    tras = hist[idx:] if idx >= 0 else hist
    resultado("con undo=true: el historial sí muestra UNDO", any("UNDO" in l for l in tras))


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description="Pruebas del servidor ArbaMcp")
    ap.add_argument("--dwg", help="Ruta de un DWG de prueba con al menos un corredor")
    ap.add_argument("--sin-escritura", action="store_true", help="No modificar el dibujo (omite las pruebas 3 y 5)")
    ap.add_argument("--solo-dibujo", action="store_true", help="Omitir las pruebas de seguridad HTTP y de ejecutar_comando")
    args = ap.parse_args()

    if not args.solo_dibujo:
        pruebas_seguridad()
        print("\n--- ejecutar_comando ---")
        pruebas_comando()
        pruebas_ocupado()
    if args.dwg:
        pruebas_dibujo(args.dwg, not args.sin_escritura)

    print("\n=== Resultado: " + ("TODO OK" if not fallos else f"{len(fallos)} fallo(s): " + "; ".join(fallos)) + " ===")
    sys.exit(0 if not fallos else 1)

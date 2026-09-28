"""
Pruebas del servidor local de ArbaMcp (con Civil 3D abierto y el plugin cargado).

  python probar_servidor.py                 -> seguridad HTTP (desde 1.3.0 GET /ping es público y el token se exige
                                               en el resto de rutas), ejecutar_comando (como en la versión 1.1) y
                                               "Civil 3D ocupado" (1.2.2: con _.LINE activo, ping responde y las
                                               herramientas de dibujo esperan y se descartan sin ejecutarse)
  python probar_servidor.py --dwg RUTA.dwg  -> además abre ese dibujo (debe tener al menos un corredor) y comprueba:
      1. todas las herramientas de lectura responden ok=true en menos de 5 s
      2. asignar_objetivo con simular=true no cambia nada (listar_objetivos idéntico antes y después)
      3. asignar_objetivo real se refleja en listar_objetivos y hace copia de seguridad (un archivo nuevo en backups\\,
         o ninguno si reutiliza la copia de la escritura anterior: copia.reutilizada, 1.3.x)
      4. ejecutar_comando sin undo no envía _.UNDO
      5. mcp_log.jsonl recibe una línea por cada llamada de escritura
      6. _.UNDO 1 revierte entera la última escritura (una entrada de deshacer por herramienta, 1.3.0)
  --sin-escritura  omite las pruebas 3, 5 y 6 (no toca el dibujo).
  --fase 13 [--dwg RUTA.dwg]  -> solo lo nuevo de la 1.3.0: GET /ping sin token, 401 con token malo agrupados en el
                                 historial, ms_espera/ms_ejecucion en el envoltorio, ms_puente al llamar por el puente
                                 (tools/call a http://127.0.0.1:8001/mcp) y, con --dwg: asignar_objetivos con 3
                                 asignaciones (simulado y real, una línea de log y una copia), _.UNDO 1 revierte el
                                 lote entero, establecer_frecuencias sobre 2 regiones, copia.reutilizada en la segunda
                                 escritura y copia.espera_ms.
  --fase todo [--dwg RUTA.dwg]  -> las pruebas de siempre y después las de la 1.3.0.
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

token_path = os.environ.get("ARBA_MCP_TOKEN_FILE") or os.path.expandvars(r"%LOCALAPPDATA%\ArbaMcp\token")
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


def llamar_bruto(tool, args=None, timeout_s=120):
    """Como llamar, pero devuelve el envoltorio completo de /execute (ok, tool, ms, ms_espera, ms_ejecucion, result | error)."""
    try:
        res = httpx.post(f"{BASE_URL}/execute", headers=CABECERAS,
                         json={"tool": tool, "args": args or {}, "timeout_s": timeout_s}, timeout=timeout_s + 10)
        return res.json()
    except Exception as e:
        return {"ok": False, "error": f"excepción: {e}"}


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


def cuerpo_json(res):
    """El cuerpo de una respuesta como JSON, o None si no lo es."""
    try:
        return res.json()
    except Exception:
        return None


# ---------------------------------------------------------------- seguridad HTTP (versión 1.1; /ping público desde 1.3.0)
def pruebas_seguridad():
    print(f"--- Token actual: {TOKEN[:8]}... ---")
    # Desde 1.3.0 GET /ping responde sin token (solo ok, servidor y version, sin datos del dibujo): es lo que sondea el
    # puente mientras Civil 3D arranca. El token se sigue exigiendo en el resto de rutas (/tools, /execute).
    r = test_req("Sin token: /ping es público (200)", "GET", "/ping", {})
    c = cuerpo_json(r) if r is not None else None
    resultado("sin token /ping responde 200 con solo ok, servidor y version", r is not None and r.status_code == 200 and isinstance(c, dict) and set(c) == {"ok", "servidor", "version"}, (r.text[:160] if r is not None else ""))
    r = test_req("Sin token: /tools (401)", "GET", "/tools", {})
    resultado("sin token /tools responde 401", r is not None and r.status_code == 401)
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
        print("\n(--sin-escritura: se omiten las pruebas 3, 5 y 6)")
        pruebas_undo()
        return

    # ---- 3. escritura real + copia de seguridad
    print("\n--- 3. asignar_objetivo real ---")
    destino_real = otra if otra else "ninguno"
    backups_antes = archivos_backups(dwg)
    log_antes = len(lineas_log(dwg))
    ok, r, _ = llamar("asignar_objetivo", dict(base, objetivo=destino_real))
    resultado("asignar_objetivo real responde ok", ok, str(r)[:300])
    copia_real = r.get("copia") if isinstance(r, dict) else None
    nuevos = list(llamar("listar_objetivos", {"corredor": cor["nombre"], "linea_base": reg["linea_base"], "region": reg["nombre"]})[1] or [])
    obj_nuevo = next((o for o in nuevos if o.get("subensamblaje") == obj_sup["subensamblaje"] and o.get("tipo") == "superficie" and o.get("parametro") == obj_sup.get("parametro")), None)
    nombres_nuevos = [o.get("nombre") for o in (obj_nuevo or {}).get("objetivos", [])]
    esperado = [] if destino_real == "ninguno" else [destino_real]
    resultado("listar_objetivos refleja el cambio", nombres_nuevos == esperado, f"{nombres_nuevos} (esperado {esperado})")
    nuevos_backups = archivos_backups(dwg) - backups_antes
    # 1.3.x: la copia de disco se reutiliza si el .dwg no cambió desde la escritura anterior (copia.reutilizada=true, sin
    # archivo nuevo); en la primera escritura desde que se abrió el dibujo tiene que aparecer exactamente un archivo
    resultado("copia de seguridad: un archivo nuevo en backups\\ (ninguno si copia.reutilizada)", len(nuevos_backups) == (0 if (copia_real or {}).get("reutilizada") else 1), f"nuevos={sorted(nuevos_backups)} copia={str(copia_real)[:160]}")

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

    # ---- 6. deshacer (1.3.0): cada escritura queda como UNA entrada; _.UNDO 1 revierte la última entera
    print("\n--- 6. _.UNDO 1 revierte la última escritura entera ---")
    ok, r, _ = llamar("ejecutar_comando", {"comando": "_.UNDO 1", "timeout_s": 30}, timeout_s=40)
    resultado("_.UNDO 1 terminado", ok and r == "terminado", str(r))
    tras_undo = list(llamar("listar_objetivos", {"corredor": cor["nombre"], "linea_base": reg["linea_base"], "region": reg["nombre"]})[1] or [])
    obj_undo = next((o for o in tras_undo if o.get("subensamblaje") == obj_sup["subensamblaje"] and o.get("tipo") == "superficie" and o.get("parametro") == obj_sup.get("parametro")), None)
    nombres_undo = [o.get("nombre") for o in (obj_undo or {}).get("objetivos", [])]
    resultado("tras _.UNDO 1 el objetivo vuelve al valor previo a la última escritura (la restauración se deshizo entera)", nombres_undo == esperado, f"{nombres_undo} (esperado {esperado})")
    ok3, r3, _ = llamar("asignar_objetivo", dict(base, objetivo=nombre_actual or "ninguno"))
    resultado("restaurar de nuevo el objetivo original", ok3, str(r3)[:300])
    copia3 = (r3 or {}).get("copia") if isinstance(r3, dict) else None
    resultado("la respuesta lleva copia como objeto (ruta, reutilizada, ms, espera_ms, refleja_guardado_de)", isinstance(copia3, dict) and all(k in copia3 for k in ("ruta", "reutilizada", "ms", "espera_ms", "refleja_guardado_de")), str(copia3)[:300])

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


# ---------------------------------------------------------------- fase 13 (1.3.0)
PUENTE_URL = os.environ.get("ARBA_PUENTE_URL", "http://127.0.0.1:8001/mcp")


def pruebas_fase13_servidor():
    print("\n=== 1.3.0: servidor y puente ===")
    # ping sin token: 200 con versión y sin datos del dibujo; con token malo también 200; /tools sin token sigue en 401
    r = test_req("GET /ping sin token (200, versión)", "GET", "/ping", {})
    cuerpo = {}
    try:
        cuerpo = r.json() if r is not None else {}
    except Exception:
        pass
    resultado("ping sin token responde 200", r is not None and r.status_code == 200)
    resultado("ping sin token devuelve ok, servidor y version 1.3.x y nada más", cuerpo.get("ok") is True and cuerpo.get("servidor") == "ArbaMcp" and str(cuerpo.get("version", "")).startswith("1.3.") and set(cuerpo) == {"ok", "servidor", "version"}, str(cuerpo)[:160])
    r = test_req("GET /ping con token malo (200)", "GET", "/ping", {"X-Arba-Token": "malo"})
    resultado("ping con token malo también responde 200", r is not None and r.status_code == 200)
    r = test_req("GET /tools sin token (401)", "GET", "/tools", {})
    resultado("tools sin token sigue respondiendo 401", r is not None and r.status_code == 401)

    # 401 con token malo: una sola línea en el historial por ruta y minuto (no una por petición)
    ok, hist_antes, _ = llamar("leer_historial", {"ultimas_n": 200})
    n_antes = sum(1 for l in (hist_antes or []) if "MCP 401 GET /tools" in l)
    for _ in range(3):
        httpx.get(f"{BASE_URL}/tools", headers={"X-Arba-Token": "malo"}, timeout=5)
    ok, hist, _ = llamar("leer_historial", {"ultimas_n": 200})
    lineas_401 = [l for l in (hist or []) if "MCP 401 GET /tools" in l]
    n_despues = len(lineas_401)
    # Se agrupan por ruta y minuto: si otra petición rechazada de este minuto ya dejó la línea, las 3 no añaden ninguna
    resultado("3 peticiones con token malo añaden como máximo 1 línea 'MCP 401 GET /tools' al historial (se agrupan por minuto)", n_despues >= 1 and n_despues - n_antes <= 1, f"{n_antes} -> {n_despues}: " + " | ".join(lineas_401[-2:])[:300])

    # ms, ms_espera y ms_ejecucion en el envoltorio de /execute
    env = llamar_bruto("listar_alineamientos", timeout_s=30)
    resultado("el envoltorio de /execute lleva ms, ms_espera y ms_ejecucion", env.get("ok") is True and all(k in env for k in ("ms", "ms_espera", "ms_ejecucion")), str({k: env.get(k) for k in ("ms", "ms_espera", "ms_ejecucion")}))
    env = llamar_bruto("ping", timeout_s=10)
    resultado("ping (Inmediato) también lleva ms_espera y ms_ejecucion", env.get("ok") is True and env.get("ms_espera") is not None and env.get("ms_ejecucion") is not None, str({k: env.get(k) for k in ("ms", "ms_espera", "ms_ejecucion")}))

    # ms_puente al llamar por el puente (tools/call a /mcp en modo sin sesión)
    print(f"\n[TEST] tools/call ping por el puente {PUENTE_URL}")
    try:
        res = httpx.post(PUENTE_URL, headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream"},
                         json={"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": "ping", "arguments": {"args": {}}}}, timeout=30)
        print(f"Status: {res.status_code}")
        print(f"Body: {res.text[:400]}")
        contenido = res.json()["result"]["content"][0]["text"]
        datos = json.loads(contenido)
        resultado("por el puente, ping devuelve ms_puente junto a ms de Civil 3D", isinstance(datos.get("ms_puente"), int) and "ms" in datos and datos.get("plugin") == "ArbaMcp", str(datos)[:200])
    except Exception as e:
        resultado("por el puente, ping devuelve ms_puente junto a ms de Civil 3D", False, f"¿está el puente en marcha en {PUENTE_URL}? {e}")


def contexto_corredor(dwg):
    """Abre el dibujo y devuelve (corredor, regiones, objetivos de superficie de la primera región, superficies) o None."""
    ok, r, _ = llamar("abrir_dibujo", {"ruta": dwg})
    resultado("abrir_dibujo", ok, str(r))
    if not ok:
        return None
    corredores = lectura("listar_corredores") or []
    superficies = lectura("listar_superficies") or []
    resultado("el dibujo tiene al menos un corredor", len(corredores) > 0)
    if not corredores:
        return None
    cor = corredores[0]
    regiones = lectura("listar_regiones", {"corredor": cor["nombre"]}) or []
    resultado("el corredor tiene regiones", len(regiones) > 0)
    if not regiones:
        return None
    reg = regiones[0]
    objetivos = lectura("listar_objetivos", {"corredor": cor["nombre"], "linea_base": reg["linea_base"], "region": reg["nombre"]}) or []
    de_superficie = [o for o in objetivos if o.get("tipo") == "superficie"]
    resultado("la primera región tiene objetivos de superficie", len(de_superficie) > 0)
    if not de_superficie or not superficies:
        return None
    return cor, regiones, de_superficie, superficies


def objetivos_region(cor, reg):
    return list(llamar("listar_objetivos", {"corredor": cor["nombre"], "linea_base": reg["linea_base"], "region": reg["nombre"]})[1] or [])


def nombres_objetivo(objetivos, obj):
    o = next((x for x in objetivos if x.get("subensamblaje") == obj["subensamblaje"] and x.get("tipo") == "superficie" and x.get("parametro") == obj.get("parametro")), None)
    return [t.get("nombre") for t in (o or {}).get("objetivos", [])]


def pruebas_fase13_dibujo(dwg):
    print(f"\n=== 1.3.0: lotes, deshacer y copia sobre {dwg} ===")
    ctx = contexto_corredor(dwg)
    if ctx is None:
        return
    cor, regiones, de_superficie, superficies = ctx
    reg = regiones[0]
    # 3 asignaciones válidas (los 3 primeros objetivos de superficie; se repite el primero si hay menos) y 1 con región inexistente
    elegidos = (de_superficie * 3)[:3]
    actuales = {i: nombres_objetivo(de_superficie, o) for i, o in enumerate(elegidos)}
    nombre_actual = (actuales[0] or [None])[0]
    otra = next((s["nombre"] for s in superficies if s["nombre"] != nombre_actual), None) or superficies[0]["nombre"]

    def asignacion(o, objetivo):
        d = {"region": reg["nombre"], "subensamblaje": o["subensamblaje"], "tipo": "superficie", "objetivo": objetivo}
        if o.get("grupo"):
            d["grupo"] = o["grupo"]
        if o.get("parametro"):
            d["parametro"] = o["parametro"]
        return d

    asignaciones = [asignacion(o, otra) for o in elegidos] + [dict(asignacion(elegidos[0], otra), region="region_que_no_existe_13")]
    args = {"corredor": cor["nombre"], "linea_base": reg["linea_base"], "asignaciones": json.dumps(asignaciones, ensure_ascii=False)}

    # ---- simulado
    print("\n--- asignar_objetivos simular=true (3 válidas + 1 con región inexistente) ---")
    log_antes, backups_antes = len(lineas_log(dwg)), archivos_backups(dwg)
    ok, r, _ = llamar("asignar_objetivos", dict(args, simular=True))
    resultado("asignar_objetivos simulado responde ok con plan de 4", ok and isinstance(r, dict) and r.get("simulado") is True and len(r.get("plan") or []) == 4, str(r)[:400])
    resultado("simulado: fallidos tiene solo la asignación 3 (región inexistente)", ok and [f.get("indice") for f in (r or {}).get("fallidos", [])] == [3], str((r or {}).get("fallidos"))[:200])
    resultado("simulado: datos = total 4, aplicadas 3, fallidas 1", ok and (r or {}).get("datos") == {"total": 4, "aplicadas": 3, "fallidas": 1}, str((r or {}).get("datos")))
    resultado("simulado: una línea de log y ninguna copia nueva", len(lineas_log(dwg)) == log_antes + 1 and archivos_backups(dwg) == backups_antes)
    resultado("simulado: listar_objetivos no cambió", nombres_objetivo(objetivos_region(cor, reg), elegidos[0]) == actuales[0])

    # ---- real: una copia, una línea de log, 3 aplicadas
    print("\n--- asignar_objetivos real ---")
    log_antes, backups_antes = len(lineas_log(dwg)), archivos_backups(dwg)
    ok, r, seg = llamar("asignar_objetivos", args)
    resultado(f"asignar_objetivos real responde ok ({seg:.2f} s)", ok and isinstance(r, dict) and r.get("simulado") is False, str(r)[:400])
    datos = (r or {}).get("datos") if ok else None
    resultado("real: datos = total 4, aplicadas 3, fallidas 1", datos == {"total": 4, "aplicadas": 3, "fallidas": 1}, str(datos))
    copia1 = (r or {}).get("copia") if ok else None
    resultado("real: copia es un objeto con ruta, metodo, reutilizada, ms, espera_ms, refleja_guardado_de y nota", isinstance(copia1, dict) and all(k in copia1 for k in ("ruta", "metodo", "reutilizada", "ms", "espera_ms", "refleja_guardado_de", "nota")), str(copia1)[:300])
    resultado("real: la nota dice que la copia refleja el último guardado en disco", isinstance(copia1, dict) and "guardado en disco" in str(copia1.get("nota", "")), str((copia1 or {}).get("nota"))[:200])
    nuevos = archivos_backups(dwg) - backups_antes
    resultado("real: una sola línea de log para el lote", len(lineas_log(dwg)) == log_antes + 1, f"{log_antes} -> {len(lineas_log(dwg))}")
    resultado("real: una copia nueva como máximo (0 si se reutilizó)", len(nuevos) == (0 if (copia1 or {}).get("reutilizada") else 1), ", ".join(sorted(nuevos)))
    despues = objetivos_region(cor, reg)
    resultado("real: los 3 objetivos de superficie apuntan a '" + otra + "'", all(nombres_objetivo(despues, o) == [otra] for o in elegidos), str([nombres_objetivo(despues, o) for o in elegidos])[:200])

    # ---- _.UNDO 1 revierte el lote entero
    print("\n--- _.UNDO 1 revierte el lote entero ---")
    ok, r, _ = llamar("ejecutar_comando", {"comando": "_.UNDO 1", "timeout_s": 30}, timeout_s=40)
    resultado("_.UNDO 1 terminado", ok and r == "terminado", str(r))
    tras = objetivos_region(cor, reg)
    resultado("tras _.UNDO 1 los 3 objetivos vuelven a su valor original (el lote es una sola entrada de deshacer)", all(nombres_objetivo(tras, o) == actuales[i] for i, o in enumerate(elegidos)), str([nombres_objetivo(tras, o) for o in elegidos])[:200])

    # ---- establecer_frecuencias sobre 2 regiones; copia reutilizada en la segunda escritura
    print("\n--- establecer_frecuencias sobre 2 regiones ---")
    if len(regiones) < 2:
        print("  (el corredor tiene una sola región: se prueba el lote con esa región dos veces)")
    dos = (regiones * 2)[:2]
    originales = [float(x.get("frecuencia_tangentes") or 20) for x in dos]
    pedido = [{"region": x["nombre"], "linea_base": x["linea_base"], "tangentes": originales[i] + 1} for i, x in enumerate(dos)]
    args_f = {"corredor": cor["nombre"], "regiones": json.dumps(pedido, ensure_ascii=False)}
    ok, r, _ = llamar("establecer_frecuencias", dict(args_f, simular=True))
    resultado("establecer_frecuencias simulado responde plan de 2", ok and len((r or {}).get("plan") or []) == 2, str(r)[:300])
    ok, r, _ = llamar("establecer_frecuencias", args_f)
    resultado("establecer_frecuencias real responde ok con 2 aplicadas", ok and (r or {}).get("datos", {}).get("aplicadas") == 2, str(r)[:400])
    copia2 = (r or {}).get("copia") if ok else None
    resultado("segunda escritura: copia.reutilizada es true (el .dwg de disco no cambió)", isinstance(copia2, dict) and copia2.get("reutilizada") is True, str(copia2)[:300])
    resultado("copia.espera_ms es un número", isinstance(copia2, dict) and isinstance(copia2.get("espera_ms"), int), str((copia2 or {}).get("espera_ms")))
    regs = lectura("listar_regiones", {"corredor": cor["nombre"]}) or []
    leidas = {x["nombre"]: x.get("frecuencia_tangentes") for x in regs}
    resultado("listar_regiones refleja las frecuencias nuevas", all(abs(float(leidas.get(x["nombre"]) or 0) - (originales[i] + 1)) < 1e-6 for i, x in enumerate(dos)), str(leidas)[:200])
    ok, r, _ = llamar("ejecutar_comando", {"comando": "_.UNDO 1", "timeout_s": 30}, timeout_s=40)
    regs = lectura("listar_regiones", {"corredor": cor["nombre"]}) or []
    leidas = {x["nombre"]: x.get("frecuencia_tangentes") for x in regs}
    resultado("_.UNDO 1 revierte el lote de frecuencias entero", ok and r == "terminado" and all(abs(float(leidas.get(x["nombre"]) or 0) - originales[i]) < 1e-6 for i, x in enumerate(dos)), str(leidas)[:200])

    # ---- validación previa: error con el índice y límite de 200
    print("\n--- validación de lotes ---")
    malo = [asignacion(elegidos[0], otra), {"region": reg["nombre"], "subensamblaje": "x"}]
    ok, r, _ = llamar("asignar_objetivos", dict(args, asignaciones=json.dumps(malo)))
    resultado("un elemento sin 'tipo' rechaza el lote con su índice y no escribe", (not ok) and "asignaciones[1]" in str(r) and "tipo" in str(r), str(r)[:200])
    muchos = [asignacion(elegidos[0], otra)] * 201
    ok, r, _ = llamar("asignar_objetivos", dict(args, asignaciones=json.dumps(muchos), simular=True))
    resultado("201 elementos sin forzar: error con el límite", (not ok) and "201" in str(r) and "forzar" in str(r), str(r)[:200])
    ok, r, _ = llamar("asignar_objetivos", dict(args, asignaciones="[{", simular=True))
    resultado("asignaciones que no es JSON: error claro", (not ok) and "JSON" in str(r), str(r)[:200])


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description="Pruebas del servidor ArbaMcp")
    ap.add_argument("--dwg", help="Ruta de un DWG de prueba con al menos un corredor")
    ap.add_argument("--sin-escritura", action="store_true", help="No modificar el dibujo (omite las pruebas 3, 5 y 6)")
    ap.add_argument("--solo-dibujo", action="store_true", help="Omitir las pruebas de seguridad HTTP y de ejecutar_comando")
    ap.add_argument("--fase", choices=["122", "13", "todo"], default="122", help="122: las pruebas de siempre; 13: solo lo nuevo de la 1.3.0; todo: ambas")
    args = ap.parse_args()

    if args.fase in ("122", "todo"):
        if not args.solo_dibujo:
            pruebas_seguridad()
            print("\n--- ejecutar_comando ---")
            pruebas_comando()
            pruebas_ocupado()
        if args.dwg:
            pruebas_dibujo(args.dwg, not args.sin_escritura)
    if args.fase in ("13", "todo"):
        pruebas_fase13_servidor()
        if args.dwg:
            if args.sin_escritura:
                print("\n(--sin-escritura: se omiten los lotes y el deshacer de la 1.3.0)")
            else:
                pruebas_fase13_dibujo(args.dwg)

    print("\n=== Resultado: " + ("TODO OK" if not fallos else f"{len(fallos)} fallo(s): " + "; ".join(fallos)) + " ===")
    sys.exit(0 if not fallos else 1)

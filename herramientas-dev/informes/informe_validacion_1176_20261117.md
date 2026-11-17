# Informe de Validación 1.17.6 — Ronda C (17 de noviembre de 2026)

- **Versión de Civil 3D:** Autodesk Civil 3D 2027 (build 13.9.628.0, AutoCAD X.60.0.0 x64)
- **Rama:** `tarea/ronda-c`
- **Commit:** `7775612` (Merge remote-tracking branch 'origin/tarea/3.6-correcciones-1176' into tarea/ronda-c)
- **Versión de ping:** `1.19.0`
- **Dibujo usado:** `PRUEBA_1176.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)
- **ARBA_MCP_CODIGO:** `1` al empezar.

---

## 1. Resumen Paso a Paso

| Paso | Decisivo | Resultado | ms_puente | Detalle / Respuesta literal |
| :--- | :--- | :--- | :--- | :--- |
| **0** | Sí | **OK** | 31 ms | `ping` devolvió `version: "1.19.0"`, `hay_dibujo: true`, 59 herramientas. `curl http://127.0.0.1:8765/ping` devolvió `{"ok":true,"servidor":"ArbaMcp","version":"1.19.0"}`. Parámetro `solo_lectura` presente en `ejecutar_codigo`. |
| **1** | No | **OK** | — | `[Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')` = `1`. |
| **2** | No | **OK** | 21 ms | `CSV` existe; `backups\` tenía 17 archivos; `listar_superficies` devolvió `[]`. |
| **3** | Sí | **OK** | 249 ms | `crear_superficie_tin` ("Terreno"): `despues.estilo`: `"Contours 1m and 5m (Background)"`, `puntos: 9`, `triangulos: 8`. En `avisos`: **ningún** mensaje de fallo de relectura de estilo. `leer_historial` sin líneas de error de relectura. |
| **4** | No | **OK** | 87 ms | `crear_superficie_tin` ("Terreno 2", estilo `Contours 5m and 25m (Background)`): `despues.estilo: "Contours 5m and 25m (Background)"`. En `avisos`: intervalos sin aplicar porque viven en el estilo; ningún error de relectura. Copia reutilizada. |
| **5** | Sí | **OK** | 2957 ms | `ejecutar_codigo` (simulación, sin `solo_lectura`): `simulado: true`, `resultado: {"largo": 100, "capa": "0"}`. `despues.objetos_creados: 1` (`Line`). `datos.compilacion.solo_lectura: false`, `escrituras_reconocidas: 1`. |
| **6** | Sí | **OK** | 74 ms | `ejecutar_codigo` (real, `simular: false`): `simulado: false`, 1 objeto creado (`handle: "746C"`, `Line`). Menú Deshacer sumó `Executefunction`. |
| **7** | Sí | **OK** | 506 ms | **Contar con solo lectura**: `solo_lectura: true`, `copia: null`, `simulado: true`, `resultado: 1`. `datos.deshacer.comando: ""`, `dbmod_antes: 17`, `dbmod_despues: 17`. Menú Deshacer idéntico (sin entradas nuevas). `backups\` con 18 archivos (sin archivo nuevo). `leer_log` registró `"solo_lectura": true`. |
| **8a** | Sí | **OK** | 403 ms | `F.Agregar(l)` rechazado al compilar: `[bloqueado: solo lectura] F.Agregar: con solo_lectura=true el script solo puede leer`. |
| **8b** | Sí | **OK** | 333 ms | `GetObject(id, OpenMode.ForWrite)` rechazado al compilar: `[bloqueado: solo lectura] OpenMode distinto de OpenMode.ForRead (OpenMode.ForWrite)`. |
| **8c** | Sí | **OK** | 343 ms | `F.Abrir<Entity>(id, true)` rechazado al compilar: `[bloqueado: solo lectura] F.Abrir con 'escribir' distinto de false (true)`. |
| **8d** | Sí | **OK** | 366 ms | `LayoutManager.DeleteLayout` rechazado al compilar: `[bloqueado: solo lectura] Autodesk.AutoCAD.DatabaseServices.LayoutManager.DeleteLayout`. |
| **8e** | Sí | **OK** | 393 ms | `OpenMode` escondido en delegado/genérico: rechazado con 7 bloqueos reconocidos (`OpenMode como argumento genérico de GetValues`, `delegado con parámetro OpenMode: GetObject`, etc.). |
| **8-fin** | Sí | **OK** | 15 ms | Conteo con solo lectura tras 8a-8e: `resultado: 1`. Menú Deshacer intacto, `backups\` = 18 archivos. |
| **9** | Sí | **FALLO CRÍTICO (Crash)** | 19759 ms | `Curve.Extend` sobre entidad abierta para lectura con `solo_lectura: true`: **AutoCAD / Civil 3D 2027 sufrió un cierre abrupto fatal (Crash/Access Violation)**. No arrojó una excepción capturable `eNotOpenForWrite`, sino que el proceso `acad.exe` murió en código nativo de AutoCAD. Se generó reporte CER en `%LOCALAPPDATA%\Autodesk\CER\6d903fba150168a4abc949c7d407680bbfba5e80\1794931859305`. |

---

## 2. Detalle de Respuestas Literales

### Paso 3: `crear_superficie_tin` ("Terreno")
```json
{
  "simulado": false,
  "herramienta": "crear_superficie_tin",
  "mensaje": "Crear la superficie TIN 'Terreno' desde el archivo 'C:\\Proyectos\\dibujos_prueba\\terreno_corredor.csv' con el estilo 'Contours 1m and 5m (Background)' (elegido por sus curvas de nivel) y reconstruirla",
  "cambios": ["existe", "puntos", "triangulos", "origen", "estilo", "esta_desactualizada", "extension", "formato_archivo"],
  "antes": {"existe": false, "puntos": 0},
  "despues": {
    "existe": true,
    "puntos": 9,
    "triangulos": 8,
    "origen": "archivo",
    "estilo": "Contours 1m and 5m (Background)",
    "esta_desactualizada": false,
    "extension": {"min_x": 900, "max_x": 1700, "min_y": 1900, "max_y": 2400, "min_z": 97.5, "max_z": 111.5},
    "formato_archivo": "PENZD (delimitado por comas)"
  },
  "copia": {
    "ruta": "C:\\Proyectos\\dibujos_prueba\\backups\\PRUEBA_1176_20261117_110438_crear_superficie_tin.dwg",
    "metodo": "copia_de_disco",
    "reutilizada": false,
    "ms": 7
  },
  "datos": {
    "nombre": "Terreno",
    "estilo": "Contours 1m and 5m (Background)",
    "puntos": 9,
    "triangulos": 8,
    "intervalos_curvas": {
      "pedido_menor": 1,
      "pedido_mayor": 5,
      "aplicados": true,
      "estilo": "Contours 1m and 5m (Background)",
      "eleccion": "automatica",
      "estilo_menor": 1,
      "estilo_mayor": 5,
      "curvas_visibles": true
    }
  },
  "avisos": [
    "Formato de archivo de puntos 'PENZD (delimitado por comas)' elegido automáticamente: columnas PENZD con separador coma; pasa 'formato_archivo' si el archivo usa otro orden de columnas.",
    "Intervalos de curvas pedidos (menor 1 y mayor 5) aplicados sin 'estilo': se usa el estilo 'Contours 1m and 5m (Background)', que tiene menor 1 y mayor 5 (tolerancia 0.001), de la misma familia («Background») que el estilo por defecto del dibujo 'Contours 2m and 10m (Background)'. También coinciden: 'Contours 1m and 5m (Design)'. Pasa 'estilo' para usar otro. No se modificó ni se creó ningún estilo."
  ],
  "ms": 207,
  "ms_puente": 249
}
```

### Paso 7: Contar con solo lectura
```json
{
  "simulado": true,
  "herramienta": "ejecutar_codigo",
  "accion": "Ejecutar un script de solo lectura de 4 línea(s): con el dibujo bloqueado para lectura y la transacción abortada al final, sin copia de seguridad y sin entrada en Deshacer.",
  "cambios": [],
  "antes": {"objetos_creados": 0, "objetos_modificados": 0, "objetos_borrados": 0},
  "despues": {"objetos_creados": 0, "objetos_modificados": 0, "objetos_borrados": 0},
  "avisos": null,
  "solo_lectura": true,
  "copia": null,
  "datos": {
    "objetos": {"creados": [], "modificados": [], "borrados": [], "temporales": 0},
    "deshacer": {
      "comando": "",
      "undoctl_antes": 53,
      "undoctl_despues": 53,
      "dbmod_antes": 17,
      "dbmod_despues": 17
    },
    "compilacion": {
      "ms": 465,
      "de_cache": false,
      "lineas": 4,
      "roslyn": "5.0.0.0",
      "solo_lectura": true,
      "escrituras_reconocidas": 0
    }
  },
  "resultado": 1,
  "ms": 498,
  "ms_puente": 506
}
```

### Paso 9: Intento de escritura en solo lectura vía `Curve.Extend`
```csharp
Line l = null;
foreach (var id in F.EspacioModelo())
    if (F.Abrir<Entity>(id) is Line x) l = x;
if (l == null) return "sin líneas";
l.Extend(false, new Point3d(l.EndPoint.X + 50, l.EndPoint.Y, 0));
return l.Length;
```
- **Respuesta MCP:**
```json
{"ok": false, "error": "Error de conexión: ", "ms_puente": 19759}
```
- **Comportamiento en Civil 3D:** El proceso `acad.exe` finalizó repentinamente. No se capturó excepción gestionada en .NET; el módulo nativo de AutoCAD generó una falla crítica y disparó el Customer Error Reporting (CER) de Autodesk (`cer_dialog.exe`, pid 45436) con el informe `1794931859305`.

---

## 3. Estado de Menú Deshacer y Copias de Seguridad

- **Punto de partida (Paso 2):**
  - Entradas en Deshacer (3): `Arbamcp`, `Grupo de comandos`, `Grupo de comandos`.
  - Archivos en `backups\`: 17.
- **Tras Paso 3:**
  - Entradas en Deshacer (4): `Executefunction`, `Arbamcp`, `Grupo de comandos`, `Grupo de comandos`.
  - Archivos en `backups\`: 18 (`PRUEBA_1176_20261117_110438_crear_superficie_tin.dwg`).
- **Tras Paso 4:**
  - Entradas en Deshacer (5): `Executefunction`, `Executefunction`, `Arbamcp`, `Grupo de comandos`, `Grupo de comandos`.
  - Archivos en `backups\`: 18 (copia reutilizada).
- **Tras Paso 6:**
  - Entradas en Deshacer: `Executefunction` arriba.
- **Tras Paso 7 (solo lectura):**
  - Entradas en Deshacer: Intacto, ninguna entrada añadida.
  - Archivos en `backups\`: 18 (ningún archivo nuevo, `copia: null`).
- **Tras Paso 8a-8e (rechazos de compilación en solo lectura):**
  - Entradas en Deshacer: Intacto.
  - Archivos en `backups\`: 18 (ningún archivo nuevo).

---

## 4. Conclusión para la Sección 1.17.6 de `miembros_por_verificar_civil3d.md`

1. **`Entity.StyleId` / `StyleName` (enlace directo sobre `TinSurface`):** **VERIFICADO OK**. Se releyó correctamente `"Contours 1m and 5m (Background)"` y `"Contours 5m and 25m (Background)"` sin advertencias de reflexión ni avisos erróneos.
2. **`solo_lectura: true` en `ejecutar_codigo`:** **VERIFICADO OK**. Conteo sin entrada en Deshacer (`comando: ""`), `dbmod_antes == dbmod_despues == 17`, `copia: null`, y 0 archivos nuevos en `backups\`.
3. **Bloqueos de compilación con `solo_lectura: true`:** **VERIFICADO OK**. Bloqueados con éxito `F.Agregar`, `OpenMode.ForWrite`, `F.Abrir(id, true)`, `LayoutManager.DeleteLayout` y referencias delegadas/genéricas a `OpenMode`.
4. **`Curve.Extend` sobre objeto abierto para lectura:** **HALLAZGO / FALLO CRÍTICO**. En Civil 3D 2027, llamar a `Curve.Extend` en un objeto en modo solo lectura dentro del contexto de aplicación provoca un **cierre fatal no recuperable de AutoCAD (`acad.exe`)**, impidiendo que .NET capture un `eNotOpenForWrite`.

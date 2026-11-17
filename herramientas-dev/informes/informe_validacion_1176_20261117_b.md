# Informe de Validación 1.17.6 (Repetición Parte 1 con 1.19.1) — Ronda C (17 de noviembre de 2026)

- **Versión de Civil 3D:** Autodesk Civil 3D 2027 (build 13.9.628.0, AutoCAD X.60.0.0 x64)
- **Rama:** `tarea/ronda-c`
- **Commit:** `6dba870` (Merge remote-tracking branch 'origin/tarea/3.6-correcciones-1176' into tarea/ronda-c)
- **Versión de ping:** `1.19.1` (59 herramientas)
- **Dibujo usado:** `PRUEBA_1176_b.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)
- **ARBA_MCP_CODIGO:** `1` al empezar y `1` al terminar.

---

## 1. Resumen Paso a Paso

| Paso | Decisivo | Resultado | ms_puente | Detalle / Respuesta literal |
| :--- | :--- | :--- | :--- | :--- |
| **0** | Sí | **OK** | 10 ms | `ping` devolvió `version: "1.19.1"`, `hay_dibujo: true`, 59 herramientas. `curl http://127.0.0.1:8765/ping` devolvió `{"ok":true,"servidor":"ArbaMcp","version":"1.19.1"}`. Parámetro `solo_lectura` presente en `ejecutar_codigo`. |
| **1** | No | **OK** | — | `[Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')` = `1`. |
| **2** | No | **OK** | 18 ms | `terreno_corredor.csv` existe; `backups\` tenía 18 archivos; `listar_superficies` devolvió `[]`. Menú Deshacer inicial con 2 entradas de plantilla (`Grupo de comandos`, `Grupo de comandos`). |
| **3** | Sí | **OK** | 390 ms | `crear_superficie_tin` ("Terreno"): `despues.estilo`: `"Contours 1m and 5m (Background)"`, `puntos: 9`, `triangulos: 8`. En `avisos`: **ningún** mensaje de fallo de relectura de estilo. `leer_historial` sin líneas de error de relectura. Copia generada en `backups\PRUEBA_1176_b_20261117_115450_crear_superficie_tin.dwg` (19 backups total). |
| **4** | No | **OK** | 95 ms | `crear_superficie_tin` ("Terreno 2", estilo `Contours 5m and 25m (Background)`): `despues.estilo: "Contours 5m and 25m (Background)"`. En `avisos`: intervalos sin aplicar porque viven en el estilo; ningún error de relectura. Copia reutilizada. |
| **5** | Sí | **OK** | 3329 ms | `ejecutar_codigo` (simulación, sin `solo_lectura`): `simulado: true`, `resultado: {"largo": 100, "capa": "0"}`. `despues.objetos_creados: 1` (`Line`). `datos.compilacion.solo_lectura: false`, `escrituras_reconocidas: 1`. |
| **6** | Sí | **OK** | 110 ms | `ejecutar_codigo` (real, `simular: false`): `simulado: false`, 1 objeto creado (`handle: "746C"`, `Line`). Menú Deshacer sumó `Executefunction` arriba del todo (6 entradas en total). |
| **7** | Sí | **OK** | 646 ms | **Contar con solo lectura**: `solo_lectura: true`, `copia: null`, `simulado: true`, `resultado: 1`. `datos.deshacer.comando: ""`, `dbmod_antes: 1`, `dbmod_despues: 1`. Menú Deshacer idéntico (sin entradas nuevas). `backups\` con 19 archivos (sin archivo nuevo). `leer_log` registró `"solo_lectura": true`. |
| **8a** | Sí | **OK** | 362 ms | `F.Agregar(l)` rechazado al compilar: `[bloqueado: solo lectura] F.Agregar: con solo_lectura=true el script solo puede leer...`. |
| **8b** | Sí | **OK** | 334 ms | `GetObject(id, OpenMode.ForWrite)` rechazado al compilar: `[bloqueado: solo lectura] OpenMode distinto de OpenMode.ForRead (OpenMode.ForWrite)`. |
| **8c** | Sí | **OK** | 364 ms | `F.Abrir<Entity>(id, true)` rechazado al compilar: `[bloqueado: solo lectura] F.Abrir con 'escribir' distinto de false (true)`. |
| **8d** | Sí | **OK** | 368 ms | `LayoutManager.DeleteLayout` rechazado al compilar: `[bloqueado: solo lectura] Autodesk.AutoCAD.DatabaseServices.LayoutManager.DeleteLayout`. Pestaña `Presentación1` sigue intacta. |
| **8e** | Sí | **OK** | 628 ms | `OpenMode` escondido en delegado/genérico: rechazado con 7 bloqueos reconocidos (`OpenMode como argumento genérico de Zip`, `GetValues`, `Skip`, `delegado con parámetro OpenMode: GetObject`, etc.). |
| **8-fin** | Sí | **OK** | 19 ms | Conteo con solo lectura tras 8a-8e: `resultado: 1`. Menú Deshacer intacto, `backups\` = 19 archivos. |
| **9** | Sí | **OK** | 418 ms | **`Curve.Extend` con `solo_lectura: true` bloqueado al compilar.** Error: `Línea 5, columna 3: [bloqueado: solo lectura] Autodesk.AutoCAD.DatabaseServices.Curve.Extend: con solo_lectura=true el script solo puede leer: no compila nada que cree, cambie o borre objetos del dibujo...`. **Civil 3D 2027 SIGUIÓ ABIERTO**, `ping` OK (8 ms), línea de prueba midió 100 m (`resultado: 100`). |
| **9b** | No | **OK** | 355 ms | `Editor.WriteMessage("\nPrueba 1.19.1")` compila y se ejecuta en solo lectura: `resultado: 1`, `copia: null`. Texto impreso en la línea de comandos de Civil 3D. |
| **10** | Sí | **OK** | 2099 ms | `_.UNDO 1` ejecutado: `result: "terminado"`. Conteo con solo lectura posterior dio **`resultado: 0`** (la línea creada en el paso 6 se deshizo con exactamente 1 UNDO, verificando que los pasos 7, 8a-e, 9 y 9b no dejaron ninguna entrada intermedia en Deshacer). |
| **11** | No | **OK** | 358 ms | Conteo sin `solo_lectura`: `simulado: true`, `resultado: 0`, copia presente reutilizada. En `avisos`: `"El script no cambió nada y no usa ninguna de las escrituras que el plugin reconoce: si solo lee, pásalo con solo_lectura=true la próxima vez..."`. |
| **12** | No | **OK** | 279 ms | Capa `EJE_PRUEBA` creada (`terminado`, 111 ms); polilínea de 4 vértices trazada (`terminado`, 168 ms). |
| **13** | Sí | **OK** | 168 ms | `crear_alineamiento_desde_polilinea` ("Eje 1176", radio 80): Simulación `despues.estilo: "Proposed"`. Creación real: `despues.estilo: "Proposed"`, `longitud: 831.3274`, `numero_curvas: 2`. `avisos: null` (ningún fallo de relectura). |
| **14** | No | **OK** | 148 ms | `crear_perfil_terreno_y_vista`: Simulación y real coincidieron: `perfil.estilo: "Perfil del terreno existente"`, `vista.estilo: "Profile View"`. `avisos` sin errores de relectura de estilo. |
| **15** | Sí | **OK** | 94 ms | `crear_rasante` ("Rasante 1176", estilo `Design Profile`): `despues.tipo: "FG"`, `numero_pvis: 3`, `numero_curvas: 1`, `despues.estilo: "Design Profile"`. `avisos` sin errores de relectura. |
| **16** | Sí | **OK** | 466 ms | `ejecutar_receta` (`crear_capa_con_color`, `solo_lectura: true`): `ok: false`, rechazado al compilar por 5 usos no permitidos (`F.Abrir`, `SymbolTableRecord.Name`, `LayerTableRecord.Color`, `SymbolTable.Add`, `Transaction.AddNewlyCreatedDBObject`). `veces_usada: 2` (sin cambio), `receta.sumada: false`. |
| **17** | Sí | **OK** | 306 ms | `Database.AddDBObject` sin `solo_lectura`: `ok: false`, `[bloqueado: transacción] Autodesk.AutoCAD.DatabaseServices.Database.AddDBObject: usa F.Tr o una transacción anidada`. |
| **17b** | Sí | **OK** | 347 ms | `XBindXrefs` y `DetachXref`: `ok: false` (2 usos no permitidos), `[bloqueado: documentos]`. |
| **17c** | Sí | **OK** | 38 ms | Asignación por desestructuración `(F.Db.Clayer, _) = ...`: `ok: false`, `[bloqueado: variables de sistema] Autodesk.AutoCAD.DatabaseServices.Database.Clayer (asignación)`. |
| **18** | No | **OK** | 8 ms | `leer_historial` confirma los registros de `ejecutar_codigo` con solo lectura y bloqueos. `ARBA_MCP_CODIGO` permanece en `1`. |

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
    "ruta": "C:\\Proyectos\\dibujos_prueba\\backups\\PRUEBA_1176_b_20261117_115450_crear_superficie_tin.dwg",
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
  "ms": 354,
  "ms_puente": 390
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
      "undoctl_texto": "deshacer activado; Auto; zoom y encuadre agrupados; propiedades de capa agrupadas",
      "dbmod_antes": 1,
      "dbmod_despues": 1
    },
    "compilacion": {
      "ms": 547,
      "de_cache": false,
      "lineas": 4,
      "roslyn": "5.0.0.0",
      "solo_lectura": true,
      "escrituras_reconocidas": 0
    }
  },
  "resultado": 1,
  "ms": 637,
  "ms_puente": 646
}
```

### Paso 9: Error Literal de `Curve.Extend` con `solo_lectura: true`
```text
Error de Civil 3D: ArgumentException: ejecutar_codigo: el código no se puede ejecutar (1 uso no permitido). No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.
No permitido:
- Línea 5, columna 3: [bloqueado: solo lectura] Autodesk.AutoCAD.DatabaseServices.Curve.Extend: con solo_lectura=true el script solo puede leer: no compila nada que cree, cambie o borre objetos del dibujo, abra objetos para escritura ni escriba archivos; sobre los objetos del dibujo y de Civil 3D solo se pueden llamar métodos de lectura (Get..., Is..., Has..., Find..., Contains..., To..., PointLocation, StationOffset, ElevationAt...). Si el script necesita escribir, quita solo_lectura y simula primero (simular=true).
El código es el cuerpo de un método: F es la Fachada (F.Doc, F.Db, F.Tr, F.Civil, F.Alineamiento(nombre), F.Agregar(entidad), F.Escribir(texto)...), Parametros es el JSON de solo lectura de 'parametros', y lo que devuelvas con return sale en 'resultado'.
```
- **Comportamiento en Civil 3D:** Civil 3D 2027 permaneció abierto, estable y respondiendo. Sin caídas nativas ni generación de reportes CER.

---

## 3. Estado del Menú Deshacer y Backups

| Momento | Entradas en Menú Deshacer (arriba a abajo) | Total Backups | Backup más reciente |
| :--- | :--- | :--- | :--- |
| **Inicio (Paso 2)** | `Grupo de comandos`, `Grupo de comandos` (2) | 18 | `PRUEBA_1175_20261116_221446_ejecutar_codigo.dwg` |
| **Tras Paso 3** | `Executefunction`, `Grupo de comandos`, `Grupo de comandos` (3) | 19 | `PRUEBA_1176_b_20261117_115450_crear_superficie_tin.dwg` |
| **Tras Paso 5** | `Executefunction`, `Executefunction`, `Grupo de comandos`... (5) | 19 | (reutilizada) |
| **Tras Paso 6** | `Executefunction` (arriba), `Executefunction`, `Executefunction`, `Executefunction`, `Grupo de comandos`, `Grupo de comandos` (6) | 19 | (reutilizada) |
| **Tras Pasos 7, 8a-e, 9, 9b** | **Idéntico: 6 entradas**, `Executefunction` arriba (ninguna entrada agregada) | 19 | (sin cambios) |
| **Tras Paso 10 (`_.UNDO 1`)** | Se deshizo exactamente la entrada superior de la línea creada en el paso 6 | 19 | (sin cambios) |
| **Tras Paso 11** | Sumó una entrada `Executefunction` de simulación normal sin `solo_lectura` | 19 | (reutilizada) |

---

## 4. Relectura de Estilos en Entidades

- **Alineamiento (Paso 13):** Simulación: `Proposed`. Creación real: `Proposed`. Releído directamente sin avisos de fallo.
- **Perfil de Terreno y Vista (Paso 14):** Simulación: Perfil `Perfil del terreno existente`, Vista `Profile View`. Creación real: idénticos. Sin avisos de fallo.
- **Rasante (Paso 15):** Creación real: `Design Profile`. Releído directamente sin avisos de fallo.

---

## 5. Auditoría de Seguridad (Pasos 8d, 8e, 16, 17, 17b, 17c)

- **8d:** `LayoutManager.DeleteLayout` bloqueado por regla de solo lectura. La pestaña `Presentación1` permaneció en el dibujo.
- **8e:** Detección de `OpenMode` oculto en genéricos y delegados (`Zip`, `GetValues`, `Skip`, `GetObject`) bloqueado exitosamente con 7 detecciones.
- **16:** `ejecutar_receta` bloqueó la receta `crear_capa_con_color` en compilación; `veces_usada` quedó en 2 (`sumada: false`).
- **17:** `Database.AddDBObject` bloqueado por regla de transacción obligatoria.
- **17b:** `XBindXrefs` y `DetachXref` bloqueados por regla de documentos.
- **17c:** Asignación de variables de sistema mediante tupla desestructurada `(F.Db.Clayer, _) = ...` bloqueada por regla de variables de sistema.

---

## 6. Conclusión de la Parte 1 (1.19.1)

La repetición completa de la Parte 1 ha resultado **100 % satisfactoria (todos los pasos decisivos OK)**.
La corrección del commit `6dba870` en la versión `1.19.1` resolvió de raíz la vulnerabilidad de caída nativa en el paso 9 al bloquear a nivel sintáctico/Roslyn cualquier método de entidad que no pertenezca a la lista blanca de lectura estricta.

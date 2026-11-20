# Informe de Validación v1.19.2 (`ejecutar_codigo`: Escritura Segura y Análisis Semántico)

- **Fecha:** 2026-11-20
- **Civil 3D:** Autodesk Civil 3D 2027 (Español)
- **Rama:** `tarea/ronda-d` (commit `153dcb6`)
- **Versión plugin / ping:** `1.21.2`
- **DWG de prueba:** `PRUEBA_1192.dwg` (copia limpia de `PRUEBA_EVALUACION_BASE.dwg`)
- **`ARBA_MCP_CODIGO`:** `1` al inicio (ya activo en entorno de usuario) y `1` al terminar (mantenido para comprobaciones C1-C3).

---

## 1. Tabla de Ejecución Paso a Paso

| Paso | Herramienta | Parámetros Clave | Resultado | Tiempo (`ms_puente`) | Observaciones / Respuestas Literales |
| :--- | :--- | :--- | :---: | :---: | :--- |
| **0** | `ping` | Verificación plugin | **OK** | 10 ms | Versión `1.21.2`, `hay_dibujo: true`, dibujo `PRUEBA_1192.dwg`. |
| **1** | PowerShell | `ARBA_MCP_CODIGO` | **OK** | - | Valor devuelto: `1`. |
| **2** | `ejecutar_codigo` | Contar líneas (solo lectura) | **OK** | 8911 ms (Roslyn frío: 8848 ms) | `resultado: 0`. Dibujo sin líneas iniciales. Backups: 27. |
| **3** | `ejecutar_codigo` | Ronda C (`l.Extend`) sin `solo_lectura` (**Decisivo**) | **OK (Bloqueo exitoso)** | 566 ms | `ok: false`. Compilador Roslyn bloqueó la llamada por objeto abierto para lectura (`F.Abrir<Entity>(id)`). Civil 3D sigue abierto. 0 backups nuevos. |
| **4a** | `ejecutar_codigo` | `x.Layer = "0"` sin `true` | **OK (Bloqueo exitoso)** | 336 ms | `ok: false`. Bloqueado: asignación de propiedad sobre objeto abierto para lectura. 0 backups nuevos. |
| **4b** | `ejecutar_codigo` | `F.Tr.GetObject(id, OpenMode.ForRead)` + `e.Erase()` (**Decisivo**) | **OK (Bloqueo exitoso)** | 433 ms | `ok: false`. Bloqueado: `DBObject.Erase` sobre `e` abierto para lectura. 0 backups nuevos. |
| **5** | `ejecutar_codigo` | Ronda C con `solo_lectura=true` (**Decisivo**) | **OK (Bloqueo exitoso)** | 400 ms | `ok: false`. Bloqueado por `solo_lectura=true`. Mensaje estándar v1.19.1 sin mención a "abierto para lectura". |
| **6** | `ejecutar_codigo` | Crear línea real 100 m (`simular: false`) | **OK** | 478 ms | `ok: true`, `simulado: false`. Mensaje: `Script de 4 línea(s) ejecutado y confirmado: 1 objeto(s) creado(s)`. Creado handle `746A`. Medir línea posterior: `resultado: 100`. Backup: `PRUEBA_1192_20261120_010747_ejecutar_codigo.dwg`. |
| **7** | `ejecutar_codigo` | `F.Abrir<Entity>(id, true)` + `Extend` 50 m (**Decisivo**) | **OK** | 449 ms | `ok: true`, `simulado: true`. `resultado: 150`. Modificados: handle `746A`. Escrituras reconocidas: 2. Medir línea posterior: `resultado: 100` (transacción abortada). |
| **8** | `ejecutar_codigo` | `l.UpgradeOpen()` + `Extend` 50 m (**Decisivo**) | **OK** | 387 ms | `ok: true`, `simulado: true`. `resultado: 150`. Modificados: handle `746A`. Escrituras reconocidas: 2. Medir línea posterior: `resultado: 100` (transacción abortada). |
| **9** | `ejecutar_receta` | `crear_capa_con_color` (simulación) (**Decisivo**) | **OK** | 491 ms | `ok: true`, `simulado: true`. `resultado: {"capa": "MCP Receta 1192", "color": 3, "creada": true}`. Escrituras reconocidas: 5. Receta `veces_usada: 2` intacto (`sumada: false`). |
| **10** | `ejecutar_codigo`<br>`leer_historial` | Contar líneas final<br>ultimas_n=20 | **OK** | 16 ms<br>7 ms | `resultado: 1`. Historial con logs de ejecución limpios. |

---

## 2. Errores Literales Completos de Compilación

### Paso 3: Script de la Ronda C sin `solo_lectura`
```text
Error de Civil 3D: ArgumentException: ejecutar_codigo: el código no se puede ejecutar (1 uso no permitido). No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.
No permitido:
- Línea 5, columna 3: [bloqueado: abierto para lectura] Autodesk.AutoCAD.DatabaseServices.Curve.Extend sobre 'l', abierto para lectura en la línea 3 (F.Abrir<Entity>(id)): cambiar un objeto abierto para lectura no siempre da un error en Civil 3D: con Curve.Extend, Civil 3D 2027 se cerró (17/11/2026). Para cambiarlo, abre el objeto con F.Abrir(id, true) (o con OpenMode.ForWrite en GetObject), o llama a UpgradeOpen() sobre él antes de cambiarlo.
El código es el cuerpo de un método: F es la Fachada (F.Doc, F.Db, F.Tr, F.Civil, F.Alineamiento(nombre), F.Agregar(entidad), F.Escribir(texto)...), Parametros es el JSON de solo lectura de 'parametros', y lo que devuelvas con return sale en 'resultado'.
```
- **Civil 3D:** Siguió abierto y respondiendo sin inestabilidad.
- **id_log:** `20261120-010707-1b0b66`

### Paso 4a: Asignación de propiedad `x.Layer = "0"`
```text
Error de Civil 3D: ArgumentException: ejecutar_codigo: el código no se puede ejecutar (1 uso no permitido). No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.
No permitido:
- Línea 2, columna 42: [bloqueado: abierto para lectura] Autodesk.AutoCAD.DatabaseServices.Entity.Layer (asignación) sobre 'x', abierto para lectura en la línea 2 (F.Abrir<Entity>(id)): cambiar un objeto abierto para lectura no siempre da un error en Civil 3D: con Curve.Extend, Civil 3D 2027 se cerró (17/11/2026). Para cambiarlo, abre el objeto con F.Abrir(id, true) (o con OpenMode.ForWrite en GetObject), o llama a UpgradeOpen() sobre él antes de cambiarlo.
El código es el cuerpo de un método: F es la Fachada (F.Doc, F.Db, F.Tr, F.Civil, F.Alineamiento(nombre), F.Agregar(entidad), F.Escribir(texto)...), Parametros es el JSON de solo lectura de 'parametros', y lo que devuelvas con return sale en 'resultado'.
```
- **Civil 3D:** Siguió abierto.
- **id_log:** `20261120-010720-7fba16`

### Paso 4b: `F.Tr.GetObject(id, OpenMode.ForRead)` + `e.Erase()`
```text
Error de Civil 3D: ArgumentException: ejecutar_codigo: el código no se puede ejecutar (1 uso no permitido). No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.
No permitido:
- Línea 4, columna 22: [bloqueado: abierto para lectura] Autodesk.AutoCAD.DatabaseServices.DBObject.Erase sobre 'e', abierto para lectura en la línea 3 (F.Tr.GetObject(id, OpenMode.ForRead)): cambiar un objeto abierto para lectura no siempre da un error en Civil 3D: con Curve.Extend, Civil 3D 2027 se cerró (17/11/2026). Para cambiarlo, abre el objeto con F.Abrir(id, true) (o con OpenMode.ForWrite en GetObject), o llama a UpgradeOpen() sobre él antes de cambiarlo.
El código es el cuerpo de un método: F es la Fachada (F.Doc, F.Db, F.Tr, F.Civil, F.Alineamiento(nombre), F.Agregar(entidad), F.Escribir(texto)...), Parametros es el JSON de solo lectura de 'parametros', y lo que devuelvas con return sale en 'resultado'.
```
- **Civil 3D:** Siguió abierto.
- **id_log:** `20261120-010726-65f2bb`

### Paso 5: Script con `solo_lectura=true`
```text
Error de Civil 3D: ArgumentException: ejecutar_codigo: el código no se puede ejecutar (1 uso no permitido). No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.
No permitido:
- Línea 5, columna 3: [bloqueado: solo lectura] Autodesk.AutoCAD.DatabaseServices.Curve.Extend: con solo_lectura=true el script solo puede leer: no compila nada que cree, cambie o borre objetos del dibujo, abra objetos para escritura ni escriba archivos; sobre los objetos del dibujo y de Civil 3D solo se pueden llamar métodos de lectura (Get..., Is..., Has..., Find..., Contains..., To..., PointLocation, StationOffset, ElevationAt...). Si el script necesita escribir, quita solo_lectura y simula primero (simular=true).
El código es el cuerpo de un método: F es la Fachada (F.Doc, F.Db, F.Tr, F.Civil, F.Alineamiento(nombre), F.Agregar(entidad), F.Escribir(texto)...), Parametros es el JSON de solo lectura de 'parametros', y lo que devuelvas con return sale en 'resultado'.
```
- **Civil 3D:** Siguió abierto.
- **id_log:** `20261120-010735-c65a9c`

---

## 3. Detalle de Ejecuciones Correctas (Pasos 6, 7 y 8)

### Paso 6 (Línea real 100 m)
- `antes`: `{"objetos_creados": 0, "objetos_modificados": 0}`
- `despues`: `{"objetos_creados": 1, "objetos_modificados": 1}`
- `datos.compilacion`: `{"ms": 409, "de_cache": false, "lineas": 4, "roslyn": "5.0.0.0", "solo_lectura": false, "escrituras_reconocidas": 1}`
- `datos.objetos`: `{"creados": [{"handle": "746A", "tipo": "Line", "nombre": null, "capa": "0"}], "modificados": [{"handle": "1F", "tipo": "BlockTableRecord", "nombre": "*Model_Space", "capa": null}], "borrados": [], "temporales": 0, "recortado": false}`
- `avisos`: `null`
- `resultado`: `{"largo": 100, "capa": "0"}`
- Medir línea: `resultado: 100`.

### Paso 7 (Simulación con `F.Abrir<Entity>(id, true)`)
- `antes`: `{"objetos_modificados": 0}`
- `despues`: `{"objetos_modificados": 1}`
- `datos.compilacion`: `{"ms": 396, "de_cache": false, "lineas": 6, "roslyn": "5.0.0.0", "solo_lectura": false, "escrituras_reconocidas": 2}`
- `datos.objetos`: `{"creados": [], "modificados": [{"handle": "746A", "tipo": "Line", "nombre": null, "capa": "0"}], "borrados": [], "temporales": 0, "recortado": false}`
- `avisos`: `["Simulación: el script se ejecutó de verdad dentro de una transacción que se abortó al final. Con objetos de Civil 3D no es una garantía total (Civil 3D puede guardar datos fuera de la transacción); la protección real es la copia de seguridad (copia.ruta).", "La simulación puede dejar una entrada vacía (Executefunction) en el menú Deshacer: no deshace nada."]`
- `resultado`: `150`
- Medir línea posterior: `resultado: 100` (transacción abortada correctamente).

### Paso 8 (Simulación con `UpgradeOpen()`)
- `antes`: `{"objetos_modificados": 0}`
- `despues`: `{"objetos_modificados": 1}`
- `datos.compilacion`: `{"ms": 338, "de_cache": false, "lineas": 7, "roslyn": "5.0.0.0", "solo_lectura": false, "escrituras_reconocidas": 2}`
- `datos.objetos`: `{"creados": [], "modificados": [{"handle": "746A", "tipo": "Line", "nombre": null, "capa": "0"}], "borrados": [], "temporales": 0, "recortado": false}`
- `avisos`: `["Simulación: el script se ejecutó de verdad dentro de una transacción que se abortó al final. Con objetos de Civil 3D no es una garantía total (Civil 3D puede guardar datos fuera de la transacción); la protección real es la copia de seguridad (copia.ruta).", "La simulación puede dejar una entrada vacía (Executefunction) en el menú Deshacer: no deshace nada."]`
- `resultado`: `150`
- Medir línea posterior: `resultado: 100` (transacción abortada correctamente).

---

## 4. Estado de Receta y Backups

### Paso 9 (`ejecutar_receta` con `crear_capa_con_color`)
- `veces_usada` antes: `2`
- `veces_usada` después: `2` (no sumó porque fue simulación, `sumada: false`).
- `resultado`: `{"capa": "MCP Receta 1192", "color": 3, "creada": true}`
- `datos.compilacion.escrituras_reconocidas`: `5`
- Compilación limpia sin falsos positivos de bloqueo.

### Registro de Backups
- Total inicial (antes de paso 3): `27`
- Tras pasos 3, 4a, 4b y 5: `27` (0 backups por rechazo de compilación).
- Tras paso 6: `28` (`PRUEBA_1192_20261120_010747_ejecutar_codigo.dwg` creado).
- Tras pasos 7, 8 y 9: `28` (copia reutilizada de forma transparente).

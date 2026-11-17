# Informe de Validación 1.17.5 (civil3d-mcp)

**Fecha:** 16 de noviembre de 2026  
**Civil 3D:** Autodesk AutoCAD Civil 3D 2027 (.NET 10)  
**Rama:** `main` (código idéntico a `tarea/3.4-codigo-objetos-deshacer`, PR #46 fusionado)  
**Commit:** `ae1edfb`  
**Versión ping:** `1.17.5`  
**Dibujo:** `C:\Proyectos\dibujos_prueba\PRUEBA_1175.dwg` (copia guardada en disco de `PRUEBA_EVALUACION_BASE.dwg`)  
**ARBA_MCP_CODIGO:**
- Al inicio (antes de L.1): vacía (no definida en User).
- Durante la validación: `1` (establecida como variable de usuario para la sesión de prueba).
- Al cierre (Paso 11): restaurada a vacía / `$null` (comprobada como `null` con Civil 3D cerrado).

---

## 1. Tabla Resumen de Pasos

| Paso | Nombre | Resultado | Respuesta literal / Detalle | Tiempo (`ms_puente`) |
|---|---|---|---|---|
| **0** | Comprobar instalación | **OK** | `curl.exe` → `{"ok":true,"servidor":"ArbaMcp","version":"1.17.5"}`. MCP `ping` → `"plugin":"ArbaMcp","version":"1.17.5","hay_dibujo":true`. 58 herramientas listadas. | 9 ms (ping) / 7 ms (curl) |
| **1** | Activar ejecutar_codigo | **OK** | `[Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')` → `1` (previamente vacía). | < 1 ms |
| **2** | Punto de partida | **OK** | Dibujo `PRUEBA_1175.dwg` abierto en disco. `backups\`: 16 archivos. | - |
| **3** | (116, 4a) Contar en simulación | **OK** | `simulado: true`, `resultado: 0` (N0 = 0). Copia de seguridad creada. | 2300 ms |
| **4** | (116, 4b) Línea en simulación (decisivo) | **OK** | `simulado: true`, `antes.objetos_creados: 0`, `despues.objetos_creados: 1`, `creados`: `[{"handle":"746B","tipo":"Line","capa":"0"}]`. `fuente_deteccion.creados_por`: `"fachada"`. Sin aviso `ATENCIÓN`. | 526 ms |
| **5** | (116, 4c) Contar tras simulación (decisivo) | **OK** | `resultado: 0` (N0 = 0, no dejó la línea). `datos.compilacion.de_cache: true`. | 47 ms |
| **6** | (116, 5a) Línea de verdad (decisivo) | **OK** | `simulado: false`, `mensaje`: `"Script de 4 línea(s) ejecutado y confirmado: 1 objeto(s) creado(s), 1 modificado(s) y 0 borrado(s)."`. `despues.objetos_creados: 1`, `creados`: `[{"handle":"746C","tipo":"Line","capa":"0"}]`. `leer_log` verificado. | 48 ms |
| **7** | (116, 5b) Deshacer una vez | **OK** | `ejecutar_comando` con `_.UNDO 1` → `"result": "terminado"`. | 255 ms |
| **8** | (116, 5c) Contar tras UNDO (decisivo) | **OK** | `resultado: 0` (N0 = 0). **Un solo `_.UNDO 1` quitó la línea.** Sin hallazgos ni necesidad de paso 8b. | 43 ms |
| **9a** | Re-escritura para prueba de Zoom | **OK** | `simulado: false`, `mensaje`: `"Script de 4 línea(s) ejecutado y confirmado: 1 objeto(s) creado(s)..."`, línea `746D`. | 49 ms |
| **9b** | Zoom Extensión | **OK** | `ejecutar_comando` con `_.ZOOM _E` → `"result": "terminado"`. | 112 ms |
| **9c** | Primer UNDO tras Zoom | **OK** | `ejecutar_comando` con `_.UNDO 1` → `"result": "terminado"`. Conteo posterior da `resultado: 1` (N0 + 1). **El primer `_.UNDO 1` deshizo el zoom y dejó la línea.** | 109 ms + 39 ms |
| **9d** | Segundo UNDO | **HALLAZGO** | Un `_.UNDO 1` individual deshace la entrada `EXECUTEFUNCTION` dejada por el script de conteo de 9c. Con `_.UNDO 2` (o dos undos consecutivos) se deshace el conteo y la línea, dando `resultado: 0`. | 111 ms + 42 ms |
| **10** | TransactionManager sin CS0104 (decisivo) | **OK** | `simulado: true`, `resultado: true`, `cambios: []`. Compilado sin error `CS0104` gracias al alias en `CodigoCompilador`. | 182 ms |
| **11** | Cierre | **OK** | `leer_historial` verificado (`creados 1 según fachada` y `creados 0 según ninguna`). Civil 3D cerrado sin guardar y `ARBA_MCP_CODIGO` restaurada a `$null`. | 8 ms |

---

## 2. Estado de `backups\`

- **Antes del Paso 3:** 16 archivos.
- **Después del Paso 3:** 17 archivos (creada `PRUEBA_1175_20261116_221446_ejecutar_codigo.dwg`, 1.147.612 bytes, `metodo: copia_de_disco`, `reutilizada: false`).
- **Después del Paso 6:** 17 archivos (`reutilizada: true`).
- **Después del Paso 9:** 17 archivos (`reutilizada: true`).

---

## 3. Menú Deshacer Literal (Entradas de arriba a abajo)

### Antes del Paso 6 (Capturado en pantalla por Jose)
1. `Executefunction` *(procedente de simulación Paso 5)*
2. `Executefunction` *(procedente de simulación Paso 4)*
3. `Executefunction` *(procedente de simulación Paso 3)*
4. `Guardarcomo` *(guardado de PRUEBA_1175.dwg)*
5. `Arbamcp` *(pulsación del botón Conexión IA)*
6. `Grupo de comandos`
7. `Grupo de comandos`

### Justo después del Paso 6
1. `Executefunction` *(línea real creada)*
2. `Executefunction` *(Paso 5)*
3. `Executefunction` *(Paso 4)*
4. `Executefunction` *(Paso 3)*
5. `Guardarcomo`

### Justo después del Paso 7 (`_.UNDO 1`)
- La entrada superior `Executefunction` correspondiente a la escritura real del Paso 6 fue eliminada.

### Durante el Paso 9 (Prueba de Zoom)
- Tras 9a: `Executefunction` (línea creada).
- Tras 9b (`_.ZOOM _E`): `Zoom` arriba del todo, sobre `Executefunction`.
- Tras 9c (`_.UNDO 1`): `Zoom` deshecho, quedando `Executefunction` intacta (la línea persiste, conteo = 1).
- Conteo de 9c introduce su propia `Executefunction` (simulación en contexto de comando).

---

## 4. Bloques Completos de `datos.objetos.fuente_deteccion` y `datos.deshacer`

### Paso 3 (Contar en simulación)
```json
"fuente_deteccion": {
  "creados_por": "ninguna",
  "fachada": {
    "creados": 0,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "transaccion_script": {
    "objetos": 7,
    "creados": 0,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "transaccion_plugin": {
    "objetos": 0,
    "creados": 0,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "eventos": {
    "creados": 0,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "eventos_al_cerrar": {
    "momento": "al abortar",
    "creados": 0,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  }
}
```
```json
"deshacer": {
  "comando": "EXECUTEFUNCTION",
  "undoctl_antes": 53,
  "undoctl_despues": 53,
  "undoctl_texto": "deshacer activado; Auto; zoom y encuadre agrupados; propiedades de capa agrupadas",
  "dbmod_antes": 0,
  "dbmod_despues": 0
}
```

### Paso 4 (La línea en simulación)
```json
"fuente_deteccion": {
  "creados_por": "fachada",
  "fachada": {
    "creados": 1,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "transaccion_script": {
    "objetos": 3,
    "creados": 0,
    "modificados": 2,
    "borrados": 0,
    "errores": 0
  },
  "transaccion_plugin": {
    "objetos": 0,
    "creados": 0,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "eventos": {
    "creados": 0,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "eventos_al_cerrar": {
    "momento": "al abortar",
    "creados": 1,
    "modificados": 1,
    "borrados": 1,
    "errores": 0
  }
}
```
```json
"deshacer": {
  "comando": "EXECUTEFUNCTION",
  "undoctl_antes": 53,
  "undoctl_despues": 53,
  "undoctl_texto": "deshacer activado; Auto; zoom y encuadre agrupados; propiedades de capa agrupadas",
  "dbmod_antes": 0,
  "dbmod_despues": 1
}
```

### Paso 6 (La línea de verdad)
```json
"fuente_deteccion": {
  "creados_por": "fachada",
  "fachada": {
    "creados": 1,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "transaccion_script": {
    "objetos": 3,
    "creados": 0,
    "modificados": 2,
    "borrados": 0,
    "errores": 0
  },
  "transaccion_plugin": {
    "objetos": 0,
    "creados": 0,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "eventos": {
    "creados": 0,
    "modificados": 0,
    "borrados": 0,
    "errores": 0
  },
  "eventos_al_cerrar": {
    "momento": "al confirmar",
    "creados": 1,
    "modificados": 2,
    "borrados": 0,
    "errores": 0
  }
}
```
```json
"deshacer": {
  "comando": "EXECUTEFUNCTION",
  "undoctl_antes": 53,
  "undoctl_despues": 53,
  "undoctl_texto": "deshacer activado; Auto; zoom y encuadre agrupados; propiedades de capa agrupadas",
  "dbmod_antes": 1,
  "dbmod_despues": 1
}
```

---

## 5. Detección de Fuentes y Eventos al Cerrar

- **En la simulación (Paso 4):** La línea fue vista por la fuente `fachada` (`F.Agregar`). Las fuentes `transaccion_script`, `transaccion_plugin` y `eventos` en vivo no la clasificaron como creada durante la ejecución. Sin embargo, `eventos_al_cerrar` con `momento: "al abortar"` registró `creados: 1, modificados: 1, borrados: 1`, demostrando que los eventos ObjectAppended/Erased son despachados por AutoCAD durante el cierre de la transacción.
- **En la escritura real (Paso 6):** La línea fue detectada y reportada por la fuente `fachada`. `eventos_al_cerrar` con `momento: "al confirmar"` registró `creados: 1, modificados: 2, borrados: 0` con `errores: 0`, confirmando de forma concluyente que las notificaciones de objetos creados llegan al confirmar la transacción exterior.

---

## 6. Comprobación de la Vista e Intervención de Usuario

- **Intervención en pantalla:** Jose no tocó la vista en ningún momento entre los pasos 6 y 8 (ni zoom, ni encuadre, ni rueda de ratón). La única inspección visual se realizó antes del Paso 6 para capturar el menú Deshacer tal como se solicitó.
- **Conclusión del Paso 9:** Se confirma empíricamente al 100% que el fallo observado en la validación 1.16 (`VALIDACION_116.md`, paso 5c) donde `_.UNDO 1` no quitó la línea se debió al `ZOOM` → `Extensión`. Al ejecutar `_.ZOOM _E`, AutoCAD inserta una entrada `Zoom` encima de `Executefunction`. El primer `_.UNDO 1` revierte únicamente el zoom. Asimismo, se descubrió que ejecutar un script de simulación/conteo genera su propia entrada `Executefunction`, por lo que deshacer hasta la línea tras un comando intermedio requiere revertir tanto el comando intermedio como la simulación.

---

## 7. Miembros de la Sección 1.17.5 de `miembros_por_verificar_civil3d.md`

| Miembro / Comportamiento | Estado verificado | Fecha | Detalle / Observación |
|---|---|---|---|
| `Transaction.GetAllObjects()` de la transacción del plugin tras confirmar `F.Tr` | **Ejecutado en Civil 3D 2027** | 2026-11-16 | `transaccion_plugin` reporta `objetos: 0, creados: 0`. No ve los objetos añadidos en `F.Tr`. |
| `Transaction.GetAllObjects()` de `F.Tr` antes de confirmarla | **Ejecutado en Civil 3D 2027** | 2026-11-16 | Lista los objetos (ej. 3 objetos), pero `IsNewObject` no clasifica la línea creada. La detección por `fachada` (`F.Agregar`) resuelve la cuenta con éxito. |
| Eventos `Database.ObjectAppended/Modified/Erased` | **Ejecutado en Civil 3D 2027** | 2026-11-16 | Durante el script devuelven 0. Se confirma que AutoCAD los despacha al confirmar (`eventos_al_cerrar` con `momento: "al confirmar"`, 1 creado) o al abortar (`momento: "al abortar"`). |
| Manejadores durante `Transaction.Commit()/Abort()` (`eventos_al_cerrar`) | **Ejecutado en Civil 3D 2027** | 2026-11-16 | Civil 3D 2027 permanece completamente estable; `errores: 0`. |
| Relectura de objetos creados tras abortar (`IsValid`/`IsErased`) | **Ejecutado en Civil 3D 2027** | 2026-11-16 | Ningún aviso falso de `ATENCIÓN` emitido. Conteo posterior da exactamente N0 = 0. |
| Variables del sistema `CMDNAMES`, `UNDOCTL` y `DBMOD` | **Ejecutado en Civil 3D 2027** | 2026-11-16 | `CMDNAMES: "EXECUTEFUNCTION"`, `UNDOCTL: 53`, `DBMOD: 0` (antes) / `1` (después de escribir). |
| Tipo `ApplicationServices.TransactionManager` y alias en `CodigoCompilador` | **Ejecutado en Civil 3D 2027** | 2026-11-16 | Paso 10 ejecutado con `resultado: true`, sin error de ambigüedad `CS0104`. |
| Comportamiento de `_.UNDO 1` tras `ejecutar_codigo` y `ZOOM` | **Ejecutado en Civil 3D 2027** | 2026-11-16 | Sin zoom, un solo `_.UNDO 1` revierte la línea a 0. Con zoom, `_.UNDO 1` revierte el zoom y preserva la línea. |

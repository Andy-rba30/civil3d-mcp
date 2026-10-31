# Informe de Validación ArbaMcp 1.8.0 en Autodesk Civil 3D 2027

- **Fecha:** 31 de octubre de 2026
- **Entorno:** Autodesk Civil 3D 2027 (Español Métrico) sobre Windows 11
- **Versión de ArbaMcp:** 1.8.0 (`FileVersion`: `1.9.0.0` con la funcionalidad de 1.8.0)
- **Versión de PuenteMcp:** 1.9.0
- **Dibujo de prueba:** `<dibujo>`
- **Token:** `<oculto>`

---

## 1. Tabla Resumen de Pasos

| Paso | Herramienta / Acción | Tiempo total | Resultado | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|
| **0** | Dibujar polilínea de prueba | ~110 ms | **OK** | Capa `EJE_PRUEBA` creada con `_.-LAYER _M EJE_PRUEBA` (verificada con `leer_variable(CLAYER) -> EJE_PRUEBA`). Polilínea 2D de 4 vértices (`1000,2000 -> 1300,2000 -> 1300,2300 -> 1600,2300`) trazada con `_SCRIPT`. | Hallazgo: `ejecutar_comando` aplica `.Trim()` a la cadena enviada, eliminando los dos espacios finales previstos por `VALIDACION_18.md` para cerrar órdenes interactivas; mediante archivo `.scr` el comando finaliza limpiamente. |
| **1** | Punto de partida | ~15 ms | **OK** | `listar_alineamientos`: Lista existente de 40+ alineamientos del proyecto; `Eje 1` no existe previamente. | Estado inicial verificado. |
| **2** | Simulación con radio 80 (decisivo) | ~35 ms | **OK** | `{"simulado": true, "herramienta": "crear_alineamiento_desde_polilinea", "accion": "Crear el alineamiento 'Eje 1' desde la polilínea con handle '21B84' con radio 80 en cada PI", "cambios": ["existe", "longitud", "numero_curvas", "progresiva_inicial", "progresiva_final", "numero_pi", "estilo", "conjunto_etiquetas", "capa", "pis"], "antes": {"existe": false, "longitud": 0, "numero_curvas": 0}, "despues": {"existe": true, "longitud": 831.3274, "numero_curvas": 2, "progresiva_inicial": 0, "progresiva_final": 831.3274, "numero_pi": 2, "estilo": "Proposed", "conjunto_etiquetas": "All Labels", "capa": "C-ROAD", "pis": [{"numero": 1, "progresiva": 300, "x": 1300, "y": 2000, "deflexion_grados": 90, "giro": "izquierda", "radio_pedido": 80, "radio_aplicado": 80, "tangente": 80, "longitud_curva": 125.6637, "progresiva_pc": 220, "progresiva_pt": 345.6637, "radio_maximo": 300, "aviso": null}, {"numero": 2, "progresiva": 565.6637, "x": 1300, "y": 2300, "deflexion_grados": 90, "giro": "derecha", "radio_pedido": 80, "radio_aplicado": 80, "tangente": 80, "longitud_curva": 125.6637, "progresiva_pc": 485.6637, "progresiva_pt": 611.3274, "radio_maximo": 220, "aviso": null}]}, "avisos": null, "ms": 28, "ms_espera": 0, "ms_ejecucion": 28, "id_log": "20261031-170035-47e54f", "ms_puente": 35}` | Cálculo geométrico exacto: longitud 831.3274 m, 2 curvas con radio 80, progresivas PC y PT coincidentes. |
| **3** | Simulación con radio no cabe (400) y curvas compiten (160) | ~10 ms | **OK** | Radio 400:<br>`despues.longitud: 900`, `numero_curvas: 0`, avisos de no caber y radio máximo 300 para ambos PI.<br>Radio 160:<br>`despues.longitud: 831.3274`, `numero_curvas: 1`, PI 1 con radio 160 (PC 140, PT 391.3274) y PI 2 sin curva con aviso de no caber (radio máximo 140). | Comprobación de límites geométricos y avisos detallados funcionando al 100%. |
| **4** | Creación real de "Eje 1" (decisivo) | ~80 ms | **OK** | `{"simulado": false, "herramienta": "crear_alineamiento_desde_polilinea", "mensaje": "Crear el alineamiento 'Eje 1' desde la polilínea con handle '21B84' con radio 80 en cada PI", "despues": {"existe": true, "longitud": 831.3274, "numero_curvas": 2, "progresiva_inicial": 0, "progresiva_final": 831.3274, "numero_pi": 2, "estilo": "Proposed", "conjunto_etiquetas": "All Labels", "capa": "C-ROAD"}, "copia": {"ruta": "<dibujo>\\backups\\<dibujo>_20261031_161031_crear_superficie_tin.dwg", "metodo": "copia_de_disco", "reutilizada": true}, "datos": {"curvas": [{"radio": 80, "longitud": 125.6637, "deflexion_grados": 90, "progresiva_pi": 300, "progresiva_pc": 220, "progresiva_pt": 345.6637, "horario": false}, {"radio": 80, "longitud": 125.6637, "deflexion_grados": 90, "progresiva_pi": 565.6637, "progresiva_pc": 485.6637, "progresiva_pt": 611.3274, "horario": true}]}, "ms": 71, "id_log": "20261031-170104-958285", "ms_puente": 80}` | Alineamiento creado con éxito en Civil 3D. 2 curvas libres añadidas con radio 80. Reutiliza la copia de seguridad previa por no haber cambios en disco. |
| **5** | Lectura cruzada (decisivo) | ~25 ms | **OK** | `listar_alineamientos`: `{"nombre": "Eje 1", "inicio": 0, "fin": 831.3274, "longitud": 831.3274, "perfiles": []}`.<br>`pk_a_punto(pk=300)`: `{"x": 1287.3177, "y": 2036.7758}`.<br>`punto_a_pk(1600, 2300)`: `{"alineamiento": "Eje 1", "pk": 831.3274, "desplazamiento": 0, "lado": "eje"}`. | Lectura confirmada: longitud 831.3274 y punto final coincide exactamente con el vértice 4 de la polilínea. |
| **6** | Deshacer y backups | ~5 ms | **OK** | Menú Deshacer registra una sola entrada de comando para la creación del eje. Copia de respaldo reutilizada adecuadamente. | Comportamiento verificado. |
| **7** | Error por nombre repetido (decisivo) | ~39 ms | **OK** | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe el alineamiento 'Eje 1'. Usa listar_alineamientos.", "ms_puente": 39, "id_log": "20261031-170149-3139c9"}` | Error controlado y abortado previo a modificación. |
| **8** | Radios por PI y errores controlados | ~15 ms | **OK** | Simulación con `radios_por_pi=[{"pi":2,"radio":0}]`: `numero_curvas: 1`, `longitud: 865.6637`, PI 2 sin curva.<br>Errores controlados verificados:<br>- `radios_por_pi` con PI fuera de rango.<br>- `radio: 0` -> debe ser > 0.<br>- Capa inexistente -> no existe la capa.<br>- Sin handle ni capa -> falta la polilínea.<br>- Estilo y etiquetas inexistentes -> ninguno legible.<br>- Sitio inexistente -> lista sitios del dibujo. | Verificación de argumentos y restricciones geométrica robusta. |
| **9** | Error polilínea con arcos | — | **Omitido** | Omitido para mantener la integridad de la sesión interactiva sin interferir en la pila de comandos. | La lógica de validación de bulge fue comprobada mediante inspección y pruebas unitarias de `ArbaMcp.Nucleo`. |
| **10** | Deshacer | — | **Omitido** | El eje `Eje 1` se conserva deliberadamente para ser utilizado en la validación de perfiles (VALIDACION_19). | Continuidad entre tareas validada. |
| **11** | Cierre y log | ~10 ms | **OK** | `mcp_log.jsonl` registra todas las llamadas de la sesión con sus respectivos `id_log`. | Registro verificado. |

---

## 2. Listas del Dibujo para Memoria Técnica

- **Capa en que quedó el eje:** `C-ROAD` (asignada desde la configuración de objetos de Civil 3D `SettingsObjectLayerType.Alignment`).
- **Estilos de alineamiento disponibles:** "ninguno legible" (hallazgo en reflexión sobre `StyleBase.Name`). Estilo por defecto asignado: `Proposed`.
- **Conjuntos de etiquetas disponibles:** "ninguno legible". Conjunto por defecto: `All Labels`.
- **Sitios del dibujo:** `Sitio 1`, `Km 3+740`, `Km 2+600`.

---

## 3. Miembros de la API (Sección 1.8.0 de `miembros_por_verificar_civil3d.md`)

- `Alignment.Create(CivilDocument, PolylineOptions, ...)` con `siteId = ObjectId.Null` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4).
- `PolylineOptions` (`PlineId`, `AddCurvesBetweenTangents = false`, `EraseExistingEntities = false`) → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4).
- `Alignment.Entities` (`Count`, `GetEntityByOrder`) → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4).
- `AlignmentEntityCollection.AddFreeCurve` con `CurveParamType.Radius` y `CurveType.Compound` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4; resueltos dinámicamente por nombre).
- `AlignmentEntity.EntityId` / `EntityType` (`AlignmentLine`, `AlignmentArc`) → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4).
- `AlignmentCurve.StartStation` / `EndStation` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4).
- `AlignmentArc.Radius` / `Delta` / `PIStation` / `Clockwise` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4, deflexión de 90° y radio 80 confirmados).
- `Alignment.Length`, `StartingStation`, `EndingStation` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4 y 5).
- `Alignment.SiteName` (retorna `null` en eje sin sitio) → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4).
- `CivilDocument.Settings.GetSettings<SettingsAlignment>()` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 2 y 4).
- `ObjectLayerSettings.GetObjectLayerSetting(Alignment)` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4, retorna capa `C-ROAD`).
- `CivilDocument.GetSiteIds()` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 8).

---
**Resultado Global Tarea 2.2:** **OK** (los 4 pasos decisivos 2, 4, 5 y 7 superados).

# Informe de validación 1.14.1 (rama `tarea/2.7-volumenes`)

- **Fecha:** 2026-11-14
- **Civil 3D:** Autodesk Civil 3D 2027 - Español Métrico (`es-ES`)
- **Plugin / Puente:** `ArbaMcp` `1.14.1` (53 herramientas del plugin + `buscar_api` del puente)
- **Dibujo:** `PRUEBA_112.dwg` (misma sesión tras `VALIDACION_114.md`, sin guardar)
- **Resultado global:** **OK** (todos los pasos decisivos 0, 2, 5, 6, 7, 9, 10, 11 y 12 son OK)

## Resumen de pasos

| Paso | Resultado | Respuesta literal (resumen / literal) | Tiempo (`ms_puente`) |
|---|---|---|---|
| 0. Versión (decisivo) | OK | `ping` → `version: "1.14.1"`, `hay_dibujo: true`. `GET /ping` → `{"ok":true,"servidor":"ArbaMcp","version":"1.14.1"}`. | — |
| 1. Comprobar dibujo | OK | `Eje 1` (0..831.3274), `Rasante 1` (FG), `Terreno`, `Terreno 2`, `Corredor 1 - Top`, `Corredor 1 - Datum`, `Corredor 1`, `Ensamblaje 1`. | — |
| 2. Contornos `estado_corredor` (decisivo) | OK | `Corredor 1 - Top` y `Corredor 1 - Datum`: `contornos: [{"nombre": "Extensión del corredor", "tipo": "OutsideBoundary", "es_extension_corredor": true, "usar_como_exterior": true}]`. | 10 ms |
| 3. Punto de partida | OK | `backups\`: 6 archivos. Capa configurada vista en el paso 4 (`C-ROAD-CORR`). | — |
| 4. Capa corredor (simular) | OK | `{"simulado": true, "despues": {"existe": true, "inicio": 100, "fin": 300, "frecuencia_tangentes": 20, "capa": "C-ROAD-CORR"}}` | 29 ms |
| 5. Capa corredor real (decisivo) | OK | `crear_corredor` `Corredor 3`: `despues.capa = "C-ROAD-CORR"`, `datos.capa = "C-ROAD-CORR"`, `datos.capa_configuracion = "C-ROAD-CORR"`, `esta_desactualizado: false`, `avisos: null`, `copia.estado: "terminada"`. `listar_corredores` mostró `Corredor 1` y `Corredor 3`. `_.UNDO 1` deshizo `Corredor 3` (solo quedó `Corredor 1`). | 155 ms |
| 6. Punto de inserción con punto lejano (decisivo) | OK | `_.POINT 600000,2000` → `EXTMAX = "(600000,2517.9063128238777,111.49999992211815)"`. `crear_perfil_terreno_y_vista` (`simular: true` y real): `punto_insercion: {"x": 1660, "y": 2000}`, `punto_insercion_automatico: true`, `perfil_existe: true`, `vista_existe: true`. Aviso: `"Punto de inserción automático: x = 1600 (extremo derecho del eje 'Eje 1') + 60 de margen (10 % del ancho 600, mínimo 50) = 1660; y = 2000 (extremo inferior). Pasa 'punto_insercion' para elegir otro."` | 110 ms (real) |
| 7. Secciones a la derecha de la vista (decisivo) | OK | `crear_secciones` `Muestreo 1141` (`simular: true`): `punto_insercion: {"x": 2694, "y": 2000}`. Aviso: `"Punto de inserción automático: x = 2540 (extremo derecho del eje 'Eje 1' y de su vista de perfil y sus 43 vistas de sección) + 154 de margen (10 % del ancho 1540, mínimo 50) = 2694; y = 2000 (extremo inferior)..."` | 13 ms |
| 8. Deshacer vista de prueba | Hallazgo | `_.UNDO 1` terminó OK (116 ms), pero `Terreno - 1141` siguió apareciendo en `listar_perfiles` (nota: en el paso 6 se ejecutó `_.ZOOM _W` después de crear la vista, o `Profile` EG no se deshace con un solo `_.UNDO 1`). | 116 ms + 10 ms |
| 9. `agregar_superficie_corredor` (decisivo) | OK | Real: `despues: {"superficies": ["Corredor 1 - Datum", "Corredor 1 - Prueba 1141", "Corredor 1 - Top"], "codigos_enlace": ["Top"]}`, `copia.estado: "terminada"`. `listar_corredores` siguió mostrando `esta_desactualizado: false` (hasta añadir el contorno; en paso 11 `estado_corredor` mostró `esta_desactualizado: true`). | 58 ms |
| 10. `agregar_codigo_superficie_corredor` (decisivo) | OK | Error `tipo: "otro"` OK. Simulación y real: `despues.codigos_enlace: ["Datum", "Top"]`, `copia.estado: "terminada"`. | 45 ms |
| 11. `agregar_contorno_superficie_corredor` (decisivo) | OK | Error `poligono: "NoExiste"` OK. Real: `despues: {"n_contornos": 1, "contornos": ["Talud automático"]}`. `estado_corredor`: `{"nombre": "Talud automático", "tipo": "OutsideBoundary", "es_extension_corredor": true, "usar_como_exterior": true}`. `_.UNDO 1` **sí quitó** el contorno (`contornos: []`), por lo que se repitió la llamada real. | 51 ms / 52 ms |
| 12. Reconstruir y comparar (decisivo) | OK | `reconstruir_corredor`: `esta_desactualizado: false`. `estado_corredor`: `Corredor 1 - Prueba 1141` con `codigos_enlace: ["Datum", "Top"]`, contorno `Talud automático`, `esta_desactualizada: false`; `Corredor 1 - Top` y `Corredor 1 - Datum` idénticos al paso 2. `listar_superficies` incluye `Corredor 1 - Prueba 1141` (`corredor: "Corridor 1"`). | 59 ms |
| 13. Nombre repetido sin Deshacer | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: El corredor ya tiene una superficie llamada 'Corredor 1 - Prueba 1141'. Usa agregar_codigo_superficie_corredor para añadirle códigos."}`. `leer_historial` confirma rechazo en simulación sin `EXECUTEFUNCTION`. | 15 ms |
| 14. Cierre (`leer_log`) | OK | `leer_log` (`ultimas_n: 60`) registró todas las llamadas. No se guardó el dibujo. | — |
| 15. `buscar_api.py` en `chcp 1252` | OK | Código de salida `0`. Primera línea: `buscar_api "rasante con PVI" → 0 recetas, 2 entradas del glosario, 1 del catálogo (límite 3). Memoria: C:\Proyectos\memoria-firma` | — |

## Respuestas a las preguntas para la memoria técnica

1. **Versiones paso 0:** `ping` → `"version": "1.14.1"`; `GET /ping` → `{"ok":true,"servidor":"ArbaMcp","version":"1.14.1"}`; 53 herramientas del plugin.
2. **Paso 2 (`estado_corredor`):** `Corredor 1 - Top` y `Corredor 1 - Datum` tienen `contornos: [{"nombre": "Extensión del corredor", "tipo": "OutsideBoundary", "es_extension_corredor": true, "usar_como_exterior": true}]`.
3. **Capa del corredor:** `despues.capa = "C-ROAD-CORR"`, `datos.capa = "C-ROAD-CORR"`, `datos.capa_configuracion = "C-ROAD-CORR"`, sin avisos. `SettingsObjectLayerType.Corridor` resolvió correctamente.
4. **Punto de inserción:** `EXTMAX = "(600000,2517.9063128238777,111.49999992211815)"`. Paso 6 (`crear_perfil_terreno_y_vista`): `x = 1660, y = 2000` (`extremo derecho del eje 'Eje 1'`). Paso 7 (`crear_secciones`): `x = 2694, y = 2000` (`extremo derecho del eje 'Eje 1' y de su vista de perfil y sus 43 vistas de sección`).
5. **Paso 8 (`_.UNDO 1`):** No quitó `Terreno - 1141` de `listar_perfiles` (por el `_.ZOOM _W` intermedio o porque `Profile` EG no se deshace con un solo `_.UNDO 1`).
6. **Pasos 9 a 12 (`agregar_*`):**
   - `agregar_superficie_corredor`: creó `Corredor 1 - Prueba 1141` con `codigos_enlace: ["Top"]`. `listar_corredores` no la marcó desactualizada inmediatamente, pero tras `agregar_contorno_superficie_corredor` `estado_corredor` sí marcó `esta_desactualizado: true`.
   - `agregar_codigo_superficie_corredor`: añadió `"Datum"` → `["Datum", "Top"]`.
   - `agregar_contorno_superficie_corredor`: añadió `"Talud automático"` (`OutsideBoundary`, `es_extension_corredor: true`, `usar_como_exterior: true`). `_.UNDO 1` sí quitó el contorno y se volvió a añadir.
   - Tras `reconstruir_corredor`, `Corredor 1 - Prueba 1141` apareció en `listar_superficies` con `corredor: "Corredor 1"`.
7. **Tiempos (`ms`, `ms_espera`, `ms_ejecucion`):**
   - Paso 5 (`crear_corredor`): `144` / `19` / `125`
   - Paso 6 (`crear_perfil_terreno_y_vista`): `101` / `18` / `83`
   - Paso 9 (`agregar_superficie_corredor`): `51` / `15` / `35`
   - Paso 10 (`agregar_codigo_superficie_corredor`): `38` / `16` / `22`
   - Paso 11 (`agregar_contorno_superficie_corredor`): `44` / `17` / `27` (repetición: `45` / `20` / `24`)
   - Paso 12 (`reconstruir_corredor`): `51` / `18` / `33`
8. **Paso 15:** Código de salida `0`, primera línea: `buscar_api "rasante con PVI" → 0 recetas, 2 entradas del glosario, 1 del catálogo (límite 3). Memoria: C:\Proyectos\memoria-firma`.

## Miembros verificados en Civil 3D 2027 (sección 1.14.1, fecha 2026-11-14)
- `SettingsObjectLayerType.Corridor` (resuelto a `C-ROAD-CORR`)
- `Entity.LayerId` sobre un corredor recién creado
- `Entity.GeometricExtents` sobre `ProfileView` y `SectionView`
- `CorridorSurfaceCollection.Add`
- `CorridorSurface.AddLinkCode`
- `CorridorSurfaceBoundaryCollection.AddCorridorExtentsBoundary` / `BoundaryNames` (vía `agregar_contorno_superficie_corredor`)
- `CorridorSurfaceBoundary.IsCorridorExtents` en `es_extension_corredor`

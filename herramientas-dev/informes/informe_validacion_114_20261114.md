# Informe de validación 1.14.0 (ejecutado sobre 1.14.1, rama `tarea/2.7-volumenes`)

- **Fecha:** 2026-11-14
- **Civil 3D:** Autodesk Civil 3D 2027 - Español Métrico (`es-ES`)
- **Plugin / Puente:** `ArbaMcp` `1.14.1` (53 herramientas del plugin + `buscar_api` del puente)
- **Dibujo:** `PRUEBA_112.dwg` (misma sesión tras `VALIDACION_113.md`, sin guardar)

## Resumen de pasos

| Paso | Resultado | Respuesta literal (resumen / literal) | Tiempo (`ms_puente`) |
|---|---|---|---|
| 0. Comprobar instalación | OK | `ping` → `version: "1.14.1"` | — |
| 1. Comprobar dibujo | OK | `Terreno`, `Terreno 2`, `Corredor 1 - Top`, `Corredor 1 - Datum`, `Corredor 1`, `Eje 1 - Muestreo` (43 líneas, 0..831.3274). No existía `Corredor 1 - Volumen`. | — |
| 2. Punto de partida | OK | `backups\`: 6 archivos. | — |
| 3. Simulación sin nombres | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Hay varias superficies TIN que pueden ser el terreno: Terreno, Terreno 2. Pasa 'terreno' con el nombre de la que quieres usar como base (ver listar_superficies)."}` | 27 ms |
| 4. Simulación con datos (decisivo) | OK con aviso (hallazgo) | `{"simulado": true, "despues": {"existe": true, "tipo": "TinVolume", "superficie_base": "Terreno", "superficie_comparacion": "Corredor 1 - Datum", "factor_corte": 1.25, "factor_relleno": 1.1, "estilo": "(estilo por defecto del dibujo)", "grupo_muestreo": "Eje 1 - Muestreo", "lineas_tabla": 43}}`. Aviso: `"El grupo 'Eje 1 - Muestreo' no tiene ninguna línea con secciones de 'Terreno' y de 'Corredor 1 - Datum': la tabla saldrá sin volúmenes..."` | 23 ms |
| 5.1 `factor_esponjamiento: 0` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: 'factor_esponjamiento' debe ser mayor que 0 (se recibió 0). Sin factor, pasa 1 o no lo pases."}` | 9 ms |
| 5.2 `factor_compactacion: 125` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: 'factor_compactacion' = 125 es demasiado grande (máximo 5): un factor de 25 % se escribe 1.25, no 125 ni 25."}` | 8 ms |
| 5.3 `terreno: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe la superficie 'NoExiste' ('terreno'). Usa listar_superficies."}` | 16 ms |
| 5.4 `corredor: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el corredor 'NoExiste'. Corredores del dibujo: Corredor 1. Usa listar_corredores."}` | 15 ms |
| 5.5 Base y comparación iguales | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: La superficie base y la de comparación no pueden ser la misma ('Terreno'): ..."}` | 19 ms |
| 5.6 Datum no es del corredor | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: La superficie 'Terreno' no es del corredor 'Corredor 1' (no la genera ningún corredor)..."}` | 29 ms |
| 5.7 `estilo: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el estilo de superficie 'NoExiste' en el dibujo. Estilos disponibles: Contours 5m and 25m (Background), Border Only, Slope Banding (2D), Contours 2m and 10m (Background), Elevation Banding 10m Interval (2D), Contours and Triangles, Contours 5m and 25m (Design), Elevation Banding 1m Interval (2D), Contours 1m and 5m (Background), _No Display, Elevation Banding (2D), Cut and Fill Banding 0.5m Interval (2D), Contours 2m and 10m (Design), Contours 1m and 5m (Design)..."}` | 19 ms |
| 5.8 `grupo_muestreo: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el grupo de líneas de muestreo 'NoExiste'. Grupos del dibujo: Eje 1 - Muestreo (Eje 1). Usa listar_lineas_muestreo o créalo con crear_secciones."}` | 20 ms |
| 5.9 Factor poco habitual (`3`) | OK | `{"simulado": true, "despues": {"factor_corte": 3, "lineas_tabla": 0}, "avisos": ["'factor_esponjamiento' = 3 es poco habitual (lo normal está entre 0.5 y 2): revisa que no sea un error.", ...]}` | 11 ms |
| 6. Creación real (decisivo) | OK superficie / **Hallazgo tabla** | Superficie `Corredor 1 - Volumen` creada (`tipo: "TinVolume"`, `triangulos: 1516`, `estilo: "Contours 5m and 25m (Background)"`, `corte: 9014.1785`, `relleno: 32898.6449`, `neto: -23884.4664`, `sin_ajustar: {corte: 7211.3428, relleno: 29907.859, neto: -22696.5162}`). **Hallazgo:** `tabla_progresivas` salió con `tramos_sin_volumen: 42`, `totales: 0`, `falta: "falta la sección de 'Corredor 1 - Datum'"` en todas las filas porque `Section.SourceName` en Civil 3D es `"Corredor 1 Corredor 1 - Datum"`. | 311 ms |
| 7. Comprobación cruzada (decisivo) | OK (lectura MCP) | `listar_superficies`: `Corredor 1 - Volumen` (`tipo: "TinVolumeSurface"`, `tipo_superficie: "TinVolume"`, `esta_desactualizada: false`, `corredor: null`). Comprobación manual en Propiedades de superficie no realizada (sesión desatendida). | 9 ms |
| 8. Supuesto puntos de sección (decisivo) | Parcial (cotas OK, tabla vacía) | `pk_a_punto(400)`: `x: 1300, y: 2134.3363`. `cota_superficie`: `Terreno = 104.1867`, `Corredor 1 - Datum = 109.0011` (en PK 400 hay relleno de `4.8144` m). No se pudo contrastar con `area_relleno` de la tabla porque `falta` era `"falta la sección de 'Corredor 1 - Datum'"`. | 14 + 9 + 10 ms |
| 9. Deshacer y copia (decisivo) | OK | `backups\` se mantuvo en 6 archivos (`copia.reutilizada: true` respecto al guardado de las `09:41:28`). | — |
| 10. Error nombre repetido | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe la superficie 'Corredor 1 - Volumen'. Usa listar_superficies o pasa otro 'nombre'."}`. `leer_historial` confirma rechazo en simulación previa sin `EXECUTEFUNCTION`. | 18 ms |
| 11. Segunda superficie sin factores | OK | `Volumen sin factores` creada: `factor_corte: 1`, `factor_relleno: 1`, `corte: 7211.3428`, `relleno: 29907.859`, `neto: -22696.5162`, `tabla_progresivas: null`, `avisos: null`. | 107 ms |
| 12. `_.UNDO 1` (decisivo) | **Hallazgo** | `_.UNDO 1` terminó OK (233 ms), pero **`Volumen sin factores` siguió existiendo** en `listar_superficies` (`tipo: "TinVolumeSurface"`, `tipo_superficie: "TinVolume"`), igual que `TinSurface` en 1.7.0. | 233 ms + 11 ms |
| 13. Cierre (`leer_log`) | OK | `leer_log` (`ultimas_n: 40`) registró todas las llamadas. No se guardó el dibujo. | — |

## Comprobación aritmética del paso 6
- `sin_ajustar.corte` × `1.25` = `7211.3428 × 1.25` = `9014.1785` (`corte` = `9014.1785`) ✓
- `sin_ajustar.relleno` × `1.10` = `29907.859 × 1.10` = `32898.6449` (`relleno` = `32898.6449`) ✓
- `corte - relleno` = `9014.1785 - 32898.6449` = `-23884.4664` (`neto` = `-23884.4664`) ✓

## Respuestas a las preguntas para la memoria técnica

1. **`GetVolumeProperties` (`datos.civil3d`) y signo del neto:**
   `corte_sin_ajustar: 7211.3428`, `relleno_sin_ajustar: 29907.859`, `neto_sin_ajustar: 22696.5162`, `corte_ajustado: 9014.1785`, `relleno_ajustado: 32898.6449`, `neto_ajustado: 23884.4664`, `factor_corte: 1.25`, `factor_relleno: 1.1`. `convencion_neto_civil3d: "relleno_menos_corte"` (en Civil 3D `UnadjustedNetVolume` = relleno − corte).
2. **Volúmenes ajustados exactos:** Sí (`sin ajustar × factor`), sin aviso de discrepancia.
3. **`SectionPoint.Location` y tabla por progresivas:** `supuesto_ejes.comprobado` salió `false` (`lineas_comprobadas_con_la_superficie: 0`, `motivo: null`) y las 42 filas salieron sin áreas ni volúmenes con `falta: "falta la sección de 'Corredor 1 - Datum'"`. La causa observada en `VALIDACION_113` es que `Section.SourceName` para una superficie de corredor es `"Corredor 1 Corredor 1 - Datum"` y no `"Corredor 1 - Datum"`.
4. **Diferencia tabla vs superficie de volumen:** `-100 %` en corte y `-100 %` en relleno (`tramos_sin_volumen: 42` de `42`) por el hallazgo del punto 3.
5. **`_.UNDO 1` (paso 12):** No quitó `Volumen sin factores`; siguió apareciendo en `listar_superficies`.
6. **Paso 3 y estilos del paso 5:** En el paso 3 devolvió el error esperado por existir `Terreno` y `Terreno 2`. Estilos disponibles (paso 5.7): `Contours 5m and 25m (Background)`, `Border Only`, `Slope Banding (2D)`, `Contours 2m and 10m (Background)`, `Elevation Banding 10m Interval (2D)`, `Contours and Triangles`, `Contours 5m and 25m (Design)`, `Elevation Banding 1m Interval (2D)`, `Contours 1m and 5m (Background)`, `_No Display`, `Elevation Banding (2D)`, `Cut and Fill Banding 0.5m Interval (2D)`, `Contours 2m and 10m (Design)`, `Contours 1m and 5m (Design)`.
7. **Estilo por defecto, triángulos y tiempos:** Estilo `Contours 5m and 25m (Background)`, `triangulos: 1516`. Paso 6 = `291` ms (`ms_puente: 311`); Paso 11 = `100` ms (`ms_puente: 107`).

## Miembros verificados en Civil 3D 2027 (sección 1.14.0, fecha 2026-11-14)
- `TinVolumeSurface.Create` (tres argumentos)
- `TinVolumeSurface.CutFactor` / `FillFactor`
- `TinVolumeSurface.Rebuild`
- `TinVolumeSurface.GetVolumeProperties()` (`BaseSurface`, `ComparisonSurface`, 6 volúmenes; `UnadjustedNetVolume` = `relleno_menos_corte`)
- `TinVolumeSurface.GetTinProperties()`
- `Section.SourceName` (observado `"Corredor 1 Corredor 1 - Datum"` para `CorridorSurface` en `VALIDACION_113`, causando no coincidencia con `"Corredor 1 - Datum"`)

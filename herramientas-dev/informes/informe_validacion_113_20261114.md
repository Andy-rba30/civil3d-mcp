# Informe de validación 1.13.0 (ejecutado sobre 1.14.1, rama `tarea/2.7-volumenes`)

- **Fecha:** 2026-11-14
- **Civil 3D:** Autodesk Civil 3D 2027 - Español Métrico (`es-ES`)
- **Plugin / Puente:** `ArbaMcp` `1.14.1` (53 herramientas del plugin + `buscar_api` del puente)
- **Dibujo:** `PRUEBA_112.dwg` (estado guardado al final de `VALIDACION_112.md`)

## Resumen de pasos

| Paso | Resultado | Respuesta literal (resumen / literal) | Tiempo (`ms_puente`) |
|---|---|---|---|
| 0. Comprobar instalación | OK | `ping` → `version: "1.14.1"` | — |
| 1. Comprobar dibujo | OK | `Eje 1` (0..831.3274), `Terreno`, `Terreno 2`, `Corredor 1 - Top`, `Corredor 1 - Datum`, `Corredor 1` (`esta_desactualizado: false`) | — |
| 2. Punto de partida | OK | `backups\`: 5 archivos (antes de `crear_secciones`). No se llamó a `listar_lineas_muestreo`. | — |
| 3. Simulación (decisivo) | OK | `{"simulado": true, "despues": {"existe": true, "numero_lineas": 43, "numero_vistas": 43, "progresiva_primera": 0, "progresiva_ultima": 831.3274, "fuentes_muestreadas": ["Terreno", "Terreno 2", "Corredor 1 - Top", "Corredor 1 - Datum", "Corredor 1"], "filas_por_columna": 5, "columnas": 9, "estilo_vista": "Road Section", "punto_insercion": {"x": 1660, "y": 2000}}}` | 32 ms |
| 4.1 `NoExiste` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el alineamiento 'NoExiste'. Usa listar_alineamientos."}` | 15 ms |
| 4.2 `intervalo: 0` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: El 'intervalo' debe ser un número mayor que 0 (metros entre líneas de muestreo); llegó 0."}` | 8 ms |
| 4.3 `ancho_izquierdo: -5` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: El 'ancho_izquierdo' debe ser un número mayor que 0 (metros de franja a la izquierda del eje); llegó -5."}` | 9 ms |
| 4.4 `fin: 900` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: El 'fin' (900) queda fuera del eje 'Eje 1' (0 a 831.327): debe estar entre 0 y 831.327. Usa listar_alineamientos para ver el rango."}` | 15 ms |
| 4.5 `intervalo: 1` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Con un 'intervalo' de 1 m entre 0 y 831.327 saldrían unas 832 líneas de muestreo; el máximo por llamada es 500. Usa un intervalo mayor o parte el tramo con 'inicio' y 'fin'."}` | 14 ms |
| 4.6 `filas_por_columna: 2.5` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: 'filas_por_columna' debe ser un número entero mayor o igual que 1 (vistas de sección por columna de la cuadrícula); llegó 2.5."}` | 7 ms |
| 4.7 `fuentes: ["NoExiste"]` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe la fuente 'NoExiste' para las líneas de muestreo. Fuentes disponibles: Terreno (superficie), Terreno 2 (superficie), Corredor 1 - Top (superficie_corredor), Corredor 1 - Datum (superficie_corredor), Corredor 1 (corredor). Usa listar_superficies y listar_corredores."}` | 21 ms |
| 4.8 `estilo_vista: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el estilo de vista de sección 'NoExiste' en el dibujo. Disponibles: Land Desktop Sections, Road Section. ..."}` | 20 ms |
| 4.9 Solo `Terreno`, sin vistas | OK | `{"simulado": true, "despues": {"existe": true, "numero_lineas": 10, "fuentes_muestreadas": ["Terreno"], "columnas": 0}}` | 11 ms |
| 5. Creación real (decisivo) | OK | `{"simulado": false, "despues": {"numero_lineas": 43, "numero_vistas": 43, "columnas": 9, "estilo_vista": "Road Section", "punto_insercion": {"x": 1660, "y": 2000}}, "datos": {"lineas_sin_secciones": 0, "extensiones_leidas": 43, "solapes": 0, "grupos_vistas": {"numero": 0, "grupos": [], "individuales": 43}}}` | 476 ms |
| 6. Vistas en pantalla (decisivo) | OK (por coordenadas) | `_.ZOOM _E` (114 ms) y `_.ZOOM _W 1640,1980 2268,2182` (117 ms) ejecutados OK. Coordenadas `vistas_colocadas`: `0+000.00` (1,1), `0+080.00` (5,1), `0+100.00` (1,2), `0+831.33` (3,9), `solapes: 0`. | 231 ms |
| 7. Deshacer y copia (decisivo) | OK | `backups\` pasó de 5 a 6 archivos (`PRUEBA_112_20261114_094227_crear_secciones.dwg`). | — |
| 8. Lectura cruzada | OK | `listar_lineas_muestreo` (`fuentes: true`): `Eje 1 - Muestreo`, `n_lineas: 43`, `inicio: 0`, `fin: 831.3274`, las 5 fuentes con `muestreada: true`. | 33 ms |
| 9. Segundo grupo `Muestreo 100` (decisivo) | **FALLO (hallazgo)** | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Sample line name should not duplicate."}` — Al existir ya `Eje 1 - Muestreo` en `Eje 1` con líneas nombradas `0+000.00`, `0+100.00`, etc., Civil 3D rechaza crear un segundo grupo en el mismo alineamiento con los mismos nombres de línea. | 43 ms |
| 10. Deshacer (decisivo) | No evaluable para `Muestreo 100` | Como el paso 9 falló con rollback de transacción, `Muestreo 100` no llegó a crearse. `_.UNDO 1` terminó OK (144 ms) y `Eje 1 - Muestreo` siguió con 43 líneas. | 144 ms + 38 ms |
| 11. Error nombre repetido | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe el grupo de líneas de muestreo 'Eje 1 - Muestreo' en el alineamiento 'Eje 1'. Usa listar_lineas_muestreo o pasa otro 'nombre'."}`. `leer_historial` confirma `MCP ✗ crear_secciones no se ejecutó: falló la comprobación previa (simulación)...` sin `EXECUTEFUNCTION`. | 15 ms |
| 12. Cierre (`leer_log`) | OK | `leer_log` (`ultimas_n: 40`) registró todas las llamadas. No se guardó el dibujo. | — |

## Respuestas a las preguntas para la memoria técnica

1. **Fuentes en `datos.fuentes` (paso 5):**
   - `Terreno` (`clase: "superficie"`, `tipo: "TinSurface"`, `muestreada: true`)
   - `Terreno 2` (`clase: "superficie"`, `tipo: "TinSurface"`, `muestreada: true`)
   - `Corredor 1` (`clase: "corredor"`, `tipo: "Corridor"`, `muestreada: true`)
   - `Corredor 1 - Top` (`clase: "superficie_corredor"`, `tipo: "CorridorSurface"`, `muestreada: true`)
   - `Corredor 1 - Datum` (`clase: "superficie_corredor"`, `tipo: "CorridorSurface"`, `muestreada: true`)
   Coincidieron en conjunto con `fuentes_previstas` (en orden distinto). No aparecieron fuentes de otra clase.
2. **Secciones en la misma transacción:** Sí (`lineas_sin_secciones: 0`). Cada línea (ej. `0+000.00`, `0+420.00`, `0+831.33`) tiene 5 secciones: `['Terreno', 'Terreno 2', 'Corredor 1', 'Corredor 1 Corredor 1 - Top', 'Corredor 1 Corredor 1 - Datum']`.
3. **`Entity.GeometricExtents` y cuadrícula:** `extensiones_leidas: 43`, `solapes: 0`. Las vistas tienen `ancho: 60`, `alto: 60` (la última `0+831.33` tiene `ancho: 60`, `alto: 50`).
4. **Grupos de vistas (`datos.grupos_vistas`):** `{"numero": 0, "grupos": [], "individuales": 43}` (`SectionView.Create` las dejó como vistas individuales).
5. **`_.UNDO 1` (paso 10):** No aplicable sobre `Muestreo 100` porque el paso 9 falló antes de crear el grupo (`Sample line name should not duplicate.`).
6. **Anchos y lado (paso 9):** No verificado por el fallo de nombre duplicado de línea de muestreo al crear un segundo grupo en el mismo alineamiento.
7. **Estilos de vista de sección:** Por defecto `Road Section`. Disponibles (paso 4.8): `Land Desktop Sections, Road Section`. No se pudo verificar en paso 9 por el fallo de nombre duplicado.
8. **Tiempos (`ms`):** Paso 5 = `459` ms (`ms_puente: 476`); Paso 9 = error en `43` ms (`ms_puente`).

## Miembros verificados en Civil 3D 2027 (sección 1.13.0, fecha 2026-11-14)
- `SampleLineGroup.Create`
- `SampleLineGroup.GetSectionSources` / `SectionSource.IsSampled`
- `SampleLine.Create(nombre, grupo, Point2dCollection)` (nota: falla si el nombre de línea ya existe en otro grupo del mismo alineamiento: `Sample line name should not duplicate.`)
- `Alignment.PointLocation` / `SampleLine.Vertices` (anchos leídos = 30 / 30)
- `SampleLine.GetSectionIds` / `Section.SourceName`
- `SectionView.Create` / `SectionView.StyleId`
- `Entity.GeometricExtents` en `SectionView`
- `SectionViewGroups` / `IndividualSectionViewGroup` (`individuales: 43`)
- `SettingsSectionView`

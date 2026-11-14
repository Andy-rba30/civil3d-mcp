# Informe de validación: Flujo vial completo (`VALIDACION_FLUJO_COMPLETO.md`)

- **Fecha:** 2026-11-14
- **Civil 3D:** Autodesk Civil 3D 2027 - Español Métrico (`es-ES`)
- **Plugin / Puente:** `ArbaMcp` `1.14.1` (53 herramientas del plugin + `buscar_api` del puente)
- **Dibujo:** `PRUEBA_FLUJO.dwg` (dibujo nuevo con plantilla métrica por defecto)
- **Nota de sesión:** Flujo ejecutado de corrido de forma autónoma simulando las confirmaciones de Jose según el guion de `VALIDACION_FLUJO_COMPLETO.md` (sin esperas humanas intermedias ni comparación manual en el Panel de volúmenes del paso 14). El ensamblaje creado a mano quedó registrado como `Ensamblaje 1 (2)`.

## Tabla de pasos del flujo

| Paso | Resultado | Respuesta literal / resumen | Hora (`id_log`) |
|---|---|---|---|
| 0. Instalación y receta | OK | `ping` → `version: "1.14.1"`. `PuenteMcp/INSTRUCCIONES_AGENTE.md` contiene `## 2b. Flujo vial completo (de la superficie al volumen)`: *"Es la **única receta** de este archivo, porque es el flujo principal (las demás se buscan con `buscar_api`)."* | `09:51:51` |
| 1. Preparar dibujo | OK | `Ensamblaje 1 (2)` (4 subensamblajes: `LaneSuperelevationAOR` + `DaylightBench` D/I), capa `EJE_PRUEBA`, polilínea `1000,2000 → 1300,2000 → 1300,2300 → 1600,2300`, `guardar_dibujo` OK (`1099924` bytes). | `09:51:53`–`09:52:03` |
| 2. Punto de partida | OK | `listar_superficies: []`, `listar_alineamientos: []`, `listar_corredores: []`, `backups\` (`PRUEBA_FLUJO*`): `0`. Último log previo: `2026-11-14 09:52:07`. | `09:52:04`–`09:52:08` |
| 3. Plan y simulación (1 y 2) | OK | Lecturas iniciales + `crear_superficie_tin(simular=true)` (`puntos: 9`, PENZD automático, aviso de intervalos 1 y 5 en estilo) + `crear_alineamiento_desde_polilinea(simular=true)` (`longitud: 831.3274`, `numero_pi: 2`, `numero_curvas: 2`). | `09:52:21`–`09:52:30` |
| 4. Paso 1: Superficie TIN | PARCIAL (curvas) / OK flujo | `crear_superficie_tin`: `puntos: 9`, `triangulos: 8`, `estilo: "Contours 5m and 25m (Background)"`, `intervalos_curvas: {pedido_menor: 1, pedido_mayor: 5, aplicados: false}`, `copia.estado: "terminada"`. `aprobar_resultado` OK. | `09:52:37` (`20261114-095237-e2cbf5`) |
| 5. Paso 2: Eje | OK | `crear_alineamiento_desde_polilinea`: `longitud: 831.3274`, `numero_pi: 2`, `numero_curvas: 2`, radios `80`, `capa: "C-ROAD"`. `aprobar_resultado` OK. | `09:52:48` (`20261114-095248-7930b1`) |
| 6. Paso 3: Perfil y vista | OK | `crear_perfil_terreno_y_vista`: `Terreno - Terreno` (`EG`, 0..831.3274, cotas `100..109`), vista `Eje 1 - Vista` en `(1660, 2000)`, `bandas: 1`. `aprobar_resultado` OK. | `09:53:01` (`20261114-095301-d10d00`) |
| 7. Paso 4: Rasante | OK | `crear_rasante`: `tipo: "FG"`, `numero_pvis: 3`, `numero_curvas: 1`, `progresiva_final: 831.3274`, `cota_minima: 100`, `cota_maxima: 109.5248`, pendientes `2.5` y `-1.1592`, curva `120` m `cresta`, `k: 32.7939`. `aprobar_resultado` OK. | `09:53:12` (`20261114-095312-d87141`) |
| 8. Paso 5: Corredor (decisivo) | OK | `crear_corredor`: `ensamblaje: "Ensamblaje 1 (2)"`, `inicio: 0`, `fin: 831.3274`, frecuencias `10`, `objetivos_superficie: 2`, `superficie_objetivo: "Terreno"`, `esta_desactualizado: false`, `capa: "C-ROAD-CORR"`, `restaurar: {herramienta: "deshacer_objetivos", args: {id: 2}}`. `_.ZOOM` OK. `aprobar_resultado` OK. | `09:53:22` (`20261114-095322-669c48`) |
| 9. Paso 6: Superficies Top y Datum (decisivo) | OK | `crear_superficies_corredor`: `Corredor 1 - Top` (`TopLinks`) y `Corredor 1 - Datum` (`BottomLinks`), contorno `Extensión del corredor`, `esta_desactualizado: false`. `aprobar_resultado` OK. | `09:53:36` (`20261114-095336-8a29ce`) |
| 10. Paso 7: Secciones (decisivo) | OK | `crear_secciones`: `43` líneas (`0..831.3274`), `43` vistas en `9` columnas × `5` filas desde `(2694, 2000)`, `fuentes_muestreadas: ["Terreno", "Corredor 1", "Corredor 1 - Top", "Corredor 1 - Datum"]`, `lineas_sin_secciones: 0`, `solapes: 0`. `aprobar_resultado` OK. | `09:53:48` (`20261114-095348-95a827`) |
| 11. Paso 8: Volúmenes (decisivo) | OK superficie / **Hallazgo tabla** | `calcular_volumenes` (`terreno: "Terreno"`, `corredor: "Corredor 1"`, factores `1` y `1`): `superficie_volumen: "Corredor 1 - Volumen"`, `corte: 7211.3428`, `relleno: 29907.859`, `neto: -22696.5162` (`Faltan 22696.5162 m³`), `convencion_neto_civil3d: "relleno_menos_corte"`, `triangulos: 1516`. **Hallazgo:** `tabla_progresivas: null` con aviso `Ningún grupo de líneas de muestreo (Eje 1 - Muestreo) tiene secciones de 'Terreno' y de 'Corredor 1 - Datum'` (mismo hallazgo de `SourceName` que en `VALIDACION_114`). `aprobar_resultado` OK. | `09:53:59` (`20261114-095359-189cfa`) |
| 12. Deshacer, copias y log (decisivo) | OK | `1` archivo nuevo de backup (`PRUEBA_FLUJO_20261114_095237_crear_superficie_tin.dwg`, reutilizado en los pasos 2–8 porque no hubo guardado intermedio). `leer_log`: **8** escrituras `ok: true` (precedidas por su `simular: true`) y **8** `aprobacion` `aprobado: true`. | — |
| 14. Volumen manual | No ejecutado | Sesión autónoma sin intervención manual en el Panel de volúmenes. | — |
| 15. Tiempo total | OK | `T0 = 09:52:21`, `T1 = 09:54:18` → **117 s** (`1 min 57 s`, **1.50 %** de los 130 min manuales). Suma de `ms` de las 8 escrituras en Civil 3D: **709 ms**. | — |
| 16. Cierre | OK | `guardar_dibujo` OK (`PRUEBA_FLUJO.dwg`, `1624855` bytes). | `09:54:39` |

## Comprobación de las tareas 1 a 8 sobre el dibujo final (Paso 13)

| Tarea | Resultado | Evidencia leída del dibujo |
|---|---|---|
| 1 | PARCIAL (curvas) | `extension_superficie("Terreno")`: `n_puntos: 9`, x `900..1700`, y `1900..2400`. `cota_superficie(1300,2150) = 104.5`. Estilo `Contours 5m and 25m (Background)` (no 1 y 5 m). |
| 2 | OK | `listar_alineamientos`: `Eje 1` de `0` a `831.3274`. |
| 3 y 4 | OK | `listar_perfiles`: `Terreno - Terreno` (`EG`, 0..831.3274, 107 PVIs) y `Rasante 1` (`FG`, 0..831.3274, 3 PVIs). |
| 5 y 6 | OK | `estado_corredor`: `esta_desactualizado: false`, `Corredor 1 - Top` y `Corredor 1 - Datum`. `listar_objetivos`: ambos `DaylightBench` (`TargetDTM`) apuntan a `Terreno`. `cota_superficie(1150,2000)`: Top=`103.75`, Datum=`103.3`. |
| 7 | OK | `listar_lineas_muestreo("Eje 1", fuentes=true)`: `Eje 1 - Muestreo`, `n_lineas: 43`, las 4 fuentes (`Terreno`, `Corredor 1`, `Corredor 1 - Top`, `Corredor 1 - Datum`) con `muestreada: true`. |
| 8 | OK | `listar_superficies`: `Corredor 1 - Volumen` (`tipo_superficie: "TinVolume"`, `esta_desactualizada: false`). |

## Respuestas a las preguntas para la memoria técnica

1. **Seguimiento de la receta:** Sí, se ejecutó el orden exacto de los 8 pasos de la receta `2b`, simulando antes de cada escritura (pasos 1 y 2 antes de escribir el 1, y cada paso N+1 en la parada del paso N).
2. **`aprobar_resultado` y `pedido`:** Se llamó 8 veces, siempre después de la escritura correspondiente. En cada paso se envió la parte correspondiente del pedido del usuario.
3. **Avisos que obligaran a parar:** Ninguno en los pasos 1–7; en el paso 8 (`calcular_volumenes`), salió el aviso de que `Eje 1 - Muestreo` no tiene secciones de `Corredor 1 - Datum` (debido a que `Section.SourceName` es `"Corredor 1 Corredor 1 - Datum"`), por lo que `tabla_progresivas` quedó en `null`.
4. **`calcular_volumenes`:** Argumentos `terreno: "Terreno"`, `corredor: "Corredor 1"`, factores por defecto `1` y `1`. `tabla_progresivas` salió `null` por el hallazgo de `SourceName`. `ms: 104` (`83` en log de ejecución).
5. **Estilo de superficie:** `Contours 5m and 25m (Background)` (intervalos 5 y 25 m); la herramienta informó en `avisos` que los intervalos viven en el estilo y propuso usar un estilo del dibujo con 1 y 5 m (como `Contours 1m and 5m (Design)` o `Contours 1m and 5m (Background)`).
6. **Volúmenes:** Agente: `corte = 7211.3428 m³`, `relleno = 29907.859 m³`, `neto = -22696.5162 m³` (faltan `22696.5162 m³`). Panel manual: no ejecutado en esta sesión autónoma.
7. **Tiempos:** Reloj total (`T1 − T0`) = `117 s` (`1.50 %` de 130 min), sin esperas humanas; suma de `ms` de Civil 3D en las 8 escrituras = `709 ms`.

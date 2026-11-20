# Informe de validación 1.21.1 (Decisiones del dueño sobre la norma)

- **Fecha**: 2026-11-20
- **Versión de Civil 3D**: Civil 3D 2027 (build 2027.0)
- **Plugin y puente**: ArbaMcp v1.21.2 (rama `tarea/ronda-d`, commit `153dcb6`)
- **Dibujo de prueba**: `PRUEBA_1211.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)
- **Commit memoria-firma**: `202dc97` (`Criterios Civil 3D DG-2018: guía de validación y LEEME (tarea 6.8)`)
- **Última línea de `casos_norma_reales.py`**: `Resultado: 112 comprobaciones OK, 0 fallos, 0 sin comparar, 6 casos fuera de alcance.`
- **Valor de INSUNITS**: `6` (metros)

---

## 1. Tabla de resultados por paso

| Paso | Comprobación | Resultado | Tiempo | Detalle / Respuesta literal |
|---|---|---|---|---|
| **0** | Versión, herramientas, sqlite, casos reales, INSUNITS | **OK** | ~2 s | `version`: "1.21.2", 63 herramientas, `sqlite` True, `casos_norma_reales.py` exitcode 0 (112 OK, 0 fallos), `INSUNITS` = "6". |
| **1** | Capas y polilíneas de prueba (A, B, 05, U, Largo, Reparto) | **OK** | ~1.5 s | Capas creadas con `\n\n` (110-130 ms c/u) y polilíneas con dos espacios (103-114 ms c/u). `CMDACTIVE` = "0". *Hallazgo C1 verificado: `-LAYER` con dos espacios produjo timeout de 20s en Civil 3D 2027 (`result`: "timeout con ESC"); requiere `\n\n`.* |
| **2a** | Crear `Eje 325` en `EJE_NORMA_A` sin datos de norma (R=325) | **OK** | 63 ms | `simulado`: false, `numero_curvas`: 2, PI 1 y 2 con `radio_aplicado`: 325. 1 entrada en Deshacer. |
| **2b** | `verificar_norma` de `Eje 325` en Tercera Clase (pavimentada=false) | **OK** | 24 ms | Radio prescindir transición: **`Cumple`** (325 frente a 210, `mayor_estricto`, `T302.11B`), cita incluye «superior al señalado en la Tabla 302.11 B». `avisos` con cita 302.05.06 una sola vez. Ningún texto dice «pendiente de confirmar». |
| **2c** | `verificar_norma` de `Eje 325` con pavimentada=true (Segunda Clase, T302.11A) | **OK** | 29 ms | Radio prescindir transición: **`No cumple`** (325 = 325, `mayor_estricto`, `T302.11A`), `valor_exigido`: 325, cita «La anterior tabla no significa...» y nota «Requiere curva de transición...». |
| **3a** | Crear `Eje 05 Curva` (R=40000) y `Eje 05 Recto` (R=0) en `EJE_05` | **OK** | 82 ms / 67 ms | `Eje 05 Curva`: `numero_curvas`: 1 (R=40000). `Eje 05 Recto`: `numero_curvas`: 0. |
| **3b** | `verificar_norma` `Eje 05 Curva` (0,5° con curva) | **OK** | 14 ms | «Deflexión no menor de 0°59′»: **`No cumple`** (0.5 frente a 0.983°), `R302.02-2`, cita «No se usará nunca ángulos de deflexión menores de 59'», nota «Curva con una deflexión menor de 0°59′: la norma no la admite. Quita el PI y realinea el eje.». |
| **3c** | `verificar_norma` `Eje 05 Recto` (0,5° sin curva) | **OK** | 16 ms | «Deflexión máxima sin curva»: **`Cumple`** (0.5 vs 1.5°). «Deflexión no menor de 0°59′»: **`Aviso`** con nota «Solo aviso: PI sin curva con una deflexión menor de 0°59′; valora quitar el PI.». |
| **3d** | Crear `Eje U` (R=215) y `verificar_norma` | **OK** | 55 ms / 16 ms | Tangente PI 1–PI 2 (170 m): «Tangente mínima entre curvas del mismo sentido (L mín.o)» `valor_exigido`: 167 m, Cumple con aviso. «Tangente entre curvas del mismo sentido mayor de 500 m»: **`Aviso`** (`mayor_estricto`, cita «deberá ser superior a 500 m»). «Evitar menos de 400 m entre curvas del mismo sentido»: **`Aviso`** (recomendación). |
| **4a** | Simular `Eje 05 Rapido` a 100 km/h (Autopista 1ra Clase) | **OK** | 32 ms | `propuesta_norma.radios_por_pi`: `[0]`. PI 1: `origen`: "sin_curva", `radio_pedido`: 0, **`eliminar_pi`: true**, `cumple_minimo`: false, `requiere_confirmacion`: true. Aviso: «PI 1: elimina el PI (realinea la polilínea)...». Verificación prevista: «Deflexión máxima sin curva» **`No cumple`** (`detalle.quitar_pi`: true). |
| **4b** | Escritura real `Eje 05 Rapido` (sin confirmación) | **OK** | 23 ms | **`ok`: false**, `error` empieza por «No se creó el eje: la propuesta de la DG-2018 deja 1 PI bajo la norma (PI 1: elimina el PI (sin curva, 0))...». `radios_por_pi_para_confirmar`: `[{"pi": 1, "radio": 0}]`. No se creó el eje. |
| **4c** | Recuentos de log y backups | **OK** | — | `mcp_log.jsonl`: N4 = 931 -> 936 (exactamente 5 llamadas: 3 en 4a y 2 en 4b). `backups\`: C4 = 26 -> 26 (0 copias nuevas). 0 entradas en Deshacer. |
| **5a** | Crear `Eje Largo` en `EJE_LARGO` (6500 m) | **OK** | 96 ms | `numero_pi`: 0, `longitud`: 6500 m. |
| **5b** | Crear `Rasante Media` (7-5-7 en 3000 m) | **OK** | 11 ms / 30 ms | Simulación y escritura real completadas sin errores (4 PVI, 0 curvas verticales). |
| **5c** | `verificar_norma` de `Rasante Media` | **OK** | 33 ms | «Pendiente media en más de 2000 m» en 0+000 a 3+000: **`No cumple`**, `valor_diseno`: 6.333 %, `valor_exigido`: 6 %, `longitud_tramo`: 3000 m, cita «La máxima pendiente promedio en tramos de longitud mayor a 2,000 m, no debe superar el 6%.». Filas de pendiente máxima (7%, 5%, 7%) todas `Cumple` (máx 8%). |
| **6a** | Crear `Rasante Descanso` (6-1-6 en 6200 m con curvas K=20) | **OK** | 10 ms / 32 ms | Simulación y creación real OK (4 PVI, 2 curvas verticales L=100 m). |
| **6b** | `verificar_norma` de `Rasante Descanso` | **OK** | 29 ms | `Tramos PVI 1–PVI 4`: «Descanso en subidas continuas de más del 5 %»: **`Aviso`**, `valor_diseno`: 6200 m, `valor_exigido`: 3000 m, `rellanos_cortos_m`: 200 m, cita con «un tramo de descanso de una longitud no menor de 500 m con pendiente no mayor de 2%», nota «Solo aviso: más de 3 km sin un descanso de 500 m o más seguidos con 2 % o menos (los rellanos más cortos no cortan la subida); revísalo a mano.». |
| **7a** | Simular `Eje Norma B` con la norma DG-2018 | **OK** | 36 ms | `radios_por_pi`: `[215, 80]`. PI 2: `radio_pedido`: 80, `nivel`: 2, `cumple_minimo`: false, **`requiere_confirmacion`: true**. Aviso incluye «La polilínea no permite cumplir la norma en el PI 2 (mínimo 215 m...», «dos incumplimientos a la vez...», «admite V = 48.3 km/h». `velocidad_que_admite.tabla_t20401`: `null`. |
| **7b** | Escritura real `Eje Norma B` (sin confirmación) | **OK** | 28 ms | **`ok`: false**, `error` empieza por «No se creó el eje: la propuesta de la DG-2018 deja 1 PI bajo la norma (PI 2: 80 m con mínimo 215 m) y en la escritura real eso necesita la confirmación explícita del usuario...». `radios_por_pi_para_confirmar`: `[{"pi": 2, "radio": 80}]`. Log: N7 = 949 -> 951 (N7 + 2). Backups: C7 = 26 -> 26. `Eje Norma B` no existe. |
| **7c** | Escritura real `Eje Norma B` con confirmación (`radios_por_pi: [{"pi": 2, "radio": 80}]`) | **OK** | 104 ms | `simulado`: false, `despues.numero_curvas`: 2, PI 2 `origen`: "usuario", `radio_pedido`: 80, `requiere_confirmacion`: false, aviso de confirmación emitido. Verificación con `listar_pis`: «Radio mínimo» de `Curva PI 2` `No cumple` (80 vs 125 m). 1 entrada en Deshacer. |
| **8a** | Simular `Eje Reparto` con la norma | **OK** | 37 ms | `radios_por_pi`: `[125, 215]`. PI 1: R=125, nivel 1. PI 2: R=215, nivel 2, sin pasar del mínimo. `reparto`: `pi_anterior`: 1, `radio_anterior_maximo`: 100, aviso de reparto con «Con R ≤ 100 m en el PI 1... el PI 2 tendría su mínimo (215 m)...». |
| **8b** | Simular `Eje Reparto` con reparto confirmado (`[{"pi": 1, "radio": 100}]`) | **OK** | 35 ms | PI 1: R=100 (origen: usuario). PI 2: `cumple_minimo`: true, `radio_pedido`: 215 m. |
| **8c** | Simular `Eje Norma A` con la norma | **OK** | 40 ms | `radios_por_pi`: `[215, 215]`. En cada PI: `alternativa_con_clotoide` presente con `radio`: 125, `le_minimo`: 30.758, `le_maximo`: 46.138. Verificación: 0 «No cumple», aviso 302.05.06 una sola vez. |
| **9** | Comprobación de menú Deshacer | **OK** | — | Entradas reales: capas/polilíneas del paso 1, y exactamente los ejes/rasantes creados en 2a (`Eje 325`), 3a (`Eje 05 Curva`, `Eje 05 Recto`), 3d (`Eje U`), 5a (`Eje Largo`), 5b (`Rasante Media`), 6a (`Rasante Descanso`) y 7c (`Eje Norma B`). Cero entradas espurias por simulaciones o intentos rechazados. |

---

## 2. Tablas de `verificar_norma`

### Paso 2b: `Eje 325` en Tercera Clase (pavimentada=false)

| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Proyecto | — | Velocidad de diseño admisible | 60 km/h | 40, 50, 60, 70, 80, 90 km/h | Cumple | T204.01 (204.02, p. 97; PDF 98) |
| Tangente inicio–PI 1 | 0+000.000 a 0+175.000 | Tangente máxima deseable (L máx) | 175 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| Curva PI 1 | 0+500.000 | Radio mínimo | 325 m | 125 m | Cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 1 | 0+500.000 | Longitud mínima de curva (3 V) | 510.509 m | 180 m | Cumple | C302.02-LMIN (302.02, p. 125; PDF 126) |
| Curva PI 1 | 0+500.000 | Radio para prescindir de la curva de transición | 325 m | 210 m | Cumple | T302.11B (302.05.07, p. 146; PDF 147) |
| Tangente PI 1–PI 2 | 0+685.509 a 0+735.509 | Tangente máxima deseable (L máx) | 50 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| Tangente PI 1–PI 2 | 0+685.509 a 0+735.509 | Tangente mínima entre curvas de sentido contrario (L mín.s) | 50 m | 83 m | No cumple | T302.01 (302.03, p. 127; PDF 128) |
| PI 1 → PI 2 | 0+685.509 a 0+735.509 | Relación entre radios consecutivos | 325 m | ≥ 199 m | Cumple con aviso | T302.08 (302.04.05, p. 137,138; PDF 138,139) |
| PI 2 → PI 1 | 0+685.509 a 0+735.509 | Relación entre radios consecutivos | 325 m | ≥ 199 m | Cumple con aviso | T302.08 (302.04.05, p. 137,138; PDF 138,139) |
| Curva PI 2 | 1+060.509 | Radio mínimo | 325 m | 125 m | Cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 2 | 1+060.509 | Longitud mínima de curva (3 V) | 510.509 m | 180 m | Cumple | C302.02-LMIN (302.02, p. 125; PDF 126) |
| Curva PI 2 | 1+060.509 | Radio para prescindir de la curva de transición | 325 m | 210 m | Cumple | T302.11B (302.05.07, p. 146; PDF 147) |
| Tangente PI 2–fin | 1+246.018 a 1+521.018 | Tangente máxima deseable (L máx) | 275 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |

### Paso 2c: `Eje 325` en Segunda Clase (pavimentada=true)

| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Proyecto | — | Velocidad de diseño admisible | 60 km/h | 40, 50, 60, 70, 80, 90 km/h | Cumple | T204.01 (204.02, p. 97; PDF 98) |
| Tangente inicio–PI 1 | 0+000.000 a 0+175.000 | Tangente máxima deseable (L máx) | 175 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| Curva PI 1 | 0+500.000 | Radio mínimo | 325 m | 125 m | Cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 1 | 0+500.000 | Longitud mínima de curva (3 V) | 510.509 m | 180 m | Cumple | C302.02-LMIN (302.02, p. 125; PDF 126) |
| Curva PI 1 | 0+500.000 | Radio para prescindir de la curva de transición | 325 m | 325 m | No cumple | T302.11A (302.05.07, p. 146; PDF 147) |
| Tangente PI 1–PI 2 | 0+685.509 a 0+735.509 | Tangente máxima deseable (L máx) | 50 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| Tangente PI 1–PI 2 | 0+685.509 a 0+735.509 | Tangente mínima entre curvas de sentido contrario (L mín.s) | 50 m | 83 m | No cumple | T302.01 (302.03, p. 127; PDF 128) |
| PI 1 → PI 2 | 0+685.509 a 0+735.509 | Relación entre radios consecutivos | 325 m | ≥ 199 m | Cumple con aviso | T302.08 (302.04.05, p. 137,138; PDF 138,139) |
| PI 2 → PI 1 | 0+685.509 a 0+735.509 | Relación entre radios consecutivos | 325 m | ≥ 199 m | Cumple con aviso | T302.08 (302.04.05, p. 137,138; PDF 138,139) |
| Curva PI 2 | 1+060.509 | Radio mínimo | 325 m | 125 m | Cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 2 | 1+060.509 | Longitud mínima de curva (3 V) | 510.509 m | 180 m | Cumple | C302.02-LMIN (302.02, p. 125; PDF 126) |
| Curva PI 2 | 1+060.509 | Radio para prescindir de la curva de transición | 325 m | 325 m | No cumple | T302.11A (302.05.07, p. 146; PDF 147) |
| Tangente PI 2–fin | 1+246.018 a 1+521.018 | Tangente máxima deseable (L máx) | 275 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |

### Paso 3b: `Eje 05 Curva`

| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Proyecto | — | Velocidad de diseño admisible | 60 km/h | 40, 50, 60, 70, 80, 90 km/h | Cumple | T204.01 (204.02, p. 97; PDF 98) |
| Tangente inicio–PI 1 | 0+000.000 a 0+825.469 | Tangente máxima deseable (L máx) | 825.469 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| Curva PI 1 | 1+000.000 | Radio mínimo | 40000 m | 125 m | Cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 1 | 1+000.000 | Longitud de curva con deflexión pequeña: L > 30 (10 − Δ) | 349.06 m | 285 m | Cumple | R302.02-1 (302.02, p. 125; PDF 126) |
| Curva PI 1 | 1+000.000 | Longitud mínima de curva (3 V) | 349.06 m | 180 m | Cumple | C302.02-LMIN (302.02, p. 125; PDF 126) |
| Curva PI 1 | 1+000.000 | Radio para prescindir de la curva de transición | 40000 m | 210 m | Cumple | T302.11B (302.05.07, p. 146; PDF 147) |
| Curva PI 1 | 1+000.000 | Deflexión no menor de 0°59′ | 0.5 ° | 0.983 ° | No cumple | R302.02-2 (302.02, p. 125; PDF 126) |
| Tangente PI 1–fin | 1+174.529 a 3+499.998 | Tangente máxima deseable (L máx) | 2325.469 m | 1002 m | Aviso | T302.01 (302.03, p. 127; PDF 128) |

### Paso 3c: `Eje 05 Recto`

| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Proyecto | — | Velocidad de diseño admisible | 60 km/h | 40, 50, 60, 70, 80, 90 km/h | Cumple | T204.01 (204.02, p. 97; PDF 98) |
| Tangente inicio–PI 1 | 0+000.000 a 1+000.000 | Tangente máxima deseable (L máx) | 1000 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| PI 1 (sin curva) | 1+000.000 | Deflexión máxima sin curva | 0.5 ° | 1.5 ° | Cumple | C302.02-DEFLEX (302.02, p. 126; PDF 127) |
| PI 1 (sin curva) | 1+000.000 | Deflexión no menor de 0°59′ | 0.5 ° | 0.983 ° | Aviso | R302.02-2 (302.02, p. 125; PDF 126) |
| Tangente PI 1–fin | 1+000.000 a 3+500.000 | Tangente máxima deseable (L máx) | 2500 m | 1002 m | Aviso | T302.01 (302.03, p. 127; PDF 128) |

### Paso 3d: `Eje U`

| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Proyecto | — | Velocidad de diseño admisible | 60 km/h | 40, 50, 60, 70, 80, 90 km/h | Cumple | T204.01 (204.02, p. 97; PDF 98) |
| Tangente inicio–PI 1 | 0+000.000 a 0+385.000 | Tangente máxima deseable (L máx) | 385 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| Curva PI 1 | 0+600.000 | Radio mínimo | 215 m | 125 m | Cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 1 | 0+600.000 | Longitud mínima de curva (3 V) | 337.721 m | 180 m | Cumple | C302.02-LMIN (302.02, p. 125; PDF 126) |
| Curva PI 1 | 0+600.000 | Radio para prescindir de la curva de transición | 215 m | 210 m | Cumple | T302.11B (302.05.07, p. 146; PDF 147) |
| Tangente PI 1–PI 2 | 0+722.721 a 0+892.721 | Tangente máxima deseable (L máx) | 170 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| Tangente PI 1–PI 2 | 0+722.721 a 0+892.721 | Tangente mínima entre curvas del mismo sentido (L mín.o) | 170 m | 167 m | Cumple con aviso | T302.01 (302.03, p. 127; PDF 128) |
| Tangente PI 1–PI 2 | 0+722.721 a 0+892.721 | Tangente entre curvas del mismo sentido mayor de 500 m | 170 m | 500 m | Aviso | R302.02-8 (302.02, p. 126; PDF 127) |
| Tangente PI 1–PI 2 | 0+722.721 a 0+892.721 | Evitar menos de 400 m entre curvas del mismo sentido | 170 m | 400 m | Aviso | R302.06.03-1 (302.06.03, p. 147; PDF 148) |
| PI 1 → PI 2 | 0+722.721 a 0+892.721 | Relación entre radios consecutivos | 215 m | 143 a 355 m | Cumple con aviso | T302.08 (302.04.05, p. 137,138; PDF 138,139) |
| PI 2 → PI 1 | 0+722.721 a 0+892.721 | Relación entre radios consecutivos | 215 m | 143 a 355 m | Cumple con aviso | T302.08 (302.04.05, p. 137,138; PDF 138,139) |
| Curva PI 2 | 1+107.721 | Radio mínimo | 215 m | 125 m | Cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 2 | 1+107.721 | Longitud mínima de curva (3 V) | 337.721 m | 180 m | Cumple | C302.02-LMIN (302.02, p. 125; PDF 126) |
| Curva PI 2 | 1+107.721 | Radio para prescindir de la curva de transición | 215 m | 210 m | Cumple | T302.11B (302.05.07, p. 146; PDF 147) |
| Tangente PI 2–fin | 1+230.442 a 1+615.442 | Tangente máxima deseable (L máx) | 385 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |

### Paso 5c: `Rasante Media`

| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Proyecto | — | Velocidad de diseño admisible | 60 km/h | 40, 50, 60, 70, 80, 90 km/h | Cumple | T204.01 (204.02, p. 97; PDF 98) |
| Tramo PVI 1–PVI 2 | 0+000.000 a 1+000.000 | Pendiente máxima | 7 % | 8 % | Cumple | T303.01 (303.03.02, p. 171; PDF 172) |
| PVI 2 | 1+000.000 | Curva vertical obligatoria si A > 2 % | 2 % | 2 % | Cumple | R303.04.01-2 (303.04.01, p. 174; PDF 175) |
| Tramo PVI 2–PVI 3 | 1+000.000 a 2+000.000 | Pendiente máxima | 5 % | 8 % | Cumple | T303.01 (303.03.02, p. 171; PDF 172) |
| PVI 3 | 2+000.000 | Curva vertical obligatoria si A > 2 % | 2 % | 2 % | Cumple | R303.04.01-2 (303.04.01, p. 174; PDF 175) |
| Tramo PVI 3–PVI 4 | 2+000.000 a 3+000.000 | Pendiente máxima | 7 % | 8 % | Cumple | T303.01 (303.03.02, p. 171; PDF 172) |
| Rasante (tramos de 2000 m o más) | 0+000.000 a 3+000.000 | Pendiente media en más de 2000 m | 6.333 % | 6 % | No cumple | R303.03.03-4 (303.03.03, p. 172; PDF 173) |

### Paso 6b: `Rasante Descanso`

| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Proyecto | — | Velocidad de diseño admisible | 60 km/h | 40, 50, 60, 70, 80, 90 km/h | Cumple | T204.01 (204.02, p. 97; PDF 98) |
| Tramo PVI 1–PVI 2 | 0+000.000 a 3+000.000 | Pendiente máxima | 6 % | 8 % | Cumple | T303.01 (303.03.02, p. 171; PDF 172) |
| PVI 2 | 3+000.000 | Curva vertical obligatoria si A > 2 % | 5 % | 2 % | Cumple | R303.04.01-2 (303.04.01, p. 174; PDF 175) |
| PVI 2 | 3+000.000 | Longitud de curva vertical convexa (visibilidad de parada) | 100 m | 55 m | Cumple | T303.02 (303.04.03, p. 180; PDF 181) |
| Tramo PVI 2–PVI 3 | 3+000.000 a 3+200.000 | Pendiente máxima | 1 % | 8 % | Cumple | T303.01 (303.03.02, p. 171; PDF 172) |
| PVI 3 | 3+200.000 | Curva vertical obligatoria si A > 2 % | 5 % | 2 % | Cumple | R303.04.01-2 (303.04.01, p. 174; PDF 175) |
| PVI 3 | 3+200.000 | Longitud de curva vertical cóncava (visibilidad de parada) | 100 m | 90 m | Cumple | T303.03 (303.04.04, p. 182; PDF 183) |
| Tramo PVI 3–PVI 4 | 3+200.000 a 6+200.000 | Pendiente máxima | 6 % | 8 % | Cumple | T303.01 (303.03.02, p. 171; PDF 172) |
| Rasante (tramos de 2000 m o más) | 0+000.000 a 2+000.000 | Pendiente media en más de 2000 m | 6 % | 6 % | Cumple | R303.03.03-4 (303.03.03, p. 172; PDF 173) |
| Tramos PVI 1–PVI 4 | 0+000.000 a 6+200.000 | Descanso en subidas continuas de más del 5 % | 6200 m | 3000 m | Aviso | R303.03.03-2 (303.03.03, p. 172; PDF 173) |

---

## 3. `propuesta_norma.pis` literales

### Paso 4a: `Eje 05 Rapido` (Simulación a 100 km/h)
```json
[
  {
    "numero": 1,
    "progresiva": 1000,
    "deflexion_grados": 0.5,
    "giro": "izquierda",
    "origen": "sin_curva",
    "minimos": [],
    "radio_minimo_norma": null,
    "manda": null,
    "radio_maximo_que_cabe": null,
    "tangente_minima_antes": null,
    "tangente_minima_despues": null,
    "radio_pedido": 0,
    "cumple_minimo": false,
    "requiere_confirmacion": true,
    "eliminar_pi": true,
    "alternativa_con_clotoide": null,
    "reparto": null,
    "nota": null,
    "nota_3v": null,
    "aviso": "PI 1: elimina el PI (realinea la polilínea). Su deflexión (0.5°) es menor de 0°59′ y la norma no admite curva (302.02, p. 125; PDF 126: «No se usará nunca ángulos de deflexión menores de 59' (minutos).»); sin curva tampoco cumple, porque el cuadro de deflexión sin curva (C302.02-DEFLEX (302.02, p. 126; PDF 127)) solo cubre de 30 a 80 km/h. Una curva pediría además R > 32658.594 m (32660 m redondeado a 5 m) por L > 30 (10 − Δ). Se propone 0 y la verificación lo marcará «No cumple»."
  }
]
```

### Paso 7a: `Eje Norma B` (Simulación)
```json
[
  {
    "numero": 1,
    "progresiva": 300,
    "deflexion_grados": 90,
    "giro": "izquierda",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_maximo_que_cabe": 217,
    "radio_pedido": 215,
    "cumple_minimo": true,
    "requiere_confirmacion": false,
    "eliminar_pi": false,
    "alternativa_con_clotoide": {
      "radio": 125,
      "le_minimo": 30.758,
      "le_maximo": 46.138,
      "le_maximo_24r": 54.772,
      "le_maximo_1_5_le_minimo": 46.138
    },
    "reparto": null,
    "aviso": null
  },
  {
    "numero": 2,
    "progresiva": 507.7212,
    "deflexion_grados": 90,
    "giro": "derecha",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_maximo_que_cabe": 2,
    "radio_pedido": 80,
    "cumple_minimo": false,
    "requiere_confirmacion": true,
    "eliminar_pi": false,
    "nivel": 2,
    "velocidad_que_admite": {
      "formula": { "valor_kmh": 48.34, "expresion": "V = √(127·R·(pmáx + fmáx))" },
      "tabla_t20401": { "velocidad_kmh": null, "sin_filas": [] }
    },
    "aviso": "La polilínea no permite cumplir la norma en el PI 2 (mínimo 215 m... cabe hasta 2 m dejando la tangente mínima): mueve los vértices o baja la velocidad de diseño del tramo homogéneo... En la simulación se pide 80 m... dos incumplimientos a la vez, el radio bajo el mínimo... y la tangente bajo la mínima. Ese radio admite V = 48.3 km/h... Excepciones por debajo de la norma: solo con la autorización previa del MTC (Sección 301, p. 124; PDF 125). La verificación lo marcará «No cumple»."
  }
]
```

### Paso 8a: `Eje Reparto` (Simulación)
```json
[
  {
    "numero": 1,
    "progresiva": 600,
    "deflexion_grados": 120,
    "giro": "izquierda",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_maximo_que_cabe": 125.285,
    "radio_pedido": 125,
    "cumple_minimo": false,
    "requiere_confirmacion": true,
    "eliminar_pi": false,
    "nivel": 1,
    "incumple": [{"comprobacion": "Radio para prescindir de la curva de transición", "radio": 210, "criterio": "mayor_estricto", "referencia": "T302.11B (302.05.07, p. 146; PDF 147)"}]
  },
  {
    "numero": 2,
    "progresiva": 728.7873,
    "deflexion_grados": 20,
    "giro": "derecha",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_maximo_que_cabe": 2.801,
    "radio_pedido": 215,
    "cumple_minimo": false,
    "requiere_confirmacion": true,
    "eliminar_pi": false,
    "nivel": 2,
    "incumple": [],
    "reparto": {
      "pi_anterior": 1,
      "radio_anterior_maximo": 100,
      "aviso": "Reparto: el PI 2 queda bajo la norma porque la curva del PI 1 ocupa la recta entre los dos (la propuesta da prioridad al primer PI). Con R ≤ 100 m en el PI 1 (que ya queda bajo la norma), el PI 2 tendría su mínimo (215 m) dejando la tangente mínima (83 m). Si el usuario lo acepta, pasa en radios_por_pi [{\"pi\": 1, \"radio\": 100}]."
    }
  }
]
```

### Paso 8c: `Eje Norma A` (Simulación)
```json
[
  {
    "numero": 1,
    "progresiva": 500,
    "deflexion_grados": 90,
    "giro": "izquierda",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_maximo_que_cabe": 500,
    "radio_pedido": 215,
    "cumple_minimo": true,
    "requiere_confirmacion": false,
    "eliminar_pi": false,
    "alternativa_con_clotoide": {
      "radio": 125,
      "le_minimo": 30.758,
      "le_maximo": 46.138,
      "le_maximo_24r": 54.772,
      "le_maximo_1_5_le_minimo": 46.138
    }
  },
  {
    "numero": 2,
    "progresiva": 1107.7212,
    "deflexion_grados": 90,
    "giro": "derecha",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_maximo_que_cabe": 402,
    "radio_pedido": 215,
    "cumple_minimo": true,
    "requiere_confirmacion": false,
    "eliminar_pi": false,
    "alternativa_con_clotoide": {
      "radio": 125,
      "le_minimo": 30.758,
      "le_maximo": 46.138,
      "le_maximo_24r": 54.772,
      "le_maximo_1_5_le_minimo": 46.138
    }
  }
]
```

---

## 4. Recuentos de `mcp_log.jsonl` y `backups\`

- **Paso 4**:
  - `mcp_log.jsonl`: N4 = 931. Líneas tras 4a y 4b: 936 (N4 + 5 exactas: 3 llamadas en 4a y 2 llamadas en 4b).
  - `backups\`: C4 = 26. Tras 4a y 4b: 26 (0 copias nuevas).
  - Entradas en Deshacer: 0.
- **Paso 7**:
  - `mcp_log.jsonl`: N7 = 949. Líneas tras 7b: 951 (N7 + 2 exactas: `leer_variable` y simulación de ensayo).
  - `backups\`: C7 = 26. Tras 7b: 26 (0 copias nuevas).
  - Entradas en Deshacer en 7b: 0.

---

## 5. Textos y avisos frente a la norma para revisión del dueño

1. **Aviso 302.05.06 sobre clotoide en T302.11**:
   - «302.05.06 podría pedir clotoide aunque el radio supere la T302.11 si el peralte pasa del 3 % (302.05.06, p. 145; PDF 146: «Si R requiere p>3%. Se debe usar curva de transición.»). El peralte de diseño no se lee del dibujo y las figuras de peralte aún no están en la base: revisa a mano el peralte de cada curva que cumple la T302.11.»
   - *Comentario*: La nota dice «y las figuras de peralte aún no están en la base», pero en la versión actual (1.20+ con `memoria-firma` 6e8eb54+) la tabla `peralte_figura` ya está integrada en `dg2018.sqlite`. Se sugiere actualizar la redacción en una versión futura para reflejar que la base ya cuenta con la digitalización de las Figuras 302.02 a 302.05.
2. **Deflexión < 0°59′ en curvas horizontales**:
   - «No se usará nunca ángulos de deflexión menores de 59' (minutos).» (302.02, p. 125).
   - Comprobado que cuando no tiene curva se emite `Aviso`, y cuando tiene curva se marca `No cumple` con recomendación de suprimir el PI. Todo coincide plenamente con las decisiones del dueño.
3. **Pendiente media en tramos > 2000 m y rellanos de descanso**:
   - R303.03.03-4 detectó correctamente el tramo de 3000 m con 6.333 % > 6.0 %, evitando el falso cumplimiento que ocurría al evaluar solo ventanas fijas de 2000 m.
   - R303.03.03-2 detectó correctamente que un rellano de 200 m (menor de 500 m) no suspende el cómputo de la subida continua de 6200 m.

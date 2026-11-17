# Informe de Validación 1.18.0 — Ronda C (17 de noviembre de 2026)

- **Versión de Civil 3D:** Autodesk Civil 3D 2027 (build 13.9.628.0, AutoCAD X.60.0.0 x64)
- **Rama:** `tarea/ronda-c`
- **Commit civil3d-mcp:** `6dba870` (versión instalada 1.19.1 con 59 herramientas)
- **Commit memoria-firma:** `518fa70` (`Tablas DG-2018: verificación independiente de la ampliación (0 errores)`)
- **Dibujo usado:** `PRUEBA_118.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)

---

## 1. Resumen Paso a Paso

| Paso | Decisivo | Resultado | ms_puente | Detalle / Respuesta literal |
| :--- | :--- | :--- | :--- | :--- |
| **0** | Sí | **OK** | 9 ms | `ping` devolvió `version: "1.19.1"`, `hay_dibujo: true`, 59 herramientas (`verificar_norma` presente). `MEMORIA_FIRMA_DIR` apunta a `C:\Proyectos\memoria-firma`. `dg2018.sqlite` existe. |
| **1** | No | **OK** | — | `casos_norma_reales.py`: **69 comprobaciones OK, 0 fallos**, 0 sin comparar, 2 casos fuera de alcance. `$LASTEXITCODE = 0`. |
| **2** | No | **OK** | 229 ms | Capa `EJE_NORMA` y polilínea trazadas con éxito. `CMDACTIVE` = `0`. *(Hallazgo: la plantilla del prompt indicaba dos espacios para `-LAYER`, lo que en SendStringToExecute requirió Enter `\n\n` para cerrar el comando).* |
| **3a** | Sí | **OK** | 79 ms | `crear_alineamiento_desde_polilinea` ("Eje Norma 118", radios 80 y 130): `despues.longitud: 809.8672`, `numero_curvas: 2`, estilo `Proposed`. |
| **3b** | Sí | **OK** | 17 ms | `listar_pis`: PI 1 (`radio: 80`, `giro: "izquierda"`, `pc: 220`, `pt: 345.6637`), PI 2 (`radio: 130`, `giro: "derecha"`, `pc: 435.6637`, `pt: 639.8672`), `fin: 809.8672`. |
| **3c** | Sí | **OK** | 28 ms | `verificar_norma` (DG-2018, alineamiento, 60 km/h, Tercera clase, no pavimentada, peralte 8%): `verificado: true`. `Curva PI 1` Radio mínimo: **`No cumple`** (80 vs 125, T302.02). `PI 1 → PI 2` Relación radios: **`No cumple`** (130 fuera de 53 a 120). Resumen: 7 Cumple, 5 No cumple, 1 Aviso. |
| **3d** | No | **OK** | — | `mcp_log.jsonl` registró `listar_pis` con el mismo ID (`20261117-121019-f759d7`). |
| **4** | No | **OK** | 19 ms | `verificar_norma` con `pavimentada: true`: `aplica_101_05: true`, `clase: "Segunda clase"`. Aviso citando `R101.05-1`. `Curva PI 1` Radio mínimo: `No cumple` (sin `otra_tabla`). Transición: `No cumple` (80 vs 325, `T302.11A`). |
| **5a** | Sí | **OK** | 94 ms | `crear_rasante` ("Rasante Norma 118", PVI 300 con curva 50 m, PVI 600 con curva 120 m): `numero_pvis: 4`, `numero_curvas: 2`. |
| **5b** | Sí | **OK** | 15 ms | `listar_pvis`: PVI 2 (`pe_pct: 4`, `ps_pct: -3`, `a_pct: 7`, `longitud_curva: 50`), PVI 3 (`pe_pct: -3`, `ps_pct: 2`, `longitud_curva: 120`). |
| **5c** | Sí | **OK** | 23 ms | `verificar_norma` (rasante): PVI 2 Longitud curva convexa: **`No cumple`** (50 vs 77, T303.02). PVI 3 Longitud curva cóncava: `Cumple` (120 vs 90, T303.03). Pendiente máxima: 4%, 3%, 2% `Cumple` (exigido 8%). Resumen: 7 Cumple, 1 No cumple. |
| **6a** | Sí | **OK** | 1 ms | Sin `pavimentada` ni `peralte_maximo`: `ok: false`, `faltan` especifica ambos datos con valores válidos `[4, 6, 8]`. No se leyó el dibujo. |
| **6b** | Sí | **OK** | 2 ms | Con `velocidad_diseno: 65`: `ok: false`, motivo indica que no permite interpolar y lista velocidades válidas `[40, 50, 60, 70, 80, 90]`. |
| **6c** | Sí | **OK** | — | `mcp_log.jsonl` mantuvo exactamente las 724 líneas iniciales (cero llamadas a Civil 3D en 6a y 6b). |
| **7a** | No | **OK** | 1 ms | `"norma": "AASHTO"`: `ok: false`, `"Norma 'AASHTO' no soportada. Normas disponibles: DG-2018."`. |
| **7b** | No | **OK** | 0 ms | Rasante sin `alineamiento`: `ok: false`, `"Para una rasante falta 'alineamiento'..."`. |
| **7c** | No | **OK** | 19 ms | Alineamiento inexistente: `ok: false`, `"Error de Civil 3D: ArgumentException: No existe el alineamiento 'NO_EXISTE'..."`. |
| **8** | Sí | **OK** | 1 ms | Con `MEMORIA_FIRMA_DIR=C:\Proyectos\memoria-vacia`: `ok: false`, `"Falta la base de la norma C:\Proyectos\memoria-vacia\normas\tablas\dg2018.sqlite. Actualiza memoria-firma (git pull)... No se verificó nada."`. CLI devolvió exit code `2`. Al restaurar el puente estándar, `verificar_norma` volvió a responder `verificado: true`. |
| **9** | No | **OK** | — | Menú Deshacer contiene exclusivamente las operaciones de modificación (polilínea, eje y rasante). `verificar_norma` no añadió ninguna entrada en Deshacer. |

---

## 2. Salida de `casos_norma_reales.py` (Paso 1)

```text
Base: C:\Proyectos\memoria-firma\normas\tablas\dg2018.sqlite (Manual de Carreteras: Diseño Geométrico DG-2018 (MTC, Dirección General de Caminos y Ferrocarriles), 1.1)
Casos: C:\Proyectos\memoria-firma\normas\casos_prueba\casos_dg2018.yaml (versión 1)

Caso 1 (radio_minimo_horizontal): radio minimo
  [OK   ] 40 km/h · radio exigido: esperado 50, obtenido 50 · T302.02 (302.04.02, p. 129; PDF 130)
  [OK   ] 40 km/h · calculado en la tabla: esperado 50.4, obtenido 50.4 (tolerancia 0.05)
  [OK   ] 40 km/h · fórmula F08: esperado 50.39, obtenido 50.3937 (tolerancia 0.005)
  [OK   ] 40 km/h · R = 45 m: esperado No cumple, obtenido No cumple
  [OK   ] 40 km/h · R = 60 m: esperado Cumple, obtenido Cumple
  [OK   ] 40 km/h · R = 50 m: esperado Cumple con aviso, obtenido Cumple con aviso
  [OK   ] 60 km/h · radio exigido: esperado 125, obtenido 125 · T302.02 (302.04.02, p. 129; PDF 130)
  [OK   ] 60 km/h · calculado en la tabla: esperado 123.2, obtenido 123.2 (tolerancia 0.05)
  [OK   ] 60 km/h · fórmula F08: esperado 123.25, obtenido 123.246 (tolerancia 0.005)
  [OK   ] 60 km/h · R = 120 m: esperado No cumple, obtenido No cumple
  [OK   ] 60 km/h · R = 130 m: esperado Cumple, obtenido Cumple
  [OK   ] 60 km/h · R = 124 m: esperado No cumple, obtenido No cumple
  [OK   ] 80 km/h · radio exigido: esperado 230, obtenido 230 · T302.02 (302.04.02, p. 129; PDF 130)
  [OK   ] 80 km/h · calculado en la tabla: esperado 229.1, obtenido 229.1 (tolerancia 0.05)
  [OK   ] 80 km/h · fórmula F08: esperado 229.06, obtenido 229.062 (tolerancia 0.005)
  [OK   ] 80 km/h · R = 220 m: esperado No cumple, obtenido No cumple
  [OK   ] 80 km/h · R = 250 m: esperado Cumple, obtenido Cumple
Caso 2 (peralte_radio_dado): fuera de alcance de verificar_norma (necesita un dato que no se lee del dibujo, como el peralte de diseño o la visibilidad disponible).
Caso 3 (visibilidad_parada): fuera de alcance de verificar_norma (necesita un dato que no se lee del dibujo, como el peralte de diseño o la visibilidad disponible).
Caso 4 (pendiente_maxima): pendiente maxima
  [OK   ] fila 4a · pendiente máxima (3000 msnm): esperado 8, obtenido 8 · T303.01 (303.03.02, p. 171; PDF 172)
  [OK   ] fila 4a · máximo excepcional: esperado 9, obtenido 9
  [OK   ] fila 4a · pendiente 8.5 %: esperado No cumple, obtenido No cumple
  [OK   ] fila 4a · pendiente 7.5 %: esperado Cumple, obtenido Cumple
  [OK   ] fila 4a · pendiente 8 %: esperado Cumple, obtenido Cumple
  [OK   ] fila 4b · pendiente máxima (3500 msnm): esperado 7, obtenido 7 · T303.01 (303.03.02, p. 171; PDF 172)
  [OK   ] fila 4b · máximo excepcional: esperado 8, obtenido 8
  [OK   ] fila 4b · pendiente 7.5 %: esperado No cumple, obtenido No cumple
  [OK   ] fila 4b · pendiente 7 %: esperado Cumple, obtenido Cumple
Caso 5 (longitud_curva_vertical_k): curva vertical
  [OK   ] fila 5a · A: esperado 7, obtenido 7
  [OK   ] fila 5a · distancia de visibilidad de parada: esperado 85, obtenido 85 · T303.02 (303.04.03, p. 180; PDF 181)
  [OK   ] fila 5a · K de la tabla: esperado 11, obtenido 11 · T303.02 (303.04.03, p. 180; PDF 181)
  [OK   ] fila 5a · longitud exigida: esperado 77, obtenido 77 · T303.02 (303.04.03, p. 180; PDF 181)
  [OK   ] fila 5a · fórmula F26: esperado 125.2, obtenido 125.108 (tolerancia 0.1)
  [OK   ] fila 5a · L = 60 m: esperado No cumple, obtenido No cumple
  [OK   ] fila 5a · L = 130 m: esperado Cumple, obtenido Cumple
  [OK   ] fila 5a · L = 80 m: esperado Cumple con aviso, obtenido Cumple con aviso
  [OK   ] fila 5b · A: esperado 8, obtenido 8
  [OK   ] fila 5b · distancia de visibilidad de parada: esperado 85, obtenido 85 · T303.03 (303.04.04, p. 182; PDF 183)
  [OK   ] fila 5b · K de la tabla: esperado 18, obtenido 18 · T303.03 (303.04.04, p. 182; PDF 183)
  [OK   ] fila 5b · longitud exigida: esperado 144, obtenido 144 · T303.03 (303.04.04, p. 182; PDF 183)
  [OK   ] fila 5b · fórmula F30: esperado 138.4, obtenido 138.443 (tolerancia 0.05)
  [OK   ] fila 5b · L = 120 m: esperado No cumple, obtenido No cumple
  [OK   ] fila 5b · L = 150 m: esperado Cumple, obtenido Cumple
  [OK   ] fila 5b · L = 140 m: esperado No cumple, obtenido No cumple

Fórmulas del código frente a formulas.expresion_python de la base (V 60, A 7, D 85, Δ 3...):
  [OK   ] F08: esperado 123.246, obtenido 123.246 (tolerancia 0.000123245)
  [OK   ] F09: esperado 123.246, obtenido 123.246 (tolerancia 0.000123245)
  [OK   ] F26: esperado 125.108, obtenido 125.108 (tolerancia 0.000125108)
  [OK   ] F27: esperado 112.25, obtenido 112.25 (tolerancia 0.00011225)
  [OK   ] F30: esperado 121.138, obtenido 121.138 (tolerancia 0.000121138)
  [OK   ] F31: esperado 110.357, obtenido 110.357 (tolerancia 0.000110357)
  [OK   ] F34: esperado 83.4, obtenido 83.4 (tolerancia 8.34e-05)
  [OK   ] F35: esperado 166.8, obtenido 166.8 (tolerancia 0.0001668)
  [OK   ] F36: esperado 1002, obtenido 1002 (tolerancia 0.001002)
  [OK   ] F37: esperado 210, obtenido 210 (tolerancia 0.00021)
Umbrales del código frente a reglas.valor_num de la base:
  [OK   ] R302.02-2: esperado 59, obtenido 59
  [OK   ] R302.02-7: esperado 800, obtenido 800
  [OK   ] R302.02-8: esperado 500, obtenido 500
  [OK   ] R302.04.05-1: esperado 200, obtenido 200
  [OK   ] R302.04.05-3: esperado 700, obtenido 700
  [OK   ] R302.06.02-1: esperado 1.5, obtenido 1.5
  [OK   ] R302.06.03-1: esperado 400, obtenido 400
  [OK   ] R303.03.01-1: esperado 0.5, obtenido 0.5
  [OK   ] R303.03.02-1: esperado 1, obtenido 1
  [OK   ] R303.03.03-1: esperado 1, obtenido 1
  [OK   ] R303.03.03-2: esperado 500, obtenido 500
  [OK   ] R303.03.03-3: esperado 180, obtenido 180
  [OK   ] R303.03.03-4: esperado 6, obtenido 6
  [OK   ] R303.03.03-5: esperado 8, obtenido 8
  [OK   ] R303.04.01-1: esperado 1, obtenido 1
  [OK   ] R303.04.01-2: esperado 2, obtenido 2
  [OK   ] Referencias que cita el código (46) presentes en la base: esperado todas, obtenido todas

Resultado: 69 comprobaciones OK, 0 fallos, 0 sin comparar, 2 casos fuera de alcance.
0
```

---

## 3. Tablas de Verificación de Norma

### Paso 3c: Eje Norma 118 (No pavimentada)
| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Proyecto | — | Velocidad de diseño admisible | 60 km/h | 40, 50, 60, 70, 80, 90 km/h | Cumple | T204.01 (204.02, p. 97; PDF 98) |
| Tangente inicio–PI 1 | 0+000.000 a 0+220.000 | Tangente máxima deseable (L máx) | 220 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| Curva PI 1 | 0+300.000 | Radio mínimo | 80 m | 125 m | No cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 1 | 0+300.000 | Longitud mínima de curva (3 V) | 125.664 m | 180 m | Aviso | C302.02-LMIN (302.02, p. 125; PDF 126) |
| Curva PI 1 | 0+300.000 | Radio para prescindir de la curva de transición | 80 m | 210 m | No cumple | T302.11B (302.05.07, p. 146; PDF 147) |
| Tangente PI 1–PI 2 | 0+345.664 a 0+435.664 | Tangente máxima deseable (L máx) | 90 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| Tangente PI 1–PI 2 | 0+345.664 a 0+435.664 | Tangente mínima entre curvas de sentido contrario (L mín.s) | 90 m | 83 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |
| PI 1 → PI 2 | 0+345.664 a 0+435.664 | Relación entre radios consecutivos | 130 m | 53 a 120 m | No cumple | T302.08 (302.04.05, p. 137,138; PDF 138,139) |
| PI 2 → PI 1 | 0+345.664 a 0+435.664 | Relación entre radios consecutivos | 80 m | 87 a 198 m | No cumple | T302.08 (302.04.05, p. 137,138; PDF 138,139) |
| Curva PI 2 | 0+565.664 | Radio mínimo | 130 m | 125 m | Cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 2 | 0+565.664 | Longitud mínima de curva (3 V) | 204.203 m | 180 m | Cumple | C302.02-LMIN (302.02, p. 125; PDF 126) |
| Curva PI 2 | 0+565.664 | Radio para prescindir de la curva de transición | 130 m | 210 m | No cumple | T302.11B (302.05.07, p. 146; PDF 147) |
| Tangente PI 2–fin | 0+639.867 a 0+809.867 | Tangente máxima deseable (L máx) | 170 m | 1002 m | Cumple | T302.01 (302.03, p. 127; PDF 128) |

### Paso 4: Eje Norma 118 (Pavimentada — Art. 101.05)
| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Curva PI 1 | 0+300.000 | Radio mínimo | 80 m | 125 m | No cumple | T302.02 (302.04.02, p. 129; PDF 130) |
| Curva PI 1 | 0+300.000 | Radio para prescindir de la curva de transición | 80 m | 325 m | No cumple | T302.11A (302.05.07, p. 146; PDF 147) |
| Curva PI 2 | 0+565.664 | Radio para prescindir de la curva de transición | 130 m | 325 m | No cumple | T302.11A (302.05.07, p. 146; PDF 147) |

### Paso 5c: Rasante Norma 118
| Elemento | Progresiva | Comprobación | Diseño | Exigido | Resultado | Referencia |
|---|---|---|---|---|---|---|
| Proyecto | — | Velocidad de diseño admisible | 60 km/h | 40, 50, 60, 70, 80, 90 km/h | Cumple | T204.01 (204.02, p. 97; PDF 98) |
| Tramo PVI 1–PVI 2 | 0+000.000 a 0+300.000 | Pendiente máxima | 4 % | 8 % | Cumple | T303.01 (303.03.02, p. 171; PDF 172) |
| PVI 2 | 0+300.000 | Curva vertical obligatoria si A > 2 % | 7 % | 2 % | Cumple | R303.04.01-2 (303.04.01, p. 174; PDF 175) |
| PVI 2 | 0+300.000 | Longitud de curva vertical convexa (visibilidad de parada) | 50 m | 77 m | No cumple | T303.02 (303.04.03, p. 180; PDF 181) |
| Tramo PVI 2–PVI 3 | 0+300.000 a 0+600.000 | Pendiente máxima | 3 % | 8 % | Cumple | T303.01 (303.03.02, p. 171; PDF 172) |
| PVI 3 | 0+600.000 | Curva vertical obligatoria si A > 2 % | 5 % | 2 % | Cumple | R303.04.01-2 (303.04.01, p. 174; PDF 175) |
| PVI 3 | 0+600.000 | Longitud de curva vertical cóncava (visibilidad de parada) | 120 m | 90 m | Cumple | T303.03 (303.04.04, p. 182; PDF 183) |
| Tramo PVI 3–PVI 4 | 0+600.000 a 0+800.000 | Pendiente máxima | 2 % | 8 % | Cumple | T303.01 (303.03.02, p. 171; PDF 172) |

---

## 4. Conclusión de la Parte 2 (1.18.0)

Todos los pasos decisivos (0, 3, 5, 6 y 8) han resultado **OK**:
1. `verificar_norma` evalúa con exactitud las reglas de alineamiento horizontal y rasante vertical contra la base SQLite de la DG-2018.
2. Identifica con total claridad los parámetros faltantes del proyecto sin realizar llamadas innecesarias a Civil 3D.
3. Detecta la ausencia de la base de datos de la norma devolviendo un error explícito.
4. Las lecturas no ensucian el historial de Deshacer de Civil 3D.

# Informe de Validación 1.19.0 — Ronda C (17 de noviembre de 2026)

- **Versión de Civil 3D:** Autodesk Civil 3D 2027 (build 13.9.628.0, AutoCAD X.60.0.0 x64)
- **Rama:** `tarea/ronda-c`
- **Commit civil3d-mcp:** `6dba870` (versión instalada 1.19.1 con 59 herramientas)
- **Commit memoria-firma:** `518fa70` (`Tablas DG-2018: verificación independiente de la ampliación (0 errores)`)
- **Dibujo usado:** `PRUEBA_119.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)
- **INSUNITS (Paso 0):** `6` (metros)

---

## 1. Resumen Paso a Paso

| Paso | Decisivo | Resultado | ms_puente | Detalle / Respuesta literal |
| :--- | :--- | :--- | :--- | :--- |
| **0** | Sí | **OK** | 10 ms | `ping`: `version: "1.19.1"`, `hay_dibujo: true`, 59 herramientas. `INSUNITS` = `6` (metros). |
| **1** | No | **OK** | 338 ms | Capas `EJE_NORMA_A` y `EJE_NORMA_B` creadas. Polilíneas A y B dibujadas exitosamente con `_.PLINE`. `CMDACTIVE` = `0`. |
| **2** | No | **OK** | 62 ms | `crear_alineamiento_desde_polilinea` ("Eje Sin Norma 119", radio 80, `simular: true`): `simulado: true`, 2 curvas con radio 80, sin `propuesta_norma` ni `verificacion`. `mcp_log.jsonl` pasó de 735 a 736 (+1). |
| **3a** | Sí | **OK** | 1 ms | Sin `pavimentada` ni `peralte_maximo`: `ok: false`, `error` indica «Faltan datos del proyecto para proponer los radios de la DG-2018... No se llamó a Civil 3D». `faltan: ["pavimentada", "peralte_maximo"]`. |
| **3b** | Sí | **OK** | 1 ms | Con `radio: 80` y `clasificacion`: `ok: false`, `faltan` indica `velocidad_diseno` entre otros. |
| **3c** | Sí | **OK** | 1 ms | Con `velocidad_diseno: 65`: `ok: false`, `faltan: ["velocidad_diseno"]`, motivo indica que DG-2018 no permite interpolar y lista `valores_validos: [40, 50, 60, ...]`. |
| **3d** | No | **OK** | — | `mcp_log.jsonl` se mantuvo en 736 líneas (+0 en todo el paso 3). Menú Deshacer sin cambios. |
| **4** | Sí | **OK** | 104 ms | Simulación Polilínea A con DG-2018: propone `radios_por_pi: [215, 215]`. Tangente mínima 83 m (T302.01). `verificacion.prevista: true`, **0 «No cumple»**. `mcp_log.jsonl` pasó de 736 a 739 (+3). `backups\` sin copias nuevas (14). |
| **5** | Sí | **OK** | 148 ms | Creación real Polilínea A ("Eje Norma A"): creado con `[215, 215]`, longitud 1615.442 m. `verificacion.prevista: false`, **0 «No cumple»**. Deshacer tiene 1 entrada (`Executefunction`). `backups\` sumó 1 copia (`PRUEBA_119_20261117_122557_crear_alineamiento_desde_polilinea.dwg`). `listar_pis` y `verificar_norma` confirmaron radios 215 y 0 «No cumple». |
| **6** | Sí | **OK** | 68 ms | Polilínea B simulada: `propuesta_norma.radios_por_pi: [215, 80]`. PI 2 no admite 215 m (máximo 2 m con tangente mínima); puente calculó 80 m y emitió aviso explícito. `verificacion.prevista: true` reflejó «No cumple» en radio mínimo, transición, tangente mínima y relación de radios. `despues.numero_curvas: 2` (no quedó sin curva). |
| **7** | Sí | **OK** | 73 ms | Radios de usuario mandan: `radios_por_pi: "[{\"pi\": 2, \"radio\": 150}]"`. Propuesta final: `[215, 150]`. PI 2 asignó 150 m con `origen: "usuario"`, `cumple_minimo: false` y aviso citando T302.11B. Verificación: Radio mínimo `Cumple`, Transición `No cumple` (1 «No cumple»). |
| **8a** | No | **OK** | 30 ms | Eje duplicado ("Eje Norma A"): `ok: false`, `error: "Error de Civil 3D: ArgumentException: Ya existe el alineamiento 'Eje Norma A'. Usa listar_alineamientos."`, `id_log: "20261117-122802-f46d7e"`. Sin `propuesta_norma`. |
| **8b** | No | **OK** | 26 ms | Capa inexistente ("NO_EXISTE"): `ok: false`, `error: "Error de Civil 3D: ArgumentException: No existe la capa 'NO_EXISTE' en el dibujo. Dibuja la polilínea de tangentes en una capa propia (por ejemplo EJE_PRUEBA) o pasa 'handle'."`, `id_log: "20261117-122808-a1d2f0"`. |
| **9** | No | **OK** | — | Menú Deshacer contiene exclusivamente el paso 1 (polilíneas/capas) y 1 sola entrada del paso 5 (`Executefunction`). `backups\` contiene exactamente 1 backup correspondiente al paso 5. |

---

## 2. Recuentos de `mcp_log.jsonl` y `backups\`

- **Paso 2:** `mcp_log.jsonl` inició en 735 líneas (N0) y terminó en 736 líneas (**N0 + 1**).
- **Paso 3:** `mcp_log.jsonl` inició en 736 líneas (N1) y terminó en 736 líneas (**N1 + 0**). Civil 3D no fue invocado.
- **Paso 4:** `mcp_log.jsonl` inició en 736 líneas (N2) y terminó en 739 líneas (**N2 + 3**). `backups\` se mantuvo en 14 copias (**C2 + 0**).
- **Paso 5:** `mcp_log.jsonl` inició en 739 líneas (N3) y terminó en 745 líneas (**N3 + 6**). `backups\` sumó 1 copia nueva (**C3 + 1**): `PRUEBA_119_20261117_122557_crear_alineamiento_desde_polilinea.dwg`.

---

## 3. Tablas Detalladas de Propuesta y Verificación

### Paso 4: Polilínea A (Simulada, Norma DG-2018)

#### `propuesta_norma.pis`
```json
[
  {
    "pi": 1,
    "progresiva": 500.0,
    "deflexion_grados": 90.0,
    "giro": "izquierda",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_pedido": 215,
    "cumple_minimo": true,
    "radio_maximo_que_cabe": 500,
    "aviso": null,
    "manda": {
      "comprobacion": "Radio para prescindir de la curva de transición",
      "referencia": "T302.11B (302.05.07, p. 146; PDF 147)",
      "explicacion": "210 m redondeado hacia arriba a un múltiplo de 5 m (desigualdad estricta)"
    },
    "minimos": [
      {
        "comprobacion": "Radio mínimo",
        "valor_exigido": 125,
        "referencia": {
          "id": "T302.02",
          "seccion": "302.04.02",
          "pagina_documento": 129,
          "pagina_pdf": 130
        }
      },
      {
        "comprobacion": "Radio para prescindir de la curva de transición",
        "valor_exigido": 210,
        "radio_necesario": 215,
        "referencia": {
          "id": "T302.11B",
          "seccion": "302.05.07",
          "pagina_documento": 146,
          "pagina_pdf": 147
        }
      }
    ]
  },
  {
    "pi": 2,
    "progresiva": 1107.721,
    "deflexion_grados": 90.0,
    "giro": "derecha",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_pedido": 215,
    "cumple_minimo": true,
    "radio_maximo_que_cabe": 402,
    "aviso": null,
    "manda": {
      "comprobacion": "Radio para prescindir de la curva de transición",
      "referencia": "T302.11B (302.05.07, p. 146; PDF 147)",
      "explicacion": "210 m redondeado hacia arriba a un múltiplo de 5 m (desigualdad estricta)"
    },
    "minimos": [
      {
        "comprobacion": "Radio mínimo",
        "valor_exigido": 125,
        "referencia": {
          "id": "T302.02",
          "seccion": "302.04.02",
          "pagina_documento": 129,
          "pagina_pdf": 130
        }
      },
      {
        "comprobacion": "Radio para prescindir de la curva de transición",
        "valor_exigido": 210,
        "radio_necesario": 215,
        "referencia": {
          "id": "T302.11B",
          "seccion": "302.05.07",
          "pagina_documento": 146,
          "pagina_pdf": 147
        }
      }
    ]
  }
]
```

#### `verificacion.tabla`
```json
[
  {
    "elemento": "Curva PI 1",
    "progresiva": 500.0,
    "parametro": "Radio mínimo",
    "valor_calculado": 215.0,
    "valor_exigido": 125.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "T302.02 (302.04.02, p. 129; PDF 130)"
  },
  {
    "elemento": "Curva PI 1",
    "progresiva": 500.0,
    "parametro": "Radio para prescindir de la curva de transición",
    "valor_calculado": 215.0,
    "valor_exigido": 210.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "T302.11B (302.05.07, p. 146; PDF 147)"
  },
  {
    "elemento": "Curva PI 1",
    "progresiva": 500.0,
    "parametro": "Longitud mínima de curva (3 V)",
    "valor_calculado": 337.721,
    "valor_exigido": 180.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "302.04.03, p. 132; PDF 133"
  },
  {
    "elemento": "Tangente PI 1–PI 2",
    "progresiva": 622.721,
    "parametro": "Tangente mínima entre curvas de sentido contrario (L mín.s)",
    "valor_calculado": 270.0,
    "valor_exigido": 83.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "T302.01 (302.03.02, p. 125; PDF 126)"
  },
  {
    "elemento": "Curva PI 2",
    "progresiva": 1107.721,
    "parametro": "Radio mínimo",
    "valor_calculado": 215.0,
    "valor_exigido": 125.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "T302.02 (302.04.02, p. 129; PDF 130)"
  },
  {
    "elemento": "Curva PI 2",
    "progresiva": 1107.721,
    "parametro": "Radio para prescindir de la curva de transición",
    "valor_calculado": 215.0,
    "valor_exigido": 210.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "T302.11B (302.05.07, p. 146; PDF 147)"
  },
  {
    "elemento": "Curva PI 2",
    "progresiva": 1107.721,
    "parametro": "Longitud mínima de curva (3 V)",
    "valor_calculado": 337.721,
    "valor_exigido": 180.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "302.04.03, p. 132; PDF 133"
  }
]
```

---

### Paso 5: Polilínea A (Creación Real, Eje Norma A)

- `listar_pis`: PI 1 (`radio: 215`, `pc: 285.0`, `pt: 622.721`), PI 2 (`radio: 215`, `pc: 892.721`, `pt: 1230.442`), `fin: 1615.442`.
- `verificar_norma`: Exactamente la misma tabla que en la simulación prevista, confirmando 5 Cumple, 0 No cumple, 0 Avisos.

---

### Paso 6: Polilínea B (Simulada, Conflicto Geométrico en PI 2)

#### `propuesta_norma.pis`
```json
[
  {
    "pi": 1,
    "progresiva": 300.0,
    "deflexion_grados": 90.0,
    "giro": "izquierda",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_pedido": 215,
    "cumple_minimo": true,
    "radio_maximo_que_cabe": 217,
    "aviso": null,
    "manda": {
      "comprobacion": "Radio para prescindir de la curva de transición",
      "referencia": "T302.11B (302.05.07, p. 146; PDF 147)",
      "explicacion": "210 m redondeado hacia arriba a un múltiplo de 5 m (desigualdad estricta)"
    }
  },
  {
    "pi": 2,
    "progresiva": 600.0,
    "deflexion_grados": 90.0,
    "giro": "derecha",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_pedido": 80,
    "cumple_minimo": false,
    "radio_maximo_que_cabe": 2,
    "aviso": "La polilínea no permite cumplir la norma en el PI 2 (mínimo 215 m; cabe hasta 2 m dejando la tangente mínima): mueve los vértices o baja la velocidad. Se pide 80 m, el mayor que cabe sin dejar la tangente mínima con la curva anterior (lo que admite el plugin), redondeado hacia abajo a 5 m; la verificación lo marcará «No cumple»."
  }
]
```

#### `verificacion.tabla`
```json
[
  {
    "elemento": "Curva PI 1",
    "progresiva": 300.0,
    "parametro": "Radio mínimo",
    "valor_calculado": 215.0,
    "valor_exigido": 125.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "T302.02 (302.04.02, p. 129; PDF 130)"
  },
  {
    "elemento": "Curva PI 1",
    "progresiva": 300.0,
    "parametro": "Radio para prescindir de la curva de transición",
    "valor_calculado": 215.0,
    "valor_exigido": 210.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "T302.11B (302.05.07, p. 146; PDF 147)"
  },
  {
    "elemento": "Curva PI 1",
    "progresiva": 300.0,
    "parametro": "Longitud mínima de curva (3 V)",
    "valor_calculado": 337.721,
    "valor_exigido": 180.0,
    "unidad": "m",
    "cumple": "Cumple",
    "referencia": "302.04.03, p. 132; PDF 133"
  },
  {
    "elemento": "Tangente PI 1–PI 2",
    "progresiva": 422.721,
    "parametro": "Tangente mínima entre curvas de sentido contrario (L mín.s)",
    "valor_calculado": 5.0,
    "valor_exigido": 83.0,
    "unidad": "m",
    "cumple": "No cumple",
    "referencia": "T302.01 (302.03.02, p. 125; PDF 126)"
  },
  {
    "elemento": "PI 1 → PI 2",
    "progresiva": 422.721,
    "parametro": "Relación entre radios consecutivos (curvas de sentido contrario)",
    "valor_calculado": 80.0,
    "valor_exigido": "entre 157 y 268",
    "unidad": "m",
    "cumple": "No cumple",
    "referencia": "T302.08 (302.04.05, p. 136; PDF 137)"
  },
  {
    "elemento": "PI 2 → PI 1",
    "progresiva": 422.721,
    "parametro": "Relación entre radios consecutivos (curvas de sentido contrario)",
    "valor_calculado": 215.0,
    "valor_exigido": "entre 53 y 120",
    "unidad": "m",
    "cumple": "No cumple",
    "referencia": "T302.08 (302.04.05, p. 136; PDF 137)"
  },
  {
    "elemento": "Curva PI 2",
    "progresiva": 507.721,
    "parametro": "Radio mínimo",
    "valor_calculado": 80.0,
    "valor_exigido": 125.0,
    "unidad": "m",
    "cumple": "No cumple",
    "referencia": "T302.02 (302.04.02, p. 129; PDF 130)"
  },
  {
    "elemento": "Curva PI 2",
    "progresiva": 507.721,
    "parametro": "Radio para prescindir de la curva de transición",
    "valor_calculado": 80.0,
    "valor_exigido": 210.0,
    "unidad": "m",
    "cumple": "No cumple",
    "referencia": "T302.11B (302.05.07, p. 146; PDF 147)"
  },
  {
    "elemento": "Curva PI 2",
    "progresiva": 507.721,
    "parametro": "Longitud mínima de curva (3 V)",
    "valor_calculado": 125.664,
    "valor_exigido": 180.0,
    "unidad": "m",
    "cumple": "Aviso",
    "referencia": "302.04.03, p. 132; PDF 133"
  }
]
```

---

### Paso 7: Polilínea A con Radio Forzado por Usuario (PI 2 = 150 m)

#### `propuesta_norma.pis`
```json
[
  {
    "pi": 1,
    "progresiva": 500.0,
    "deflexion_grados": 90.0,
    "giro": "izquierda",
    "origen": "norma",
    "radio_minimo_norma": 215,
    "radio_pedido": 215,
    "cumple_minimo": true,
    "radio_maximo_que_cabe": 500,
    "aviso": null
  },
  {
    "pi": 2,
    "progresiva": 1107.721,
    "deflexion_grados": 90.0,
    "giro": "derecha",
    "origen": "usuario",
    "radio_minimo_norma": 215,
    "radio_pedido": 150,
    "cumple_minimo": false,
    "radio_maximo_que_cabe": 467,
    "aviso": "PI 2: tu radio 150 m queda por debajo del mínimo de la norma (215 m, T302.11B (302.05.07, p. 146; PDF 147)): la verificación lo marcará «No cumple»."
  }
]
```

#### `verificacion.tabla`
- Curva PI 1: Radio mínimo (215 vs 125) `Cumple`, Transición (215 vs 210) `Cumple`, Longitud (337.721 vs 180) `Cumple`.
- Tangente PI 1–PI 2: Tangente mínima (335 vs 83) `Cumple`.
- Curva PI 2: Radio mínimo (150 vs 125) `Cumple`, Transición (150 vs 210) **`No cumple`**, Longitud (235.619 vs 180) `Cumple`.
- **Resumen:** 6 Cumple, 1 No cumple, 0 Avisos.

---

## 4. Radios y Comprobaciones Notables frente a DG-2018

1. **Prioridad de T302.11B sobre T302.02:** Para 60 km/h, tercera clase no pavimentada (peralte 8%), el radio mínimo absoluto es 125 m (T302.02). Sin embargo, al prescindir de curvas de transición (espirales), rige la condición $R > 210\text{ m}$ (T302.11B), por lo que la herramienta exige estrictamente $R \ge 215\text{ m}$ (redondeo hacia arriba al múltiplo de 5 m). Este comportamiento garantiza alineamientos compatibles con la geometría sin espirales.
2. **Conflicto geométrico en Polilínea B:** El PI 2 dispone de tangentes de 300 m y sentido contrario. Tras la curva de 215 m en el PI 1, el espacio restante no permite un radio de 215 m respetando la tangente mínima de 83 m. El puente calcula automáticamente el radio admisible (80 m) advirtiendo explícitamente al usuario y marcando las penalizaciones normativas en la verificación prevista.

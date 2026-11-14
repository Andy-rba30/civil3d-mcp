# Informe de validación 1.14.2 (ejecutado sobre 1.15.0, rama `tarea/2.9-tabla-pi`)

> **Nota de la orquestadora:** el agente instalador no guardó este informe como archivo en la PC (solo entregó el
> informe corto para pegar). Este archivo es la **transcripción literal de ese informe corto**, tal como lo pegó el
> dueño en la sesión orquestadora el 14/11/2026. El paso 8 (tabla por progresivas) se corrigió en la 1.14.3 y se
> validó en `informe_validacion_1143_20261114.md`.

- **Fecha:** 2026-11-14
- **Civil 3D:** 2027 (español)
- **Rama instalada:** `tarea/2.9-tabla-pi` (commit `31ef86b`, `ArbaMcp` v1.15.0)
- **Dibujo:** `PRUEBA_1142.dwg` (copia de `PRUEBA_112.dwg`)
- **Herramientas MCP visibles:** 55 (54 del plugin, incluida `listar_pis`, + `buscar_api`)
- **Resultado global:** `crear_secciones` **OK** (segundo grupo `Muestreo 100`); `calcular_volumenes` crea la superficie
  e identifica **43/43** secciones por `SampleLine.GetSectionId`, pero **`tabla_progresivas` FALLO** con
  `supuesto_ejes.comprobado: false` por un punto en `X = -30` en `Corredor 1 - Datum`.

## Pasos (informe corto del agente, literal)

```text
=== VALIDACION_1142.md ===
- Paso 0 (ping): OK (12 ms, puente 44 ms) — v1.15.0, hay_dibujo: true.
- Paso 1 (listar_alineamientos 24 ms, listar_superficies 35 ms, listar_corredores 12 ms, listar_lineas_muestreo 55 ms): OK — Eje 1 (0..831.3274), Corredor 1, Terreno, Terreno 2, Corredor 1 - Top, Corredor 1 - Datum, sin Volumen ni líneas previas.
- Paso 2 (backups\): OK — 8 archivos previos.
- Paso 3 (crear_secciones simular=true): OK (89 ms, puente 126 ms) — 43 líneas (0+000.00..0+831.33), sin prefijo_lineas, estilo "Road Section".
- Paso 4 (crear_secciones real Eje 1 - Muestreo): OK (899 ms, puente 937 ms) — 43 líneas y 43 vistas; datos.nombres.leidos todos 0; copia PRUEBA_1142_20261114_111000_crear_secciones.dwg.
- Paso 5a (crear_secciones estilo_vista="NoExiste" simular=true): OK (puente 58 ms) — ok: false controlado; disponibles: Land Desktop Sections, Road Section.
- Paso 5b (crear_secciones Muestreo 100 simular=true): OK (10 ms, puente 22 ms) — prefijo_lineas: "Muestreo 100 ", estilo "Land Desktop Sections", aviso literal de choque OK.
- Paso 6 (crear_secciones real Muestreo 100 + ejecutar_comando _.ZOOM): OK (213 ms, puente 226 ms; _.ZOOM 123 ms) — 10 líneas prefijadas (ancho_izquierdo: 10, ancho_derecho: 25), 10 vistas ("Muestreo 100 0+000.00".."0+831.33" sin repetir grupo). datos.nombres.leidos: {"lineas":43,"vistas":43,"lineas_por_ejes":43,"lineas_en_espacio_modelo":43,"lineas_solo_en_espacio_modelo":0,"vistas_por_ejes":43,"vistas_en_espacio_modelo":43,"vistas_solo_en_espacio_modelo":0}. Copia reutilizada.
- Paso 7 (calcular_volumenes simular=true): OK (65 ms, puente 88 ms) — identificacion_secciones: terreno 43/43 y datum 43/43 por SampleLine.GetSectionId (lineas_sin_seccion: 0).
- Paso 8 (calcular_volumenes real): FALLO parcial en tabla_progresivas (289 ms, puente 314 ms)
  * Superficie TIN (despues): OK — Corredor 1 - Volumen, corte: 9014.1785, relleno: 32898.6449, neto: -23884.4664, 1516 triángulos.
  * identificacion_secciones: OK — terreno 43/43 y datum 43/43 por SampleLine.GetSectionId (lineas_sin_seccion: 0).
  * tabla_progresivas: FALLO — numeros: false, filas: null, supuesto_ejes: {"comprobado": false, "lineas_comprobadas_con_la_superficie": 0, "motivo": "un punto de la sección de 'Corredor 1 - Datum' tiene X = -30, fuera de los desplazamientos de la sección (-3.9 a 3.6)"}.
  * Aviso literal: "La tabla por progresivas sale sin números: un punto de la sección de 'Corredor 1 - Datum' tiene X = -30, fuera de los desplazamientos de la sección (-3.9 a 3.6). Anótalo en miembros_por_verificar_civil3d.md (SectionPoint.Location) para corregir la herramienta; los totales de la superficie de volumen no dependen de esto."
- Paso 9 (pk_a_punto 4 ms, cota_superficie Terreno 5 ms -> 104.1867, Datum 3 ms -> 109.0011): OK lecturas; sin contraste de area_relleno porque tabla_progresivas.filas fue null.
- Paso 10 (backups\): OK — 1 archivo nuevo (9 total).
- Paso 11 (guardar_dibujo): OK (717 ms, puente 731 ms).
- Paso 12 (leer_log): OK — ok: false solo en paso 5a.
```

## Para la memoria técnica (lo que deja este informe)

- `SampleLine.Create` exige nombres únicos: con el prefijo del grupo el segundo grupo se creó sin el error
  `Sample line name should not duplicate.`
- Las líneas de muestreo y las vistas de sección **son entidades del espacio modelo**: las 43 se encontraron también
  ahí (`lineas_en_espacio_modelo: 43`, ninguna solo en el espacio modelo).
- `SampleLine.GetSectionId(id de la superficie)` encuentra la sección del terreno y la de la Datum (superficie de
  corredor) en las 43 líneas: es el intento que funcionó.
- Las preguntas 6 y 7 de `VALIDACION_113` (anchos y estilo del segundo grupo): 10 m a la izquierda y 25 m a la derecha,
  estilo `Land Desktop Sections`, como se pidió.

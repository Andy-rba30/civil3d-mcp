# Prompt de validación de la 1.22.1 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Plugin y puente 1.22.1** desde la rama `tarea/7.6-correcciones-1221`. Siguen siendo **64** herramientas.
- **Dibujo**: una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` guardada como
  `C:\Proyectos\dibujos_prueba\PRUEBA_1221.dwg`. Nunca el archivo base ni un dibujo real. Su `Ensamblaje 1 (3)` trae los
  dos `LaneSuperelevationAOR` con `UseSuperelevation` = `None` (validado el 20/11/2026): esta prueba lo usa a propósito.
- `ARBA_MCP_CODIGO` no hace falta. `MEMORIA_FIRMA_DIR` como siempre.
- El agente local **instala y verifica**; no edita ningún archivo del repositorio salvo su informe, y no hace commits.

## Reglas

- **No marques OK lo que no viste.** Cada resultado lleva la respuesta literal. Si un paso no se hizo, `NO HECHO` y por
  qué. Los errores y las líneas de `mcp_log.jsonl`, literales, leídas con `Get-Content -Encoding UTF8`.
- **Sin zoom** hasta el final. El token del plugin nunca se muestra: `<oculto>`.

## Qué trae la 1.22.1 (correcciones de la validación de la ronda E, 20/11/2026)

1. **Bombeo en los extremos del eje** (`aplicar_peralte`): Civil 3D deja `BeginAlignment` y `EndAlignment` con 0 % y el
   eje interpolaba de 0 a −2 % en las rectas de los extremos (−1.0417 % en el pk 100). Ahora `aplicar_peralte` les pone
   el bombeo normal y lo comprueba también en la mitad de la recta inicial y de la final.
2. **Aviso de carriles que ignoran el peralte** (`aplicar_peralte` y `crear_corredor`): si un corredor del eje usa un
   subensamblaje con `UseSuperelevation` = `None`, aviso y `datos.carriles_sin_peralte`.
3. **40 m de transición por defecto** (`crear_bordes_calzada`): sin `longitud_transicion`, Civil 3D dejaba la transición
   en 0 m y el borde daba un salto.

**Pasos decisivos: 0, 1.3, 1.4, 1.5, 1.6 y 2.1.**

---

Eres el agente de validación de la versión 1.22.1 del conector Civil 3D MCP. Usa solo las herramientas del MCP `civil3d`.
Avisa antes de cada paso que cambie el dibujo.

## 0. Versión (decisivo)

`ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.22.1"`, `"hay_dibujo": true`, en
`PRUEBA_1221.dwg`. Si no, **para aquí**.

## 1. Peralte en los extremos y carriles que lo ignoran

1.1. **Preparación, con el corredor ANTES del peralte.** Haz en `PRUEBA_1221.dwg` el paso 1 de
     `herramientas-dev/VALIDACION_120.md` (capa con `\n\n`, polilínea, `Terreno`, eje `Eje Peralte` con R 80) y su paso 8
     (rasante, corredor `Corredor Peralte` con `Ensamblaje 1 (3)` **sin cambiarle nada**), pero **todavía sin
     `aplicar_peralte`**.
1.2. `aplicar_peralte` **simulado** con las `curvas` del paso 4 de `VALIDACION_120.md` y `"simular": true`. Esperado:
     `despues.bombeo_en_extremos` con dos entradas, `BeginAlignment` (desde `EndNormalCrown` del pk 192) y `EndAlignment`
     (desde la última `BeginNormalCrown`), con `pendientes` −2 / −2 y `se_escribe: true`. Sin copia ni entrada en Deshacer.
1.3. **(Decisivo)** La misma llamada **sin** `simular`. Esperado:
     - `ok: true`; `datos.bombeo_en_extremos` con cada extremo `escrito: true` y `leido` −2 / −2;
     - `datos.comprobacion_entre_progresivas` con filas `tramo: "recta inicial"` (pk ≈ 96) y `"recta final"`, todas con
       `coincide: true`;
     - en `avisos`, el aviso «El corredor 'Corredor Peralte' usa el subensamblaje '…' con UseSuperelevation = None: ese
       carril no sigue el peralte del eje…» por cada carril, y `datos.carriles_sin_peralte` con `clave:
       "UseSuperelevation"`, `valor: 6`, `opcion: "None"`;
     - una sola entrada nueva en el menú Deshacer («Grupo de comandos»).
     Si sale «No se pudo poner el bombeo normal en BeginAlignment…» o «El eje no devuelve el peralte escrito en el pk
     0+096…», **cópialo entero**: `SetSlope` no funciona en las progresivas de contorno.
1.4. **(Decisivo)** `listar_peralte` con
     `args={"alineamiento":"Eje Peralte","progresivas":"[100, 280, 700]","pedido":"peralte en las rectas y en la curva"}`.
     Esperado: en pk 100 y pk 700 (rectas) **−2** en los segmentos escritos (con la 1.22.0 daba −1.0417); en pk 280, ±6.
1.5. **(Decisivo)** `crear_corredor` **simulado** sobre `Eje Peralte` con `Ensamblaje 1 (3)` y un nombre nuevo
     (`Corredor Aviso`), `"simular": true`. Esperado: el mismo aviso de carriles con `UseSuperelevation = None` y
     `datos.eje_con_peralte: true`.
1.6. **(Decisivo)** Pide a Jose que cambie a mano, en los dos `LaneSuperelevationAOR` de `Ensamblaje 1 (3)`,
     `UseSuperelevation` a `RightLO` (derecho) y `LeftLO` (izquierdo); después `reconstruir_corredor` con
     `args={"corredor":"Corredor Peralte"}`. Luego:
     - `seccion_corredor` con `{"corredor":"Corredor Peralte","progresiva":100}` → los dos carriles a **−2** %;
     - `seccion_corredor` con `"progresiva":280` → **+6** y **−6** (como en la ronda E);
     - `crear_corredor` simulado como en 1.5 → **sin** el aviso de carriles.

## 2. Transición del sobreancho por defecto (en el mismo dibujo)

2.1. **(Decisivo)** `crear_bordes_calzada` con
     `args={"eje":"Eje Peralte","desplazamiento":3.6,"ensanchamientos":"[{\"borde\":\"izquierdo\",\"progresiva_inicial\":60,\"progresiva_final\":140,\"sobreancho\":0.6}]","simular":true,"pedido":"simula un sobreancho sin longitud de transición"}`.
     Esperado: en `avisos`, «El ensanchamiento 1 no tiene 'longitud_transicion': se usan 40 m (DG-2018, 302.09.02…)»; en
     el ensanchamiento, `longitud_transicion: 40`, `longitud_transicion_por_defecto: true`, `rango_add_widening: [20, 140]`
     y `transicion_prevista` con `entrada: [20, 60]` y `salida: [140, 180]`. Repite **sin** `simular`: además
     `longitud_transicion_aplicada: true`, `medido.sobreancho_medido: 0.6`, `medido.transicion: "fuera"`, y en la
     medición `longitud_entrada` y `longitud_salida` ≈ 40 (**no 0**). Con `listar_bordes_calzada`
     (`{"alineamiento":"Eje Peralte"}`), la región `Specific` con entrada de 20 a 60 y salida de 140 a 180, las dos de 40 m.
2.2. Lo mismo con `\"longitud_transicion\":0` en el ensanchamiento y `"simular": true` (nombres de borde nuevos,
     `nombre_izquierdo` y `nombre_derecho`, para no chocar con los del 2.1) → el aviso de que el borde dará un salto en 60 y
     en 140, y `longitud_transicion: 0`.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_1221_<AAAAMMDD>.md` con: versión de Civil 3D, commit de la rama, una
tabla paso / resultado (OK, FALLO, NO HECHO) / respuesta literal / tiempo, las respuestas completas de 1.3, 1.4 y 1.6, la
línea de `mcp_log.jsonl` de cada FALLO y la lista de lo que te parezca dudoso. **Antes de subirlo a ningún sitio**, cambia
cualquier ruta con tu nombre de usuario por `%USERPROFILE%` o `%APPDATA%` (el repositorio es público).

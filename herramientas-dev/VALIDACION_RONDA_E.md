# Prompt de validación de la ronda E (1.22.0) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Plugin y puente 1.22.0** desde la rama `tarea/ronda-e`. Hay **64** herramientas: **60** del plugin (las 59 de la
  1.21.5 y `seccion_corredor`) y **4** del puente.
- **Dibujos**: copias nuevas, nunca un dibujo real ni el archivo base. Cada parte dice de qué copia parte.
- `memoria-firma` al día (`git pull`, commit `a9e5d6d` o posterior) en `C:\Proyectos\memoria-firma`, con
  `MEMORIA_FIRMA_DIR` apuntando ahí. Lo necesitan las partes 2 y 4.
- `ARBA_MCP_CODIGO=1` antes de abrir Civil 3D, solo para las partes 3 y 5. Anota el valor que tenía y devuélvelo al
  terminar.
- Reparto de papeles: el agente local **instala y verifica**. No edita ningún archivo del repositorio salvo sus
  informes en `herramientas-dev/informes/`, y no hace commits.

## Reglas

- **No marques OK lo que no viste.** Cada resultado lleva la respuesta literal. Si un paso no se hizo, escribe
  `NO HECHO` y por qué.
- **Copia los errores y las líneas de `mcp_log.jsonl` literales**, leídas del archivo con
  `Get-Content -Encoding UTF8` (sin `-Encoding` los acentos salen rotos). Nunca reescritas ni resumidas.
- **No deduzcas causas.** Si algo falla, pega el error tal cual y sigue con el paso siguiente.
- **Sin zoom** hasta el final de cada parte (el zoom deja su propia entrada en el menú Deshacer).
- El token del plugin nunca se muestra: `<oculto>`.

## Qué trae la 1.22.0

1. **`seccion_corredor`** (lectura, nueva, tarea 7.3): la sección aplicada de un corredor en una progresiva:
   subensamblajes, parámetros aplicados, puntos con desplazamiento, cota y códigos, pendiente de cada tramo y de cada
   carril (de `Crown` a `ETW`). `listar_ensamblajes` añade `parametros_detalle` (clave interna y opciones de cada
   parámetro de lista).
2. **`listar_peralte` con `progresivas`** y **comprobación en `aplicar_peralte`** (tarea 7.4): el peralte de los 8
   segmentos en cualquier progresiva (`Alignment.GetCrossSlopeAtStation`), y tras escribir, un aviso si el eje no
   devuelve el peralte escrito en el centro de cada curva.
3. **`verificar_norma`** (tarea 7.2): 302.05.06 curva a curva con la figura de peralte: fila «Aviso» con la cifra si
   la figura pide más del 3 %.
4. **`ejecutar_codigo`** (tarea 7.1): leer `Bounds` de una entidad compila, también con `solo_lectura`.
5. **Sobreancho con la decisión P5, opción F** (tarea 7.5): por defecto el sobreancho entero de la F46; la reducción
   de la Tabla 302.20 solo si se pide con `reduccion_t30220`, con calzada de 7.20 m y V ≤ 80 km/h (factor mayor entre filas,
   0.05 por encima de 500 m, nunca menos de 0.40 m).

**Lo que decide esta validación**: si el corredor sigue el peralte del eje en las curvas (partes 1A y 1B, que miden lo
que en la ronda D se midió mal), el orden real de los ejes de `CalculatedPoint.StationOffsetElevationToBaseline` y si
`GetCrossSlopeAtStation` da el peralte entre progresivas críticas. **Pasos decisivos: 0, 1.2, 1.3, 1.4, 1.6, 2.2, 3.1 y
4.2.**

---

Eres el agente de validación de la versión 1.22.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Usa solo las herramientas del MCP `civil3d`. Avisa antes de cada paso que cambie el dibujo.

## 0. Versión e instalación (decisivo)

- `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.22.0"`, `"hay_dibujo": true`.
- La lista de herramientas del MCP tiene **64**, entre ellas `seccion_corredor`.
- Si la versión no es exactamente `1.22.0` o falta `seccion_corredor`, **para aquí**.

## 1. El corredor y el peralte (dibujo: copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)

1.1. **Preparación.** Haz los pasos 1 a 8 de `herramientas-dev/VALIDACION_120.md` en esta copia (eje `Eje Peralte` con
     R 80, `aplicar_peralte` con los valores explícitos del paso 4 de ese prompt, rasante, ensamblaje y corredor
     `Corredor Peralte`). Anota la respuesta de `aplicar_peralte`: debe traer `datos.comprobacion_entre_progresivas`.
     **Si trae algún aviso que empiece por «El eje no devuelve el peralte escrito…», cópialo entero.**
1.2. **(Decisivo) Peralte del eje en cualquier progresiva.** `listar_peralte` con
     `args={"alineamiento":"Eje Peralte","progresivas":"[100, 280, 550]","pedido":"peralte en tres progresivas"}`.
     Esperado: `pendientes_en_progresivas` con 3 filas y `lectura_en_progresivas` con `applySmoothing: true`. En pk 100
     (recta) los carriles a −2; en pk 280 (centro de la curva del PI 1, peralte total) los carriles exteriores con el
     peralte escrito (±6, con el signo según el giro), igual que `BeginFullSuper`/`EndFullSuper` de
     `curvas[].estaciones` de la misma respuesta. **Si en pk 280 sale −2 o 0, el eje no da el peralte entre
     progresivas críticas: cópialo entero.** Cualquier `"error: ..."`, literal.
1.3. **(Decisivo) Sección del corredor en recta.** `seccion_corredor` con
     `args={"corredor":"Corredor Peralte","progresiva":100,"pedido":"sección del corredor en recta"}`. Esperado:
     `progresiva_usada: 100`, en `carriles` dos entradas (`izquierda` y `derecha`) con `pendiente_pct` ≈ −2, puntos con
     `progresiva` ≈ 100, desplazamiento negativo a la izquierda y códigos con `Crown` y `ETW`. **Si aparece el aviso
     «X de StationOffsetElevationToBaseline…», cópialo:** el orden de los ejes es otro.
1.4. **(Decisivo) Sección del corredor en la curva.** Lo mismo con `"progresiva":280`. Esperado: el carril interior de
     la curva con `pendiente_pct` ≈ −(peralte) y el exterior con ≈ +(peralte), coincidiendo (±0.05) con lo del paso 1.2.
     **Si los dos carriles salen a −2, el corredor no sigue el peralte: copia la respuesta entera**, con
     `subensamblajes[].parametros` (los parámetros aplicados de cada carril).
1.5. **Progresiva no calculada.** `seccion_corredor` con `"progresiva":103.7`. Anota si sale la sección de la más
     cercana con el aviso «En la progresiva 103.7 no hay sección calculada…» o la de 103.7 sin aviso.
1.6. **(Decisivo) Parámetros del carril.** `listar_ensamblajes` con `args={"pedido":"parámetros de los ensamblajes"}`.
     Copia literal el bloque `parametros_detalle` de los dos `LaneSuperelevationAOR` del ensamblaje del corredor: cada
     parámetro con `clave`, `tipo`, `valor` y, en los de lista, `opcion`, `etiqueta` y `opciones[]`. **Anota cuál es el
     parámetro de peralte («Use Superelevation» o el que sea) y su valor.**
1.7. **Solo si el paso 1.4 dio −2 en los dos carriles y el 1.6 muestra el parámetro de peralte en «ninguno» o en el
     carril interior.** Pide a Jose que lo cambie a mano al carril exterior en los dos subensamblajes, luego
     `reconstruir_corredor` con `args={"corredor":"Corredor Peralte"}` y repite los pasos 1.4 y 1.6.
1.8. **Errores.** `seccion_corredor` con `{"corredor":"No existe","progresiva":100}` → «No existe el corredor 'No
     existe'… Usa listar_corredores.»; con `{"corredor":"Corredor Peralte","progresiva":5000}` → «La progresiva 5000 está
     fuera del rango…»; `listar_peralte` con `"progresivas":"[]"` → «'progresivas' está vacía…».
1.9. **Sin escritura.** Después de 1.2 a 1.8, el menú Deshacer no tiene entradas nuevas y `backups\` no tiene archivos
     nuevos.

## 2. `verificar_norma` con la figura de peralte (dibujo: el de la parte 1, o copia nueva)

2.1. Crea `Eje Norma A` como en el paso 10a de `VALIDACION_120.md`, o con `crear_alineamiento_desde_polilinea` y
     `velocidad_diseno` 60, Tercera Clase, ondulado, rural, sin pavimentar, peralte máximo 8 (propone 215 m por PI).
2.2. **(Decisivo)** `verificar_norma` con
     `args={"norma":"DG-2018","tipo_elemento":"alineamiento","nombre":"Eje Norma A","velocidad_diseno":60,"clasificacion":"Carretera de Tercera Clase","orografia":"Ondulado","ubicacion":"rural","pavimentada":false,"peralte_maximo":8}`.
     Esperado: en cada curva, justo debajo de «Radio para prescindir de la curva de transición | 215 m | 210 m |
     Cumple», la fila «Peralte para prescindir de la curva de transición | 6.5 % | 3 % | Aviso | 302.05.06 (p. 145; PDF
     146)», con `detalle.figura` «F302.03 …», `lectura_pct` 6.54. `resumen["No cumple"]` en 0. En `avisos` **no** sale el
     texto «302.05.06 podría pedir clotoide…». Ninguna entrada en Deshacer.

## 3. `ejecutar_codigo`: leer `Bounds` (dibujo: el de la parte 1; `ARBA_MCP_CODIGO=1`)

3.1. **(Decisivo)** `ejecutar_codigo` con
     `args={"codigo_csharp":"foreach (var id in F.EspacioModelo())\n{\n    var e = F.Abrir<Entity>(id);\n    var c = e.Bounds;\n    return new { tipo = e.GetType().Name, tiene = c.HasValue, min_x = c.HasValue ? c.Value.MinPoint.X : 0, max_x = c.HasValue ? c.Value.MaxPoint.X : 0, limites = \"\" + c };\n}\nreturn \"sin entidades\";","solo_lectura":true,"pedido":"lee Bounds de la primera entidad sin tocar el dibujo"}`.
     Esperado: `ok: true`, `copia: null`, `datos.deshacer.comando: ""`, y en `resultado` el tipo, `tiene: true` y las dos
     esquinas. Si sale `[bloqueado: ventanas y diálogos] Autodesk.AutoCAD.GraphicsInterface.Drawable`, el plugin no es el
     de esta rama.
3.2. **Control.** El mismo script con `var d = e.DrawableType;` en la línea 4 → `ok: false` con «[bloqueado: ventanas y
     diálogos]» en la línea 4.

## 4. Sobreancho con la opción F de P5 (dibujo: copia nueva con el `Eje 30` de `VALIDACION_121.md`, paso 13)

4.1. Crea el `Eje 30` (R 25 y R 30) como en los pasos 13a y 13b de `VALIDACION_121.md`.
4.2. **(Decisivo)** `crear_bordes_calzada` con
     `args={"eje":"Eje 30","simular":true,"velocidad_diseno":30,"numero_carriles":2,"ancho_calzada":7.2,"vehiculo_diseno":"B2","pedido":"simula los bordes con el sobreancho de la norma"}`
     → el sobreancho aplicado es el **entero**: **5.27** m en R 25 y **4.38** m en R 30, con el factor (0.86 y 0.83) y
     el valor reducido (4.53 y 3.64) solo como información.
4.3. Lo mismo con `"reduccion_t30220": true` → aplica el reducido: **4.53** y **3.64**.
4.4. Con `"reduccion_t30220": true` y `"ancho_calzada": 6.6` → aplica el entero (5.27 y 4.38) y avisa de que la reducción
     solo vale con 7.20 m.
4.5. En `propuesta_norma.pendiente_de_decision`, P5 ya no está «a confirmar» como un todo: dice opción F decidida el
     20/11/2026 y solo marca `a_confirmar: true` en las dos confirmaciones de Jose.

## 5. Transición del sobreancho por defecto (no decisivo; dibujo: el de la parte 4; `ARBA_MCP_CODIGO=1`)

5.1. `ejecutar_codigo` con `"solo_lectura": true` y este `codigo_csharp`:
```
var s = F.Civil.Settings.GetSettings<Autodesk.Civil.Settings.SettingsCmdAddWidening>();
return new {
  tipo = s.WideningOptions.TransitionType.Value.ToString(),
  longitud = s.WideningOptions.TransitionLength.Value,
  taper_entrada = s.WideningOptions.TaperInputType.Value.ToString(),
  taper_ratio = s.WideningOptions.TaperRatio.Value,
  segmentos_en_curva = s.LinearTransitionAroundCurves.NumberOfSegments.Value,
  tipo_segmento = s.LinearTransitionAroundCurves.TransitionSegmentType.Value.ToString(),
  tipos_transicion = Enum.GetNames(typeof(Autodesk.Civil.TransitionType))
};
```
     Pega la respuesta (o el error de compilación) literal.
5.2. En un eje nuevo **en recta** (o en la recta inicial del `Eje 30`), `crear_bordes_calzada` real con un
     ensanchamiento **sin** `longitud_transicion` en el borde izquierdo, de 60 a 140 con 0.6. Anota de la respuesta
     `regiones_civil3d[].entrada` y `.salida` (tipo, progresivas, longitud) y `medicion.ensanchamientos_medidos`
     (`longitud_entrada`, `longitud_salida`). Después `_.UNDO 1`.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_ronda_e_<AAAAMMDD>.md` con: versión de Civil 3D, commit de la rama
y de `memoria-firma`, una tabla paso / resultado (OK, FALLO, NO HECHO) / respuesta literal / tiempo, las respuestas
completas de 1.2, 1.3, 1.4 y 1.6, la línea de `mcp_log.jsonl` de cada FALLO, los recuentos de `backups\` y del menú
Deshacer de 1.9, y la lista de lo que te parezca dudoso. Al terminar, devuelve `ARBA_MCP_CODIGO` a su valor inicial.

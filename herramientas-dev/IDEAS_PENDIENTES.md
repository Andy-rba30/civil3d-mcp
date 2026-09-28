# Ideas pendientes (Civil 3D, Revit e interoperabilidad)

Notas para retomar el trabajo después de la pausa del 28/09/2026. **Nada de esto está implementado.** Son ideas
ordenadas por prioridad, pensadas para que el agente ayude en tareas largas que dependen del contexto: extraer
datos, llenar parámetros, corregir un corredor o un modelo, crear familias o ensamblajes. Lo repetitivo y siempre
igual se resuelve mejor con un plugin en C#.

Todo miembro de la API que se nombra aquí es una pista, no algo comprobado. Al implementarlo entra en
`miembros_por_verificar_civil3d.md` como `por verificar`, igual que en las entregas anteriores.

## Estado de partida

| | Civil 3D (`civil3d-mcp`) | Revit (`revit-mcp`) |
|---|---|---|
| Versión en `main` | 1.3.3, validada e instalada | 0.5.0 (entrega 2b fusionada) |
| Herramientas | 43 | 52 (tras la consolidación de 78 a 40 en la 0.4.0) |
| Pendiente | Nada | Validar la 2b (`VALIDACION_2B.md`, unas 50 filas `por verificar`); después, la 2c (familias) |

## Principios (para los dos MCP)

1. **La IA razona y las herramientas le dan ojos y manos.** No hace falta una herramienta por tarea. Hace falta:
   - **ver:** lecturas ricas, advertencias y capturas;
   - **actuar con red:** escrituras en lote con `simular`, copia y deshacer;
   - **comprobar:** reconstruir y volver a leer;
   - **una vía de escape:** ejecutar código para lo raro.
2. **Entre 40 y 60 herramientas por MCP.** Cada herramienta ocupa contexto en cada conversación. Con más de 80–100
   el agente elige peor. Mejor pocas herramientas con parámetros que muchas casi iguales.
3. **Añadir herramientas por repetición, no por cobertura.** Si algo se hace a menudo con la vía de escape, merece
   una herramienta propia.
4. **La lentitud suele estar en las vueltas del agente, no en el programa.** Cada llamada es un turno del modelo.
   - Si una tarea tarda, mirar `ms_espera`, `ms_ejecucion` y `ms_puente` antes de tocar el código.
   - Si la herramienta es rápida y el total lento, la solución es un lote, una macro o mejores instrucciones.
   - Para tareas simples, un modelo rápido.
5. **Recetas en `INSTRUCCIONES_AGENTE.md`.** Aquí entra el criterio del ingeniero: qué revisar y en qué orden, qué
   herramienta usar para cada tarea y qué no hacer.
6. **Cada entrega sigue el ciclo de la 1.3.** Rama y PR, CI en verde, validación en el programa con informe, tabla
   de miembros al día y fusión.

## Civil 3D: 1.4 y siguientes (diseño de carreteras)

Hoy el MCP cubre muy bien el corredor. Le falta el principio del flujo (geometría) y el final (cantidades).

| Etapa | Hoy | Falta |
|---|---|---|
| Superficie del terreno | Listar, cota, líneas de rotura, pegar, reconstruir | Crear desde puntos o curvas de nivel (baja prioridad) |
| Alineamiento | Listar, PK ↔ punto, intersecciones | Revisar criterios, editar geometría |
| Perfil | Listar, leer PVIs | Editar la rasante |
| Peralte | — | Leer y ajustar |
| Ensamblajes | Listar, asignar a región | Parámetros de subensamblajes |
| Corredor | Regiones, objetivos, frecuencias, superficies, reconstruir, deshacer | Diagnóstico |
| Secciones | Listar líneas de muestreo | Crearlas, leer secciones |
| Cantidades | — | Volúmenes, materiales, diagrama de masas |

### Prioridad 1

**1. Parámetros de subensamblajes**
- Herramientas: `listar_parametros_subensamblaje` y `establecer_parametros_subensamblaje` (en lote, con `simular`,
  devolviendo `antes`/`despues`).
- Qué permite: anchos de carril, bombeo, taludes de corte y relleno, profundidad de cunetas, por ensamblaje o por
  región.
- Por qué va primero: casi toda corrección real de un corredor acaba siendo cambiar un parámetro en un tramo.
- API por verificar: los parámetros de `Subassembly` (`ParamsDouble`, `ParamsString`, `ParamsBool`, `ParamsLong` o
  equivalentes).
- Ojo: comprobar si el cambio se deshace con `_.UNDO` o si necesita un deshacer propio, como pasó con los
  objetivos en la 1.3.

**2. Diagnóstico de corredor**
- Herramienta: `diagnosticar_corredor`.
- Devuelve, en una sola llamada: advertencias de la reconstrucción, huecos y solapes entre regiones, objetivos sin
  asignar o que apuntan a objetos que ya no existen, frecuencias fuera de rango, y si el corredor está desactualizado.
- Va con una receta en `INSTRUCCIONES_AGENTE.md`: el orden en que se revisa y se corrige un corredor.

**3. Revisión de la geometría contra la norma**
- Herramienta: `revisar_criterios_diseno`, solo lectura.
- Parámetros: `alineamiento`, `perfil`, `velocidad_diseno`, `norma` (por ejemplo DG-2018) y un archivo de tabla de
  criterios opcional.
- Revisa radios mínimos, longitudes de tangente y de transición, pendientes máximas y mínimas, valores K de las
  curvas verticales (cóncavas y convexas) y la longitud mínima de curva vertical.
- Devuelve cada incumplimiento con PK, valor, límite y norma.
- Mejor calcularlo nosotros a partir de las entidades del alineamiento y del perfil, con la tabla de la norma en un
  archivo editable. Así no dependemos de los `DesignCheckSet` de Civil 3D, que están poco expuestos en la API.

**4. Editar la rasante**
- Herramienta: `editar_pvis` (en lote, con `simular` y deshacer).
- Acciones: mover un PVI (PK y cota), añadir, eliminar, y cambiar la longitud o el tipo de la curva vertical.
- Devuelve las pendientes y los valores K resultantes para que la IA compruebe el cambio junto con el punto 3.
- API por verificar: `Profile.PVIs` y sus métodos de añadir y eliminar, y las entidades de curva vertical.

**5. Volúmenes y cantidades**
- Herramientas: `crear_lineas_muestreo` (grupo, intervalo, anchos a izquierda y derecha, PK inicial y final),
  `calcular_volumenes` (corte y relleno por tramo entre dos superficies o por lista de materiales) y
  `exportar_cantidades` (CSV o Excel en la carpeta que se indique).
- Es la "extracción de datos" típica: tablas para el metrado o el presupuesto.
- API parcial: si la lista de materiales no se puede crear bien vía API, usar el comando de Civil 3D por la vía de
  escape y leer el resultado.

### Prioridad 2

**6. Ejecutar código con copia y deshacer**
- Herramienta: `ejecutar_codigo`, el equivalente al `execute_code` de Revit.
- Ejecuta C# (con Roslyn) o Python con acceso a la API de Civil 3D, bajo la misma copia de seguridad, el bloqueo de
  escritura y el registro que el resto de las escrituras.
- Es la herramienta que más amplía lo que el agente puede hacer. Hoy la vía de escape (`ejecutar_comando`) solo
  lanza comandos de la línea de comandos.
- Seguridad: solo con token, siempre con copia, `simular` que compile sin ejecutar, y una traza en el historial con
  el código ejecutado.

**7. Peralte**
- Herramientas: `listar_peralte` y `establecer_peralte` (PK críticos, tasas, criterio).
- API delicada: validar muy bien en Civil 3D antes de usarla en un proyecto real.

**8. Edición del alineamiento**
- Herramienta: `editar_alineamiento` (radio de una curva, longitud de las espirales, mover un PI).
- Lo último de la lista: las restricciones entre entidades (fijas, flotantes, libres) complican la API.

### Prioridad 3

- Crear superficies desde puntos, curvas de nivel o un CSV.
- Crear un ensamblaje con subensamblajes del catálogo. La API lo permite solo en parte: empezar por duplicar un
  ensamblaje existente y cambiar sus parámetros (punto 1).
- Diagrama de masas.
- Etiquetas y planos (planta y perfil), probablemente por la vía de escape.

Total estimado: 12–15 herramientas nuevas, hasta unas 55–58. Sigue dentro del rango sano.

## Revit: lo pendiente

1. **Validar la 2b** (acero y modelo analítico) con `herramientas-dev/VALIDACION_2B.md`, pasar la tabla de miembros
   a `ejecutado` y corregir lo que falle (0.5.x).
2. **2c: editor de familias.** Es lo más difícil vía API (documento de familia, planos de referencia, parámetros,
   tipos) y lo que más aporta a las tareas complejas. Empezar por recetas más `execute_revit_code`. Pasar a
   herramientas fijas solo lo que se repita: crear parámetros, tipos y una extrusión simple, por ejemplo.

## Interoperabilidad Civil 3D ↔ Revit

### Cómo funciona

No hace falta que los dos plugins se hablen entre sí. **El agente es el puente:** con los dos MCP conectados a la vez,
lee de uno, decide y escribe en el otro. Hay dos canales, según el tamaño de los datos:

| Canal | Cuándo | Ejemplo |
|---|---|---|
| **Directo por el agente** | Datos pequeños: unas decenas de valores | Leer los niveles y el NPT de un edificio en Revit y usarlos como cota objetivo de una plataforma en Civil 3D |
| **Por archivo** | Datos grandes: superficies, miles de puntos, geometría | Exportar la superficie del terreno a LandXML en Civil 3D e importarla como toposólido en Revit |

Los datos grandes nunca deben pasar por la conversación: gastan contexto y créditos y se pueden cortar. El agente solo
maneja rutas de archivo y resúmenes.

### Lo que ya existe

- Civil 3D → Revit por archivo: `exportar_landxml` (Civil 3D) y `import_from_civil` (Revit). Este último acepta
  LandXML y CSV (P,N,E,Z / X,Y,Z) para crear un toposólido, y DWG/DXF/DGN para vincularlo, con adquisición de
  coordenadas compartidas.
- Revit tiene `get_project_location` y `set_project_location` para el punto base y el punto de reconocimiento.

### Lo que faltaría

**Reglas comunes, lo primero**
- **Carpeta de intercambio fija**, por ejemplo `Documentos\ArbaIntercambio\<proyecto>\`. Cada archivo lleva al lado un
  `manifiesto.json` con origen, fecha, unidades, sistema de coordenadas y qué contiene.
- **Coordenadas:**
  - todo lo que se intercambia va en coordenadas del levantamiento (UTM o las del proyecto de Civil 3D), en metros;
  - Revit convierte con su punto de reconocimiento y el ángulo al norte real;
  - una herramienta en cada lado que diga la transformación en uso, para que el agente la compruebe antes de
    escribir nada.
- **Unidades:** Civil 3D en metros y Revit en mm en su API. Cada herramienta de intercambio declara sus unidades en
  el manifiesto.
- **Identidad:** un archivo de correspondencias (id de Revit ↔ handle de Civil 3D) cuando un objeto de un lado
  depende de otro, para poder actualizar en lugar de duplicar.
- **Siempre `simular` en el destino** antes de escribir, y el cambio en el destino con su copia y deshacer.

**Herramientas nuevas**
- Civil 3D:
  - `exportar_datos`: puntos, polilíneas 3D, líneas características, alineamientos o cotas de una superficie en
    una malla o una lista de puntos, a CSV o JSON en la carpeta de intercambio;
  - `importar_datos`: puntos o polilíneas desde CSV o JSON, como puntos COGO, líneas características o
    polilíneas 3D, con `simular`;
  - `sistema_coordenadas`: sistema, zona y unidades del dibujo.
- Revit:
  - `export_to_civil`: huella de edificios, niveles, NPT, puntos de acometida (tuberías, cajas) y límites de
    parcela, en coordenadas del levantamiento y metros, a CSV o JSON en la carpeta de intercambio.
  - Ampliar `import_from_civil` para ejes (alineamientos como líneas de modelo o de detalle) y rasantes (cotas por
    PK para rampas y accesos).

### Flujos de ejemplo

1. **Plataforma de un edificio.**
   - Revit: huella y NPT (`export_to_civil`).
   - Civil 3D: línea característica a la cota del NPT menos el espesor del piso, explanación hacia el terreno, y la
     superficie de la plataforma como objetivo o como superficie pegada.
2. **Acceso vial al edificio.**
   - Revit: puntos de entrada y cotas de los accesos.
   - Civil 3D: el agente revisa si la rasante del vial llega a esas cotas con pendientes dentro de la norma
     (`revisar_criterios_diseno`) y propone ajustes de PVIs (`editar_pvis`).
3. **Terreno en Revit.**
   - Civil 3D: superficie a LandXML.
   - Revit: `import_from_civil` crea el toposólido en coordenadas compartidas.
4. **Redes.**
   - Revit: cotas de salida de desagües (`export_to_civil`).
   - Civil 3D: el agente comprueba la pendiente hasta el colector y avisa de los conflictos.
5. **Coordinación de cambios.** Si cambia el NPT en Revit, el agente vuelve a leerlo, localiza con el archivo de
   correspondencias la plataforma y el corredor afectados en Civil 3D, simula el cambio, te lo enseña y lo aplica.

### Coste de tener los dos MCP conectados

Con los dos conectados a la vez el agente carga unas 95 herramientas. Para el trabajo diario, conectar solo el del
programa que se esté usando, y conectar los dos solo para tareas de intercambio. Si se vuelve habitual, estudiar un
modo "intercambio" en cada puente que exponga solo las herramientas de lectura y de intercambio.

## Orden sugerido al retomar

1. Revit: validar la 2b y corregir (0.5.x).
2. Civil 3D 1.4: subensamblajes, diagnóstico de corredor, revisión de criterios y rasante (puntos 1 a 4).
3. Civil 3D 1.5: volúmenes y cantidades (punto 5) y `ejecutar_codigo` (punto 6).
4. Interoperabilidad: carpeta de intercambio, manifiesto, coordenadas, `exportar_datos`/`importar_datos` y
   `export_to_civil`. Probar con el flujo 1 (plataforma de un edificio).
5. Revit 2c (familias).
6. Peralte, edición del alineamiento y el resto de la prioridad 3.

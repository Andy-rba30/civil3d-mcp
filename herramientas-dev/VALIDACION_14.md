# Prompt de validación de la 1.4.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectados el MCP de Civil 3D y, para los pasos 6 a 9, el MCP de
Revit. Requisitos previos:

- Civil 3D 2027 abierto con **una copia** de un DWG georreferenciado (Configuración del dibujo → Unidades y zona con
  un sistema asignado, por ejemplo `UTM84-18S`) que tenga al menos una superficie TIN que **no** genere ningún
  corredor. Dibujo guardado en disco.
- El plugin **1.4.0** se instala en el paso 0 desde la rama `feature/extension-superficie`; el bundle queda con
  `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.4.0 y el cliente MCP reiniciado para
  que cargue la lista de **44** herramientas.
- Para los pasos 6 a 9: Revit 2027 con pyRevit y la extensión `revit-mcp` de la rama
  `feature/coordenadas-compartidas` (herramientas `get_project_coordinates`, `set_project_coordinates` y `link_file`
  con `placement`), con un **proyecto nuevo de prueba** abierto (nunca un modelo de entrega: el paso 8 cambia sus
  coordenadas compartidas).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.4.0**: la herramienta de lectura `extension_superficie` (extensión, centro, `origen_sugerido`,
  sistema de coordenadas del dibujo y avisos de 16 km y 33 km) y la sección 7 de `INSTRUCCIONES_AGENTE.md` con el
  flujo para vincular una superficie en Revit por coordenadas compartidas, sin el aviso "muy lejos del origen".
  **Los pasos 2 y 9 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección
  1.4.0): `Surface.GetGeneralProperties()` y sus propiedades, y
  `CivilDocument.Settings.DrawingSettings.UnitZoneSettings` (`CoordinateSystemCode`, `DrawingUnits`).

---

Eres el agente de validación de la versión 1.4.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo.

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `feature/extension-superficie`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. Reinicia el puente y el cliente MCP. Abre Civil 3D
   con la copia del DWG. `ping` → `version` debe ser `1.4.0`. Pide la lista de herramientas: deben ser 44 e incluir
   `extension_superficie`. Si no, para aquí.

1. **Elegir la superficie.** `listar_superficies`. Anota una superficie TIN con `corredor` nulo: la llamaremos `SUP`.

2. **Extensión (decisivo).** `extension_superficie` con `superficie=SUP`. Anota el JSON completo. En Civil 3D abre
   Propiedades de superficie → Estadísticas → General y compara: `min_x`/`max_x` con Mín./Máx. X, `min_y`/`max_y` con
   Mín./Máx. Y, `min_z`/`max_z` con Elevación mín./máx. y `n_puntos` con Número de puntos (tolerancia 0,01 en
   coordenadas; igualdad en puntos). Abre Configuración del dibujo → Unidades y zona y compara
   `sistema_coordenadas.codigo` con el código de la zona y `sistema_coordenadas.unidades` con las unidades del dibujo.
   Comprueba que `centro` es el punto medio de los mínimos y máximos, que `origen_sugerido.este`/`norte` son el centro
   redondeado a 100 y `cota` el centro de cotas redondeado a 10, y que `avisos` está vacío si `diagonal` es menor de
   16 000 m (si la superficie es mayor, debe traer el aviso correspondiente). OK solo si todo coincide.

3. **Superficie sin sistema (opcional).** Si tienes un DWG sin sistema de coordenadas asignado, repite el paso 2 en él:
   `sistema_coordenadas.codigo` debe ser `null` y `nota` debe decir que el dibujo no tiene sistema asignado.

4. **Error controlado.** `extension_superficie` con `superficie=NO_EXISTE`. Esperado: `ok: false` con "No existe la
   superficie 'NO_EXISTE'. Usa listar_superficies."

5. **Sin entrada en Deshacer.** Abre el menú Deshacer de Civil 3D: los pasos 2 a 4 no deben haber añadido ninguna
   entrada (`extension_superficie` es lectura en contexto `Aplicacion` con bloqueo de lectura). Anota `ms` del paso 2:
   debe ser menor de 2000 ms aunque la superficie sea grande (lee propiedades, no recorre puntos).

6. **Revit, estado inicial.** En el MCP de Revit, con el proyecto nuevo de prueba abierto: `get_project_coordinates`.
   Esperado: `shared_coordinates_set: false` e `internal_origin` con `east`, `north` y `elevation` en 0.

7. **Revit rechaza el vínculo sin coordenadas.** `link_file` con `file_path` = ruta de la copia del DWG y
   `placement="shared"`. Esperado: error que empieza por "placement 'shared' needs shared coordinates" y que nombra
   `set_project_coordinates`. No debe quedar ningún vínculo en Revit.

8. **Georreferenciar Revit.** `set_project_coordinates` con `east`, `north` y `elevation` = `origen_sugerido` del
   paso 2, `units="m"`, `angle_degrees=0`. Luego `get_project_coordinates`: `shared_coordinates_set: true` e
   `internal_origin` igual a lo enviado (tolerancia 0,001 m). En Revit, Gestionar → Coordenadas → Especificar
   coordenadas en un punto sobre el origen interno debe mostrar esos mismos valores.

9. **Vincular por coordenadas compartidas (decisivo).** `link_file` con la ruta del DWG y `placement="shared"`.
   Esperado: `ok` con `placement: shared`, `extents.far_from_origin: false`, `extents.beyond_revit_limit: false`,
   `extents.max_distance_m` del orden de la mitad de la diagonal del paso 2 y `extents.center_shared_m` igual a
   `centro` del paso 2 (tolerancia 1 m). En Revit **no debe aparecer** el cuadro "la geometría está muy lejos del
   origen" ni ninguno parecido, y la superficie debe verse junto al origen interno en una vista 3D. Luego Gestionar →
   Vínculos → la posición del DWG debe decir "Compartido". Si el cuadro aparece o `far_from_origin` es true, anota el
   valor de `max_distance_m` y para aquí.

10. **Comprobación cruzada.** Elige un punto conocido de la superficie (por ejemplo un vértice visible) y con
    `cota_superficie` en Civil 3D y la cota del mismo punto en Revit (coordenadas compartidas, Z) comprueba que
    coinciden (tolerancia 0,05 m).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_14_<AAAAMMDD>.md` con: versión de Civil 3D y de Revit, DWG
usado (nombre, sistema de coordenadas, `diagonal`), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, y una lista final de los miembros de la sección 1.4.0 de `miembros_por_verificar_civil3d.md` que pasan a
`ejecutado en Civil 3D 2027` con la fecha. Si el paso 9 falla, incluye una captura (`capturar_pantalla`) del cuadro de
Revit y el JSON completo de `link_file`.

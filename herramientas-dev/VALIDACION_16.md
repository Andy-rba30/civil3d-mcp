# Prompt de validación de la 1.6.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- Civil 3D 2027 abierto con **una copia** de un DWG de prueba **guardado en disco** (cualquiera con al menos una
  superficie; no hace falta corredor). Anota la carpeta del DWG: ahí aparecerá `mcp_log.jsonl`.
- El plugin **1.6.0** se instala en el paso 0 desde la rama `tarea/5.1-registro`; el bundle queda con `ArbaMcp.dll` y
  `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.6.0 (`uv sync` antes, por si acaso) y el cliente MCP
  reiniciado para que cargue la lista de **45** herramientas del plugin (más `buscar_api` del puente).
- Para el paso 9: PowerShell en `PuenteMcp` y la variable de usuario `MEMORIA_FIRMA_DIR` apuntando a la carpeta de
  `memoria-firma` (si no está, el paso 9 usa `--salida`).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.6.0**: el parámetro opcional `pedido` en **todas** las herramientas (solo se guarda), una línea
  en `mcp_log.jsonl` por **cada** llamada (también las lecturas) con `id`, `pedido` y `version`, el campo `id_log` en
  toda respuesta, la herramienta `aprobar_resultado` (anota una línea `aprobacion`, no toca el dibujo) y el script
  `exportar_dataset` del puente (pares `pedido → herramientas` de las llamadas aprobadas, anonimizados).
  **Los pasos 2, 4 y 9 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección
  1.6.0): ninguno nuevo. Lo que se decide es comportamiento: que el registro de las lecturas y de `aprobar_resultado`
  **no deja entrada en Deshacer** (paso 7) y que las herramientas asíncronas dejan su línea (paso 8).

---

Eres el agente de validación de la versión 1.6.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `tarea/5.1-registro`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`, `uv sync`. Reinicia el puente y
   el cliente MCP. Abre Civil 3D con la copia del DWG. `ping` con `args={"pedido":"comprueba que el plugin responde"}`
   → `version` debe ser `1.6.0` y la respuesta debe traer `id_log` con la forma `AAAAMMDD-HHMMSS-xxxxxx`. Pide la
   lista de herramientas: deben ser 45 del plugin, incluir `aprobar_resultado`, y **cada una** debe mostrar el
   parámetro `pedido` (string, opcional). Si no, para aquí.

1. **Borrón y cuenta nueva (opcional).** Si en la carpeta del DWG ya existe `mcp_log.jsonl` de versiones anteriores,
   renómbralo a `mcp_log_anterior.jsonl` para que el paso 4 sea legible. No lo borres.

2. **Lectura con pedido (decisivo).** `listar_superficies` con `args={"pedido":"lista las superficies"}`. Esperado:
   la lista de superficies de siempre **y** un campo `id_log` (anótalo: lo llamaremos `ID1`). Abre `mcp_log.jsonl`
   en la carpeta del DWG: la última línea debe ser
   `{"tipo":"llamada","id":"<ID1>","hora":"...","herramienta":"listar_superficies","pedido":"lista las superficies","args":{"pedido":"lista las superficies"},"ok":true,"ms":<n>,"error":null,"version":"1.6.0"}`
   (el orden de los campos puede variar). OK solo si `id` de la línea = `id_log` de la respuesta y `pedido` está
   escrito tal cual.

3. **`pedido` no cambia nada.** Repite `listar_superficies` **sin** `pedido`. Esperado: la misma lista que en el
   paso 2 (mismos nombres y valores), un `id_log` distinto (`ID2`) y una línea nueva en el log con `"pedido":null`.

4. **Aprobar (decisivo).** `aprobar_resultado` con `args={"id_log":"<ID1>","aprobado":true,"comentario":"correcto"}`.
   Esperado: `ok` con `id` (nuevo), `id_log` = `ID1`, `aprobado: true`, `comentario: "correcto"`,
   `herramienta: "listar_superficies"`, `hora_llamada` (la del paso 2), `pedido: "lista las superficies"`,
   `llamada_ok: true` y `ruta` del log; y además el `id_log` propio de esta llamada (`ID3`). En `mcp_log.jsonl`
   deben haberse añadido **dos** líneas: `{"tipo":"aprobacion","id":"...","hora":"...","id_log":"<ID1>","aprobado":true,"comentario":"correcto"}`
   y la línea `llamada` de `aprobar_resultado` (con `id` = `ID3`). `leer_historial` debe mostrar
   `Aprobación <ID1> (listar_superficies): aprobado - correcto`.

5. **leer_log muestra las dos clases de línea.** `leer_log` con `args={"ultimas_n":10}`. Esperado: `ruta` y
   `lineas[]` donde aparecen, en orden, la línea `llamada` de `listar_superficies` (`id` = `ID1`, `pedido` = "lista
   las superficies"), la de `ID2`, la línea `aprobacion` con `id_log` = `ID1` y la `llamada` de `aprobar_resultado`.
   (La propia llamada a `leer_log` se escribe después de leer, así que no se ve a sí misma.)

6. **Error controlado.** `aprobar_resultado` con `args={"id_log":"no-existe","aprobado":true}`. Esperado: `ok: false`
   y un `error` que contiene "No existe ninguna llamada con id_log 'no-existe'" y "Usa leer_log". La respuesta de
   error también trae `id_log`. Luego `aprobar_resultado` con `args={"id_log":"<ID1>"}` (sin `aprobado`): error
   "Falta el parámetro obligatorio 'aprobado'". En el log no debe haber ninguna línea `aprobacion` nueva por estos
   dos intentos (sí dos líneas `llamada` con `ok:false`).

7. **Sin entrada en Deshacer.** Abre el menú Deshacer de Civil 3D: los pasos 2 a 6 no deben haber añadido ninguna
   entrada (`listar_superficies` es lectura en contexto `Aplicacion`; `aprobar_resultado` y `leer_log` son
   `Inmediato`; el registro solo escribe un archivo). Comprueba también que **no** apareció ningún archivo nuevo en
   `backups\` (ninguna de estas herramientas hace copia de seguridad).

8. **Escritura simulada y asíncrona.** `establecer_frecuencia` con `simular=true` sobre cualquier corredor y región
   (si el dibujo no tiene corredor, usa `guardar_copia` con `simular=true` y `sufijo="prueba16"`), con
   `pedido="simula un cambio de frecuencia"`. Esperado: la simulación de siempre más `id_log`, y en el log **una**
   línea `llamada` con ese `pedido` (no dos). Luego `ejecutar_comando` con `comando="REGEN"` y
   `pedido="regenera el dibujo"`: respuesta `terminado` con `id_log` y, en el log, su línea `llamada` con
   `herramienta: "ejecutar_comando"` y `ok: true` (las asíncronas registran al terminar, por la cola `Inmediato`).

9. **exportar_dataset (decisivo).** En PowerShell, desde `PuenteMcp`, con `<carpeta>` = la carpeta del DWG:
   `uv run --frozen python exportar_dataset.py --logs <carpeta> --salida <carpeta>\pares_prueba.jsonl`
   (si `MEMORIA_FIRMA_DIR` está puesta, corre además sin `--salida` y comprueba que escribe en
   `%MEMORIA_FIRMA_DIR%\dataset\pares.jsonl`). Esperado en pantalla:
   `exportar_dataset: <n> líneas de 1 archivo(s) mcp_log.jsonl, 1 llamadas aprobadas, 1 pares → <carpeta>\pares_prueba.jsonl`
   (si no hay `PROHIBIDOS.txt`, con el aviso "sin lista de prohibidos" al final). Abre `pares_prueba.jsonl`: una sola
   línea, `{"pedido":"lista las superficies","herramientas":[{"nombre":"listar_superficies","args":{}}],"resultado":{"ok":true,"resumen":"1 llamada correcta"},"fecha":"<AAAA-MM-DD de hoy>","version":"1.6.0"}`.
   OK solo si la línea **no contiene ninguna ruta** (ni `C:\`, ni el nombre del DWG, ni `.dwg`) y solo entra la
   llamada aprobada en el paso 4 (ni `ID2`, ni `aprobar_resultado`, ni `ejecutar_comando`). Si `pedido` está en
   blanco o aparece más de un par, es un fallo.

10. **Aprobación de una escritura simulada y agrupamiento (opcional).** Aprueba con `aprobar_resultado` el
    `id_log` de la simulación del paso 8 y también el de `ejecutar_comando` con `comentario="también bien"`; vuelve a
    correr el paso 9. Esperado: 3 llamadas aprobadas y 3 pares (pedidos distintos no se agrupan), y en el par de la
    simulación `args` conserva `simular: true` pero **no** `pedido`.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_16_<AAAAMMDD>.md` con: versión de Civil 3D, DWG usado (solo el
nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal / tiempo, el
contenido de `pares_prueba.jsonl` del paso 9 y la lista de comprobaciones de comportamiento de la sección 1.6.0 de
`miembros_por_verificar_civil3d.md` (Deshacer en el paso 7, línea de las asíncronas en el paso 8) que pasan a
`ejecutado en Civil 3D 2027` con la fecha. **No pegues en el informe líneas de `mcp_log.jsonl` con rutas reales**:
sustitúyelas por `<ruta>`.

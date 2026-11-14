# Prompt de validación de la 1.16.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Va antes de la validación de la 3.2 (recetas)**, en la misma instalación. Si la orquestadora te dio una rama que junta
  la 3.1 y la 3.2, instala esa: entonces `ping` dará la versión de esa rama (por ejemplo `1.17.0`) y la lista tendrá
  también las herramientas de la 3.2; anótalo y sigue. Si no, rama `tarea/3.1-ejecutar-codigo`, versión `1.16.0`.
- **Dibujo**: una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica y un
  ensamblaje, nada más) guardada como `C:\Proyectos\dibujos_prueba\PRUEBA_116.dwg`. Tiene que estar **guardada en
  disco**: así la copia de seguridad es `copia_de_disco`. En esa carpeta aparecerán `mcp_log.jsonl` y `backups\`.
  Ningún dato real.
- **La variable `ARBA_MCP_CODIGO` NO debe estar puesta al empezar**: el paso 2 comprueba la herramienta desactivada.
  El paso 3 la activa y **se deja activada al terminar** (la necesita la validación de la 3.2, que la quita al final).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.16.0**: la herramienta `ejecutar_codigo` (55 herramientas del plugin), **desactivada por defecto**.
  Compila un fragmento de C# con Roslyn (en `Contents\roslyn` del bundle y en su propio contexto de carga) y lo ejecuta
  en el hilo principal con el dibujo bloqueado. Una lista blanca impide la red, los procesos, los archivos fuera de
  `%LOCALAPPDATA%\ArbaMcp\scripts`, la reflexión, los hilos, el código nativo, los eventos y los comandos. Por defecto
  **simula**: hace la copia de seguridad, ejecuta dentro de una transacción que se aborta y dice qué habría creado. Con
  `simular=false` aplica los cambios (copia, una entrada de Deshacer, `mcp_log.jsonl`, antes/después). Tiempo límite de
  60 s. **Los pasos 0, 2, 4, 5, 6, 8 y 9 son los decisivos.** Lo que Jose mira en pantalla (paso 5) no decide.
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.16.0):
  Roslyn dentro de Civil 3D, `Transaction.GetAllObjects()`, `DBObject.IsNewObject`/`IsModified`/`IsErased`,
  `ObjectId.IsValid`/`IsErased`, `TransactionManager.TopTransaction` y los eventos `Database.ObjectAppended`,
  `ObjectModified` y `ObjectErased`.

---

Eres el agente de validación de la versión 1.16.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso** (`ms_puente`).

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` se usa **solo** donde este prompt lo indica (el `_.UNDO 1` del paso 5). Si algo no funciona, es un
  hallazgo: no lo arregles con otro comando ni con otro script.
- En `codigo_csharp` y `pedido` usa **exactamente** el texto que se indica (los `\n` son saltos de línea dentro del texto).
- **Menú Deshacer**: anota (la flecha junto al botón Deshacer) **antes y después** de los pasos 2, 4, 5, 6, 8 y 9 qué
  entradas aparecen. Lo esperado: un error de compilación o la herramienta desactivada **no** añaden ninguna; cada
  ejecución (simulación o real) añade **una** `Executefunction` (la de la simulación, vacía).
- **`backups\`**: anota cuántos archivos hay antes y después de los pasos 2, 4, 6 y 8. Un error de compilación o la
  herramienta desactivada **no** crean copia; una ejecución sí (nueva, o `copia.reutilizada: true` si el `.dwg` no cambió).

## Pasos

0. **Instalar y versión (decisivo).** Con Civil 3D cerrado, en PowerShell desde la raíz del repo:
   `git fetch origin`, `git checkout tarea/3.1-ejecutar-codigo` (o la rama conjunta) y `git pull`; después
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. Debe terminar con
   `Instalado en: ... (ArbaMcp.dll + ArbaMcp.Nucleo.dll + roslyn\ para ejecutar_codigo)`. Comprueba el bundle:
   ```powershell
   Get-ChildItem "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\Contents" -Recurse -File | Select-Object FullName, Length
   (Get-Item "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\Contents\roslyn\Microsoft.CodeAnalysis.CSharp.dll").VersionInfo.FileVersion
   [Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')
   ```
   Esperado: `Contents\ArbaMcp.dll`, `Contents\ArbaMcp.Nucleo.dll`, `Contents\roslyn\Microsoft.CodeAnalysis.dll`,
   `Contents\roslyn\Microsoft.CodeAnalysis.CSharp.dll` y `Contents\roslyn\es\Microsoft.CodeAnalysis.resources.dll` y
   `...CSharp.resources.dll`; **ninguna** `Microsoft.CodeAnalysis*.dll` directamente en `Contents\`; la versión empieza
   por `5.0.`; la variable sale **vacía** (si vale algo, quítala con
   `[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO',$null,'User')` y anótalo). Solo para el informe (no decide):
   ```powershell
   Get-ChildItem "C:\Program Files\Autodesk" -Recurse -Filter "Microsoft.CodeAnalysis*.dll" -ErrorAction SilentlyContinue | Select-Object FullName, @{n='Version';e={$_.VersionInfo.FileVersion}}
   ```
   (dice si AutoCAD o Dynamo traen su propio Roslyn; puede salir vacío). En `PuenteMcp`, `uv sync`; reinicia el puente
   y **recarga el MCP `civil3d`** en el cliente. Abre Civil 3D **desde el menú Inicio** con `PRUEBA_116.dwg`. Después:
   `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.16.0"`, `"hay_dibujo": true`. En
   PowerShell: `curl.exe -s http://127.0.0.1:8765/ping` → `{"ok":true,"servidor":"ArbaMcp","version":"1.16.0"}`. Pide la
   lista de herramientas: **55** del plugin (más `buscar_api` del puente), con `ejecutar_codigo` y sus parámetros
   `codigo_csharp` (obligatorio), `simular`, `parametros` y `pedido`. Si la versión no es la de la rama, para aquí.

1. **Comprobar el dibujo** (solo lectura, no decisivo). `listar_superficies`, `listar_alineamientos` y
   `listar_ensamblajes`, cada una con `args={"pedido":"lista lo que hay en el dibujo"}`. Esperado: sin superficies ni
   alineamientos y al menos un ensamblaje. Anota las tres listas.

2. **Herramienta desactivada (decisivo).** `ejecutar_codigo` con
   `args={"codigo_csharp":"return 1;","pedido":"prueba con la herramienta desactivada"}`. Esperado: `ok: false` y un
   error que contiene `ejecutar_codigo está desactivada: se activa con la variable de entorno de usuario
   ARBA_MCP_CODIGO=1 y reiniciando Civil 3D` (delante puede ir `InvalidOperationException: `). **Ningún** archivo nuevo
   en `backups\` y **ninguna** entrada nueva en Deshacer. `leer_log` con `args={"ultimas_n":1}`: la línea es de
   `ejecutar_codigo`, `ok: false` y el mismo error. `leer_historial` con `args={"ultimas_n":10}`: **no** aparece
   `ejecutar_codigo: compilado` (con la herramienta desactivada no se compila nada).

3. **Activar la herramienta** (lo hace el agente instalador; no decide). Cierra Civil 3D. En PowerShell:
   ```powershell
   [Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO','1','User')
   [Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')
   ```
   La segunda línea debe imprimir `1`. Abre Civil 3D **desde el menú Inicio o el acceso directo** (no desde una ventana
   de PowerShell o de comandos abierta antes de poner la variable: esa ventana no la ve) con `PRUEBA_116.dwg`, y recarga
   el MCP `civil3d`. `ping` → `"hay_dibujo": true`.

4. **Simulación (decisivo).** Tres llamadas seguidas, todas sin `simular` (por defecto es `true`):
   - **4a, contar las líneas**: `ejecutar_codigo` con
     `args={"codigo_csharp":"int n = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line) n++;\nreturn n;","pedido":"cuenta las líneas del espacio modelo"}`.
     Anota `resultado`: es **N0** (en el dibujo base debería ser `0`). Debe traer `simulado: true` y `copia` (es la
     primera ejecución: copia nueva en `backups\`).
   - **4b, la línea en simulación**: `ejecutar_codigo` con
     `args={"codigo_csharp":"var l = new Line(new Point3d(1000, 2000, 0), new Point3d(1100, 2000, 0));\nF.Agregar(l);\nF.Escribir(\"línea de prueba\");\nreturn new { largo = l.Length, capa = l.Layer };","pedido":"dibuja una línea de prueba de 100 m"}`.
     Esperado: `ok: true`, `simulado: true`, `herramienta: "ejecutar_codigo"`, `accion` empieza por
     `Ejecutar un script de 4 línea(s) en simulación`, `antes.objetos_creados: 0`, `despues.objetos_creados: 1`,
     `cambios` incluye `objetos_creados` (si incluye también `objetos_modificados`, anota qué objeto sale en
     `datos.objetos.modificados`; lo normal es el espacio modelo, `BlockTableRecord`), `datos.objetos.creados` con
     **un** elemento con `tipo: "Line"` (anota su `capa`), `resultado.largo: 100`, `datos.mensajes: ["línea de prueba"]`,
     `copia` con `ruta` en `backups\` (`reutilizada: true` es correcto: el `.dwg` no cambió), `avisos` cuyo primero
     empieza por `Simulación: el script se ejecutó de verdad dentro de una transacción que se abortó al final` y **sin**
     ningún aviso que empiece por `ATENCIÓN`. Anota también `datos.compilacion.roslyn` (debe empezar por `5.0`),
     `datos.compilacion.ms` y `datos.eventos_bd` (`vigilados`, o el texto literal si dice `no disponibles: ...`).
   - **4c, contar otra vez**: la misma llamada que 4a. Esperado: `resultado` igual a **N0**: la simulación no dejó la
     línea en el dibujo. **Este es el resultado decisivo del paso.** En `datos.compilacion.de_cache` debe salir `true`.
   Anota el menú Deshacer: lo esperado es una `Executefunction` por cada llamada (tres), que no deshacen nada.

5. **Escritura real (decisivo).**
   - **5a**: la misma llamada que 4b con `"simular": false` y `"pedido":"dibuja de verdad la línea de prueba de 100 m"`.
     Esperado: `simulado: false`, `mensaje` empieza por `Script de 4 línea(s) ejecutado y confirmado: 1 objeto(s)
     creado(s)`, `despues.objetos_creados: 1`, `resultado.largo: 100`, `copia` presente y **una** `Executefunction`
     nueva en Deshacer. `leer_log` con `args={"ultimas_n":1}`: herramienta `ejecutar_codigo`, `ok: true`, en `args` el
     `codigo_csharp`, `"simular": false` y el `pedido`. **Jose** (no decide): en Civil 3D, `ZOOM` → `Extensión` muestra
     una línea de (1000, 2000) a (1100, 2000) en el espacio modelo.
   - **5b**: `ejecutar_comando` con `args={"comando":"_.UNDO 1"}` → `"terminado"`.
   - **5c**: la llamada de 4a (contar). Esperado: `resultado` igual a **N0**: un solo `_.UNDO 1` quitó la línea. Si da
     N0 + 1, anota el menú Deshacer y sigue (es un hallazgo).

6. **Un script con red no compila (decisivo).** `ejecutar_codigo` con
   `args={"codigo_csharp":"var cliente = new System.Net.Http.HttpClient();\nreturn cliente.GetStringAsync(\"https://example.com\").Result;","pedido":"intenta descargar una página web"}`.
   Esperado: `ok: false`, error que contiene
   `ejecutar_codigo: el código no se puede ejecutar (3 usos no permitidos). No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.`
   y las líneas
   `- Línea 1, columna 1: [bloqueado: red] System.Net.Http.HttpClient: ejecutar_codigo no admite acceso a la red.` y
   `- Línea 2, columna 8: [bloqueado: hilos] System.Threading.Tasks.Task<string>: ...`. **Ningún** archivo nuevo en
   `backups\`, **ninguna** entrada nueva en Deshacer, y `ms_puente` de unos pocos segundos como mucho. `leer_historial`
   muestra `ejecutar_codigo: no compila (2 líneas, ...)`.

7. **Procesos, archivos y la carpeta permitida** (no decisivo).
   - **7a**: `args={"codigo_csharp":"System.Diagnostics.Process.Start(\"notepad.exe\");\nreturn null;"}` → `ok: false`
     con `[bloqueado: procesos] System.Diagnostics.Process`. **No** se abre el Bloc de notas.
   - **7b**: `args={"codigo_csharp":"System.IO.File.WriteAllText(@\"C:\\Proyectos\\fuera.txt\", \"x\");\nreturn null;"}` →
     `ok: false` con `[bloqueado: archivos] System.IO.File` y la mención de `F.EscribirTexto`. En PowerShell,
     `Test-Path C:\Proyectos\fuera.txt` → `False`.
   - **7c**: `args={"codigo_csharp":"F.EscribirTexto(\"prueba116_sim.txt\", \"x\");\nreturn 1;"}` (simulación) →
     `datos.archivos.no_escritos_por_simulacion: ["prueba116_sim.txt"]` y un aviso
     `Simulación: no se escribió ningún archivo; ...`. En PowerShell,
     `Test-Path "$env:LOCALAPPDATA\ArbaMcp\scripts\prueba116_sim.txt"` → `False`.
   - **7d**: `args={"codigo_csharp":"F.EscribirTexto(\"prueba116.txt\", \"hola\");\nreturn F.ListarArchivos();","simular":false,"pedido":"escribe un archivo de prueba en la carpeta de scripts"}` →
     `datos.archivos.escritos: ["prueba116.txt"]` y `resultado` contiene `"prueba116.txt"`. En PowerShell,
     `Get-Content "$env:LOCALAPPDATA\ArbaMcp\scripts\prueba116.txt"` → `hola`. (Es una ejecución real sin cambios en el
     dibujo: deja una `Executefunction` en Deshacer y `objetos_creados: 0`.)

8. **Error de compilación con su mensaje (decisivo).** `ejecutar_codigo` con
   `args={"codigo_csharp":"var x = 1\nreturn x + y;","pedido":"script con errores a propósito"}`. Esperado: `ok: false`,
   error que contiene `ejecutar_codigo: el código no se puede ejecutar (2 error(es) de compilación)`,
   `- Línea 1, columna 10: CS1002 Se esperaba ;` y `- Línea 2, columna 12: CS0103 El nombre 'y' no existe en el contexto actual.`,
   cada uno con su línea de código, la marca `^` y una `Pista:`. (Si Civil 3D carga los mensajes en inglés, `CS1002
   ; expected` y `CS0103 The name 'y' does not exist...` también valen: anota cuál.) **Ningún** archivo nuevo en
   `backups\` y **ninguna** entrada nueva en Deshacer.

9. **Un script lento se corta por el tiempo límite (decisivo).** `ejecutar_codigo` con
   `args={"codigo_csharp":"long n = 0;\nwhile (true)\n{\n    n++;\n}","pedido":"prueba del tiempo límite"}` (simulación).
   Esperado, a los 60 s aproximadamente (`ms_puente` entre 60 000 y 90 000): `ok: false` y un error que empieza por
   `TimeoutException: El script superó el tiempo límite de 60 s y se detuvo en la línea 4` y contiene
   `No se aplicó nada: la transacción se abortó (era una simulación).` y `Copia de seguridad: `. **Mientras corre**,
   Civil 3D no responde (es lo esperado). Nada más terminar: `ping` responde y `listar_superficies` responde en menos de
   5 s; la llamada de 4a (contar) da **N0**. Anota el menú Deshacer.

10. **Error en tiempo de ejecución con su línea** (no decisivo). `ejecutar_codigo` con
    `args={"codigo_csharp":"var id = F.Alineamiento(\"NO_EXISTE\");\nreturn id;"}`. Esperado: `ok: false` con
    `El script falló en la línea 1: ArgumentException: No existe el alineamiento 'NO_EXISTE'. Usa listar_alineamientos.`
    y `No se aplicó nada: la transacción se abortó (era una simulación).`

11. **Parámetros** (no decisivo). `ejecutar_codigo` con
    `args={"codigo_csharp":"return new { capa = F.Texto(\"capa\"), doble = F.Numero(\"largo\") * 2, directo = Parametros.GetProperty(\"capa\").GetString() };","parametros":"{\"capa\":\"0\",\"largo\":50}"}`.
    Esperado: `resultado: {"capa":"0","doble":100,"directo":"0"}`. Después, con `"parametros":"[1,2]"` → `ok: false` con
    `El parámetro 'parametros' debe ser un objeto JSON`.

12. **Cierre** (no decisivo). **No borres la variable**: `[Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')`
    debe seguir dando `1` (la necesita la validación de la 3.2, que la quita al final). No guardes el dibujo. Anota cuántas
    líneas de `ejecutar_codigo` hay en `mcp_log.jsonl` (`leer_log` con `args={"ultimas_n":40}`) y las últimas 20 líneas de
    `leer_historial`.

Para quitar la variable cuando lo pida Jose (no en esta validación): cierra Civil 3D,
`[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO',$null,'User')` y vuelve a abrirlo desde el menú Inicio;
`ejecutar_codigo` volverá a responder `ejecutar_codigo está desactivada: ...`.

## Informe

**Guarda el informe como archivo** (no basta con el resumen en el chat) en
`herramientas-dev/informes/informe_validacion_116_<AAAAMMDD>.md`, con:

- Versión de Civil 3D, rama y commit instalados, versión de `ping`, DWG usado (nombre y carpeta) y la salida de los dos
  `Get-ChildItem` del paso 0 (el bundle y otras `Microsoft.CodeAnalysis*.dll` del equipo).
- Una tabla paso / resultado (OK, FALLO) / respuesta literal / tiempo (`ms_puente`).
- Por cada paso marcado: el menú Deshacer antes y después y los archivos de `backups\` antes y después.
- `datos.eventos_bd`, `datos.compilacion.roslyn` y `datos.compilacion.ms` de la primera y de una ejecución repetida.
- Una lista final de los miembros de la sección 1.16.0 de `miembros_por_verificar_civil3d.md` que pasan a
  `ejecutado en Civil 3D 2027` con la fecha, y los que fallaron con su error literal.
- Que la variable `ARBA_MCP_CODIGO` quedó en `1`.

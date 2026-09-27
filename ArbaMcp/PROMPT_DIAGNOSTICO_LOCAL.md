# Diagnóstico: ¿el plugin ArbaMcp causó los cierres de Civil 3D y los trazados en blanco?

Eres un agente que trabaja en el equipo Windows de Andy (Civil 3D 2027, español). Tu tarea es **solo de exploración**:
reunir evidencia y ejecutar una prueba de aislamiento para decidir si el plugin `ArbaMcp` (botón *Conexión IA*) es la
causa de dos problemas:

1. ERROR FATAL `Unhandled Access Violation Reading 0x003f Exception at 9A4127Bh` mientras Andy estaba en la pestaña
   Salida (trazar / publicar).
2. Al trazar, algunas ventanas gráficas con perfiles salen solo con la cuadrícula; las líneas aparecen al hacer doble
   clic dentro de la ventana (regeneración).

## Reglas (léelas dos veces)

- **No modifiques ni borres nada** del código, del repo `C:\IA\civil3d-mcp`, del bundle del plugin ni de la
  instalación de Civil 3D. No compiles. No ejecutes `instalar.ps1`. No instales programas.
- Los únicos cambios permitidos son: definir y luego borrar la variable de entorno de usuario `ARBA_MCP`, crear
  copias del dibujo con otro nombre, y crear archivos de texto de informe en el Escritorio.
- Trabaja siempre sobre **copias** del dibujo. Nunca abras la entrega original.
- En el informe pega las **salidas literales** de los comandos. No resumas con tus palabras lo que no hayas visto.
  Si un comando falla, pega el error y sigue con el siguiente paso. Si no sabes algo, escribe "no se pudo determinar".
- No des un veredicto hasta el bloque 6. No afirmes que el plugin es la causa (ni que no lo es) sin la prueba del bloque 4.
- Durante la prueba de aislamiento el servidor del plugin estará apagado: **tú no puedes controlar Civil 3D**. Guía a
  Andy paso a paso, espera su respuesta y anótala tal cual.

Empieza preguntando a Andy dos cosas y espera la respuesta:

- Fecha y hora aproximada del último ERROR FATAL y qué estaba haciendo justo antes (¿había pedido algo al agente en ese
  momento o en los minutos anteriores?).
- Ruta completa del DWG con el que falló (en la captura aparece `..._v2_interseccion3.dwg`; la carpeta habitual es
  `D:\CURSO CIVIL 3D\Tarea 3`).

Guarda todo en `%USERPROFILE%\Desktop\diagnostico_arbamcp\` (créala con `New-Item -ItemType Directory -Force`).

---

## Bloque 1: inventario (qué hay instalado y en qué estado)

Ejecuta en PowerShell y pega las salidas.

```powershell
$dll = "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\Contents\ArbaMcp.dll"
Test-Path $dll
(Get-Item $dll).VersionInfo | Select-Object FileVersion, ProductVersion
(Get-Item $dll).LastWriteTime
Select-String -Path "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\PackageContents.xml" -Pattern 'AppVersion|Version='
Get-ChildItem -Recurse "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle" | Select-Object FullName, Length, LastWriteTime
```

Qué significa: `1.1.x` es la versión básica (10 herramientas); `1.2.0` o `1.2.1` es la fase 1 (39 herramientas, con
`historial.log`); `1.2.2` es la versión con el arreglo de hilos (no debería estar instalada todavía). Anota la versión
exacta: es el dato más importante del informe.

```powershell
cd C:\IA\civil3d-mcp
git status
git branch --show-current
git log -3 --oneline
git fetch origin
git branch -r
```

Qué significa: `git status` con archivos modificados o borrados indica que alguien tocó el clon; anótalo, no lo
arregles. Si la carpeta no existe, dilo y busca el clon con
`Get-ChildItem C:\, D:\ -Directory -Recurse -Depth 3 -Filter civil3d-mcp -ErrorAction SilentlyContinue`.

```powershell
Get-Item "$env:LOCALAPPDATA\ArbaMcp\token" -ErrorAction SilentlyContinue | Select-Object LastWriteTime
Get-ChildItem "$env:APPDATA\Autodesk\ApplicationPlugins", "$env:ProgramData\Autodesk\ApplicationPlugins" -Directory -ErrorAction SilentlyContinue | Select-Object FullName, LastWriteTime
Get-ChildItem "$env:APPDATA\Autodesk" -Recurse -Include acad.lsp, acaddoc.lsp, *.fas, *.vlx -ErrorAction SilentlyContinue | Select-Object FullName, LastWriteTime
```

Qué significa: la fecha del `token` es la última vez que Civil 3D arrancó con el plugin cargado. La lista de bundles
muestra qué otros complementos se cargan (anótalos todos). Los `.lsp`/`.fas`/`.vlx` en el perfil de usuario se cargan
solos al abrir dibujos; si hay alguno que Andy no reconozca, anótalo.

## Bloque 2: ¿estaba el plugin haciendo algo cuando falló?

```powershell
$h = "$env:LOCALAPPDATA\ArbaMcp\historial.log"
Test-Path $h
if (Test-Path $h) { Get-Content $h -Tail 300 | Out-File "$env:USERPROFILE\Desktop\diagnostico_arbamcp\historial_ultimas300.txt" -Encoding utf8 }
# Sustituye la fecha por la del error que te dio Andy (formato yyyy-MM-dd):
if (Test-Path $h) { Select-String -Path $h -Pattern '^2026-09-2[0-9]' | Select-Object -Last 200 }
```

Qué buscar: líneas `MCP → nombre_de_herramienta` con hora cercana a la del error (5 minutos antes o menos). Las líneas
`Comando inicia:` / `Comando termina:` son solo registro de lo que Andy hacía a mano; no indican actividad del plugin.
Si el archivo no existe y la versión es 1.1.x, escribe: "sin historial persistente; no se puede saber si el plugin
recibía llamadas". Si existe `historial.1.log` en la misma carpeta, revísalo igual.

En la carpeta del dibujo (usa la ruta que te dio Andy):

```powershell
$carpeta = "D:\CURSO CIVIL 3D\Tarea 3"   # cámbiala por la real
Get-ChildItem $carpeta -Filter mcp_log.jsonl -ErrorAction SilentlyContinue
if (Test-Path "$carpeta\mcp_log.jsonl") { Get-Content "$carpeta\mcp_log.jsonl" -Tail 50 }
Get-ChildItem "$carpeta\backups" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object Name, Length, LastWriteTime
Get-ChildItem $carpeta -Filter *.dwg | Sort-Object LastWriteTime -Descending | Select-Object Name, Length, LastWriteTime
```

Qué significa: `mcp_log.jsonl` y `backups\` solo existen si alguna herramienta de **escritura** del plugin actuó sobre un
dibujo de esa carpeta; sus fechas dicen cuándo. Anota si el dibujo que falló recibió escrituras del plugin y cuándo.

## Bloque 3: registros de Windows y volcados

```powershell
$desde = (Get-Date).AddDays(-45)
$ev = Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=$desde} -ErrorAction SilentlyContinue |
      Where-Object { $_.ProviderName -in 'Application Error','.NET Runtime','Windows Error Reporting' -and $_.Message -match 'acad' }
$ev | Select-Object TimeCreated, Id, ProviderName | Format-Table -AutoSize
$ev | ForEach-Object { "==== $($_.TimeCreated) [$($_.ProviderName) $($_.Id)]"; $_.Message; "" } |
  Out-File "$env:USERPROFILE\Desktop\diagnostico_arbamcp\eventos_acad.txt" -Encoding utf8
Select-String -Path "$env:USERPROFILE\Desktop\diagnostico_arbamcp\eventos_acad.txt" -Pattern 'módulo|modulo|module|^P4:|^P7:|Exception|excepci'
```

Qué buscar: en cada evento, el **módulo con errores** (`Nombre del módulo con errores:` o `P4:`). Clasifica cada uno:

| Módulo | Qué sugiere |
|---|---|
| `ArbaMcp.dll`, `coreclr.dll`, `clrjit.dll`, `hostfxr.dll`, `acmgd.dll`, `acdbmgd.dll`, `AeccDbMgd.dll` | Código .NET / plugins; el plugin pasa a ser sospechoso principal |
| `nvoglv64.dll`, `nvwgf2umx.dll`, `nvd3dumx.dll`, `ig*.dll`, `d3d11.dll`, `dxgi.dll`, `AcGs*.dll`, `acgsopengl*.dll` | Gráficos / controlador de vídeo; no es el plugin |
| `AeccProfile*.dll`, `AeccPlot*`, `AcPlot*`, `acpublish*`, `AeccUiPlot*` | Trazado o perfiles de Civil 3D; puede ser el dibujo o el producto |
| `ntdll.dll` (heap), `KERNELBASE.dll`, `unknown` | No concluyente |

Si no hay ningún evento de `acad.exe`, dilo: AutoCAD suele atrapar el fallo con su propio cuadro de diálogo y Windows
no registra nada. Entonces busca los volcados que deja AutoCAD:

```powershell
Get-ChildItem $env:TEMP, "$env:LOCALAPPDATA\Autodesk" -Recurse -Include *.dmp -ErrorAction SilentlyContinue |
  Sort-Object LastWriteTime -Descending | Select-Object -First 10 FullName, Length, LastWriteTime
Get-ChildItem $env:TEMP -Filter *.xml -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $desde } |
  Sort-Object LastWriteTime -Descending | Select-Object -First 10 FullName, LastWriteTime
```

Anota rutas y fechas. No abras los `.dmp`; solo importa si su fecha coincide con el error y si hay alguno.

## Bloque 4: prueba de aislamiento (la que decide)

Objetivo: reproducir el trazado con el servidor del plugin **apagado**. Con `ARBA_MCP=0` el plugin se carga (se ve el
botón) pero no arranca el servidor, no engancha el evento `Idle` ni los eventos de comando y no ejecuta nada.

**4.1 Apagar el servidor.** Pide a Andy que cierre Civil 3D. Comprueba y define la variable:

```powershell
Get-Process acad -ErrorAction SilentlyContinue      # debe devolver nada
setx ARBA_MCP 0
```

Abre una PowerShell **nueva** (setx no afecta a la ventana actual) y comprueba:

```powershell
[Environment]::GetEnvironmentVariable("ARBA_MCP", "User")   # debe imprimir 0
```

**4.2 Arrancar y verificar que el servidor está apagado.** Pide a Andy que abra Civil 3D desde el menú Inicio (no
desde una ventana ya abierta). Cuando esté abierto:

```powershell
Test-NetConnection 127.0.0.1 -Port 8765 -InformationLevel Quiet   # debe imprimir False
```

Pide a Andy que escriba `ARBAMCP` en la línea de comandos: debe decir `Servidor MCP: INACTIVO`. Si dice "activo",
la variable no se aplicó; repite 4.1 y no sigas hasta que esté apagado. (El puente Python en el puerto 8001 puede
seguir corriendo; es irrelevante porque no tiene con quién hablar.)

**4.3 Preparar una copia del dibujo.**

```powershell
$dwg = "D:\CURSO CIVIL 3D\Tarea 3\NOMBRE_REAL.dwg"   # la ruta que te dio Andy
Copy-Item $dwg ($dwg -replace '\.dwg$', '_prueba_aislamiento.dwg')
```

**4.4 Reproducir, guiando a Andy.** Pídele que haga esto y anota cada respuesta:

1. Abrir la copia `_prueba_aislamiento.dwg`.
2. Escribir `BACKGROUNDPLOT` y decirte el valor actual (0, 1 o 2). Escribir `LAYOUTREGENCTL` y decirte el valor.
   Escribir `GRAPHICSCONFIG` y decirte si la aceleración por hardware está activada y qué tarjeta aparece.
3. Ir a la presentación donde faltaban las líneas de perfil (por ejemplo Lámina (11)) y hacer **Vista preliminar**
   (pestaña Salida → Vista preliminar). ¿Se ven las líneas de los perfiles o solo la cuadrícula? Cerrar la vista.
4. Hacer **Trazar por lotes / Publicar** con el mismo conjunto de presentaciones que cuando falló. ¿Termina? ¿Aparece
   el ERROR FATAL? ¿Los PDF/impresiones tienen los perfiles?
5. Repetir los pasos 3 y 4 **tres veces** (el fallo puede ser intermitente). Si en alguna aparece el ERROR FATAL,
   anotar la hora exacta y volver al bloque 3 para capturar el evento nuevo.
6. Si no falló ninguna vez: escribir `BACKGROUNDPLOT`, ponerlo en `0`, y repetir el paso 4 una vez más. Anotar.

**4.5 Solo si en 4.4 siguió fallando:** para descartar también la simple carga de la DLL, pide a Andy que cierre
Civil 3D, mueve el bundle al Escritorio y repite 4.4 una vez:

```powershell
Move-Item "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle" "$env:USERPROFILE\Desktop\ArbaMcp.bundle_apartado"
```

Al terminar, con Civil 3D cerrado, devuélvelo a su sitio con `Move-Item` en sentido contrario y comprueba con
`Test-Path` que volvió.

**4.6 Solo si en 4.4 NO falló:** ahora la prueba contraria, con el servidor encendido. Con Civil 3D cerrado:

```powershell
[Environment]::SetEnvironmentVariable("ARBA_MCP", $null, "User")
[Environment]::GetEnvironmentVariable("ARBA_MCP", "User")   # debe imprimir nada
```

Pide a Andy que abra Civil 3D de nuevo, compruebe `ARBAMCP` (ahora "activo") y repita los pasos 3 a 5 de 4.4 **sin
pedirte nada mientras traza**. Anota el resultado. Después, y solo si Andy lo autoriza porque es una copia y todo
está guardado, repite el paso 4 una vez mientras tú llamas cada 2 segundos a la herramienta `listar_alineamientos`
durante todo el trazado (10 a 20 llamadas). Anota si aparece el ERROR FATAL o los perfiles en blanco.

**4.7 Dejar todo como estaba.** Al final del bloque, con Civil 3D cerrado, la variable `ARBA_MCP` debe estar borrada
(comando de 4.6) y el bundle en su carpeta original. Comprueba ambas cosas y pégalo en el informe.

## Bloque 5: rastro de cambios en la instalación de Civil 3D

Andy teme que un agente anterior haya borrado o movido algún archivo del programa. No se puede listar lo borrado, pero
sí lo modificado o creado después de que empezara el trabajo con el MCP (usa como corte el 1 de septiembre de 2026 o
la fecha que te dé Andy):

```powershell
$corte = Get-Date "2026-09-01"
$salida = "$env:USERPROFILE\Desktop\diagnostico_arbamcp\cambios_program_files.txt"
Get-ChildItem "C:\Program Files\Autodesk\AutoCAD 2027" -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.LastWriteTime -gt $corte -or $_.CreationTime -gt $corte } |
  Select-Object FullName, LastWriteTime, CreationTime | Out-File $salida -Encoding utf8
(Get-Content $salida | Measure-Object -Line).Lines
Get-Content $salida | Select-Object -First 60
```

Qué significa: las actualizaciones de Autodesk también escriben ahí, así que una lista larga no es prueba de nada.
Lo sospechoso son archivos sueltos con fecha igual a la de las sesiones del agente. Anota el recuento y los 60
primeros.

Busca también en el historial de PowerShell del usuario órdenes de borrado o movimiento sobre rutas de Autodesk:

```powershell
$hist = "$env:APPDATA\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt"
if (Test-Path $hist) { Select-String -Path $hist -Pattern 'Remove-Item|Move-Item|Rename-Item|rmdir|del ' | Select-String -Pattern 'Autodesk|AutoCAD|Civil|acad|ApplicationPlugins' }
```

Aclara en el informe que ese historial solo recoge comandos escritos en consolas interactivas; lo que un agente ejecutó
por su herramienta puede no estar. Añade, de memoria, qué comandos de borrado o movimiento recuerdas haber ejecutado
tú sobre rutas de Autodesk en sesiones anteriores, o "ninguno que recuerde".

Si Andy quiere una comprobación definitiva de la instalación, la vía es **Panel de control → Programas → Autodesk
Civil 3D 2027 → Modificar → Reparar** (o desde Autodesk Access). Es decisión suya; tú no la ejecutes.

## Bloque 6: informe y veredicto

Escribe `%USERPROFILE%\Desktop\diagnostico_arbamcp\INFORME.md` con esta estructura y cópialo también en
`C:\IA\civil3d-mcp\ArbaMcp\pruebas\informe_diagnostico_<yyyyMMdd>.md` (sin hacer commit):

```
# Informe de diagnóstico ArbaMcp – <fecha>

## Datos de partida
- Hora del último ERROR FATAL y qué hacía Andy:
- DWG que falló:

## 1. Inventario
- Versión de ArbaMcp.dll instalada / fecha:
- Estado del clon (rama, último commit, archivos modificados):
- Otros bundles y archivos de carga automática:

## 2. Actividad del plugin en el momento del error
- historial.log existe: sí/no. Llamadas MCP → en los 5 min anteriores al error: (pegar líneas o "ninguna")
- mcp_log.jsonl / backups en la carpeta del dibujo: (fechas o "no existen")

## 3. Registros de Windows
- Eventos de acad.exe encontrados (fecha, proveedor, módulo con errores):
- Volcados .dmp recientes:

## 4. Prueba de aislamiento
- BACKGROUNDPLOT / LAYOUTREGENCTL / GRAPHICSCONFIG:
- Servidor APAGADO: vista preliminar (perfiles visibles sí/no) · trazado por lotes x3 (error fatal sí/no, perfiles sí/no)
- Bundle apartado (si se hizo):
- Servidor ENCENDIDO sin agente (si se hizo):
- Servidor ENCENDIDO con llamadas durante el trazado (si se hizo):
- Estado final: ARBA_MCP borrada sí/no · bundle en su sitio sí/no

## 5. Instalación
- Archivos de Program Files modificados tras el corte (recuento y sospechosos):
- Órdenes de borrado/movimiento encontradas:

## 6. Veredicto (marca uno)
- [ ] A. PLUGIN DESCARTADO: el fallo se reproduce con el servidor apagado (o con el bundle apartado). Causas a mirar:
      gráficos híbridos (forzar acad.exe a la NVIDIA), GRAPHICSCONFIG sin aceleración, BACKGROUNDPLOT 0, AUDIT y -PURGE
      sobre una copia, estilos de perfil.
- [ ] B. PLUGIN PROBABLE: con el servidor apagado no falla y con el servidor encendido sí (con o sin llamadas del agente).
      Siguiente paso: instalar la versión 1.2.2 (ArbaMcp/ESTABILIDAD.md, sección 4) y repetir 4.6.
- [ ] C. NO CONCLUYENTE: no se reprodujo en ningún escenario. Siguiente paso: instalar 1.2.2, dejar historial.log
      activo y, si vuelve a fallar, guardar la hora y el historial antes de reiniciar.
Evidencia principal en la que te basas (dos o tres frases, citando los datos de arriba):
```

Cuando termines, muestra el informe a Andy y **no instales nada**. Si Andy te pide después instalar 1.2.2, los pasos
están en `C:\IA\civil3d-mcp\ArbaMcp\ESTABILIDAD.md`, sección 4, y solo entonces puedes ejecutar `instalar.ps1`.

# Validación e instalación de ArbaMcp 1.2.2 en Civil 3D 2027

Eres un agente que trabaja en el equipo Windows de Andy (Civil 3D 2027 en español, clon del repo en
`C:\IA\civil3d-mcp`, puente Python en `C:\IA\civil3d-mcp\PuenteMcp\.venv`). Tu tarea: **instalar la versión 1.2.2**
del plugin `ArbaMcp` y **comprobar en el programa real** lo que esa versión no pudo probar donde se escribió (se hizo
sin compilar ni ejecutar Civil 3D). El informe que produzcas es la entrada del siguiente agente, que hará la 1.3.0.

Estado real del repositorio a 2026-09-28: `origin/main` = commit `6c764b2` = versión **1.2.2**. No existe ninguna
versión 1.3.0 ni la rama `feature/proceso-1.3`; no la busques.

Documentos de referencia (léelos antes de empezar, no los edites): `ArbaMcp\ESTABILIDAD.md` (secciones 2, 3, 4 y 6),
`ArbaMcp\LEEME.md` (Instalar, Comprobar), `README.md` (Instalar el puente, Pruebas) y
`ArbaMcp\pruebas\probar_servidor.py` (cabecera del archivo).

## Reglas

- **No marques OK lo que no viste.** Cada OK va acompañado de la salida literal del comando o de la respuesta literal
  de Civil 3D. Si un paso no se hizo, escribe `NO HECHO` y por qué.
- **No deduzcas causas que no comprobaste.** Si algo falla, pega el error tal cual y sigue con el paso siguiente.
  El veredicto sobre la causa lo dará el siguiente agente con tu informe.
- **No muestres el token.** Nunca imprimas el contenido de `%LOCALAPPDATA%\ArbaMcp\token`. `probar_servidor.py`
  imprime la línea `--- Token actual: XXXXXXXX... ---`: en el informe sustitúyela por `--- Token actual: <oculto> ---`.
- **No modifiques código**, ni el repo (nada de `git commit`, `git checkout` de otras ramas, `git reset`), ni el
  bundle a mano, ni la instalación de Civil 3D. Los únicos cambios permitidos: `git checkout main` + `git pull`,
  ejecutar `instalar.ps1`, reiniciar el puente, crear copias del DWG con otro nombre, crear la carpeta y los archivos
  del informe, y (solo en el paso 2, si Andy lo pide) definir y borrar la variable de entorno `ARBA_MCP`.
- **Trabaja siempre sobre copias del dibujo.** Nunca abras la entrega original con el plugin activo.
- Anota la **hora de inicio y fin de cada paso**.
- Mientras corre `probar_servidor.py` **nadie toca Civil 3D** (la prueba lanza `_.LINE` y le manda ESC ella sola).

Guarda todo en `%USERPROFILE%\Desktop\validacion_122\` (créala con `New-Item -ItemType Directory -Force`).

Antes de empezar pregunta a Andy y espera la respuesta:

1. Ruta de un DWG **con al menos un corredor** para las pruebas (se usará una copia). Si la primera región del
   primer corredor no tiene ningún objetivo de superficie, la prueba 8 se detiene ahí; si Andy tiene otro DWG que sí
   lo tenga, mejor ese.
2. Si quiere hacer la prueba de aislamiento del paso 2 (unos 20 a 30 minutos más) o pasar directamente a instalar.

---

## Paso 1. Estado de partida (2 min)

```powershell
$dll = "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\Contents\ArbaMcp.dll"
Test-Path $dll
(Get-Item $dll -ErrorAction SilentlyContinue).VersionInfo.FileVersion
(Get-Item $dll -ErrorAction SilentlyContinue).LastWriteTime
Select-String -Path "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\PackageContents.xml" -Pattern 'AppVersion' -ErrorAction SilentlyContinue
[Environment]::GetEnvironmentVariable("ARBA_MCP", "User")
cd C:\IA\civil3d-mcp
git status --short
git branch --show-current
git fetch origin
git log -1 --oneline origin/main
```

Respuesta esperada: la versión instalada antes de tocar nada (`1.2.0.0`, `1.2.1.0`, `1.2.2.0` o "no existe";
anótala, es el dato más importante), `ARBA_MCP` vacía (si vale `0`, el servidor está desactivado desde el
diagnóstico: anótalo y bórrala en el paso 3), `git status` sin cambios y `origin/main` = `6c764b2`. Si `git status`
muestra archivos modificados, anótalos y **no los arregles**.

## Paso 2. Solo si Andy lo pidió: aislamiento con el servidor apagado (20 a 30 min)

Es la sección 3 de `ESTABILIDAD.md` resumida. Objetivo: saber si los cierres o los trazados en blanco ocurren también
con el servidor del plugin apagado, **antes** de instalar 1.2.2.

1. Civil 3D cerrado (`Get-Process acad -ErrorAction SilentlyContinue` no devuelve nada). `setx ARBA_MCP 0`. Abre una
   PowerShell nueva y comprueba `[Environment]::GetEnvironmentVariable("ARBA_MCP","User")` → `0`.
2. Andy abre Civil 3D desde el menú Inicio y escribe `ARBAMCP`. Esperado: `Servidor MCP: INACTIVO`. Además
   `Test-NetConnection 127.0.0.1 -Port 8765 -InformationLevel Quiet` → `False`. Si dice "activo", repite el punto 1.
3. Copia del dibujo que falló (`$dwg` = su ruta completa): `Copy-Item $dwg ($dwg -replace '\.dwg$','_aislamiento.dwg')`. Andy la abre, va a la
   presentación con los perfiles, hace Vista preliminar y luego Trazar por lotes / Publicar, **tres veces**. Anota
   por cada intento: ¿ERROR FATAL sí/no? ¿perfiles visibles sí/no?
4. Civil 3D cerrado. Borra la variable: `[Environment]::SetEnvironmentVariable("ARBA_MCP", $null, "User")` y
   comprueba que ya no devuelve nada.

Resultado del paso: una de estas tres frases literales en el informe: "falla igual con el servidor apagado",
"no falla con el servidor apagado" o "no se reprodujo en ningún intento". Nada más; no interpretes.

## Paso 3. Instalar 1.2.2 (5 min)

Civil 3D **cerrado** (compruébalo: `Get-Process acad -ErrorAction SilentlyContinue` no devuelve nada; si no, `instalar.ps1` se niega).

```powershell
cd C:\IA\civil3d-mcp
git checkout main
git pull origin main
git log -1 --oneline
powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1
```

Respuesta esperada, literal: `Compilando (Release)...`, la salida de `dotnet build` **sin la palabra `error`**, y al
final `Instalado en: C:\Users\<usuario>\AppData\Roaming\Autodesk\ApplicationPlugins\ArbaMcp.bundle`. Después:

```powershell
$dll = "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\Contents\ArbaMcp.dll"
(Get-Item $dll).VersionInfo.FileVersion      # 1.2.2.0
(Get-Item $dll).LastWriteTime                # hora de hace un momento
Select-String -Path "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\PackageContents.xml" -Pattern 'AppVersion="1.2.2"'
```

Si `dotnet build` falla: pega **todas** las líneas que contengan `error` (código `CS…` o `MSB…`, archivo y línea),
marca el paso como FALLO, **no toques el código** y salta al paso 10 (informe). La versión que había instalada sigue
en su sitio. Si el error dice que no encuentra `acdbmgd.dll`, `AeccDbMgd.dll` o similar, comprueba y anota si
`C:\Program Files\Autodesk\AutoCAD 2027` existe (es la ruta `C3DPath` del `.csproj`).

## Paso 4. Reiniciar el puente (2 min)

```powershell
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'main\.py' -and $_.CommandLine -match 'PuenteMcp' } | Select-Object ProcessId, CommandLine
# Detén cada proceso que aparezca:
# Stop-Process -Id <ProcessId>
cd C:\IA\civil3d-mcp\PuenteMcp
.\.venv\Scripts\pip show mcp | Select-String -Pattern '^Version'
$log = "$env:USERPROFILE\Desktop\validacion_122\puente.log"; $err = "$env:USERPROFILE\Desktop\validacion_122\puente_err.log"
Start-Process -FilePath .\.venv\Scripts\python.exe -ArgumentList "main.py" -WorkingDirectory (Get-Location) -RedirectStandardOutput $log -RedirectStandardError $err
Start-Sleep 6
Get-Content $log, $err -ErrorAction SilentlyContinue | Select-Object -Last 8
Test-NetConnection 127.0.0.1 -Port 8001 -InformationLevel Quiet
```

Respuesta esperada: versión de `mcp` 2.2 o superior, una línea `Uvicorn running on http://127.0.0.1:8001` en uno de
los dos archivos, y `True`. Un `Warning: could not read token` en el arranque es normal si Civil 3D aún no está
abierto. Anota si el puente lo había arrancado el `.vbs` de inicio de sesión (se verá en `CommandLine`).

## Paso 5. Arrancar Civil 3D y comprobar el hilo principal (3 min)

Andy abre Civil 3D desde el menú Inicio y abre cualquier dibujo (vale uno nuevo en blanco). En la línea de comandos
escribe `ARBAMCP` y te dicta las líneas que aparecen. Esperado: `Servidor MCP activo en http://127.0.0.1:8765/` y las
10 últimas líneas del historial. Luego, en PowerShell:

```powershell
$h = "$env:LOCALAPPDATA\ArbaMcp\historial.log"
$hoy = Get-Date -Format 'yyyy-MM-dd'
Select-String -Path $h -Pattern "^$hoy .*(Servidor MCP escuchando|HiloPrincipal listo|HiloPrincipal|No se pudo iniciar)" | Select-Object -Last 6
(Get-Item "$env:LOCALAPPDATA\ArbaMcp\token").LastWriteTime
```

Respuesta esperada, literal (cambian fecha y hora):

```
2026-09-28 10:12:33  Servidor MCP escuchando en http://127.0.0.1:8765/
2026-09-28 10:12:34  HiloPrincipal listo: despachador sí, ventana principal sí
```

Criterio: `despachador sí, ventana principal sí` → OK. `ventana principal todavía no` → OK con nota (pégala).
`despachador no`, `HiloPrincipal.Iniciar se llamó fuera del hilo principal` o ausencia de la línea → FALLO (pega las
últimas 30 líneas del historial). La fecha del archivo `token` debe ser la de este arranque.

## Paso 6. Servidor HTTP a mano (1 min)

```powershell
$t = (Get-Content "$env:LOCALAPPDATA\ArbaMcp\token" -Raw).Trim()      # no imprimas $t
curl.exe -s -i http://127.0.0.1:8765/ping | Select-Object -First 1
curl.exe -s -H "X-Arba-Token: $t" http://127.0.0.1:8765/ping
curl.exe -s -X POST http://127.0.0.1:8765/execute -H "X-Arba-Token: $t" -H "Content-Type: application/json" -d "{\"tool\":\"ping\"}"
```

Respuesta esperada, en este orden: `HTTP/1.1 401 Unauthorized`; `{"ok":true,"servidor":"ArbaMcp","puerto":8765}`;
y un JSON con `"ok":true`, `"tool":"ping"`, un `"ms"` y dentro de `result` `"plugin":"ArbaMcp"` y
`"version":"1.2.2.0"`. Si `version` no es `1.2.2.0`, Civil 3D cargó otra DLL: anota de dónde
(`Get-ChildItem -Recurse "$env:APPDATA\Autodesk\ApplicationPlugins" -Filter ArbaMcp.dll`, y lo mismo en
`$env:ProgramData\Autodesk\ApplicationPlugins`).

## Paso 7. Pruebas de humo sin dibujo de corredor (3 min)

Con un dibujo cualquiera abierto y Civil 3D **sin ningún comando activo ni cuadro de diálogo**:

```powershell
cd C:\IA\civil3d-mcp
.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py 2>&1 | Tee-Object "$env:USERPROFILE\Desktop\validacion_122\probar_servidor.txt"
```

Respuesta esperada: estas 13 líneas con `[OK ]` y al final `=== Resultado: TODO OK ===` (la prueba tarda unos 60 s
porque deja `_.LINE` esperando 12 s y luego le manda ESC):

```
sin token responde 401
con token responde 200
con Origin responde 403
POST sin JSON responde 415
ejecutar_comando REGEN terminado
comando inexistente: no se inició
_.LINE: timeout con ESC
ping responde con comando activo (x.xx s)
leer_variable CMDACTIVE > 0 durante el comando
listar_alineamientos espera y se descarta al agotar el tiempo (x.x s)
_.LINE termina con 'timeout con ESC'
listar_alineamientos vuelve a responder tras el ESC (x.xx s)
el historial anota la espera de listar_alineamientos (linea MCP espera)
```

Pega en el informe cada línea `[FAL]` con su `->` completo. Caso especial: si falla `ping responde con comando
activo`, el Dispatcher no entrega trabajos mientras hay un comando activo en esta instalación (sección 4 de
`ESTABILIDAD.md`); anótalo como FALLO con la salida literal y **no intentes arreglarlo**. Si al terminar Civil 3D se
queda con `_.LINE` activo, Andy pulsa ESC y lo anotas.

## Paso 8. Pruebas con un dibujo con corredor, sobre una copia (5 min)

```powershell
$dwg = "D:\RUTA\QUE\DIO\ANDY.dwg"                      # la ruta del punto 1 de las preguntas iniciales
$copia = $dwg -replace '\.dwg$', '_validacion122.dwg'
Copy-Item $dwg $copia
cd C:\IA\civil3d-mcp
.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py --solo-dibujo --dwg $copia 2>&1 | Tee-Object "$env:USERPROFILE\Desktop\validacion_122\probar_servidor_dwg.txt"
```

Respuesta esperada: `abrir_dibujo` OK (Civil 3D abre la copia), todas las líneas `lectura …` OK con menos de 5 s,
`el dibujo tiene al menos un corredor`, `el corredor tiene regiones`, `hay un objetivo de superficie en la primera
región`, `asignar_objetivo simular=true responde ok`, `listar_objetivos idéntico tras simular`, `mcp_log.jsonl
registra la simulación`, `asignar_objetivo real responde ok`, `listar_objetivos refleja el cambio`, `se creó un
archivo en backups\`, `restaurar el objetivo original`, `mcp_log.jsonl: una línea por escritura`, `última línea del
log tiene hora/herramienta/args/ok/ms`, `REGEN terminado`, `sin undo: el historial no muestra el comando UNDO`,
`con undo=true: el historial sí muestra UNDO`, y `=== Resultado: TODO OK ===`.

Notas: si se detiene en `hay un objetivo de superficie en la primera región` es una limitación del dibujo, no del
plugin; anótalo como `NO HECHO (dibujo sin objetivo de superficie)` y, si hay otro DWG, repite con él. Una `lectura`
que tarde más de 5 s aparece como FAL: pega el tiempo. Después de la prueba comprueba y pega:

```powershell
Get-ChildItem (Join-Path (Split-Path $copia) 'backups') | Sort-Object LastWriteTime | Select-Object -Last 3 Name, Length, LastWriteTime
Get-Content (Join-Path (Split-Path $copia) 'mcp_log.jsonl') -Tail 3
```

## Paso 9. Por el puente y menú Deshacer (3 min)

1. Si tú mismo eres el cliente MCP de Andy: vuelve a conectar el servidor `civil3d` (para releer la lista de
   herramientas) y llama a la herramienta `ping` sin argumentos y luego a `listar_alineamientos`. Pega las dos
   respuestas literales; la de `ping` debe llevar `"version": "1.2.2.0"`. Si no puedes llamar herramientas MCP,
   escribe `NO HECHO (sin cliente MCP)`.
2. Pide a Andy que, en la copia abierta en el paso 8, despliegue la flecha del botón **Deshacer** de la barra de
   acceso rápido y te dicte **literalmente las 6 últimas entradas** de la lista. Anótalas tal cual. No es OK/FALLO:
   es el dato que necesita el siguiente agente para decidir si cada escritura queda como una sola entrada de deshacer.
   Andy no debe ejecutar Deshacer.

## Paso 10. Informe

Escribe `%USERPROFILE%\Desktop\validacion_122\INFORME.md` y cópialo a
`C:\IA\civil3d-mcp\ArbaMcp\pruebas\informe_validacion_122_<yyyyMMdd>.md` (**sin commit**). Estructura:

```
# Validación ArbaMcp 1.2.2 – <fecha>

## Resumen
| Paso | Resultado (OK / FALLO / NO HECHO) | Inicio–fin | Nota de una línea |
|---|---|---|---|
| 1 Estado de partida | | | versión instalada antes: … |
| 2 Aislamiento (opcional) | | | frase literal del paso |
| 3 Instalar 1.2.2 | | | |
| 4 Puente | | | |
| 5 HiloPrincipal listo | | | |
| 6 HTTP a mano | | | |
| 7 probar_servidor.py | | | n OK / n FAL |
| 8 probar_servidor.py --dwg | | | n OK / n FAL |
| 9 Puente y Deshacer | | | |

## Respuestas literales por paso
(La salida completa de cada comando, con el token sustituido por <oculto>. Para los pasos 7 y 8, el archivo .txt entero.)

## Errores de compilación (paso 3)
(literal, o "ninguno")

## Miembros de la API que fallaron
(Cada error que nombre un tipo o miembro .NET: textos como "no tiene el miembro", "MissingMethodException",
"no se encontró", "Api.Leer", "Api.Invocar", "TargetInvocationException". Formato: herramienta → mensaje literal.
O "ninguno".)

## Menú Deshacer (paso 9.2)
(las 6 entradas dictadas por Andy, o "no se hizo")

## Estado final
- Versión instalada ahora (FileVersion):
- ARBA_MCP (usuario): vacía sí/no
- Puente en marcha en 8001: sí/no
- Copias creadas (rutas):
- Archivos que git status muestra modificados en el clon:
```

Al terminar, muestra el informe a Andy. No instales nada más, no hagas commit y no toques el código.

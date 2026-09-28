# Validación ArbaMcp 1.2.2 – 2026-09-28

Informe del agente local (Antigravity) al ejecutar `herramientas-dev/VALIDACION_122.md` en el equipo de Andy
(Civil 3D 2027 en español, DWG de prueba `PRUEBA_AISLAMIENTO.dwg`, copia `_validacion122.dwg`). Se guarda tal cual
lo entregó; el token aparece como `<oculto>`. Es la entrada de `miembros_por_verificar_civil3d.md`.

## Resumen

| Paso | Resultado (OK / FALLO / NO HECHO) | Inicio–fin | Nota de una línea |
|---|---|---|---|
| 1 Estado de partida | **OK** | 23:48:32 – 23:48:34 | Versión instalada antes: `1.2.2.0` (commit `6c764b2`, `ARBA_MCP` vacía) |
| 2 Aislamiento (opcional) | **NO HECHO** | — | Andy decidió pasar directamente a instalar (Paso 3) |
| 3 Instalar 1.2.2 | **OK** | 23:48:57 – 23:49:12 | Compilación Release sin errores ni advertencias; `FileVersion 1.2.2.0` instalada |
| 4 Puente | **OK** | 23:49:20 – 23:51:10 | Uvicorn en http://127.0.0.1:8001, `mcp 2.2.0`, puerto 8001 responde `True` |
| 5 HiloPrincipal listo | **OK** | 23:51:20 – 23:58:32 | `HiloPrincipal listo: despachador sí, ventana principal sí` a las 23:56:36 |
| 6 HTTP a mano | **OK** | 23:58:45 – 23:59:16 | 401 Unauthorized sin token, 200 con token, `version: "1.2.2.0"` |
| 7 probar_servidor.py (1ª ejec., ocupado) | **FALLO** | 23:59:26 – 00:01:24 | 8 OK / 5 FAL (ejecutada con Civil 3D ocupado: cuadro de diálogo abierto) |
| 7 probar_servidor.py (2ª ejec., libre) | **OK** | 00:19:09 – 00:19:36 | 13 OK / 0 FAL (`=== Resultado: TODO OK ===`) |
| 8 probar_servidor.py --dwg | **OK** | 00:08:31 – 00:09:21 | 31 OK / 0 FAL (`=== Resultado: TODO OK ===`), backup y log creados |
| 9 Puente y Deshacer | **OK** | 00:09:51 – 00:24:09 | MCP tool `ping` (1.2.2.0) y `listar_alineamientos` (68 alineamientos) OK; 6 entradas de Deshacer registradas |

## Respuestas literales por paso

### Paso 1. Estado de partida
```
Test-Path: True
FileVersion: 1.2.2.0
LastWriteTime: 09/27/2026 11:59:22
...\ArbaMcp.bundle\PackageContents.xml:4: AppVersion="1.2.2"
ARBA_MCP User: ''
?? ArbaAssemblyReport/
?? ArbaMcp/pruebas/errores_compilacion_1.2.2.txt
?? ArbaMcp/pruebas/informe_diagnostico_20260927.md
?? ArbaMcp/pruebas/resultado_pruebas_1.2.2.txt
?? ArbaMcp/pruebas/resultado_pruebas_1.2.2_dwg.txt
main
 * [new branch]      feature/proceso-1.3 -> origin/feature/proceso-1.3
6c764b2 listar_lineas_muestreo: no llamar a GetSectionSources con el grupo abierto para lectura
```

### Paso 2. Aislamiento con el servidor apagado
`NO HECHO`: Andy eligió pasar directamente a la instalación de la versión 1.2.2 (Paso 3).

### Paso 3. Instalar 1.2.2
```
Already on 'main'
Already up to date.
6c764b2 listar_lineas_muestreo: no llamar a GetSectionSources con el grupo abierto para lectura
Compilando (Release)...
  ArbaMcp -> C:\IA\civil3d-mcp\ArbaMcp\bin\Release\ArbaMcp.dll
Compilación correcta.
    0 Advertencia(s)
    0 Errores
Tiempo transcurrido 00:00:04.57
Instalado en: C:\Users\...\AppData\Roaming\Autodesk\ApplicationPlugins\ArbaMcp.bundle
```
Comprobación del bundle instalado: `FileVersion: 1.2.2.0`, `LastWriteTime: 09/27/2026 23:49:04`, `AppVersion="1.2.2"`.

### Paso 4. Reiniciar el puente
Procesos previos: ninguno en ejecución. `mcp` `Version: 2.2.0`.
```
INFO:     Waiting for application startup.
StreamableHTTP session manager started
INFO:     Application startup complete.
INFO:     Uvicorn running on http://127.0.0.1:8001 (Press CTRL+C to quit)
True
```
Nota: el primer arranque con `Start-Process` no se mantuvo escuchando en 8001 (dos comprobaciones); el puente se
relanzó como tarea en segundo plano con la salida redirigida y entonces sí respondió.

### Paso 5. Arrancar Civil 3D y comprobar el hilo principal
Cuadro de diálogo en Civil 3D al ejecutar `ARBAMCP` a las 23:57:27: "Conectado. El servidor local para la IA está
activo en http://127.0.0.1:8765/ Ya puedes usar el agente con Civil 3D (el puente MCP debe estar en marcha)."
```
23:56:44  Comando termina: COMMANDLINE
23:56:44  Comando inicia: LOGINITIALWORKSPACEESW
23:56:44  Comando termina: LOGINITIALWORKSPACEESW
23:57:27  Comando inicia: ARBAMCP
```
```
historial.log:212:2026-09-27 23:56:36  HiloPrincipal listo: despachador sí, ventana principal sí
historial.log:213:2026-09-27 23:56:36  Servidor MCP escuchando en http://127.0.0.1:8765/
token LastWriteTime: domingo, 27 de setiembre de 2026 23:56:36
```

### Paso 6. Servidor HTTP a mano
(Los primeros intentos de POST fallaron por el escapado de comillas de PowerShell al invocar `curl.exe`, no por el
servidor.)
```
HTTP/1.1 401 Unauthorized
{"ok":true,"servidor":"ArbaMcp","puerto":8765}
{"ok":true,"tool":"ping","ms":1,"result":{"plugin":"ArbaMcp","version":"1.2.2.0","puerto":8765,"dibujo":"D:\\CURSO CIVIL 3D\\Tarea 3\\PRUEBA_AISLAMIENTO.dwg","hay_dibujo":true,"hora":"2026-09-27 23:59:16"}}
```

### Paso 7. Pruebas de humo sin dibujo de corredor

#### Primera ejecución (con Civil 3D ocupado: el cuadro de diálogo de ARBAMCP seguía abierto)
```
  [OK ] sin token responde 401
  [OK ] con token responde 200
  [OK ] con Origin responde 403
  [OK ] POST sin JSON responde 415
  [FAL] ejecutar_comando REGEN terminado  -> no se envió: Civil 3D siguió ocupado (comando activo o cuadro de diálogo) durante 60 s
  [FAL] comando inexistente: no se inició  -> no se envió: Civil 3D siguió ocupado (comando activo o cuadro de diálogo) durante 5 s
  [FAL] _.LINE: timeout con ESC  -> no se envió: Civil 3D siguió ocupado (comando activo o cuadro de diálogo) durante 5 s
  [OK ] ping responde con comando activo (0.01 s)  -> {'plugin': 'ArbaMcp', 'version': '1.2.2.0', ...}
  [OK ] leer_variable CMDACTIVE > 0 durante el comando  -> {'variable': 'CMDACTIVE', 'valor': '1', 'tipo': 'Int16'}
  [OK ] listar_alineamientos espera y se descarta al agotar el tiempo (8.0 s)  -> Tiempo agotado (3 s): la acción seguía esperando ... y se ha descartado: no se ejecutó.
  [FAL] _.LINE termina con 'timeout con ESC'  -> (True, 'no se envió: Civil 3D siguió ocupado (comando activo o cuadro de diálogo) durante 12 s', 12.02)
  [FAL] listar_alineamientos vuelve a responder tras el ESC (35.02 s)  -> Tiempo agotado (30 s): ...
  [OK ] el historial anota la espera de listar_alineamientos (linea MCP espera)
=== Resultado: 5 fallo(s) ===
```
Líneas de `historial.log` durante esa ejecución:
```
23:59:27  MCP ⏳ ejecutar_comando espera: hay un cuadro de diálogo abierto en Civil 3D
00:00:27  MCP ⏳ ejecutar_comando espera: hay un cuadro de diálogo abierto en Civil 3D
00:00:32  MCP ⏳ ejecutar_comando espera: hay un cuadro de diálogo abierto en Civil 3D
00:00:37  MCP ⏳ ejecutar_comando espera: hay un cuadro de diálogo abierto en Civil 3D
00:00:48  MCP ← listar_alineamientos TIEMPO AGOTADO (3 s): la acción seguía esperando ... y se ha descartado: no se ejecutó.
00:00:49  MCP ⏳ listar_alineamientos espera: hay un cuadro de diálogo abierto en Civil 3D
00:01:24  MCP ← listar_alineamientos TIEMPO AGOTADO (30 s): ...
```
Al terminar el paso 7 CMDACTIVE valía 1; Andy pulsó ESC a mano (cerró el cuadro de diálogo) y volvió a 0.

#### Segunda ejecución (Civil 3D libre)
```
  [OK ] sin token responde 401
  [OK ] con token responde 200
  [OK ] con Origin responde 403
  [OK ] POST sin JSON responde 415
  [OK ] ejecutar_comando REGEN terminado  -> terminado
  [OK ] comando inexistente: no se inició  -> el comando no se inició en 5 s (¿nombre incorrecto o Civil 3D ocupado?)
  [OK ] _.LINE: timeout con ESC  -> timeout con ESC
  [OK ] ping responde con comando activo (0.02 s)  -> {'plugin': 'ArbaMcp', 'version': '1.2.2.0', ...}
  [OK ] leer_variable CMDACTIVE > 0 durante el comando  -> {'variable': 'CMDACTIVE', 'valor': '1', 'tipo': 'Int16'}
  [OK ] listar_alineamientos espera y se descarta al agotar el tiempo (8.0 s)  -> Tiempo agotado (3 s): ... se ha descartado: no se ejecutó.
  [OK ] _.LINE termina con 'timeout con ESC'  -> (True, 'timeout con ESC', 12.01)
  [OK ] listar_alineamientos vuelve a responder tras el ESC (0.07 s)  -> [{'nombre': 'Analisis VE (2)', 'inicio': 0, 'fin': 4272.0709, ...}, ...]
  [OK ] el historial anota la espera de listar_alineamientos (linea MCP espera)
=== Resultado: TODO OK ===
```

### Paso 8. Pruebas con un dibujo con corredor, sobre una copia
```
=== Dibujo de prueba: D:\CURSO CIVIL 3D\Tarea 3\PRUEBA_AISLAMIENTO_validacion122.dwg ===
  [OK ] abrir_dibujo  -> {'abierto': '...PRUEBA_AISLAMIENTO_validacion122.dwg', 'ya_estaba_abierto': False}
  [OK ] lectura ping (0.41 s)
  [OK ] lectura listar_alineamientos (1.34 s)
  [OK ] lectura listar_superficies (1.46 s)
  [OK ] lectura listar_corredores (0.11 s)
  [OK ] lectura listar_ensamblajes (0.15 s)
  [OK ] lectura listar_intersecciones (0.09 s)
  [OK ] lectura listar_lineas_muestreo (0.07 s)
  [OK ] lectura leer_historial (0.02 s)
  [OK ] lectura leer_log (0.04 s)
  [OK ] lectura leer_variable (0.02 s)
  [OK ] el dibujo tiene al menos un corredor
  [OK ] lectura estado_corredor (0.07 s)
  [OK ] lectura listar_regiones (0.06 s)
  [OK ] el corredor tiene regiones
  [OK ] lectura listar_objetivos (0.10 s)
  [OK ] lectura pk_a_punto (0.04 s)
  [OK ] lectura punto_a_pk (0.08 s)
  [OK ] lectura cota_superficie (0.07 s)
  [OK ] lectura listar_perfiles (0.06 s)
  [OK ] lectura listar_pvis (0.08 s)
  [OK ] lectura interseccion_ejes (0.06 s)
  [OK ] hay un objetivo de superficie en la primera región
  [OK ] asignar_objetivo simular=true responde ok  -> {'simulado': True, 'accion': "Asignar 'Interseccion 3' como objetivo de superficie del subensamblaje 'Talud_derecha' en la región 'RG - Acuerdo solo exterior - (99)'", 'cambios': ['objetivos'], 'antes': {'objetivos': ['superficie:Topografia']}, ...}
  [OK ] listar_objetivos idéntico tras simular
  [OK ] mcp_log.jsonl registra la simulación  -> 1 -> 2
  [OK ] asignar_objetivo real responde ok  -> {'simulado': False, ..., 'cambios': ['objetivos'], 'antes': {'objetivos': ['superficie:Topografia']}, ...}
  [OK ] listar_objetivos refleja el cambio  -> ['Interseccion 3'] (esperado ['Interseccion 3'])
  [OK ] se creó un archivo en backups\  -> PRUEBA_AISLAMIENTO_validacion122_20260928_000901_asignar_objetivo.dwg
  [OK ] restaurar el objetivo original  -> {'simulado': False, ..., 'antes': {'objetivos': ['superficie:Interseccion 3']}, ...}
  [OK ] mcp_log.jsonl: una línea por escritura  -> 2 -> 4 (esperado +2)
  [OK ] última línea del log tiene hora/herramienta/args/ok/ms
  [OK ] REGEN terminado  -> terminado
  [OK ] sin undo: el historial no muestra el comando UNDO
  [OK ] con undo=true: el historial sí muestra UNDO
=== Resultado: TODO OK ===
```
`backups\`: `PRUEBA_AISLAMIENTO_validacion122_20260928_000901_asignar_objetivo.dwg`, 70 972 777 bytes, 28/09/2026 00:09:05.
`mcp_log.jsonl` (últimas 3): la simulación (`ms: 12`), la escritura real (`ms: 4252`, incluye el `SaveAs` de la copia
de 70 MB) y la restauración en el mismo minuto, sin copia (`ms: 6`). Argumentos: corredor `Interseccion 3`, línea base
`BL - Interseccion 3 (1) - SO - Quadrant - (80)`, región `RG - Acuerdo solo exterior - (99)`, subensamblaje
`Talud_derecha`, grupo `Derecha`, parametro `TargetDTM`.

### Paso 9. Por el puente y menú Deshacer
`ping` por MCP: `{"plugin": "ArbaMcp", "version": "1.2.2.0", "puerto": 8765, "dibujo": "...PRUEBA_AISLAMIENTO_validacion122.dwg", "hay_dibujo": true, "hora": "2026-09-28 00:10:01"}`.
`listar_alineamientos` por MCP: 68 alineamientos con sus perfiles y longitudes.

Menú Deshacer (captura de Andy tras la repetición del paso 7, de arriba abajo):
```
1) Executefunction
2) Linea
3) Linea
4) Regen
5) Executefunction
6) Regen
```
Después siguen: Regen y una lista larga de "Executefunction" (más de 15 seguidas), una por cada llamada de lectura o
escritura del paso 8. Al pie: "Deshacer 1 comando".

## Errores de compilación (paso 3)
ninguno

## Miembros de la API que fallaron
ninguno

## Estado final
- Versión instalada ahora (FileVersion): `1.2.2.0`
- ARBA_MCP (usuario): vacía
- Puente en marcha en 8001: sí (PID 41412 / uvicorn)
- Copias creadas: `D:\CURSO CIVIL 3D\Tarea 3\PRUEBA_AISLAMIENTO_validacion122.dwg` y su `backups\..._000901_asignar_objetivo.dwg`
- Archivos que git status muestra modificados en el clon: ninguno (solo archivos no rastreados `??`)

## Lectura del informe (agente de la 1.3.0)

- La 1.2.2 queda **validada**: el Dispatcher entrega trabajos con un comando activo (`ping` y `leer_variable` en
  0,02 s con `CMDACTIVE=1`), los ESC llegan por la cola Inmediato y cancelan `_.LINE`, los trabajos descartados no se
  ejecutan a destiempo y el contexto de comando ejecuta lecturas y escrituras (paso 8, 31/31).
- La primera ejecución del paso 7 no es un fallo del plugin: el cuadro de diálogo de `ARBAMCP` seguía abierto y el
  plugin hizo lo previsto (esperar y descartar). Lección para los prompts: cerrar el cuadro antes de las pruebas.
- **Deshacer**: cada herramienta en contexto de comando deja una entrada `Executefunction`, incluidas las lecturas.
  Una escritura es una sola entrada (no hacen falta marcas de deshacer); las lecturas pasan a contexto de aplicación
  en 1.3.0 para no dejar entrada.
- La escritura real tardó 4252 ms frente a 6 ms la siguiente: casi todo es el `SaveAs` de la copia de 70 MB en el
  hilo principal. Es lo que la copia de disco en hilo aparte de la 1.3.0 elimina.

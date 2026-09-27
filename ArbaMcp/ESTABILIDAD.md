# Estabilidad: errores fatales y trazados en blanco con el plugin cargado

Este documento explica qué se revisó en el código de `ArbaMcp` tras los fallos de Civil 3D 2027 (ERROR FATAL
`Unhandled Access Violation Reading 0x003f` en la pestaña Salida y perfiles que no salen al trazar hasta regenerar la
ventana), qué se corrigió en la versión **1.2.2** y cómo comprobar en tu equipo si el plugin era la causa.

## 1. Qué hacía mal el plugin (versiones 1.1.x y 1.2.1)

El diseño era correcto en la idea (el servidor HTTP corre en otros hilos y las herramientas se encolan al hilo principal
por el evento `Idle`), pero tenía cuatro fallos reales:

| # | Fallo | Dónde | Riesgo |
|---|---|---|---|
| 1 | Llamadas a la API de AutoCAD **desde el hilo del servidor** (no el principal): `MdiActiveDocument` al empezar `ejecutar_comando`, `SendStringToExecute` para los ESC y el `UNDO _E` al agotarse el tiempo, y `MdiActiveDocument` al registrar el log de `exportar_landxml`/`exportar_imx`. | `Herramientas.cs`, `Herramientas.Superficies.cs` | AutoCAD no admite ninguna llamada fuera de su hilo principal; una de estas en mal momento puede terminar en *Access Violation*. |
| 2 | `Application.MainWindow` se leía desde el hilo del servidor cada vez que llegaba una petición (para despertar al hilo principal). | `HiloPrincipal.cs` | Igual que el anterior, en cada llamada MCP. |
| 3 | Las herramientas corrían en el hilo principal pero en **contexto de aplicación** (evento `Idle`) y **sin comprobar** si Civil 3D estaba en medio de un comando, de un trazado o con un cuadro de diálogo abierto. | `HiloPrincipal.cs` | Una lectura o escritura del dibujo (transacciones, `SaveAs` de las copias de seguridad, `Rebuild` de corredores) podía colarse en mitad de otra operación de Civil 3D. Es el escenario típico de los objetos con gráficos sin actualizar y de los cierres inesperados. |
| 4 | Con un comando esperando entrada, `Idle` no llega: los trabajos encolados no se atendían y por eso los ESC se enviaban desde el hilo equivocado (fallo 1). | `HiloPrincipal.cs` | — |

**Lo que no se puede afirmar** solo leyendo el código: que el plugin sea la causa segura de tus dos síntomas. Los
fallos de arriba lo hacen *posible*, sobre todo si el agente llamó a alguna herramienta mientras trazabas. La sección 3
explica cómo comprobarlo en tu equipo.

## 2. Qué cambia en 1.2.2

| Antes | Ahora |
|---|---|
| Los trabajos llegaban al hilo principal solo por `Idle`. | Llegan por el **Dispatcher de WPF del hilo principal** (el mismo que usa la cinta), que AutoCAD atiende en su bucle de mensajes incluso con un comando activo. `Idle` queda de respaldo. |
| Contexto de aplicación para todo. | Cada herramienta declara su **contexto** (`ContextoEjecucion`): `Documento` (por defecto: contexto de comando del dibujo activo mediante `ExecuteInCommandContextAsync`, como si fuera un comando), `Aplicacion` (`abrir_dibujo`, envío de comandos) o `Inmediato` (`ping`, `leer_historial`, `leer_log`, `leer_variable`, `capturar_pantalla`). |
| Sin comprobar el estado de Civil 3D. | `Documento` y `Aplicacion` **esperan a que Civil 3D esté libre**: `CMDACTIVE = 0` y ventana principal habilitada (sin cuadro de diálogo modal). Se ejecutan de uno en uno. Si el tiempo se agota mientras esperan, se descartan y no se ejecutan a destiempo; el historial anota `MCP ⏳ nombre espera: ...` con el motivo. |
| ESC y `UNDO _E` desde el hilo del servidor. | Van por la cola `Inmediato` al hilo principal. Nada de la API de AutoCAD se llama fuera del hilo principal. |
| `ejecutar_comando` fallaba con "hay un comando activo". | Espera a que Civil 3D esté libre dentro de su `timeout_s`; si no se libera responde `no se envió: Civil 3D siguió ocupado...` sin enviar nada. |
| Las herramientas de escritura rechazaban `CMDACTIVE > 0` siempre. | Igual, salvo que el comando activo sea el propio pseudocomando en el que corre la herramienta. |

Archivos tocados: `HiloPrincipal.cs` (reescrito), `Servidor.cs`, `Herramientas.cs` (`EjecutarComando`, contextos),
`Herramientas.Superficies.cs` (`ExportarPorComando`), `Herramientas.Seguridad.cs`, `Escritura.cs`,
`pruebas/probar_servidor.py` (prueba nueva "Civil 3D ocupado").

## 3. Cómo comprobar en tu equipo si el plugin era la causa

Hazlo antes de instalar 1.2.2, con el mismo dibujo con el que falló (siempre sobre una copia).

1. **Qué versión tienes instalada** (PowerShell):
   ```powershell
   (Get-Item "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\Contents\ArbaMcp.dll").VersionInfo.FileVersion
   ```
   También lo devuelve la herramienta `ping` (`version`).
2. **Mira si el agente estaba llamando al plugin cuando falló**: abre `%LOCALAPPDATA%\ArbaMcp\historial.log`
   (existe desde 1.2.0) y busca líneas `MCP →` con la hora del error. Si no hay ninguna llamada en ese momento, el
   plugin estaba inactivo (solo tenía enganchados el evento `Idle`, que no hacía nada con la cola vacía, y los eventos
   de comando, que solo escriben una línea de texto).
3. **Desactiva el servidor sin desinstalar**: define la variable de entorno `ARBA_MCP=0` y reinicia Civil 3D.
   ```powershell
   setx ARBA_MCP 0
   ```
   Con eso el plugin se carga (verás el botón) pero no arranca el servidor, no engancha `Idle` ni los eventos de
   comando y no toca nada. Para volver a activarlo: `setx ARBA_MCP 1` (o bórrala) y reinicia.
   Si prefieres, mueve la carpeta `%APPDATA%\Autodesk\ApplicationPlugins\ArbaMcp.bundle` al Escritorio.
4. **Reproduce**: abre el dibujo, ve a Salida, traza por lotes / publica y revisa los perfiles.
   - Si **sigue fallando igual** con el servidor desactivado, la causa no es el plugin. Lo habitual en ese caso:
     gráficos híbridos (fuerza `acad.exe` a la NVIDIA en el panel de control de NVIDIA o en Configuración de
     Windows → Pantalla → Gráficos), desactiva la aceleración por hardware (`GRAPHICSCONFIG`), pon
     `BACKGROUNDPLOT` en `0` para la entrega, y pasa `AUDIT` y `-PURGE` a una copia del dibujo. El síntoma de "las líneas
     aparecen al hacer doble clic en la ventana" apunta a la caché gráfica de la presentación (`LAYOUTREGENCTL`), no
     a un plugin.
   - Si **deja de fallar** con el servidor desactivado, instala 1.2.2 (sección 4) y repite la prueba con el
     servidor activo pero **sin** usar el agente mientras trazas; después, usando el agente.

## 4. Instalar 1.2.2

Con Civil 3D cerrado, desde el clon del repo (`C:\IA\civil3d-mcp`):

```powershell
git fetch origin
git checkout claude/zealous-planck-nfxbwq
git pull
powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1
```

Reinicia el puente (`PuenteMcp\main.py`) y Civil 3D. Comprueba con `ARBAMCP` (botón Conexión IA) que el historial muestra
`HiloPrincipal listo: despachador sí, ventana principal sí`. Luego, con un dibujo abierto:

```powershell
.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py
```

La prueba nueva **"Civil 3D ocupado"** lanza `_.LINE` y, mientras el comando espera un punto, comprueba que `ping` y
`leer_variable` responden, que `listar_alineamientos` espera y se descarta al agotar su tiempo sin ejecutarse, y que
`_.LINE` termina con `timeout con ESC`. Si `ping responde con comando activo` falla, el Dispatcher no está entregando
trabajos durante los comandos en tu instalación: anótalo para el siguiente agente, porque entonces los ESC tampoco
llegarían (el resto del arreglo sigue siendo válido).

## 5. Recomendaciones de uso

- No pidas nada al agente mientras Civil 3D traza, publica o tiene un cuadro de diálogo abierto. Ahora el plugin
  espera solo, pero la espera consume el `timeout_s` de la herramienta.
- Trabaja siempre sobre copias del dibujo de la entrega; las herramientas de escritura hacen su propia copia en
  `backups\`, pero el archivo original no debería ser el que el agente toca.
- Si Civil 3D vuelve a cerrarse con el servidor activo, guarda `%LOCALAPPDATA%\ArbaMcp\historial.log` y la hora del
  error antes de reiniciar: es lo que permite saber qué herramienta corría.

## 6. Lo que no se pudo probar aquí

Este cambio se escribió y revisó sin Civil 3D (no se pudo compilar ni ejecutar en el entorno donde se hizo). Lo que hay
que confirmar en tu equipo, en este orden: que compila con `instalar.ps1`; que el historial muestra `HiloPrincipal
listo`; que `probar_servidor.py` pasa, incluida la prueba "Civil 3D ocupado"; y, con `--dwg` sobre una copia, que las
herramientas de lectura y escritura de fase 1 siguen respondiendo. Cualquier error de compilación o de prueba se pasa
al agente de fase 2 tal cual.

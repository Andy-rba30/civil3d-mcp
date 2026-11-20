# CLAUDE.md: reglas permanentes de este repositorio

Este archivo lo lee Claude Code al empezar cada sesión en `civil3d-mcp`. Las reglas valen para todas las tareas;
si una tarea choca con alguna, se avisa antes de hacer nada. Está escrito en español, como el resto del repositorio.

## 1. Contexto

- **Qué es**: un conector entre Civil 3D 2027 y un agente de IA. Dos piezas:
  - `ArbaMcp/`: plugin **.NET 10** (`net10.0-windows`) para Civil 3D 2027. Abre un servidor HTTP local en
    `127.0.0.1:8765`, pone el botón **Conexión IA** en la pestaña ARBA (comando `ARBAMCP`) y publica las
    herramientas en `GET /tools`. La lógica que no toca AutoCAD vive en `ArbaMcp.Nucleo/` y se prueba con
    `ArbaMcp.Pruebas/` (xUnit) sin Civil 3D.
  - `PuenteMcp/`: puente **MCP en Python** (`main.py`). Lee las herramientas del plugin y las expone al agente por
    `streamable-http` en `127.0.0.1:8001/mcp`. Registra las herramientas de forma dinámica: añadir una herramienta en
    C# **no** exige tocar Python. Entrega `INSTRUCCIONES_AGENTE.md` al agente como instrucciones del servidor MCP.
  - `herramientas-dev/`: prompts de validación (`VALIDACION_*.md`), informes (`informes/`), la tabla de miembros de la
    API (`miembros_por_verificar_civil3d.md`), `InspectC3D` (inspección de metadatos de las DLL) y `CompilarSinCivil`
    (compila el plugin contra sustitutos para cazar errores de nombres sin Civil 3D).
- **Versión actual**: 1.22.1 (correcciones de la validación de la ronda E: bombeo normal en el inicio y el final del eje y aviso de carriles con `UseSuperelevation` = None en `aplicar_peralte` y `crear_corredor`, y 40 m de transición por defecto en `crear_bordes_calzada`. Antes, 1.22.0: la ronda E sobre la 1.21.5: `seccion_corredor`, herramienta nueva de lectura con la sección aplicada de un corredor en una progresiva, y `parametros_detalle` en `listar_ensamblajes`; `progresivas` en `listar_peralte` y la comprobación del peralte entre progresivas críticas en `aplicar_peralte`; `verificar_norma` con la figura de peralte para 302.05.06; leer `Bounds` en `ejecutar_codigo`; y el sobreancho con la decisión P5, opción F. Antes, 1.21.5: la ronda D sobre la 1.19.1: en `ejecutar_codigo` sin `solo_lectura`, un cambio sobre un objeto abierto para lectura ya no compila, de la 1.19.2; en `crear_secciones`, el nombre de grupo que ya usa otro eje, con el grupo `<eje> - <nombre>` si Civil 3D lo rechaza, y la superficie del dibujo de cada sección, de la 1.19.3; el peralte del eje con `listar_peralte` y `aplicar_peralte`, que con `velocidad_diseno` calcula el puente con la DG-2018 leyendo las Figuras 302.02 a 302.05, de la 1.20.0; los bordes de calzada con sobreancho con `crear_bordes_calzada` y `listar_bordes_calzada`, que con `velocidad_diseno` calcula el puente con la DG-2018, 302.09, de la 1.21.0; las decisiones del dueño sobre los criterios pendientes de la norma en `verificar_norma` y en la propuesta de radios, sin crear un radio bajo el mínimo sin la confirmación del usuario, de la 1.21.1; el peralte de un eje recién creado, que cuenta como sin peralte y usa la curva vacía de cada arco, de la 1.21.2; `AddWidening` con la distancia sin signo y un error por cada llamada a Civil 3D en `crear_bordes_calzada`, de la 1.21.3; el rango de `AddWidening` adelantado una transición para que el sobreancho pleno vaya de PC a PT, de la 1.21.4; y la opción `mas_cercano`/`exterior`/`interior` de `asignar_objetivo(s)` y `deshacer_objetivos` solo con dos o más objetivos, de la 1.21.5. Antes, la ronda C sobre la 1.17.5: la relectura del estilo en las herramientas que crean objetos y `solo_lectura` en `ejecutar_codigo` y `ejecutar_receta`, de la 1.17.6; `verificar_norma` en el puente, que compara un eje o una rasante con la DG-2018, de la 1.18.0; `velocidad_diseno` en `crear_alineamiento_desde_polilinea`, que propone el radio mínimo de la norma en cada PI, de la 1.19.0; y, con `solo_lectura`, solo métodos de lectura en los objetos del dibujo, de la 1.19.1. Antes, la ronda B sobre la 1.15.1: `ejecutar_codigo`, desactivada por defecto con `ARBA_MCP_CODIGO`, de la 1.16.0; las recetas de código `guardar_receta` y `ejecutar_receta` en el puente, de la 1.17.0; la tabla por progresivas de `calcular_volumenes` solo donde existe la Datum, de la 1.17.1; `crear_superficie_tin` que elige un estilo con las curvas pedidas, de la 1.17.2; la corrección de las referencias de `ejecutar_codigo`, sin los `.exe` ni las DLL nativas de la carpeta de AutoCAD, de la 1.17.3; la carpeta de AutoCAD encontrada aunque el ensamblado se llame `Acdbmgd`, de la 1.17.4; y el recuento de lo que crea un script de `ejecutar_codigo`, con la fuente que lo vio, el diagnóstico de Deshacer y el alias `TransactionManager`, de la 1.17.5), con 60 herramientas del plugin (todas con el parámetro `pedido`; `aprobar_resultado` desde 1.6.0; `crear_superficie_tin` desde 1.7.0; `crear_alineamiento_desde_polilinea` desde 1.8.0; `crear_perfil_terreno_y_vista` desde 1.9.0; `crear_rasante` desde 1.10.0; `crear_corredor` desde 1.11.0; `crear_superficies_corredor` desde 1.12.0; `crear_secciones` desde 1.13.0; `calcular_volumenes` desde 1.14.0; `listar_pis` desde 1.15.0; `ejecutar_codigo` desde 1.16.0; `listar_peralte` y `aplicar_peralte` desde 1.20.0; `crear_bordes_calzada` y `listar_bordes_calzada` desde 1.21.0; `seccion_corredor` desde 1.22.0) más `buscar_api`, `guardar_receta` y `ejecutar_receta` (desde 1.17.0) y `verificar_norma` (desde 1.18.0), 64 en total, y el script `exportar_dataset` en el puente. El plugin no se compila en la integración continua porque referencia
  las DLL de una instalación local de Civil 3D 2027 (`C3DPath` del `.csproj`); lo que depende de ellas se valida en el
  programa real y se anota en `herramientas-dev/miembros_por_verificar_civil3d.md`.
- **Objetivo del proyecto**: automatizar el **flujo vial completo** (superficie, alineamiento, perfil, corredor,
  secciones, movimiento de tierras) desde el agente, con escrituras seguras y verificadas, y **construir una memoria
  técnica propia**: lo que se aprende de la API y de Civil 3D queda escrito en el repositorio (contrato, tabla de
  miembros, informes de validación y, cuando exista, `memoria-firma/`), no en la cabeza de nadie.
- **Documentos que mandan** (leerlos antes de tocar código): `README.md`, `ArbaMcp/LEEME.md`, `ArbaMcp/CONTRATO.md`,
  `ArbaMcp/ESTABILIDAD.md`, `PuenteMcp/INSTRUCCIONES_AGENTE.md`, `herramientas-dev/miembros_por_verificar_civil3d.md`
  y el `VALIDACION_*.md` más reciente.
- **Dónde está cada cosa en el plugin**: `Herramientas.cs` (registro, ayudantes, herramientas generales),
  `Herramientas.Corredores.cs` (corredores, regiones, objetivos, lotes), `Herramientas.Corredores.Crear.cs` (creación de corredores, 1.11.0), `Herramientas.Corredores.Seccion.cs` (sección aplicada de un corredor en una progresiva, `seccion_corredor`, 1.22.0; el núcleo en `ArbaMcp.Nucleo/SeccionCorredor.cs`), `Herramientas.Corredores.Superficies.cs` (superficies Top y Datum de un corredor, 1.12.0), `Herramientas.Secciones.cs` (líneas de muestreo y vistas de sección, 1.13.0), `Herramientas.Volumenes.cs` (superficie de volumen y tabla por progresivas, 1.14.0), `Herramientas.Superficies.cs` (geometría,
  superficies, exportación), `Herramientas.Alineamientos.cs` (creación de alineamientos desde polilínea, 1.8.0), `Herramientas.Alineamientos.Pis.cs` (tabla de PI de un eje, `listar_pis`, 1.15.0), `Herramientas.Alineamientos.Peralte.cs` (peralte del eje, `listar_peralte` y `aplicar_peralte` con valores explícitos, 1.20.0; el núcleo en `ArbaMcp.Nucleo/Peralte.cs` y el cálculo con la DG-2018 en `PuenteMcp/norma_peralte.py`), `Herramientas.Alineamientos.Bordes.cs` (bordes de calzada con ensanchamientos, `crear_bordes_calzada` y `listar_bordes_calzada`, 1.21.0; el núcleo en `ArbaMcp.Nucleo/BordesCalzada.cs` y el sobreancho de la DG-2018 en `PuenteMcp/norma_sobreancho.py`), `Herramientas.Perfiles.cs` (perfil de terreno y vista de perfil, 1.9.0), `Herramientas.Seguridad.cs` (`leer_log`, `guardar_*`), `Herramientas.Codigo.cs` y `Fachada.cs` (`ejecutar_codigo` y la `F` de sus scripts, 1.16.0; la lista blanca, el compilador con Roslyn y el tiempo límite están en `ArbaMcp.Nucleo/Codigo*.cs`; desde 1.19.2, `ArbaMcp.Nucleo/CodigoAperturas.cs` sigue de dónde viene cada objeto para que no compile un cambio sobre uno abierto para lectura), `Escritura.cs` (reglas de
  escritura y, desde 1.7.0, `CrearObjeto` para las herramientas que crean objetos), `Restauraciones.cs` (deshacer propio de objetivos), `HiloPrincipal.cs` (hilo principal y contextos),
  `Servidor.cs` (HTTP), `Cinta.cs` (botón y comando).

## 2. Quién lee esto y cómo hablarle

El dueño del repositorio **no programa**. Por eso:

- Todo se explica en **lenguaje sencillo**: qué se va a cambiar, por qué y qué efecto tendrá en Civil 3D. Los nombres
  de archivos y herramientas se mencionan, pero no se da por sabido ningún término de programación sin una frase que
  lo explique.
- **Cada tarea termina con pasos exactos para verificarla en Civil 3D**, en este formato:
  1. **Qué abrir**: Civil 3D con una copia del dibujo de prueba, PowerShell en la raíz del repo, el agente con el MCP
     conectado, o lo que toque.
  2. **Qué escribir**: el comando literal (`ARBAMCP`, un `curl.exe`, la herramienta MCP con sus `args`, o el comando de
     PowerShell).
  3. **Qué debería ver**: la respuesta literal esperada (campos y valores), lo que debe aparecer en el menú Deshacer,
     en `backups\`, en `mcp_log.jsonl` o en la pantalla de Civil 3D, y qué significaría que no aparezca.
- Si algo no se pudo probar aquí (lo normal: no hay Civil 3D en este entorno), se dice con claridad y se deja el paso
  de verificación para el equipo con Civil 3D.

## 3. Plan antes de código

Antes de modificar cualquier archivo de código (`.cs`, `.py`, `.csproj`, `.toml`, `.yml`, `.ps1`) se presenta un
**plan** y se **espera la aprobación**. El plan dice: qué archivos se tocan y por qué, qué herramienta o
comportamiento cambia, qué documentos se actualizan, qué pruebas lo cubren y cómo se verificará en Civil 3D. Sin
aprobación explícita no se escribe código. Leer, buscar y explicar no necesita aprobación.

## 4. Toda herramienta de escritura nueva sigue `ArbaMcp/Escritura.cs`

Una herramienta "de escritura" es la que cambia el dibujo. Todas pasan por `Escritura.Ejecutar` y, según el objeto,
por `CambiarCorredor`, `CambiarSuperficie` o `LoteCorredor`. No se inventa otro camino. Las reglas son:

1. **Comprobaciones previas**: si hay un comando activo (`CMDACTIVE > 0`), el dibujo es de solo lectura o el mismo
   archivo está abierto dos veces, responde error **sin tocar nada**.
2. **Copia de seguridad antes de escribir**: copia del `.dwg` de disco en un hilo aparte a
   `<carpeta del dwg>\backups\<nombre>_<fecha>_<herramienta>.dwg` (se reutiliza si el archivo no cambió, se conservan
   20); un dibujo sin guardar usa `SaveAs` en `%LOCALAPPDATA%\ArbaMcp\backups`. **Nunca se escribe sin copia
   terminada**; si la copia falla, no se escribe.
3. **`simular`**: todas aceptan el parámetro `simular` (boolean). Con `true` devuelven `antes`/`despues` previstos sin
   tocar el dibujo (y sin copia). El servidor lleva las simulaciones a contexto de aplicación con bloqueo de lectura.
4. **Antes/después verificado**: tras escribir se vuelve a leer el objeto y se devuelven `antes`, `despues` y
   `cambios`. Si el dibujo no refleja el cambio, la herramienta responde error y **no reintenta**.
5. **Una sola entrada de Deshacer**: cada herramienta (también un lote) corre en contexto `Documento` y queda como una
   única entrada `Executefunction` del menú Deshacer. Las lecturas y simulaciones no dejan entrada. Si la herramienta
   cambia objetivos de corredor, guarda lo anterior en `Restauraciones` y devuelve `restaurar`, porque Civil 3D **no
   deshace objetivos** con `_.UNDO` ni Ctrl+Z (validado el 28/09/2026).
6. **Log**: cada llamada añade una línea JSON a `<carpeta del dwg>\mcp_log.jsonl` (hora, herramienta, args, ok, ms,
   error) y otra al historial del plugin.

Además: ninguna herramienta borra ni recrea regiones, líneas base, ensamblajes ni superficies; `ejecutar_comando` es
el último recurso y no se usa para esquivar estas reglas; los parámetros son `string`, `number`, `boolean` o `json`;
los nombres van en español y en `snake_case`; los errores dicen qué herramienta de lectura usar para corregirlos
("No existe la superficie 'X'. Usa listar_superficies.").

## 5. Cada herramienta nueva actualiza la documentación y la versión

Una herramienta no está terminada hasta que **todo esto** está hecho en la misma tarea:

| Qué | Dónde | Qué se añade |
|---|---|---|
| Contrato | `ArbaMcp/CONTRATO.md` | Fila en la tabla que corresponda (parámetros, qué devuelve, miembro de la API usado) y un ejemplo `curl` |
| Guía del plugin | `ArbaMcp/LEEME.md` | El nombre en la lista de herramientas (lectura, escritura o exportación) |
| Instrucciones del agente | `PuenteMcp/INSTRUCCIONES_AGENTE.md` | Solo si cambia una regla o el orden de uso de las herramientas (ver regla 10) |
| Miembros de la API | `herramientas-dev/miembros_por_verificar_civil3d.md` | Cada miembro nuevo de la API de AutoCAD o Civil 3D entra como `por verificar`, en la sección de la versión |
| Sustitutos | `herramientas-dev/CompilarSinCivil/Stubs/*.cs` | Cada miembro nuevo de la API, con firma plausible y cuerpo vacío, para que `CompilarSinCivil` siga compilando |
| Versión | `ArbaMcp/ArbaMcp.csproj`, `ArbaMcp.Nucleo/ArbaMcp.Nucleo.csproj`, `ArbaMcp/Bundle/PackageContents.xml` (`AppVersion` y `Version`), `PuenteMcp/main.py` (`__version__`), `PuenteMcp/pyproject.toml` + `uv.lock` (regenerado con `uv lock`), `README.md` ("Versión actual") | El mismo número en todos; una herramienta nueva sube el segundo dígito (1.4.0 → 1.5.0), una corrección el tercero |
| Prompt de validación | `herramientas-dev/VALIDACION_<versión>.md` | Con la estructura de `VALIDACION_14.md`: requisitos, reglas, pasos numerados con la respuesta esperada literal, pasos decisivos marcados, y el informe que debe guardarse en `herramientas-dev/informes/informe_validacion_<versión>_<AAAAMMDD>.md` |

El puente no cambia para una herramienta nueva, salvo que necesite más de 120 s (se añade a `HERRAMIENTAS_LARGAS` en
`main.py`) o que se retire o renombre una herramienta (va a `HERRAMIENTAS_RETIRADAS` con su sustituta; nunca se borra
un nombre sin dejar esa pista).

## 6. Las pruebas existentes deben seguir pasando

Son las tres que ejecuta la integración continua (`.github/workflows/pruebas.yml`) en cada PR y push a `main`, y se
corren antes de dar una tarea por terminada:

```powershell
dotnet test ArbaMcp.Pruebas                        # núcleo del plugin, xUnit
cd PuenteMcp; uv lock --check; uv run --frozen pytest -q; cd ..   # puente, pytest contra un servidor falso
dotnet build herramientas-dev\CompilarSinCivil     # el plugin contra sustitutos (errores de nombre son fallo)
cd herramientas-dev\Evaluador; uv lock --check; uv run --frozen pytest -q; cd ..\..   # evaluador automático, pytest con un banco de ejemplo
```

Si una prueba deja de pasar por un cambio pedido, se explica el motivo y se propone la corrección; **nunca se borra,
desactiva ni afloja una prueba** para que pase. Lo que solo se puede probar con Civil 3D (todo lo que toca la API
real) se deja escrito en el prompt de validación y en los pasos de verificación de la regla 2.

## 7. Este repositorio es público

Nunca se añade al repositorio, ni en un commit, ni en un informe, ni en un ejemplo:

- **DLL de Autodesk** (`acdbmgd`, `acmgd`, `accoremgd`, `AeccDbMgd`, `AecBaseMgd`, `AdWindows`) ni el contenido del
  bundle compilado. El `.gitignore` ya excluye `*.dll`, `*.pdb`, `bin/` y `obj/`; no se relaja.
- **Dibujos reales** (`.dwg`, copias de `backups\`, `mcp_log.jsonl` de proyectos) ni capturas con datos de obra.
- **Datos de clientes**: nombres de proyectos reales, rutas con nombres de clientes, coordenadas de obras reales. Los
  ejemplos usan nombres genéricos (`Corredor 1`, `Terreno`, `C:\Proyectos\prueba.dwg`).
- **El catálogo de la API**: ningún volcado de `InspectC3D` ni listado masivo de tipos y miembros de las DLL de
  Autodesk. En el repositorio solo va la tabla de los miembros que el plugin usa, en
  `herramientas-dev/miembros_por_verificar_civil3d.md`.
- El **token** del plugin (`%LOCALAPPDATA%\ArbaMcp\token`): en cualquier salida se sustituye por `<oculto>`.

## 8. Una tarea por sesión

Cada sesión resuelve **una** tarea. Si durante la tarea aparece otra cosa que convendría hacer, se anota como
propuesta para otra sesión, no se hace. Al terminar, el último mensaje incluye un **resumen de 5 líneas** de qué
cambió (archivos, comportamiento, versión, pruebas, qué queda pendiente de verificar en Civil 3D).

## 9. Tocar solo los archivos que la tarea necesita

Se modifican únicamente los archivos que la tarea exige (incluidos los de la regla 5 cuando hay herramienta nueva).
Si al trabajar parece necesario cambiar otro archivo, **se dice cuál y por qué y se espera la respuesta** antes de
tocarlo. Nada de reformatear, renombrar o "aprovechar para ordenar" lo que no se pidió.

## 10. `INSTRUCCIONES_AGENTE.md` se mantiene corto

`PuenteMcp/INSTRUCCIONES_AGENTE.md` se entrega al agente en cada conexión, así que solo contiene **reglas y el orden
de uso de las herramientas** (precedencia API > comando, flujo obligatorio para corredores, reglas de dominio,
glosario, mensajes típicos). Las **recetas** (procedimientos paso a paso para un caso concreto, ejemplos largos,
aprendizajes de una validación) viven en `memoria-firma/recetas/` y el agente las encuentra con la herramienta
`buscar_api`; **nunca se copian** en `INSTRUCCIONES_AGENTE.md`. Si una tarea produce una receta, va a esa carpeta.
**Única excepción**, pedida expresamente por el dueño (prompt 2.8, 13/11/2026): la receta «Flujo vial completo»
(sección 2b de `INSTRUCCIONES_AGENTE.md`), porque es el flujo principal. Se mantiene corta: orden, paradas y reglas.

Nota (actualizada el 31/10/2026): `memoria-firma` existe como **repositorio privado aparte** (catálogo de la API en
`catalogo/catalogo_c3d2027.sqlite`, glosario en `catalogo/glosario.yaml` y recetas en `recetas/`); en la PC se
encuentra por la variable de entorno `MEMORIA_FIRMA_DIR`. La herramienta `buscar_api` existe desde la **1.5.0** en el
puente (`PuenteMcp/buscar_api.py`) y busca en esas tres fuentes. Las recetas siguen sin copiarse en
`INSTRUCCIONES_AGENTE.md`.

## 11. Trabajo en GitHub

El código se escribe en **Claude Code web** (la nube), un contenedor Linux que **no** tiene Civil 3D, ni las DLL de
Autodesk, ni acceso a la PC del dueño ni al MCP. En la PC, un **agente instalador** descarga las ramas, compila con
las DLL reales, instala el plugin y prueba las herramientas en Civil 3D usando solo el MCP `civil3d`. El código
**solo se arregla en la nube**; el agente instalador no edita código.

- **a) Nunca se trabaja directo en `main`.** Cada tarea va en una rama propia con nombre
  `tarea/[numero]-[nombre-corto]` (por ejemplo `tarea/0.5-github`), con commits pequeños y mensajes en español,
  `push` de la rama y un **Pull Request en borrador**. Si no se puede abrir el PR desde la sesión, se dice y el
  dueño lo abre con el botón **Create PR**.
- **b) En la nube solo se corre lo que no necesita Civil 3D**: las tres pruebas de la regla 6. Si algo no se puede
  correr aquí, se dice; **no se da por bueno**.
- **c) Nunca se afirma que algo funciona en Civil 3D.** Eso lo confirma el dueño con el informe del agente
  instalador.
- **d) No se inventan miembros de la API de Civil 3D.** Antes de usar un miembro de la API, búscalo con el comando de
  `buscar_api` (`cd PuenteMcp; uv run --frozen python buscar_api.py "consulta"`, con `MEMORIA_FIRMA_DIR` apuntando a
  `memoria-firma`). Se confirman en el catálogo de `memoria-firma` (cuando esté en la sesión) y lo que no se pueda
  confirmar entra en `herramientas-dev/miembros_por_verificar_civil3d.md` como `por verificar`.
- **e) Cada PR lleva dos secciones**: **"Para el agente instalador"** (rama, qué compilar, qué instalar, qué
  variables de entorno poner, qué reiniciar) y **"Para Jose"** (qué abrir en Civil 3D, qué pedirle al agente que
  pruebe con el MCP, qué debería salir y qué archivo `VALIDACION_*.md` usar). La plantilla completa está en
  `herramientas-dev/PLANTILLA_PR.md`.
- **f) Cuando el dueño pegue el informe del agente instalador con errores**, se corrige en la **misma rama**, sin
  cambiar nada más.
- **g) El PR pasa a "listo" solo cuando el dueño diga que la verificación en Civil 3D pasó.** El **Merge** lo hace
  él en GitHub.
- **h) Commits, nombres de rama y descripciones de PR también son públicos** (regla 7): nada de datos de clientes,
  ni rutas de la PC con nombres de proyectos, ni contenido de `memoria-firma`.
- **i) `memoria-firma` es un repositorio privado aparte.** Si la tarea lo necesita y no está en la sesión, se dice
  antes de empezar.

**El ciclo de cada tarea:**

1. El dueño abre una sesión nueva en claude.ai/code con los repositorios que indica el prompt y en modo **Plan**.
2. Pega el prompt, lee el plan y responde "aprobado" o lo corrige.
3. Claude Code web trabaja en la rama `tarea/...`, corre las pruebas y la sube; si no abrió el PR, el dueño pulsa
   **Create PR** (borrador).
4. El agente instalador recibe el **Prompt L.1** con el nombre de la rama.
5. El agente instalador recibe el **Prompt P** y prueba con el MCP siguiendo "Para Jose".
6. Si falla, el dueño pega el informe en la **misma** sesión web: "Falló esto: [informe]. Corrígelo en la misma rama
   sin cambiar nada más", y se vuelve al paso 4.
7. Si pasa, el dueño escribe "Verificación OK", pulsa **Merge pull request** y corre L.1 con `main`.

Los prompts que no tocan Civil 3D (solo documentación, scripts de la nube o el puente) se saltan los pasos 4 y 5.

## Hechos ya validados en Civil 3D 2027 (no hay que volver a descubrirlos)

- Cada escritura en contexto `Documento` es una entrada `Executefunction` del menú Deshacer; las lecturas y las
  simulaciones, en contexto `Aplicacion` con bloqueo de lectura, no dejan ninguna (28/09/2026).
- `_.UNDO 1` revierte las frecuencias, pero **Civil 3D no deshace los objetivos** de corredor, ni siquiera los
  cambiados desde Propiedades de corredor con Ctrl+Z. Por eso existe `deshacer_objetivos` (1.3.3).
- `SetTargets` acepta como objetivo la superficie generada por el propio corredor; el plugin la rechaza, como hace
  Civil 3D en su interfaz.
- La copia de seguridad refleja el **último guardado en disco**, no el estado en memoria; `guardar_copia` sí guarda
  lo que hay en memoria.
- `CorridorSurface.AddPointCode` y `SubassemblyTargetInfo.SubassemblyId` no existen en 2027; `ProfilePVI.Station`
  está obsoleto (se usa `RawStation`). Los detalles y alternativas están en `miembros_por_verificar_civil3d.md`.
- Nada de la API de AutoCAD se llama fuera del hilo principal; el servidor HTTP solo encola (`ESTABILIDAD.md`).
- **`_.UNDO 1` sin zoom quita todo lo que crea el plugin** (17/11/2026, `informe_validacion_deshacer_20261117.md`, con
  la 1.19.1). Hasta entonces se creía que `_.UNDO 1` / Ctrl+Z no quitaba la superficie TIN (informe 1.7.0, paso 10), ni
  las superficies Top y Datum (1.12.0), ni el perfil de terreno (1.14.1), ni la superficie de volumen (1.14.0), y el eje
  no se había probado. Repetido **sin ningún zoom, encuadre ni regeneración** entre la creación y el `_.UNDO 1`, con la
  entrada de arriba del menú Deshacer en `Executefunction` cada vez, **un solo `_.UNDO 1` quitó**: la TIN de
  `crear_superficie_tin`, el eje de `crear_alineamiento_desde_polilinea`, el perfil de terreno **y su vista** de
  `crear_perfil_terreno_y_vista`, las superficies Top y Datum de `crear_superficies_corredor` y la superficie de volumen
  de `calcular_volumenes` (cada una desaparece de su `listar_*`). Antes ya se sabía que quita las rasantes (1.10.0), el
  corredor (1.11.0 y 1.14.1), un contorno (1.14.1) y la línea de `ejecutar_codigo` (1.17.5). Lo que fallaba era el zoom:
  deja su propia entrada (`Zoom`, `Intellizoom`, `Viewcubeaction`) y el `_.UNDO 1` deshacía esa. **No hace falta un
  deshacer propio de superficies**; sí sigue haciendo falta `deshacer_objetivos` para los objetivos de corredor.
- Hasta la 1.10.0, `ejecutar_comando` recortaba los espacios finales del texto (`Trim`), así que un comando interactivo
  que necesita un espacio doble final para cerrar la entrada (por ejemplo `_.PLINE ...`) no terminaba; el agente lo
  resolvió con un archivo `.scr` (31/10/2026, informe 1.8.0, paso 0) y en la validación 1.10.0 la polilínea se dibujó a
  mano (13/11/2026, paso 0). Desde la 1.10.1 el texto se envía con sus espacios finales y sin `"\n"` añadido:
  `_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ` (dos espacios finales) termina con `"result": "terminado"` y
  `CMDACTIVE` en `0` (13/11/2026, informe 1.10.1, paso 3).
- Hasta la 1.10.0, en `crear_superficie_tin`, `crear_alineamiento_desde_polilinea` y `crear_perfil_terreno_y_vista` la
  lista de estilos que acompaña a los errores "No existe el estilo..." salía como "ninguno legible": el nombre de
  `StyleBase` no se podía leer por reflexión (31/10/2026, informe 1.7.0, paso 8; igual en 1.8.0 y 1.9.0). Esas tres
  herramientas y `crear_rasante` leen ahora el nombre con `Autodesk.Civil.DatabaseServices.DBObject.Name` y devuelven la
  lista real: `crear_rasante` desde la 1.10.0 (13/11/2026, informe 1.10.0, paso 4) y las otras tres desde la 1.10.1
  (las siete listas del paso 7 del informe 1.10.1 salieron con nombres, ninguna con "ninguno legible").
- El formato de archivo de puntos `PENZD (comma delimited)` existe en la instalación del dueño y `crear_superficie_tin`
  lo elige solo (31/10/2026, informe 1.7.0, pasos 2 y 3).
- `Alignment.Create` con `PolylineOptions` más una curva libre por PI con `AddFreeCurve` crea el eje con las curvas y
  los radios pedidos (31/10/2026, informe 1.8.0, pasos 4 y 5), y la copia de seguridad se reutiliza entre escrituras
  seguidas si el archivo no cambió.
- `Profile.CreateFromSurface` y `ProfileView.Create` en una misma transacción dejan **una** entrada de Deshacer, y la
  asignación del perfil a `Profile1Id`/`Profile2Id` de las bandas funciona (31/10/2026, informe 1.9.0, pasos 4 y 6).
  Hasta la 1.14.0 el punto de inserción automático usaba la extensión del dibujo entera, así que en dibujos con
  contenido lejano la vista quedaba muy a la derecha (informe 1.9.0, paso 2: x ≈ 6 × 10^5 con un eje en x ≈ 1000..1600).
  Resuelto en la 1.14.1: usa solo el eje y sus vistas de perfil y de sección. Con un punto en x = 600000 en el dibujo,
  la vista de perfil quedó en (1660, 2000) y las secciones en (2694, 2000), a la derecha de la vista de perfil y de las
  43 vistas de sección ya existentes (14/11/2026, informe 1.14.1, pasos 6 y 7).
- `Profile.CreateByLayout` más un `AddPVI` por PVI y una curva por PVI (`AddFreeSymmetricParabolaByPVIAndCurveLength` o
  `AddFreeCircularCurveByPVIAndLength`) en una misma transacción crean la rasante (tipo `FG`) con sus curvas y dejan
  **una** entrada de Deshacer (13/11/2026, informe 1.10.0, pasos 5, 7 y 9). `_.UNDO 1` **sí** quita estos perfiles del
  dibujo (paso 10).
- Las pendientes de la API (`ProfileTangent.Grade`, `ProfilePVI.GradeIn`/`GradeOut`) vienen en **tanto por uno**
  (0,025 = 2,5 %), y `Profile.ElevationMax` da la cota del vértice de la parábola, no la del PVI (13/11/2026, informe
  1.10.0, paso 5: 109,5248 frente a 110). Los textos de los enumerados de curvas y PVI están en
  `miembros_por_verificar_civil3d.md`.
- Hasta la 1.10.0, el error por nombre repetido de `crear_rasante` (repitiendo la creación real) dejaba una entrada
  `EXECUTEFUNCTION` **vacía** en el menú Deshacer, así que después hacía falta un `_.UNDO 1` de más (13/11/2026,
  informe 1.10.0, pasos 8 y 10). Desde la 1.10.1, la **comprobación previa por simulación** ensaya cada escritura real
  con `simular=true` antes de abrir el contexto de comando; si falla, la herramienta responde el error **sin contexto de
  comando, sin copia y sin entrada de Deshacer**. Repetir `Rasante 1` ya no dejó `EXECUTEFUNCTION` ni archivo nuevo en
  `backups\`, y después bastó **un** `_.UNDO 1` para quitar la rasante (13/11/2026, informe 1.10.1, pasos 10 y 11);
  repetir `Corredor 1` se comportó igual (informe 1.11.0, paso 11).
- Hasta la 1.10.0, `ping` devolvía `"version": "1.10.0.0"` (la `FileVersion` con cuatro cifras) en vez de `"1.10.0"`
  (13/11/2026, informe 1.10.0, paso 0). Desde la 1.10.1 da tres cifras: `ping` y `GET /ping` responden
  `"version": "1.11.0"` (13/11/2026, informe 1.10.1, paso 0).
- `CorridorCollection.Add` de 6 argumentos (corredor, línea base y región en una llamada) crea la región con el **rango
  completo de la línea base** (de 0 a 831.3274 en la prueba), y el ajuste posterior de `BaselineRegion.StartStation` y
  `EndStation` en la misma transacción funciona (`Corredor 2` quedó de 100 a 600); no hizo falta
  `BaselineRegionCollection.Add` (13/11/2026, informe 1.11.0, pasos 5 y 9).
- En una región recién creada, los objetivos de superficie ya aparecen con `GetTargets()` **antes de la primera
  reconstrucción** (`camino_objetivos: "antes_de_reconstruir"`), y `Corridor.Rebuild()` funciona dentro de la
  transacción que crea el corredor (una reconstrucción, `esta_desactualizado: false`). Hasta la 1.14.0 el corredor
  quedaba en la capa `0` (13/11/2026, informe 1.11.0, pasos 5 y 9); desde la 1.14.1 queda en la capa de corredores de la
  configuración del dibujo, `C-ROAD-CORR` (`SettingsObjectLayerType.Corridor`; 14/11/2026, informe 1.14.1, paso 5, y
  flujo completo, paso 8).
- Un `_.UNDO 1` después de `crear_corredor` **quita el corredor entero** (desaparece de `listar_corredores`), y entonces
  `deshacer_objetivos` sobre esa creación responde "No existe el corredor 'Corredor 2'. Corredores del dibujo: Corredor
  1." (13/11/2026, informe 1.11.0, paso 10).
- Superficies Top y Datum de un corredor (`crear_superficies_corredor`): la corrección de voladizo tiene los valores
  `None`, `TopLinks` y `BottomLinks`; la Top queda con `TopLinks` y la Datum con `BottomLinks`. El contorno «Extensión
  del corredor» (`AddCorridorExtentsBoundary`) se puede añadir **antes** de `Rebuild()` y queda `OutsideBoundary`, y la
  superficie nueva se construye sola (`IsBuild` = `true`) (14/11/2026, informe 1.12.0, paso 5 y preguntas 1, 3 y 4).
  `guardar_dibujo` guarda el dibujo sin pulsar Ctrl+S (paso 11).
- Los nombres de las líneas de muestreo deben ser **únicos en todo el dibujo**, no solo en su eje: `SampleLine.Create`
  rechaza uno repetido con `Sample line name should not duplicate.` y no crea nada (14/11/2026, informe 1.13.0, paso 9,
  y la evaluación automática de ese día, con un eje sin grupos). Desde la 1.14.2, `crear_secciones` antepone el nombre
  del grupo cuando hay choque y el segundo grupo se crea (informe 1.14.2, paso 6). Las líneas de muestreo y las vistas
  de sección son entidades del espacio modelo (mismo paso, `datos.nombres`).
- En las secciones de una **superficie de corredor**, `Section.SourceName` es `"<corredor> <superficie>"` (`Corredor 1
  Corredor 1 - Datum`) y `Section.SourceId` **no** es la superficie TIN del dibujo (14/11/2026, informe 1.13.0, pregunta
  2, e informe 1.14.0, paso 6), así que ni por nombre ni por id se encuentra. Lo que funciona es
  `SampleLine.GetSectionId(id de la superficie TIN)`: 43 de 43 (informe 1.14.2, pasos 7 y 8). La fuente del grupo,
  `SectionSource.SourceId`, sí abre la TIN (informe 1.13.0, paso 5).
- Geometría de una sección (14/11/2026, informe 1.14.3, preguntas 1 y 2): `SectionPoint.Location` es (desplazamiento,
  cota), con el desplazamiento negativo a la izquierda, comprobado con las cotas de las superficies (diferencia 0).
  `LeftSwathWidth`/`RightSwathWidth` dan el ancho de la línea de muestreo (30/30). En una sección de superficie de
  corredor, `LeftOffset`/`RightOffset` (−3,9 y 3,6) y `MinmumElevation`/`MaximumElevation` **no** son los extremos de
  los puntos, que llegan de −30 a 30; en la del terreno sí coinciden.
- Superficie de volumen (`TinVolumeSurface.GetVolumeProperties`): Civil 3D da el neto como **relleno − corte**
  (`UnadjustedNetVolume`); el plugin da `neto` = corte − relleno (negativo = falta tierra). Los volúmenes ajustados son
  exactamente los sin ajustar × factor (14/11/2026, informe 1.14.0, paso 6 y preguntas 1 y 2).
- La tabla por progresivas de `calcular_volumenes` (áreas medias entre líneas de muestreo) no da lo mismo que la
  superficie de volumen: en la prueba salió un 16,6 % más de corte y un 4,1 % más de relleno (14/11/2026, informe
  1.14.3, paso 4). Es lo observado, no un fallo: **manda la superficie de volumen**. Desde la 1.17.1 las áreas solo
  cuentan donde existen la Datum y el terreno, y la diferencia baja a **0,31 %** en corte y **0,05 %** en relleno
  (informe 1.17.1, paso 4: se recortaron las 43 líneas). En la evaluación con la 1.17.4, sobre otro dibujo, salió 0,21 %
  y 0,06 % (tarea 8). Sigue mandando la superficie.
- `listar_pis`: `AlignmentEntity.SubEntityCount`, `AlignmentEntity[int]` y `AlignmentSubEntityArc.PIPoint` funcionan y
  el PI de Civil 3D coincide con el calculado (`diferencia_pi_civil3d: 0` en las tres curvas); `Alignment.PointLocation`
  en el final exacto de cada entidad da los PC y PT correctos (14/11/2026, informe 1.15.0, pasos 2 y 4).
- Los intervalos de las curvas de nivel de `crear_superficie_tin` viven en el **estilo** de la superficie: con el estilo
  por defecto (`Contours 5m and 25m (Background)`) no se aplican los 1 y 5 m pedidos (`aplicados: false`); la
  herramienta lo avisa y propone un estilo con esos intervalos, como `Contours 1m and 5m (Design)` (14/11/2026, flujo
  completo, paso 4). Desde la 1.17.2, con 1 y 5 m y sin `estilo`, la herramienta elige sola un estilo del dibujo con
  esos intervalos: `Contours 1m and 5m (Background)` (`aplicados: true`), de la familia del estilo por defecto, antes
  que el `(Design)`; con `estilo` lo respeta y solo avisa (informe 1.17.2, pasos 1, 2 y 4). El aviso «No se pudo releer
  el estilo de la superficie creada (Entity.StyleId)» de esa validación ya no sale desde la 1.17.6 (ver «Relectura del
  estilo», más abajo).
- **Flujo vial completo** (14/11/2026, flujo completo, con la 1.14.1): el agente hizo las 8 escrituras de la receta,
  cada una con su simulación previa y su `aprobar_resultado`, en **117 s** frente a unos 130 min a mano (pasos 12 y 15).
  La evaluación automática del mismo día dio **10 de 10** OK con la 1.15.0, pero el agente no hizo tres comprobaciones
  manuales del banco: las curvas cada 1 y 5 m (tarea 1), el cálculo manual del volumen (tarea 8) y el informe de
  alineamiento de Civil 3D (tarea 9). Con la 1.17.4 la evaluación volvió a dar **10 de 10** OK, en **284 s** sumando las
  diez tareas; la tarea 10, el flujo completo en una sola corrida, tardó 140 s frente a 7800 s a mano (14/11/2026,
  `informes/informe_evaluacion_20261114_2252.yaml`).
- En acad.exe, `TRUSTED_PLATFORM_ASSEMBLIES` trae **todos** los `.dll` y `.exe` de la carpeta de AutoCAD, también los
  nativos (`slidelib.exe`, `QtWebEngineProcess.exe`, `w3dtk_wt.dll`...), pero **no** los de las subcarpetas `C3D\`
  (`AeccDbMgd`) y `ACA\` (`AecBaseMgd`). Pasarlos todos a Roslyn dio 282 `CS0009` «La imagen PE no contiene metadatos
  administrados» (14/11/2026, primer intento de la validación 1.16.0, con la 1.17.2, paso 4a). En la nube no sale: el
  TPA del proceso de pruebas solo trae ensamblados .NET.
- El **nombre de ensamblado** de `acdbmgd.dll` es `Acdbmgd`, y el de `acmgd.dll`, `Acmgd` (catálogo de la API).
  Compararlo con `"acdbmgd"` distinguiendo mayúsculas no encuentra la carpeta de AutoCAD: con la 1.17.3 eso dejó fuera
  `AeccDbMgd` y salió `CS0234` «'Civil' no existe en el espacio de nombres 'Autodesk'» (14/11/2026, segundo intento de
  la validación 1.16.0, paso 4a). Desde la 1.17.4 la carpeta sale de `typeof(Database).Assembly`.
- **Roslyn 5.0.0** (`Microsoft.CodeAnalysis.CSharp`, en `Contents\roslyn\`) compila dentro de Civil 3D 2027 con las
  referencias de la 1.17.4: la primera compilación tardó 2541 ms y la repetida salió de la caché en 0 ms (14/11/2026,
  informe 1.16.0, pasos 0, 4a y 4c).
- `ejecutar_codigo` (14/11/2026, informe 1.16.0): el **tiempo límite de 60 s** corta un script en bucle
  (`TimeoutException` a los 60086 ms, «se detuvo en la línea 4») y Civil 3D sigue estable: `ping` responde justo después
  y 30 s más tarde (paso 9); con un error del script, lo mismo (paso 10). La lista blanca rechaza la red, los hilos, los
  procesos y los archivos fuera de la carpeta permitida, sin copia ni Deshacer (pasos 6 y 7). Sin `ARBA_MCP_CODIGO`,
  `ejecutar_codigo` y `ejecutar_receta` responden «ejecutar_codigo está desactivada» sin compilar nada (informe 1.17.0,
  paso 19).
- **Recetas** (14/11/2026, informe 1.17.0): `guardar_receta` solo guarda con la aprobación literal del dueño y no
  sobrescribe (pasos 4, 7 y 8). `buscar_api` devuelve la receta guardada la primera (paso 9). En una **conversación
  nueva**, el agente la encuentra con otras palabras y la reutiliza con `ejecutar_receta`, primero simulada y luego
  real; `veces_usada` pasa de 0 a 1 y a 2 (pasos 11 y 13). Un parámetro que la receta no tiene se rechaza sin llamar al
  plugin (paso 14).
- **Qué crea un script de `ejecutar_codigo` y cómo se deshace** (16/11/2026, informe 1.17.5, con la 1.17.5): la línea
  de `F.Agregar` sale con `despues.objetos_creados: 1`, en simulación y en real (pasos 4 y 6), y **solo la ve la fuente
  `fachada`**. `Transaction.GetAllObjects()` de la transacción del plugin viene vacía (`objetos: 0`: por eso la 1.16.0
  daba 0); la de `F.Tr` trae objetos, pero ninguno marcado como nuevo; los eventos de la base de datos no llegan
  durante el script, sino al confirmar o abortar la transacción (`eventos_al_cerrar`). Sin tocar la vista, **un solo
  `_.UNDO 1` quita la línea** (paso 8). Un `_.ZOOM` deja su propia entrada `Zoom` encima, y entonces el primer
  `_.UNDO 1` deshace el zoom (paso 9): eso pasó en la 1.16.0. Cada **simulación** de `ejecutar_codigo`, también un
  simple conteo, deja su propia entrada `Executefunction` y hace copia de seguridad (las excepciones de esta
  herramienta), así que cuenta al deshacer; desde la 1.17.6, con `solo_lectura=true`, no (ver «`solo_lectura` de
  `ejecutar_codigo`», más abajo). El alias `TransactionManager` compila sin CS0104 (paso 10).
- **Relectura del estilo** (17/11/2026, informes 1176 y 1176_b, con la 1.19.0 y la 1.19.1). Leer el estilo por reflexión
  fallaba en la TIN porque `Surface` vuelve a declarar `StyleId` solo de escritura. Con enlace directo a
  `Civil.Entity.StyleId`/`StyleName` (1.17.6) sale el nombre real y sin avisos: TIN `Contours 1m and 5m (Background)`
  (paso 3) y `Contours 5m and 25m (Background)` cuando se pide (paso 4), eje `Proposed` (paso 13), perfil de terreno
  `Perfil del terreno existente` con vista `Profile View` (paso 14) y rasante `Design Profile` (paso 15).
- **`Curve.Extend` sobre un objeto abierto para lectura cierra Civil 3D** (17/11/2026, `informe_validacion_1176_20261117.md`,
  paso 9, con la 1.19.0). Con `ejecutar_codigo` y `solo_lectura=true`, alargar una `Line` abierta con `F.Abrir<Entity>`
  no dio `eNotOpenForWrite`: `acad.exe` se cerró (el puente respondió «Error de conexión» a los 19759 ms y Autodesk abrió
  su informe de error, CER). No se puede contar con que Civil 3D frene una escritura sobre un objeto abierto para lectura.
  Desde la 1.19.1, con `solo_lectura`, sobre los objetos del dibujo y de Civil 3D solo compilan los métodos de lectura, y
  el mismo script se rechaza al compilar: `Línea 5, columna 3: [bloqueado: solo lectura]
  Autodesk.AutoCAD.DatabaseServices.Curve.Extend: ...`, con Civil 3D abierto (`informe_validacion_1176_20261117_b.md`,
  paso 9). **Sin** `solo_lectura`, un script que olvide `F.Abrir(id, true)` antes de cambiar un objeto puede cerrar Civil
  3D igual: es un límite conocido.
- **`solo_lectura` de `ejecutar_codigo`** (17/11/2026, informe 1176_b, con la 1.19.1). Un conteo con `solo_lectura=true`
  sale con `copia: null`, `datos.deshacer.comando: ""` y `dbmod_antes` igual a `dbmod_despues`, sin entrada nueva en el
  menú Deshacer ni archivo nuevo en `backups\` (paso 7). Al compilar se rechazan `F.Agregar`, `OpenMode.ForWrite`,
  `F.Abrir(id, true)`, `LayoutManager.DeleteLayout` y el `OpenMode` escondido en genéricos y delegados (pasos 8a a 8e), y
  `ejecutar_receta` con una receta que escribe (paso 16, `veces_usada` sin cambio). `Editor.WriteMessage` sí compila y
  escribe en la línea de comandos (paso 9b). Después de los pasos 7, 8, 9 y 9b, **un solo** `_.UNDO 1` quitó la línea del
  paso 6: ninguna llamada de solo lectura dejó entrada (paso 10). Sin `solo_lectura`, `Database.AddDBObject`,
  `XBindXrefs`, `DetachXref` y `(F.Db.Clayer, _) = ...` tampoco compilan (pasos 17a a 17c).
- **`verificar_norma` y la norma dentro del eje** (17/11/2026, informes 118 y 119, con la 1.19.1 y `dg2018.sqlite` de
  `memoria-firma` en el commit `518fa70`). `casos_norma_reales.py` da 69 comprobaciones OK y 0 fallos en la PC. Un eje de
  60 km/h, Tercera Clase, sin pavimentar y con peralte del 8 % con un radio de 80 m sale «No cumple» (125 m, T302.02), y
  una curva vertical convexa de 50 m con A = 7 % sale «No cumple» (77 m, T303.02). Si falta un dato del proyecto, o la
  velocidad es 65 (la norma no interpola), responde sin llamar a Civil 3D: el `mcp_log.jsonl` no suma ninguna línea. Sin
  la base, responde «Falta la base de la norma ...» y la línea de comandos sale con el código 2. `verificar_norma` no deja
  entrada en Deshacer. Con `velocidad_diseno` 60 y esos datos, `crear_alineamiento_desde_polilinea` propone **215 m** en
  cada PI (manda la T302.11B: radio mayor que 210 m para prescindir de la curva de transición, redondeado hacia arriba a
  5 m), crea el eje con una sola entrada de Deshacer y 0 «No cumple»; si la polilínea no deja sitio, pide el mayor radio
  que cabe (80 m) con un aviso; un radio del usuario (150 m) manda y se avisa (informe 119, pasos 4 a 7).
- **Ronda D con la 1.21.2** (20/11/2026, `informe_validacion_ronda_d_20261120.md`). Peralte: 16 puntos críticos
  escritos y `enumerados` completo (`VALIDACION_120`); la API da las pendientes del peralte en tanto por uno (0.06 = 6 %), y
  **un `_.UNDO 1` quita el peralte** (`tiene_peralte: false`, 0 estaciones): no hace falta un `deshacer_peralte` (paso 7). Decisiones de la norma: OK (`VALIDACION_1211`). En
  `crear_secciones`, Civil 3D **rechaza** el nombre de un grupo de muestreo que ya usa otro eje con «Sample line group
  name is duplicate.», y el plugin creó `Eje 2 - Muestreo 100` en la misma transacción, con 215 de 215 secciones
  identificadas (`VALIDACION_1193`). `ejecutar_codigo` sin `solo_lectura` rechaza al compilar un cambio sobre un objeto
  abierto para lectura, y las correcciones `F.Abrir(id, true)` y `UpgradeOpen()` compilaron y funcionaron **sin cerrar
  Civil 3D** (`VALIDACION_1192`).
- **Comprobaciones C1 a C3** (20/11/2026, `COMPROBACIONES_RONDA_D.md`). C1: `_.-LAYER _M <capa>` con dos espacios finales
  **no termina** (se corta a los 20 s) y con `"\n\n"` termina en 125 ms: al pedir el nombre de la capa, `-LAYER` no
  toma el espacio como Enter (al revés que `_.PLINE`, que sí termina con dos espacios desde la 1.10.1); en los guiones
  con `-LAYER` hay que usar `"\n\n"`. C2: confirmada la hipótesis H4: la lista blanca de `ejecutar_codigo` rechaza al
  compilar la lectura de `Bounds`, aunque sea solo una lectura, porque esa propiedad solo la declara
  `GraphicsInterface.Drawable` (sale como «[bloqueado: ventanas y diálogos] Autodesk.AutoCAD.GraphicsInterface.Drawable»; `DrawableType` también) (las que `DBObject` vuelve a declarar, como `Id`, sí compilan). C3: durante la transacción, `IsNewObject` da `false` e
  `IsModified` da `true`.
- **Bordes de calzada** (20/11/2026, `crear_bordes_calzada`, informes 121, 1213 y 1214). `Alignment.CreateOffsetAlignment`
  con −3.6 crea el borde a la **izquierda** (`NominalOffset` −3.6, `Side` `Left`; a la derecha, `Right`).
  `OffsetAlignmentInfo.AddWidening` recibe el desplazamiento **total sin signo**: con −4.2 respondió «The offsetDistance
  value must greater than 0. Nombre del parámetro: offsetDistance» y no creó nada (1.21.2); con 4.2, Civil 3D lo guarda
  con el signo del borde (−4.2). Las regiones son `Norminal` (sic) y `Specific`, y la transición, `Linear`. Civil 3D pone
  la transición de **entrada al principio** del rango que recibe `AddWidening` y la de **salida después del final**: con
  `AddWidening(220, 345.6637)` la entrada quedó de 220 a 260, dentro de la curva, y ahí el borde salió deformado
  (sobreancho medido hasta 1.0518 con 0.6 pedidos); en la recta, bien (1.21.3). Desde la 1.21.4 se pasa
  `AddWidening(180, 345.6637)`: tramo pleno de 220 a 345.6637, transiciones lineales de 40 m fuera (180–220 y
  345.6637–385.6637), sobreancho medido 0.6 en los dos bordes con la rampa 0 → 0.15 → 0.30 → 0.45 → 0.60. Jose leyó
  −3.600 m en Propiedades de alineación; **un `_.UNDO 1` quita los dos bordes**. Jose ve «un poco brusca» la transición
  en planta, que es lo propio de una transición lineal (F47).
- **Sobreancho de la norma a 30 km/h** (20/11/2026, informe 1214, paso 13). Con B2 y 2 carriles, la herramienta da
  **5.2702 m** (R 25) y **4.3802 m** (R 30) sin factor, frente a 5.27 y 4.38 de control, y factores de la T302.20 de 0.86
  y 0.83. Cómo se aplica el factor (P5) sigue **«a confirmar»**: Jose no lo aplicaba porque el XML de criterios de Civil
  3D que usaba no lo tiene.
- **Opción de un objetivo de corredor** (20/11/2026, informes 1214, paso 7d, y 1215).
  `SubassemblyTargetInfo.TargetToOption` = Nearest con un solo objetivo de desplazamiento **falla al escribir** con «The
  count of TargetIds should be greater or equal to 2.» (la simulación no lo detecta). Desde la 1.21.5 la opción solo se
  escribe con dos o más objetivos: asignar con `mas_cercano` da `ok: true` con aviso, `deshacer_objetivos` devuelve los
  dos carriles a vacío y, al reasignar y reconstruir, el corredor queda al día y el Visor de sucesos limpio (0 sucesos).
- **Deshacer en Civil 3D en español**: la entrada `Executefunction` del menú Deshacer se ve como **«Grupo de
  comandos»** (20/11/2026, informe 1214, paso 10).
- **Errores literales** (20/11/2026). El servidor informa la excepción más interna, así que un error envuelto con
  `InnerException` pierde el texto del plugin (desde la 1.21.3, `crear_bordes_calzada` lanza sin `InnerException` y deja
  el original en `leer_historial`). El primer informe corto del agente de la ronda D traía una línea de `mcp_log.jsonl`
  reescrita con otros parámetros; la copiada del archivo era la buena: **pedir siempre la línea literal** con
  `Get-Content -Encoding UTF8` (sin `-Encoding` los acentos salen rotos).

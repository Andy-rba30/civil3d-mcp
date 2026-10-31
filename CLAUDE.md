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
- **Versión actual**: 1.5.0, con 44 herramientas del plugin más `buscar_api` en el puente. El plugin no se compila en la integración continua porque referencia
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
  `Herramientas.Corredores.cs` (corredores, regiones, objetivos, lotes), `Herramientas.Superficies.cs` (geometría,
  superficies, exportación), `Herramientas.Seguridad.cs` (`leer_log`, `guardar_*`), `Escritura.cs` (reglas de
  escritura), `Restauraciones.cs` (deshacer propio de objetivos), `HiloPrincipal.cs` (hilo principal y contextos),
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

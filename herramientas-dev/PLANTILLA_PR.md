# Plantilla de Pull Request

<!-- Copia desde "## Resumen" hasta el final en la descripción del PR y rellena cada sección. Borra los
     comentarios de ayuda (las líneas entre <!-- y -->) si quieres; GitHub no los muestra. Recuerda que el PR es
     público (regla 7 y regla 11.h de CLAUDE.md): sin datos de clientes, sin rutas con nombres de proyectos, sin
     contenido de memoria-firma y con el token sustituido por <oculto>. -->

## Resumen

<!-- Qué cambia y por qué, en dos o tres frases de lenguaje sencillo. Indica la versión antes y después
     (por ejemplo "1.4.0 → 1.5.0"; si no cambia, "sigue en 1.4.0"). -->

## Archivos tocados

<!-- Una frase por archivo: qué se cambió en él. Si hay archivos nuevos, dilo. -->

## Pruebas en la nube

<!-- Las tres pruebas de la regla 6 de CLAUDE.md con su resultado literal (la línea final que imprime cada una).
     Si alguna no se pudo correr, se dice y se explica por qué; nunca se da por buena. -->

| Prueba | Comando | Resultado literal |
|---|---|---|
| Núcleo del plugin (xUnit) | `dotnet test ArbaMcp.Pruebas` | |
| Puente (pytest) | `cd PuenteMcp && uv lock --check && uv run --frozen pytest -q` | |
| Plugin contra sustitutos | `dotnet build herramientas-dev/CompilarSinCivil` | |

## Para el agente instalador

<!-- Lo que el agente de la PC necesita para dejar el plugin instalado. Si la tarea no toca el plugin ni el puente,
     escribe "No hay nada que compilar ni instalar". -->

- **Rama**:
- **Qué compilar**:
- **Qué instalar**:
- **Qué variables de entorno poner**:
- **Qué reiniciar**:

## Para Jose

<!-- Pasos para el dueño, en el formato de la regla 2 de CLAUDE.md: qué abrir en Civil 3D, qué pedirle al agente
     con el MCP (herramienta y args literales), qué debería salir (respuesta esperada, Deshacer, backups\,
     mcp_log.jsonl) y qué archivo herramientas-dev/VALIDACION_*.md usar. Si la tarea no necesita Civil 3D, dilo y
     deja solo los pasos que sí hay que hacer. -->

1. **Qué abrir**:
2. **Qué pedirle al agente con el MCP**:
3. **Qué debería salir**:
4. **Archivo de validación**: `herramientas-dev/VALIDACION_<versión>.md`

## Pendiente de verificar en Civil 3D

<!-- Todo lo que solo se puede comprobar con Civil 3D y las DLL reales (miembros de la API, comportamiento del
     dibujo, Deshacer, objetivos). Si no hay nada, escribe "Nada: esta tarea no toca Civil 3D". -->

## Lista de comprobación

<!-- Marca cada casilla antes de pasar el PR de borrador a listo. Las cinco últimas solo aplican si hay herramienta
     nueva o renombrada (regla 5 de CLAUDE.md). -->

- [ ] Sin DLL de Autodesk (`acdbmgd`, `acmgd`, `accoremgd`, `AeccDbMgd`, `AecBaseMgd`, `AdWindows`) ni bundle compilado.
- [ ] Sin `.dwg`, sin copias de `backups\` y sin `mcp_log.jsonl` de proyectos.
- [ ] Sin datos de clientes ni rutas con nombres de proyectos (los ejemplos usan `Corredor 1`, `Terreno`, `C:\Proyectos\prueba.dwg`).
- [ ] Token del plugin sustituido por `<oculto>` en cualquier salida.
- [ ] Si hay herramienta nueva: `ArbaMcp/CONTRATO.md` al día (fila y ejemplo `curl`).
- [ ] Si hay herramienta nueva: `ArbaMcp/LEEME.md` al día (nombre en la lista).
- [ ] Si hay herramienta nueva: `herramientas-dev/miembros_por_verificar_civil3d.md` al día (miembros `por verificar`).
- [ ] Si hay herramienta nueva: sustitutos en `herramientas-dev/CompilarSinCivil/Stubs/*.cs` al día.
- [ ] Si hay herramienta nueva: versión subida en todos los archivos de la regla 5 y `herramientas-dev/VALIDACION_<versión>.md` creado.

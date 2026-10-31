# Evaluador automático (línea base)

Programa en Python que mide, corrida a corrida, qué sabe hacer el agente con el conector `civil3d-mcp` y cuánto
tarda frente al trabajo manual. No toca Civil 3D ni el MCP: solo lee y escribe archivos de texto. El banco de tareas
y los informes viven en el repositorio privado `memoria-firma` (carpeta `evaluacion/`), nunca aquí.

Tiene dos comandos:

- **`generar`**: lee `evaluacion/tareas.yaml` de memoria-firma y escribe `evaluacion/PROMPT_EVALUACION.md`, el prompt
  que el dueño pega al agente instalador de su PC. El prompt le dice al agente que use solo herramientas del MCP
  `civil3d`, que trabaje sobre una copia del dibujo, que llame primero con `simular=true`, que compruebe cada tarea
  como dice `como_se_comprueba`, que no arregle código y que termine con un informe en un bloque YAML de formato fijo.
- **`comparar`**: valida ese bloque YAML, lo guarda con fecha en `evaluacion/informes/` y lo compara con el informe
  anterior más reciente: qué tareas pasaron de FALLO a OK y de OK a FALLO, cuáles siguen igual, cómo cambió el
  tiempo por tarea y el total, y el tiempo total frente a la suma de `tiempo_manual_min` del banco.

## Requisitos

- Python 3.11 o superior y `uv` (el mismo que usa el puente).
- La variable de entorno `MEMORIA_FIRMA_DIR` apuntando a la carpeta del repositorio `memoria-firma` (por ejemplo
  `C:\Proyectos\memoria-firma`). Si no está definida, hay que pasar las rutas con `--tareas`, `--salida` o
  `--informes`; sin ninguna de las dos cosas el programa se detiene con un error que dice qué falta.

La primera vez, desde esta carpeta:

```powershell
cd C:\Proyectos\civil3d-mcp\herramientas-dev\Evaluador
uv sync
```

## Cómo se corre `generar`

```powershell
uv run --frozen python evaluador.py generar
```

Escribe `MEMORIA_FIRMA_DIR\evaluacion\PROMPT_EVALUACION.md` e imprime cuántas tareas y cuántos minutos manuales
suma el banco. Opciones:

| Opción | Para qué |
|---|---|
| `--tareas <ruta>` | Otro `tareas.yaml` (por defecto `MEMORIA_FIRMA_DIR/evaluacion/tareas.yaml`). |
| `--salida <ruta>` | Otro archivo de salida (por defecto `MEMORIA_FIRMA_DIR/evaluacion/PROMPT_EVALUACION.md`; sin `MEMORIA_FIRMA_DIR` ni `--salida`, el prompt sale por pantalla). |

Cada tarea de `tareas.yaml` debe traer `id`, `pedido`, `archivo_de_partida`, `resultado_esperado`,
`como_se_comprueba` y `tiempo_manual_min`; si falta alguno, `generar` lo dice y no escribe nada. Hay que volver a
correr `generar` cada vez que cambie `tareas.yaml`.

## Cómo se corre `comparar`

El agente instalador termina su respuesta con un bloque YAML. Se guarda ese bloque en un archivo (por ejemplo
`C:\Proyectos\informe_evaluacion.yaml`; también vale guardar la respuesta completa en un `.md`: el programa toma el
primer bloque ```` ```yaml ````) y se corre:

```powershell
uv run --frozen python evaluador.py comparar C:\Proyectos\informe_evaluacion.yaml
```

Qué hace, en orden:

1. **Valida** el formato (ver abajo). Si falta un campo, un `resultado` no es `OK` ni `FALLO`, una tarea `FALLO` no
   trae texto en `fallo` o la fecha está mal escrita, se detiene con un mensaje que dice qué tarea y qué campo, y
   **no guarda nada**.
2. **Guarda** el informe como `evaluacion/informes/informe_evaluacion_<AAAAMMDD_HHMM>.yaml` (la fecha y hora salen del
   campo `fecha` del informe; si ya existía uno igual, añade `_2`, `_3`…). `respuesta_literal` se recorta a 600
   caracteres al guardar.
3. **Compara** con el informe anterior más reciente de esa carpeta (el de nombre más alto, que es el de fecha más
   reciente) e imprime una tabla con, por tarea: resultado antes y ahora, el cambio (`FALLO → OK`, `OK → FALLO`,
   `sigue OK`, `sigue FALLO`, `nueva`), tiempo antes y ahora con su diferencia, el tiempo manual del banco y el texto
   de `fallo`. Debajo: cuántas pasaron de FALLO a OK y de OK a FALLO (con sus ids), cuántas siguen igual, el tiempo
   total antes y ahora, y el total frente a la suma de `tiempo_manual_min` (con el porcentaje). Si no hay informe
   anterior lo dice ("este es el primero, línea base") y solo guarda.

Opciones:

| Opción | Para qué |
|---|---|
| `--informes <carpeta>` | Otra carpeta de informes (por defecto `MEMORIA_FIRMA_DIR/evaluacion/informes`). |
| `--tareas <ruta>` | Otro `tareas.yaml` para el tiempo manual (por defecto el de `MEMORIA_FIRMA_DIR`; si no se encuentra, se avisa y se compara sin el tiempo manual). |

Después de `comparar`, el informe guardado se sube a memoria-firma con `sincronizar_memoria.ps1` desde la carpeta
de ese repositorio.

## Formato del YAML del informe (fijo)

Es lo que el agente instalador devuelve al final del prompt y lo que `comparar` exige. Los campos van en este orden:

```yaml
informe_evaluacion: 1                 # número del formato; siempre 1
fecha: "2026-11-02 10:00"             # día y hora de la corrida, AAAA-MM-DD HH:MM
version_plugin: "1.6.0"               # lo que devuelve ping en "version"
version_puente: "1.6.0"               # la versión del puente que muestra la conexión MCP
dibujo_de_partida: "C:\\Proyectos\\prueba_copia.dwg"   # la COPIA del dibujo con la que se empezó
tareas:                               # una entrada por tarea del banco, con el mismo id y en el mismo orden
  - id: 1
    resultado: OK                     # OK o FALLO (nada más)
    tiempo_s: 20                      # segundos desde que empezó la tarea hasta que terminó de comprobarla
    herramientas: [ping, listar_superficies]   # herramientas del MCP usadas, en orden; "nombre(simular)" si fue con simular=true
    respuesta_literal: '{"ok": true, "superficies": [{"nombre": "Terreno"}]}'   # respuesta decisiva, recortada a 600 caracteres, token como <oculto>
    fallo: ""                         # vacío si OK; si FALLO, qué faltó o qué salió distinto
  - id: 2
    resultado: FALLO
    tiempo_s: 90
    herramientas: [listar_corredores, "cambiar_frecuencias(simular)"]
    respuesta_literal: '{"ok": false, "error": "No existe el corredor"}'
    fallo: "la herramienta no encontró el Corredor 1"
```

Reglas de validación: los seis campos de cabecera son obligatorios; `informe_evaluacion` debe ser `1`; `fecha` debe
poder leerse como `AAAA-MM-DD HH:MM` (se admite solo `AAAA-MM-DD`); `tareas` no puede estar vacía ni repetir `id`;
`resultado` solo admite `OK` o `FALLO`; `tiempo_s` es un número mayor o igual que cero; `herramientas` es una lista de
nombres (también se admite un texto separado por comas); `fallo` no puede estar vacío si `resultado` es `FALLO`. Si
los `id` no coinciden con los del banco, `comparar` avisa pero continúa.

## Pruebas

```powershell
cd herramientas-dev\Evaluador
uv lock --check
uv run --frozen pytest -q
```

Las pruebas usan un banco de ejemplo con datos genéricos (`tests/datos/tareas_ejemplo.yaml`: "Corredor 1",
"Terreno", `C:\Proyectos\prueba.dwg`) y dos informes de ejemplo. Las tareas reales de la firma viven solo en
memoria-firma y nunca se copian a este repositorio. La integración continua corre estas pruebas en el job
`evaluador` de `.github/workflows/pruebas.yml`.

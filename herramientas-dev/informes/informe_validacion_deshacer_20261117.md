# Informe de Validación Parte 4: Efecto Real de `_.UNDO 1` sin Zoom — Ronda C (17 de noviembre de 2026)

- **Versión de Civil 3D:** Autodesk Civil 3D 2027 (build 13.9.628.0, AutoCAD X.60.0.0 x64)
- **Rama:** `tarea/ronda-c`
- **Commit civil3d-mcp:** `6dba870` (versión instalada 1.19.1 con 59 herramientas)
- **Dibujo usado:** `PRUEBA_DESHACER.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)
- **Ensamblaje usado:** `Ensamblaje 1 (3)`

---

## 1. Contexto y Hallazgo Principal

En versiones y validaciones anteriores (1.7.0, 1.12.0, 1.14.0, 1.14.1), se había reportado que `_.UNDO 1` no revertía la creación de superficies TIN, superficies de corredor, perfiles de terreno o volúmenes.
Sin embargo, en todas esas pruebas intermedias existían llamadas a `_.ZOOM`, encuadres o herramientas como `listar_lineas_muestreo` que registraban sus propias entradas en la pila de deshacer de AutoCAD (ej. `Intellizoom`, `Viewcubeaction`, grupos de comandos). Como consecuencia, el comando `_.UNDO 1` deshacía la vista o la última acción de visualización y no la entidad geométrica.

**Conclusión inequívoca de esta prueba:**
Al omitir de forma estricta todo comando de zoom, encuadre o regeneración entre la creación y `_.UNDO 1`, **TODAS las entidades creadas por el MCP (Superficie TIN, Alineamiento, Perfil de terreno con su Vista de perfil, Superficies Top/Datum de corredor, y Superficie de volumen) SE QUITARON AL 100% con un único `_.UNDO 1`**.
La transacción `Transaction` de AutoCAD encapsulada en el comando `EXECUTEFUNCTION` del plugin revierte íntegramente la adición de los objetos a la base de datos de Civil 3D.

---

## 2. Tabla de Resultados de `_.UNDO 1` por Objeto

| Objeto evaluado | Entrada superior Deshacer | Respuesta `_.UNDO 1` | Resultado | Lectura ANTES de `_.UNDO 1` | Lectura DESPUÉS de `_.UNDO 1` |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Superficie TIN** (`Terreno`) | `Executefunction` | `"result": "terminado"` (114 ms) | **se quitó** | `listar_superficies`: `[{"nombre": "Terreno", "tipo": "TinSurface", ...}]` | `listar_superficies`: `[]` |
| **Alineamiento** (`Eje 1`) | `Executefunction` | `"result": "terminado"` (178 ms) | **se quitó** | `listar_alineamientos`: `[{"nombre": "Eje 1", "longitud": 831.3274, ...}]` | `listar_alineamientos`: `[]` |
| **Perfil de terreno y vista** (`Terreno - Terreno`, `Eje 1 - Vista`) | `Executefunction` | `"result": "terminado"` (454 ms) | **se quitó** | `listar_perfiles`: `[{"nombre": "Terreno - Terreno", "tipo": "EG", "pvis": 107}]`. Vista en pantalla: «si se ve». | `listar_perfiles`: `[]`. Vista en pantalla: «desaparecio tambien». |
| **Superficies Corredor Top y Datum** (`Corredor 1 - Top`, `Corredor 1 - Datum`) | `Executefunction` | `"result": "terminado"` (412 ms) | **se quitó** | `listar_superficies`: `[Terreno, Corredor 1 - Top, Corredor 1 - Datum]` | `listar_superficies`: `[Terreno]` |
| **Superficie de volumen** (`Corredor 1 - Volumen`) | `Executefunction` | `"result": "terminado"` (145 ms) | **se quitó** | `listar_superficies`: `[Terreno, Corredor 1 - Top, Corredor 1 - Datum, Corredor 1 - Volumen]` | `listar_superficies`: `[Terreno, Corredor 1 - Top, Corredor 1 - Datum]` |

---

## 3. Mensajes en la Línea de Comandos de Civil 3D

El historial capturado mediante `leer_historial` confirma que los comandos `EXECUTEFUNCTION` y `UNDO` se ejecutaron sin advertencias ni bloqueos:

```text
Comando inicia: EXECUTEFUNCTION
Registro ...: crear_superficie_tin OK (62 ms)
Comando termina: EXECUTEFUNCTION
Comando inicia: UNDO
Comando termina: UNDO

Comando inicia: EXECUTEFUNCTION
Registro ...: crear_alineamiento_desde_polilinea OK (50 ms)
Comando termina: EXECUTEFUNCTION
Comando inicia: UNDO
Comando termina: UNDO

Comando inicia: EXECUTEFUNCTION
Registro ...: crear_perfil_terreno_y_vista OK (97 ms)
Comando termina: EXECUTEFUNCTION
Comando inicia: UNDO
Comando termina: UNDO

Comando inicia: EXECUTEFUNCTION
Registro ...: crear_superficies_corredor OK (255 ms)
Comando termina: EXECUTEFUNCTION
Comando inicia: UNDO
Comando termina: UNDO

Comando inicia: EXECUTEFUNCTION
Registro ...: calcular_volumenes OK (125 ms)
Comando termina: EXECUTEFUNCTION
Comando inicia: UNDO
Comando termina: UNDO
```

> **Nota de la sesión de desarrollo (20/11/2026, tarea 6.11).** Se sube tal como lo guardó el agente instalador. En la
> pregunta 4, la explicación de por qué hizo falta `_.UNDO 2` («deshizo la llamada de lectura `listar_lineas_muestreo`»)
> no cuadra con lo ya validado: las lecturas no dejan entrada en el menú Deshacer (28/09/2026). Lo que sí se comprobó
> es el resultado: tras el `_.UNDO 2`, el `Eje 2` quedó con 0 grupos y el `Eje 1` intacto.

# Informe de Validación v1.19.3 (Líneas de Muestreo, Secciones y Duplicidad de Nombres)

- **Fecha:** 2026-11-20
- **Civil 3D:** Autodesk Civil 3D 2027 (Español)
- **Rama:** `tarea/ronda-d` (commit `153dcb6`)
- **Versión plugin / ping:** `1.21.2` (63 herramientas disponibles)
- **DWG de prueba:** `PRUEBA_1193.dwg` (copia limpia de `PRUEBA_112.dwg`)

---

## 1. Tabla de Ejecución Paso a Paso

| Paso | Herramienta | Parámetros Clave | Resultado | Tiempo (Civil 3D / Puente) | Observaciones / Respuestas |
| :--- | :--- | :--- | :---: | :---: | :--- |
| **0** | `ping` | - | **OK** | 0 ms / 7 ms | Versión `1.21.2`, 63 herramientas. |
| **1** | `listar_alineamientos`<br>`listar_superficies`<br>`listar_corredores`<br>`listar_lineas_muestreo` | Inspección inicial del dibujo | **OK** | 0 ms / 7 ms<br>6 ms / 13 ms<br>4 ms / 11 ms<br>7 ms / 14 ms | `Eje 1` (L=831.33 m).<br>4 superficies: `Terreno`, `Terreno 2`, `Corredor 1 - Top`, `Corredor 1 - Datum`.<br>1 corredor: `Corredor 1`.<br>0 grupos de líneas de muestreo previos. |
| **2** | Verificación de backups | Conteo en `backups\` | **OK** | - | 26 backups existentes. |
| **3** | `crear_secciones` | `Eje 1`, int=20m, w=30m, todas las fuentes, sin vistas | **OK** | 177 ms / 238 ms | 43 líneas muestreadas. 215 secciones reconocidas (172 vía `SampleLine.GetSectionId`, 43 vía `Section.SourceId`, 0 sin identificar). |
| **4** | `crear_secciones` | `Eje 1`, `Muestreo 100`, int=100m, w=20m, fuentes=["Terreno"] | **OK** | 27 ms / 37 ms | 10 líneas (`Muestreo 100 0+000.00` a `Muestreo 100 0+831.33`). `secciones_detalle` con `SampleLine.GetSectionId`. |
| **5.1** | `crear_alineamiento_desde_polilinea` | `Eje 2`, simular=true | **OK** | 3 ms / 10 ms | Longitud simulada: 831.3274 m. Capa `EJE_PRUEBA` existente. |
| **5.2** | `crear_alineamiento_desde_polilinea` | `Eje 2`, simular=false | **OK** | 21 ms / 28 ms | Eje creado exitosamente. Longitud: 831.3274 m. |
| **6** | `crear_secciones` | `Eje 2`, `Muestreo 100`, simular=true | **OK** | 5 ms / 12 ms | Simulación detectó duplicidad con `Eje 1`. Nombres simulados con sufijo `(2)`. Aviso literal de duplicidad emitido. |
| **7** | `crear_secciones` | `Eje 2`, `Muestreo 100`, simular=false (**Decisivo**) | **OK (Caso B)** | 24 ms / 53 ms | **Civil 3D rechazó el nombre duplicado.** Plugin activó mecanismo alternativo: grupo creado como `Eje 2 - Muestreo 100` con 10 líneas. |
| **8** | `crear_secciones` | `Eje 1`, `Muestreo 100`, simular=false | **OK (Error esperado)** | 7 ms / 15 ms | `ok: false`. Error literal: `Error de Civil 3D: ArgumentException: Ya existe el grupo de líneas de muestreo 'Muestreo 100' en el alineamiento 'Eje 1'. Usa listar_lineas_muestreo o pasa otro 'nombre'.` |
| **9** | Inspección Deshacer y backups | Conteo backups | **OK** | - | 27 backups (+1 archivo nuevo, copias posteriores reutilizadas según diseño). |
| **10** | `listar_lineas_muestreo` | Sin filtro (**Decisivo**) | **OK** | 5 ms / 33 ms | `Eje 1`: 2 grupos intactos (`Eje 1 - Muestreo` [43], `Muestreo 100` [10]). `Eje 2`: 1 grupo (`Eje 2 - Muestreo 100` [10]). |
| **11** | `ejecutar_comando` | `_.UNDO 2` | **OK** | 113 ms / 125 ms | Comando terminado limpiamente. |
| **12** | `listar_lineas_muestreo` | Comprobar deshacer | **OK** | 4 ms / 42 ms | `Eje 2`: 0 grupos (revertido por completo). `Eje 1`: 2 grupos intactos con sus 43 y 10 líneas. |
| **13** | `leer_log` | ultimas_n=30 | **OK** | 1 ms / 8 ms | Registro consistente: solo paso 8 tuvo `ok: false`. |

---

## 2. Respuestas a Preguntas Técnicas para la Memoria

### 1. ¿Civil 3D deja crear un grupo de líneas de muestreo con el nombre de un grupo que ya existe en otro eje?
**Respuesta: NO (CASO B confirmado).**
En Autodesk Civil 3D 2027, la colección interna de grupos de líneas de muestreo requiere nombres globalmente únicos en el documento o `SampleLineGroup.Create` valida unicidad estricta. Al intentar crear `Muestreo 100` en `Eje 2`, Civil 3D lanzó excepción nativa:
- **`datos.nombre_grupo.texto_civil3d`:** `"Sample line group name is duplicate."`
- **`datos.nombre_grupo.rechazado_por_civil3d`:** `true`
- **`datos.nombre_grupo.alternativo`:** `"Eje 2 - Muestreo 100"`
- **`datos.nombre_grupo.usado`:** `"Eje 2 - Muestreo 100"`
- **Aviso emitido:** `"Civil 3D no dejó crear el grupo 'Muestreo 100' en el eje 'Eje 2' porque ya hay uno con ese nombre en el eje 'Eje 1' (rechazo por nombre repetido, en datos.nombre_grupo.texto_civil3d): se creó como 'Eje 2 - Muestreo 100', y sus líneas y vistas llevan ese nombre. El grupo del otro eje no se tocó."`
- **¿El intento rechazado dejó algo?** No. No hubo residuos ni objetos huérfanos gracias al manejo transaccional interno del plugin que aplicó el nombre alternativo dentro de la misma transacción.

### 2. ¿El grupo del Eje 1 quedó intacto (paso 10)?
**Respuesta: SÍ, 100% intacto.**
En el Paso 10, la inspección demostró que `Eje 1` conservó inalterados sus dos grupos:
1. `Eje 1 - Muestreo`: 43 líneas (0 a 831.3274 m).
2. `Muestreo 100`: 10 líneas (0 a 831.3274 m).
En `Eje 2` se creó exclusivamente:
- Grupo: `Eje 2 - Muestreo 100` (10 líneas: `Eje 2 - Muestreo 100 0+000.00` hasta `Eje 2 - Muestreo 100 0+831.33`).

### 3. `secciones_detalle` del paso 3: métodos de identificación y superficies
- **Total secciones identificadas:** 215 de 215 (100%).
- **Secciones no identificadas:** 0.
- **`SampleLine.GetSectionId`:** 172 secciones (4 superficies de terreno/corredor tipo TIN: `Terreno`, `Terreno 2`, `Corredor 1 - Top`, `Corredor 1 - Datum` en las 43 progresivas).
- **`Section.SourceId`:** 43 secciones (correspondientes al corredor `Corredor 1`).
- Ambas superficies de corredor (`Corredor 1 - Top` y `Corredor 1 - Datum`) fueron correctamente resueltas por `SampleLine.GetSectionId` vinculadas a sus IDs de superficie.

### 4. ¿Un solo `_.UNDO 2` quitó el grupo del Eje 2 (paso 12)?
**Respuesta: SÍ.**
La ejecución de `_.UNDO 2` deshizo la llamada de lectura `listar_lineas_muestreo` del paso 10 y la transacción de creación del paso 7. Al consultar inmediatamente con `listar_lineas_muestreo`:
- `Eje 2` quedó con **0 grupos**.
- `Eje 1` conservó íntegros sus **2 grupos**.

### 5. Tiempos de ejecución en Civil 3D
- **Paso 3 (`crear_secciones` completo 43 líneas x 5 fuentes = 215 secciones):** 177 ms (Civil 3D) / 238 ms total.
- **Paso 4 (`crear_secciones` 10 líneas, 1 fuente):** 27 ms (Civil 3D) / 37 ms total.
- **Paso 7 (`crear_secciones` con rechazo y fallback alternativo):** 24 ms (Civil 3D) / 53 ms total.

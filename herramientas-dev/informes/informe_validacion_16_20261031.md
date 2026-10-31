# Informe de Validación ArbaMcp 1.6.0 en Autodesk Civil 3D 2027

- **Fecha:** 31 de octubre de 2026
- **Entorno:** Autodesk Civil 3D 2027 (Español Métrico) sobre Windows 11
- **Versión de ArbaMcp:** 1.6.0 (`FileVersion`: `1.6.0.0` en `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`)
- **Versión de PuenteMcp:** 1.6.0
- **Dibujo de prueba:** `<dibujo>`
- **Token:** `<oculto>`

---

## 1. Tabla Resumen de Pasos

| Paso | Herramienta / Acción | Tiempo total | Resultado | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|
| **0** | Instalar 1.6.0 y `ping` | ~5 s | **OK** | `ping`: `{"plugin": "ArbaMcp", "version": "1.6.0.0", "puerto": 8765, "dibujo": "<dibujo>", "hay_dibujo": true, "hora": "2026-10-31 11:57:33", "ms": 22, "ms_espera": 0, "ms_ejecucion": 21, "id_log": "20261031-115733-a69cb0", "ms_puente": 38}` | Compilación e instalación sin errores. `id_log` generado con formato `AAAAMMDD-HHMMSS-xxxxxx`. 46 herramientas disponibles. |
| **1** | Borrón y cuenta nueva | ~10 ms | **OK** | Carpeta de trabajo limpia creada para la validación con la copia del dibujo. | `mcp_log.jsonl` inicia solo con las llamadas de esta sesión. |
| **2** | Lectura con pedido (decisivo) | ~35 ms | **OK** | `listar_superficies`: `{"result": [{"nombre": "Topografia", "tipo": "TinSurface", ...}], "ms": 21, "ms_espera": 0, "ms_ejecucion": 21, "id_log": "20261031-115830-d6e8f4", "ms_puente": 34}` | `id_log` generado: `20261031-115830-d6e8f4`. La línea en `mcp_log.jsonl` registra exactamente el `pedido` "lista las superficies". |
| **3** | `pedido` opcional sin cambios | ~15 ms | **OK** | `listar_superficies`: `{"result": [...], "ms": 5, "id_log": "20261031-115834-f0928f", "ms_puente": 12}` | Misma lista de superficies; nuevo `id_log` y en el log `"pedido": null`. |
| **4** | Aprobar resultado (decisivo) | ~15 ms | **OK** | `aprobar_resultado`: `{"id": "20261031-115842-21f103", "id_log": "20261031-115830-d6e8f4", "aprobado": true, "comentario": "correcto", "herramienta": "listar_superficies", "hora_llamada": "2026-10-31 11:58:30", "pedido": "lista las superficies", "llamada_ok": true, "ruta": "<ruta>\\mcp_log.jsonl", "ms": 4, "ms_puente": 11}` | Añade en el log la línea tipo `aprobacion` y la llamada de `aprobar_resultado`. |
| **5** | `leer_log` con las dos clases de línea | ~10 ms | **OK** | `leer_log`: `{"ruta": "<ruta>\\mcp_log.jsonl", "lineas": [{"tipo": "llamada", "id": "20261031-115733-a69cb0", ...}, {"tipo": "llamada", "id": "20261031-115830-d6e8f4", "herramienta": "listar_superficies", "pedido": "lista las superficies", ...}, {"tipo": "aprobacion", "id": "20261031-115842-21f103", "id_log": "20261031-115830-d6e8f4", "aprobado": true, "comentario": "correcto"}, ...], "ms": 2, "ms_puente": 8}` | Muestra las llamadas y la aprobación vinculada por `id_log`. |
| **6** | Error controlado | ~30 ms | **OK** | `aprobar_resultado` con `id_log: "no-existe"`: `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe ninguna llamada con id_log 'no-existe' en <ruta>\\mcp_log.jsonl. Usa leer_log para ver los id de las últimas llamadas."}`.<br>Llamada sin `aprobado`: error de validación de esquema pydantic. | Ambas llamadas registradas sin crear entradas `aprobacion`. |
| **7** | Sin entrada en Deshacer ni copias | ~5 ms | **OK** | Comprobado que la carpeta `backups\` no existe. Las herramientas de consulta y registro corren en contexto `Aplicacion` / `Inmediato` sin alterar la pila de comandos de Civil 3D. | Comportamiento verificado. |
| **8** | Escritura simulada y asíncrona | ~3.4 s | **OK** | `guardar_copia(simular=true)`: `{"simulado": true, "herramienta": "guardar_copia", ..., "id_log": "20261031-115906-ad9716", "ms_puente": 10}`.<br>`ejecutar_comando("REGEN")`: `{"result": "terminado", "ms": 3346, "id_log": "20261031-115910-de2bb1", "ms_puente": 3356}`. | Registra la simulación y la orden asíncrona al terminar. |
| **9** | `exportar_dataset` (decisivo) | ~400 ms | **OK** | Salida CLI:<br>`exportar_dataset: 10 líneas de 1 archivo(s) mcp_log.jsonl, 1 llamadas aprobadas, 1 pares → <ruta>\pares_prueba.jsonl (sin lista de prohibidos: solo se anonimizan rutas y nombres .dwg)`. | Genera archivo con 1 línea anonimizada sin rutas locales ni referencias `.dwg`. Nota: En consolas Windows requiere `$env:PYTHONUTF8='1'`. |
| **10** | Exportación a dataset general | ~200 ms | **OK** | Salida CLI:<br>`exportar_dataset: 10 líneas de 1 archivo(s) mcp_log.jsonl, 1 llamadas aprobadas, 1 pares → C:\Proyectos\memoria-firma\dataset\pares.jsonl`. | Dataset de la firma actualizado con el nuevo par anonimizado. |

---

## 2. Contenido de `pares_prueba.jsonl`

```json
{"pedido": "lista las superficies", "herramientas": [{"nombre": "listar_superficies", "args": {}}], "resultado": {"ok": true, "resumen": "1 llamada correcta"}, "fecha": "2026-10-31", "version": "1.6.0"}
```

---

## 3. Comprobaciones de comportamiento (sección 1.6.0 de `miembros_por_verificar_civil3d.md`)

- **Deshacer en el paso 7**: verificado que lecturas y aprobaciones no alteran la pila UNDO ni crean copias de respaldo (ejecutado en Civil 3D 2027, 31 de octubre de 2026).
- **Línea de las asíncronas en el paso 8**: verificado que `ejecutar_comando` registra su línea de log con `ok: true` al terminar mediante la cola `Inmediato` (ejecutado en Civil 3D 2027, 31 de octubre de 2026).

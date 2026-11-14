# Informe de validación 1.15.0 (`listar_pis`, rama `tarea/2.9-tabla-pi`)

> **Nota de la orquestadora:** el agente instalador no guardó este informe como archivo en la PC (solo entregó el
> informe corto para pegar). Este archivo es la **transcripción literal de ese informe corto**, tal como lo pegó el
> dueño en la sesión orquestadora el 14/11/2026.

- **Fecha:** 2026-11-14
- **Civil 3D:** 2027 (español)
- **Rama instalada:** `tarea/2.9-tabla-pi` (commit `31ef86b`, `ArbaMcp` v1.15.0)
- **Dibujo:** `PRUEBA_1142.dwg` (el que dejó guardado `VALIDACION_1142.md`)
- **Resultado global:** **OK en todos los pasos** (`diferencia_pi_civil3d: 0` en las curvas; el PI sin curva de
  `Eje 115`, correcto)

## Pasos (informe corto del agente, literal)

```text
=== VALIDACION_115.md (listar_pis) ===
- Paso 0 (ping + 54 herramientas + listar_pis): OK.
- Paso 1 (listar_alineamientos): OK (5 ms) — Eje 1 (0..831.3274).
- Paso 2 (listar_pis Eje 1): OK (23 ms, puente 34 ms) — numero_pi: 2, numero_curvas: 2, avisos: [], PI 1 (pk 300, 1300/2000, 90°00'00.00" izq, R=80, L=125.6637, T=80, PC=220, PT=345.6637, diferencia_pi_civil3d=0), PI 2 (pk 565.6637, 1300/2300, 90°00'00.00" der, R=80, L=125.6637, T=80, PC=485.6637, PT=611.3274, diferencia_pi_civil3d=0).
- Paso 3 (contraste fila a fila): OK.
- Paso 4.1 (crear_alineamiento_desde_polilinea Eje 115 simular=true): OK (40 ms, puente 50 ms) — polilínea de EJE_PRUEBA encontrada, longitud 865.6637, numero_curvas: 1.
- Paso 4.2 (crear_alineamiento_desde_polilinea real Eje 115): OK (103 ms, puente 116 ms) — copia PRUEBA_1142_20261114_111738_crear_alineamiento_desde_polilinea.dwg.
- Paso 4.3 (listar_pis Eje 115): OK (5 ms, puente 20 ms) — PI 1 tipo: "sin_curva" (pk 300, 1300/2000, radio/PC/PT/diferencia_pi_civil3d: null), PI 2 tipo: "curva" (pk 600, 1300/2300, R=80, PC=520, PT=645.6637, diferencia_pi_civil3d: 0), avisos: [].
- Paso 5a (listar_pis NoExiste): OK (puente 19 ms) — "Error de Civil 3D: ArgumentException: No existe el alineamiento 'NoExiste'. Usa listar_alineamientos."
- Paso 5b (listar_pis sin alineamiento): OK — error de esquema Pydantic MCP: "1 validation error for listar_pisArguments / args.alineamiento / Field required".
- Paso 6 (leer_log 3 ms + backups\): OK — 1 copia nueva en backups\ (10 total) y llamadas registradas con su pedido literal.
```

## Para la memoria técnica (lo que deja este informe)

- `AlignmentEntity.SubEntityCount`, `AlignmentEntity[int]` y `AlignmentSubEntityArc.PIPoint` funcionan: el PI que da
  Civil 3D coincide con el calculado por el plugin (`diferencia_pi_civil3d: 0` en las tres curvas).
- `Alignment.PointLocation` en el final exacto de las entidades da los PC y PT esperados (220 / 345.6637 y
  485.6637 / 611.3274).
- El paso manual con el informe de alineamiento de Civil 3D (Caja de herramientas → Informes) no consta en el informe
  corto.

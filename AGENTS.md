# AGENTS.md — ODW

Guía para cualquier agente que retome este repositorio. La guía completa de continuidad está en
[`docs/HANDOFF.md`](docs/HANDOFF.md); la puesta en marcha rápida en [`CONTINUE.md`](CONTINUE.md);
el estado técnico en [`docs/IMPLEMENTATION.md`](docs/IMPLEMENTATION.md).

## Qué es

ODW conecta PCs Windows con OpenCode: un nodo **origen** propone una tarea, un humano la aprueba
por pipe/Telegram y un nodo **destino** la ejecuta como `odw-agent` en un workspace aislado.
El resultado incluye resumen, `changed_paths`, commit y diff.

## Estado actual (2026-11-25)

- **328/328 pruebas**, build sin advertencias; commit de referencia: el último de `main`.
- H0, H1 (loopback), H2 (pipe/CLI/HMAC) y H3 cerrados.
- Nodo destino `PC-CASA` preparado y verificado: servicio `OdwNode` como `.\odw-svc`, listener
  `100.94.46.95:7421`, `/peer/v1/status` autenticado OK y ensayo A→B contra la IP tailnet
  (`E2E-REMOTE` y `E2E-REMOTE-WRITE`, origen en el mismo equipo).
- Pendiente: salto real PC casa ↔ PC oficina (kit en `C:\ODW\kit-origen.zip`; pasos en
  `docs/RUNBOOK-DOS-PCS.md`), artefactos descargables, Telegram real, H4.

## Antes de tocar código

```powershell
dotnet build Odw.slnx -warnaserror
dotnet test Odw.slnx
```

No debilites sin justificar y documentar:

- `opencode serve` solo en `127.0.0.1`.
- aprobación solo por pipe/Telegram, ligada a `task_hash`, expira y un solo uso;
- el agente no lee `node.json`, `odw.db` ni claves;
- `network:"none"` = solo el proxy del modelo; `network:"allow"` se rechaza;
- `max_concurrent=1` y `keep=false` con `run_as_agent` (mismo SID);
- el nodo no corre como administrador (`odw-svc`).

## Arranque / pruebas

```powershell
powershell -ExecutionPolicy Bypass -File tools/provision-sandbox.ps1  # consola elevada
odw sandbox check
tools/e2e-loopback.ps1              # A→B en loopback, read_only
tools/e2e-loopback.ps1 -WriteTask   # changed_paths/commit/diff
```

## Dónde mirar

| Tema | Documento |
|---|---|
| Continuidad, secretos locales y puesta en marcha | `docs/HANDOFF.md` |
| Implementación, cifras y configuración | `docs/IMPLEMENTATION.md` |
| Hitos y criterios | `docs/ROADMAP.md` |
| Seguridad, ACLs, WFP, aprobaciones | `docs/SECURITY.md` |
| Contrato, estados y resultado | `docs/DOMAIN-MODEL.md` |
| API de pares | `docs/PROTOCOL.md` |
| Fachada MCP | `docs/MCP-FACADE.md` |
| Prueba entre casa y oficina (dos PC) | `docs/RUNBOOK-DOS-PCS.md` |
| Grafo del código | `graphify-out/GRAPH_REPORT.md` |

## Grafo

```powershell
python -m graphify query "¿Cómo se aisla la red del agente?"
python tools/graphify-project.py   # regenerar
```

## Reglas de trabajo

- Cambios pequeños, compilando y con pruebas; no declares aislamiento no verificado.
- No copies secretos ni `node.json` entre PCs; usa `tools/provision-sandbox.ps1`.
- El repo de trabajo debe ser legible por `odw-svc` (es quien clona).
- El workspace temporal se borra: el diff inline es la evidencia del cambio.
- Antes de cerrar, actualiza `docs/IMPLEMENTATION.md`, `docs/ROADMAP.md` y este estado.

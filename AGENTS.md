# AGENTS.md — ODW

Guía para cualquier agente que retome este repositorio. La guía completa de continuidad está en
[`docs/HANDOFF.md`](docs/HANDOFF.md); la puesta en marcha rápida en [`CONTINUE.md`](CONTINUE.md);
el estado técnico en [`docs/IMPLEMENTATION.md`](docs/IMPLEMENTATION.md).

## Qué es

ODW conecta PCs Windows con OpenCode: un nodo **origen** propone una tarea, un humano la aprueba
por pipe/Telegram y un nodo **destino** la ejecuta como `odw-agent` en un workspace aislado.
El resultado incluye resumen, `changed_paths`, commit y diff.

## Estado actual (2026-11-26)

- **421/421 pruebas**, build sin advertencias; commit de referencia: el último de `main`.
- H0, H2 (pipe/CLI/HMAC) y H3 cerrados. H1: delegación real en dos PCs verificada; doctor PASS
  en PC-OFICINA y PC-CASA y cobertura Core 97,8 %; falta el reinicio a mitad.
- Nodo destino `PC-CASA` preparado y verificado: servicio `OdwNode` como `.\odw-svc`, listener
  en la IPv4 tailnet (`<IP-TAILNET-CASA>:7421`), `/peer/v1/status` autenticado OK.
- **H1 en dos PCs verificado (2026-11-26):** `PC-OFICINA` → `PC-CASA` por Tailscale.
  `read_only` (`E2E-REMOTE`, tarea `01M8C901HCSFCE93QJH1P3YRYD`) y `read_write_worktree`
  (`E2E-REMOTE-WRITE`, tarea `01M8C90GX6903D3VZBMEGXKAD9`, `changed_paths=["src/remoto.txt"]`,
  commit `48d36b9…` y diff inline).
- **Perfiles de modelo verificados (2026-11-26):** tarea `01M8CGJSEFCR29SPN1SSJ4M7C7` con
  `model_profile="free"` → `DONE`/`MODELPROFILE-FREE`; resultado `model_profile=free`,
  `model_provider=opencode`, `model_id=deepseek-v4.1-flash-free`, `cost_usd=0`.
  `PC-CASA` actualizado a `87855c7`; `paid-deepseek` sigue deshabilitado.
- **Doctor y cobertura (2026-11-26):** `odw doctor` 10/10 PASS en PC-OFICINA
  (`tools/doctor-node.ps1 -AllowLoopback`) y en PC-CASA (`tools/doctor-node.ps1`, 0
  advertencias); `Odw.Core` 97,8 % líneas / 95 % ramas (1013/1036) con `coverlet.runsettings`
  (excluye generados).
- Decisión 2026-11-26: el token y el repo puente se mantienen para la campaña de pruebas; el repo
  debe seguir privado. Al terminar: rotar el token, borrar el repo y el kit.
- Pendiente: artefactos descargables, Telegram real y H4.
- **Repo puente del kit:** su `peer.token` se verificó **vivo** y se usa para pruebas. No lo
  publiques ni compartas el kit; al terminar la campaña, bórralo y rota el token.

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

# origen persistente para OpenCode (MCP en 127.0.0.1:7422)
tools/origin-node.ps1 -PeerTokenFile <token> -TargetAddress http://<IP-TAILNET-CASA>:7421

# doctor con la config real del nodo; en el origen de pruebas añade -AllowLoopback
tools/doctor-node.ps1
tools/doctor-node.ps1 -Config <origen>\data\node.json -AllowLoopback -OpenCode <opencode.exe>
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
- No transportes el kit ni `peer.token` por git, chat ni correo: usa USB, Taildrop o gestor de
  contraseñas. Un repo temporal con secretos se borra y el token se rota.
- Los documentos públicos usan marcadores `<IP-TAILNET-...>`; no metas IPs, hostnames ni cuentas
  reales de la tailnet.
- El origen solo pide `model_profile`; el destino valida contra `models.allowed`. No copies
  credenciales de modelo al perfil del agente.
- El repo de trabajo debe ser legible por `odw-svc` (es quien clona).
- El workspace temporal se borra: el diff inline es la evidencia del cambio.
- Antes de cerrar, actualiza `docs/IMPLEMENTATION.md`, `docs/ROADMAP.md` y este estado.

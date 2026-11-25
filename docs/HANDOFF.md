# ODW — Continuar desde otra PC

> **Último commit:** `ebdf119` · **Rama:** `main` · **Fecha:** 2026-11-25
>
> **Estado:** H0, H1 (protocolo en loopback), H2 (pipe/CLI/HMAC) y H3 cerrados y verificados.
> Quedan H1 en dos PCs con Tailscale, artefactos descargables, Telegram real y H4.
> **328/328 pruebas**, build sin advertencias.

Este documento es el punto de entrada para retomar el proyecto en otro equipo sin depender de
la memoria de una sesión. El diseño detallado vive en `docs/`; el grafo del código en
`graphify-out/`.

---

## 1. Qué es ODW

Red cooperativa de PCs Windows con OpenCode: un nodo **origen** propone una tarea, un humano la
aprueba y un nodo **destino** la ejecuta en un workspace aislado. El destino devuelve resumen,
rutas cambiadas, commit y diff; el origen lo consulta por la API de pares.

```mermaid
flowchart LR
  H[Humano] -->|MCP propose| OA[PC-A origen]
  H -->|pipe approve| OA
  OA -->|POST /peer/v1/tasks| OB[PC-B destino]
  OB --> SCH[Scheduler]
  SCH --> WS[Workspace Git + ACL]
  WS --> AG[opencode serve como odw-agent]
  AG -->|HTTPS_PROXY| PX[Proxy loopback]
  PX -->|allowlist| MODEL[opencode.ai]
  OA -->|poller| OB
  OB -->|resultado| OA
```

**Reglas que no se negocian**

1. `opencode serve` solo escucha en `127.0.0.1`.
2. No hay aprobación por HTTP ni por herramienta MCP; solo pipe/Telegram.
3. La aprobación va ligada a `task_hash`, expira y es de un solo uso.
4. El agente no puede aprobar sus propias delegaciones.
5. Ninguna ruta absoluta remota entra en el contrato.
6. `odw-agent` no puede leer configuración, base ni claves del nodo.
7. `network: "none"` = sin red arbitraria; solo el proxy del modelo.

---

## 2. Requisitos para otra PC

- **Windows 11 x64** (el sandbox real es Windows: WFP, ACLs, `CreateProcessWithLogonW`).
- **.NET 10 SDK** y Git.
- **OpenCode 2.3.2** (el ejecutable se copia al sandbox; no hace falta el escritorio).
- **Consola elevada** para aprovisionar usuarios, ACLs, WFP y el servicio.
- Para la prueba real entre PCs: **Tailscale** en ambos nodos y ACL que permita el puerto de pares
  (por defecto 7421). Sin Tailscale, usar loopback + túnel SSH; el bind a una IP LAN directa se
  rechaza por diseño.
- Opcional para el grafo: Python 3.11+ y `pip install graphifyy`.

---

## 3. Puesta en marcha desde cero

```powershell
git clone https://github.com/DanielCB096/ODW-OpenCode-Distributed-Worker-Network.git
cd "ODW-OpenCode-Distributed-Worker-Network"

dotnet build Odw.slnx -warnaserror
dotnet test Odw.slnx

# Consola ELEVADA: usuarios odw-agent/odw-svc, DPAPI, ACLs, plantilla y WFP
powershell -ExecutionPolicy Bypass -File tools/provision-sandbox.ps1
dotnet run --project src/Odw.Cli -- sandbox check
dotnet run --project src/Odw.Cli -- sandbox net status
```

El aprovisionamiento crea:

- `odw-agent` (sin privilegios) y `odw-svc` (servicio sin admin);
- `%PROGRAMDATA%\ODW\agent.secret` y `service.secret` (DPAPI de máquina);
- `%PROGRAMDATA%\ODW\opencode\opencode.exe` con RX para el agente;
- `%PROGRAMDATA%\ODW\agent-config\opencode.json` (modelo gratuito, `snapshots=false`);
- `%PROGRAMDATA%\ODW\runtime` para perfiles desechables por tarea;
- ACLs de datos y filtros WFP para `odw-agent` (solo `127.0.0.1:7423`) + `network.state`.

Publica e instala el servicio:

```powershell
dotnet publish src/Odw.Cli -c Release -o C:\ODW\publish -r win-x64 --self-contained false
odw service install `
  --config C:\ODW\node.json `
  --exe C:\ODW\publish\odw.exe `
  --account .\odw-svc `
  --secret %PROGRAMDATA%\ODW\service.secret
```

`odw-svc` necesita **RX** sobre `C:\ODW\publish` y su directorio. Si el config está en
`%PROGRAMDATA%\ODW`, ya tiene F por el aprovisionamiento.

---

## 4. Qué NO está en Git

Nunca copies estos elementos entre PCs; son locales y contienen secretos o estado de máquina:

| Elemento | Dónde | Qué hacer |
|---|---|---|
| Usuarios `odw-agent`/`odw-svc` | SAM local | Crearlos con `provision-sandbox.ps1` |
| `agent.secret`, `service.secret` | `%PROGRAMDATA%\ODW` | Regenerar con el script; nunca copiar |
| `network.state` | `%PROGRAMDATA%\ODW` | Lo escribe `odw sandbox net install` |
| Filtros WFP | BFE | Instalarlos con el script/CLI |
| `node.json`, tokens de par/MCP | `%PROGRAMDATA%\ODW` o config | Crear y intercambiar por canal seguro |
| `odw.db`, `approvals.key` | data dir | Se generan en el primer arranque |
| Workspaces y runtime | `%PROGRAMDATA%\ODW` | Efímeros; no copiar |
| `opencode.exe` | `%PROGRAMDATA%\ODW\opencode` | Lo copia el aprovisionamiento |

El token de par es **compartido y bidireccional** en el MVP. Intercámbialo por Signal/gestor de
contraseñas, nunca por chat ni en el repositorio.

---

## 5. Configurar un nodo

Parte de `config/node.example.json`. Campos que importan:

```jsonc
{
  "node_id": "PC-A",
  "peer_api": { "bind": "IP_TAILNET", "port": 7421, "allow_loopback": false },
  "mcp": { "bind": "127.0.0.1", "port": 7422, "token_ref": "env:ODW_MCP_TOKEN" },
  "scheduler": { "enabled": true, "max_concurrent": 1, "poll_seconds": 2 },
  "workspaces": { "root": "%PROGRAMDATA%\\ODW\\workspaces", "keep": false },
  "repos": [ { "name": "SistemaMaestro", "path": "C:\\ruta\\repo", "base_ref": "main" } ],
  "peers": [
    { "node_id": "PC-B", "address": "http://IP_TAILNET_B:7421",
      "token_ref": "env:ODW_PEER_TOKEN", "trust": "trusted" }
  ],
  "sandbox": {
    "run_as_agent": true,
    "network_isolation": true,
    "network_proxy_port": 7423,
    "runtime_root": "%PROGRAMDATA%\\ODW\\runtime",
    "agent_config_path": "%PROGRAMDATA%\\ODW\\agent-config\\opencode.json",
    "network_state_path": "%PROGRAMDATA%\\ODW\\network.state"
  }
}
```

- **Repo de origen legible por `odw-svc`** (es quien clona). Si está en una carpeta privada,
  concede lectura al servicio; el agente no necesita leerlo, solo el workspace clonado.
- Con `run_as_agent` el nodo exige `max_concurrent=1` y `keep=false`; no los cambies sin diseñar
  aislamiento por tarea.
- El origen puede tener `scheduler.enabled=false`; el destino `true`.
- Para TLS opt-in: `peer_api.tls` con PFX y `password_ref`. Sobre Tailscale/SSH no es obligatorio.
- El bind de pares solo acepta tailnet o loopback (`allow_loopback` para pruebas/túnel).

---

## 6. Operación

```powershell
odw service status
odw service uninstall
odw sandbox check
odw sandbox net status
Invoke-RestMethod http://127.0.0.1:7421/peer/v1/health
```

Logs: el servicio escribe en el Event Log; en consola, `odw serve` imprime el estado y los
errores de OpenCode. El workspace y el runtime por tarea se borran al terminar.

### E2E en un solo PC

```powershell
powershell -ExecutionPolicy Bypass -File tools/e2e-loopback.ps1
# escritura: valida changed_paths/commit/diff
powershell -ExecutionPolicy Bypass -File tools/e2e-loopback.ps1 -WriteTask
# conservar logs/config
powershell -ExecutionPolicy Bypass -File tools/e2e-loopback.ps1 -WriteTask -Keep
```

El script arranca dos nodos como `odw-svc`, propone por MCP, aprueba por el pipe y comprueba el
resultado. No sustituye a Tailscale/dos PCs.

---

## 7. Evidencia actual

```text
328/328 pruebas · build 0 warnings
commit ebdf119
```

- E2E loopback `read_only`: `DONE` / `E2E-LOOPBACK`.
- E2E loopback `-WriteTask`: `DONE` / `E2E-WRITE`, `changed_paths=["src/e2e-changed.txt"]`,
  commit y diff.
- Nodo real como `odw-svc` con scheduler, `odw-agent`, WFP, proxy y runtime.
- Aprobación por pipe, propuesta MCP y poller del origen.
- Servicio Windows instalado/arrancado como `.\odw-svc`.

---

## 8. Límites y pendientes

| Tema | Estado |
|---|---|
| Prueba en dos PCs con Tailscale | ⬜ pendiente (sin segunda PC) |
| Artefactos descargables | ⬜ el diff va inline (256 KiB); el bundle/artefacto no |
| `commit`/`branch` | ✅ se crean, pero el workspace se borra: el diff es la evidencia; el SHA es informativo |
| `network: "allow"` | ⛔ rechazado (filtro por SID, no por tarea) |
| WFP en servicio sin admin | ✅ marca `network.state`; un admin puede retirar filtros |
| Telegram real | ⬜ implementado, sin probar con bot |
| H4 mensajes/cancelación/leases | ⬜ siguiente hito grande |
| `%PROGRAMDATA%\ODW` legible por el agente | ✅ solo listar/stat; archivos protegidos |
| Derechos de logon | ✅ red/RDP denegados; interactivo local necesario para `CreateProcessWithLogonW` |

---

## 9. Grafo del proyecto

El grafo está en `graphify-out/`:

- `graph.json` — datos para consultas y para el agente.
- `GRAPH_REPORT.md` — auditoría, comunidades, nodos dios y conexiones.
- `graph.html` — visualización interactiva en el navegador.

Consulta desde otra PC:

```powershell
python -m graphify query "¿Cómo se aisla la red del agente?"
python -m graphify path "McpServer" "OpenCodeTaskRunner"
python -m graphify explain "PolicyEngine"
```

Regenerar (AST de código + chunks semánticos de docs):

```powershell
python tools/graphify-project.py
```

Si no está instalado: `pip install graphifyy`. La extracción AST no necesita API key; la semántica
de docs se puede añadir con un subagente que escriba `graphify-out/.graphify_chunk_*.json` y
volviendo a ejecutar el script.

Preguntas útiles para el grafo:

- ¿Qué camino une la propuesta MCP con el scheduler y el workspace?
- ¿Dónde se aplican las ACLs y el filtro WFP?
- ¿Qué componentes participan en la aprobación por pipe?
- ¿Qué queda pendiente en H4?

---

## 10. Mapa de documentación

| Documento | Fuente de verdad para |
|---|---|
| `README.md` | Estado, arranque, CLI y sandbox |
| `ARCHITECTURE.md` | Visión y decisiones originales |
| `docs/IMPLEMENTATION.md` | Qué está implementado, cifras, configuración y comandos |
| `docs/ROADMAP.md` | Hitos y criterios de aceptación |
| `docs/SECURITY.md` | Modelo de amenazas, identidades, ACLs, red y aprobaciones |
| `docs/DOMAIN-MODEL.md` | Contrato, estados, hash, resultado |
| `docs/PROTOCOL.md` | API de pares, outbox, poller |
| `docs/PERSISTENCE.md` | SQLite, WAL, migraciones, auditoría |
| `docs/MCP-FACADE.md` | Herramientas MCP y su seguridad |
| `docs/OPENCODE-ADAPTER.md` | Cliente/SSE/ejecutor de OpenCode |
| `docs/PLANNER-POLICY.md` | Planner y Policy Engine |
| `docs/TESTING.md` | Estrategia y casos hostiles |
| `docs/HANDOFF.md` | Este documento: continuidad desde otra PC |
| `graphify-out/GRAPH_REPORT.md` | Mapa generado del código y docs |

---

## 11. Próximos pasos recomendados

1. **Artefactos descargables**: `GET /peer/v1/tasks/{id}/artifacts/{name}` + bundle/parche completo.
2. **Prueba real en dos PCs** con Tailscale (cuando haya hardware).
3. **Telegram** con un bot real.
4. **H4**: mensajes, cancelación remota, leases y reconciliación tras reinicio.
5. **H0-6**: aislamiento por directorio con instancias de OpenCode ya separadas.

---

## 12. Checklist antes de continuar

- [ ] `git status` limpio y `dotnet test Odw.slnx` en verde.
- [ ] `odw sandbox check`: `admin`, `agent`, `acls`, `network`, `enforced` en `True`.
- [ ] `tools/provision-sandbox.ps1` ejecutado en esta PC (no copiar secretos de otra).
- [ ] Repo de trabajo legible por `odw-svc`.
- [ ] Token de par intercambiado por canal seguro; `node.json` sin secretos literales.
- [ ] `tools/e2e-loopback.ps1` pasa en read_only y `-WriteTask`.
- [ ] Tailscale/SSH decidido antes de intentar dos máquinas.

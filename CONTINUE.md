# Continuar ODW desde otra PC

Guía corta y accionable. El detalle está en [`AGENTS.md`](AGENTS.md) y
[`docs/HANDOFF.md`](docs/HANDOFF.md); el estado técnico en
[`docs/IMPLEMENTATION.md`](docs/IMPLEMENTATION.md).

## 1. Clonar y comprobar

```powershell
git clone https://github.com/DanielCB096/ODW-OpenCode-Distributed-Worker-Network.git
cd "ODW - OpenCode Distributed Worker Network"

dotnet build Odw.slnx -warnaserror
dotnet test Odw.slnx
```

## 2. Leer antes de tocar

1. [`AGENTS.md`](AGENTS.md): reglas que no se deben romper.
2. [`docs/HANDOFF.md`](docs/HANDOFF.md): estado, secretos locales, límites y siguientes pasos.
3. [`graphify-out/GRAPH_REPORT.md`](graphify-out/GRAPH_REPORT.md): mapa del código y los docs.

## 3. Aprovisionar el sandbox (consola elevada)

```powershell
powershell -ExecutionPolicy Bypass -File tools/provision-sandbox.ps1
dotnet run --project src/Odw.Cli -- sandbox check
dotnet run --project src/Odw.Cli -- sandbox net status
```

Crea `odw-agent`/`odw-svc`, secretos DPAPI, ACLs, plantilla de OpenCode, runtime por tarea y
filtros WFP. **No copies** usuarios, secretos, `network.state`, `node.json`, `odw.db` ni tokens
desde otra PC: son locales y se regeneran.

## 4. Publicar e instalar el servicio

```powershell
dotnet publish src/Odw.Cli -c Release -o C:\ODW\publish -r win-x64 --self-contained false
& C:\ODW\publish\odw.exe service install `
  --config C:\ODW\node.json `
  --exe C:\ODW\publish\odw.exe `
  --account .\odw-svc `
  --secret %PROGRAMDATA%\ODW\service.secret
```

El repo configurado en `repos` debe ser legible por `odw-svc`. El servicio no corre como
administrador.

## 5. Configurar y arrancar

Parte de [`config/node.example.json`](config/node.example.json):

- `peer_api.bind` = IP Tailscale (o `127.0.0.1` + `allow_loopback` para túnel/loopback);
- `mcp` solo en `127.0.0.1`;
- `scheduler.max_concurrent=1` y `workspaces.keep=false` con `run_as_agent`;
- `sandbox.network_isolation=true`, `network_proxy_port=7423`;
- pares con token por canal seguro; sin secretos literales en el JSON.

```powershell
& C:\ODW\publish\odw.exe service status
Invoke-RestMethod http://127.0.0.1:7421/peer/v1/health
```

## 6. Validar de punta a punta

En un solo PC:

```powershell
powershell -ExecutionPolicy Bypass -File tools/e2e-loopback.ps1
powershell -ExecutionPolicy Bypass -File tools/e2e-loopback.ps1 -WriteTask
```

Debe terminar en `DONE` con `E2E-LOOPBACK` y, en el caso de escritura, con
`changed_paths`, commit y diff. `-Keep` conserva logs y configuración.

Para dos PCs: Tailscale en ambos, tokens intercambiados por canal seguro y el bind de pares en la
IP tailnet. Sin Tailscale, usa loopback + túnel SSH; el bind directo a una IP LAN se rechaza.

## 7. Consultar el grafo

```powershell
python -m graphify query "¿Cómo se aisla la red del agente?"
python -m graphify path "McpServer" "OpenCodeTaskRunner"
python -m graphify explain "PolicyEngine"
```

Regenerar: `python tools/graphify-project.py` (requiere `pip install graphifyy`).

## 8. Antes de cerrar un cambio

- `dotnet build Odw.slnx -warnaserror` y `dotnet test Odw.slnx` en verde.
- No debilitar aislamiento, aprobaciones ni binds sin documentarlo.
- Actualizar `docs/IMPLEMENTATION.md`, `docs/ROADMAP.md` y `AGENTS.md`.
- Commit y push a `main`.

# Runbook — prueba H1 entre la PC de casa y la PC del trabajo

> Nodo casa: **PC-CASA** · `pc-casa.<tailnet>.ts.net` · `<IP-TAILNET-CASA>` · servicio `OdwNode` (`.\odw-svc`)
> Nodo oficina: **PC-OFICINA** · `pc-oficina.<tailnet>.ts.net` · `<IP-TAILNET-OFICINA>`
> Puerto de pares: **7421/tcp, solo en la interfaz Tailscale** · MCP local: 7422 · proxy del modelo: 7423

> Los valores entre `<...>` son marcadores: sustitúyelos solo en tu copia local. No publiques
> IPs, hostnames ni cuentas reales de la tailnet.

El objetivo es cerrar el criterio pendiente de H1: **una sesión del nodo origen delega una tarea
`analysis` al nodo destino, un humano la aprueba por el named pipe y el origen recupera el
resultado**, con las dos máquinas en la misma tailnet.

---

## 0. Estado verificado la noche anterior (2026-11-25)

- Servicio `OdwNode` como `.\odw-svc`, dependiente de `Tailscale`, con reinicio automático.
- Listener en `<IP-TAILNET-CASA>:7421`. Se usa `peer_api.address` con la IPv4 explícita porque
  `bind: "tailscale"` resolvía la IPv6 (`fd7a:…`) y Kestrel escuchaba solo ahí.
- `/peer/v1/status` autenticado como `PC-OFICINA` → `status: online`.
- Ensayo A→B con el origen en el mismo equipo, contra la IP tailnet:
  - `read_only` → `WAITING_APPROVAL → ACCEPTED → RUNNING → DONE`, resumen `E2E-REMOTE`.
  - `read_write_worktree` → `DONE`, `E2E-REMOTE-WRITE`, `changed_paths = ["src/remoto.txt"]`,
    commit `7050f45…` y diff con el archivo.
- `odw sandbox check`: `windows`, `admin`, `agent`, `acls`, `network`, `enforced` = `True`.
- Derechos de logon verificados en la política local: `SeServiceLogonRight` y
  `SeIncreaseQuotaPrivilege` para `odw-svc`; `SeDenyNetworkLogonRight` y
  `SeDenyRemoteInteractiveLogonRight` para `odw-agent`.
- BitLocker desactivado en `C:` (un reinicio no pedirá clave).
- El ensayo **no** sustituye el salto entre dos máquinas; eso es lo que se hace mañana.
- **Cierre 2026-11-26 (PC-OFICINA):** `PC-CASA` estaba online y el puerto 7421 respondía. La
  prueba real pasó con el token del kit:
  - `read_only` → `E2E-REMOTE`, tarea `01M8C901HCSFCE93QJH1P3YRYD`.
  - `read_write_worktree` → `E2E-REMOTE-WRITE`, tarea `01M8C90GX6903D3VZBMEGXKAD9`,
    `changed_paths=["src/remoto.txt"]`, commit `48d36b9…` y diff `+HOLA`.
  El token usado **sigue vivo** (la rotación no se aplicó): ejecuta la sección 8 ahora.

---

## 1. Antes de salir de casa

- [ ] `tailscale status` muestra el nodo casa en línea y el de oficina cuando arranque.
- [ ] `Get-Service OdwNode` → `Running`; el servicio depende de `Tailscale`.
- [ ] `Get-NetTCPConnection -LocalPort 7421 -State Listen` → `<IP-TAILNET-CASA>:7421`.
  (`/peer/v1/health` es solo loopback y con el bind a la IP tailnet no es alcanzable; la
  verificación autenticada se hace mañana desde el trabajo con `/peer/v1/status`.)
- [ ] La PC **no se suspende**: `powercfg /change standby-timeout-ac 0` ya aplicado.
- [ ] Windows Update no reinicia solo (horas activas 07:00–01:00, sin reinicio automático).
- [ ] BitLocker: si tiene PIN, ten la clave a mano por si hay un reinicio.
- [ ] Kit copiado a un USB / gestor de contraseñas: `C:\ODW\kit-origen.zip`.
- [ ] El token **no** va por chat ni por correo: solo USB, Taildrop o el gestor de contraseñas.

Deja el equipo encendido y conectado. No hace falta sesión iniciada: el servicio arranca solo.

---

## 2. Mañana en la oficina

### 2.1 Comprobar Tailscale

```powershell
tailscale status
tailscale ping <IP-TAILNET-CASA>
```

Si `PC-OFICINA` no está en la tailnet, instala Tailscale y entra con la misma cuenta
(`<cuenta-tailnet>`). Sin Tailscale la prueba no es posible: el bind de pares rechaza IP LAN.

### 2.2 Copiar y descomprimir el kit

Desde el USB (o `tailscale file get` si usaste Taildrop):

```powershell
Expand-Archive -Path E:\kit-origen.zip -DestinationPath C:\ODW-kit -Force
cd C:\ODW-kit
```

Contenido: `odw.exe` (autocontenido, no necesita .NET), `kit.json`, `peer.token`,
`h1-remote-test.ps1` y este runbook. **No requiere permisos de administrador** ni Git.
El script comprueba el destino con `GET /peer/v1/hello` autenticado antes de proponer.

El kit **no se transporta por git, chat ni correo**: usa USB, Taildrop o el gestor de contraseñas.
Si el token ha estado en un repo, USB compartido o cualquier medio temporal, rótalo antes de la
prueba (`tools/rotate-peer-token.ps1` en el destino), verifica que el anterior da `401` y regenera
el kit con `tools/prepare-origin-kit.ps1`. No reutilices un `kit-origen.zip` antiguo.

### 2.3 Ejecutar la prueba

```powershell
powershell -ExecutionPolicy Bypass -File .\h1-remote-test.ps1
```

El script, en orden:

1. Crea `C:\ODW-origen` con `node.json`, `policy.json` (todo `ASK`) y un token MCP local.
2. Arranca el nodo origen `PC-OFICINA` en `127.0.0.1:17431` (pares) y `17432` (MCP).
3. Consulta el estado de `PC-CASA` con `odw_get_peer_status` (autenticado).
4. Propone `analysis` / `read_only` sobre el repo `demo` con `odw_propose_task`.
5. Aprueba por el pipe `odw-approvals` con `odw approvals approve`.
6. Espera `Done` y muestra resumen, `changed_paths` y commit.

Salida esperada:

```text
PRUEBA OK
  tarea:     <ULID>
  estado:    Done
  resumen:   E2E-REMOTE
```

Variante de escritura (valida `changed_paths`, commit y diff):

```powershell
powershell -ExecutionPolicy Bypass -File .\h1-remote-test.ps1 -WriteTask
```

Debe terminar en `E2E-REMOTE-WRITE` con `src/remoto.txt`, commit y diff.

Si quieres dejar el origen vivo para inspeccionar: `-KeepOpen`; los logs quedan en
`C:\ODW-origen\data\node.out` y el resultado en `C:\ODW-origen\result.json`.

---

## 3. Verificación manual (sin el script)

```powershell
# Estado del destino (autenticado). health es solo loopback por diseño: 404 desde la tailnet.
$token = Get-Content C:\ODW-kit\peer.token -Raw
Invoke-RestMethod http://<IP-TAILNET-CASA>:7421/peer/v1/status `
  -Headers @{ Authorization = "Bearer $token"; 'X-Odw-Node' = 'PC-OFICINA' } |
  ConvertTo-Json -Depth 6
```

En casa, para vigilar:

```powershell
Get-Service OdwNode
Get-NetTCPConnection -LocalPort 7421 -State Listen
Get-WinEvent -LogName Application -MaxEvents 30 |
  Where-Object ProviderName -match 'Odw' | Select-Object TimeCreated, Message
```

RDP (ya habilitado) por la tailnet: `mstsc /v:<IP-TAILNET-CASA>`.

---

## 4. Si algo falla

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| `tailscale ping` no responde | PC casa dormida, apagada o Tailscale caído | Revisar energía/Tailscale; RDP no ayuda si está dormida |
| `401` en `/peer/v1/status` | Token distinto o `X-Odw-Node` no registrado | Usar el `peer.token` del kit; el `node_id` debe ser `PC-OFICINA` |
| Timeout al proponer | Firewall de casa o listener en otra IP | Revisar la regla `ODW peer API (Tailscale, 7421)` y `Get-NetTCPConnection` |
| Tarea en `WaitingApproval` sin avanzar | El pipe no es accesible para tu usuario | El kit añade tu SID al descriptor; comprobar `whoami /user` |
| `Done` pero resumen vacío | El modelo del agente no respondió | Ver logs del destino; `network_isolation` solo permite `opencode.ai:443` |
| Servicio caído tras reinicio | Arrancó antes que Tailscale | Tiene dependencia + reinicio automático; si persiste, `Start-Service OdwNode` |
| `POLICY_DENIED` | El peer no es `trusted` o el límite no coincide | Revisar `policy.json` del destino (`peer = PC-OFICINA`, `network = none`) |

Evidencia para el HANDOFF: guarda la salida de consola, `result.json` y las últimas líneas de
`node.out`. Si falla, no borres `C:\ODW-origen`.

---

## 5. Alcance de esta prueba (honestidad)

- ✅ Dos máquinas reales, dos SAM distintos, dos aprovisionamientos independientes.
- ✅ Autenticación de pares por token compartido sobre la tailnet, no por LAN.
- ✅ WFP, ACLs, `odw-agent`, proxy y servicio Windows reales en el destino.
- ⚠️ El ensayo de esta noche se hizo con el origen en **el mismo host**, contra la IP tailnet:
  valida config, firewall, token, sandbox, modelo y protocolo, pero **no** la red entre dos PCs.
  La prueba de mañana sí añade el salto entre máquinas.
- ⚠️ No valida hardware distinto ni latencia WAN real; queda como evidencia parcial de H1.

---

## 6. Revertir los ajustes de esta noche

```powershell
# Energía
powercfg /change standby-timeout-ac 15
powercfg /change hibernate-timeout-ac 60
powercfg /change disk-timeout-ac 0

# Windows Update (quitar la política de no reinicio)
Remove-Item 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Recurse -Force

# Servicio y regla de firewall
Stop-Service OdwNode
& C:\ODW\publish\odw.exe service uninstall --name OdwNode
Remove-NetFirewallRule -DisplayName 'ODW peer API (Tailscale, 7421)'
```

Los usuarios `odw-agent`/`odw-svc`, secretos DPAPI y filtros WFP se retiran solo si se decide
desmontar el nodo; no los copies a otra PC.

---

## 7. Seguridad

- El token de par es **compartido y bidireccional**: quien lo tenga puede proponer tareas.
  Trátalo como una contraseña; bórralo del USB y del kit cuando termine la prueba.
- `peer_api.bind = tailscale`: el puerto 7421 no se expone en la LAN ni en Internet.
- `/peer/v1/health` solo responde a loopback; la verificación remota es `/peer/v1/status`
  con token.
- La aprobación no existe por HTTP ni por MCP: solo por el named pipe (o Telegram si se
  configura). El agente no puede aprobar sus propias delegaciones.

---

## 8. Después de la prueba

La prueba ya se hizo el 2026-11-26. El propietario decidió mantener el token para más pruebas; esta
limpieza se ejecuta al terminar la campaña.

1. Borra el repositorio puente privado que contiene `kit-origen.zip` **y todo su historial**.
   Borrar solo la rama `main` no elimina el blob del token; si no puedes borrar el repo,
   considéralo comprometido. El kit incluye el token de par.
2. Rota el token en casa, en consola elevada:

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\rotate-peer-token.ps1
   ```

   Genera uno nuevo, reinicia `OdwNode`, verifica `/status` y restaura el anterior si algo
   falla. No imprime el token.
3. Comprueba que el token anterior ya **no** autentica (`401` en `/peer/v1/status`). Si sigue
   funcionando, la rotación no se aplicó: repítela antes de seguir.
4. Actualiza `peer.token` en la PC origen con el archivo nuevo
   (`%PROGRAMDATA%\ODW\secrets\peer-oficina.token`), regenera el kit y vuelve a ejecutar la prueba.
5. Borra el kit y el token del USB y del equipo de la oficina.

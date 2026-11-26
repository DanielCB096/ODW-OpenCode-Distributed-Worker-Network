<#
.SYNOPSIS
    Prueba H1 real: este PC (origen) delega una tarea al nodo destino por Tailscale.

.DESCRIPTION
    Prepara un nodo ORIGEN local (sin sandbox ni servicio), propone una tarea por MCP, la
    aprueba por el named pipe y espera el resultado que el destino devuelve por la API de pares.

    No ejecuta nada en este equipo: el destino es otro nodo. Si se ejecuta junto al kit
    (prepare-origin-kit.ps1), toma odw.exe, el token de par y los identificadores de kit.json.
    No requiere .NET, Git ni permisos de administrador: el pipe se crea con una ACE para el
    usuario actual.

.PARAMETER WorkDir
    Directorio de trabajo del origen. Por defecto C:\ODW-origen.

.PARAMETER KeepOpen
    No detiene el nodo al terminar (util para inspeccionar).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File h1-remote-test.ps1
#>
[CmdletBinding()]
param(
    [string]$WorkDir = 'C:\ODW-origen',
    [string]$OdwExe,
    [string]$KitFile,
    [string]$NodeId,
    [string]$TargetNodeId,
    [string]$TargetAddress,
    [string]$PeerTokenFile,
    [string]$RepoName = 'demo',
    [int]$PeerPort = 17431,
    [int]$McpPort = 17432,
    [string]$PipeName = 'odw-approvals',
    [switch]$WriteTask,
    [switch]$KeepOpen,
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ------------------------------------------------------------------ parametros del kit

if (-not $KitFile) {
    $candidate = Join-Path $PSScriptRoot 'kit.json'
    if (Test-Path $candidate) { $KitFile = $candidate }
}

if ($KitFile -and (Test-Path $KitFile)) {
    $kit = Get-Content -Path $KitFile -Raw | ConvertFrom-Json
    if (-not $NodeId) { $NodeId = $kit.node_id }
    if (-not $TargetNodeId) { $TargetNodeId = $kit.target_node_id }
    if (-not $TargetAddress) { $TargetAddress = $kit.target_address }
    if (-not $PeerTokenFile) {
        $tokenCandidate = Join-Path (Split-Path $KitFile -Parent) 'peer.token'
        if (Test-Path $tokenCandidate) { $PeerTokenFile = $tokenCandidate }
    }
}

if (-not $OdwExe) {
    $candidate = Join-Path $PSScriptRoot 'odw.exe'
    if (Test-Path $candidate) { $OdwExe = $candidate }
}

$missing = @()
foreach ($pair in @(
        @('OdwExe', $OdwExe),
        @('NodeId', $NodeId),
        @('TargetNodeId', $TargetNodeId),
        @('TargetAddress', $TargetAddress),
        @('PeerTokenFile', $PeerTokenFile))) {
    if ([string]::IsNullOrWhiteSpace($pair[1])) { $missing += $pair[0] }
}
if ($missing.Count -gt 0) {
    throw "Faltan parametros: $($missing -join ', '). Ejecuta desde la carpeta del kit o pasalos a mano."
}
if (-not (Test-Path $OdwExe)) { throw "No existe odw.exe en $OdwExe." }
if (-not (Test-Path $PeerTokenFile)) { throw "No existe el token de par en $PeerTokenFile." }

$OdwExe = [IO.Path]::GetFullPath($OdwExe)
$WorkDir = [IO.Path]::GetFullPath($WorkDir)

# ------------------------------------------------------------------ utilidades

function Write-JsonFile([string]$Path, $Value) {
    $json = $Value | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($Path, $json, (New-Object Text.UTF8Encoding($false)))
}

function Wait-Health([int]$Port, [int]$Seconds = 30) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/peer/v1/health" -TimeoutSec 2
            if ($response.status -eq 'ok') { return }
        }
        catch {
        }
        Start-Sleep -Milliseconds 500
    }
    throw "El nodo en el puerto $Port no respondio a /peer/v1/health."
}

function Invoke-Mcp([string]$Url, [string]$Token, [int]$Id, [string]$Method, $Params) {
    $body = [ordered]@{
        jsonrpc = '2.0'
        id      = $Id
        method  = $Method
    }
    if ($null -ne $Params) { $body.params = $Params }

    return Invoke-RestMethod -Method Post -Uri $Url -TimeoutSec 60 `
        -Headers @{ Authorization = "Bearer $Token" } `
        -ContentType 'application/json' `
        -Body ($body | ConvertTo-Json -Depth 12)
}

function Stop-Tree([string]$Root) {
    Get-CimInstance Win32_Process -Filter "Name='odw.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($Root) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Get-CimInstance Win32_Process -Filter "Name='cmd.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($Root) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

# ------------------------------------------------------------------ configuracion del origen

$dataDir = Join-Path $WorkDir 'data'
$repoDir = Join-Path $WorkDir 'repo'
New-Item -ItemType Directory -Force -Path $dataDir, $repoDir | Out-Null
Stop-Tree $WorkDir

$localPeerToken = Join-Path $dataDir 'peer.token'
Copy-Item $PeerTokenFile $localPeerToken -Force
$mcpTokenPath = Join-Path $dataDir 'mcp.token'
if (-not (Test-Path $mcpTokenPath)) {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
    Set-Content -Path $mcpTokenPath -Value ([Convert]::ToBase64String($bytes)) -Encoding ASCII -NoNewline
}
$mcpToken = (Get-Content -Path $mcpTokenPath -Raw).Trim()

$policyPath = Join-Path $dataDir 'policy.json'
Write-JsonFile $policyPath ([ordered]@{
        version = 1
        default = 'ASK'
        rules   = @()
    })

# El pipe debe permitir al usuario actual aunque el token no este elevado.
$userSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$pipeSddl = "D:(A;;GA;;;BA)(A;;GA;;;SY)(A;;GA;;;$userSid)"

$configPath = Join-Path $dataDir 'node.json'
Write-JsonFile $configPath ([ordered]@{
        node_id     = $NodeId
        data_dir    = $dataDir
        peer_api    = [ordered]@{ bind = '127.0.0.1'; port = $PeerPort; allow_loopback = $true }
        mcp         = [ordered]@{ bind = '127.0.0.1'; port = $McpPort; token_ref = "file:$mcpTokenPath" }
        policy      = $policyPath
        scheduler   = [ordered]@{ enabled = $false; max_concurrent = 1; poll_seconds = 2 }
        poller      = [ordered]@{ enabled = $true; interval_seconds = 3 }
        approvals   = [ordered]@{
            enabled             = $true
            pipe_name           = $PipeName
            ttl_minutes         = 15
            security_descriptor = $pipeSddl
        }
        capabilities = @('git')
        accepts      = @('analysis')
        workspaces   = [ordered]@{ root = (Join-Path $WorkDir 'workspaces'); keep = $false }
        opencode     = [ordered]@{ executable = $OdwExe; pinned_version = '2.3.2' }
        repos        = @([ordered]@{ name = $RepoName; path = $repoDir; base_ref = 'main' })
        peers        = @([ordered]@{
                node_id      = $TargetNodeId
                address      = $TargetAddress
                token_ref    = "file:$localPeerToken"
                trust        = 'trusted'
                capabilities = @('git', 'opencode')
            })
        sandbox      = [ordered]@{
            agent_account     = 'odw-agent'
            apply_acls        = $false
            run_as_agent      = $false
            network_isolation = $false
        }
    })

# ------------------------------------------------------------------ arrancar origen

$logPath = Join-Path $dataDir 'node.out'
$scriptPath = Join-Path $dataDir 'run-origin.cmd'
@"
@echo off
"$OdwExe" serve --config "$configPath" > "$logPath" 2>&1
"@ | Set-Content -Path $scriptPath -Encoding ASCII

Write-Host "== Nodo origen $NodeId (local) =="
$origin = Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', $scriptPath -WindowStyle Hidden -PassThru
$succeeded = $false

try {
    Wait-Health $PeerPort
    $mcpUrl = "http://127.0.0.1:$McpPort/mcp"
    Invoke-Mcp $mcpUrl $mcpToken 1 'initialize' ([ordered]@{ protocolVersion = '2025-06-18' }) | Out-Null

    # Alcance real al destino: mismo camino (tailnet + token + X-Odw-Node) que usara el POST.
    $peerToken = (Get-Content -Path $PeerTokenFile -Raw).Trim()
    $hello = Invoke-RestMethod -Uri "$($TargetAddress.TrimEnd('/'))/peer/v1/hello" -TimeoutSec 15 `
        -Headers @{ Authorization = "Bearer $peerToken"; 'X-Odw-Node' = $NodeId }
    Write-Host "Destino $TargetNodeId alcanzable: protocolo $($hello.protocol_version), opencode $($hello.opencode_version)."

    $mode = if ($WriteTask) { 'read_write_worktree' } else { 'read_only' }
    $instructions = if ($WriteTask) {
        'Crea el archivo src/remoto.txt con el texto HOLA. Responde exactamente: E2E-REMOTE-WRITE.'
    }
    else {
        'Responde exactamente: E2E-REMOTE. No uses herramientas.'
    }

    Write-Host "== Proponiendo tarea ($mode) a $TargetNodeId =="
    $proposal = Invoke-Mcp $mcpUrl $mcpToken 3 'tools/call' ([ordered]@{
            name      = 'odw_propose_task'
            arguments = [ordered]@{
                target_node   = $TargetNodeId
                kind          = 'analysis'
                mode          = $mode
                repo          = $RepoName
                base_ref      = 'main'
                instructions  = $instructions
                allowed_paths = @('src/', 'tests/')
                denied_paths  = @()
                limits        = [ordered]@{
                    max_minutes   = 10
                    max_memory_gb = 4
                    max_processes = 16
                    network       = 'none'
                    max_cost_usd  = 2
                }
            }
        })

    $proposed = $proposal.result.structuredContent
    if ($null -eq $proposed) { throw "MCP no devolvio structuredContent: $($proposal | ConvertTo-Json -Depth 8)" }
    if ($proposed.PSObject.Properties.Match('code').Count -gt 0) {
        throw "Propuesta rechazada: $($proposed.code) $($proposed.message)"
    }

    $taskId = $proposed.task_id
    Write-Host "Tarea: $taskId ($($proposed.state))"

    $approvalId = $null
    if ($proposed.PSObject.Properties.Match('approval_id').Count -gt 0) {
        $approvalId = $proposed.approval_id
    }

    if (-not $approvalId) {
        $deadline = (Get-Date).AddSeconds(20)
        while ((Get-Date) -lt $deadline -and -not $approvalId) {
            $listJson = & $OdwExe approvals list --pipe $PipeName --json 2>$null
            if ($LASTEXITCODE -eq 0 -and $listJson) {
                $list = $listJson | ConvertFrom-Json
                $match = @($list.data) | Where-Object { $_.task_id -eq $taskId } | Select-Object -First 1
                if ($match) { $approvalId = $match.approval_id }
            }
            if (-not $approvalId) { Start-Sleep -Milliseconds 500 }
        }
    }

    if (-not $approvalId) { throw "No se creo la aprobacion para $taskId." }

    Write-Host "== Aprobando por el pipe $PipeName =="
    & $OdwExe approvals approve $approvalId --by "cli:$env:USERNAME" --pipe $PipeName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "La aprobacion fallo (codigo $LASTEXITCODE)." }

    Write-Host "== Esperando resultado (max ${TimeoutSeconds}s) =="
    $terminal = @('Done', 'Failed', 'Rejected', 'Cancelled', 'Expired')
    $state = ''
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $status = Invoke-Mcp $mcpUrl $mcpToken 4 'tools/call' ([ordered]@{
                name      = 'odw_get_task_status'
                arguments = [ordered]@{ task_id = $taskId }
            })
        $state = $status.result.structuredContent.state
        Write-Host "  estado: $state"
        if ($terminal -contains $state) { break }
        Start-Sleep -Seconds 3
    }

    if ($state -ne 'Done') {
        throw "La tarea termino en $state. Logs del origen: $logPath"
    }

    $result = Invoke-Mcp $mcpUrl $mcpToken 5 'tools/call' ([ordered]@{
            name      = 'odw_get_task_result'
            arguments = [ordered]@{ task_id = $taskId; include_diff = [bool]$WriteTask }
        })
    $final = $result.result.structuredContent

    $summaryPath = Join-Path $WorkDir 'result.json'
    $final | ConvertTo-Json -Depth 12 | Set-Content -Path $summaryPath -Encoding UTF8

    if ($WriteTask) {
        $changed = @($final.changed_paths)
        if ($changed -notcontains 'src/remoto.txt') {
            throw "No se registro src/remoto.txt. Cambios: $($changed -join ', ')"
        }
        if ([string]::IsNullOrWhiteSpace($final.commit)) {
            throw "No se creo el commit de la tarea."
        }
        if ($final.diff -notmatch 'remoto') {
            throw "El diff no contiene el archivo esperado."
        }
    }
    elseif ($final.summary_md -notmatch 'E2E-REMOTE') {
        throw "Resultado inesperado: $($final.summary_md)"
    }

    Write-Host ""
    Write-Host "PRUEBA OK"
    Write-Host "  tarea:     $taskId"
    Write-Host "  estado:    $state"
    Write-Host "  resumen:   $($final.summary_md)"
    if ($WriteTask) {
        Write-Host "  paths:     $(@($final.changed_paths) -join ', ')"
        Write-Host "  commit:    $($final.commit)"
    }
    Write-Host "  resultado: $summaryPath"
    $succeeded = $true
}
catch {
    Write-Host ""
    Write-Host "PRUEBA FALLIDA: $($_.Exception.Message)" -ForegroundColor Red
    if (Test-Path $logPath) {
        Write-Host "--- ultimas lineas del nodo origen ---"
        Get-Content $logPath -Tail 30
    }
}
finally {
    if (-not $KeepOpen) {
        if ($origin) { Stop-Process -Id $origin.Id -Force -ErrorAction SilentlyContinue }
        Stop-Tree $WorkDir
    }
    else {
        Write-Host "Nodo origen en ejecucion (PID $($origin.Id)). Log: $logPath"
    }
}

if (-not $succeeded) { exit 1 }

<#
.SYNOPSIS
    Arranca o detiene un nodo ORIGEN persistente con la fachada MCP de ODW.

.DESCRIPTION
    Crea <WorkDir>\data con node.json, policy.json, mcp.token y peer.token, arranca
    `odw serve` en segundo plano y deja el MCP en 127.0.0.1:<McpPort>. El origen no
    ejecuta tareas: solo propone y consulta al nodo destino.

    La aprobacion sigue siendo humana por el named pipe. El token MCP no se imprime.

.PARAMETER WorkDir
    Directorio de trabajo del origen. Por defecto %USERPROFILE%\ODW-origen.

.PARAMETER PeerTokenFile
    Token de par del destino. Obligatorio al arrancar.

.PARAMETER TargetAddress
    URL de la API de pares del destino, por ejemplo http://<IP-TAILNET-CASA>:7421.

.PARAMETER Stop
    Detiene el origen que usa este WorkDir.

.PARAMETER Status
    Comprueba si el origen responde; no arranca ni detiene nada.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\origin-node.ps1 `
        -PeerTokenFile C:\ruta\peer.token -TargetAddress http://<IP-TAILNET-CASA>:7421

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\origin-node.ps1 -Stop
#>
[CmdletBinding()]
param(
    [string]$WorkDir = (Join-Path $env:USERPROFILE 'ODW-origen'),
    [string]$OdwExe,
    [string]$PeerTokenFile,
    [string]$TargetNodeId = 'PC-CASA',
    [string]$TargetAddress,
    [string]$RepoName = 'demo',
    [int]$PeerPort = 17431,
    [int]$McpPort = 7422,
    [string]$PipeName = 'odw-approvals',
    [switch]$Stop,
    [switch]$Status
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$WorkDir = [IO.Path]::GetFullPath($WorkDir)
$dataDir = Join-Path $WorkDir 'data'
$repoDir = Join-Path $WorkDir 'repo'
$configPath = Join-Path $dataDir 'node.json'
$logPath = Join-Path $dataDir 'node.out'
$pidPath = Join-Path $WorkDir 'origin.pid'
$infoPath = Join-Path $WorkDir 'origin.json'

function Write-JsonFile([string]$Path, $Value) {
    $json = $Value | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($Path, $json, (New-Object Text.UTF8Encoding($false)))
}

function Get-OriginProcesses {
    Get-CimInstance Win32_Process -Filter "Name='odw.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($WorkDir) }
}

function Stop-Origin {
    foreach ($process in @(Get-OriginProcesses)) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }
    Get-CimInstance Win32_Process -Filter "Name='cmd.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($WorkDir) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Remove-Item $pidPath -Force -ErrorAction SilentlyContinue
}

function Test-OriginHealth {
    try {
        $response = Invoke-RestMethod -Uri "http://127.0.0.1:$PeerPort/peer/v1/health" -TimeoutSec 2
        return $response.status -eq 'ok'
    }
    catch {
        return $false
    }
}

function Wait-Health([int]$Seconds = 30) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-OriginHealth) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "El origen no respondio a /peer/v1/health en 127.0.0.1:$PeerPort."
}

if ($Stop) {
    Stop-Origin
    Write-Host "Origen detenido ($WorkDir)."
    return
}

if ($Status) {
    $healthy = Test-OriginHealth
    Write-Host "Origen $(if ($healthy) { 'OK' } else { 'NO responde' }): http://127.0.0.1:$PeerPort/peer/v1/health"
    if (Test-Path $infoPath) { Get-Content $infoPath -Raw -Encoding UTF8 }
    return
}

if ([string]::IsNullOrWhiteSpace($PeerTokenFile) -or -not (Test-Path $PeerTokenFile)) {
    throw "Falta -PeerTokenFile con el token de par del destino."
}
if ([string]::IsNullOrWhiteSpace($TargetAddress)) {
    throw "Falta -TargetAddress con la URL de pares del destino (p. ej. http://<IP-TAILNET-CASA>:7421)."
}
if ([string]::IsNullOrWhiteSpace($OdwExe)) {
    $candidate = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\Odw.Cli\bin\Debug\net10.0\odw.exe'
    if (Test-Path $candidate) {
        # Copia estable fuera del repo: evita bloquear bin\Debug durante los builds.
        $sourceDir = Split-Path $candidate -Parent
        $runDir = Join-Path $WorkDir 'bin'
        New-Item -ItemType Directory -Force -Path $runDir | Out-Null
        Copy-Item (Join-Path $sourceDir '*') $runDir -Recurse -Force
        $OdwExe = Join-Path $runDir 'odw.exe'
    }
}
if ([string]::IsNullOrWhiteSpace($OdwExe) -or -not (Test-Path $OdwExe)) {
    throw "No existe odw.exe. Indica -OdwExe o compila src/Odw.Cli."
}
$OdwExe = [IO.Path]::GetFullPath($OdwExe)

New-Item -ItemType Directory -Force -Path $dataDir, $repoDir | Out-Null
Stop-Origin

$localPeerToken = Join-Path $dataDir 'peer.token'
if ([IO.Path]::GetFullPath($PeerTokenFile) -ne [IO.Path]::GetFullPath($localPeerToken)) {
    Copy-Item $PeerTokenFile $localPeerToken -Force
}

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

$userSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$pipeSddl = "D:(A;;GA;;;BA)(A;;GA;;;SY)(A;;GA;;;$userSid)"

Write-JsonFile $configPath ([ordered]@{
        node_id     = 'PC-OFICINA'
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

$errorLogPath = Join-Path $dataDir 'node.err'
$origin = Start-Process -FilePath $OdwExe `
    -ArgumentList @('serve', '--config', $configPath) `
    -WorkingDirectory $WorkDir `
    -RedirectStandardOutput $logPath `
    -RedirectStandardError $errorLogPath `
    -WindowStyle Hidden -PassThru
$origin.Id | Set-Content -Path $pidPath -Encoding ASCII
try {
    Wait-Health
}
catch {
    if (Test-Path $logPath) { Get-Content $logPath -Tail 20 }
    if (Test-Path $errorLogPath) { Get-Content $errorLogPath -Tail 20 }
    throw
}

$info = [ordered]@{
    node_id         = 'PC-OFICINA'
    work_dir        = $WorkDir
    mcp_url         = "http://127.0.0.1:$McpPort/mcp"
    mcp_token_path  = $mcpTokenPath
    pipe_name       = $PipeName
    target_node_id  = $TargetNodeId
    target_address  = $TargetAddress
    peer_health_url = "http://127.0.0.1:$PeerPort/peer/v1/health"
    log_path        = $logPath
    error_log_path  = $errorLogPath
    pid             = $origin.Id
}
Write-JsonFile $infoPath $info

Write-Host "Origen arrancado en $WorkDir."
Write-Host "  MCP:       http://127.0.0.1:$McpPort/mcp"
Write-Host "  Token MCP: $mcpTokenPath (no se imprime)"
Write-Host "  Pipe:      $PipeName"
Write-Host "  Destino:   $TargetNodeId ($TargetAddress)"
Write-Host "  Log:       $logPath"
Write-Host "  Info:      $infoPath"

#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Deja este PC listo como nodo DESTINO de ODW, encendido y alcanzable por Tailscale.

.DESCRIPTION
    Ejecuta, en una consola ELEVADA y de forma idempotente:

      1. tools/provision-sandbox.ps1 (usuarios odw-agent/odw-svc, DPAPI, ACLs, WFP, opencode).
      2. Publica odw.exe en <InstallDir>\publish.
      3. Genera secretos locales (token de par compartido y token MCP) si faltan.
      4. Escribe node.json y policy.json para el nodo destino.
      5. Crea el repositorio de prueba <InstallDir>\demo-repo (rama main).
      6. ACLs de <InstallDir>: administradores/SYSTEM/usuario F, odw-svc RX; el agente no entra.
      7. Regla de firewall para el puerto de pares SOLO en la interfaz Tailscale.
      8. Instala el servicio Windows como .\odw-svc, dependiente de Tailscale, con reinicio
         automatico ante fallo.
      9. Energia: sin suspension ni hibernacion en corriente alterna.
     10. Windows Update: sin reinicio automatico y horas activas ampliadas.
     11. Verifica servicio, listener en la IP tailnet, salud local y sandbox.

    No imprime ni copia secretos a otro equipo. El token de par se escribe en
    <DataDir>\secrets\peer-oficina.token para que prepare-origin-kit.ps1 lo empaquete.

.PARAMETER NodeId
    node_id de este nodo (destino). Por defecto PC-CASA.

.PARAMETER OriginNodeId
    node_id del nodo origen que podra delegar tareas. Por defecto PC-OFICINA.

.PARAMETER OriginAddress
    Direccion del origen (informativa; el destino no inicia conexiones).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/prepare-destination-node.ps1
#>
[CmdletBinding()]
param(
    [string]$NodeId = 'PC-CASA',
    [string]$OriginNodeId = 'PC-OFICINA',
    [string]$OriginAddress = 'http://100.124.89.57:7421',
    [string]$InstallDir = 'C:\ODW',
    [string]$DataDir = (Join-Path $env:ProgramData 'ODW'),
    [string]$RepoName = 'demo',
    [string]$ServiceName = 'OdwNode',
    [string]$OpenCodeSource,
    [string]$AgentModel = 'opencode/deepseek-v4.1-flash-free',
    [int]$PeerPort = 7421,
    [switch]$SkipNetwork,
    [switch]$SkipPower,
    [switch]$SkipService,
    [switch]$SkipProvision,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ------------------------------------------------------------------ utilidades

function Assert-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Se requiere una consola elevada. Abre PowerShell 'Como administrador'."
    }
}

function Invoke-Icacls([string[]]$Arguments) {
    $output = & icacls.exe @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "icacls fallo: $($output -join ' ')"
    }
}

function New-Token {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
    return [Convert]::ToBase64String($bytes)
}

function Write-JsonFile([string]$Path, $Value) {
    $json = $Value | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($Path, $json, (New-Object Text.UTF8Encoding($false)))
}

function Get-TailscaleIp {
    $cli = Get-Command tailscale.exe -ErrorAction SilentlyContinue
    if ($cli) {
        $ip = (& $cli.Source ip -4 2>$null | Select-Object -First 1)
        if ($ip) { return $ip.Trim() }
    }
    foreach ($network in [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
        if ($network.OperationalStatus -ne 'Up') { continue }
        if ($network.Name -notmatch 'tailscale' -and $network.Description -notmatch 'tailscale') { continue }
        foreach ($address in $network.GetIPProperties().UnicastAddresses) {
            if ($address.Address.AddressFamily -eq 'InterNetwork') { return $address.Address.ToString() }
        }
    }
    return $null
}

function Stop-OdwService {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -eq $service) { return }
    if ($service.Status -ne 'Stopped') {
        & sc.exe stop $ServiceName | Out-Null
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
}

Assert-Admin

$repoRoot = Split-Path $PSScriptRoot -Parent
$InstallDir = [IO.Path]::GetFullPath($InstallDir)
$DataDir = [IO.Path]::GetFullPath($DataDir)
$publishDir = Join-Path $InstallDir 'publish'
$configPath = Join-Path $InstallDir 'node.json'
$policyPath = Join-Path $InstallDir 'policy.json'
$repoPath = Join-Path $InstallDir 'demo-repo'
$secretsDir = Join-Path $DataDir 'secrets'
$peerTokenPath = Join-Path $secretsDir 'peer-oficina.token'
$mcpTokenPath = Join-Path $secretsDir 'mcp.token'
$serviceSecret = Join-Path $DataDir 'service.secret'
$agentSecret = Join-Path $DataDir 'agent.secret'
$agentConfig = Join-Path $DataDir 'agent-config\opencode.json'
$networkState = Join-Path $DataDir 'network.state'
$openCodeExe = Join-Path $DataDir 'opencode\opencode.exe'

Write-Host "== ODW destino: $NodeId =="
$tailnetIp = Get-TailscaleIp
if (-not $tailnetIp) { throw "Tailscale no reporta IP. Arranca Tailscale antes de preparar el nodo." }
Write-Host "IP tailnet: $tailnetIp"

if (-not (Test-Path $openCodeExe)) {
    if (-not $OpenCodeSource) {
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA 'Programs\ai.opencode.desktop\resources\opencode-cli.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\ai.opencode.desktop\OpenCode.exe')
        )
        $OpenCodeSource = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $OpenCodeSource) {
        throw "No se encontro opencode-cli.exe. Indica -OpenCodeSource <ruta>."
    }
    $version = (& $OpenCodeSource --version 2>$null | Select-Object -First 1)
    Write-Host "OpenCode origen: $OpenCodeSource ($version)"
}

# ------------------------------------------------------------------ 1. sandbox

Write-Host ""
Write-Host "== Sandbox (usuarios, DPAPI, ACLs, WFP) =="
Stop-OdwService

$provision = Join-Path $PSScriptRoot 'provision-sandbox.ps1'
if (-not $SkipProvision) {
    $provisionArgs = @{
        DataDir        = $DataDir
        AgentModel     = $AgentModel
        OpenCodeSource = $OpenCodeSource
    }
    if ($SkipNetwork) { $provisionArgs['SkipNetwork'] = $true }
    & $provision @provisionArgs
    if ($LASTEXITCODE -ne 0) { throw "provision-sandbox.ps1 termino con codigo $LASTEXITCODE." }
}
else {
    Write-Host "Omitido (-SkipProvision)."
}

# ------------------------------------------------------------------ 2. publicar

Write-Host ""
Write-Host "== Publicando odw =="
New-Item -ItemType Directory -Force -Path $publishDir, $InstallDir | Out-Null
$odwExe = Join-Path $publishDir 'odw.exe'
if (-not $SkipPublish) {
    $cliProject = Join-Path $repoRoot 'src\Odw.Cli\Odw.Cli.csproj'
    & dotnet publish $cliProject -c Release -o $publishDir -r win-x64 --self-contained false --nologo |
        Select-Object -Last 2
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $odwExe)) {
        throw "No se pudo publicar odw.exe."
    }
}
else {
    Write-Host "Omitido (-SkipPublish)."
}

# ------------------------------------------------------------------ 3. secretos

Write-Host ""
Write-Host "== Secretos locales =="
New-Item -ItemType Directory -Force -Path $secretsDir | Out-Null
if (-not (Test-Path $peerTokenPath)) {
    Set-Content -Path $peerTokenPath -Value (New-Token) -Encoding ASCII -NoNewline
    Write-Host "Token de par generado."
}
else {
    Write-Host "Token de par existente; se conserva."
}
if (-not (Test-Path $mcpTokenPath)) {
    Set-Content -Path $mcpTokenPath -Value (New-Token) -Encoding ASCII -NoNewline
    Write-Host "Token MCP generado."
}

# El directorio de datos ya tiene ACL restrictiva; reforzamos secretos y denegamos al agente.
Invoke-Icacls @($secretsDir, '/inheritance:r',
    '/grant:r', '*S-1-5-32-544:(OI)(CI)F',
    '/grant:r', '*S-1-5-18:(OI)(CI)F',
    '/grant:r', 'odw-svc:(OI)(CI)R',
    '/deny', 'odw-agent:(OI)(CI)(RX)') 

# ------------------------------------------------------------------ 4. configuracion

Write-Host ""
Write-Host "== Configuracion del nodo =="
$nodeConfig = [ordered]@{
    node_id    = $NodeId
    data_dir   = $DataDir
    peer_api   = [ordered]@{
        # IPv4 explicita: bind="tailscale" puede resolver la IPv6 y dejar fuera a los clientes IPv4.
        address        = $tailnetIp
        port           = $PeerPort
        allow_loopback = $false
    }
    mcp        = [ordered]@{
        bind      = '127.0.0.1'
        port      = 7422
        token_ref = "file:$mcpTokenPath"
    }
    opencode   = [ordered]@{
        executable     = $openCodeExe
        pinned_version = '2.3.2'
        use_job_object = $true
    }
    workspaces = [ordered]@{
        root = (Join-Path $DataDir 'workspaces')
        keep = $false
    }
    repos      = @(
        [ordered]@{
            name     = $RepoName
            path     = $repoPath
            base_ref = 'main'
        }
    )
    policy     = $policyPath
    capabilities = @('git', 'opencode')
    accepts      = @('analysis')
    scheduler    = [ordered]@{ enabled = $true; max_concurrent = 1; poll_seconds = 2 }
    poller       = [ordered]@{ enabled = $false; interval_seconds = 5 }
    approvals    = [ordered]@{ enabled = $false; pipe_name = 'odw-approvals'; ttl_minutes = 15 }
    peers        = @(
        [ordered]@{
            node_id      = $OriginNodeId
            address      = $OriginAddress
            token_ref    = "file:$peerTokenPath"
            trust        = 'trusted'
            capabilities = @('git')
        }
    )
    sandbox      = [ordered]@{
        agent_account         = 'odw-agent'
        apply_acls            = $true
        run_as_agent          = $true
        agent_secret_path     = $agentSecret
        network_isolation     = $true
        network_allowed_hosts = @('opencode.ai')
        network_proxy_port    = 7423
        network_state_path    = $networkState
        runtime_root          = (Join-Path $DataDir 'runtime')
        agent_config_path     = $agentConfig
    }
}
Write-JsonFile $configPath $nodeConfig

$policy = [ordered]@{
    version = 1
    default = 'ASK'
    rules   = @(
        [ordered]@{
            id       = 'deny-unknown-peer'
            priority = 5
            effect   = 'DENY'
            when     = [ordered]@{ peer_trust = @('blocked', 'unknown') }
            reason   = 'Par no autorizado'
        },
        [ordered]@{
            id       = 'auto-origin-analysis'
            priority = 30
            effect   = 'AUTO'
            when     = [ordered]@{
                direction  = 'in'
                kind       = @('analysis')
                mode       = @('read_only', 'read_write_worktree')
                peer       = @($OriginNodeId)
                peer_trust = 'trusted'
                limits     = [ordered]@{ network = 'none' }
            }
            reason   = "Analisis sin red desde $OriginNodeId"
        }
    )
}
Write-JsonFile $policyPath $policy
Write-Host "node.json y policy.json escritos."

# ------------------------------------------------------------------ 5. repo de prueba

Write-Host ""
Write-Host "== Repositorio de prueba =="
if (-not (Test-Path (Join-Path $repoPath '.git'))) {
    New-Item -ItemType Directory -Force -Path (Join-Path $repoPath 'src'), (Join-Path $repoPath 'tests') | Out-Null
    & git -C $repoPath init -q -b main
    & git -C $repoPath config user.email odw@example.com
    & git -C $repoPath config user.name ODW
    Set-Content -Path (Join-Path $repoPath 'src\readme.txt') -Value 'Hola desde el repositorio de prueba de ODW.' -Encoding UTF8
    Set-Content -Path (Join-Path $repoPath 'tests\placeholder.txt') -Value 'ok' -Encoding UTF8
    & git -C $repoPath add .
    & git -C $repoPath commit -qm 'init'
    if ($LASTEXITCODE -ne 0) { throw "No se pudo crear el repositorio de prueba." }
    Write-Host "Creado $repoPath (rama main)."
}
else {
    Write-Host "Repositorio existente; se conserva."
}

# ------------------------------------------------------------------ 6. ACL de C:\ODW

Write-Host ""
Write-Host "== ACL de $InstallDir =="
$me = [Security.Principal.WindowsIdentity]::GetCurrent().Name
Invoke-Icacls @($InstallDir, '/inheritance:r',
    '/grant:r', '*S-1-5-32-544:(OI)(CI)F',
    '/grant:r', '*S-1-5-18:(OI)(CI)F',
    '/grant:r', "${me}:(OI)(CI)F",
    '/grant:r', 'odw-svc:(OI)(CI)RX')
Write-Host "Servicio RX; el agente no tiene acceso a $InstallDir."

# ------------------------------------------------------------------ 7. firewall

Write-Host ""
Write-Host "== Firewall =="
$ruleName = "ODW peer API (Tailscale, $PeerPort)"
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($null -eq $existing) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow `
        -Protocol TCP -LocalPort $PeerPort -InterfaceAlias 'Tailscale' -Profile Private | Out-Null
    Write-Host "Regla creada: TCP $PeerPort solo en la interfaz Tailscale."
}
else {
    Write-Host "Regla existente; se conserva."
}

# ------------------------------------------------------------------ 8. servicio

if (-not $SkipService) {
    Write-Host ""
    Write-Host "== Servicio $ServiceName =="
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        & $odwExe service install --config $configPath --exe $odwExe `
            --account '.\odw-svc' --secret $serviceSecret --name $ServiceName
        if ($LASTEXITCODE -ne 0) { throw "No se pudo instalar el servicio (codigo $LASTEXITCODE)." }
    }
    else {
        Write-Host "Servicio ya instalado; se reutiliza."
    }

    # Arrancar despues de Tailscale y recuperarse de un fallo (p. ej. carrera al arrancar).
    & sc.exe config $ServiceName 'depend=' 'Tailscale' | Out-Null
    & sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null

    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq 'Running') {
        # Recargar la configuracion recien escrita.
        Restart-Service -Name $ServiceName -Force
    }
    else {
        Start-Service $ServiceName
    }
    (Get-Service -Name $ServiceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(60))
}

# ------------------------------------------------------------------ 9-10. energia y actualizaciones

if (-not $SkipPower) {
    Write-Host ""
    Write-Host "== Energia =="
    & powercfg /change standby-timeout-ac 0
    & powercfg /change hibernate-timeout-ac 0
    & powercfg /change disk-timeout-ac 0
    & powercfg /change monitor-timeout-ac 10
    Write-Host "Sin suspension ni hibernacion en AC."

    Write-Host ""
    Write-Host "== Windows Update =="
    $au = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU'
    New-Item -Path $au -Force | Out-Null
    Set-ItemProperty -Path $au -Name NoAutoRebootWithLoggedOnUsers -Value 1 -Type DWord
    Set-ItemProperty -Path $au -Name AUOptions -Value 4 -Type DWord
    Set-ItemProperty -Path $au -Name AlwaysAutoRebootAtScheduledTime -Value 0 -Type DWord
    $ux = 'HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings'
    New-Item -Path $ux -Force | Out-Null
    Set-ItemProperty -Path $ux -Name ActiveHoursStart -Value 7 -Type DWord
    Set-ItemProperty -Path $ux -Name ActiveHoursEnd -Value 1 -Type DWord
    Set-ItemProperty -Path $ux -Name SmartActiveHoursState -Value 0 -Type DWord
    Write-Host "Sin reinicio automatico; horas activas 07:00-01:00."
}

Write-Host ""
Write-Host "== BitLocker =="
try {
    $bl = Get-BitLockerVolume -MountPoint 'C:' -ErrorAction Stop
    Write-Host "Estado: $($bl.VolumeStatus) / proteccion: $($bl.ProtectionStatus)"
    if ($bl.ProtectionStatus -eq 'On') {
        $bl.KeyProtector | ForEach-Object { Write-Host "  protector: $($_.KeyProtectorType)" }
        if ($bl.KeyProtector.KeyProtectorType -contains 'TpmPin') {
            Write-Warning "BitLocker usa PIN: un reinicio inesperado dejara el equipo sin arrancar hasta que alguien lo teclee."
        }
    }
}
catch {
    Write-Warning "No se pudo consultar BitLocker: $($_.Exception.Message)"
}

# ------------------------------------------------------------------ 11. verificacion

Write-Host ""
Write-Host "== Verificacion =="
Start-Sleep -Seconds 2

if (-not $SkipService) {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -eq $service -or $service.Status -ne 'Running') {
        Write-Warning "El servicio no esta en ejecucion; revisando eventos..."
        Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddMinutes(-10) } `
            -MaxEvents 40 -ErrorAction SilentlyContinue |
            Where-Object { $_.ProviderName -match 'Odw|\.NET|Application Error' } |
            Select-Object -First 8 |
            ForEach-Object { Write-Host "  [$($_.TimeCreated)] $($_.ProviderName): $($_.Message)" }
        throw "El servicio $ServiceName no arranco."
    }
    Write-Host "Servicio: Running"
}

$deadline = (Get-Date).AddSeconds(20)
do {
    $listen = Get-NetTCPConnection -LocalPort $PeerPort -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -eq $tailnetIp }
    if (-not $listen) { Start-Sleep -Milliseconds 500 }
} while (-not $listen -and (Get-Date) -lt $deadline)

if (-not $listen) {
    $all = Get-NetTCPConnection -LocalPort $PeerPort -State Listen -ErrorAction SilentlyContinue
    $where = if ($all) { $all.LocalAddress -join ', ' } else { 'ninguna direccion' }
    throw "El peer API no escucha en ${tailnetIp}:$PeerPort (escucha en: $where)."
}
Write-Host "Listener: ${tailnetIp}:$PeerPort"

# Verificacion autenticada como el origen: ejercita firewall, bind, token y registro del par.
$peerToken = (Get-Content -Path $peerTokenPath -Raw).Trim()
$deadline = (Get-Date).AddSeconds(20)
$status = $null
do {
    try {
        $status = Invoke-RestMethod -Uri "http://${tailnetIp}:$PeerPort/peer/v1/status" -TimeoutSec 10 `
            -Headers @{ Authorization = "Bearer $peerToken"; 'X-Odw-Node' = $OriginNodeId }
    }
    catch {
        $status = $null
        Start-Sleep -Milliseconds 500
    }
} while ($null -eq $status -and (Get-Date) -lt $deadline)

if ($null -eq $status) {
    throw "Fallo /peer/v1/status por tailnet como $OriginNodeId (revisa token, firewall y logs del servicio)."
}
Write-Host "Peer API por tailnet: OK (autenticado como $OriginNodeId)"
Write-Host ($status | ConvertTo-Json -Compress -Depth 4)

& $odwExe sandbox check --agent odw-agent 2>&1 | Select-Object -Last 12
& $odwExe sandbox net status --agent odw-agent 2>&1 | Select-Object -Last 3

Write-Host ""
Write-Host "== Listo =="
Write-Host "  nodo:      $NodeId"
Write-Host "  peer API:  http://${tailnetIp}:$PeerPort"
Write-Host "  config:    $configPath"
Write-Host "  servicio:  $ServiceName (.\\odw-svc, dependiente de Tailscale)"
Write-Host "  token:     $peerTokenPath (no lo copies por chat)"

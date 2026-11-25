#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Provisiona el sandbox de ODW (H3): usuario odw-agent, secreto DPAPI, ACLs y firewall.

.DESCRIPTION
    Debe ejecutarse en una consola ELEVADA. Crea:
      - usuario local odw-agent con contraseña aleatoria (no se imprime),
      - secreto DPAPI de máquina en <DataDir>\agent.secret (solo admins/SYSTEM),
      - ACL del workspace raíz (servicio F, agente RX, sin herencia),
      - copia de opencode.exe accesible al agente y su opencode.json con un modelo gratuito,
      - regla de firewall que intenta bloquear la salida de red del agente (no soportada en
        todas las versiones de Windows).
    No lanza procesos ni modifica la base de datos.

.PARAMETER AgentAccount
    Cuenta local del agente. Por defecto odw-agent.

.PARAMETER AgentModel
    Modelo por defecto del agente. Por defecto el gratuito
    opencode/deepseek-v4.1-flash-free (no requiere credenciales del usuario).

.PARAMETER DataDir
    Directorio de datos de ODW. Por defecto %PROGRAMDATA%\ODW.

.PARAMETER WorkspaceRoot
    Raíz de workspaces. Por defecto <DataDir>\workspaces.

.PARAMETER OpenCodeSource
    Ruta de opencode.exe a copiar para el agente. Si falta, se busca en el PATH.

.PARAMETER SkipFirewall
    No crea la regla de bloqueo de red.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/provision-sandbox.ps1
#>
[CmdletBinding()]
param(
    [string]$AgentAccount = 'odw-agent',
    [string]$DataDir = (Join-Path $env:ProgramData 'ODW'),
    [string]$WorkspaceRoot,
    [string]$OpenCodeSource,
    [string]$AgentModel = 'opencode/deepseek-v4.1-flash-free',
    [switch]$SkipFirewall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Se requiere una consola elevada (token sin filtrar). Cierra OpenCode y ábrelo 'Como administrador'."
    }
}

function Test-Account([string]$Name) {
    try {
        $null = ([Security.Principal.NTAccount]::new($Name)).Translate([Security.Principal.SecurityIdentifier])
        return $true
    }
    catch { return $false }
}

function Invoke-Icacls([string[]]$Arguments) {
    $output = & icacls.exe @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "icacls falló: $($output -join ' ')"
    }
    return $output
}

Assert-Admin

if (-not $WorkspaceRoot) { $WorkspaceRoot = Join-Path $DataDir 'workspaces' }
$DataDir = [IO.Path]::GetFullPath($DataDir)
$WorkspaceRoot = [IO.Path]::GetFullPath($WorkspaceRoot)
New-Item -ItemType Directory -Force -Path $DataDir, $WorkspaceRoot | Out-Null

Write-Host "== Agente $AgentAccount =="
$secretPath = Join-Path $DataDir 'agent.secret'
$password = $null

if (Test-Account $AgentAccount) {
    Write-Host "El usuario ya existe; no se cambia su contraseña."
    if (-not (Test-Path $secretPath)) {
        throw "El usuario existe pero falta $secretPath. Recrea la cuenta o el secreto."
    }
}
else {
    $alphabet = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%*-_'
    $password = -join (1..48 | ForEach-Object { $alphabet[(Get-Random -Maximum $alphabet.Length)] })
    $secure = ConvertTo-SecureString $password -AsPlainText -Force

    New-LocalUser -Name $AgentAccount -Password $secure `
        -PasswordNeverExpires -UserMayNotChangePassword -AccountNeverExpires `
        -Description 'ODW sandbox agent (sin privilegios)' | Out-Null

    Add-Type -AssemblyName System.Security
    $bytes = [Text.Encoding]::UTF8.GetBytes($password)
    $protected = [Security.Cryptography.ProtectedData]::Protect(
        $bytes, $null, [Security.Cryptography.DataProtectionScope]::LocalMachine)
    [IO.File]::WriteAllBytes($secretPath, $protected)

    Invoke-Icacls @($secretPath, '/inheritance:r',
        '/grant:r', '*S-1-5-32-544:F',
        '/grant:r', '*S-1-5-18:F') | Out-Null

    Write-Host "Usuario creado y secreto DPAPI guardado."
}

Write-Host "== ACLs del workspace =="
$service = [Security.Principal.WindowsIdentity]::GetCurrent().Name
Invoke-Icacls @($WorkspaceRoot, '/inheritance:r') | Out-Null
Invoke-Icacls @($WorkspaceRoot, '/grant:r', "$($service):(OI)(CI)F",
    '/grant:r', "$($AgentAccount):(OI)(CI)RX", '/T') | Out-Null
Write-Host "Raíz con herencia deshabilitada: servicio F, agente RX."

Write-Host "== OpenCode compartido =="
if (-not $OpenCodeSource) {
    $shim = Get-Command opencode.cmd -ErrorAction SilentlyContinue
    if ($shim) {
        $candidate = Join-Path (Split-Path $shim.Source) 'node_modules\@opencode\cli\bin\opencode.exe'
        if (Test-Path $candidate) { $OpenCodeSource = $candidate }
    }
}

if ($OpenCodeSource) {
    $openDir = Join-Path $DataDir 'opencode'
    New-Item -ItemType Directory -Force -Path $openDir | Out-Null
    $openExe = Join-Path $openDir 'opencode.exe'
    Copy-Item $OpenCodeSource $openExe -Force
    Invoke-Icacls @($openExe, '/inheritance:r',
        '/grant:r', "$($service):F",
        '/grant:r', "$($AgentAccount):RX") | Out-Null
    Write-Host "OpenCode para el agente: $openExe"
}
else {
    Write-Warning "No se encontró opencode.exe. Copia el binario a $DataDir\opencode y ajusta opencode.executable."
}

Write-Host "== Configuración de OpenCode del agente =="
Add-Type -AssemblyName System.Security
if (-not $password) {
    $protected = [IO.File]::ReadAllBytes($secretPath)
    $password = [Text.Encoding]::UTF8.GetString(
        [Security.Cryptography.ProtectedData]::Unprotect(
            $protected, $null, [Security.Cryptography.DataProtectionScope]::LocalMachine))
}
$secureAgent = ConvertTo-SecureString $password -AsPlainText -Force
$agentCredentials = [PSCredential]::new($AgentAccount, $secureAgent)

# Fuerza la creación del perfil (LoadUserProfile) antes de escribir su configuración.
Start-Service seclogon -ErrorAction SilentlyContinue
Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', 'exit' `
    -Credential $agentCredentials -WindowStyle Hidden -Wait

$agentSid = ([Security.Principal.NTAccount]::new($AgentAccount)).Translate(
    [Security.Principal.SecurityIdentifier]).Value
$agentProfile = Get-CimInstance Win32_UserProfile -Filter "SID='$agentSid'" |
    Select-Object -ExpandProperty LocalPath
if (-not $agentProfile) { $agentProfile = Join-Path $env:SystemDrive "Users\$AgentAccount" }

$agentConfigDir = Join-Path $agentProfile '.config\opencode'
New-Item -ItemType Directory -Force -Path $agentConfigDir | Out-Null
$agentConfig = Join-Path $agentConfigDir 'opencode.json'

$agentSettings = [ordered]@{
    '$schema' = 'https://opencode.ai/config.json'
    model     = $AgentModel
}
$agentJson = $agentSettings | ConvertTo-Json -Compress
[IO.File]::WriteAllText($agentConfig, $agentJson, (New-Object Text.UTF8Encoding($false)))
Invoke-Icacls @($agentConfigDir, '/inheritance:r',
    '/grant:r', "$($AgentAccount):(OI)(CI)F",
    '/grant:r', "$($service):(OI)(CI)F") | Out-Null
Invoke-Icacls @($agentConfig, '/inheritance:r',
    '/grant:r', "$($AgentAccount):F",
    '/grant:r', "$($service):F") | Out-Null
Write-Host "Agente configurado con modelo gratuito '$AgentModel' (sin credenciales del usuario)."

if (-not $SkipFirewall) {
    Write-Host "== Firewall =="
    $ruleName = 'ODW-Agent-Block-Out'
    Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule -ErrorAction SilentlyContinue

    try {
        New-NetFirewallRule -DisplayName $ruleName -Direction Out -Action Block `
            -LocalUser $AgentAccount -Profile Any `
            -Description 'ODW: bloquea la red del agente de sandbox' `
            -ErrorAction Stop | Out-Null
        Write-Host "Regla '$ruleName' creada (salida bloqueada para $AgentAccount)."
    }
    catch {
        Write-Warning "Esta versión de Windows no admite firewall por usuario (-LocalUser)."
        Write-Warning "La red del agente NO está aislada todavía; hace falta WFP o WSL/AppContainer."
    }
}

Write-Host ""
Write-Host "Provisionado. Verifica con:"
Write-Host "  odw sandbox check --agent $AgentAccount"
Write-Host "El secreto está en $secretPath (DPAPI máquina)."

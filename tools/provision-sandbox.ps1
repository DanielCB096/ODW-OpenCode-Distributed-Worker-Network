#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Provisiona el sandbox de ODW (H3): usuario odw-agent, secreto DPAPI, ACLs y firewall.

.DESCRIPTION
    Debe ejecutarse en una consola ELEVADA. Crea:
      - usuario local odw-agent con contraseña aleatoria (no se imprime),
      - secreto DPAPI de máquina en <DataDir>\agent.secret (solo admins/SYSTEM),
      - ACL del workspace raíz (servicio F, agente RX, sin herencia),
      - regla de firewall que bloquea la salida de red del agente.
    No lanza procesos ni modifica la base de datos.

.PARAMETER AgentAccount
    Cuenta local del agente. Por defecto odw-agent.

.PARAMETER DataDir
    Directorio de datos de ODW. Por defecto %PROGRAMDATA%\ODW.

.PARAMETER WorkspaceRoot
    Raíz de workspaces. Por defecto <DataDir>\workspaces.

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
Invoke-Icacls @($WorkspaceRoot, '/inheritance:r',
    '/grant:r', "$($service):(OI)(CI)F",
    '/grant:r', "$($AgentAccount):(OI)(CI)RX",
    '/T') | Out-Null
Write-Host "Raíz con herencia deshabilitada: servicio F, agente RX."

if (-not $SkipFirewall) {
    Write-Host "== Firewall =="
    $ruleName = 'ODW-Agent-Block-Out'
    Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule
    New-NetFirewallRule -DisplayName $ruleName -Direction Out -Action Block `
        -LocalUser $AgentAccount -Profile Any `
        -Description 'ODW: bloquea la red del agente de sandbox' | Out-Null
    Write-Host "Regla '$ruleName' creada (salida bloqueada para $AgentAccount)."
}

Write-Host ""
Write-Host "Provisionado. Verifica con:"
Write-Host "  odw sandbox check --agent $AgentAccount"
Write-Host "El secreto está en $secretPath (DPAPI máquina)."

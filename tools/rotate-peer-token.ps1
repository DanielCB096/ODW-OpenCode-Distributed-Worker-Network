#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Rota el token de par del nodo destino y reinicia el servicio.

.DESCRIPTION
    Genera un token nuevo, guarda el anterior como .bak en el mismo directorio protegido,
    reinicia OdwNode y verifica /peer/v1/status con el token nuevo. Si la verificacion falla,
    restaura el token anterior y reinicia.

    El token NO se imprime. Despues de rotarlo hay que actualizar el peer.token de la PC
    origen y reiniciar su nodo (o volver a ejecutar h1-remote-test.ps1 con el archivo nuevo).

    Usalo cuando el token haya estado en un repo privado, un USB o cualquier medio temporal.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/rotate-peer-token.ps1
#>
[CmdletBinding()]
param(
    [string]$DataDir = (Join-Path $env:ProgramData 'ODW'),
    [string]$PeerTokenPath,
    [string]$ServiceName = 'OdwNode',
    [string]$PeerNodeId = 'PC-OFICINA',
    [int]$PeerPort = 7421
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Se requiere una consola elevada: el token vive en un directorio protegido y hay que reiniciar el servicio."
}

if (-not $PeerTokenPath) {
    $PeerTokenPath = Join-Path $DataDir 'secrets\peer-oficina.token'
}
$PeerTokenPath = [IO.Path]::GetFullPath($PeerTokenPath)
if (-not (Test-Path $PeerTokenPath)) {
    throw "No existe el token actual en $PeerTokenPath."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
    throw "No existe el servicio $ServiceName."
}

$tailnetIp = $null
$tailscale = Get-Command tailscale.exe -ErrorAction SilentlyContinue
if ($tailscale) {
    $tailnetIp = (& $tailscale.Source ip -4 2>$null | Select-Object -First 1)
    if ($tailnetIp) { $tailnetIp = $tailnetIp.Trim() }
}
if (-not $tailnetIp) {
    throw "Tailscale no reporta IPv4; no se puede verificar el nodo."
}

function New-Token {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
    return [Convert]::ToBase64String($bytes)
}

function Restart-NodeService {
    Restart-Service -Name $ServiceName -Force
    (Get-Service -Name $ServiceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(60))
}

function Test-PeerStatus([string]$Token) {
    $deadline = (Get-Date).AddSeconds(20)
    do {
        try {
            $status = Invoke-RestMethod -Uri "http://${tailnetIp}:$PeerPort/peer/v1/status" -TimeoutSec 10 `
                -Headers @{ Authorization = "Bearer $Token"; 'X-Odw-Node' = $PeerNodeId }
            if ($status.node_id) { return $true }
        }
        catch {
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    return $false
}

$backupPath = "$PeerTokenPath.bak"
Copy-Item $PeerTokenPath $backupPath -Force
$previous = (Get-Content $PeerTokenPath -Raw).Trim()

$newToken = New-Token
[IO.File]::WriteAllText($PeerTokenPath, $newToken, (New-Object Text.UTF8Encoding($false)))
Write-Host "Token nuevo escrito; reiniciando $ServiceName..."

try {
    Restart-NodeService
    if (-not (Test-PeerStatus $newToken)) {
        throw "El nodo no respondio con el token nuevo."
    }
    Write-Host "Token rotado y verificado (${PeerNodeId} -> ${tailnetIp}:$PeerPort)."
    Write-Host ""
    Write-Host "Falta el origen:"
    Write-Host "  - actualiza peer.token en la PC origen con el contenido de:"
    Write-Host "      $PeerTokenPath"
    Write-Host "  - o vuelve a copiar el kit y ejecuta h1-remote-test.ps1."
    Write-Host "El token NO se imprime: leelo desde el archivo en una consola elevada."
}
catch {
    Write-Warning "Rotacion fallida ($($_.Exception.Message)); restaurando el token anterior."
    [IO.File]::WriteAllText($PeerTokenPath, $previous, (New-Object Text.UTF8Encoding($false)))
    Restart-NodeService
    throw "Se restauro el token anterior; revisa el servicio y los logs."
}

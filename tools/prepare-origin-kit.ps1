<#
.SYNOPSIS
    Empaqueta el kit para ejecutar la prueba H1 desde la PC del trabajo (nodo origen).

.DESCRIPTION
    Genera <OutDir> con:
      - odw.exe autocontenido (win-x64, sin .NET ni Git en la PC del trabajo),
      - h1-remote-test.ps1,
      - kit.json (node_id del origen, destino y direccion de la API de pares),
      - peer.token (token compartido leido del nodo destino local),
      - RUNBOOK-DOS-PCS.md.

    Y comprime todo en <OutDir>.zip. El token no se imprime en consola.

.PARAMETER OutDir
    Carpeta del kit. Por defecto C:\ODW\kit-origen.

.PARAMETER TargetAddress
    Direccion de la API de pares del nodo destino (este PC). Por defecto la IP tailnet local.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/prepare-origin-kit.ps1
#>
[CmdletBinding()]
param(
    [string]$OutDir = 'C:\ODW\kit-origen',
    [string]$NodeId = 'PC-OFICINA',
    [string]$TargetNodeId = 'PC-CASA',
    [string]$TargetAddress,
    [string]$PeerTokenFile = (Join-Path $env:ProgramData 'ODW\secrets\peer-oficina.token'),
    [int]$PeerPort = 7421
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-TailscaleIp {
    $cli = Get-Command tailscale.exe -ErrorAction SilentlyContinue
    if ($cli) {
        $ip = (& $cli.Source ip -4 2>$null | Select-Object -First 1)
        if ($ip) { return $ip.Trim() }
    }
    return $null
}

if (-not $TargetAddress) {
    $ip = Get-TailscaleIp
    if (-not $ip) { throw "Tailscale no reporta IP; indica -TargetAddress." }
    $TargetAddress = "http://${ip}:$PeerPort"
}

if (-not (Test-Path $PeerTokenFile)) {
    throw "Falta $PeerTokenFile. Ejecuta tools/prepare-destination-node.ps1 primero."
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$OutDir = [IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "== Publicando odw autocontenido =="
& dotnet publish (Join-Path $repoRoot 'src\Odw.Cli\Odw.Cli.csproj') `
    -c Release -o $OutDir -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --nologo |
    Select-Object -Last 2
if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $OutDir 'odw.exe'))) {
    throw "No se pudo publicar odw.exe autocontenido."
}

Copy-Item (Join-Path $PSScriptRoot 'h1-remote-test.ps1') $OutDir -Force
Copy-Item (Join-Path $repoRoot 'docs\RUNBOOK-DOS-PCS.md') $OutDir -Force
Copy-Item $PeerTokenFile (Join-Path $OutDir 'peer.token') -Force

$kit = [ordered]@{
    node_id        = $NodeId
    target_node_id = $TargetNodeId
    target_address = $TargetAddress
    pipe_name      = 'odw-approvals'
    created_utc    = (Get-Date).ToUniversalTime().ToString('o')
}
$kit | ConvertTo-Json | Set-Content -Path (Join-Path $OutDir 'kit.json') -Encoding UTF8

$readme = @"
KIT ODW - PRUEBA H1 DESDE LA PC DEL TRABAJO
===========================================

1. Descomprime este kit en C:\ODW-kit (o ejecutalo desde el USB).
2. Comprueba que Tailscale esta arriba:  tailscale ping $($TargetAddress -replace '^http://', '' -replace ':\d+$', '')
3. Ejecuta:
       powershell -ExecutionPolicy Bypass -File .\h1-remote-test.ps1
   Variante de escritura:
       powershell -ExecutionPolicy Bypass -File .\h1-remote-test.ps1 -WriteTask

No necesitas .NET, Git ni permisos de administrador.
Destino: $TargetNodeId en $TargetAddress
Origen local: $NodeId (127.0.0.1:17431 / 17432)

El token de par esta en peer.token. No lo compartas por chat ni correo.
"@
Set-Content -Path (Join-Path $OutDir 'LEEME.txt') -Value $readme -Encoding UTF8

$zip = "$OutDir.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $OutDir '*') -DestinationPath $zip -CompressionLevel Optimal

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "== Kit listo =="
Write-Host "  carpeta: $OutDir"
Write-Host "  zip:     $zip ($size MB)"
Write-Host "  destino: $TargetNodeId $TargetAddress"
Write-Host "  token:   incluido en peer.token (no se imprime)"

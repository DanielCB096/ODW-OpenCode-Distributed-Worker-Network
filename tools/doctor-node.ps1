<#
.SYNOPSIS
    Ejecuta `odw doctor` con los valores reales de un node.json.

.DESCRIPTION
    Lee la configuración del nodo, resuelve el token de fachada por `file:`/`env:`, y llama a
    `odw doctor` con el bind, puertos, OpenCode y spec correctos. No imprime el token ni
    modifica nada. El doctor abre SQLite en solo lectura.

.PARAMETER Config
    Ruta de node.json. Por defecto C:\ODW\node.json (nodo destino).

.PARAMETER OdwExe
    Ejecutable de ODW. Por defecto C:\ODW\publish\odw.exe.

.PARAMETER AllowLoopback
    Acepta loopback en pares y su listener (origen de pruebas).

.PARAMETER Json
    Salida JSON del doctor.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\doctor-node.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\doctor-node.ps1 `
        -Config "$env:USERPROFILE\ODW-origen\data\node.json" -AllowLoopback `
        -OdwExe "src\Odw.Cli\bin\Debug\net10.0\odw.exe"
#>
[CmdletBinding()]
param(
    [string]$Config = 'C:\ODW\node.json',
    [string]$OdwExe = 'C:\ODW\publish\odw.exe',
    [string]$SpecPath,
    [string]$SpecSha256 = '48f6cb36f2cdf9d6fbc9b0a9f1a879d398fb7d5d4fe20718f85faaaf22099973',
    [string]$OpenCode,
    [string]$OpenCodeVersion,
    [switch]$AllowLoopback,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Expand-Value([string]$value) {
    return [Environment]::ExpandEnvironmentVariables($value)
}

function Get-OptionalProperty($object, [string]$name) {
    $property = $object.PSObject.Properties[$name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

if (-not (Test-Path $Config)) {
    throw "No existe la configuración: $Config."
}

if (-not (Test-Path $OdwExe)) {
    throw "No existe odw.exe: $OdwExe. Indica -OdwExe."
}

$node = Get-Content $Config -Raw -Encoding UTF8 | ConvertFrom-Json

$dataDir = Expand-Value ([string]$node.data_dir)
$databasePath = Get-OptionalProperty $node 'database_path'
$database = if ($databasePath) {
    Expand-Value ([string]$databasePath)
}
else {
    Join-Path $dataDir 'odw.db'
}

$peerAddress = Get-OptionalProperty $node.peer_api 'address'
$peerBind = if ($peerAddress) {
    [string]$peerAddress
}
else {
    [string]$node.peer_api.bind
}

$peerPort = [int]$node.peer_api.port
$mcpBind = [string]$node.mcp.bind
$mcpPort = [int]$node.mcp.port
$tokenRef = [string]$node.mcp.token_ref

if ($tokenRef.StartsWith('file:', [StringComparison]::OrdinalIgnoreCase)) {
    $tokenPath = Expand-Value $tokenRef.Substring(5)
    if (-not (Test-Path $tokenPath)) {
        throw "No existe el token de fachada: $tokenPath."
    }

    $token = (Get-Content $tokenPath -Raw).Trim()
}
elseif ($tokenRef.StartsWith('env:', [StringComparison]::OrdinalIgnoreCase)) {
    $token = [Environment]::GetEnvironmentVariable($tokenRef.Substring(4))
}
else {
    throw "mcp.token_ref no es file: ni env:."
}

if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'El token de fachada está vacío.'
}

$opencode = if ($OpenCode) {
    Expand-Value $OpenCode
}
else {
    Expand-Value ([string]$node.opencode.executable)
}

$opencodeVersion = if ($OpenCodeVersion) {
    $OpenCodeVersion
}
else {
    [string]$node.opencode.pinned_version
}

if (-not $SpecPath) {
    $SpecPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'docs\reference\opencode-2.3.2-openapi.json'
}

$arguments = @(
    'doctor',
    '--db', $database,
    '--peer-bind', $peerBind,
    '--peer-port', $peerPort,
    '--mcp-bind', $mcpBind,
    '--mcp-port', $mcpPort,
    '--opencode', $opencode,
    '--opencode-version', $opencodeVersion,
    '--spec', $SpecPath,
    '--spec-sha256', $SpecSha256
)

if ($AllowLoopback) {
    $arguments += '--allow-loopback'
}

if ($Json) {
    $arguments += '--json'
}

$env:ODW_MCP_TOKEN = $token
try {
    & $OdwExe @arguments
    exit $LASTEXITCODE
}
finally {
    Remove-Item Env:ODW_MCP_TOKEN -ErrorAction SilentlyContinue
}

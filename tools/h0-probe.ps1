#requires -Version 5.1
<#
.SYNOPSIS
    Arnés reproducible de H0 para ODW.

.DESCRIPTION
    Verifica contra un OpenCode real:
      H0-3  --port 0 + descubrimiento de URL por stdout y /api/info
      H0-1  un prompt con permiso pendiente queda bloqueado; 'once' lo desbloquea
      H0-4  Permission.Ruleset aplicado al crear la sesión surte efecto
      H0-5  el SSE no usa Last-Event-ID (solo data: + : heartbeat)

    Nunca expone el servidor fuera de 127.0.0.1. El proceso y los artefactos se limpian al final.

.PARAMETER OpenCodeExe
    Ruta al ejecutable/shim de OpenCode. Por defecto resuelve 'opencode' en PATH.

.PARAMETER TimeoutSeconds
    Espera máxima para que aparezca permission.asked.

.PARAMETER KeepArtifacts
    Conserva el directorio temporal con eventos, salidas y mensajes para inspección.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/h0-probe.ps1 -KeepArtifacts
#>
[CmdletBinding()]
param(
    [string]$OpenCodeExe,
    [int]$TimeoutSeconds = 90,
    [switch]$KeepArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step([string]$Text) { Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Pass([string]$Text) { Write-Host "PASS  $Text" -ForegroundColor Green }
function Write-Fail([string]$Text) { Write-Host "FAIL  $Text" -ForegroundColor Red }

$results = [ordered]@{}
$failures = 0
function Assert-True([bool]$Condition, [string]$Name, [string]$Detail = '') {
    if ($Condition) { $results[$Name] = 'PASS'; Write-Pass $Name }
    else { $results[$Name] = 'FAIL'; $failures++; Write-Fail "$Name $Detail" }
}

function Resolve-OpenCode([string]$Explicit) {
    if ($Explicit) {
        if (-not (Test-Path -LiteralPath $Explicit)) { throw "No existe OpenCodeExe: $Explicit" }
        return (Resolve-Path -LiteralPath $Explicit).Path
    }
    foreach ($name in @('opencode.cmd', 'opencode.exe', 'opencode')) {
        $cmd = Get-Command $name -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($cmd) { return $cmd.Source }
    }
    throw 'No se encontró opencode en PATH. Usa -OpenCodeExe.'
}

function Invoke-Curl([string]$Url, [string]$Credential, [string[]]$ExtraArgs = @()) {
    $curlArgs = @('-sS', '-u', $Credential) + $ExtraArgs + @($Url)
    $output = & curl.exe @curlArgs
    if ($LASTEXITCODE -ne 0) { throw "curl falló ($LASTEXITCODE) en $Url" }
    return ($output -join "`n")
}

function Write-JsonFile([string]$Path, [object]$Object) {
    $json = $Object | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText($Path, $json, (New-Object Text.UTF8Encoding($false)))
}

function Wait-ForPattern([string]$Path, [string]$Pattern, [int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $hit = Select-String -LiteralPath $Path -Pattern $Pattern -ErrorAction SilentlyContinue |
                   Select-Object -First 1
            if ($hit) { return $hit }
        }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("odw-h0-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$workspace = Join-Path $work 'ws'
New-Item -ItemType Directory -Force -Path $workspace | Out-Null
$stdout = Join-Path $work 'serve.out'
$stderr = Join-Path $work 'serve.err'
$events = Join-Path $work 'events.txt'
$messagesPath = Join-Path $work 'messages.json'

$serve = $null
$sse = $null
$info = $null
$password = 'h0-' + [guid]::NewGuid().ToString('N')
$credential = "opencode:$password"

try {
    $exe = Resolve-OpenCode $OpenCodeExe
    Write-Step "OpenCode: $exe"
    Write-Step "Directorio de trabajo: $work"

    # ---------- H0-3: arranque con --port 0 ----------
    $env:OPENCODE_SERVER_PASSWORD = $password
    $env:OPENCODE_SERVER_USERNAME = 'opencode'
    $serve = Start-Process -FilePath $exe `
        -ArgumentList @('serve', '--port', '0', '--hostname', '127.0.0.1') `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr

    $urlLine = Wait-ForPattern $stdout 'http://127\.0\.0\.1:(\d+)' 40
    Assert-True ($null -ne $urlLine) 'H0-3 stdout publica la URL'
    if (-not $urlLine) { throw 'El servidor no publicó una URL en stdout.' }

    $baseUrl = [regex]::Match($urlLine.Line, 'http://127\.0\.0\.1:\d+').Value
    Assert-True ($baseUrl -match '^http://127\.0\.0\.1:\d+$') 'H0-3 bind solo loopback' $baseUrl
    Write-Step "URL descubierta: $baseUrl"

    $info = Invoke-Curl "$baseUrl/api/info" $credential | ConvertFrom-Json
    Assert-True ($info.version -match '^\d+\.\d+\.\d+') 'H0-3 /api/info responde con versión' $info.version
    Assert-True ($info.pid -gt 0) 'H0-3 /api/info expone pid' "$($info.pid)"
    Assert-True (($info.urls -contains $baseUrl)) 'H0-3 /api/info confirma la URL' ($info.urls -join ',')

    # ---------- H0-4: sesión con reglas de permiso ----------
    $sessionBody = Join-Path $work 'create-session.json'
    Write-JsonFile $sessionBody @{
        title       = 'ODW H0 probe'
        location    = @{ directory = $workspace }
        permissions = @(@{ action = '*'; resource = '*'; effect = 'ask' })
    }
    $sessionJson = Invoke-Curl "$baseUrl/api/session" $credential @('-H', 'Content-Type: application/json', '--data-binary', "@$sessionBody")
    $session = $sessionJson | ConvertFrom-Json
    $sid = $session.data.id
    Assert-True ([bool]$sid) 'H0-4 sesión creada con Permission.Ruleset' $sid
    Assert-True (@($session.data.permissions).Count -eq 1) 'H0-4 el ruleset se refleja en la sesión'

    # ---------- SSE ----------
    $sse = Start-Process -FilePath 'curl.exe' `
        -ArgumentList @('-sS', '-N', '-u', $credential, "$baseUrl/api/event", '-o', $events) `
        -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 1

    # ---------- H0-1: prompt que requiere permiso ----------
    $promptBody = Join-Path $work 'prompt.json'
    Write-JsonFile $promptBody @{
        text      = 'Usa la herramienta shell para ejecutar exactamente: echo ODW_H0_PROBE. Luego informa la salida. No hagas nada más.'
        delivery  = 'queue'
    }
    $null = Invoke-Curl "$baseUrl/api/session/$sid/prompt" $credential @('-H', 'Content-Type: application/json', '--data-binary', "@$promptBody")

    $askLine = Wait-ForPattern $events '"type":"permission\.asked"' $TimeoutSeconds
    Assert-True ($null -ne $askLine) 'H0-1 permission.asked emitido' "en <= ${TimeoutSeconds}s"
    if (-not $askLine) { throw 'No se emitió permission.asked.' }

    $askJson = ($askLine.Line -replace '^data:\s*', '') | ConvertFrom-Json
    $permissionId = $askJson.data.id
    Assert-True ($askJson.data.action -eq 'shell') 'H0-1 action = shell' $askJson.data.action

    # Sin responder: no debe haber evento terminal ni idle.
    $blockedSeconds = 15
    Start-Sleep -Seconds $blockedSeconds
    $terminal = Select-String -LiteralPath $events -Pattern '"type":"(session\.execution\.(succeeded|failed|interrupted)|session\.idle)"' -ErrorAction SilentlyContinue
    Assert-True (@($terminal).Count -eq 0) "H0-1 sigue bloqueado tras ${blockedSeconds}s"

    $pending = Invoke-Curl "$baseUrl/api/session/$sid/permission" $credential | ConvertFrom-Json
    Assert-True (@($pending.data | Where-Object { $_.id -eq $permissionId }).Count -eq 1) 'H0-1 la solicitud sigue pendiente'

    # ---------- once ----------
    $replyBody = Join-Path $work 'reply.json'
    Write-JsonFile $replyBody @{ decision = 'once' }
    $null = Invoke-Curl "$baseUrl/api/session/$sid/permission/$permissionId/reply" $credential @('-H', 'Content-Type: application/json', '--data-binary', "@$replyBody")
    $done = Wait-ForPattern $events '"type":"session\.execution\.(succeeded|failed)"' 60
    Assert-True ($null -ne $done) 'H0-1 once desbloquea y el turno termina'

    $messages = Invoke-Curl "$baseUrl/api/session/$sid/message" $credential
    [IO.File]::WriteAllText($messagesPath, $messages, (New-Object Text.UTF8Encoding($false)))
    Assert-True ($messages -match 'ODW_H0_PROBE') 'H0-1 el comando aprobado se ejecutó (salida presente)'

    # ---------- H0-5 ----------
    Assert-True ((Select-String -LiteralPath $events -Pattern '^id: ' -Quiet) -eq $false) 'H0-5 el SSE no emite id: (sin Last-Event-ID)'
    Assert-True ((Select-String -LiteralPath $events -Pattern '^: heartbeat' -Quiet) -eq $true) 'H0-5 el SSE emite : heartbeat'

    Write-Host ''
    Write-Host 'Resumen:'
    $results.GetEnumerator() | ForEach-Object { "  {0,-52} {1}" -f $_.Key, $_.Value } | Write-Host
}
catch {
    Write-Fail $_.Exception.Message
    $failures++
}
finally {
    if ($sse -and -not $sse.HasExited) { Stop-Process -Id $sse.Id -Force -ErrorAction SilentlyContinue }
    if ($serve -and -not $serve.HasExited) { Stop-Process -Id $serve.Id -Force -ErrorAction SilentlyContinue }
    if ($info -and $info.pid) { Stop-Process -Id $info.pid -Force -ErrorAction SilentlyContinue }
    Remove-Item Env:OPENCODE_SERVER_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:OPENCODE_SERVER_USERNAME -ErrorAction SilentlyContinue

    if ($KeepArtifacts) { Write-Host "Artefactos en: $work" }
    else { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($failures -gt 0) { exit 1 } else { exit 0 }

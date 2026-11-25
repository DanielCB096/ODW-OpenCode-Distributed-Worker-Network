#Requires -RunAsAdministrator
<#
.SYNOPSIS
    E2E de protocolo ODW en un solo PC: nodo PC-A (origen) + PC-B (destino) por loopback.

.DESCRIPTION
    Arranca dos nodos separados como odw-svc, propone una tarea por MCP, la aprueba por el
    named pipe y espera el resultado. El destino usa el sandbox real (odw-agent, WFP, proxy).
    No sustituye a una prueba en dos PCs: no valida Tailscale ni la red física.

    Requiere haber ejecutado tools/provision-sandbox.ps1 (usuarios, template, WFP y state).

.PARAMETER DataRoot
    Directorio de trabajo del E2E. Por defecto %PROGRAMDATA%\ODW\e2e-loopback.

.PARAMETER OdwExe
    odw.exe ya publicado. Si se omite, se publica en <DataRoot>\publish.

.PARAMETER Keep
    No borra <DataRoot> al terminar (deja logs y configs para inspección).

.PARAMETER TimeoutSeconds
    Espera máxima para la tarea. Por defecto 300.
#>
[CmdletBinding()]
param(
    [string]$DataRoot = (Join-Path $env:ProgramData 'ODW\e2e-loopback'),
    [string]$OdwExe,
    [switch]$Keep,
    [switch]$WriteTask,
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Security

$commonData = $env:ProgramData
$odwRoot = Join-Path $commonData 'ODW'
$serviceSecret = Join-Path $odwRoot 'service.secret'
$agentTemplate = Join-Path $odwRoot 'agent-config\opencode.json'
$networkState = Join-Path $odwRoot 'network.state'
$agentSecret = Join-Path $odwRoot 'agent.secret'
$openCodeExe = Join-Path $odwRoot 'opencode\opencode.exe'

foreach ($required in @($serviceSecret, $agentTemplate, $networkState, $agentSecret, $openCodeExe)) {
    if (-not (Test-Path $required)) {
        throw "Falta $required. Ejecuta tools/provision-sandbox.ps1 antes del E2E."
    }
}

function Invoke-Icacls([string[]]$Arguments) {
    $output = & icacls.exe @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "icacls falló: $($output -join ' ')" }
}

function Stop-Tree([string]$root) {
    if (Test-Path $root) {
        & cmd.exe /c "attrib -r -h /s /d `"$root\*`"" 2>&1 | Out-Null
        & cmd.exe /c "rd /s /q `"$root`"" 2>&1 | Out-Null
    }
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
    throw "El nodo en el puerto $Port no respondió a /peer/v1/health."
}

function Start-Node([string]$Name, [string]$Config, [string]$Log, $Credential) {
    $script = Join-Path (Split-Path $Config -Parent) "run-$Name.cmd"
    @"
@echo off
"$OdwExe" serve --config "$Config" > "$Log" 2>&1
"@ | Set-Content -Path $script -Encoding ASCII

    $process = Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', $script `
        -Credential $Credential -WindowStyle Hidden -PassThru
    return $process
}

function Stop-Node([string]$Root) {
    Get-CimInstance Win32_Process -Filter "Name='odw.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($Root) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

    Get-CimInstance Win32_Process -Filter "Name='opencode.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($openCodeExe, [StringComparison]::OrdinalIgnoreCase) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

    Get-CimInstance Win32_Process -Filter "Name='cmd.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($Root) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

function New-Token {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
    return [Convert]::ToBase64String($bytes)
}

function Write-Json([string]$Path, $Value) {
    $Value | ConvertTo-Json -Depth 12 | Set-Content -Path $Path -Encoding UTF8
}

function Invoke-Mcp([string]$Url, [string]$Token, [int]$Id, [string]$Method, $Params) {
    $body = [ordered]@{
        jsonrpc = '2.0'
        id      = $Id
        method  = $Method
    }
    if ($null -ne $Params) { $body.params = $Params }

    return Invoke-RestMethod -Method Post -Uri $Url -TimeoutSec 30 `
        -Headers @{ Authorization = "Bearer $Token" } `
        -ContentType 'application/json' `
        -Body ($body | ConvertTo-Json -Depth 12)
}

$succeeded = $false
$target = $null
$origin = $null

try {
    if (-not $Keep) {
        Stop-Node $DataRoot
        Stop-Tree $DataRoot
    }
    New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null

    # El agente necesita poder listar/stat hasta la raíz del workspace (git).
    Invoke-Icacls @($DataRoot, '/grant:r', 'odw-agent:(RX)')

    if (-not $OdwExe) {
        $OdwExe = Join-Path $DataRoot 'publish\odw.exe'
        Write-Host "== Publicando odw =="
        & dotnet publish (Join-Path $PSScriptRoot '..\src\Odw.Cli\Odw.Cli.csproj') `
            -c Release -o (Split-Path $OdwExe -Parent) -r win-x64 --self-contained false --nologo |
            Select-Object -Last 2
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $OdwExe)) {
            throw "No se pudo publicar odw.exe."
        }
    }

    $OdwExe = [IO.Path]::GetFullPath($OdwExe)

    $servicePassword = [Text.Encoding]::UTF8.GetString(
        [Security.Cryptography.ProtectedData]::Unprotect(
            [IO.File]::ReadAllBytes($serviceSecret), $null,
            [Security.Cryptography.DataProtectionScope]::LocalMachine))
    $serviceCredential = [PSCredential]::new(
        'odw-svc', (ConvertTo-SecureString $servicePassword -AsPlainText -Force))

    $peerToken = New-Token
    $originMcpToken = New-Token
    $targetMcpToken = New-Token

    $targetDir = Join-Path $DataRoot 'target'
    $originDir = Join-Path $DataRoot 'origin'
    $repoDir = Join-Path $DataRoot 'repo'
    New-Item -ItemType Directory -Force -Path $targetDir, $originDir, $repoDir | Out-Null

    Set-Content -Path (Join-Path $originDir 'peer.token') -Value $peerToken -Encoding ASCII -NoNewline
    Set-Content -Path (Join-Path $targetDir 'peer.token') -Value $peerToken -Encoding ASCII -NoNewline
    Set-Content -Path (Join-Path $originDir 'mcp.token') -Value $originMcpToken -Encoding ASCII -NoNewline
    Set-Content -Path (Join-Path $targetDir 'mcp.token') -Value $targetMcpToken -Encoding ASCII -NoNewline

    & git -C $repoDir init -q
    & git -C $repoDir config user.email e2e@example.com
    & git -C $repoDir config user.name ODW
    New-Item -ItemType Directory -Force -Path (Join-Path $repoDir 'src'), (Join-Path $repoDir 'tests') | Out-Null
    Set-Content -Path (Join-Path $repoDir 'src\readme.txt') -Value 'hola' -Encoding UTF8
    Set-Content -Path (Join-Path $repoDir 'tests\placeholder.txt') -Value 'ok' -Encoding UTF8
    & git -C $repoDir add .
    & git -C $repoDir commit -qm init
    if ($LASTEXITCODE -ne 0) { throw "No se pudo crear el repo de prueba." }

    $targetPolicy = Join-Path $targetDir 'policy.json'
    $taskMode = if ($WriteTask) { 'read_write_worktree' } else { 'read_only' }
    Write-Json $targetPolicy ([ordered]@{
        version = 1
        default = 'ASK'
        rules   = @(
            [ordered]@{
                id       = 'auto-e2e'
                priority = 1
                effect   = 'AUTO'
                when     = [ordered]@{
                    direction  = 'in'
                    kind       = @('analysis')
                    mode       = $taskMode
                    peer_trust = 'trusted'
                    limits     = [ordered]@{ network = 'none' }
                }
                reason   = 'E2E loopback'
            }
        )
    })

    $originPolicy = Join-Path $originDir 'policy.json'
    Write-Json $originPolicy ([ordered]@{
        version = 1
        default = 'ASK'
        rules   = @()
    })

    $targetConfig = Join-Path $targetDir 'node.json'
    Write-Json $targetConfig ([ordered]@{
        node_id     = 'PC-B'
        data_dir    = $targetDir
        peer_api    = [ordered]@{ bind = '127.0.0.1'; port = 17421; allow_loopback = $true }
        mcp         = [ordered]@{
            bind      = '127.0.0.1'
            port      = 17422
            token_ref = "file:$targetDir\mcp.token"
        }
        policy      = $targetPolicy
        scheduler   = [ordered]@{ enabled = $true; max_concurrent = 1; poll_seconds = 1 }
        poller      = [ordered]@{ enabled = $false }
        approvals   = [ordered]@{ enabled = $false }
        capabilities = @('git', 'opencode')
        accepts     = @('analysis')
        workspaces  = [ordered]@{ root = "$targetDir\workspaces"; keep = $false }
        opencode    = [ordered]@{ executable = $openCodeExe; pinned_version = '2.3.2' }
        repos       = @([ordered]@{ name = 'SistemaMaestro'; path = $repoDir; base_ref = 'main' })
        peers       = @([ordered]@{
            node_id      = 'PC-A'
            address      = 'http://127.0.0.1:17431'
            token_ref    = "file:$targetDir\peer.token"
            trust        = 'trusted'
            capabilities = @('git')
        })
        sandbox     = [ordered]@{
            agent_account      = 'odw-agent'
            apply_acls         = $true
            run_as_agent       = $true
            agent_secret_path  = $agentSecret
            network_isolation  = $true
            network_allowed_hosts = @('opencode.ai')
            network_proxy_port = 7423
            network_state_path = $networkState
            runtime_root       = (Join-Path $odwRoot 'runtime')
            agent_config_path  = $agentTemplate
        }
    })

    $originConfig = Join-Path $originDir 'node.json'
    Write-Json $originConfig ([ordered]@{
        node_id     = 'PC-A'
        data_dir    = $originDir
        peer_api    = [ordered]@{ bind = '127.0.0.1'; port = 17431; allow_loopback = $true }
        mcp         = [ordered]@{
            bind      = '127.0.0.1'
            port      = 17432
            token_ref = "file:$originDir\mcp.token"
        }
        policy      = $originPolicy
        scheduler   = [ordered]@{ enabled = $false; max_concurrent = 1 }
        poller      = [ordered]@{ enabled = $true; interval_seconds = 1 }
        approvals   = [ordered]@{
            enabled     = $true
            pipe_name   = 'odw-e2e-approvals'
            ttl_minutes = 15
        }
        capabilities = @('git')
        accepts     = @('analysis')
        workspaces  = [ordered]@{ root = "$originDir\workspaces"; keep = $false }
        opencode    = [ordered]@{ executable = $openCodeExe; pinned_version = '2.3.2' }
        repos       = @([ordered]@{ name = 'SistemaMaestro'; path = $repoDir; base_ref = 'main' })
        peers       = @([ordered]@{
            node_id      = 'PC-B'
            address      = 'http://127.0.0.1:17421'
            token_ref    = "file:$originDir\peer.token"
            trust        = 'trusted'
            capabilities = @('git')
        })
        sandbox     = [ordered]@{
            agent_account  = 'odw-agent'
            apply_acls     = $false
            run_as_agent   = $false
            network_isolation = $false
        }
    })

    Write-Host "== Arrancando PC-B (destino) =="
    $target = Start-Node 'target' $targetConfig (Join-Path $targetDir 'node.out') $serviceCredential
    Wait-Health 17421

    Write-Host "== Arrancando PC-A (origen) =="
    $origin = Start-Node 'origin' $originConfig (Join-Path $originDir 'node.out') $serviceCredential
    Wait-Health 17431

    $mcpUrl = 'http://127.0.0.1:17432/mcp'
    Invoke-Mcp $mcpUrl $originMcpToken 1 'initialize' ([ordered]@{ protocolVersion = '2025-06-18' }) | Out-Null

    $proposal = Invoke-Mcp $mcpUrl $originMcpToken 3 'tools/call' ([ordered]@{
        name      = 'odw_propose_task'
        arguments = [ordered]@{
            target_node     = 'PC-B'
            kind            = 'analysis'
            mode            = $taskMode
            repo            = 'SistemaMaestro'
            base_ref        = 'main'
            instructions    = if ($WriteTask) {
                'Crea el archivo src/e2e-changed.txt con el texto CAMBIO. Responde exactamente: E2E-WRITE.'
            } else {
                'Responde exactamente: E2E-LOOPBACK. No uses herramientas.'
            }
            allowed_paths   = @('src/', 'tests/')
            denied_paths    = @()
            limits          = [ordered]@{
                max_minutes   = 5
                max_memory_gb = 4
                max_processes = 16
                network       = 'none'
                max_cost_usd  = 2
            }
        }
    })

    $proposed = $proposal.result.structuredContent
    if ($null -eq $proposed) { throw "MCP no devolvió structuredContent: $($proposal | ConvertTo-Json -Depth 8)" }
    if ($proposed.PSObject.Properties.Match('code').Count -gt 0) {
        throw "Propuesta rechazada: $($proposed.code) $($proposed.message)"
    }

    $taskId = $proposed.task_id
    Write-Host "Tarea creada: $taskId ($($proposed.state))"

    $approval = $null
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline -and $null -eq $approval) {
        $listJson = & $OdwExe approvals list --pipe odw-e2e-approvals --json
        if ($LASTEXITCODE -eq 0 -and $listJson) {
            $list = $listJson | ConvertFrom-Json
            $approval = @($list.data) | Where-Object { $_.task_id -eq $taskId } | Select-Object -First 1
        }
        if ($null -eq $approval) { Start-Sleep -Milliseconds 500 }
    }

    if ($null -eq $approval) { throw "No apareció la aprobación para $taskId." }

    Write-Host "Aprobando $($approval.approval_id) por el pipe..."
    & $OdwExe approvals approve $approval.approval_id --by cli:e2e --pipe odw-e2e-approvals | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "La aprobación falló." }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $terminal = @('Done', 'Failed', 'Rejected', 'Cancelled', 'Expired')
    $state = ''
    while ((Get-Date) -lt $deadline) {
        $status = Invoke-Mcp $mcpUrl $originMcpToken 4 'tools/call' ([ordered]@{
            name      = 'odw_get_task_status'
            arguments = [ordered]@{ task_id = $taskId }
        })
        $state = $status.result.structuredContent.state
        if ($terminal -contains $state) { break }
        Start-Sleep -Seconds 2
    }

    if ($state -ne 'Done') {
        throw "La tarea terminó en estado $state (ver logs en $DataRoot)."
    }

    $result = Invoke-Mcp $mcpUrl $originMcpToken 5 'tools/call' ([ordered]@{
        name      = 'odw_get_task_result'
        arguments = [ordered]@{ task_id = $taskId; include_diff = $true }
    })
    $final = $result.result.structuredContent
    $summary = $final.summary_md

    if ($WriteTask) {
        $changed = @($final.changed_paths)
        if ($changed -notcontains 'src/e2e-changed.txt') {
            throw "No se registró src/e2e-changed.txt. Cambios: $($changed -join ', ')"
        }

        if ([string]::IsNullOrWhiteSpace($final.commit)) {
            throw "No se creó el commit de la tarea."
        }

        if ($final.diff -notmatch 'e2e-changed') {
            throw "El parche no contiene el archivo esperado."
        }
    }
    elseif ($summary -notmatch 'E2E-LOOPBACK') {
        throw "Resultado inesperado: $summary"
    }

    Write-Host ""
    Write-Host "E2E OK"
    Write-Host "  tarea:  $taskId"
    Write-Host "  estado: $state"
    Write-Host "  result: $summary"

    if ($WriteTask) {
        Write-Host "  paths:  $($changed -join ', ')"
        Write-Host "  commit: $($final.commit)"
    }

    Write-Host "  logs:   $DataRoot"
    $succeeded = $true
}
finally {
    if ($origin) { Stop-Process -Id $origin.Id -Force -ErrorAction SilentlyContinue }
    if ($target) { Stop-Process -Id $target.Id -Force -ErrorAction SilentlyContinue }
    Stop-Node $DataRoot

    if (-not $Keep -and $succeeded) {
        Stop-Tree $DataRoot
    }
    elseif (-not $succeeded) {
        Write-Host "E2E falló; se conservan logs y configuración en $DataRoot." -ForegroundColor Yellow
    }
}

if (-not $succeeded) { exit 1 }

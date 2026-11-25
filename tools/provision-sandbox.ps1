#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Provisiona el sandbox de ODW (H3): usuario odw-agent, secreto DPAPI, ACLs y red.

.DESCRIPTION
    Debe ejecutarse en una consola ELEVADA. Crea:
      - usuario local odw-agent con contraseña aleatoria (no se imprime),
      - usuario local odw-svc con contraseña aleatoria y secreto DPAPI en <DataDir>\service.secret,
      - derechos: odw-svc puede iniciar sesión como servicio y aumentar cuota; odw-agent tiene
        denegados el logon de red y el interactivo remoto (conserva el interactivo local que exige
        CreateProcessWithLogonW),
      - ACL del workspace raíz (servicio/odw-svc/SYSTEM/administradores F, agente RX),
      - copia de opencode.exe accesible al agente y plantilla de opencode.json con modelo gratuito,
      - perfil de ejecución desechable por tarea (<DataDir>\runtime) y perfil real sin escritura,
      - ACL de la raíz de datos (servicio/odw-svc/SYSTEM/administradores; el agente solo traverse)
        y denegación de lectura sobre configuración, bases y claves existentes,
      - filtros WFP persistentes que bloquean la salida del agente salvo 127.0.0.1:7423 y la marca
        network.state para el servicio sin privilegios.
    Detén el nodo antes de ejecutarlo: cambia ACLs de archivos en uso.
    No lanza procesos del nodo ni modifica la base de datos.

.PARAMETER AgentAccount
    Cuenta local del agente. Por defecto odw-agent.

.PARAMETER ServiceAccount
    Cuenta local del servicio. Por defecto odw-svc.

.PARAMETER AgentModel
    Modelo por defecto del agente. Por defecto el gratuito
    opencode/deepseek-v4.1-flash-free (no requiere credenciales del usuario).

.PARAMETER DataDir
    Directorio de datos de ODW. Por defecto %PROGRAMDATA%\ODW.

.PARAMETER WorkspaceRoot
    Raíz de workspaces. Por defecto <DataDir>\workspaces.

.PARAMETER OpenCodeSource
    Ruta de opencode.exe a copiar para el agente. Si falta, se busca en el PATH.

.PARAMETER SkipNetwork
    No instala los filtros WFP (la red del agente queda sin aislar).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/provision-sandbox.ps1
#>
[CmdletBinding()]
param(
    [string]$AgentAccount = 'odw-agent',
    [string]$ServiceAccount = 'odw-svc',
    [string]$DataDir = (Join-Path $env:ProgramData 'ODW'),
    [string]$WorkspaceRoot,
    [string]$OpenCodeSource,
    [string]$AgentModel = 'opencode/deepseek-v4.1-flash-free',
    [switch]$SkipNetwork
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

function Grant-Rights {
    param([hashtable]$Assignments)

    $base = Join-Path $env:TEMP ("odw-secedit-" + [guid]::NewGuid().ToString('N'))
    $inf = "$base.inf"
    $db = "$base.sdb"

    try {
        & secedit /export /cfg $inf /areas USER_RIGHTS | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "secedit /export falló." }

        $lines = @(Get-Content -Path $inf)
        $missing = @()
        $section = -1
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i].Trim() -eq '[Privilege Rights]') { $section = $i; break }
        }

        foreach ($sid in $Assignments.Keys) {
            foreach ($right in $Assignments[$sid]) {
                $index = -1
                for ($i = 0; $i -lt $lines.Count; $i++) {
                    if ($lines[$i] -match "^\s*$([regex]::Escape($right))\s*=") { $index = $i; break }
                }

                $entry = "*$sid"
                if ($index -ge 0) {
                    if ($lines[$index] -notmatch [regex]::Escape($entry)) {
                        $lines[$index] = $lines[$index].TrimEnd() + ",$entry"
                    }
                }
                else {
                    $missing += "$right = $entry"
                }
            }
        }

        if ($missing.Count -gt 0) {
            if ($section -lt 0) { throw "El INF exportado no contiene [Privilege Rights]." }
            $head = if ($section -ge 0) { @($lines[0..$section]) } else { @() }
            $tail = if ($section + 1 -lt $lines.Count) { @($lines[($section + 1)..($lines.Count - 1)]) } else { @() }
            $lines = @($head + $missing + $tail)
        }

        Set-Content -Path $inf -Value $lines -Encoding Unicode
        & secedit /configure /db $db /cfg $inf /areas USER_RIGHTS /quiet | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "secedit /configure falló." }
    }
    finally {
        Remove-Item $inf, $db, "$db.jfm" -Force -ErrorAction SilentlyContinue
    }
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

$agentSid = ([Security.Principal.NTAccount]::new($AgentAccount)).Translate(
    [Security.Principal.SecurityIdentifier]).Value

Write-Host "== Servicio $ServiceAccount =="
$serviceSecretPath = Join-Path $DataDir 'service.secret'

if (Test-Account $ServiceAccount) {
    Write-Host "El usuario de servicio ya existe; no se cambia su contraseña."
    if (-not (Test-Path $serviceSecretPath)) {
        throw "El usuario existe pero falta $serviceSecretPath. Recrea la cuenta o el secreto."
    }
}
else {
    $svcAlphabet = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%*-_'
    $svcPassword = -join (1..48 | ForEach-Object { $svcAlphabet[(Get-Random -Maximum $svcAlphabet.Length)] })
    $svcSecure = ConvertTo-SecureString $svcPassword -AsPlainText -Force

    New-LocalUser -Name $ServiceAccount -Password $svcSecure `
        -PasswordNeverExpires -UserMayNotChangePassword -AccountNeverExpires `
        -Description 'ODW node service (mínimo privilegio)' | Out-Null

    Add-Type -AssemblyName System.Security
    $svcBytes = [Text.Encoding]::UTF8.GetBytes($svcPassword)
    $svcProtected = [Security.Cryptography.ProtectedData]::Protect(
        $svcBytes, $null, [Security.Cryptography.DataProtectionScope]::LocalMachine)
    [IO.File]::WriteAllBytes($serviceSecretPath, $svcProtected)

    Invoke-Icacls @($serviceSecretPath, '/inheritance:r',
        '/grant:r', '*S-1-5-32-544:F',
        '/grant:r', '*S-1-5-18:F') | Out-Null

    Write-Host "Usuario de servicio creado y secreto DPAPI guardado."
}

$serviceSid = ([Security.Principal.NTAccount]::new($ServiceAccount)).Translate(
    [Security.Principal.SecurityIdentifier]).Value
Grant-Rights @{
    $serviceSid = @('SeServiceLogonRight', 'SeIncreaseQuotaPrivilege')
    $agentSid   = @('SeDenyNetworkLogonRight', 'SeDenyRemoteInteractiveLogonRight')
}
Write-Host "Derechos aplicados: servicio puede iniciar sesión; agente sin red ni RDP."

Write-Host "== ACLs del workspace =="
$service = [Security.Principal.WindowsIdentity]::GetCurrent().Name
Invoke-Icacls @($WorkspaceRoot, '/inheritance:r') | Out-Null
Invoke-Icacls @($WorkspaceRoot, '/grant:r', "$($service):(OI)(CI)F",
    '/grant:r', "$($ServiceAccount):(OI)(CI)F",
    '/grant:r', '*S-1-5-18:(OI)(CI)F',
    '/grant:r', '*S-1-5-32-544:(OI)(CI)F',
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
    Invoke-Icacls @($openDir, '/inheritance:r',
        '/grant:r', "$($service):(OI)(CI)F",
        '/grant:r', "$($ServiceAccount):(OI)(CI)RX",
        '/grant:r', "$($AgentAccount):(OI)(CI)RX") | Out-Null
    $openExe = Join-Path $openDir 'opencode.exe'
    Copy-Item $OpenCodeSource $openExe -Force
    Invoke-Icacls @($openExe, '/inheritance:r',
        '/grant:r', "$($service):F",
        '/grant:r', "$($ServiceAccount):RX",
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

# Plantilla de solo lectura para el perfil desechable de cada tarea.
$templateDir = Join-Path $DataDir 'agent-config'
New-Item -ItemType Directory -Force -Path $templateDir | Out-Null
$agentConfigTemplate = Join-Path $templateDir 'opencode.json'
[IO.File]::WriteAllText($agentConfigTemplate, $agentJson, (New-Object Text.UTF8Encoding($false)))
Invoke-Icacls @($templateDir, '/inheritance:r',
    '/grant:r', "$($service):(OI)(CI)F",
    '/grant:r', "$($ServiceAccount):(OI)(CI)RX",
    '/grant:r', "$($AgentAccount):(OI)(CI)RX") | Out-Null
Invoke-Icacls @($agentConfigTemplate, '/inheritance:r',
    '/grant:r', "$($service):F",
    '/grant:r', "$($ServiceAccount):RX",
    '/grant:r', "$($AgentAccount):RX") | Out-Null

# Copia de compatibilidad en el perfil real, sin escritura para el agente.
$agentConfigDir = Join-Path $agentProfile '.config\opencode'
New-Item -ItemType Directory -Force -Path $agentConfigDir | Out-Null
$agentConfig = Join-Path $agentConfigDir 'opencode.json'
[IO.File]::WriteAllText($agentConfig, $agentJson, (New-Object Text.UTF8Encoding($false)))
Invoke-Icacls @($agentConfigDir, '/inheritance:r',
    '/grant:r', "$($service):(OI)(CI)F",
    '/grant:r', "$($AgentAccount):(OI)(CI)RX") | Out-Null
Invoke-Icacls @($agentConfig, '/inheritance:r',
    '/grant:r', "$($service):F",
    '/grant:r', "$($AgentAccount):RX") | Out-Null

# El agente ya no debe escribir datos de OpenCode en su perfil compartido.
foreach ($profileDir in @(
        (Join-Path $agentProfile '.local\share\opencode'),
        (Join-Path $agentProfile '.cache\opencode'))) {
    Remove-Item $profileDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $profileDir | Out-Null
    Invoke-Icacls @($profileDir, '/inheritance:r',
        '/grant:r', "$($service):(OI)(CI)F",
        '/grant:r', "$($AgentAccount):(OI)(CI)RX") | Out-Null
}

$runtimeRoot = Join-Path $DataDir 'runtime'
New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null
Invoke-Icacls @($runtimeRoot, '/inheritance:r',
    '/grant:r', "$($service):(OI)(CI)F",
    '/grant:r', "$($ServiceAccount):(OI)(CI)F",
    '/grant:r', '*S-1-5-18:(OI)(CI)F',
    '/grant:r', '*S-1-5-32-544:(OI)(CI)F',
    '/grant:r', "$($AgentAccount):(X)") | Out-Null

Write-Host "Agente configurado con modelo gratuito '$AgentModel' (plantilla + perfiles por tarea)."

Write-Host "== Protección de datos del nodo =="
# La raíz de datos queda solo para el servicio/SYSTEM/administradores. El agente conserva
# únicamente traverse (X) para llegar a workspaces y opencode, que tienen su propia ACL.
Invoke-Icacls @($DataDir, '/inheritance:r',
    '/grant:r', "$($service):(OI)(CI)F",
    '/grant:r', "$($ServiceAccount):(OI)(CI)F",
    '/grant:r', '*S-1-5-18:(OI)(CI)F',
    '/grant:r', '*S-1-5-32-544:(OI)(CI)F',
    '/grant:r', "$($AgentAccount):(X)") | Out-Null

# El servicio necesita descifrar el secreto del agente para lanzarlo.
Invoke-Icacls @($secretPath, '/grant:r', "$($ServiceAccount):R") | Out-Null

# Denegación explícita en los datos sensibles ya existentes (defensa en profundidad).
Get-ChildItem -Path $DataDir -File -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -like '*.json' -or $_.Name -like '*.db*' -or
        $_.Name -like '*.key' -or $_.Name -like '*.pfx' -or $_.Name -like '*.secret'
    } |
    ForEach-Object {
        Invoke-Icacls @($_.FullName, '/deny', "$($AgentAccount):(R)") | Out-Null
    }

$probe = Join-Path $DataDir 'acl-probe.tmp'
[IO.File]::WriteAllText($probe, 'odw-acl-probe')
try {
    $readProbe = Start-Process -FilePath 'cmd.exe' `
        -ArgumentList '/c', "type `"$probe`" >nul 2>&1" `
        -Credential $agentCredentials -WindowStyle Hidden -PassThru -Wait
    if ($readProbe.ExitCode -eq 0) {
        throw "El agente puede leer ${probe}: la ACL de datos no se aplicó."
    }

    $readConfig = Start-Process -FilePath 'cmd.exe' `
        -ArgumentList '/c', "type `"$agentConfig`" >nul 2>&1" `
        -Credential $agentCredentials -WindowStyle Hidden -PassThru -Wait
    if ($readConfig.ExitCode -ne 0) {
        throw "El agente no puede leer su propia configuración: $agentConfig"
    }
}
finally {
    Remove-Item $probe -Force -ErrorAction SilentlyContinue
}

Write-Host "Verificado: el agente no lee datos del nodo y sí su propia configuración."

if (-not $SkipNetwork) {
    Write-Host "== Red (WFP) =="
    $repo = Split-Path $PSScriptRoot -Parent
    $cli = Join-Path $repo 'src\Odw.Cli\Odw.Cli.csproj'

    if (Test-Path $cli) {
        & dotnet run --project $cli -- sandbox net install --agent $AgentAccount `
            --port 7423 --state (Join-Path $DataDir 'network.state')
        if ($LASTEXITCODE -ne 0) {
            throw "No se pudieron instalar los filtros WFP (código $LASTEXITCODE)."
        }
        Write-Host "Red del agente bloqueada salvo loopback (proxy del modelo)."
    }
    else {
        Write-Warning "No se encontró el proyecto del CLI. Ejecuta 'odw sandbox net install --agent $AgentAccount'."
    }
}

Write-Host ""
Write-Host "Provisionado. Verifica con:"
Write-Host "  odw sandbox check --agent $AgentAccount"
Write-Host "  odw sandbox net status --agent $AgentAccount"
Write-Host "El secreto está en $secretPath (DPAPI máquina)."

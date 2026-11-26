#Requires -Version 5.1
<#
.SYNOPSIS
    Otorga o consulta derechos de logon de una cuenta local usando la API LSA (sin secedit).

.DESCRIPTION
    LsaAddAccountRights es idempotente: repetir la llamada no duplica derechos. Se usa desde
    provision-sandbox.ps1 porque secedit /configure puede fallar o quedar desincronizado en
    algunas instalaciones de Windows 11.

    Derechos usados por ODW:
      - odw-svc:   SeServiceLogonRight, SeIncreaseQuotaPrivilege
      - odw-agent: SeDenyNetworkLogonRight, SeDenyRemoteInteractiveLogonRight

.PARAMETER QueryOnly
    No otorga nada; solo consulta. En Windows 11 la lectura de derechos esta restringida,
    asi que tambien requiere elevacion.

.PARAMETER Verify
    Comprueba que la cuenta tiene exactamente los derechos indicados.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/grant-logon-rights.ps1 `
        -Account odw-svc -Right SeServiceLogonRight,SeIncreaseQuotaPrivilege -Verify

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/grant-logon-rights.ps1 `
        -Account odw-agent -Right SeDenyNetworkLogonRight,SeDenyRemoteInteractiveLogonRight `
        -QueryOnly -Verify
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Account,
    [Parameter(Mandatory)][string[]]$Right,
    [switch]$Verify,
    [switch]$QueryOnly,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Se requiere una consola elevada: Windows 11 restringe la lectura y el alta de derechos de logon."
}

if (-not ('OdwLsaRights' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;

public static class OdwLsaRights
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LSA_UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LSA_OBJECT_ATTRIBUTES
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public int Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    private const uint PolicyViewLocalInformation = 0x00000001;
    private const uint PolicyCreateAccount = 0x00000010;
    private const uint PolicyLookupNames = 0x00000800;
    private const uint StatusObjectNameNotFound = 0xC0000034;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint LsaOpenPolicy(IntPtr systemName, ref LSA_OBJECT_ATTRIBUTES attrs, uint access, out IntPtr policy);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint LsaAddAccountRights(IntPtr policy, byte[] sid, LSA_UNICODE_STRING[] rights, uint count);

    [DllImport("advapi32.dll")]
    private static extern uint LsaEnumerateAccountRights(IntPtr policy, byte[] sid, out IntPtr rights, out uint count);

    [DllImport("advapi32.dll")]
    private static extern uint LsaFreeMemory(IntPtr buffer);

    [DllImport("advapi32.dll")]
    private static extern uint LsaClose(IntPtr policy);

    [DllImport("advapi32.dll")]
    private static extern uint LsaNtStatusToWinError(uint status);

    private static byte[] SidBytes(string account)
    {
        var sid = (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));
        var bytes = new byte[sid.BinaryLength];
        sid.GetBinaryForm(bytes, 0);
        return bytes;
    }

    private static IntPtr Open(uint access)
    {
        var attrs = new LSA_OBJECT_ATTRIBUTES();
        attrs.Length = Marshal.SizeOf(typeof(LSA_OBJECT_ATTRIBUTES));
        IntPtr policy;
        uint status = LsaOpenPolicy(IntPtr.Zero, ref attrs, access, out policy);
        if (status != 0)
        {
            throw new InvalidOperationException(
                "LsaOpenPolicy fallo con Win32 " + LsaNtStatusToWinError(status));
        }
        return policy;
    }

    public static string Grant(string account, string[] rights)
    {
        var sidBytes = SidBytes(account);
        IntPtr policy = Open(PolicyCreateAccount | PolicyLookupNames);

        var handles = new IntPtr[rights.Length];
        var strings = new LSA_UNICODE_STRING[rights.Length];
        try
        {
            for (int i = 0; i < rights.Length; i++)
            {
                handles[i] = Marshal.StringToHGlobalUni(rights[i]);
                strings[i].Buffer = handles[i];
                strings[i].Length = (ushort)(rights[i].Length * 2);
                strings[i].MaximumLength = (ushort)((rights[i].Length + 1) * 2);
            }

            uint status = LsaAddAccountRights(policy, sidBytes, strings, (uint)strings.Length);
            if (status != 0)
            {
                throw new InvalidOperationException(
                    "LsaAddAccountRights fallo con Win32 " + LsaNtStatusToWinError(status));
            }
        }
        finally
        {
            for (int i = 0; i < handles.Length; i++)
            {
                if (handles[i] != IntPtr.Zero) Marshal.FreeHGlobal(handles[i]);
            }
            LsaClose(policy);
        }

        return ((SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier))).Value;
    }

    public static string[] Query(string account)
    {
        var sidBytes = SidBytes(account);
        IntPtr policy = Open(PolicyViewLocalInformation);
        try
        {
            IntPtr buffer;
            uint count;
            uint status = LsaEnumerateAccountRights(policy, sidBytes, out buffer, out count);
            if (status == StatusObjectNameNotFound)
            {
                return new string[0];
            }
            if (status != 0)
            {
                throw new InvalidOperationException(
                    "LsaEnumerateAccountRights fallo con Win32 " + LsaNtStatusToWinError(status));
            }

            try
            {
                var result = new List<string>((int)count);
                int size = Marshal.SizeOf(typeof(LSA_UNICODE_STRING));
                for (uint i = 0; i < count; i++)
                {
                    IntPtr item = new IntPtr(buffer.ToInt64() + (i * size));
                    var str = (LSA_UNICODE_STRING)Marshal.PtrToStructure(item, typeof(LSA_UNICODE_STRING));
                    result.Add(Marshal.PtrToStringUni(str.Buffer, str.Length / 2));
                }
                return result.ToArray();
            }
            finally
            {
                LsaFreeMemory(buffer);
            }
        }
        finally
        {
            LsaClose(policy);
        }
    }
}
'@
}

if ($QueryOnly) {
    $present = [OdwLsaRights]::Query($Account)
    if ($present.Count -eq 0) {
        Write-Host "$Account : sin derechos explicitos."
    }
    else {
        Write-Host "$Account :"
        $present | Sort-Object | ForEach-Object { Write-Host "  $_" }
    }

    if ($Verify) {
        $missing = @($Right | Where-Object { $present -notcontains $_ })
        if ($missing.Count -gt 0) {
            throw "Verificacion fallida para ${Account}: faltan $($missing -join ', ')."
        }
        if (-not $Quiet) { Write-Host "Verificado (LSA) para ${Account}: $($Right -join ', ')" }
    }
    return
}

$sid = [OdwLsaRights]::Grant($Account, $Right)
if (-not $Quiet) {
    Write-Host "Derechos otorgados a $Account ($sid): $($Right -join ', ')"
}

if ($Verify) {
    $present = [OdwLsaRights]::Query($Account)
    $missing = @($Right | Where-Object { $present -notcontains $_ })
    if ($missing.Count -gt 0) {
        throw "Verificacion fallida para ${Account}: faltan $($missing -join ', ')."
    }
    if (-not $Quiet) { Write-Host "Verificado (LSA) para ${Account}: $($Right -join ', ')" }
}

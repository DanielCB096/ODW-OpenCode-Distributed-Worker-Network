using System.Buffers.Binary;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Odw.Sandbox.Windows;

/// <summary>
/// Bloquea la salida de red de una cuenta local mediante filtros WFP persistentes.
/// Excepción: loopback (127.0.0.1 / ::1), para que el agente solo pueda hablar con el
/// proxy local del modelo. Requiere una sesión elevada.
/// </summary>
public static partial class WfpNetworkPolicy
{
    private const uint ErrorSuccess = 0;
    private const uint ErrorAlreadyExists = 0x80320009;
    private const uint ErrorNotFound = 0x80320008;
    private const uint ErrorFilterNotFound = 0x80320003;

    private static readonly Guid ProviderKey = new("7f4c2a10-3b6e-4d21-9a55-0d8c1e2f3a44");
    private static readonly Guid SubLayerKey = new("c1d2e3f4-a5b6-4c7d-8e9f-0123456789ab");
    private static readonly Guid BlockV4Key = new("5d6e7f80-91a2-4b3c-8d4e-5f60718293a4");
    private static readonly Guid BlockV6Key = new("6e7f8091-a2b3-4c4d-9e5f-6071829304b5");
    private static readonly Guid PermitV4Key = new("7f8091a2-b3c4-4d5e-8f60-7182930415c6");
    private static readonly Guid PermitV6Key = new("8091a2b3-c4d5-4e6f-9061-8293041526d7");

    private static readonly Guid LayerConnectV4 = new("c38d57d1-05a7-4c33-904f-7fbceee60e82");
    private static readonly Guid LayerConnectV6 = new("4a72393b-319f-44bc-84c3-ba54dcb3b6b4");
    private static readonly Guid ConditionUserId = new("af043a0a-b34d-4f86-979c-c90371af6e66");
    private static readonly Guid ConditionRemoteAddress = new("b235ae9a-1d64-49b8-a44c-5ff3d9095045");
    private static readonly Guid ConditionRemotePort = new("c35a604d-d22b-4e1a-91b4-68f674ee674b");

    private const uint FilterFlagPersistent = 0x00000001;
    private const uint ProviderFlagPersistent = 0x00000001;
    private const uint SubLayerFlagPersistent = 0x00000001;

    private const uint ActionBlock = 0x00000001 | 0x00001000;
    private const uint ActionPermit = 0x00000002 | 0x00001000;

    private const uint TypeUint8 = 1;
    private const uint TypeUint16 = 2;
    private const uint TypeUint32 = 3;
    private const uint TypeUint64 = 4;
    private const uint TypeByteArray16 = 11;
    private const uint TypeSecurityDescriptor = 14;

    /// <summary>Instala los filtros persistentes para la cuenta y el puerto del proxy local.</summary>
    public static void Install(string account, int loopbackPort = 7423)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);

        if (loopbackPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(loopbackPort));
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WFP solo existe en Windows.");
        }

        var sid = new NTAccount(account).Translate(typeof(SecurityIdentifier));
        var engine = OpenEngine();

        try
        {
            DeleteFilters(engine);
            AddProvider(engine);
            AddSubLayer(engine);

            var (securityDescriptor, sdSize) = CreateMatchSecurityDescriptor(sid.Value);
            try
            {
                AddFilter(engine, PermitV4Key, LayerConnectV4, securityDescriptor, sdSize,
                    IPAddress.Loopback, loopbackPort, permit: true, weight: 13);
                AddFilter(engine, BlockV4Key, LayerConnectV4, securityDescriptor, sdSize,
                    remoteAddress: null, remotePort: null, permit: false, weight: 12);
                AddFilter(engine, PermitV6Key, LayerConnectV6, securityDescriptor, sdSize,
                    IPAddress.IPv6Loopback, loopbackPort, permit: true, weight: 13);
                AddFilter(engine, BlockV6Key, LayerConnectV6, securityDescriptor, sdSize,
                    remoteAddress: null, remotePort: null, permit: false, weight: 12);
            }
            finally
            {
                if (securityDescriptor != IntPtr.Zero)
                {
                    _ = LocalFree(securityDescriptor);
                }
            }
        }
        finally
        {
            _ = FwpmEngineClose0(engine);
        }
    }

    /// <summary>Elimina los filtros, el sublayer y el proveedor de ODW.</summary>
    public static void Uninstall()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engine = OpenEngine();

        try
        {
            DeleteFilters(engine);
            var subLayer = SubLayerKey;
            var provider = ProviderKey;
            _ = FwpmSubLayerDeleteByKey0(engine, ref subLayer);
            _ = FwpmProviderDeleteByKey0(engine, ref provider);
        }
        finally
        {
            _ = FwpmEngineClose0(engine);
        }
    }

    /// <summary>Indica si los cuatro filtros de ODW existen.</summary>
    public static bool IsInstalled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var engine = OpenEngine();

            try
            {
                return FilterExists(engine, BlockV4Key)
                       && FilterExists(engine, BlockV6Key)
                       && FilterExists(engine, PermitV4Key)
                       && FilterExists(engine, PermitV6Key);
            }
            finally
            {
                _ = FwpmEngineClose0(engine);
            }
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static IntPtr OpenEngine()
    {
        var result = FwpmEngineOpen0(
            IntPtr.Zero,
            10,
            IntPtr.Zero,
            IntPtr.Zero,
            out var engine);

        if (result != ErrorSuccess)
        {
            throw new Win32Exception((int)result, $"FwpmEngineOpen0 falló (0x{result:X8}).");
        }

        return engine;
    }

    private static void DeleteFilters(IntPtr engine)
    {
        DeleteFilter(engine, BlockV4Key);
        DeleteFilter(engine, BlockV6Key);
        DeleteFilter(engine, PermitV4Key);
        DeleteFilter(engine, PermitV6Key);
    }

    private static void DeleteFilter(IntPtr engine, Guid key)
    {
        var local = key;
        var result = FwpmFilterDeleteByKey0(engine, ref local);

        if (result != ErrorSuccess
            && result != ErrorFilterNotFound
            && result != ErrorNotFound)
        {
            throw new Win32Exception((int)result, $"FwpmFilterDeleteByKey0 falló (0x{result:X8}).");
        }
    }

    private static bool FilterExists(IntPtr engine, Guid key)
    {
        var local = key;
        var result = FwpmFilterGetByKey0(engine, ref local, out var filter);

        if (result == ErrorSuccess)
        {
            _ = FwpmFreeMemory0(ref filter);
            return true;
        }

        return false;
    }

    private static void AddProvider(IntPtr engine)
    {
        var name = Marshal.StringToHGlobalUni("ODW sandbox network policy");
        var description = Marshal.StringToHGlobalUni("Bloquea la salida de red del agente ODW");
        var provider = new FwpmProvider0
        {
            ProviderKey = ProviderKey,
            Flags = ProviderFlagPersistent,
        };

        try
        {
            provider.DisplayData = new FwpmDisplayData0
            {
                Name = name,
                Description = description,
            };

            var result = FwpmProviderAdd0(engine, ref provider, IntPtr.Zero);
            if (result != ErrorSuccess && result != ErrorAlreadyExists)
            {
                throw new Win32Exception((int)result, $"FwpmProviderAdd0 falló (0x{result:X8}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(description);
            Marshal.FreeHGlobal(name);
        }
    }

    private static void AddSubLayer(IntPtr engine)
    {
        var name = Marshal.StringToHGlobalUni("ODW sandbox network policy");
        var providerKey = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
        var subLayer = new FwpmSubLayer0
        {
            SubLayerKey = SubLayerKey,
            Flags = SubLayerFlagPersistent,
            Weight = 0x8000,
        };

        try
        {
            subLayer.DisplayData = new FwpmDisplayData0
            {
                Name = name,
                Description = IntPtr.Zero,
            };
            Marshal.StructureToPtr(ProviderKey, providerKey, false);
            subLayer.ProviderKey = providerKey;

            var result = FwpmSubLayerAdd0(engine, ref subLayer, IntPtr.Zero);
            if (result != ErrorSuccess && result != ErrorAlreadyExists)
            {
                throw new Win32Exception((int)result, $"FwpmSubLayerAdd0 falló (0x{result:X8}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(providerKey);
            Marshal.FreeHGlobal(name);
        }
    }

    private static void AddFilter(
        IntPtr engine,
        Guid filterKey,
        Guid layerKey,
        IntPtr securityDescriptor,
        uint securityDescriptorSize,
        IPAddress? remoteAddress,
        int? remotePort,
        bool permit,
        byte weight)
    {
        var conditions = Marshal.AllocHGlobal(Marshal.SizeOf<FwpmFilterCondition0>() * 3);
        var blob = Marshal.AllocHGlobal(Marshal.SizeOf<FwpByteBlob>());
        var address = Marshal.AllocHGlobal(16);
        var providerKey = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
        var name = Marshal.StringToHGlobalUni(
            permit ? "ODW permit loopback agent" : "ODW block agent network");
        var filter = new FwpmFilter0
        {
            FilterKey = filterKey,
            Flags = FilterFlagPersistent,
            LayerKey = layerKey,
            SubLayerKey = SubLayerKey,
        };

        try
        {
            filter.DisplayData = new FwpmDisplayData0
            {
                Name = name,
                Description = IntPtr.Zero,
            };

            Marshal.StructureToPtr(new FwpByteBlob
            {
                Size = securityDescriptorSize,
                Data = securityDescriptor,
            }, blob, false);

            Marshal.StructureToPtr(ProviderKey, providerKey, false);
            filter.ProviderKey = providerKey;

            var userCondition = new FwpmFilterCondition0
            {
                FieldKey = ConditionUserId,
                MatchType = 0,
            };
            userCondition.Value.Type = TypeSecurityDescriptor;
            userCondition.Value.Value = blob;

            var count = 1;
            var size = Marshal.SizeOf<FwpmFilterCondition0>();
            Marshal.StructureToPtr(userCondition, conditions, false);

            if (remoteAddress is not null)
            {
                var addressCondition = new FwpmFilterCondition0
                {
                    FieldKey = ConditionRemoteAddress,
                    MatchType = 0,
                };

                if (remoteAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    addressCondition.Value.Type = TypeUint32;
                    addressCondition.Value.Value = (IntPtr)BinaryPrimitives.ReadUInt32BigEndian(
                        remoteAddress.GetAddressBytes());
                }
                else
                {
                    var bytes = remoteAddress.GetAddressBytes();
                    Marshal.Copy(bytes, 0, address, bytes.Length);
                    addressCondition.Value.Type = TypeByteArray16;
                    addressCondition.Value.Value = address;
                }

                Marshal.StructureToPtr(addressCondition, IntPtr.Add(conditions, size), false);
                count++;
            }

            if (remotePort is not null)
            {
                var portCondition = new FwpmFilterCondition0
                {
                    FieldKey = ConditionRemotePort,
                    MatchType = 0,
                };
                portCondition.Value.Type = TypeUint16;
                portCondition.Value.Value = (IntPtr)remotePort.Value;

                Marshal.StructureToPtr(
                    portCondition,
                    IntPtr.Add(conditions, size * count),
                    false);
                count++;
            }

            filter.NumFilterConditions = (uint)count;
            filter.FilterCondition = conditions;

            var weightBuffer = Marshal.AllocHGlobal(sizeof(ulong));
            try
            {
                Marshal.WriteInt64(weightBuffer, weight);
                filter.Weight.Type = TypeUint64;
                filter.Weight.Value = weightBuffer;
                filter.Action.Type = permit ? ActionPermit : ActionBlock;

                var result = FwpmFilterAdd0(engine, ref filter, IntPtr.Zero, out _);
                if (result != ErrorSuccess && result != ErrorAlreadyExists)
                {
                    throw new Win32Exception((int)result, $"FwpmFilterAdd0 falló (0x{result:X8}).");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(weightBuffer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(name);
            Marshal.FreeHGlobal(providerKey);
            Marshal.FreeHGlobal(address);
            Marshal.FreeHGlobal(blob);
            Marshal.FreeHGlobal(conditions);
        }
    }

    private static (IntPtr Descriptor, uint Size) CreateMatchSecurityDescriptor(string sddlSid)
    {
        // El motor comprueba FWP_ACTRL_MATCH_FILTER (0x1) en la DACL: solo el SID indicado
        // hace que la condición ALE_USER_ID sea verdadera.
        var sddl = $"O:{sddlSid}D:(A;;0x1;;;{sddlSid})";

        if (ConvertStringSecurityDescriptorToSecurityDescriptorW(
                sddl, 1, out var descriptor, out var size) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo crear el descriptor de seguridad.");
        }

        return (descriptor, size);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FwpmDisplayData0
    {
        public IntPtr Name;
        public IntPtr Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FwpmProvider0
    {
        public Guid ProviderKey;
        public FwpmDisplayData0 DisplayData;
        public uint Flags;
        public FwpByteBlob ProviderData;
        public IntPtr ServiceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FwpmSubLayer0
    {
        public Guid SubLayerKey;
        public FwpmDisplayData0 DisplayData;
        public uint Flags;
        public IntPtr ProviderKey;
        public FwpByteBlob ProviderData;
        public ushort Weight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FwpValue0
    {
        public uint Type;
        public IntPtr Value;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct FwpAction0
    {
        [FieldOffset(0)]
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FwpmFilter0
    {
        public Guid FilterKey;
        public FwpmDisplayData0 DisplayData;
        public uint Flags;
        public IntPtr ProviderKey;
        public FwpByteBlob ProviderData;
        public Guid LayerKey;
        public Guid SubLayerKey;
        public FwpValue0 Weight;
        public uint NumFilterConditions;
        public IntPtr FilterCondition;
        public FwpAction0 Action;
        public Guid ProviderContextKey;
        public IntPtr Reserved;
        public ulong FilterId;
        public FwpValue0 EffectiveWeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FwpConditionValue0
    {
        public uint Type;
        public IntPtr Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FwpmFilterCondition0
    {
        public Guid FieldKey;
        public uint MatchType;
        public FwpConditionValue0 Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FwpByteBlob
    {
        public uint Size;
        public IntPtr Data;
    }

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmEngineOpen0(
        IntPtr serverName,
        uint authnService,
        IntPtr authIdentity,
        IntPtr session,
        out IntPtr engineHandle);

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmEngineClose0(IntPtr engineHandle);

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmProviderAdd0(
        IntPtr engineHandle,
        ref FwpmProvider0 provider,
        IntPtr securityDescriptor);

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmSubLayerAdd0(
        IntPtr engineHandle,
        ref FwpmSubLayer0 subLayer,
        IntPtr securityDescriptor);

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmFilterAdd0(
        IntPtr engineHandle,
        ref FwpmFilter0 filter,
        IntPtr securityDescriptor,
        out ulong id);

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmFilterDeleteByKey0(IntPtr engineHandle, ref Guid key);

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmFilterGetByKey0(
        IntPtr engineHandle,
        ref Guid key,
        out IntPtr filter);

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmSubLayerDeleteByKey0(IntPtr engineHandle, ref Guid key);

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmProviderDeleteByKey0(IntPtr engineHandle, ref Guid key);

    [LibraryImport("fwpuclnt.dll")]
    private static partial uint FwpmFreeMemory0(ref IntPtr memory);

    [LibraryImport("advapi32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial int ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor,
        uint stringSdRevision,
        out IntPtr securityDescriptor,
        out uint securityDescriptorSize);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr LocalFree(IntPtr memory);
}

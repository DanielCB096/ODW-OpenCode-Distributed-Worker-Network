using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32.SafeHandles;

namespace Odw.Sandbox.Workspaces;

public sealed class WorkspaceGuardException(string message) : Exception(message);

/// <summary>
/// Rechaza reparse points (junctions/symlinks) y hard links dentro del workspace. Un junction
/// permite salir del árbol sin necesitar privilegios especiales; el ACL del destino sigue
/// aplicándose, pero rompe la frontera del workspace y puede falsear changed_paths.
/// </summary>
public static partial class WorkspaceGuard
{
    public static void EnsureNoReparsePoints(string workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);

        if (!Directory.Exists(workspace))
        {
            return;
        }

        var pending = new Stack<string>();
        pending.Push(workspace);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new WorkspaceGuardException(
                        $"REPARSE_DETECTED: {System.IO.Path.GetRelativePath(workspace, entry.FullName)}");
                }

                if (entry is DirectoryInfo child)
                {
                    pending.Push(child.FullName);
                }
                else if (OperatingSystem.IsWindows() && HasMultipleLinks(entry.FullName))
                {
                    throw new WorkspaceGuardException(
                        $"HARD_LINK_DETECTED: {System.IO.Path.GetRelativePath(workspace, entry.FullName)}");
                }
            }
        }
    }

    private static bool HasMultipleLinks(string path)
    {
        try
        {
            using var handle = File.OpenHandle(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (!GetFileInformationByHandle(handle, out var information))
            {
                return false;
            }

            return information.NumberOfLinks > 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle,
        out ByHandleFileInformation information);
#pragma warning restore SYSLIB1054
}

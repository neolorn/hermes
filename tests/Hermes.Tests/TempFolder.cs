using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Hermes.Tests;

/// <summary>A new, empty folder under the system's temporary folder, removed when the test ends.</summary>
internal sealed class TempFolder : IDisposable
{
    private string? unreadable;

    internal TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hermes-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    /// <summary>Makes a file at <paramref name="relativePath"/> and answers its full path.</summary>
    internal string File(string relativePath)
    {
        string file = System.IO.Path.Combine(Path, relativePath);
        System.IO.File.WriteAllText(file, "a file");
        return file;
    }

    /// <summary>Makes a folder at <paramref name="relativePath"/> holding one file and answers its full path.</summary>
    internal string Folder(string relativePath)
    {
        string folder = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "a.liquid"), "a template");
        return folder;
    }

    /// <summary>Makes a folder at <paramref name="relativePath"/> whose entries this process cannot list, and answers its full path.</summary>
    internal string UnreadableFolder(string relativePath)
    {
        string folder = Folder(relativePath);
        if (OperatingSystem.IsWindows())
        {
            DirectoryInfo info = new(folder);
            DirectorySecurity security = info.GetAccessControl();
            security.AddAccessRule(Refusal());
            info.SetAccessControl(security);
        }
        else
        {
            System.IO.File.SetUnixFileMode(folder, UnixFileMode.None);
        }

        unreadable = folder;
        return folder;
    }

    public void Dispose()
    {
        if (unreadable is not null)
        {
            if (OperatingSystem.IsWindows())
            {
                DirectoryInfo info = new(unreadable);
                DirectorySecurity security = info.GetAccessControl();
                security.RemoveAccessRule(Refusal());
                info.SetAccessControl(security);
            }
            else
            {
                System.IO.File.SetUnixFileMode(unreadable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        Directory.Delete(Path, recursive: true);
    }

    /// <summary>Refuses everyone the listing of a folder's entries.</summary>
    [SupportedOSPlatform("windows")]
    private static FileSystemAccessRule Refusal()
    {
        return new(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.ListDirectory, AccessControlType.Deny);
    }
}
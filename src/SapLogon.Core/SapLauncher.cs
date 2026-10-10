namespace SapLogon.Core;

/// <summary>
/// Launch helpers. The preferred path is SAP GUI Scripting (see Scripts/open-connection.vbs and
/// <see cref="ScriptHost"/>). The fallback is sapshcut.exe: <c>-sysname</c> names the SAP Logon entry, so
/// router, message server and SNC settings come from SAP Logon itself, but without user and password it shows
/// a small password dialog with no client field.
/// </summary>
public static class SapLauncher
{
    public const string ExeName = "sapshcut.exe";

    /// <summary>Builds the sapshcut command line for <paramref name="connection"/>.</summary>
    public static string Arguments(SapConnection connection)
    {
        // A double quote cannot be escaped inside -key="value", and a trailing backslash would escape the
        // closing quote under the usual argv rules, so both are dropped.
        var args = $"-sysname=\"{Clean(connection.Name)}\"";
        if (connection.SystemId.Length > 0)
        {
            args += $" -system=\"{Clean(connection.SystemId)}\"";
        }

        return args;
    }

    /// <summary>
    /// cscript.exe with the same bitness as SAP GUI: SAP GUI's scripting COM objects are registered only for
    /// its own architecture, so a 32-bit SAP GUI (under Program Files (x86)) needs the 32-bit script host.
    /// </summary>
    public static string ScriptHost(string sapGuiDir)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var is32Bit = x86.Length > 0
            && Path.GetFullPath(sapGuiDir).StartsWith(x86.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        return Path.Combine(windows, is32Bit && Environment.Is64BitOperatingSystem ? "SysWOW64" : "System32", "cscript.exe");
    }

    /// <summary>First existing sapshcut.exe in the given SAP GUI folders, then the default install folders.</summary>
    public static string? Find(IEnumerable<string?> preferredDirs)
    {
        var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var x64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var defaults = new[]
        {
            Path.Combine(x86, "SAP", "FrontEnd", "SAPGUI"),
            Path.Combine(x64, "SAP", "FrontEnd", "SAPGUI"),
        };

        foreach (var dir in preferredDirs.Concat(defaults))
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            var exe = Path.Combine(dir.Trim(), ExeName);
            if (File.Exists(exe))
            {
                return exe;
            }
        }

        return null;
    }

    private static string Clean(string value) => value.Replace("\"", "", StringComparison.Ordinal).TrimEnd('\\');
}

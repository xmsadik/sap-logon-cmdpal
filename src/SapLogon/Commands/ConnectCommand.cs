using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;
using SapLogon.Core;

namespace SapLogon;

/// <summary>
/// Opens the SAP Logon entry like a double-click in SAP Logon: SAP GUI Scripting's OpenConnection, which shows
/// SAP GUI's own logon screen with the system's default client. When scripting is not available, falls back
/// to sapshcut.exe, whose small password dialog has no client field (it logs on to client 000).
/// </summary>
internal sealed partial class ConnectCommand : InvokableCommand
{
    private const int ScriptingUnavailable = 2;
    private const int OpenConnectionFailed = 3;
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(2);
    private readonly SapConnection _connection;

    public ConnectCommand(SapConnection connection)
    {
        _connection = connection;
        Name = "Connect";
        Icon = new IconInfo("\uE8A7"); // Segoe Fluent "OpenInNewWindow"
    }

    public override CommandResult Invoke()
    {
        var shortcut = SapLauncher.Find(SapGuiDirs());
        if (shortcut is null)
        {
            return CommandResult.ShowToast("sapshcut.exe not found. Is SAP GUI installed?");
        }

        try
        {
            var script = StartScript(Path.GetDirectoryName(shortcut)!);
            if (script is null)
            {
                StartShortcut(shortcut);
            }
            else
            {
                // OpenConnection blocks until the connection is up; watch it off the host's thread.
                _ = Task.Run(() => FallBackIfScriptingUnavailable(script, shortcut));
            }

            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            DiagLog.WriteLine($"Starting SAP GUI for '{_connection.Name}' failed: {ex}");
            return CommandResult.ShowToast($"Could not start SAP GUI: {ex.Message}");
        }
    }

    /// <summary>Starts the OpenConnection script, or returns null when the script or its host is missing.</summary>
    private Process? StartScript(string sapGuiDir)
    {
        var script = Path.Combine(AppContext.BaseDirectory, "Scripts", "open-connection.vbs");
        var host = SapLauncher.ScriptHost(sapGuiDir);
        if (!File.Exists(script) || !File.Exists(host))
        {
            DiagLog.WriteLine($"Scripting launch unavailable (script: {File.Exists(script)}, host: {host})");
            return null;
        }

        var info = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = sapGuiDir,
        };
        foreach (var arg in new[] { "//nologo", "//B", script, _connection.Name, Path.Combine(sapGuiDir, "saplogon.exe") })
        {
            info.ArgumentList.Add(arg);
        }

        return Process.Start(info);
    }

    private void FallBackIfScriptingUnavailable(Process script, string shortcut)
    {
        try
        {
            using (script)
            {
                if (!script.WaitForExit(ScriptTimeout))
                {
                    return; // still waiting for a slow connection; SAP GUI reports its own errors
                }

                // 0 = opened; 3 = SAP GUI already showed why the connection failed. Anything else means
                // scripting could not be reached (disabled, VBScript missing, SAP Logon did not start).
                if (script.ExitCode is 0 or OpenConnectionFailed)
                {
                    return;
                }

                DiagLog.WriteLine($"Scripting launch of '{_connection.Name}' exited with {script.ExitCode}"
                    + (script.ExitCode == ScriptingUnavailable ? " (scripting unavailable)" : "") + "; using sapshcut");
            }

            StartShortcut(shortcut);
        }
        catch (Exception ex)
        {
            DiagLog.WriteLine($"Fallback for '{_connection.Name}' failed: {ex}");
        }
    }

    private void StartShortcut(string shortcut)
    {
        using var process = Process.Start(new ProcessStartInfo(shortcut, SapLauncher.Arguments(_connection))
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(shortcut),
        });
    }

    /// <summary>SAP GUI install folders as registered by the SAP setup (32-bit SAP GUI first, then 64-bit).</summary>
    private static string?[] SapGuiDirs() => [SapGuiDir(RegistryView.Registry32), SapGuiDir(RegistryView.Registry64)];

    private static string? SapGuiDir(RegistryView view)
    {
        try
        {
            using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = hive.OpenSubKey(@"SOFTWARE\SAP\SAP Shared");
            return key?.GetValue("SAPsysdir") as string;
        }
        catch (Exception ex)
        {
            DiagLog.WriteLine($"Reading SAPsysdir ({view}) failed: {ex.Message}");
            return null;
        }
    }
}

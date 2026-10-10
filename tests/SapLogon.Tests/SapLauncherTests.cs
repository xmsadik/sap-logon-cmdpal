using SapLogon.Core;
using Xunit;

namespace SapLogon.Tests;

public sealed class SapLauncherTests
{
    private static SapConnection Conn(string name, string sid) => new("id", name, sid, "h:3200", null, null, "");

    [Fact]
    public void Arguments_name_the_entry_and_the_sid()
    {
        Assert.Equal(
            "-sysname=\"Acme - DEV [192.0.2.10]\" -system=\"DEV\"",
            SapLauncher.Arguments(Conn("Acme - DEV [192.0.2.10]", "DEV")));
    }

    [Fact]
    public void Arguments_without_sid_and_with_quotes()
    {
        Assert.Equal("-sysname=\"My system\"", SapLauncher.Arguments(Conn("My \"system\"", "")));
    }

    [Fact]
    public void Arguments_drop_trailing_backslash_so_the_closing_quote_survives()
    {
        Assert.Equal(@"-sysname=""Path C:\x""", SapLauncher.Arguments(Conn(@"Path C:\x\", "")));
    }

    [Fact]
    public void ScriptHost_matches_SAP_GUI_bitness()
    {
        var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var x64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.EndsWith(@"\SysWOW64\cscript.exe", SapLauncher.ScriptHost(Path.Combine(x86, "SAP", "FrontEnd", "SAPGUI")), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"\System32\cscript.exe", SapLauncher.ScriptHost(Path.Combine(x64, "SAP", "FrontEnd", "SAPGUI")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Find_prefers_given_folder_and_skips_blank_ones()
    {
        var dir = Directory.CreateTempSubdirectory("saplogon-exe-").FullName;
        try
        {
            var exe = Path.Combine(dir, SapLauncher.ExeName);
            File.WriteAllText(exe, "");
            Assert.Equal(exe, SapLauncher.Find([null, " ", dir]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

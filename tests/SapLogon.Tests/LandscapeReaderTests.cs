using SapLogon.Core;
using Xunit;

namespace SapLogon.Tests;

public sealed class LandscapeReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("saplogon-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string xml)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, xml);
        return path;
    }

    private const string Main = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Landscape updated="2026-01-01T00:00:00Z" version="1" generator="saplogon">
          <Includes>
            <Include url="INCLUDE_URL" index="0"/>
            <Include url="file:///C:/does/not/exist.xml" index="1"/>
            <Include url="https://example.com/landscape.xml" index="2"/>
          </Includes>
          <Workspaces>
            <Workspace uuid="w1" name="Customers">
              <Node uuid="n1" name="Acme">
                <Item uuid="i1" serviceid="s-direct"/>
                <Node uuid="n2" name="Prod">
                  <Item uuid="i2" serviceid="s-ms"/>
                </Node>
              </Node>
              <Item uuid="i3" serviceid="s-root"/>
            </Workspace>
            <Workspace uuid="w2" name="Favorites">
              <Item uuid="i4" serviceid="s-direct"/>
            </Workspace>
          </Workspaces>
          <Services>
            <Service type="SAPGUI" uuid="s-direct" name="Acme - DEV [10.0.0.1]" systemid="DEV" server="10.0.0.1:3200" routerid="r1">
              <Memo>secret password</Memo>
            </Service>
            <Service type="SAPGUI" uuid="s-ms" name="Acme - PRD" systemid="PRD" msid="m1" server="PUBLIC"/>
            <Service type="SAPGUI" uuid="s-root" name="Beta QAS" systemid="QAS" server="qas.local:3201"/>
            <Service type="SAPGUI" uuid="s-loose" name="aaa Not in tree " systemid="X01" msid="m-missing" server="GRP" routerid="r-missing"/>
            <Service type="FIORI" uuid="s-web" name="Fiori Launchpad" url="https://x"/>
            <Service type="SAPGUI" uuid="s-noname" systemid="Y01" server="y:3200"/>
          </Services>
          <Routers>
            <Router uuid="r1" name="/H/router.acme/S/3299/H/" router="/H/router.acme/S/3299/H/"/>
          </Routers>
          <Messageservers>
            <Messageserver uuid="m1" name="PRD" host="prd-ms.acme" port="3600"/>
          </Messageservers>
        </Landscape>
        """;

    private const string Include = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Landscape>
          <Includes><Include url="MAIN_URL" index="0"/></Includes>
          <Services>
            <Service type="SAPGUI" uuid="s-inc" name="Included S4H" systemid="S4H" msid="m-inc" server="SPACE"/>
            <Service type="SAPGUI" uuid="s-direct" name="Duplicate of DEV" systemid="DEV" server="dup:3200"/>
          </Services>
          <Messageservers>
            <Messageserver uuid="m-inc" name="S4H" host="s4h-ms"/>
          </Messageservers>
        </Landscape>
        """;

    private Landscape ReadSample()
    {
        var mainPath = Path.Combine(_dir, "SAPUILandscape.xml");
        var incPath = Path.Combine(_dir, "Global.xml");
        Write("Global.xml", Include.Replace("MAIN_URL", new Uri(mainPath).AbsoluteUri, StringComparison.Ordinal));
        Write("SAPUILandscape.xml", Main.Replace("INCLUDE_URL", new Uri(incPath).AbsoluteUri, StringComparison.Ordinal));
        return LandscapeReader.Read(mainPath);
    }

    private static SapConnection Get(Landscape l, string id) => l.Connections.Single(c => c.Id == id);

    [Fact]
    public void Reads_only_named_SAPGUI_entries_sorted_by_name_and_skips_duplicate_uuids()
    {
        var l = ReadSample();
        Assert.Equal(
            ["aaa Not in tree ", "Acme - DEV [10.0.0.1]", "Acme - PRD", "Beta QAS", "Included S4H"],
            l.Connections.Select(c => c.Name));
    }

    [Fact]
    public void Direct_entry_resolves_router_and_first_folder()
    {
        var c = Get(ReadSample(), "s-direct");
        Assert.Equal("DEV", c.SystemId);
        Assert.Equal("10.0.0.1:3200", c.Address);
        Assert.Equal("/H/router.acme/S/3299/H/", c.Router);
        Assert.Null(c.MessageServer);
        Assert.Equal("Customers / Acme", c.Folder);
    }

    [Fact]
    public void Message_server_entry_shows_host_port_and_group_in_nested_folder()
    {
        var c = Get(ReadSample(), "s-ms");
        Assert.Equal("prd-ms.acme:3600", c.MessageServer);
        Assert.Equal("prd-ms.acme:3600 (group PUBLIC)", c.Address);
        Assert.Equal("Customers / Acme / Prod", c.Folder);
    }

    [Fact]
    public void Workspace_level_item_and_entry_outside_tree()
    {
        var l = ReadSample();
        Assert.Equal("Customers", Get(l, "s-root").Folder);
        var loose = Get(l, "s-loose");
        Assert.Equal("", loose.Folder);
        Assert.Null(loose.Router); // unknown router id
        Assert.Equal("unknown message server (group GRP)", loose.Address);
        Assert.Equal("aaa Not in tree ", loose.Name); // kept exact for sapshcut -sysname
    }

    [Fact]
    public void Include_is_read_with_its_own_message_server_and_cycle_is_ignored()
    {
        var l = ReadSample();
        Assert.Equal("s4h-ms (group SPACE)", Get(l, "s-inc").Address);
        // Main, Global.xml and the missing include (watched so it is picked up once it appears); http is ignored.
        Assert.Equal(3, l.Files.Count);
    }

    [Fact]
    public void Memo_is_never_exposed()
    {
        var l = ReadSample();
        Assert.DoesNotContain(l.Connections, c => c.ToString().Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_main_file_throws()
    {
        Assert.Throws<FileNotFoundException>(() => LandscapeReader.Read(Path.Combine(_dir, "missing.xml")));
    }

    [Fact]
    public void Dtd_is_rejected()
    {
        var path = Write("evil.xml", """<?xml version="1.0"?><!DOCTYPE x [<!ENTITY e "boom">]><Landscape/>""");
        Assert.Throws<System.Xml.XmlException>(() => LandscapeReader.Read(path));
    }
}

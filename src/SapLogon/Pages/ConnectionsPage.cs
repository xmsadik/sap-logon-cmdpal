using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SapLogon.Core;

namespace SapLogon;

/// <summary>
/// Lists every SAP GUI entry in SAP Logon. Filtering is left to the host's built-in search, which matches
/// the title (SAP Logon description) and subtitle (SID, address, folder). The landscape is re-read when one
/// of its files changed since the last read, so new entries show up without reloading the extension.
/// </summary>
internal sealed partial class ConnectionsPage : ListPage
{
    private static readonly IconInfo SystemIcon = new("\uE7F4"); // Segoe Fluent "TVMonitor"
    private readonly object _sync = new(); // guards the cache fields only; never held across host calls
    private IListItem[]? _items;
    private IReadOnlyList<string> _files = [];
    private string _stamp = "";

    public ConnectionsPage()
    {
        Id = "SapLogon.connections";
        Name = "Open";
        Title = "SAP Logon";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        PlaceholderText = "Search by description, SID, server or folder";
    }

    public override IListItem[] GetItems()
    {
        try
        {
            IListItem[]? cached;
            IReadOnlyList<string> files;
            string stamp;
            lock (_sync)
            {
                (cached, files, stamp) = (_items, _files, _stamp);
            }

            if (cached is not null && Stamp(files) == stamp)
            {
                return cached;
            }

            // Stamp the previously known files before reading, so a write during the read is seen next time.
            var before = Stamp(files);
            var landscape = LandscapeReader.Read(LandscapeReader.DefaultPath());
            var items = Build(landscape.Connections);
            var newStamp = SameFiles(files, landscape.Files) ? before : Stamp(landscape.Files);
            lock (_sync)
            {
                (_items, _files, _stamp) = (items, landscape.Files, newStamp);
            }

            return items;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return [Message("SAP Logon landscape file not found", LandscapeReader.DefaultPath())];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            DiagLog.WriteLine($"Reading landscape failed: {ex}");
            return [Message("Could not read the SAP Logon landscape file", ex.Message)];
        }
        catch (Exception ex)
        {
            DiagLog.WriteLine($"GetItems failed: {ex}");
            return [Message("Something went wrong", ex.Message)];
        }
    }

    private static IListItem[] Build(IReadOnlyList<SapConnection> connections)
    {
        if (connections.Count == 0)
        {
            return [Message("No SAP GUI entries in SAP Logon", LandscapeReader.DefaultPath())];
        }

        var items = new IListItem[connections.Count];
        for (var i = 0; i < connections.Count; i++)
        {
            var c = connections[i];
            var more = new List<IContextItem>();
            if (c.Address.Length > 0)
            {
                more.Add(new CommandContextItem(new CopyTextCommand(c.Address) { Name = "Copy server address" }));
            }

            if (c.SystemId.Length > 0)
            {
                more.Add(new CommandContextItem(new CopyTextCommand(c.SystemId) { Name = "Copy SID" }));
            }

            more.Add(new CommandContextItem(new CopyTextCommand(c.Name) { Name = "Copy description" }));

            items[i] = new ListItem(new ConnectCommand(c))
            {
                Title = c.Name.Trim(),
                Subtitle = Subtitle(c),
                Icon = SystemIcon,
                Tags = c.SystemId.Length > 0 ? [new Tag(c.SystemId)] : [],
                MoreCommands = [.. more],
            };
        }

        return items;
    }

    internal static string Subtitle(SapConnection c)
    {
        // The host's search scores Title and Subtitle only (not Tags), so the SID must be here to be searchable.
        var parts = new List<string>(4);
        if (c.SystemId.Length > 0)
        {
            parts.Add(c.SystemId);
        }

        if (c.Address.Length > 0)
        {
            parts.Add(c.Address);
        }

        if (c.Router is not null)
        {
            parts.Add("via SAProuter");
        }

        if (c.Folder.Length > 0)
        {
            parts.Add(c.Folder);
        }

        return string.Join(" · ", parts);
    }

    private static ListItem Message(string title, string subtitle) =>
        new(new NoOpCommand()) { Title = title, Subtitle = subtitle, Icon = new IconInfo("\uE783") }; // "Error"

    private static bool SameFiles(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
        a.Count == b.Count && System.Linq.Enumerable.SequenceEqual(a, b, StringComparer.OrdinalIgnoreCase);

    /// <summary>Last-write time and size of every landscape file; any change means re-read.</summary>
    private static string Stamp(IEnumerable<string> files)
    {
        var parts = new List<string>();
        foreach (var f in files)
        {
            var info = new FileInfo(f);
            parts.Add($"{f}*{(info.Exists ? info.LastWriteTimeUtc.Ticks + ":" + info.Length : "-")}");
        }

        return string.Join('|', parts);
    }
}

using System.Xml;
using System.Xml.Linq;

namespace SapLogon.Core;

/// <summary>The connections read from a landscape file, and every local file that was read or tried (to watch for changes).</summary>
public sealed record Landscape(IReadOnlyList<SapConnection> Connections, IReadOnlyList<string> Files);

/// <summary>
/// Reads SAP Logon's SAPUILandscape.xml (7.40 and later) and the local files it includes.
/// Only reads; never writes. The <c>Memo</c> of an entry is deliberately ignored: people keep
/// credentials there, and the list must not show them.
/// </summary>
public static class LandscapeReader
{
    private const int MaxIncludeDepth = 8;

    /// <summary>%APPDATA%\SAP\Common\SAPUILandscape.xml.</summary>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SAP", "Common", "SAPUILandscape.xml");

    /// <summary>
    /// Reads <paramref name="path"/> and its <c>file:</c> includes. A missing or broken include is skipped;
    /// a missing or broken main file throws, so the caller can show the reason.
    /// </summary>
    public static Landscape Read(string path)
    {
        var docs = new List<XDocument>();
        var files = new List<string>();
        Load(Path.GetFullPath(path), docs, files, depth: 0);

        // Routers and message servers are referenced by uuid, possibly across files.
        var routers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var messageServers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in docs)
        {
            foreach (var r in doc.Descendants("Router"))
            {
                if (Attr(r, "uuid") is { Length: > 0 } id)
                {
                    routers.TryAdd(id, Attr(r, "router") ?? Attr(r, "name") ?? "");
                }
            }

            foreach (var m in doc.Descendants("Messageserver"))
            {
                if (Attr(m, "uuid") is { Length: > 0 } id && Attr(m, "host") is { Length: > 0 } host)
                {
                    messageServers.TryAdd(id, Attr(m, "port") is { Length: > 0 } port ? $"{host}:{port}" : host);
                }
            }

            foreach (var ws in doc.Descendants("Workspace"))
            {
                CollectFolders(ws, Attr(ws, "name") ?? "", folders);
            }
        }

        var connections = new List<SapConnection>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in docs)
        {
            foreach (var s in doc.Descendants("Service"))
            {
                // SAP Logon also stores non-GUI services (e.g. web entries); only SAP GUI ones can be opened by sapshcut.
                var type = Attr(s, "type");
                var name = Attr(s, "name"); // untrimmed: sapshcut matches the description exactly
                var id = Attr(s, "uuid") ?? name;
                if (!string.Equals(type, "SAPGUI", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(name) || id is null || !seen.Add(id))
                {
                    continue;
                }

                string? ms = Attr(s, "msid") is { Length: > 0 } msid
                    ? messageServers.TryGetValue(msid, out var m) ? m : "unknown message server"
                    : null;
                string? router = Attr(s, "routerid") is { Length: > 0 } rid && routers.TryGetValue(rid, out var r) && r.Length > 0 ? r : null;

                connections.Add(new SapConnection(
                    id,
                    name,
                    Attr(s, "systemid")?.Trim() ?? "",
                    Attr(s, "server")?.Trim() ?? "",
                    ms,
                    router,
                    folders.TryGetValue(id, out var folder) ? folder : ""));
            }
        }

        connections.Sort(static (a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
        return new Landscape(connections, files);
    }

    private static void Load(string path, List<XDocument> docs, List<string> files, int depth)
    {
        if (files.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            return; // include cycle
        }

        files.Add(path);
        var doc = Parse(path);
        docs.Add(doc);

        if (depth >= MaxIncludeDepth)
        {
            return;
        }

        foreach (var inc in doc.Descendants("Include"))
        {
            if (Attr(inc, "url") is not { Length: > 0 } url
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsFile)
            {
                continue; // server-side (http) landscapes are out of scope
            }

            try
            {
                Load(Path.GetFullPath(uri.LocalPath), docs, files, depth + 1);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException
                or ArgumentException or NotSupportedException)
            {
                DiagLog.WriteLine($"Skipped include {uri.LocalPath}: {ex.Message}");
            }
        }
    }

    private static XDocument Parse(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }

    /// <summary>Maps each service uuid to the first folder path it appears under.</summary>
    private static void CollectFolders(XElement parent, string path, Dictionary<string, string> folders)
    {
        foreach (var child in parent.Elements())
        {
            if (child.Name.LocalName == "Item")
            {
                if (Attr(child, "serviceid") is { Length: > 0 } sid)
                {
                    folders.TryAdd(sid, path);
                }
            }
            else if (child.Name.LocalName == "Node")
            {
                var name = Attr(child, "name") ?? "";
                CollectFolders(child, path.Length == 0 ? name : $"{path} / {name}", folders);
            }
        }
    }

    private static string? Attr(XElement e, string name) => e.Attribute(name)?.Value;
}

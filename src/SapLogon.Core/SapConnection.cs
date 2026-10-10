namespace SapLogon.Core;

/// <summary>One SAP GUI entry from SAP Logon (a <c>Service</c> element in SAPUILandscape.xml).</summary>
/// <param name="Id">The entry's uuid.</param>
/// <param name="Name">The description shown in SAP Logon, untrimmed; sapshcut finds the entry by this exact text.</param>
/// <param name="SystemId">SID, e.g. "DEV". May be empty.</param>
/// <param name="Server">"host:port" for a direct entry, or the logon group for a message server entry.</param>
/// <param name="MessageServer">"host:port" of the message server for load-balanced entries, otherwise null.</param>
/// <param name="Router">SAProuter string, otherwise null.</param>
/// <param name="Folder">Workspace / folder path in the SAP Logon tree, e.g. "Customers / Acme". May be empty.</param>
public sealed record SapConnection(
    string Id,
    string Name,
    string SystemId,
    string Server,
    string? MessageServer,
    string? Router,
    string Folder)
{
    /// <summary>Address to show and copy: the application server, or the message server and logon group.</summary>
    public string Address => MessageServer is null ? Server : $"{MessageServer} (group {Server})";
}

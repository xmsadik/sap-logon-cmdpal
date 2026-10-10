using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SapLogon;

public partial class SapLogonCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public SapLogonCommandsProvider()
    {
        DisplayName = "SAP Logon";
        Id = "SapLogon";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

        _commands =
        [
            new CommandItem(new ConnectionsPage())
            {
                Title = DisplayName,
                Subtitle = "Search SAP Logon systems and connect",
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

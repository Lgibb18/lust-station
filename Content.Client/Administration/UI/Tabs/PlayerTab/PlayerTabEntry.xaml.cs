﻿using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;

namespace Content.Client.Administration.UI.Tabs.PlayerTab;

[GenerateTypedNameReferences]
public sealed partial class PlayerTabEntry : PanelContainer
{
    public NetEntity? PlayerEntity;

    public Action<NetEntity?>? OnObjectives; // Sunrise-Edit

    public PlayerTabEntry(PlayerInfo player, StyleBoxFlat styleBoxFlat)
    {
        RobustXamlLoader.Load(this);
        var config = IoCManager.Resolve<IConfigurationManager>();

        UsernameLabel.Text = player.Username;
        if (!player.Connected)
            UsernameLabel.StyleClasses.Add("Disabled");
        JobLabel.Text = player.StartingJob;
        var separateAntagSymbols = config.GetCVar(CCVars.AdminPlayerlistSeparateSymbols);
        var genericAntagSymbol = player.Antag ? Loc.GetString("player-tab-antag-prefix") : string.Empty;
        var roleSymbol = player.Antag ? player.RoleProto.Symbol : string.Empty;
        var symbol = separateAntagSymbols ? roleSymbol : genericAntagSymbol;
        CharacterLabel.Text = Loc.GetString("player-tab-character-name-antag-symbol", ("symbol", symbol), ("name", player.CharacterName));

        if (player.Antag && config.GetCVar(CCVars.AdminPlayerlistHighlightedCharacterColor))
            CharacterLabel.FontColorOverride = player.RoleProto.Color;
        if (player.IdentityName != player.CharacterName)
            CharacterLabel.Text += $" [{player.IdentityName}]";
        SponsorLabel.Text = player.IsSponsor ? player.SponsorTitle : ""; // Sunrise-Sponsors
        RoleTypeLabel.Text = Loc.GetString(player.RoleProto.Name);
        if (config.GetCVar(CCVars.AdminPlayerlistRoleTypeColor))
            RoleTypeLabel.FontColorOverride = player.RoleProto.Color;
        BackgroundColorPanel.PanelOverride = styleBoxFlat;
        OverallPlaytimeLabel.Text = player.PlaytimeString;
        PlayerEntity = player.NetEntity;

        ObjectivesButton.OnPressed += _ => OnObjectives?.Invoke(player.NetEntity); // Sunrise-Edit
    }
}

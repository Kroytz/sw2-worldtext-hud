using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Players;
using WorldTextHud.Models;
using NativeColor = SwiftlyS2.Shared.Natives.Color;

namespace WorldTextHud;

public sealed partial class WorldTextHudPlugin
{
    private void ShowHudMenu(IPlayer player)
    {
        var menus = Core.MenusAPI;
        if (menus == null)
            return;

        var builder = menus.CreateBuilder();
        builder.Design.SetMenuTitle("HUD Settings");

        foreach (var (key, entry) in _entries)
        {
            var entryKey = key;
            var displayName = entry.DisplayName;

            var option = new ButtonMenuOption(displayName);
            option.Click += (_, args) =>
            {
                Core.Scheduler.NextWorldUpdate(() => ShowEntryMenu(args.Player, entryKey, displayName));
                return ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        builder.EnableExit();
        menus.OpenMenuForPlayer(player, builder.Build());
    }

    private void ShowEntryMenu(IPlayer player, string key, string displayName)
    {
        var menus = Core.MenusAPI;
        if (menus == null)
            return;

        var state = GetOrCreatePlayerState(player);
        if (!state.Entries.TryGetValue(key, out var entryState))
            return;

        var builder = menus.CreateBuilder();
        builder.Design.SetMenuTitle($"{displayName} Settings");

        // X position slider
        var xSlider = new SliderMenuOption($"X Position", MinHudPosition, MaxHudPosition, entryState.X, 0.01f, 40);
        xSlider.ValueChanged += (_, e) =>
        {
            if (!_playerStates.TryGetValue(player.SteamID, out var s) ||
                !s.Entries.TryGetValue(key, out var es))
                return;

            es.X = e.NewValue;
            SaveUserPreferences(s);
        };
        builder.AddOption(xSlider);

        // Y position slider
        var ySlider = new SliderMenuOption($"Y Position", MinHudPosition, MaxHudPosition, entryState.Y, 0.01f, 40);
        ySlider.ValueChanged += (_, e) =>
        {
            if (!_playerStates.TryGetValue(player.SteamID, out var s) ||
                !s.Entries.TryGetValue(key, out var es))
                return;

            es.Y = e.NewValue;
            SaveUserPreferences(s);
        };
        builder.AddOption(ySlider);

        // Font size slider
        var fontSizeSlider = new SliderMenuOption("Font Size", 10, 60, entryState.FontSize, 1, 10);
        fontSizeSlider.ValueChanged += (_, e) =>
        {
            if (!_playerStates.TryGetValue(player.SteamID, out var s) ||
                !s.Entries.TryGetValue(key, out var es))
                return;

            es.FontSize = e.NewValue;
            if (es.Entity is { IsValidEntity: true } entity)
            {
                entity.FontSize = e.NewValue;
                entity.FontSizeUpdated();
            }
            SaveUserPreferences(s);
        };
        builder.AddOption(fontSizeSlider);

        // Show/hide toggle
        var hiddenToggle = new ToggleMenuOption("Hidden", entryState.Hidden);
        hiddenToggle.ValueChanged += (_, e) =>
        {
            if (!_playerStates.TryGetValue(player.SteamID, out var s) ||
                !s.Entries.TryGetValue(key, out var es))
                return;

            es.Hidden = e.NewValue;
            if (es.Entity is { IsValidEntity: true } entity)
            {
                entity.Enabled = !e.NewValue;
                entity.EnabledUpdated();
            }
            SaveUserPreferences(s);
        };
        builder.AddOption(hiddenToggle);

        // Reset to defaults
        var resetButton = new ButtonMenuOption("Reset to Defaults");
        resetButton.Click += (_, args) =>
        {
            Core.Scheduler.NextWorldUpdate(() =>
            {
                if (!_entries.TryGetValue(key, out var entry) ||
                    !_playerStates.TryGetValue(args.Player.SteamID, out var s) ||
                    !s.Entries.TryGetValue(key, out var es))
                    return;

                es.X = entry.Config.DefaultX;
                es.Y = entry.Config.DefaultY;
                es.FontSize = entry.Config.DefaultFontSize;
                es.Color = NativeColor.FromBuiltin(entry.Config.DefaultColor);
                es.Hidden = false;

                if (es.Entity is { IsValidEntity: true } entity)
                {
                    entity.FontSize = es.FontSize;
                    entity.FontSizeUpdated();
                    entity.Enabled = true;
                    entity.EnabledUpdated();
                }

                SaveUserPreferences(s);
                args.Player.SendChat($"[HUD] {displayName} reset to defaults.");

                // Re-open menu to reflect updated values
                var currentMenu = menus.GetCurrentMenu(args.Player);
                if (currentMenu != null)
                    menus.CloseMenuForPlayer(args.Player, currentMenu);
                ShowEntryMenu(args.Player, key, displayName);
            });
            return ValueTask.CompletedTask;
        };
        builder.AddOption(resetButton);

        builder.EnableExit();
        menus.OpenMenuForPlayer(player, builder.Build());
    }
}

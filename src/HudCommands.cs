using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using WorldTextHud.Models;

namespace WorldTextHud;

public sealed partial class WorldTextHudPlugin
{
    private readonly List<string> _registeredCommands = [];

    private void RegisterAllCommands()
    {
        RegisterCommand("hudmenu", ShowHudMenuCommand, helpText: "Opens the HUD settings menu.");

        foreach (var key in _entries.Keys)
            RegisterEntryCommand(key);
    }

    private void UnregisterAllCommands()
    {
        foreach (var commandName in _registeredCommands)
        {
            if (Core.Command.IsCommandRegistered(commandName))
                Core.Command.UnregisterCommand(commandName);
        }

        _registeredCommands.Clear();
    }

    private void RegisterEntryCommand(string key)
    {
        RegisterCommand(key, context => OnEntryCommand(context, key), helpText: $"Sets the {key} HUD position. Usage: !{key} <x> <y>");
    }

    private void UnregisterEntryCommand(string key)
    {
        if (_registeredCommands.Remove(key) && Core.Command.IsCommandRegistered(key))
            Core.Command.UnregisterCommand(key);
    }

    private void RegisterCommand(
        string commandName,
        ICommandService.CommandListener callback,
        string helpText = "")
    {
        if (Core.Command.IsCommandRegistered(commandName))
            return;

        Core.Command.RegisterCommand(commandName, callback, registerRaw: true, permission: string.Empty, helpText: helpText);
        _registeredCommands.Add(commandName);
    }

    private void OnEntryCommand(ICommandContext context, string key)
    {
        if (!context.IsSentByPlayer || context.Sender is not { IsValid: true } player)
            return;

        if (!_entries.TryGetValue(key, out var entry))
        {
            player.SendChat($"[HUD] Unknown HUD entry: {key}");
            return;
        }

        var state = GetOrCreatePlayerState(player);

        if (!state.Entries.TryGetValue(key, out var entryState))
        {
            player.SendChat($"[HUD] HUD entry '{entry.DisplayName}' is not active for you.");
            return;
        }

        if (context.Args.Length >= 2 &&
            float.TryParse(context.Args[0], out var x) &&
            float.TryParse(context.Args[1], out var y))
        {
            entryState.X = Math.Clamp(x, MinHudPosition, MaxHudPosition);
            entryState.Y = Math.Clamp(y, MinHudPosition, MaxHudPosition);
            SaveUserPreferences(state);
            player.SendChat($"[HUD] {entry.DisplayName} position set to ({entryState.X:F2}, {entryState.Y:F2}).");
        }
        else
        {
            player.SendChat($"[HUD] {entry.DisplayName} position: ({entryState.X:F2}, {entryState.Y:F2}). Usage: !{key} <x> <y>");
        }
    }

    private void ShowHudMenuCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is not { IsValid: true } player)
            return;

        ShowHudMenu(player);
    }
}

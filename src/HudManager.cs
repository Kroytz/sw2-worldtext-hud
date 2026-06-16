using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using WorldTextHud.Contracts;
using WorldTextHud.Models;
using NativeColor = SwiftlyS2.Shared.Natives.Color;

namespace WorldTextHud;

public sealed partial class WorldTextHudPlugin
{
    /// <summary>
    /// All registered HUD entries, keyed by their unique key.
    /// </summary>
    private readonly Dictionary<string, RegisteredEntry> _entries = [];

    /// <summary>
    /// Per-player HUD state, keyed by SteamID.
    /// </summary>
    private readonly Dictionary<ulong, PlayerHudState> _playerStates = [];

    private IHudEntry RegisterEntry(HudEntryConfig config)
    {
        if (_entries.ContainsKey(config.Key))
            throw new InvalidOperationException($"HUD entry with key '{config.Key}' is already registered.");

        var entry = new RegisteredEntry(config);
        _entries[config.Key] = entry;

        // Create player states for existing players
        foreach (var player in Core.PlayerManager.GetAllValidPlayers())
        {
            if (player.IsFakeClient || player.SteamID == 0)
                continue;

            var state = GetOrCreatePlayerState(player);
            EnsurePlayerEntry(state, entry);
        }

        RegisterEntryCommand(config.Key);

        _logger?.LogInformation("Registered HUD entry: {Key} ({DisplayName})", config.Key, config.DisplayName);
        return new HudEntryHandle(entry, this);
    }

    private void UnregisterEntry(string key)
    {
        if (!_entries.Remove(key, out var entry))
            return;

        // Destroy all entities for this entry
        foreach (var playerState in _playerStates.Values)
        {
            if (playerState.Entries.Remove(key, out var entryState))
                DestroyEntity(entryState);
        }

        UnregisterEntryCommand(key);
        _logger?.LogInformation("Unregistered HUD entry: {Key}", key);
    }

    private void SetEntryText(RegisteredEntry entry, string? text)
    {
        var content = text ?? string.Empty;
        entry.CurrentText = content;

        foreach (var playerState in _playerStates.Values)
        {
            if (!playerState.Entries.TryGetValue(entry.Key, out var entryState))
                continue;

            if (entryState.Hidden)
                continue;

            if (string.IsNullOrEmpty(content))
            {
                // Hide entity
                if (entryState.Entity is { IsValidEntity: true } entity)
                {
                    entity.Enabled = false;
                    entity.EnabledUpdated();
                }
                entryState.LastText = string.Empty;
                continue;
            }

            // Content changed — update or create entity
            if (entryState.LastText == content)
                continue;

            entryState.LastText = content;

            if (entryState.Entity == null || !entryState.Entity.IsValidEntity)
            {
                var player = Core.PlayerManager.GetPlayer(playerState.PlayerId);
                if (player != null && player.IsValid)
                    CreateEntityForPlayer(player, entry, entryState);
            }

            if (entryState.Entity is { IsValidEntity: true } ent)
            {
                ent.MessageText = content;
                ent.MessageTextUpdated();
                ent.Enabled = true;
                ent.EnabledUpdated();
            }
        }
    }

    private PlayerHudState GetOrCreatePlayerState(IPlayer player)
    {
        var steamId = player.SteamID != 0 ? player.SteamID : 0x8000000000000000UL | (ulong)player.SessionId;

        if (!_playerStates.TryGetValue(steamId, out var state))
        {
            state = new PlayerHudState(player.PlayerID, steamId);
            _playerStates[steamId] = state;

            // Ensure entries for all registered HUDs
            foreach (var entry in _entries.Values)
                EnsurePlayerEntry(state, entry);

            BeginLoadUserPreferences(state, player);
        }
        else if (state.PlayerId != player.PlayerID)
        {
            state.PlayerId = player.PlayerID;
        }

        return state;
    }

    private void EnsurePlayerEntry(PlayerHudState state, RegisteredEntry entry)
    {
        if (!state.Entries.ContainsKey(entry.Key))
        {
            state.Entries[entry.Key] = new PlayerEntryState(
                entry.Config.DefaultX,
                entry.Config.DefaultY,
                entry.Config.DefaultFontSize,
                NativeColor.FromBuiltin(entry.Config.DefaultColor));
        }
    }

    private CPointWorldText? CreateEntityForPlayer(IPlayer player, RegisteredEntry entry, PlayerEntryState entryState)
    {
        try
        {
            var entity = Core.EntitySystem.CreateEntity<CPointWorldText>();
            if (!entity.IsValid || !entity.IsValidEntity)
                return null;

            entity.FontSize = entryState.FontSize;
            entity.Color = entryState.Color;
            entity.Fullbright = true;
            entity.WorldUnitsPerPx = 0.005f;
            entity.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_LEFT;
            entity.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_BOTTOM;
            entity.MessageText = entryState.LastText;
            entity.Enabled = !entryState.Hidden;
            entity.DispatchSpawn();

            // Only visible to this player
            entity.SetTransmitState(false);
            entity.SetTransmitState(true, player.PlayerID);

            entryState.Entity = entity;
            return entity;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void DestroyEntity(PlayerEntryState entryState)
    {
        if (entryState.Entity == null)
            return;

        try
        {
            if (entryState.Entity.IsValidEntity)
                entryState.Entity.Despawn();
        }
        catch (InvalidOperationException)
        {
        }

        entryState.Entity = null;
    }

    private void CleanupAllEntities()
    {
        foreach (var playerState in _playerStates.Values)
        {
            foreach (var entryState in playerState.Entries.Values)
                DestroyEntity(entryState);
        }
    }

    private void CleanupPlayer(int playerId)
    {
        var player = Core.PlayerManager.GetPlayer(playerId);
        if (player == null)
            return;

        var steamId = player.SteamID != 0 ? player.SteamID : 0x8000000000000000UL | (ulong)player.SessionId;

        if (_playerStates.Remove(steamId, out var state))
        {
            foreach (var entryState in state.Entries.Values)
                DestroyEntity(entryState);

            SaveUserPreferences(state);
        }
    }

    private void SetEntryPlayerText(RegisteredEntry entry, ulong steamId, string? text)
    {
        var content = text ?? string.Empty;

        if (!_playerStates.TryGetValue(steamId, out var playerState))
        {
            var player = Core.PlayerManager.GetAllValidPlayers()
                .FirstOrDefault(candidate => GetPlayerStateKey(candidate) == steamId);
            if (player == null)
                return;

            playerState = GetOrCreatePlayerState(player);
        }

        if (!playerState.Entries.TryGetValue(entry.Key, out var entryState))
        {
            EnsurePlayerEntry(playerState, entry);
            if (!playerState.Entries.TryGetValue(entry.Key, out entryState))
                return;
        }

        if (!TryResolvePlayer(playerState, out var owner))
            return;

        playerState.PlayerId = owner.PlayerID;

        if (entryState.Hidden)
            return;

        if (string.IsNullOrEmpty(content))
        {
            if (entryState.Entity is { IsValidEntity: true } entity)
            {
                entity.Enabled = false;
                entity.EnabledUpdated();
            }
            entryState.LastText = string.Empty;
            return;
        }

        if (entryState.LastText == content)
            return;

        entryState.LastText = content;

        if (entryState.Entity == null || !entryState.Entity.IsValidEntity)
        {
            CreateEntityForPlayer(owner, entry, entryState);
        }

        if (entryState.Entity is { IsValidEntity: true } ent)
        {
            ent.MessageText = content;
            ent.MessageTextUpdated();
            ent.Enabled = true;
            ent.EnabledUpdated();
        }
    }

    private static ulong GetPlayerStateKey(IPlayer player)
    {
        return player.SteamID != 0 ? player.SteamID : 0x8000000000000000UL | (ulong)player.SessionId;
    }

    private static bool IsSamePlayerState(IPlayer player, PlayerHudState state)
    {
        return GetPlayerStateKey(player) == state.SteamId;
    }

    private bool TryResolvePlayer(PlayerHudState state, out IPlayer player)
    {
        var current = Core.PlayerManager.GetPlayer(state.PlayerId);
        if (current != null && current.IsValid && IsSamePlayerState(current, state))
        {
            player = current;
            return true;
        }

        foreach (var candidate in Core.PlayerManager.GetAllValidPlayers())
        {
            if (!IsSamePlayerState(candidate, state))
                continue;

            player = candidate;
            return true;
        }

        player = null!;
        return false;
    }

    /// <summary>
    /// IHudEntry handle implementation. Holds a reference to the RegisteredEntry and calls back
    /// into the plugin for SetText.
    /// </summary>
    private sealed class HudEntryHandle(RegisteredEntry entry, WorldTextHudPlugin plugin) : IHudEntry
    {
        public string Key => entry.Key;

        public void SetText(string? text)
        {
            plugin.SetEntryText(entry, text);
        }

        public void SetPlayerText(ulong steamId, string? text)
        {
            plugin.SetEntryPlayerText(entry, steamId, text);
        }
    }
}

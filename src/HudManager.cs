using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using WorldTextHud.Contracts;
using WorldTextHud.Models;
using NativeColor = SwiftlyS2.Shared.Natives.Color;

namespace WorldTextHud;

public sealed partial class WorldTextHudPlugin
{
    private const float MinHudPosition = -10.0f;
    private const float MaxHudPosition = 10.0f;

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

        if (!string.IsNullOrEmpty(content))
            EnsureEntryForOnlinePlayers(entry);

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
                    CreateEntityForPlayer(player, playerState, entry, entryState);
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

    private void EnsureEntryForOnlinePlayers(RegisteredEntry entry)
    {
        foreach (var player in Core.PlayerManager.GetAllValidPlayers())
        {
            if (!player.IsValid)
                continue;

            var state = GetOrCreatePlayerState(player);
            EnsurePlayerEntry(state, entry);
        }
    }

    private void SyncPlayerStateForEntries(IPlayer player)
    {
        var state = GetOrCreatePlayerState(player);

        foreach (var entry in _entries.Values)
        {
            EnsurePlayerEntry(state, entry);

            if (!string.IsNullOrEmpty(entry.CurrentText))
                SetEntryPlayerText(entry, state.SteamId, entry.CurrentText);
        }
    }

    /// <summary>
    /// Rebuilds the player's HUD against their current pawn. Called on pawn spawn (mirrors
    /// CS2Fixes ZEPlayer::OnSpawn recreating the point_orient and HUD text). The old text entities
    /// were parented to the now-stale orient, so they are destroyed and recreated.
    /// </summary>
    private void RebuildPlayerHudForCurrentPawn(CEntityInstance pawnEntity)
    {
        var pawn = pawnEntity.As<CCSPlayerPawn>();
        if (!pawn.IsValid)
            return;

        var player = Core.PlayerManager.GetPlayerFromPawn(pawn);
        if (player == null || !player.IsValid)
            return;

        var state = GetOrCreatePlayerState(player);

        // Drop the old orient (parented to the previous pawn) so it is recreated on next use.
        if (state.Orient is { IsValidEntity: true } oldOrient)
            oldOrient.Despawn();
        state.Orient = null;
        state.OrientPawnAddress = 0;

        // Destroy text entities that were parented to the old orient.
        foreach (var entryState in state.Entries.Values)
        {
            DestroyEntity(entryState);
            entryState.LastText = string.Empty;
        }

        SyncPlayerStateForEntries(player);
    }

    private CPointWorldText? CreateEntityForPlayer(IPlayer player, PlayerHudState state, RegisteredEntry entry, PlayerEntryState entryState)
    {
        try
        {
            // The text is parented to a per-player point_orient that auto-tracks the eye
            // orientation (CS2Fixes: ZEPlayer::CreatePointOrient + CreateEntwatchHud). Ensure it
            // exists before spawning the text.
            var orient = EnsurePointOrient(player, state);
            if (orient == null)
                return null;

            var entity = Core.EntitySystem.CreateEntityByDesignerName<CPointWorldText>("point_worldtext");
            if (!entity.IsValid || !entity.IsValidEntity)
                return null;

            entity.FontSize = entryState.FontSize;

            // CS2Fixes caps alpha at 254 so the text stays visible through world geometry / items
            // (ZEPlayer::CreateEntwatchHud forces 255 -> 254).
            var color = entryState.Color;
            entity.Color = color.A == 255 ? new NativeColor((int)color.R, (int)color.G, (int)color.B, 254) : color;

            entity.Fullbright = true;

            entity.WorldUnitsPerPx = 0.005f;

            // CS2Fixes sets m_FontName = "Verdana Bold". point_worldtext renders nothing when the
            // font name is empty, so this is required for the text to show.
            entity.FontName = "Verdana Bold";

            entity.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_LEFT;
            // CS2Fixes uses POINT_WORLD_TEXT_JUSTIFY_VERTICAL_TOP.
            entity.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_TOP;
            entity.MessageText = entryState.LastText;
            entity.Enabled = !entryState.Hidden;
            entity.DispatchSpawn();

            // Update every shits
            entity.MessageTextUpdated();
            entity.JustifyVerticalUpdated();
            entity.JustifyHorizontalUpdated();
            entity.FontNameUpdated();
            entity.WorldUnitsPerPxUpdated();
            entity.FullbrightUpdated();
            entity.ColorUpdated();
            entity.EnabledUpdated();

            // Parent the text to the orient so it follows the player automatically
            // (CS2Fixes: pText->AcceptInput("SetParent", "!activator", pOrient)).
            entity.AcceptInput("SetParent", "!activator", orient);

            // Place the text relative to the orient's current transform
            // (CS2Fixes: origin/angles derived from pOrient->GetAbsOrigin/GetAbsRotation).
            var origin = orient.AbsOrigin ?? Vector.Zero;
            var vmangles = orient.AbsRotation ?? QAngle.Zero;
            vmangles.ToDirectionVectors(out var forward, out var right, out var up);

            origin += forward * HudForwardDistance;
            origin += right * entryState.X;
            origin -= up * entryState.Y;

            // CS2Fixes orientation: text plane always faces the eye.
            //   angles.x = 0
            //   angles.y = eyeYaw - 90
            //   angles.z = -eyePitch + 90
            var angles = new QAngle(0.0f, vmangles.Yaw - 90.0f, -vmangles.Pitch + 90.0f);
            entity.Teleport(origin, angles, null);

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

    /// <summary>
    /// Returns the player's point_orient, rebuilding it if missing or parented to a stale pawn
    /// (e.g. after respawn). Mirrors CS2Fixes ZEPlayer::CreatePointOrient.
    /// </summary>
    private CPointOrient? EnsurePointOrient(IPlayer player, PlayerHudState state)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return state.Orient is { IsValidEntity: true } existing ? existing : null;

        if (state.Orient is { IsValidEntity: true } orient && state.OrientPawnAddress == pawn.Address)
            return orient;

        return CreatePointOrient(player, state);
    }

    /// <summary>
    /// Creates the per-player point_orient that tracks the owner's eye orientation. The text
    /// entities are parented to it. Mirrors CS2Fixes ZEPlayer::CreatePointOrient.
    /// </summary>
    private CPointOrient? CreatePointOrient(IPlayer player, PlayerHudState state)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return null;

        // CS2Fixes removes any existing orient before creating a new one.
        if (state.Orient is { IsValidEntity: true } oldOrient)
            oldOrient.Despawn();

        var orient = Core.EntitySystem.CreateEntityByDesignerName<CPointOrient>("point_orient");
        if (!orient.IsValid || !orient.IsValidEntity)
        {
            state.Orient = null;
            state.OrientPawnAddress = 0;
            return null;
        }

        orient.Active = true;
        orient.GoalDirection = PointOrientGoalDirectionType_t.eEyesForward;
        orient.DispatchSpawn();

        // Spawn at the eye position (CS2Fixes: Teleport to GetEyePosition).
        if (pawn.EyePosition is { } eyeOrigin)
            orient.Teleport(eyeOrigin, null, null);

        // Parent to the pawn and target the pawn so eEyesForward tracks it
        // (CS2Fixes: AcceptInput "SetParent"/"SetTarget" "!activator" pPawn).
        orient.AcceptInput("SetParent", "!activator", pawn);
        orient.AcceptInput("SetTarget", "!activator", pawn);

        state.Orient = orient;
        state.OrientPawnAddress = pawn.Address;
        return orient;
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

            DestroyOrient(playerState);
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

            DestroyOrient(state);

            SaveUserPreferences(state);
        }
    }

    private static void DestroyOrient(PlayerHudState state)
    {
        if (state.Orient is not { IsValidEntity: true } orient)
        {
            state.Orient = null;
            state.OrientPawnAddress = 0;
            return;
        }

        try
        {
            orient.Despawn();
        }
        catch (InvalidOperationException)
        {
        }

        state.Orient = null;
        state.OrientPawnAddress = 0;
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
            CreateEntityForPlayer(owner, playerState, entry, entryState);
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

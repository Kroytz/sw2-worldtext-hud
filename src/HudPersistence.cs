using System.Text.Json;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;
using WorldTextHud.Models;
using NativeColor = SwiftlyS2.Shared.Natives.Color;

namespace WorldTextHud;

public sealed partial class WorldTextHudPlugin
{
    private readonly SemaphoreSlim _persistenceLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private void BeginLoadUserPreferences(PlayerHudState state, IPlayer player)
    {
        if (player.IsFakeClient || player.SteamID == 0)
            return;

        var steamId = player.SteamID;
        var path = GetPreferencesPath(steamId);

        _ = Task.Run(async () =>
        {
            try
            {
                if (!File.Exists(path))
                    return;

                var json = await File.ReadAllTextAsync(path);
                var prefs = JsonSerializer.Deserialize<Dictionary<string, EntryPreferences>>(json, JsonOptions);
                if (prefs == null)
                    return;

                Core.Scheduler.NextTick(() =>
                {
                    if (!_playerStates.TryGetValue(steamId, out var currentState) ||
                        currentState.PlayerId != state.PlayerId)
                        return;

                    foreach (var (key, pref) in prefs)
                    {
                        if (!currentState.Entries.TryGetValue(key, out var entryState))
                            continue;

                        if (pref.X.HasValue) entryState.X = pref.X.Value;
                        if (pref.Y.HasValue) entryState.Y = pref.Y.Value;
                        if (pref.FontSize.HasValue) entryState.FontSize = pref.FontSize.Value;
                        if (pref.ColorHex != null) entryState.Color = ColorFromHex(pref.ColorHex);
                        if (pref.Hidden.HasValue) entryState.Hidden = pref.Hidden.Value;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load HUD preferences for {SteamId}", steamId);
            }
        });
    }

    private void SaveUserPreferences(PlayerHudState state)
    {
        if (state.SteamId == 0)
            return;

        var path = GetPreferencesPath(state.SteamId);
        var prefs = new Dictionary<string, EntryPreferences>();

        foreach (var (key, entryState) in state.Entries)
        {
            prefs[key] = new EntryPreferences
            {
                X = entryState.X,
                Y = entryState.Y,
                FontSize = entryState.FontSize,
                ColorHex = ColorToHex(entryState.Color),
                Hidden = entryState.Hidden
            };
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _persistenceLock.WaitAsync();
                try
                {
                    var dir = Path.GetDirectoryName(path)!;
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var json = JsonSerializer.Serialize(prefs, JsonOptions);
                    await File.WriteAllTextAsync(path, json);
                }
                finally
                {
                    _persistenceLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to save HUD preferences for {SteamId}", state.SteamId);
            }
        });
    }

    private string GetPreferencesPath(ulong steamId)
    {
        return Path.Combine(Core.PluginDataDirectory, "preferences", $"{steamId}.json");
    }

    private static string ColorToHex(NativeColor color)
    {
        return color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }

    private static NativeColor ColorFromHex(string hex)
    {
        try
        {
            return NativeColor.FromHex(hex);
        }
        catch
        {
            return NativeColor.White;
        }
    }

    private sealed class EntryPreferences
    {
        public float? X { get; set; }
        public float? Y { get; set; }
        public float? FontSize { get; set; }
        public string? ColorHex { get; set; }
        public bool? Hidden { get; set; }
    }
}

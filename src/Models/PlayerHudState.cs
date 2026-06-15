using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace WorldTextHud.Models;

internal sealed class PlayerEntryState
{
    public float X { get; set; }
    public float Y { get; set; }
    public float FontSize { get; set; }
    public Color Color { get; set; }
    public bool Hidden { get; set; }
    public CPointWorldText? Entity { get; set; }
    public string LastText { get; set; } = string.Empty;

    public PlayerEntryState(float x, float y, float fontSize, Color color)
    {
        X = x;
        Y = y;
        FontSize = fontSize;
        Color = color;
    }
}

internal sealed class PlayerHudState
{
    public int PlayerId { get; set; }
    public ulong SteamId { get; }
    public Dictionary<string, PlayerEntryState> Entries { get; } = [];

    public PlayerHudState(int playerId, ulong steamId)
    {
        PlayerId = playerId;
        SteamId = steamId;
    }
}

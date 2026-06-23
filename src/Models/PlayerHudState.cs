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

    /// <summary>
    /// Per-player point_orient entity that auto-tracks the owner's eye orientation
    /// (eEyesForward). All of this player's HUD text entities are parented to it, mirroring
    /// CS2Fixes' ZEPlayer::CreatePointOrient / CreateEntwatchHud.
    /// </summary>
    public CPointOrient? Orient { get; set; }

    /// <summary>
    /// Address of the pawn the orient is currently parented to. When the pawn changes
    /// (respawn), the orient and text entities must be rebuilt.
    /// </summary>
    public nint OrientPawnAddress { get; set; }

    public Dictionary<string, PlayerEntryState> Entries { get; } = [];

    public PlayerHudState(int playerId, ulong steamId)
    {
        PlayerId = playerId;
        SteamId = steamId;
    }
}

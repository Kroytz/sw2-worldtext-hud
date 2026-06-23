using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using WorldTextHud.Models;

namespace WorldTextHud;

public sealed partial class WorldTextHudPlugin
{
    // Mirrors CS2Fixes ZEPlayer::CreateEntwatchHud: forward offset from the eye (origin += forward * 7.0f).
    internal const float HudForwardDistance = 7.0f;

    /// <summary>
    /// Called every tick to keep each player's point_orient at their eye position, mirroring the
    /// per-frame loop in CS2Fixes CPlayerManager::UpdatePlayerStates. The orientation itself is
    /// tracked by the engine (eEyesForward); only the position needs to be refreshed each tick.
    /// The text entities are parented to the orient and follow automatically.
    /// </summary>
    private void OnTick()
    {
        foreach (var (steamKey, playerState) in _playerStates)
        {
            if (playerState.Orient is not { IsValidEntity: true } orient)
                continue;

            var player = Core.PlayerManager.GetPlayer(playerState.PlayerId);
            if (player == null || !player.IsValid || player.PlayerPawn is not { IsValid: true } pawn)
                continue;

            var eyeOrigin = pawn.EyePosition;
            if (eyeOrigin == null)
                continue;

            // CS2Fixes: Vector origin = pPawn->GetEyePosition(); pOrient->Teleport(&origin, nullptr, nullptr);
            orient.Teleport(eyeOrigin.Value, null, null);
        }
    }
}

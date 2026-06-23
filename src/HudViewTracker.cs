using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using WorldTextHud.Models;

namespace WorldTextHud;

public sealed partial class WorldTextHudPlugin
{
    // Mirrors CS2Fixes ZEPlayer::CreateEntwatchHud: forward offset from the eye (origin += forward * 7.0f).
    private const float HudForwardDistance = 7.0f;

    /// <summary>
    /// Called every tick to reposition all active HUD entities relative to their owner's eye,
    /// billboard-oriented to always face the player. Matches CS2Fixes' point_worldtext placement.
    /// </summary>
    private void OnTick()
    {
        foreach (var (steamKey, playerState) in _playerStates)
        {
            var player = Core.PlayerManager.GetPlayer(playerState.PlayerId);
            if (player == null || !player.IsValid || player.PlayerPawn is not { IsValid: true } pawn)
                continue;

            var eyeOrigin = pawn.EyePosition;
            if (eyeOrigin == null)
                continue;

            var eyeAngles = pawn.EyeAngles;
            eyeAngles.ToDirectionVectors(out var forward, out var right, out var up);

            // CS2Fixes orientation: text plane always faces the eye.
            //   angles.x = 0
            //   angles.y = eyeYaw - 90
            //   angles.z = -eyePitch + 90
            var angles = new QAngle(0.0f, eyeAngles.Yaw - 90.0f, -eyeAngles.Pitch + 90.0f);

            var origin = eyeOrigin.Value;

            foreach (var (entryKey, entryState) in playerState.Entries)
            {
                if (entryState.Hidden || entryState.Entity == null || !entryState.Entity.IsValidEntity)
                    continue;

                if (!_entries.TryGetValue(entryKey, out var entry))
                    continue;

                // CS2Fixes: origin += forward * 7.0f; origin += right * X; origin -= up * Y
                // X/Y are direct world-unit offsets (left/right, up/down).
                var position = origin
                    + forward * HudForwardDistance
                    + right * entryState.X
                    - up * entryState.Y;

                entryState.Entity.Teleport(position, angles, Vector.Zero);
            }
        }
    }
}

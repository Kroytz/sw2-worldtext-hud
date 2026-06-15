using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using WorldTextHud.Models;

namespace WorldTextHud;

public sealed partial class WorldTextHudPlugin
{
    private const float HudDistance = 20.0f;
    private const float AspectRatio = 1.7778f; // 16:9

    /// <summary>
    /// Called every tick to reposition all active HUD entities relative to their owner's camera.
    /// </summary>
    private void OnTick()
    {
        foreach (var (steamKey, playerState) in _playerStates)
        {
            var player = Core.PlayerManager.GetPlayer(playerState.PlayerId);
            if (player == null || !player.IsValid || player.PlayerPawn == null)
                continue;

            var sceneNode = player.PlayerPawn.CBodyComponent?.SceneNode;
            if (sceneNode == null)
                continue;

            var origin = sceneNode.AbsOrigin;
            var rotation = sceneNode.AbsRotation;

            rotation.ToDirectionVectors(out var forward, out var right, out var up);

            foreach (var (entryKey, entryState) in playerState.Entries)
            {
                if (entryState.Hidden || entryState.Entity == null || !entryState.Entity.IsValidEntity)
                    continue;

                if (!_entries.TryGetValue(entryKey, out var entry))
                    continue;

                var offsetX = (entryState.X - 0.5f) * HudDistance * AspectRatio;
                var offsetY = (0.5f - entryState.Y) * HudDistance;

                var position = origin + forward * HudDistance + right * offsetX + up * offsetY;
                entryState.Entity.Teleport(position, QAngle.Zero, Vector.Zero);
            }
        }
    }
}

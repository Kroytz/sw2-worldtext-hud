using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using WorldTextHud.Contracts;
using WorldTextHud.Models;

namespace WorldTextHud;

[PluginMetadata(
    Id = "sw2.worldtexthud",
    Name = "WorldText HUD",
    Version = "0.1.0",
    MinimumAPIVersion = "1.1.6",
    Author = "Kroytz",
    Description = "Provides a shared HUD system using CPointWorldText with view-angle tracking."
)]
public sealed partial class WorldTextHudPlugin : BasePlugin
{
    private readonly ILogger<WorldTextHudPlugin>? _logger;
    private bool _loaded;

    public static new ISwiftlyCore Core { get; private set; } = null!;

    public WorldTextHudPlugin(ISwiftlyCore core) : base(core)
    {
        _logger = core.LoggerFactory.CreateLogger<WorldTextHudPlugin>();
    }

    public override void Load(bool hotReload)
    {
        Core = base.Core;
        _loaded = true;

        Core.Event.OnTick += OnTick;
        Core.Event.OnMapLoad += OnMapLoad;
        Core.Event.OnMapUnload += OnMapUnload;
        Core.Event.OnClientDisconnected += OnClientDisconnected;
        Core.Event.OnClientPutInServer += OnClientPutInServer;

        RegisterAllCommands();
    }

    public override void Unload()
    {
        _loaded = false;

        Core.Event.OnTick -= OnTick;
        Core.Event.OnMapLoad -= OnMapLoad;
        Core.Event.OnMapUnload -= OnMapUnload;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        Core.Event.OnClientPutInServer -= OnClientPutInServer;

        UnregisterAllCommands();
        CleanupAllEntities();
        _playerStates.Clear();
        _entries.Clear();
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IHudManagerApi, HudManagerApi>(
            HudManagerApiConstants.InterfaceKey, new HudManagerApi(this));
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        CleanupAllEntities();
    }

    private void OnMapUnload(IOnMapUnloadEvent @event)
    {
        CleanupAllEntities();
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        CleanupPlayer(@event.PlayerId);
    }

    private void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        var playerId = @event.PlayerId;
        Core.Scheduler.NextWorldUpdate(() =>
        {
            if (!_loaded)
                return;

            var player = Core.PlayerManager.GetPlayer(playerId);
            if (player == null || !player.IsValid)
                return;

            SyncPlayerStateForEntries(player);
        });
    }

    /// <summary>
    /// Shared API implementation exposed to other plugins.
    /// </summary>
    private sealed class HudManagerApi(WorldTextHudPlugin plugin) : IHudManagerApi
    {
        public IHudEntry Register(HudEntryConfig config)
        {
            return plugin.RegisterEntry(config);
        }

        public void Unregister(string key)
        {
            plugin.UnregisterEntry(key);
        }
    }
}

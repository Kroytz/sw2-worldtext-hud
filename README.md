# sw2-worldtext-hud

A shared HUD management plugin for [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) that uses `CPointWorldText` entities with view-angle tracking to display fixed-position HUD elements on players' screens.

Other plugins can register HUD entries through the shared interface and update their content at any time. Players can customize the position, font size, and visibility of each registered HUD entry via chat commands or an in-game menu.

> 💡 **Note:** This project was ported through the collaborative efforts of multiple AI agents. The code may be a mess.

- **Shared HUD API** — Any plugin can register and update HUD entries via `IHudManagerApi`
- **View-angle tracking** — HUD elements stay fixed on screen by tracking each player's camera orientation every tick
- **Per-player customization** — Position, font size, and visibility are adjustable per player
- **Chat commands** — `!<key> <x> <y>` to set position, `!hudmenu` for a full settings menu
- **Persistence** — Player preferences are saved to disk and restored on reconnect

## Usage for Plugin Developers

### Registering a HUD Entry

```csharp
using WorldTextHud.Contracts;

public override void UseSharedInterface(IInterfaceManager interfaceManager)
{
    if (!interfaceManager.TryGetSharedInterface<IHudManagerApi>(
            HudManagerApiConstants.InterfaceKey, out var hudApi))
        return;

    var entry = hudApi.Register(new HudEntryConfig("myhud", "My HUD")
    {
        DefaultX = 0.0f,
        DefaultY = 0.5f,
        DefaultFontSize = 24.0f,
        DefaultColor = System.Drawing.Color.White
    });

    // Update content at any time
    entry.SetText("Hello, world!");
}
```

### Unregistering

```csharp
hudApi.Unregister("myhud");
```

### API Reference

| Type | Member | Description |
|------|--------|-------------|
| `IHudManagerApi` | `Register(HudEntryConfig)` | Register a HUD entry, returns `IHudEntry` |
| `IHudManagerApi` | `Unregister(string key)` | Unregister a HUD entry and destroy all entities |
| `IHudEntry` | `Key` | The unique key of this entry |
| `IHudEntry` | `SetText(string?)` | Set content for all players; pass `null` or empty to hide |

### HudEntryConfig Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Key` | `string` | *(required)* | Unique identifier, used as the chat command name |
| `DisplayName` | `string` | *(required)* | Display name shown in the HUD menu |
| `DefaultX` | `float` | `0.0f` | Default horizontal position (0.0–1.0, left to right) |
| `DefaultY` | `float` | `0.5f` | Default vertical position (0.0–1.0, bottom to top) |
| `DefaultFontSize` | `float` | `24.0f` | Default font size |
| `DefaultColor` | `System.Drawing.Color` | `White` | Default text color |

## Player Commands

| Command | Description |
|---------|-------------|
| `!<key>` | Show current position of the HUD entry |
| `!<key> <x> <y>` | Set position (e.g. `!myhud 0.3 0.7`) |
| `!hudmenu` | Open the HUD settings menu |

## Building

```bash
cd plugins/sw2-worldtext-hud
dotnet build -c Release
```

Output is placed in `build/`.

## License

GPL-3.0

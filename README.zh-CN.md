# sw2-worldtext-hud

一个基于 [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) 的共享 HUD 管理插件，使用 `CPointWorldText` 实体配合视角跟踪，在玩家屏幕上显示固定位置的 HUD 元素。

其他插件可以通过共享接口注册 HUD 条目并随时更新内容。玩家可以通过聊天命令或游戏内菜单自定义每个 HUD 条目的位置、字号和可见性。

## 功能

- **共享 HUD API** — 任何插件都可以通过 `IHudManagerApi` 注册和更新 HUD 条目
- **视角跟踪** — 通过每帧跟踪玩家相机朝向，使 HUD 元素固定在屏幕上
- **按玩家自定义** — 位置、字号、可见性均可按玩家单独调整
- **聊天命令** — `!<key> <x> <y>` 设置位置，`!hudmenu` 打开完整设置菜单
- **持久化** — 玩家偏好保存到磁盘，重连后自动恢复

## 插件开发者使用

### 注册 HUD 条目

```csharp
using WorldTextHud.Contracts;

public override void UseSharedInterface(IInterfaceManager interfaceManager)
{
    if (!interfaceManager.TryGetSharedInterface<IHudManagerApi>(
            HudManagerApiConstants.InterfaceKey, out var hudApi))
        return;

    var entry = hudApi.Register(new HudEntryConfig("myhud", "我的 HUD")
    {
        DefaultX = 0.0f,
        DefaultY = 0.5f,
        DefaultFontSize = 24.0f,
        DefaultColor = System.Drawing.Color.White
    });

    // 随时更新内容
    entry.SetText("你好，世界！");
}
```

### 注销

```csharp
hudApi.Unregister("myhud");
```

### API 参考

| 类型 | 成员 | 说明 |
|------|------|------|
| `IHudManagerApi` | `Register(HudEntryConfig)` | 注册 HUD 条目，返回 `IHudEntry` |
| `IHudManagerApi` | `Unregister(string key)` | 注销 HUD 条目并销毁所有实体 |
| `IHudEntry` | `Key` | 条目的唯一标识 |
| `IHudEntry` | `SetText(string?)` | 设置所有玩家的内容；传 `null` 或空字符串隐藏 |

### HudEntryConfig 属性

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Key` | `string` | *(必填)* | 唯一标识，同时用作聊天命令名 |
| `DisplayName` | `string` | *(必填)* | 显示名称，在 HUD 菜单中展示 |
| `DefaultX` | `float` | `0.0f` | 默认水平位置（0.0–1.0，从左到右） |
| `DefaultY` | `float` | `0.5f` | 默认垂直位置（0.0–1.0，从下到上） |
| `DefaultFontSize` | `float` | `24.0f` | 默认字号 |
| `DefaultColor` | `System.Drawing.Color` | `White` | 默认文字颜色 |

## 玩家命令

| 命令 | 说明 |
|------|------|
| `!<key>` | 显示该 HUD 条目的当前位置 |
| `!<key> <x> <y>` | 设置位置（如 `!myhud 0.3 0.7`） |
| `!hudmenu` | 打开 HUD 设置菜单 |

## 构建

```bash
cd plugins/sw2-worldtext-hud
dotnet build -c Release
```

输出在 `build/` 目录下。

## 许可证

GPL-3.0

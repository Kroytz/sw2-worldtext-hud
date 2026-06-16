namespace WorldTextHud.Contracts;

public static class HudManagerApiConstants
{
    public const string InterfaceKey = "WorldTextHud.Api.v1";
}

/// <summary>
/// HUD 条目配置，由注册方构造传入。
/// </summary>
public sealed class HudEntryConfig
{
    public string Key { get; }
    public string DisplayName { get; }
    public float DefaultX { get; init; } = 0.0f;
    public float DefaultY { get; init; } = 0.5f;
    public float DefaultFontSize { get; init; } = 24.0f;
    public System.Drawing.Color DefaultColor { get; init; } = System.Drawing.Color.White;

    public HudEntryConfig(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
    }
}

public interface IHudManagerApi
{
    /// <summary>
    /// 注册一个 HUD 条目，返回可更新的 entry 引用。
    /// </summary>
    IHudEntry Register(HudEntryConfig config);

    /// <summary>
    /// 注销一个 HUD 条目，销毁所有相关实体。
    /// </summary>
    void Unregister(string key);
}

public interface IHudEntry
{
    string Key { get; }

    /// <summary>
    /// 设置显示内容。对所有玩家生效。传空字符串或 null 隐藏。
    /// </summary>
    void SetText(string? text);

    /// <summary>
    /// 设置指定玩家的显示内容。仅更新该玩家的实体。传空字符串或 null 隐藏该玩家的 HUD。
    /// </summary>
    void SetPlayerText(ulong steamId, string? text);
}

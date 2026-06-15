using System.Drawing;
using WorldTextHud.Contracts;

namespace WorldTextHud.Models;

internal sealed class RegisteredEntry
{
    public HudEntryConfig Config { get; }
    public string Key => Config.Key;
    public string DisplayName => Config.DisplayName;
    public string CurrentText { get; set; } = string.Empty;

    public RegisteredEntry(HudEntryConfig config)
    {
        Config = config;
    }
}

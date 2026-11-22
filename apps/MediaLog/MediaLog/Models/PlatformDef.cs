namespace MediaLog.Models;

/// <summary>A game console / platform, shown as a small colored badge on game entries.</summary>
public sealed class PlatformDef
{
    public string Name { get; set; } = "";

    /// <summary>Badge text, e.g. "SNES".</summary>
    public string Short { get; set; } = "";

    public string Color { get; set; } = "#455A64";
}

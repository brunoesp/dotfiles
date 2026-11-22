namespace MediaLog.Models;

/// <summary>A media type as stored in the library (built-in defaults and user-created types).</summary>
public sealed class MediaTypeDef
{
    /// <summary>Stable identifier referenced by <see cref="MediaEntry.Kind"/>.</summary>
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";

    /// <summary>Hex color, e.g. <c>#FF1744</c>.</summary>
    public string Color { get; set; } = "#78909C";

    /// <summary>Key into <see cref="TypeIcons"/>.</summary>
    public string Icon { get; set; } = "dots";

    /// <summary>
    /// Default colors of earlier versions: built-in types still using one of them are moved to
    /// the current default (colors the user picked are left alone).
    /// </summary>
    public static readonly System.Collections.Generic.Dictionary<string, string[]> PreviousDefaultColors = new()
    {
        ["Movie"] = ["#E53935", "#FF1744"],
        ["AnimeMovie"] = ["#FF4FA3"],
        ["Series"] = ["#8E24AA", "#AA00FF"],
        ["Anime"] = ["#D81B60", "#F000B8"],
        ["Book"] = ["#1E88E5", "#2962FF"],
        ["TechBook"] = ["#00E5FF", "#651FFF"],
        ["Comic"] = ["#FB8C00", "#FF6D00"],
        ["Game"] = ["#00C853", "#43A047"],
        ["Music"] = ["#00ACC1", "#00B0FF"],
        ["Podcast"] = ["#6D4C41", "#9ACD00"],
        ["Documentary"] = ["#F9A825", "#FFC400"],
        ["Audiobook"] = ["#00BFA5", "#3949AB"],
        ["Other"] = ["#757575", "#78909C"],
    };

    public static MediaTypeDef[] Defaults() =>
    [
        new() { Id = "Movie", Label = "Movie", Color = "#F00020", Icon = "film" },
        new() { Id = "AnimeMovie", Label = "Anime Movie", Color = "#FF4FA8", Icon = "clapper" },
        new() { Id = "Series", Label = "TV Series", Color = "#5B21FF", Icon = "tv" },
        new() { Id = "Anime", Label = "Anime", Color = "#E600FF", Icon = "star" },
        new() { Id = "Book", Label = "Book", Color = "#0A6CFF", Icon = "book" },
        new() { Id = "TechBook", Label = "Tech Book", Color = "#18D4F0", Icon = "techbook" },
        new() { Id = "Comic", Label = "Comic", Color = "#FF7A00", Icon = "chat" },
        new() { Id = "Game", Label = "Game", Color = "#00D84A", Icon = "gamepad" },
        new() { Id = "Music", Label = "Album", Color = "#9C00B8", Icon = "music" },
        new() { Id = "Podcast", Label = "Podcast", Color = "#8BC700", Icon = "mic" },
        new() { Id = "Documentary", Label = "Documentary", Color = "#FFC800", Icon = "video" },
        new() { Id = "Audiobook", Label = "Audiobook", Color = "#00956A", Icon = "headphones" },
        new() { Id = "Other", Label = "Other", Color = "#8C8C9E", Icon = "dots" },
    ];
}

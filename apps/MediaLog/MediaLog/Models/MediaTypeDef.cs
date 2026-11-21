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

    public static MediaTypeDef[] Defaults() =>
    [
        new() { Id = "Movie", Label = "Movie", Color = "#FF1744", Icon = "film" },
        new() { Id = "AnimeMovie", Label = "Anime Movie", Color = "#FF4FA3", Icon = "clapper" },
        new() { Id = "Series", Label = "TV Series", Color = "#AA00FF", Icon = "tv" },
        new() { Id = "Anime", Label = "Anime", Color = "#F000B8", Icon = "star" },
        new() { Id = "Book", Label = "Book", Color = "#2962FF", Icon = "book" },
        new() { Id = "TechBook", Label = "Tech Book", Color = "#651FFF", Icon = "techbook" },
        new() { Id = "Comic", Label = "Comic", Color = "#FF6D00", Icon = "chat" },
        new() { Id = "Game", Label = "Game", Color = "#00C853", Icon = "gamepad" },
        new() { Id = "Music", Label = "Album", Color = "#00B0FF", Icon = "music" },
        new() { Id = "Podcast", Label = "Podcast", Color = "#9ACD00", Icon = "mic" },
        new() { Id = "Documentary", Label = "Documentary", Color = "#FFC400", Icon = "video" },
        new() { Id = "Audiobook", Label = "Audiobook", Color = "#00BFA5", Icon = "headphones" },
        new() { Id = "Other", Label = "Other", Color = "#78909C", Icon = "dots" },
    ];
}

using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace MediaLog.Models;

/// <summary>An icon that can be assigned to a media type.</summary>
public sealed record TypeIcon(string Key, Geometry Geometry);

/// <summary>Stroke icons (24×24 grid) available for media types.</summary>
public static class TypeIcons
{
    private static TypeIcon I(string key, string data) => new(key, Geometry.Parse(data));

    public static IReadOnlyList<TypeIcon> All { get; } =
    [
        I("film", "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M7 3v18 M17 3v18 M3 12h18 M3 7.5h4 M3 16.5h4 M17 7.5h4 M17 16.5h4"),
        I("clapper", "M3 10h18v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M3 10L2.5 7.6a2 2 0 0 1 1.5-2.4L16.8 2.4a2 2 0 0 1 2.4 1.5L19.6 6z M7.2 4.4l3 4.2 M12.6 3.3l3 4.2"),
        I("tv", "M4 7h16a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2z M17 2l-5 5-5-5"),
        I("star", "M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01z"),
        I("book", "M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"),
        I("techbook", "M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z M4 19.5A2.5 2.5 0 0 1 6.5 17H20 M10.5 6.5L8 9l2.5 2.5 M14 6.5L16.5 9 14 11.5"),
        I("chat", "M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z M8 9h8 M8 13h5"),
        I("gamepad", "M7 6h10a5 5 0 0 1 5 5v2a5 5 0 0 1-9 3h-2a5 5 0 0 1-9-3v-2a5 5 0 0 1 5-5z M6 12h4 M8 10v4 M15 11h.01 M18 13h.01"),
        I("music", "M9 18V5l12-2v13 M6 15a3 3 0 1 1 0 6 3 3 0 0 1 0-6z M18 13a3 3 0 1 1 0 6 3 3 0 0 1 0-6z"),
        I("mic", "M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3z M19 10v2a7 7 0 0 1-14 0v-2 M12 19v3 M8 22h8"),
        I("video", "M4 6h10a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M16 10.5l6-3.5v10l-6-3.5"),
        I("headphones", "M3 14h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a9 9 0 0 1 18 0v7a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3"),
        I("code", "M16 18l6-6-6-6 M8 6l-6 6 6 6"),
        I("article", "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M7 8h10 M7 12h10 M7 16h6"),
        I("course", "M22 10L12 5 2 10l10 5 10-5z M6 12v5c3 2 9 2 12 0v-5 M22 10v6"),
        I("dice", "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M8 8h.01 M16 8h.01 M12 12h.01 M8 16h.01 M16 16h.01"),
        I("ticket", "M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2z M13 5v2 M13 11v2 M13 17v2"),
        I("camera", "M4 7h3l2-3h6l2 3h3a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2z M12 10a3.5 3.5 0 1 1 0 7 3.5 3.5 0 0 1 0-7z"),
        I("globe", "M12 2a10 10 0 1 1 0 20 10 10 0 0 1 0-20z M2 12h20 M12 2a15 15 0 0 1 0 20 15 15 0 0 1 0-20z"),
        I("trophy", "M8 21h8 M12 17v4 M7 4h10v5a5 5 0 0 1-10 0z M7 6H4a3 3 0 0 0 3 4 M17 6h3a3 3 0 0 1-3 4"),
        I("heart", "M12 21l-7-7a5 5 0 0 1 7-7.1A5 5 0 0 1 19 14z"),
        I("dots", "M12 2a10 10 0 1 1 0 20 10 10 0 0 1 0-20z M8 12h.01 M12 12h.01 M16 12h.01"),
    ];

    public static TypeIcon Get(string? key) => All.FirstOrDefault(i => i.Key == key) ?? All[^1];
}

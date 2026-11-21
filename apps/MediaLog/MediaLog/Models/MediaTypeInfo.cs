using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MediaLog.Controls;

namespace MediaLog.Models;

/// <summary>Display metadata (label, color, icon) for a <see cref="MediaKind"/>.</summary>
public sealed class MediaTypeInfo
{
    private MediaTypeInfo(MediaKind kind, string label, string color, string icon)
    {
        Kind = kind;
        Label = label;
        Color = Color.Parse(color);
        Brush = new ImmutableSolidColorBrush(Color);
        SoftBrush = new ImmutableSolidColorBrush(Color, 0.18);
        // Dark foreground on light colors (yellow, lime…), white on the rest.
        var luminance = (0.2126 * Color.R + 0.7152 * Color.G + 0.0722 * Color.B) / 255;
        OnBrush = luminance > 0.6 ? new ImmutableSolidColorBrush(Color.Parse("#1A1A1A")) : Brushes.White;
        Icon = Geometry.Parse(icon);
    }

    public MediaKind Kind { get; }
    public string Label { get; }
    public Color Color { get; }
    public IBrush Brush { get; }

    /// <summary>Translucent version of <see cref="Brush"/> used for chip / badge backgrounds.</summary>
    public IBrush SoftBrush { get; }

    /// <summary>Readable foreground to draw on top of <see cref="Brush"/>.</summary>
    public IBrush OnBrush { get; }

    /// <summary>Stroke icon drawn on a 24×24 grid (rendered by <see cref="StrokeIcon"/>).</summary>
    public Geometry Icon { get; }

    public override string ToString() => Label;

    public static IReadOnlyList<MediaTypeInfo> All { get; } =
    [
        new(MediaKind.Movie, "Movie", "#FF1744",
            "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M7 3v18 M17 3v18 M3 12h18 M3 7.5h4 M3 16.5h4 M17 7.5h4 M17 16.5h4"),
        new(MediaKind.Series, "TV Series", "#AA00FF",
            "M4 7h16a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2z M17 2l-5 5-5-5"),
        new(MediaKind.Book, "Book", "#2962FF",
            "M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"),
        new(MediaKind.Game, "Game", "#00C853",
            "M7 6h10a5 5 0 0 1 5 5v2a5 5 0 0 1-9 3h-2a5 5 0 0 1-9-3v-2a5 5 0 0 1 5-5z M6 12h4 M8 10v4 M15 11h.01 M18 13h.01"),
        new(MediaKind.Comic, "Comic", "#FF6D00",
            "M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z M8 9h8 M8 13h5"),
        new(MediaKind.Anime, "Anime", "#F000B8",
            "M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01z"),
        new(MediaKind.Music, "Album", "#00B0FF",
            "M9 18V5l12-2v13 M6 15a3 3 0 1 1 0 6 3 3 0 0 1 0-6z M18 13a3 3 0 1 1 0 6 3 3 0 0 1 0-6z"),
        new(MediaKind.Podcast, "Podcast", "#9ACD00",
            "M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3z M19 10v2a7 7 0 0 1-14 0v-2 M12 19v3 M8 22h8"),
        new(MediaKind.Documentary, "Documentary", "#FFC400",
            "M4 6h10a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M16 10.5l6-3.5v10l-6-3.5"),
        new(MediaKind.Audiobook, "Audiobook", "#00BFA5",
            "M3 14h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a9 9 0 0 1 18 0v7a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3"),
        new(MediaKind.Other, "Other", "#78909C",
            "M12 2a10 10 0 1 1 0 20 10 10 0 0 1 0-20z M8 12h.01 M12 12h.01 M16 12h.01"),
    ];

    public static MediaTypeInfo For(MediaKind kind) => All.FirstOrDefault(t => t.Kind == kind) ?? All[^1];
}

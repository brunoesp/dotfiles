using Avalonia.Media;
using Avalonia.Media.Immutable;
using MediaLog.Controls;

namespace MediaLog.Models;

/// <summary>Display data (label, color brushes, icon) for a media type.</summary>
public sealed class MediaTypeInfo
{
    public MediaTypeInfo(MediaTypeDef def)
    {
        Def = def;
        Color = Color.TryParse(def.Color, out var c) ? c : Colors.SlateGray;
        Brush = new ImmutableSolidColorBrush(Color);
        SoftBrush = new ImmutableSolidColorBrush(Color, 0.18);
        // Dark foreground on light colors (yellow, lime, cyan…), white on the rest.
        var luminance = (0.2126 * Color.R + 0.7152 * Color.G + 0.0722 * Color.B) / 255;
        OnBrush = luminance > 0.6 ? new ImmutableSolidColorBrush(Color.Parse("#1A1A1A")) : Brushes.White;
        Icon = TypeIcons.Get(def.Icon).Geometry;
    }

    public MediaTypeDef Def { get; }
    public string Id => Def.Id;
    public string Label => Def.Label;
    public Color Color { get; }
    public IBrush Brush { get; }

    /// <summary>Translucent version of <see cref="Brush"/> used for chip backgrounds.</summary>
    public IBrush SoftBrush { get; }

    /// <summary>Readable foreground to draw on top of <see cref="Brush"/>.</summary>
    public IBrush OnBrush { get; }

    /// <summary>Stroke icon drawn on a 24×24 grid (rendered by <see cref="StrokeIcon"/>).</summary>
    public Geometry Icon { get; }

    public override string ToString() => Label;
}

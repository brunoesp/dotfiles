using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace MediaLog.Models;

/// <summary>Display data for a console badge.</summary>
public sealed class PlatformInfo
{
    public PlatformInfo(PlatformDef def)
    {
        Def = def;
        var color = Color.TryParse(def.Color, out var c) ? c : Colors.SlateGray;
        Brush = new ImmutableSolidColorBrush(color);
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
        OnBrush = luminance > 0.6 ? new ImmutableSolidColorBrush(Color.Parse("#1A1A1A")) : Brushes.White;
    }

    public PlatformDef Def { get; }
    public string Name => Def.Name;
    public string Short => Def.Short;
    public IBrush Brush { get; }
    public IBrush OnBrush { get; }

    public override string ToString() => Name;
}

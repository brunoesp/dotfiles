using Avalonia.Media;

namespace MediaLog.Controls;

/// <summary>UI icons (24×24 stroke geometry) referenced from XAML via x:Static.</summary>
public static class Icons
{
    public static Geometry Plus { get; } = Geometry.Parse("M12 5v14 M5 12h14");
    public static Geometry Trash { get; } = Geometry.Parse("M3 6h18 M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6 M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2 M10 11v6 M14 11v6");
    public static Geometry Menu { get; } = Geometry.Parse("M4 6h16 M4 12h16 M4 18h16");
    public static Geometry Layers { get; } = Geometry.Parse("M12 2l10 5-10 5-10-5z M2 17l10 5 10-5 M2 12l10 5 10-5");
    public static Geometry List { get; } = Geometry.Parse("M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01");
    public static Geometry Calendar { get; } = Geometry.Parse("M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M16 2v4 M8 2v4 M3 10h18");
    public static Geometry Search { get; } = Geometry.Parse("M11 3a8 8 0 1 1 0 16 8 8 0 0 1 0-16z M21 21l-4.3-4.3");
    public static Geometry Close { get; } = Geometry.Parse("M18 6L6 18 M6 6l12 12");
    public static Geometry Check { get; } = Geometry.Parse("M20 6L9 17l-5-5");
}

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
    public static Geometry Pencil { get; } = Geometry.Parse("M17 3a2.85 2.85 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5z");
    public static Geometry Tag { get; } = Geometry.Parse("M12 2H2v10l9.3 9.3a1 1 0 0 0 1.4 0l8.6-8.6a1 1 0 0 0 0-1.4z M7 7h.01");
    public static Geometry ChevronRight { get; } = Geometry.Parse("M9 18l6-6-6-6");
    public static Geometry ChevronDown { get; } = Geometry.Parse("M6 9l6 6 6-6");
    public static Geometry Pin { get; } = Geometry.Parse("M12 17v5 M9 10.8a2 2 0 0 1-1.1 1.8l-1.8.9A2 2 0 0 0 5 15.2V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.8a2 2 0 0 0-1.1-1.8l-1.8-.9A2 2 0 0 1 15 10.8V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z");
    public static Geometry ViewCards { get; } = Geometry.Parse("M4 4h16a1 1 0 0 1 1 1v4a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z M4 14h16a1 1 0 0 1 1 1v4a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-4a1 1 0 0 1 1-1z");
    public static Geometry ViewTiles { get; } = Geometry.Parse("M4 3h6a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z M14 3h6a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1h-6a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z M14 13h6a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1h-6a1 1 0 0 1-1-1v-6a1 1 0 0 1 1-1z M4 13h6a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-6a1 1 0 0 1 1-1z");
    public static Geometry ViewCompact { get; } = Geometry.Parse("M3 5h18 M3 9.67h18 M3 14.33h18 M3 19h18");
    public static Geometry Image { get; } = Geometry.Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M9 7.5a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3z M21 15l-5-5L5 21");
    public static Geometry Refresh { get; } = Geometry.Parse("M21 12a9 9 0 1 1-2.64-6.36L21 8 M21 3v5h-5");
    public static Geometry Import { get; } = Geometry.Parse("M12 3v12 M7 10l5 5 5-5 M5 21h14");
    public static Geometry Search { get; } = Geometry.Parse("M11 3a8 8 0 1 1 0 16 8 8 0 0 1 0-16z M21 21l-4.3-4.3");
    public static Geometry Close { get; } = Geometry.Parse("M18 6L6 18 M6 6l12 12");
    public static Geometry Check { get; } = Geometry.Parse("M20 6L9 17l-5-5");
}

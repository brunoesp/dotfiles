using System;
using System.Collections.Generic;
using System.Linq;
using MediaLog.Models;

namespace MediaLog.Services;

/// <summary>Built-in consoles: brand-ish colors, alternative names and libretro box-art collections.</summary>
public static class Platforms
{
    private sealed record Spec(string Name, string Short, string Color, string? Repo, string[] Aliases);

    private static Spec S(string name, string shortName, string color, string? repo, params string[] aliases) =>
        new(name, shortName, color, repo, aliases);

    private static readonly Spec[] Specs =
    [
        S("PlayStation 5", "PS5", "#0070D1", null, "ps5"),
        S("PlayStation 4", "PS4", "#003791", null, "ps4"),
        S("PlayStation 3", "PS3", "#2B2B2B", "Sony_-_PlayStation_3", "ps3"),
        S("PlayStation 2", "PS2", "#1D2C6B", "Sony_-_PlayStation_2", "ps2"),
        S("PlayStation", "PS1", "#8E8E93", "Sony_-_PlayStation", "ps1", "psx", "psone", "playstation1"),
        S("PSP", "PSP", "#4A4A4A", "Sony_-_PlayStation_Portable", "playstationportable"),
        S("PS Vita", "Vita", "#1B4F9C", null, "vita", "playstationvita"),
        S("Xbox Series X|S", "XSX", "#107C10", null, "xboxseriesx", "xboxseriess", "xboxseriesxs", "seriesx"),
        S("Xbox One", "XB1", "#0B5D0B", null, "xb1"),
        S("Xbox 360", "X360", "#6CB33E", null, "x360"),
        S("Xbox", "Xbox", "#9BC848", "Microsoft_-_Xbox", "originalxbox"),
        S("Nintendo Switch 2", "Switch 2", "#B3000F", null, "switch2", "ns2"),
        S("Nintendo Switch", "Switch", "#E60012", null, "switch", "ns"),
        S("Wii U", "Wii U", "#009AC7", null, "wiiu"),
        S("Wii", "Wii", "#34BEED", "Nintendo_-_Wii", "nintendowii"),
        S("GameCube", "GCN", "#6A0DAD", "Nintendo_-_GameCube", "gcn", "ngc", "nintendogamecube"),
        S("Nintendo 64", "N64", "#1E8C3A", "Nintendo_-_Nintendo_64", "n64"),
        S("Super Nintendo", "SNES", "#7B5AA6", "Nintendo_-_Super_Nintendo_Entertainment_System",
            "snes", "supernintendoentertainmentsystem", "superfamicom", "sfc", "supernes"),
        S("NES", "NES", "#C8102E", "Nintendo_-_Nintendo_Entertainment_System", "nintendoentertainmentsystem", "famicom"),
        S("Game Boy", "GB", "#8BAC0F", "Nintendo_-_Game_Boy", "gb"),
        S("Game Boy Color", "GBC", "#E0A800", "Nintendo_-_Game_Boy_Color", "gbc"),
        S("Game Boy Advance", "GBA", "#4B2BA0", "Nintendo_-_Game_Boy_Advance", "gba"),
        S("Nintendo DS", "DS", "#7D7F82", "Nintendo_-_Nintendo_DS", "ds", "nds"),
        S("Nintendo 3DS", "3DS", "#CE181E", "Nintendo_-_Nintendo_3DS", "3ds"),
        S("Sega Genesis", "Genesis", "#0060A8", "Sega_-_Mega_Drive_-_Genesis", "genesis", "megadrive", "segamegadrive", "md"),
        S("Sega Master System", "SMS", "#B71C1C", "Sega_-_Master_System_-_Mark_III", "mastersystem", "sms"),
        S("Sega Saturn", "Saturn", "#283593", "Sega_-_Saturn", "saturn"),
        S("Dreamcast", "DC", "#F26722", "Sega_-_Dreamcast", "segadreamcast"),
        S("Game Gear", "GG", "#37474F", "Sega_-_Game_Gear", "segagamegear", "gg"),
        S("Atari 2600", "2600", "#A0522D", "Atari_-_2600", "2600", "atari", "atarivcs", "vcs"),
        S("Atari 7800", "7800", "#8D4925", "Atari_-_7800", "7800"),
        S("TurboGrafx-16", "TG16", "#F15A24", "NEC_-_PC_Engine_-_TurboGrafx_16", "turbografx", "pcengine", "tg16"),
        S("Neo Geo", "Neo Geo", "#C9A227", "SNK_-_Neo_Geo", "neogeoaes", "aes"),
        S("PC", "PC", "#455A64", null, "windows", "microsoftwindows", "steam", "computer"),
        S("Steam Deck", "Deck", "#1A9FFF", null, "steamdeck"),
        S("Mac", "Mac", "#8E8E93", null, "macos", "macintosh", "osx"),
        S("Mobile", "Mobile", "#34A853", null, "ios", "android", "iphone", "phone"),
        S("Arcade", "Arcade", "#D81B60", null, "mame"),
    ];

    /// <summary>Platforms seeded into a new library.</summary>
    public static List<PlatformDef> Defaults() =>
        Specs.Select(s => new PlatformDef { Name = s.Name, Short = s.Short, Color = s.Color }).ToList();

    /// <summary>Lower-case letters and digits only.</summary>
    public static string Normalize(string text) => new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static Spec? SpecFor(string text)
    {
        var key = Normalize(text);
        if (key.Length == 0)
            return null;
        return Specs.FirstOrDefault(s => Normalize(s.Name) == key || Normalize(s.Short) == key || s.Aliases.Contains(key));
    }

    /// <summary>Finds a platform by name, badge text or a known alternative name ("Super Famicom" → SNES).</summary>
    public static PlatformDef? Find(IEnumerable<PlatformDef> platforms, string text)
    {
        var key = Normalize(text);
        if (key.Length == 0)
            return null;
        var list = platforms.ToList();
        var direct = list.FirstOrDefault(p => Normalize(p.Name) == key || Normalize(p.Short) == key);
        if (direct is not null)
            return direct;
        var spec = SpecFor(text);
        return spec is null ? null : list.FirstOrDefault(p => Normalize(p.Name) == Normalize(spec.Name));
    }

    /// <summary>A new platform for a name that isn't known yet, with a color picked from its name.</summary>
    public static PlatformDef Create(string name)
    {
        name = name.Trim();
        if (SpecFor(name) is { } spec)
            return new PlatformDef { Name = spec.Name, Short = spec.Short, Color = spec.Color };
        string[] palette = ["#FF1744", "#F000B8", "#AA00FF", "#651FFF", "#2962FF", "#00B0FF", "#00BFA5", "#00C853", "#9ACD00", "#FFAB00", "#FF6D00", "#8D6E63"];
        var hash = Normalize(name).Aggregate(17, (h, c) => unchecked(h * 31 + c));
        var shortName = name.Length <= 8 ? name : new string(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0])).ToArray());
        return new PlatformDef { Name = name, Short = shortName.Length > 0 ? shortName : name[..Math.Min(8, name.Length)], Color = palette[Math.Abs(hash % palette.Length)] };
    }

    /// <summary>The libretro-thumbnails repository with box art for this console, if any.</summary>
    public static string? BoxArtRepo(string? platform) => platform is null ? null : SpecFor(platform)?.Repo;
}

using MediaLog.Models;

namespace MediaLog.Services;

/// <summary>Broad kind of media, used to pick suitable online sources.</summary>
public enum MediaCategory { Film, Series, Anime, Book, Manga, Comic, Game, Music, Podcast, Audiobook, General }

public static class MediaCategories
{
    public static MediaCategory Of(MediaTypeInfo? type)
    {
        if (type is null)
            return MediaCategory.General;
        var label = type.Label.ToLowerInvariant();
        if (label.Contains("manga")) return MediaCategory.Manga;
        if (label.Contains("anime")) return MediaCategory.Anime;
        return type.Id switch
        {
            "Movie" or "Documentary" => MediaCategory.Film,
            "Series" => MediaCategory.Series,
            "Book" or "TechBook" => MediaCategory.Book,
            "Comic" => MediaCategory.Comic,
            "Game" => MediaCategory.Game,
            "Music" => MediaCategory.Music,
            "Podcast" => MediaCategory.Podcast,
            "Audiobook" => MediaCategory.Audiobook,
            _ => type.Def.Icon switch
            {
                "film" or "clapper" or "video" or "ticket" => MediaCategory.Film,
                "tv" => MediaCategory.Series,
                "book" or "techbook" or "article" or "course" => MediaCategory.Book,
                "chat" => MediaCategory.Comic,
                "gamepad" or "dice" => MediaCategory.Game,
                "music" => MediaCategory.Music,
                "mic" => MediaCategory.Podcast,
                "headphones" => MediaCategory.Audiobook,
                _ => MediaCategory.General,
            },
        };
    }
}

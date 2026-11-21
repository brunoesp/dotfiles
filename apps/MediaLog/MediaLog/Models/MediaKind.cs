namespace MediaLog.Models;

/// <summary>The kind of media an entry represents. Stored by name in the JSON file.</summary>
public enum MediaKind
{
    Movie,
    Series,
    Book,
    Game,
    Comic,
    Anime,
    Music,
    Podcast,
    Documentary,
    Audiobook,
    Other,
}

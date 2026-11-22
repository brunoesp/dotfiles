using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaLog.Models;

namespace MediaLog.Services;

/// <summary>An image suggestion for an entry.</summary>
public sealed record ImageResult(string Url, string Title, string Source);

/// <summary>
/// Finds cover / poster images by title using free, key-less public APIs, picking
/// sources that suit the media type. Sources that fail are skipped.
/// </summary>
public static class ImageSearchService
{
    private const int MaxResults = 30;

    /// <summary>A slow source must not hold up the others.</summary>
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(6);


    /// <summary>
    /// Queries all suitable sources in parallel and yields each source's results as soon as it
    /// answers (fast sources first), de-duplicated and capped at <see cref="MaxResults"/>.
    /// </summary>
    /// <param name="platform">Console of a game; retro consoles get box art from libretro.</param>
    public static async IAsyncEnumerable<IReadOnlyList<ImageResult>> SearchAsync(string query, MediaTypeInfo? type,
        string? platform, [EnumeratorCancellation] CancellationToken ct)
    {
        query = query.Trim();
        if (query.Length == 0)
            yield break;

        var sources = SourcesFor(MediaCategories.Of(type)).ToList();
        if (MediaCategories.Of(type) == MediaCategory.Game && Platforms.BoxArtRepo(platform) is { } repo)
            sources.Insert(0, (q, c) => RetroBoxArt.SearchAsync(q, repo, c));
        var tasks = sources.Select(source => SafeAsync(source, query, ct)).ToList();
        var seen = new HashSet<string>();
        var total = 0;
        await foreach (var task in Task.WhenEach(tasks).WithCancellation(ct).ConfigureAwait(false))
        {
            var batch = (await task.ConfigureAwait(false))
                .Where(r => seen.Add(r.Url))
                .Take(MaxResults - total)
                .ToList();
            total += batch.Count;
            if (batch.Count > 0)
                yield return batch;
            if (total >= MaxResults)
                yield break;
        }
    }

    private delegate Task<List<ImageResult>> Source(string query, CancellationToken ct);

    private static Source[] SourcesFor(MediaCategory category) => category switch
    {
        MediaCategory.Film => [ITunes("movie", "movie"), Wikipedia, TvMaze],
        MediaCategory.Series => [TvMaze, Wikipedia],
        MediaCategory.Anime => [AniList("ANIME"), Jikan("anime"), TvMaze, Wikipedia],
        MediaCategory.Book => [OpenLibrary, ITunes("ebook", "ebook"), Wikipedia],
        MediaCategory.Manga => [AniList("MANGA"), Jikan("manga"), OpenLibrary, Wikipedia],
        MediaCategory.Comic => [OpenLibrary, Wikipedia],
        MediaCategory.Game => [WikidataGames, Wikipedia, WikipediaWith("video game")],
        MediaCategory.Music => [ITunes("music", "album"), Wikipedia],
        MediaCategory.Podcast => [ITunes("podcast", "podcast"), Wikipedia],
        MediaCategory.Audiobook => [ITunes("audiobook", "audiobook"), OpenLibrary, Wikipedia],
        _ => [Wikipedia, OpenLibrary, TvMaze, ITunes("all", "")],
    };

    private static async Task<List<ImageResult>> SafeAsync(Source source, string query, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SourceTimeout);
        try
        {
            return await source(query, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Failed or too slow: skip this source.
            return [];
        }
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await Web.Http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    // --- Wikipedia: page images (posters, covers, box art) --------------------------------

    private static Task<List<ImageResult>> Wikipedia(string query, CancellationToken ct) => WikipediaSearch(query, ct);

    private static Source WikipediaWith(string suffix) => (q, ct) => WikipediaSearch($"{q} {suffix}", ct);

    private static async Task<List<ImageResult>> WikipediaSearch(string query, CancellationToken ct)
    {
        var url = "https://en.wikipedia.org/w/api.php?action=query&format=json&origin=*&generator=search" +
                  $"&gsrsearch={Uri.EscapeDataString(query)}&gsrlimit=12&prop=pageimages&piprop=thumbnail" +
                  "&pithumbsize=400&pilicense=any";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<(int Index, ImageResult Result)>();
        if (doc.RootElement.TryGetProperty("query", out var q) && q.TryGetProperty("pages", out var pages))
        {
            foreach (var page in pages.EnumerateObject().Select(p => p.Value))
            {
                if (page.TryGetProperty("thumbnail", out var thumb) && Str(thumb, "source") is { } src)
                {
                    var index = page.TryGetProperty("index", out var i) ? i.GetInt32() : 99;
                    results.Add((index, new ImageResult(src, Str(page, "title") ?? query, "Wikipedia")));
                }
            }
        }
        return results.OrderBy(r => r.Index).Select(r => r.Result).ToList();
    }

    // --- Wikidata game matches → their Wikipedia box art -------------------------------------

    private static async Task<List<ImageResult>> WikidataGames(string query, CancellationToken ct) =>
        (await MetadataService.GameMatchesAsync(query, ct).ConfigureAwait(false))
            .Where(s => s.ImageUrl is not null)
            .Select(s => new ImageResult(s.ImageUrl!, s.Title, "Wikipedia"))
            .ToList();

    // --- iTunes Search: albums, podcasts, books, audiobooks, movies ------------------------

    private static Source ITunes(string media, string entity) => async (query, ct) =>
    {
        var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&media={media}&limit=10" +
                  (entity.Length > 0 ? $"&entity={entity}" : "");
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<ImageResult>();
        foreach (var item in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            if (Str(item, "artworkUrl100") is { } art)
            {
                var title = Str(item, "collectionName") ?? Str(item, "trackName") ?? query;
                results.Add(new ImageResult(art.Replace("100x100bb", "600x600bb"), title, "iTunes"));
            }
        }
        return results;
    };

    // --- Open Library: book covers --------------------------------------------------------

    private static async Task<List<ImageResult>> OpenLibrary(string query, CancellationToken ct)
    {
        var url = $"https://openlibrary.org/search.json?q={Uri.EscapeDataString(query)}&limit=12&fields=title,cover_i";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<ImageResult>();
        foreach (var item in doc.RootElement.GetProperty("docs").EnumerateArray())
        {
            if (item.TryGetProperty("cover_i", out var cover) && cover.ValueKind == JsonValueKind.Number)
                results.Add(new ImageResult($"https://covers.openlibrary.org/b/id/{cover.GetInt64()}-L.jpg",
                    Str(item, "title") ?? query, "Open Library"));
        }
        return results;
    }

    // --- TVmaze: TV show posters ------------------------------------------------------------

    private static async Task<List<ImageResult>> TvMaze(string query, CancellationToken ct)
    {
        var url = $"https://api.tvmaze.com/search/shows?q={Uri.EscapeDataString(query)}";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<ImageResult>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var show = item.GetProperty("show");
            if (show.TryGetProperty("image", out var image) && Str(image, "medium") is { } src)
                results.Add(new ImageResult(src, Str(show, "name") ?? query, "TVmaze"));
        }
        return results;
    }

    // --- AniList: anime and manga covers (fast GraphQL API) --------------------------------

    private static Source AniList(string kind) => async (query, ct) =>
    {
        const string gql = "query($s:String,$t:MediaType){Page(perPage:15){media(search:$s,type:$t){title{romaji english} coverImage{extraLarge large}}}}";
        var body = new JsonObject
        {
            ["query"] = gql,
            ["variables"] = new JsonObject { ["s"] = query, ["t"] = kind },
        };
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await Web.Http.PostAsync("https://graphql.anilist.co", content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var results = new List<ImageResult>();
        foreach (var item in doc.RootElement.GetProperty("data").GetProperty("Page").GetProperty("media").EnumerateArray())
        {
            var cover = item.GetProperty("coverImage");
            if ((Str(cover, "extraLarge") ?? Str(cover, "large")) is { } src)
            {
                var title = item.GetProperty("title");
                results.Add(new ImageResult(src, Str(title, "english") ?? Str(title, "romaji") ?? query, "AniList"));
            }
        }
        return results;
    };

    // --- Jikan (MyAnimeList): anime and manga ----------------------------------------------

    private static Source Jikan(string kind) => async (query, ct) =>
    {
        var url = $"https://api.jikan.moe/v4/{kind}?q={Uri.EscapeDataString(query)}&limit=12";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<ImageResult>();
        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            if (item.TryGetProperty("images", out var images) && images.TryGetProperty("jpg", out var jpg)
                && (Str(jpg, "large_image_url") ?? Str(jpg, "image_url")) is { } src)
                results.Add(new ImageResult(src, Str(item, "title") ?? query, "MyAnimeList"));
        }
        return results;
    };
}

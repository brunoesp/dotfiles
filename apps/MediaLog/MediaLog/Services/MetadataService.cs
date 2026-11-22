using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediaLog.Models;

namespace MediaLog.Services;

/// <summary>A title match with details to fill in a new entry.</summary>
/// <param name="Creator">Director, author, developer, artist, studio… depending on the media.</param>
/// <param name="Length">Running time, pages, episodes, tracks… as display text.</param>
public sealed record MediaSuggestion(
    string Title, int? Year, string? Creator, string? Length, string? ImageUrl, string Source, string? Description = null,
    string? Platform = null, IReadOnlyList<Credit>? Credits = null)
{
    /// <summary>"2024 · Denis Villeneuve · 2h 46m"</summary>
    public string Details => string.Join(" · ",
        new[] { Year?.ToString(CultureInfo.InvariantCulture), Creator, Length }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>
/// Looks up titles in free, key-less public databases picked by media type
/// (Wikidata, TVmaze, AniList, Open Library, iTunes) and returns matching works with details.
/// </summary>
public static class MetadataService
{
    private const int MaxResults = 10;

    /// <summary>Open Library in particular can take a few seconds; anything slower is skipped.</summary>
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(8);

    private delegate Task<List<MediaSuggestion>> Source(string query, CancellationToken ct);

    /// <summary>Yields suggestions as each source answers (best source first when they're equally fast).</summary>
    /// <param name="platform">Console of a game: retro consoles also search the libretro box-art titles.</param>
    public static async IAsyncEnumerable<IReadOnlyList<MediaSuggestion>> SuggestAsync(string query, MediaTypeInfo? type,
        string? platform, [EnumeratorCancellation] CancellationToken ct)
    {
        query = query.Trim();
        if (query.Length < 2)
            yield break;

        var sources = SourcesFor(MediaCategories.Of(type));
        if (MediaCategories.Of(type) == MediaCategory.Game && Platforms.BoxArtRepo(platform) is { } repo)
            sources = [.. sources, RetroTitles(repo, platform!)];
        var tasks = sources.Select(s => SafeAsync(s, query, ct)).ToList();
        // With several sources, cap each so a fast one doesn't crowd out the others.
        var perSource = sources.Length > 1 ? 5 : MaxResults;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var total = 0;
        await foreach (var task in Task.WhenEach(tasks).WithCancellation(ct).ConfigureAwait(false))
        {
            var batch = (await task.ConfigureAwait(false))
                .OrderByDescending(s => Math.Round(TitleMatch.Score(s.Title, query), 1)) // stable: keeps each source's order on ties
                .Where(s => seen.Add($"{s.Title}|{s.Year}"))
                .Take(Math.Min(perSource, MaxResults - total))
                .ToList();
            total += batch.Count;
            if (batch.Count > 0)
                yield return batch;
            if (total >= MaxResults)
                yield break;
        }
    }

    /// <summary>
    /// Fills in what a suggestion lacks (year, creator, credits, length) from the best Wikidata
    /// match for games and films, e.g. after picking a box-art match that only has a title.
    /// </summary>
    public static async Task<MediaSuggestion> EnrichAsync(MediaSuggestion suggestion, MediaTypeInfo? type, CancellationToken ct)
    {
        var category = MediaCategories.Of(type);
        if (category is not (MediaCategory.Game or MediaCategory.Film)
            || (suggestion.Year is not null && suggestion.Creator is not null && suggestion.Credits is { Count: > 0 }))
            return suggestion;
        try
        {
            var kind = category == MediaCategory.Game ? WikidataKind.Game : WikidataKind.Film;
            var match = (await SafeAsync(Wikidata(kind), suggestion.Title, ct).ConfigureAwait(false))
                .Select(m => (Match: m, Score: TitleMatch.Score(m.Title, suggestion.Title) + TitleMatch.Score(suggestion.Title, m.Title)))
                .Where(m => m.Score >= 1.7)
                .OrderByDescending(m => m.Score)
                .Select(m => m.Match)
                .FirstOrDefault();
            if (match is null)
                return suggestion;
            return suggestion with
            {
                Year = suggestion.Year ?? match.Year,
                Creator = suggestion.Creator ?? match.Creator,
                Length = suggestion.Length ?? match.Length,
                Credits = suggestion.Credits is { Count: > 0 } ? suggestion.Credits : match.Credits,
                Platform = suggestion.Platform ?? match.Platform,
                ImageUrl = suggestion.ImageUrl ?? match.ImageUrl,
            };
        }
        catch (OperationCanceledException)
        {
            return suggestion;
        }
    }

    /// <summary>Video games matching the title (Wikidata), with their original platform and box art.</summary>
    public static Task<List<MediaSuggestion>> GameMatchesAsync(string query, CancellationToken ct) =>
        Wikidata(WikidataKind.Game)(query, ct);

    private static Source[] SourcesFor(MediaCategory category) => category switch
    {
        MediaCategory.Film => [Wikidata(WikidataKind.Film), ITunesMovies],
        MediaCategory.Series => [TvMaze],
        MediaCategory.Anime => [AniList("ANIME")],
        MediaCategory.Manga => [AniList("MANGA"), GoogleBooks, OpenLibrary],
        MediaCategory.Book => [GoogleBooks, OpenLibrary, ITunes("ebook", "ebook", null)],
        MediaCategory.Comic => [GoogleBooks, OpenLibrary, ITunes("ebook", "ebook", null)],
        MediaCategory.Game => [Wikidata(WikidataKind.Game)],
        MediaCategory.Music => [ITunes("music", "album", "track")],
        MediaCategory.Podcast => [ITunes("podcast", "podcast", "episode")],
        MediaCategory.Audiobook => [ITunes("audiobook", "audiobook", null), GoogleBooks],
        _ => [Wikidata(WikidataKind.Any)],
    };

    private static async Task<List<MediaSuggestion>> SafeAsync(Source source, string query, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SourceTimeout);
        try
        {
            return await source(query, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return [];
        }
    }

    // --- helpers -------------------------------------------------------------------------

    private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        var response = await Web.Http.GetAsync(url, ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            // Rate limited (Wikimedia limits bursts): wait a moment and try once more.
            var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
            response.Dispose();
            await Task.Delay(wait > TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : wait, ct).ConfigureAwait(false);
            response = await Web.Http.GetAsync(url, ct).ConfigureAwait(false);
        }
        using var _ = response;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static int? YearOf(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y) ? y : null;

    /// <summary>"2h 46m" / "48 min".</summary>
    public static string Minutes(double minutes)
    {
        var total = (int)Math.Round(minutes);
        return total >= 60 ? $"{total / 60}h {total % 60:00}m" : $"{total} min";
    }

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

    // --- Wikidata: films, games, anything ---------------------------------------------------

    private enum WikidataKind { Film, Game, Any }

    private static readonly HashSet<string> FilmClasses =
        ["Q11424", "Q24869", "Q506240", "Q93204", "Q202866", "Q226730", "Q29168811", "Q24862", "Q17517379", "Q20667187"];

    private static readonly HashSet<string> GameClasses = ["Q7889", "Q21125433", "Q865493", "Q4393107", "Q64170203"];

    private static Source Wikidata(WikidataKind kind) => async (query, ct) =>
    {
        const string api = "https://www.wikidata.org/w/api.php?format=json&origin=*";
        // Two searches in parallel:
        //  • full-text, so any words of the title match in any order ("a link to the past"),
        //    restricted to films / video games where that applies;
        //  • the label search, which is best for titles typed from the start.
        var kindClasses = kind switch { WikidataKind.Film => FilmClasses, WikidataKind.Game => GameClasses, _ => null };
        var fullText = TitleMatch.FullTextQuery(query) +
                       (kindClasses is null ? "" : " haswbstatement:" + string.Join('|', kindClasses.Select(c => "P31=" + c)));
        var fullTextTask = SearchIdsAsync(
            $"{api}&action=query&list=search&srnamespace=0&srlimit=10&srprop=&srsearch={Uri.EscapeDataString(fullText)}",
            doc => doc.RootElement.GetProperty("query").GetProperty("search").EnumerateArray()
                .Select(h => (Id: Str(h, "title")!, Label: (string?)null, Description: (string?)null)), ct);
        var labelTask = SearchIdsAsync(
            $"{api}&action=wbsearchentities&type=item&language=en&uselang=en&limit=10&search={Uri.EscapeDataString(query)}",
            doc => doc.RootElement.GetProperty("search").EnumerateArray()
                .Select(h => (Id: Str(h, "id")!, Label: Str(h, "label"), Description: Str(h, "description"))), ct);
        var both = await Task.WhenAll(fullTextTask, labelTask).ConfigureAwait(false);
        var hits = both[0].Concat(both[1])
            .Where(h => h.Id is { Length: > 1 } && h.Id[0] == 'Q')
            .DistinctBy(h => h.Id)
            .Take(15)
            .ToList();
        if (hits.Count == 0)
            return [];

        using var entities = await GetJsonAsync(
            $"{api}&action=wbgetentities&props=claims|sitelinks|labels|descriptions&languages=en&sitefilter=enwiki" +
            $"&ids={string.Join('|', hits.Select(h => h.Id))}", ct)
            .ConfigureAwait(false);
        var all = entities.RootElement.GetProperty("entities");

        string[] creatorProps = kind switch
        {
            WikidataKind.Film => ["P57"],
            WikidataKind.Game => ["P178", "P123"],
            _ => ["P57", "P50", "P178", "P175", "P170", "P86"],
        };

        // Extra credits worth showing, per kind: (property, role, how many values).
        (string Property, string Role, int Max)[] creditProps = kind switch
        {
            WikidataKind.Game => [("P57", "Director", 2), ("P86", "Composer", 2), ("P162", "Producer", 1), ("P123", "Publisher", 1),
                ("P287", "Designer", 1), ("P136", "Genre", 2)],
            WikidataKind.Film => [("P86", "Composer", 1), ("P58", "Writer", 2), ("P161", "Starring", 3), ("P136", "Genre", 2)],
            _ => [("P86", "Composer", 1), ("P50", "Author", 1), ("P136", "Genre", 2)],
        };

        var items = new List<(string Label, string? Description, int? Year, string? CreatorId, string? Length, string? Article,
            string? PlatformId, List<(string Role, List<string> Ids)> Credits)>();
        foreach (var hit in hits)
        {
            if (!all.TryGetProperty(hit.Id, out var entity) || !entity.TryGetProperty("claims", out var claims))
                continue;
            var label = hit.Label ?? EnglishText(entity, "labels");
            if (label is null)
                continue;
            var description = hit.Description ?? EnglishText(entity, "descriptions");
            var classes = ItemIds(claims, "P31");
            var creatorId = creatorProps.Select(p => ItemIds(claims, p).FirstOrDefault()).FirstOrDefault(id => id is not null);
            var matches = kind switch
            {
                WikidataKind.Film => classes.Any(FilmClasses.Contains) || ItemIds(claims, "P57").Count > 0,
                WikidataKind.Game => classes.Any(GameClasses.Contains) || ItemIds(claims, "P178").Count > 0,
                _ => true,
            };
            if (!matches)
                continue;

            var year = Years(claims, "P577").Concat(Years(claims, "P571")).Cast<int?>().Min();
            string? length = null;
            if (FirstValue(claims, "P2047") is { } duration && duration.TryGetProperty("amount", out var amount)
                && double.TryParse(amount.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                var unit = Str(duration, "unit") ?? "";
                var minutes = unit.EndsWith("Q11574") ? value / 60 : unit.EndsWith("Q25235") ? value * 60 : value;
                length = Minutes(minutes);
            }
            string? article = null;
            if (entity.TryGetProperty("sitelinks", out var links) && links.TryGetProperty("enwiki", out var enwiki))
                article = Str(enwiki, "title");
            var platformId = kind == WikidataKind.Game ? ItemIds(claims, "P400").FirstOrDefault() : null;
            var credits = creditProps
                .Select(c => (c.Role, Ids: ItemIds(claims, c.Property).Where(id => id != creatorId).Take(c.Max).ToList()))
                .Where(c => c.Ids.Count > 0)
                .ToList();
            items.Add((label, description, year, creatorId, length, article, platformId, credits));
        }
        if (items.Count == 0)
            return [];

        // Names of the directors / developers / authors…
        var names = new Dictionary<string, string>();
        var labelIds = items.Select(i => i.CreatorId).Concat(items.Select(i => i.PlatformId))
            .Concat(items.SelectMany(i => i.Credits.SelectMany(c => c.Ids)))
            .OfType<string>().Distinct().Take(100).ToList();
        foreach (var chunk in labelIds.Chunk(50)) // the API takes 50 ids per call
        {
            using var labels = await GetJsonAsync(
                $"{api}&action=wbgetentities&props=labels&languages=en&ids={string.Join('|', chunk)}", ct).ConfigureAwait(false);
            foreach (var e in labels.RootElement.GetProperty("entities").EnumerateObject())
            {
                if (e.Value.TryGetProperty("labels", out var l) && l.TryGetProperty("en", out var en) && Str(en, "value") is { } name)
                    names[e.Name] = name;
            }
        }

        // Posters / covers from the English Wikipedia articles (best effort).
        var images = await WikipediaImagesAsync(items.Select(i => i.Article).OfType<string>().ToList(), ct).ConfigureAwait(false);

        return items.Select(i => new MediaSuggestion(
                i.Label, i.Year, i.CreatorId is { } id ? names.GetValueOrDefault(id) : null, i.Length,
                i.Article is { } a ? images.GetValueOrDefault(a) : null, "Wikidata", i.Description,
                i.PlatformId is { } p ? names.GetValueOrDefault(p) : null,
                i.Credits
                    .Select(c => new Credit
                    {
                        Role = c.Role,
                        Name = string.Join(", ", c.Ids.Select(id => names.GetValueOrDefault(id)).OfType<string>()),
                    })
                    .Where(c => c.Name.Length > 0)
                    .ToList()))
            .ToList();
    };

    private static async Task<List<(string Id, string? Label, string? Description)>> SearchIdsAsync(string url,
        Func<JsonDocument, IEnumerable<(string Id, string? Label, string? Description)>> read, CancellationToken ct)
    {
        try
        {
            using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
            return read(doc).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return [];
        }
    }

    private static string? EnglishText(JsonElement entity, string property) =>
        entity.TryGetProperty(property, out var values) && values.TryGetProperty("en", out var en) ? Str(en, "value") : null;

    private static IEnumerable<JsonElement> Statements(JsonElement claims, string property)
    {
        if (!claims.TryGetProperty(property, out var list))
            return [];
        var statements = list.EnumerateArray().ToList();
        var preferred = statements.Where(s => Str(s, "rank") == "preferred").ToList();
        return preferred.Count > 0 ? preferred : statements.Where(s => Str(s, "rank") != "deprecated");
    }

    private static JsonElement? FirstValue(JsonElement claims, string property)
    {
        foreach (var s in Statements(claims, property))
        {
            if (s.GetProperty("mainsnak").TryGetProperty("datavalue", out var dv) && dv.TryGetProperty("value", out var v))
                return v;
        }
        return null;
    }

    private static List<string> ItemIds(JsonElement claims, string property) =>
        Statements(claims, property)
            .Select(s => s.GetProperty("mainsnak").TryGetProperty("datavalue", out var dv) && dv.TryGetProperty("value", out var v)
                ? Str(v, "id") : null)
            .OfType<string>()
            .ToList();

    private static IEnumerable<int> Years(JsonElement claims, string property) =>
        Statements(claims, property)
            .Select(s => s.GetProperty("mainsnak").TryGetProperty("datavalue", out var dv) && dv.TryGetProperty("value", out var v)
                ? Str(v, "time") : null)
            .Select(t => t is { Length: > 5 } ? YearOf(t[1..]) : null)
            .OfType<int>();

    private static async Task<Dictionary<string, string>> WikipediaImagesAsync(List<string> titles, CancellationToken ct)
    {
        var result = new Dictionary<string, string>();
        if (titles.Count == 0)
            return result;
        try
        {
            using var doc = await GetJsonAsync(
                "https://en.wikipedia.org/w/api.php?action=query&format=json&origin=*&prop=pageimages&piprop=thumbnail" +
                $"&pithumbsize=400&pilicense=any&redirects=1&titles={Uri.EscapeDataString(string.Join('|', titles.Take(20)))}", ct)
                .ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("query", out var q))
                return result;
            // Map normalized / redirected titles back to the requested ones.
            var alias = new Dictionary<string, string>();
            foreach (var name in new[] { "normalized", "redirects" })
            {
                if (q.TryGetProperty(name, out var list))
                    foreach (var n in list.EnumerateArray())
                        if (Str(n, "from") is { } from && Str(n, "to") is { } to)
                            alias[to] = alias.GetValueOrDefault(from, from);
            }
            if (q.TryGetProperty("pages", out var pages))
            {
                foreach (var page in pages.EnumerateObject().Select(p => p.Value))
                {
                    if (Str(page, "title") is { } title && page.TryGetProperty("thumbnail", out var thumb) && Str(thumb, "source") is { } src)
                        result[alias.GetValueOrDefault(title, title)] = src;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Covers are optional.
        }
        return result;
    }

    // --- libretro box art: retro game titles for the chosen console --------------------------

    private static Source RetroTitles(string repo, string platform) => async (query, ct) =>
        (await RetroBoxArt.SearchAsync(query, repo, ct).ConfigureAwait(false))
            .Select(r => new MediaSuggestion(RetroBoxArt.DisplayTitle(r.Title), null, null, null, r.Url, "libretro",
                RetroBoxArt.Region(r.Title), platform))
            .GroupBy(s => s.Title, StringComparer.OrdinalIgnoreCase) // one per title (best region first)
            .Select(g => g.First())
            .ToList();

    // --- TVmaze: TV series ------------------------------------------------------------------

    private static async Task<List<MediaSuggestion>> TvMaze(string query, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"https://api.tvmaze.com/search/shows?q={Uri.EscapeDataString(query)}", ct)
            .ConfigureAwait(false);
        var ids = doc.RootElement.EnumerateArray()
            .Select(r => r.GetProperty("show"))
            .Select(s => Int(s, "id"))
            .OfType<int>()
            .Take(6)
            .ToList();
        // Creators and episode counts need the show details; fetch the top matches in parallel.
        var details = await Task.WhenAll(ids.Select(id => TvMazeShowAsync(id, ct))).ConfigureAwait(false);
        return details.OfType<MediaSuggestion>().ToList();
    }

    private static async Task<MediaSuggestion?> TvMazeShowAsync(int id, CancellationToken ct)
    {
        try
        {
            using var doc = await GetJsonAsync($"https://api.tvmaze.com/shows/{id}?embed[]=episodes&embed[]=crew", ct)
                .ConfigureAwait(false);
            var show = doc.RootElement;
            string? creator = null;
            int episodes = 0;
            if (show.TryGetProperty("_embedded", out var embedded))
            {
                if (embedded.TryGetProperty("crew", out var crew))
                    creator = crew.EnumerateArray().Where(c => Str(c, "type") == "Creator")
                        .Select(c => Str(c.GetProperty("person"), "name")).FirstOrDefault(n => n is not null);
                if (embedded.TryGetProperty("episodes", out var eps))
                    episodes = eps.GetArrayLength();
            }
            if (creator is null)
            {
                // Fall back to the network / streaming service.
                foreach (var name in new[] { "network", "webChannel" })
                    if (creator is null && show.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Object)
                        creator = Str(n, "name");
            }
            var image = show.TryGetProperty("image", out var img) ? Str(img, "medium") : null;
            return new MediaSuggestion(Str(show, "name") ?? "", YearOf(Str(show, "premiered")), creator,
                episodes > 0 ? Count(episodes, "episode") : null, image, "TVmaze");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    // --- AniList: anime and manga --------------------------------------------------------------

    private static Source AniList(string kind) => async (query, ct) =>
    {
        const string gql =
            "query($s:String,$t:MediaType){Page(perPage:8){media(search:$s,type:$t){title{romaji english} format episodes duration " +
            "chapters volumes startDate{year} coverImage{extraLarge large} studios(isMain:true){nodes{name}} " +
            "staff(perPage:4){edges{role node{name{full}}}}}}}";
        var body = new JsonObject { ["query"] = gql, ["variables"] = new JsonObject { ["s"] = query, ["t"] = kind } };
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await Web.Http.PostAsync("https://graphql.anilist.co", content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var results = new List<MediaSuggestion>();
        foreach (var m in doc.RootElement.GetProperty("data").GetProperty("Page").GetProperty("media").EnumerateArray())
        {
            var title = m.GetProperty("title");
            var name = Str(title, "english") ?? Str(title, "romaji");
            if (name is null)
                continue;
            var format = Str(m, "format");
            string? creator;
            string? length;
            if (kind == "MANGA")
            {
                creator = m.GetProperty("staff").GetProperty("edges").EnumerateArray()
                    .OrderBy(e => (Str(e, "role") ?? "").Contains("Story", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .Select(e => Str(e.GetProperty("node").GetProperty("name"), "full"))
                    .FirstOrDefault(n => n is not null);
                length = Int(m, "volumes") is { } v ? Count(v, "volume") : Int(m, "chapters") is { } c ? Count(c, "chapter") : null;
            }
            else
            {
                creator = m.GetProperty("studios").GetProperty("nodes").EnumerateArray()
                    .Select(n => Str(n, "name")).FirstOrDefault(n => n is not null);
                var episodes = Int(m, "episodes");
                var duration = Int(m, "duration");
                length = format == "MOVIE" || episodes == 1
                    ? duration is { } d ? Minutes(d) : null
                    : episodes is { } e ? Count(e, "episode") : null;
            }
            var cover = m.GetProperty("coverImage");
            results.Add(new MediaSuggestion(name, Int(m.GetProperty("startDate"), "year"), creator, length,
                Str(cover, "extraLarge") ?? Str(cover, "large"), "AniList", format?.Replace('_', ' ')));
        }
        return results;
    };

    // --- Google Books: books (no key needed for searches) ------------------------------------

    private static async Task<List<MediaSuggestion>> GoogleBooks(string query, CancellationToken ct)
    {
        var url = $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(query)}&maxResults=8&printType=books" +
                  "&fields=items(volumeInfo(title,subtitle,authors,publishedDate,pageCount,imageLinks))";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<MediaSuggestion>();
        if (!doc.RootElement.TryGetProperty("items", out var items))
            return results;
        foreach (var item in items.EnumerateArray())
        {
            var v = item.GetProperty("volumeInfo");
            if (Str(v, "title") is not { } title)
                continue;
            var author = v.TryGetProperty("authors", out var a) && a.GetArrayLength() > 0 ? a[0].GetString() : null;
            var pages = Int(v, "pageCount");
            var cover = v.TryGetProperty("imageLinks", out var links)
                ? (Str(links, "thumbnail") ?? Str(links, "smallThumbnail"))?.Replace("http://", "https://").Replace("&edge=curl", "")
                : null;
            results.Add(new MediaSuggestion(title, YearOf(Str(v, "publishedDate")), author,
                pages is > 0 and var p ? Count(p, "page") : null, cover, "Google Books", Str(v, "subtitle")));
        }
        return results;
    }

    // --- Open Library: books -----------------------------------------------------------------

    private static async Task<List<MediaSuggestion>> OpenLibrary(string query, CancellationToken ct)
    {
        var url = $"https://openlibrary.org/search.json?q={Uri.EscapeDataString(query)}&limit=8" +
                  "&fields=title,author_name,first_publish_year,number_of_pages_median,cover_i";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<MediaSuggestion>();
        foreach (var d in doc.RootElement.GetProperty("docs").EnumerateArray())
        {
            if (Str(d, "title") is not { } title)
                continue;
            var author = d.TryGetProperty("author_name", out var a) && a.GetArrayLength() > 0 ? a[0].GetString() : null;
            var pages = Int(d, "number_of_pages_median");
            var cover = d.TryGetProperty("cover_i", out var c) && c.ValueKind == JsonValueKind.Number
                ? $"https://covers.openlibrary.org/b/id/{c.GetInt64()}-L.jpg" : null;
            results.Add(new MediaSuggestion(title, Int(d, "first_publish_year"), author,
                pages is { } p ? Count(p, "page") : null, cover, "Open Library"));
        }
        return results;
    }

    // --- iTunes: albums, podcasts, audiobooks, movies ------------------------------------------

    private static Source ITunes(string media, string entity, string? countNoun) => async (query, ct) =>
    {
        var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&media={media}&entity={entity}&limit=8";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<MediaSuggestion>();
        foreach (var r in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            if ((Str(r, "collectionName") ?? Str(r, "trackName")) is not { } title)
                continue;
            var count = countNoun is not null ? Int(r, "trackCount") : null;
            results.Add(new MediaSuggestion(title, YearOf(Str(r, "releaseDate")), Str(r, "artistName"),
                count is { } n ? Count(n, countNoun!) : null,
                Str(r, "artworkUrl100")?.Replace("100x100bb", "600x600bb"), "iTunes"));
        }
        return results;
    };

    private static async Task<List<MediaSuggestion>> ITunesMovies(string query, CancellationToken ct)
    {
        var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&media=movie&entity=movie&limit=8";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<MediaSuggestion>();
        foreach (var r in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            if (Str(r, "trackName") is not { } title)
                continue;
            var millis = r.TryGetProperty("trackTimeMillis", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetDouble() : 0;
            results.Add(new MediaSuggestion(title, YearOf(Str(r, "releaseDate")), Str(r, "artistName"),
                millis > 0 ? Minutes(millis / 60000) : null,
                Str(r, "artworkUrl100")?.Replace("100x100bb", "600x600bb"), "iTunes"));
        }
        return results;
    }
}

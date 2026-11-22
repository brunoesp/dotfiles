using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MediaLog.Services;

/// <summary>
/// Box art for retro consoles from the libretro-thumbnails collection on GitHub
/// (NES, SNES, N64, Genesis, Atari 2600, PlayStation…). The list of titles for a
/// console is fetched once and cached; if that isn't possible, likely file names are tried.
/// </summary>
public static partial class RetroBoxArt
{
    private static readonly Dictionary<string, Task<string[]?>> Indexes = new();
    private static readonly object Gate = new();

    private static readonly string? CacheFolder = OperatingSystem.IsBrowser()
        ? null
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create), "MediaLog", "boxart-index");

    private static string Url(string repo, string fileName) =>
        $"https://raw.githubusercontent.com/libretro-thumbnails/{repo}/master/Named_Boxarts/{Uri.EscapeDataString(fileName)}";

    public static async Task<List<ImageResult>> SearchAsync(string title, string repo, CancellationToken ct)
    {
        var index = await IndexAsync(repo, ct).ConfigureAwait(false);
        var names = index is not null ? Match(index, title) : await GuessAsync(title, repo, ct).ConfigureAwait(false);
        return names.Select(n => new ImageResult(Url(repo, n), Path.GetFileNameWithoutExtension(n), "libretro")).ToList();
    }

    // --- title list per console (GitHub API, cached for a week) ----------------------------

    private static Task<string[]?> IndexAsync(string repo, CancellationToken ct)
    {
        lock (Gate)
        {
            if (!Indexes.TryGetValue(repo, out var task) || (task.IsCompleted && task.Result is null))
            {
                task = LoadIndexAsync(repo);
                Indexes[repo] = task;
            }
            return task.WaitAsync(ct);
        }
    }

    private static async Task<string[]?> LoadIndexAsync(string repo)
    {
        var cache = CacheFolder is null ? null : Path.Combine(CacheFolder, repo + ".txt");
        try
        {
            if (cache is not null && File.Exists(cache) && File.GetLastWriteTimeUtc(cache) > DateTime.UtcNow.AddDays(-7))
                return await File.ReadAllLinesAsync(cache).ConfigureAwait(false);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var api = $"https://api.github.com/repos/libretro-thumbnails/{repo}/git/trees/master";
            string? sha;
            using (var root = await GetJsonAsync(api, timeout.Token).ConfigureAwait(false))
            {
                sha = root.RootElement.GetProperty("tree").EnumerateArray()
                    .Where(t => t.GetProperty("path").GetString() == "Named_Boxarts")
                    .Select(t => t.GetProperty("sha").GetString())
                    .FirstOrDefault();
            }
            if (sha is null)
                return null;
            string[] names;
            using (var tree = await GetJsonAsync($"https://api.github.com/repos/libretro-thumbnails/{repo}/git/trees/{sha}", timeout.Token)
                       .ConfigureAwait(false))
            {
                names = tree.RootElement.GetProperty("tree").EnumerateArray()
                    .Select(t => t.GetProperty("path").GetString())
                    .OfType<string>()
                    .Where(n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
            if (cache is not null && names.Length > 0)
            {
                Directory.CreateDirectory(CacheFolder!);
                await File.WriteAllLinesAsync(cache, names).ConfigureAwait(false);
            }
            return names.Length > 0 ? names : null;
        }
        catch (Exception)
        {
            // Offline or GitHub's API limit reached: an older cached list is better than nothing.
            return cache is not null && File.Exists(cache) ? await File.ReadAllLinesAsync(cache).ConfigureAwait(false) : null;
        }
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        using var response = await Web.Http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
    }

    /// <summary>"Legend of Zelda, The - A Link to the Past (USA)" → "The Legend of Zelda: A Link to the Past".</summary>
    public static string DisplayTitle(string fileTitle)
    {
        var bare = Tags().Replace(fileTitle, "").Trim();
        var article = Regex.Match(bare, @"^(.*?), (The|A|An)( - .*)?$");
        if (article.Success)
            bare = $"{article.Groups[2].Value} {article.Groups[1].Value}{article.Groups[3].Value}";
        return bare.Replace(" - ", ": ");
    }

    /// <summary>"Super Mario World (USA)" → "USA".</summary>
    public static string? Region(string fileTitle)
    {
        var m = Regex.Match(fileTitle, @"\(([^)]*)\)");
        return m.Success ? m.Groups[1].Value : null;
    }

    // --- matching ---------------------------------------------------------------------

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]")]
    private static partial Regex Tags();

    /// <summary>"Legend of Zelda, The - A Link to the Past (USA).png" → "thelegendofzeldaalinktothepast".</summary>
    private static string Key(string name)
    {
        var bare = Tags().Replace(Path.GetFileNameWithoutExtension(name), "");
        // "Title, The - Subtitle" → "The Title - Subtitle"
        var article = Regex.Match(bare, @"^(.*?), (The|A|An)( - .*)?$");
        if (article.Success)
            bare = $"{article.Groups[2].Value} {article.Groups[1].Value}{article.Groups[3].Value}";
        return Platforms.Normalize(bare);
    }

    private static int RegionRank(string name) =>
        name.Contains("(USA") ? 0 : name.Contains("(World") ? 1 : name.Contains("Europe") ? 2 : name.Contains("Japan") ? 3 : 4;

    private static List<string> Match(string[] index, string title)
    {
        var query = Platforms.Normalize(title);
        if (query.Length == 0)
            return [];
        var words = title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Platforms.Normalize).Where(w => w.Length > 1).ToArray();
        return index
            .Select(n => (Name: n, Key: Key(n)))
            .Select(x => (x.Name, Score: x.Key == query ? 0 : x.Key.StartsWith(query) ? 1 : x.Key.Contains(query) ? 2
                : words.Length > 0 && words.All(w => x.Key.Contains(w)) ? 3 : 9))
            .Where(x => x.Score < 9)
            .OrderBy(x => x.Score).ThenBy(x => RegionRank(x.Name)).ThenBy(x => x.Name.Length)
            .Take(8)
            .Select(x => x.Name)
            .ToList();
    }

    // --- fallback: guess No-Intro style file names ---------------------------------------

    private static async Task<List<string>> GuessAsync(string title, string repo, CancellationToken ct)
    {
        static string Clean(string s) => Regex.Replace(s, "[&*/:`<>?\\\\|\"]", "_");
        var baseName = title.Trim().Replace(": ", " - ");
        var variants = new List<string> { baseName };
        var the = Regex.Match(baseName, @"^(The|A|An) (.*?)( - .*)?$");
        if (the.Success)
            variants.Add($"{the.Groups[2].Value}, {the.Groups[1].Value}{the.Groups[3].Value}");
        // File names are case-sensitive and often capitalize every word ("Sonic The Hedgehog").
        foreach (var v in variants.ToList())
        {
            var titled = string.Join(' ', v.Split(' ').Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w));
            if (titled != v)
                variants.Add(titled);
        }
        string[] regions = ["(USA)", "(USA, Europe)", "(World)", "(Europe)", "(Japan, USA)", "(Japan)"];
        var candidates = variants.SelectMany(v => regions.Select(r => $"{Clean(v)} {r}.png")).ToList();

        var checks = candidates.Select(async name =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, Url(repo, name));
                using var response = await Web.Http.SendAsync(request, ct).ConfigureAwait(false);
                return response.IsSuccessStatusCode ? name : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                return null;
            }
        });
        return (await Task.WhenAll(checks).ConfigureAwait(false)).OfType<string>().ToList();
    }
}

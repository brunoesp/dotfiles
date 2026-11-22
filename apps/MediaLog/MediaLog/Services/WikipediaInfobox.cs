using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaLog.Models;

namespace MediaLog.Services;

/// <summary>
/// Reads credits from an English Wikipedia article's infobox (composer, director, programmer…),
/// which is often more complete than Wikidata for games and films.
/// </summary>
public static partial class WikipediaInfobox
{
    /// <summary>Infobox field → role shown in the app, in display order.</summary>
    private static readonly (string[] Fields, string Role)[] Roles =
    [
        (["director", "directors"], "Director"),
        (["producer", "producers"], "Producer"),
        (["designer", "designers"], "Designer"),
        (["programmer", "programmers"], "Programmer"),
        (["artist", "artists"], "Artist"),
        (["writer", "writers", "screenplay", "story"], "Writer"),
        (["composer", "composers", "music"], "Composer"),
        (["starring"], "Starring"),
        (["cinematography"], "Cinematography"),
        (["editing"], "Editor"),
        (["publisher", "publishers"], "Publisher"),
        (["series"], "Series"),
        (["genre", "genres"], "Genre"),
    ];

    /// <summary>Order credits by <see cref="Roles"/> (unknown roles last).</summary>
    public static int RoleOrder(string role)
    {
        var i = Array.FindIndex(Roles, r => r.Role.Equals(role, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? Roles.Length : i;
    }

    public static async Task<List<Credit>> CreditsAsync(string articleTitle, CancellationToken ct)
    {
        var url = "https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&origin=*&redirects=1" +
                  $"&prop=revisions&rvprop=content&rvslots=main&rvsection=0&titles={Uri.EscapeDataString(articleTitle)}";
        using var doc = JsonDocument.Parse(await Wikimedia.GetStringAsync(url, ct).ConfigureAwait(false));
        var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
        if (pages.GetArrayLength() == 0 || !pages[0].TryGetProperty("revisions", out var revisions))
            return [];
        var wikitext = revisions[0].GetProperty("slots").GetProperty("main").GetProperty("content").GetString() ?? "";
        return Parse(wikitext);
    }

    /// <summary>Extracts the known credit fields from the first infobox in the wikitext.</summary>
    public static List<Credit> Parse(string wikitext)
    {
        var start = wikitext.IndexOf("{{Infobox", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return [];
        var box = Balanced(wikitext, start);
        var fields = Fields(box);
        var credits = new List<Credit>();
        foreach (var (names, role) in Roles)
        {
            var raw = names.Select(n => fields.GetValueOrDefault(n)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (raw is null)
                continue;
            var values = Clean(raw).Take(role is "Starring" ? 3 : 4).ToList();
            if (values.Count > 0)
                credits.Add(new Credit { Role = role, Name = string.Join(", ", values) });
        }
        return credits;
    }

    /// <summary>The text of a {{…}} template starting at <paramref name="start"/>, braces balanced.</summary>
    private static string Balanced(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length - 1; i++)
        {
            if (text[i] == '{' && text[i + 1] == '{') { depth++; i++; }
            else if (text[i] == '}' && text[i + 1] == '}') { depth--; i++; if (depth == 0) return text[start..(i + 1)]; }
        }
        return text[start..];
    }

    /// <summary>Top-level "| name = value" fields (pipes inside nested templates / links are ignored).</summary>
    private static Dictionary<string, string> Fields(string box)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        int braces = 0, brackets = 0;
        for (var i = 2; i < box.Length - 2; i++)
        {
            var c = box[i];
            if (c == '{' && box[i + 1] == '{') { braces++; current.Append("{{"); i++; continue; }
            if (c == '}' && box[i + 1] == '}') { braces--; current.Append("}}"); i++; continue; }
            if (c == '[' && box[i + 1] == '[') { brackets++; current.Append("[["); i++; continue; }
            if (c == ']' && box[i + 1] == ']') { brackets--; current.Append("]]"); i++; continue; }
            if (c == '|' && braces == 0 && brackets == 0) { parts.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        parts.Add(current.ToString());

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Skip(1))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            fields[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }
        return fields;
    }

    [GeneratedRegex(@"<ref[^>]*/>|<ref[^>]*>.*?</ref>|<!--.*?-->", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex RefsAndComments();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"\[\[(?:[^|\]]*\|)?([^\]]*)\]\]")]
    private static partial Regex Links();

    [GeneratedRegex(@"\{\{\s*(?:ubl|unbulleted list|plainlist|plain list|flatlist|hlist|nowrap|nobr|ubli)\s*\|", RegexOptions.IgnoreCase)]
    private static partial Regex ListTemplates();

    [GeneratedRegex(@"\{\{[^{}]*\}\}")]
    private static partial Regex OtherTemplates();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    /// <summary>Wikitext value → plain names ("[[Koji Kondo]]&lt;br&gt;{{ubl|A|B}}" → Koji Kondo, A, B).</summary>
    private static IEnumerable<string> Clean(string value)
    {
        var s = RefsAndComments().Replace(value, "");
        s = LineBreaks().Replace(s, "\n");
        s = Links().Replace(s, "$1");
        // List-like templates keep their items (one per line); other templates are dropped.
        s = ListTemplates().Replace(s, "{{LIST|");
        for (var guard = 0; guard < 5 && s.Contains("{{"); guard++)
        {
            s = Regex.Replace(s, @"\{\{LIST\|([^{}]*)\}\}", m => "\n" + m.Groups[1].Value.Replace('|', '\n') + "\n");
            s = OtherTemplates().Replace(s, "");
        }
        s = Tags().Replace(s, "").Replace("'''", "").Replace("''", "").Replace("&nbsp;", " ");
        return s.Split(['\n', '*', '•', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.Trim(' ', ':', ';', '(', ')'))
            .Where(v => v.Length is > 1 and < 60 && !v.Contains('=') && !v.Contains('{') && !v.Contains('}'))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

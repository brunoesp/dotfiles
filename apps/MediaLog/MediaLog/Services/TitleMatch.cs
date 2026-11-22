using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaLog.Services;

/// <summary>
/// Forgiving title matching: compares words, not exact text, so a few words in any order
/// ("a link to the past", "zelda past link") find "The Legend of Zelda: A Link to the Past",
/// and the last word may still be being typed ("a link to the pa").
/// </summary>
public static class TitleMatch
{
    private static readonly HashSet<string> Minor = ["a", "an", "the", "of", "and", "to", "in", "on", "de", "la", "le", "el"];

    /// <summary>Lower-case words of letters and digits ("Zelda: A Link…" → zelda, a, link…).</summary>
    public static string[] Words(string text)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
            words.Add(current.ToString());
        return words.ToArray();
    }

    /// <summary>
    /// How well <paramref name="title"/> matches what was typed, from 0 (no shared words)
    /// to 1 (every typed word found); small bonuses for exact titles and titles starting the same.
    /// </summary>
    public static double Score(string title, string query)
    {
        var q = Words(query);
        if (q.Length == 0)
            return 0;
        var t = Words(title);
        if (t.Length == 0)
            return 0;

        double total = 0, found = 0;
        for (var i = 0; i < q.Length; i++)
        {
            var word = q[i];
            var weight = Minor.Contains(word) ? 0.3 : 1.0;
            total += weight;
            var last = i == q.Length - 1;
            // The word being typed matches as a prefix; other words also allow small typos in the
            // ending ("hedgehogs" ~ "hedgehog") by prefix matching when long enough.
            if (t.Contains(word)
                || (last && t.Any(w => w.StartsWith(word, StringComparison.Ordinal)))
                || (word.Length >= 5 && t.Any(w => w.Length >= 5 && (w.StartsWith(word[..^1]) || word.StartsWith(w[..^1])))))
                found += weight;
        }
        var score = found / total;
        var joinedTitle = string.Concat(t);
        var joinedQuery = string.Concat(q);
        if (joinedTitle == joinedQuery)
            score += 0.2;
        else if (joinedTitle.StartsWith(joinedQuery, StringComparison.Ordinal))
            score += 0.1;
        // Prefer titles without many extra words.
        score -= Math.Max(0, t.Length - q.Length) * 0.005;
        return score;
    }

    /// <summary>
    /// Full-text search text for the Wikimedia search engine. Whole words only: wildcards don't
    /// combine reliably with the type filter there. A short last word is probably still being
    /// typed, so it's left out ("a link to the pa" → "a link to the"); the label search that runs
    /// alongside handles prefixes, and <see cref="Score"/> ranks the results.
    /// </summary>
    public static string FullTextQuery(string query)
    {
        var words = Words(query).ToList();
        if (words.Count > 1 && words[^1].Length < 4)
            words.RemoveAt(words.Count - 1);
        return words.Count == 0 ? query : string.Join(' ', words);
    }
}

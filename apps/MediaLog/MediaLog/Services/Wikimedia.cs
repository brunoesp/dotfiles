using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MediaLog.Services;

/// <summary>
/// Polite access to Wikidata / Wikipedia: Wikimedia throttles anonymous apps that send bursts of
/// requests (HTTP 429), which made some lookups come back empty. Requests here are limited to two
/// at a time and spaced out, answers are cached for a while, and 429s are retried after the wait
/// the server asks for.
/// </summary>
public static class Wikimedia
{
    private static readonly SemaphoreSlim Gate = new(3, 3);
    private static readonly object Lock = new();
    private static readonly Dictionary<string, (DateTime Expires, string Body)> Cache = new();
    private static DateTime _next = DateTime.MinValue;

    private static readonly TimeSpan Spacing = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(30);

    private static readonly AsyncLocal<bool> PatientFlag = new();

    /// <summary>
    /// Background work (refreshing all entries) can wait longer when rate limited than a lookup
    /// while typing: set this for the duration of such work.
    /// </summary>
    public static bool Patient
    {
        get => PatientFlag.Value;
        set => PatientFlag.Value = value;
    }

    public static bool Handles(string url) =>
        url.Contains("wikidata.org/", StringComparison.Ordinal) || url.Contains("wikipedia.org/", StringComparison.Ordinal);

    public static async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        lock (Lock)
        {
            if (Cache.TryGetValue(url, out var hit) && hit.Expires > DateTime.UtcNow)
                return hit.Body;
        }

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                TimeSpan wait;
                lock (Lock)
                {
                    var now = DateTime.UtcNow;
                    var start = _next > now ? _next : now;
                    _next = start + Spacing;
                    wait = start - now;
                }
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, ct).ConfigureAwait(false);

                using var response = await Web.Http.GetAsync(url, ct).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < (Patient ? 5 : 2))
                {
                    var retry = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds((Patient ? 3 : 1.5) * (attempt + 1));
                    var cap = TimeSpan.FromSeconds(Patient ? 20 : 6);
                    if (retry > cap)
                        retry = cap;
                    lock (Lock)
                        _next = DateTime.UtcNow + retry; // slow everyone down, not just this request
                    continue;
                }
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                lock (Lock)
                {
                    if (Cache.Count > 400)
                        Cache.Clear();
                    Cache[url] = (DateTime.UtcNow + CacheFor, body);
                }
                return body;
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}

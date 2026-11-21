using System;
using System.Net.Http;

namespace MediaLog.Services;

/// <summary>Shared HTTP client for image search and downloads.</summary>
public static class Web
{
    public static HttpClient Http { get; } = Create();

    private static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // Browsers set their own User-Agent (and forbid changing it); elsewhere identify the app,
        // as Wikimedia and other APIs ask clients to do.
        if (!OperatingSystem.IsBrowser())
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MediaLog/1.0 (personal media tracker)");
        return client;
    }
}

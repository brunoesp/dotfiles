using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using MediaLog.Services;

namespace MediaLog.Browser;

/// <summary>
/// Persists the library in the browser's localStorage, since the WebAssembly
/// file system is in-memory and lost on reload.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed partial class LocalStorageLibraryStorage : ILibraryStorage
{
    private const string Key = "medialog.library";

    [JSImport("globalThis.localStorage.getItem")]
    private static partial string? GetItem(string key);

    [JSImport("globalThis.localStorage.setItem")]
    private static partial void SetItem(string key, string value);

    public string? Read() => GetItem(Key);

    public void Write(string json) => SetItem(Key, json);

    public void Backup(string json) => SetItem($"{Key}.corrupt", json);
}

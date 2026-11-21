using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace MediaLog.Services;

/// <summary>
/// Downloads images by URL and keeps decoded bitmaps in memory (LRU) and the
/// raw files on disk, so chosen covers keep working offline.
/// </summary>
public static class ImageCache
{
    private const int MemoryCapacity = 300;
    private const int DecodeWidth = 400;

    private static readonly Dictionary<string, LinkedListNode<(string Url, Bitmap Bitmap)>> Memory = new();
    private static readonly LinkedList<(string Url, Bitmap Bitmap)> Lru = new();
    private static readonly Dictionary<string, Task<Bitmap?>> InFlight = new();
    private static readonly object Gate = new();

    /// <summary>On-disk cache folder; <c>null</c> in the browser (no persistent file system).</summary>
    private static readonly string? Folder = OperatingSystem.IsBrowser()
        ? null
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create), "MediaLog", "images");

    public static bool TryGetCached(string url, out Bitmap? bitmap)
    {
        lock (Gate)
        {
            if (Memory.TryGetValue(url, out var node))
            {
                Lru.Remove(node);
                Lru.AddFirst(node);
                bitmap = node.Value.Bitmap;
                return true;
            }
        }
        bitmap = null;
        return false;
    }

    /// <summary>Returns the image, or <c>null</c> if it can't be downloaded or decoded.</summary>
    /// <param name="saveToDisk">Keep the file on disk (for images attached to entries).</param>
    public static Task<Bitmap?> GetAsync(string url, bool saveToDisk = true)
    {
        if (TryGetCached(url, out var cached))
            return Task.FromResult(cached);

        lock (Gate)
        {
            if (!InFlight.TryGetValue(url, out var task))
            {
                task = LoadAsync(url, saveToDisk);
                InFlight[url] = task;
            }
            return task;
        }
    }

    /// <summary>Makes sure an (already viewed) image is stored on disk for offline use.</summary>
    public static async Task SaveToDiskAsync(string url)
    {
        if (DiskPath(url) is not { } path || File.Exists(path))
            return;
        try
        {
            var bytes = await Web.Http.GetByteArrayAsync(url).ConfigureAwait(false);
            await WriteAsync(path, bytes).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Offline or gone: the image simply won't be cached.
        }
    }

    private static async Task<Bitmap?> LoadAsync(string url, bool saveToDisk)
    {
        try
        {
            byte[]? bytes = null;
            var path = DiskPath(url);
            if (path is not null && File.Exists(path))
                bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);

            if (bytes is null)
            {
                bytes = await Web.Http.GetByteArrayAsync(url).ConfigureAwait(false);
                if (saveToDisk && path is not null)
                    await WriteAsync(path, bytes).ConfigureAwait(false);
            }

            using var stream = new MemoryStream(bytes);
            var bitmap = Bitmap.DecodeToWidth(stream, DecodeWidth, BitmapInterpolationMode.MediumQuality);
            Remember(url, bitmap);
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            lock (Gate)
                InFlight.Remove(url);
        }
    }

    private static void Remember(string url, Bitmap bitmap)
    {
        lock (Gate)
        {
            if (Memory.ContainsKey(url))
                return;
            Memory[url] = Lru.AddFirst((url, bitmap));
            while (Memory.Count > MemoryCapacity && Lru.Last is { } last)
            {
                Lru.RemoveLast();
                Memory.Remove(last.Value.Url);
            }
        }
    }

    private static string? DiskPath(string url)
    {
        if (Folder is null)
            return null;
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(Folder, hash + ".img");
    }

    private static async Task WriteAsync(string path, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Caching is best effort.
        }
    }
}

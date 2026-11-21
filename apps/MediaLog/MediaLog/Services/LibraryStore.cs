using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediaLog.Models;

namespace MediaLog.Services;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Library))]
internal partial class LibraryJsonContext : JsonSerializerContext;

/// <summary>Loads and saves the library as JSON in the per-user app data folder.</summary>
public sealed class LibraryStore
{
    public LibraryStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create),
            "MediaLog", "library.json");
    }

    public string FilePath { get; }

    public Library Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                using var stream = File.OpenRead(FilePath);
                var library = JsonSerializer.Deserialize(stream, LibraryJsonContext.Default.Library);
                if (library is not null)
                    return library;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Keep the unreadable file around instead of silently overwriting it.
            TryBackupCorruptFile();
        }

        return new Library { Lists = [new MediaList { Name = "My List" }] };
    }

    public void Save(Library library)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        using (var stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, library, LibraryJsonContext.Default.Library);
        }
        File.Move(temp, FilePath, overwrite: true);
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            File.Copy(FilePath, FilePath + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}", overwrite: false);
        }
        catch
        {
            // Best effort only.
        }
    }
}

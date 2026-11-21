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

/// <summary>Where the serialized library lives (a file, browser localStorage, …).</summary>
public interface ILibraryStorage
{
    /// <summary>Returns the stored JSON, or <c>null</c> when nothing has been saved yet.</summary>
    string? Read();

    void Write(string json);

    /// <summary>Called when stored data can't be parsed, before it gets overwritten.</summary>
    void Backup(string json) { }
}

/// <summary>Stores the library as a JSON file (desktop, Android, iOS).</summary>
public sealed class FileLibraryStorage : ILibraryStorage
{
    public FileLibraryStorage(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create),
            "MediaLog", "library.json");
    }

    public string FilePath { get; }

    public string? Read() => File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;

    public void Write(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, FilePath, overwrite: true);
    }

    public void Backup(string json)
    {
        try
        {
            File.WriteAllText(FilePath + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}", json);
        }
        catch
        {
            // Best effort only.
        }
    }
}

/// <summary>Loads and saves the <see cref="Library"/> through an <see cref="ILibraryStorage"/>.</summary>
public sealed class LibraryStore
{
    private readonly ILibraryStorage _storage;

    public LibraryStore(ILibraryStorage? storage = null)
    {
        _storage = storage ?? new FileLibraryStorage();
    }

    public LibraryStore(string filePath) : this(new FileLibraryStorage(filePath))
    {
    }

    public Library Load()
    {
        string? json = null;
        try
        {
            json = _storage.Read();
            if (!string.IsNullOrWhiteSpace(json))
            {
                var library = JsonSerializer.Deserialize(json, LibraryJsonContext.Default.Library);
                if (library is not null)
                    return library;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Keep the unreadable data around instead of silently overwriting it.
            if (json is not null)
                _storage.Backup(json);
        }

        return new Library { Lists = [new MediaList { Name = "My List" }] };
    }

    public void Save(Library library) =>
        _storage.Write(JsonSerializer.Serialize(library, LibraryJsonContext.Default.Library));
}

using System;
using System.Collections.Generic;

namespace MediaLog.Models;

/// <summary>A user-defined list of finished media.</summary>
public sealed class MediaList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>Set for the automatic per-year lists; <c>null</c> for user-created lists.</summary>
    public int? Year { get; set; }

    public List<MediaEntry> Entries { get; set; } = [];
}

/// <summary>Root object persisted to disk.</summary>
public sealed class Library
{
    public int Version { get; set; } = 2;

    /// <summary>Media types; seeded with <see cref="MediaTypeDef.Defaults"/> when empty.</summary>
    public List<MediaTypeDef> Types { get; set; } = [];

    public List<MediaList> Lists { get; set; } = [];

    public LibrarySettings Settings { get; set; } = new();
}

public enum ViewMode
{
    Cards,
    Tiles,
    Compact,
}

/// <summary>UI state that should survive restarts.</summary>
public sealed class LibrarySettings
{
    /// <summary>Whether "All media" is expanded to show one list per type.</summary>
    public bool TypesExpanded { get; set; }

    /// <summary>Type ids whose lists stay visible while "All media" is collapsed.</summary>
    public List<string> PinnedTypes { get; set; } = [];

    /// <summary>View mode per list key (see <c>ListViewModel.Key</c>).</summary>
    public Dictionary<string, ViewMode> ViewModes { get; set; } = [];
}

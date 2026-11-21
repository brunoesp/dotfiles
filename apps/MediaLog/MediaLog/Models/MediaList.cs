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
}

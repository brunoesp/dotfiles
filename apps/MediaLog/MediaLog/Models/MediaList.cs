using System;
using System.Collections.Generic;

namespace MediaLog.Models;

/// <summary>A user-defined list of finished media.</summary>
public sealed class MediaList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<MediaEntry> Entries { get; set; } = [];
}

/// <summary>Root object persisted to disk.</summary>
public sealed class Library
{
    public int Version { get; set; } = 1;
    public List<MediaList> Lists { get; set; } = [];
}

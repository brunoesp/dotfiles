using System;

namespace MediaLog.Models;

/// <summary>A single piece of media that was finished.</summary>
public sealed class MediaEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    /// <summary>Id of the entry's <see cref="MediaTypeDef"/>.</summary>
    public string Kind { get; set; } = "Other";
    public DateOnly CompletedOn { get; set; }
}

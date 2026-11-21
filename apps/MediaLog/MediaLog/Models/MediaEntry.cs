using System;

namespace MediaLog.Models;

/// <summary>A single piece of media that was finished.</summary>
public sealed class MediaEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public MediaKind Kind { get; set; }
    public DateOnly CompletedOn { get; set; }
}

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

    /// <summary>Release / publication year.</summary>
    public int? Year { get; set; }

    /// <summary>Director, author, developer, artist, studio… depending on the type.</summary>
    public string? Creator { get; set; }

    /// <summary>Running time, page count, number of episodes… as display text.</summary>
    public string? Length { get; set; }

    /// <summary>Optional cover / poster image chosen by the user.</summary>
    public string? ImageUrl { get; set; }
}

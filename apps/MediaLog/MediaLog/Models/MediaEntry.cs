using System;
using System.Collections.Generic;

namespace MediaLog.Models;

/// <summary>A single piece of media that was finished.</summary>
public sealed class MediaEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    /// <summary>Id of the entry's <see cref="MediaTypeDef"/>.</summary>
    public string Kind { get; set; } = "Other";
    /// <summary>Completion date; for a to-do entry, the date it was added.</summary>
    public DateOnly CompletedOn { get; set; }

    /// <summary>Not consumed yet: something to watch / play / read later.</summary>
    public bool ToDo { get; set; }

    /// <summary>Release / publication year.</summary>
    public int? Year { get; set; }

    /// <summary>Director, author, developer, artist, studio… depending on the type.</summary>
    public string? Creator { get; set; }

    /// <summary>Running time, page count, number of episodes… as display text.</summary>
    public string? Length { get; set; }

    /// <summary>
    /// Version of the online lookup last applied by "Refresh all info" (2 = with Wikipedia infobox
    /// credits); lets a refresh revisit entries looked up with an older, less complete version.
    /// </summary>
    public int? InfoVersion { get; set; }

    /// <summary>Extra credits: director, composer, publisher, genre… (optional).</summary>
    public List<Credit>? Credits { get; set; }

    /// <summary>Console / platform name (games), matching a <see cref="PlatformDef"/>.</summary>
    public string? Platform { get; set; }

    /// <summary>Optional cover / poster image chosen by the user.</summary>
    public string? ImageUrl { get; set; }
}

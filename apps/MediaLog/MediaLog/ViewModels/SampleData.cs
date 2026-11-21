using System;
using MediaLog.Models;

namespace MediaLog.ViewModels;

/// <summary>Sample library used by the XAML previewer.</summary>
internal static class SampleData
{
    public static Library Create()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        MediaEntry E(string name, string kind, int daysAgo) =>
            new() { Name = name, Kind = kind, CompletedOn = today.AddDays(-daysAgo) };

        return new Library
        {
            Lists =
            [
                new MediaList
                {
                    Name = today.Year.ToString(),
                    Year = today.Year,
                    Entries =
                    [
                        E("Dune: Part Two", "Movie", 2),
                        E("Project Hail Mary", "Book", 5),
                        E("Hollow Knight", "Game", 9),
                    ],
                },
                new MediaList
                {
                    Name = "Favourites",
                    Entries =
                    [
                        E("Spirited Away", "AnimeMovie", 40),
                        E("Clean Code", "TechBook", 60),
                    ],
                },
            ],
        };
    }
}

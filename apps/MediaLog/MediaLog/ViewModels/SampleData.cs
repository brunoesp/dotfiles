using System;
using MediaLog.Models;

namespace MediaLog.ViewModels;

/// <summary>Sample library used by the XAML previewer.</summary>
internal static class SampleData
{
    public static Library Create()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        MediaEntry E(string name, MediaKind kind, int daysAgo) =>
            new() { Name = name, Kind = kind, CompletedOn = today.AddDays(-daysAgo) };

        return new Library
        {
            Lists =
            [
                new MediaList
                {
                    Name = "2026",
                    Entries =
                    [
                        E("Dune: Part Two", MediaKind.Movie, 2),
                        E("Project Hail Mary", MediaKind.Book, 5),
                        E("Hollow Knight", MediaKind.Game, 9),
                        E("Severance — Season 2", MediaKind.Series, 14),
                    ],
                },
                new MediaList
                {
                    Name = "Favourites",
                    Entries =
                    [
                        E("Spirited Away", MediaKind.Anime, 40),
                        E("Watchmen", MediaKind.Comic, 60),
                    ],
                },
            ],
        };
    }
}

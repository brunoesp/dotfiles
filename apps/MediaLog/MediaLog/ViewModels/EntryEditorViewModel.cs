using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Models;

namespace MediaLog.ViewModels;

/// <summary>State of the "Edit entry" dialog.</summary>
public partial class EntryEditorViewModel : ViewModelBase
{
    private readonly Action<EntryEditorViewModel> _save;
    private readonly Action<EntryViewModel> _delete;
    private readonly Action _close;

    public EntryEditorViewModel(EntryViewModel entry, IReadOnlyList<MediaTypeInfo> types, IReadOnlyList<ListViewModel> lists,
        IReadOnlyList<string> platformNames, Action<EntryEditorViewModel> save, Action<EntryViewModel> delete, Action close)
    {
        PlatformNames = platformNames;
        PlatformText = entry.Model.Platform ?? "";
        Entry = entry;
        Types = types;
        Lists = lists;
        _save = save;
        _delete = delete;
        _close = close;

        Name = entry.Model.Name;
        Type = entry.Type;
        Date = entry.Model.CompletedOn.ToDateTime(TimeOnly.MinValue);
        List = entry.List;
        YearText = entry.Model.Year?.ToString() ?? "";
        CreditsText = FormatCredits(entry.Model.Credits);
        Creator = entry.Model.Creator ?? "";
        Length = entry.Model.Length ?? "";
        Cover = new CoverPickerViewModel(entry.Model.ImageUrl, entry.Image, entry.Model.Name, () => Type, () => PlatformText);
        Suggestions = new TitleSuggestionsViewModel(() => Type, () => PlatformText, ApplySuggestion);
        // No cover yet: start suggesting right away.
        if (!Cover.HasImage)
            Cover.SearchIfIdle();
    }

    public EntryViewModel Entry { get; }
    public IReadOnlyList<MediaTypeInfo> Types { get; }
    public IReadOnlyList<ListViewModel> Lists { get; }
    public CoverPickerViewModel Cover { get; }

    /// <summary>Online matches for the title ("Look up").</summary>
    public TitleSuggestionsViewModel Suggestions { get; }

    /// <summary>Known console names for the picker; a new name is added on save.</summary>
    public IReadOnlyList<string> PlatformNames { get; }

    [ObservableProperty]
    public partial string PlatformText { get; set; }

    public bool IsGame => Services.MediaCategories.Of(Type) == Services.MediaCategory.Game;

    partial void OnTypeChanged(MediaTypeInfo value) => OnPropertyChanged(nameof(IsGame));

    [ObservableProperty]
    public partial string YearText { get; set; }

    /// <summary>One "Role: Name" per line.</summary>
    [ObservableProperty]
    public partial string CreditsText { get; set; }

    public static string FormatCredits(IEnumerable<Credit>? credits) =>
        credits is null ? "" : string.Join(Environment.NewLine, credits.Select(c => $"{c.Role}: {c.Name}"));

    public static List<Credit>? ParseCredits(string? text)
    {
        var credits = (text ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
            .Select(parts => new Credit { Role = parts[0], Name = parts[1] })
            .ToList();
        return credits.Count > 0 ? credits : null;
    }

    /// <summary>Director, author, developer, artist, studio…</summary>
    [ObservableProperty]
    public partial string Creator { get; set; }

    /// <summary>Running time, pages, episodes…</summary>
    [ObservableProperty]
    public partial string Length { get; set; }

    [RelayCommand]
    private void LookUp() => Suggestions.SearchNow(Name);

    private async void ApplySuggestion(Services.MediaSuggestion suggestion, Avalonia.Media.Imaging.Bitmap? thumbnail)
    {
        ApplyDetails(suggestion, thumbnail);
        // Complete missing details (credits, year…) from Wikidata.
        var enriched = await Services.MetadataService.EnrichAsync(suggestion, Type, default);
        if (enriched != suggestion)
            ApplyDetails(enriched with { ImageUrl = suggestion.ImageUrl ?? enriched.ImageUrl }, null, onlyMissing: true);
    }

    private void ApplyDetails(Services.MediaSuggestion suggestion, Avalonia.Media.Imaging.Bitmap? thumbnail, bool onlyMissing = false)
    {
        if (suggestion.Credits is { Count: > 0 } credits)
        {
            // Picked match: its credits; later details: only roles that are still missing.
            CreditsText = onlyMissing
                ? FormatCredits(Services.MetadataService.MergeCredits(ParseCredits(CreditsText), credits))
                : FormatCredits(credits);
        }
        if (onlyMissing)
        {
            if (string.IsNullOrWhiteSpace(YearText) && suggestion.Year is { } y)
                YearText = y.ToString();
            if (string.IsNullOrWhiteSpace(Creator) && suggestion.Creator is { } c)
                Creator = c;
            if (string.IsNullOrWhiteSpace(Length) && suggestion.Length is { } l)
                Length = l;
            if (string.IsNullOrWhiteSpace(PlatformText) && suggestion.Platform is { } p)
                PlatformText = p;
            if (Cover.ImageUrl is null && suggestion.ImageUrl is { } u)
                Cover.ImageUrl = u;
            return;
        }
        Name = suggestion.Title;
        if (suggestion.Year is { } year)
            YearText = year.ToString();
        if (suggestion.Creator is { } creator)
            Creator = creator;
        if (suggestion.Length is { } length)
            Length = length;
        if (suggestion.Platform is { } platform && string.IsNullOrWhiteSpace(PlatformText))
            PlatformText = platform;
        if (suggestion.ImageUrl is { } url)
        {
            Cover.SelectedResult = null;
            Cover.ImageUrl = url;
            Cover.Image = thumbnail;
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial MediaTypeInfo Type { get; set; }

    [ObservableProperty]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    public partial ListViewModel List { get; set; }

    partial void OnNameChanged(string oldValue, string newValue)
    {
        // Keep the image search in step with the title until the user edits it separately.
        if (Cover is not null && (Cover.SearchQuery == oldValue || Cover.SearchQuery.Length == 0))
            Cover.SearchQuery = newValue;
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        Cover.CancelSearch();
        Suggestions.Dismiss();
        _save(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        Cover.CancelSearch();
        _close();
    }

    [RelayCommand]
    private void Delete()
    {
        Cover.CancelSearch();
        _delete(Entry);
        _close();
    }
}

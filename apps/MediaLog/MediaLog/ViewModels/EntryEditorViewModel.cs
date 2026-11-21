using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Models;
using MediaLog.Services;

namespace MediaLog.ViewModels;

/// <summary>An online image suggestion with its lazily loaded thumbnail.</summary>
public partial class ImageResultViewModel : ViewModelBase
{
    public ImageResultViewModel(ImageResult result)
    {
        Result = result;
        Load();
    }

    public ImageResult Result { get; }
    public string Tip => $"{Result.Title} · {Result.Source}";

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    [ObservableProperty]
    public partial bool Failed { get; set; }

    private async void Load()
    {
        // Suggestions are only kept in memory; the chosen one is saved to disk on save.
        var bitmap = await ImageCache.GetAsync(Result.Url, saveToDisk: false);
        Dispatcher.UIThread.Post(() =>
        {
            Thumbnail = bitmap;
            Failed = bitmap is null;
        });
    }
}

/// <summary>State of the "Edit entry" dialog.</summary>
public partial class EntryEditorViewModel : ViewModelBase
{
    private readonly Action<EntryEditorViewModel> _save;
    private readonly Action<EntryViewModel> _delete;
    private readonly Action _close;
    private CancellationTokenSource? _search;

    public EntryEditorViewModel(EntryViewModel entry, IReadOnlyList<MediaTypeInfo> types, IReadOnlyList<ListViewModel> lists,
        Action<EntryEditorViewModel> save, Action<EntryViewModel> delete, Action close)
    {
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
        ImageUrl = entry.Model.ImageUrl;
        Image = entry.Image;
        SearchQuery = entry.Model.Name;
        Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasResults));
    }

    public bool HasResults => Results.Count > 0;

    public EntryViewModel Entry { get; }
    public IReadOnlyList<MediaTypeInfo> Types { get; }
    public IReadOnlyList<ListViewModel> Lists { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial MediaTypeInfo Type { get; set; }

    [ObservableProperty]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    public partial ListViewModel List { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    public partial string? ImageUrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    public partial Bitmap? Image { get; set; }

    public bool HasImage => ImageUrl is not null;

    // --- Image search ---------------------------------------------------------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchImagesCommand))]
    public partial string SearchQuery { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial string? SearchStatus { get; set; }

    public ObservableCollection<ImageResultViewModel> Results { get; } = [];

    [ObservableProperty]
    public partial ImageResultViewModel? SelectedResult { get; set; }

    [ObservableProperty]
    public partial string PastedUrl { get; set; } = "";

    partial void OnSelectedResultChanged(ImageResultViewModel? value)
    {
        if (value is null)
            return;
        ImageUrl = value.Result.Url;
        Image = value.Thumbnail;
    }

    partial void OnNameChanged(string value)
    {
        // Keep the search box in step with the title until the user edits it separately.
        if (SearchQuery is null || SearchQuery == Entry.Model.Name || SearchQuery.Length == 0)
            SearchQuery = value;
    }

    private bool CanSearch() => !string.IsNullOrWhiteSpace(SearchQuery);

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchImages()
    {
        _search?.Cancel();
        var cts = _search = new CancellationTokenSource();
        Results.Clear();
        IsSearching = true;
        SearchStatus = null;
        try
        {
            var results = await ImageSearchService.SearchAsync(SearchQuery, Type, cts.Token);
            if (cts.IsCancellationRequested)
                return;
            foreach (var result in results)
                Results.Add(new ImageResultViewModel(result));
            SearchStatus = results.Count == 0
                ? "No images found. Try a different search, or check your internet connection."
                : $"{results.Count} suggestions. Tap one to use it.";
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_search == cts)
                IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task UsePastedUrl()
    {
        var url = PastedUrl.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            SearchStatus = "That doesn't look like an image link.";
            return;
        }
        SelectedResult = null;
        ImageUrl = url;
        Image = await ImageCache.GetAsync(url, saveToDisk: false);
        SearchStatus = Image is null ? "Couldn't load that image." : null;
        PastedUrl = "";
    }

    [RelayCommand]
    private void RemoveImage()
    {
        SelectedResult = null;
        ImageUrl = null;
        Image = null;
    }

    // --- Dialog actions --------------------------------------------------------------

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        _search?.Cancel();
        _save(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        _search?.Cancel();
        _close();
    }

    [RelayCommand]
    private void Delete()
    {
        _search?.Cancel();
        _delete(Entry);
        _close();
    }
}

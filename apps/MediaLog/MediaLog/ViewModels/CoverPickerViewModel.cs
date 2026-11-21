using System;
using System.Collections.ObjectModel;
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
        // Suggestions are only kept in memory; the chosen one is saved to disk when used.
        var bitmap = await ImageCache.GetAsync(Result.Url, saveToDisk: false);
        Dispatcher.UIThread.Post(() =>
        {
            Thumbnail = bitmap;
            Failed = bitmap is null;
        });
    }
}

/// <summary>
/// Chooses a cover image: online suggestions by title, a pasted link, or none.
/// Used by the entry editor and by the add bar.
/// </summary>
public partial class CoverPickerViewModel : ViewModelBase
{
    private readonly Func<MediaTypeInfo?> _type;
    private CancellationTokenSource? _search;

    /// <param name="type">Media type used to pick suitable image sources.</param>
    public CoverPickerViewModel(string? imageUrl, Bitmap? image, string query, Func<MediaTypeInfo?> type)
    {
        _type = type;
        ImageUrl = imageUrl;
        Image = image;
        SearchQuery = query;
        Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasResults));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    public partial string? ImageUrl { get; set; }

    [ObservableProperty]
    public partial Bitmap? Image { get; set; }

    public bool HasImage => ImageUrl is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial string SearchQuery { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial string? SearchStatus { get; set; }

    public ObservableCollection<ImageResultViewModel> Results { get; } = [];

    public bool HasResults => Results.Count > 0;

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

    private bool CanSearch() => !string.IsNullOrWhiteSpace(SearchQuery);

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task Search()
    {
        _search?.Cancel();
        var cts = _search = new CancellationTokenSource();
        Results.Clear();
        IsSearching = true;
        SearchStatus = null;
        try
        {
            // Show each source's suggestions as soon as they arrive.
            await foreach (var batch in ImageSearchService.SearchAsync(SearchQuery, _type(), cts.Token))
            {
                if (cts.IsCancellationRequested)
                    return;
                foreach (var result in batch)
                    Results.Add(new ImageResultViewModel(result));
                SearchStatus = $"{Results.Count} suggestions. Tap one to use it.";
            }
            if (!cts.IsCancellationRequested && Results.Count == 0)
                SearchStatus = "No images found. Try a different search, or check your internet connection.";
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

    /// <summary>Searches right away when there's a query and nothing has been searched yet.</summary>
    public void SearchIfIdle()
    {
        if (!IsSearching && !HasResults && CanSearch())
            SearchCommand.Execute(null);
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
    private void Remove()
    {
        SelectedResult = null;
        ImageUrl = null;
        Image = null;
    }

    public void CancelSearch() => _search?.Cancel();
}

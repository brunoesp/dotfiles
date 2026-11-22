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

/// <summary>One title match in the suggestion list.</summary>
public partial class SuggestionViewModel : ViewModelBase
{
    private readonly Action<SuggestionViewModel> _pick;

    public SuggestionViewModel(MediaSuggestion suggestion, Action<SuggestionViewModel> pick)
    {
        Suggestion = suggestion;
        _pick = pick;
        LoadThumbnail();
    }

    public MediaSuggestion Suggestion { get; }
    public string Title => Suggestion.Title;
    public string Details => Suggestion.Details;
    public bool HasDetails => Details.Length > 0;
    public string Source => Suggestion.Description is { Length: > 0 } d ? $"{d} · {Suggestion.Source}" : Suggestion.Source;

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    [RelayCommand]
    private void Pick() => _pick(this);

    private async void LoadThumbnail()
    {
        if (Suggestion.ImageUrl is not { } url)
            return;
        var bitmap = await ImageCache.GetAsync(url, saveToDisk: false);
        Dispatcher.UIThread.Post(() => Thumbnail = bitmap);
    }
}

/// <summary>
/// Looks up a title online (debounced while typing) and lists matching works with
/// their details. Used by the add bar and the entry editor.
/// </summary>
public partial class TitleSuggestionsViewModel : ViewModelBase
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(400);

    private readonly Func<MediaTypeInfo?> _type;
    private readonly Action<MediaSuggestion, Bitmap?> _picked;
    private CancellationTokenSource? _cts;

    public TitleSuggestionsViewModel(Func<MediaTypeInfo?> type, Action<MediaSuggestion, Bitmap?> picked)
    {
        _type = type;
        _picked = picked;
    }

    public ObservableCollection<SuggestionViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool HasItems { get; set; }

    /// <summary>Finished searching without matches.</summary>
    public bool IsEmpty => !IsLoading && !HasItems;

    /// <summary>Searches after a short pause in typing.</summary>
    public void Update(string text) => _ = RunAsync(text, Debounce);

    /// <summary>Searches right away.</summary>
    public void SearchNow(string text) => _ = RunAsync(text, TimeSpan.Zero);

    [RelayCommand]
    public void Dismiss()
    {
        _cts?.Cancel();
        IsOpen = false;
        IsLoading = false;
        Items.Clear();
        HasItems = false;
    }

    private async Task RunAsync(string text, TimeSpan delay)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        if (text.Trim().Length < 2)
        {
            Dismiss();
            return;
        }
        try
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cts.Token);
            Items.Clear();
            HasItems = false;
            IsLoading = true;
            IsOpen = true;
            await foreach (var batch in MetadataService.SuggestAsync(text, _type(), cts.Token))
            {
                if (cts.IsCancellationRequested)
                    return;
                foreach (var suggestion in batch)
                    Items.Add(new SuggestionViewModel(suggestion, Pick));
                HasItems = Items.Count > 0;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (_cts == cts)
            IsLoading = false;
    }

    private void Pick(SuggestionViewModel item)
    {
        var thumbnail = item.Thumbnail;
        Dismiss();
        _picked(item.Suggestion, thumbnail);
    }
}

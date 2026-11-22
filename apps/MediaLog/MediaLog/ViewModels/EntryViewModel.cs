using System;
using System.Globalization;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Models;
using MediaLog.Services;

namespace MediaLog.ViewModels;

public partial class EntryViewModel : ViewModelBase
{
    private readonly Action<EntryViewModel> _delete;
    private readonly Action<EntryViewModel> _edit;

    public EntryViewModel(MediaEntry model, MediaTypeInfo type, ListViewModel list, bool showList,
        Action<EntryViewModel> delete, Action<EntryViewModel> edit, PlatformInfo? platform = null)
    {
        Platform = platform;
        Model = model;
        Type = type;
        List = list;
        ShowList = showList;
        _delete = delete;
        _edit = edit;
        LoadImage();
    }

    public MediaEntry Model { get; }

    /// <summary>The list the entry is stored in.</summary>
    public ListViewModel List { get; }

    public MediaTypeInfo Type { get; }

    /// <summary>Console badge (games).</summary>
    public PlatformInfo? Platform { get; }

    public bool HasPlatform => Platform is not null;

    public bool HasDetailsOrPlatform => HasDetails || HasPlatform;

    /// <summary>Whether the owning list name is shown (when it differs from the list being viewed).</summary>
    public bool ShowList { get; }

    public string Name => Model.Name;

    /// <summary>"2024 · Denis Villeneuve · 2h 46m" (empty when unknown).</summary>
    public string Details => string.Join(" · ",
        new[] { Model.Year?.ToString(CultureInfo.InvariantCulture), Model.Creator, Model.Length }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    public bool HasDetails => Details.Length > 0;

    public string? Creator => Model.Creator;
    public bool HasCreator => !string.IsNullOrWhiteSpace(Model.Creator);

    /// <summary>"2013 · 14 tracks"</summary>
    public string YearAndLength => string.Join(" · ",
        new[] { Model.Year?.ToString(CultureInfo.InvariantCulture), Model.Length }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public bool HasYearAndLength => YearAndLength.Length > 0;

    /// <summary>
    /// Tile shape: follows the chosen image (square-ish art such as album covers or retro game
    /// boxes → 1:1, tall posters / book covers → 2:3); without an image, albums and podcasts
    /// are square and everything else 2:3.
    /// </summary>
    public bool IsSquareCover
    {
        get
        {
            if (Image is { PixelSize: { Width: > 0, Height: > 0 } size })
            {
                // Closest of 1:1 and 2:3 (compared on a log scale: the midpoint is √1.5 ≈ 1.22).
                return (double)size.Height / size.Width < 1.22;
            }
            return MediaCategories.Of(Type) is MediaCategory.Music or MediaCategory.Podcast;
        }
    }

    /// <summary>Cover height ÷ width for tiles.</summary>
    public double CoverRatio => IsSquareCover ? 1.0 : 1.5;

    public string DateText => Model.CompletedOn.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>Edit / delete buttons are only shown in edit mode.</summary>
    [ObservableProperty]
    public partial bool ShowActions { get; set; }

    /// <summary>Cover image, once downloaded (or from cache).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage), nameof(IsSquareCover), nameof(CoverRatio))]
    public partial Bitmap? Image { get; set; }

    public bool HasImage => Image is not null;

    [RelayCommand]
    private void Delete() => _delete(this);

    [RelayCommand]
    private void Edit() => _edit(this);

    private async void LoadImage()
    {
        if (Model.ImageUrl is not { Length: > 0 } url)
            return;
        // Synchronous cache hit avoids a flash of the placeholder when the list is rebuilt.
        if (ImageCache.TryGetCached(url, out var cached))
        {
            Image = cached;
            return;
        }
        var bitmap = await ImageCache.GetAsync(url);
        Dispatcher.UIThread.Post(() => Image = bitmap);
    }
}

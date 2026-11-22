using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Models;
using MediaLog.Services;

namespace MediaLog.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly LibraryStore? _store;
    private readonly Library _library;
    private readonly DispatcherTimer? _yearTimer;
    private readonly Dictionary<string, MediaTypeInfo> _typeById = new();
    private (MediaEntry Entry, ListViewModel List, int Index)? _lastDeleted;
    private bool _inRefresh;

    /// <summary>Design-time / preview constructor with sample data and no persistence.</summary>
    public MainViewModel() : this(null, SampleData.Create())
    {
    }

    public MainViewModel(LibraryStore store) : this(store, store.Load())
    {
    }

    private MainViewModel(LibraryStore? store, Library library)
    {
        _store = store;
        _library = library;

        if (_library.Types.Count == 0)
            _library.Types.AddRange(MediaTypeDef.Defaults());
        UpgradeDefaultColors();
        RebuildTypes();
        if (_library.Platforms.Count == 0)
            _library.Platforms.AddRange(Services.Platforms.Defaults());
        RebuildPlatforms();

        Master = ListViewModel.CreateMaster(_ => ToggleTypesExpanded());
        Master.IsExpanded = _library.Settings.TypesExpanded;
        NavItems.Add(Master);
        foreach (var list in _library.Lists)
            AddListViewModel(list);

        NewType = Types[0];
        NewDate = DateTime.Today;
        NewCover = CreateNewCover();
        NewSuggestions = new TitleSuggestionsViewModel(() => NewType, () => NewPlatform, ApplyNewSuggestion);
        SyncYearLists();
        SelectedNav = CurrentYearList;
        StartNewType();

        // Roll over to a new year list if the app is left open across New Year.
        if (store is not null)
        {
            _yearTimer = new DispatcherTimer(TimeSpan.FromMinutes(5), DispatcherPriority.Background, (_, _) => CheckYear());
            _yearTimer.Start();
        }
    }

    // ==========================================================================
    // Types
    // ==========================================================================

    public ObservableCollection<MediaTypeInfo> Types { get; } = [];

    public MediaTypeInfo TypeFor(string id) =>
        _typeById.TryGetValue(id, out var type)
            ? type
            : new MediaTypeInfo(new MediaTypeDef { Id = id, Label = id, Color = "#78909C", Icon = "dots" });

    /// <summary>Moves built-in types still on an old default color to the current, more distinct palette.</summary>
    private void UpgradeDefaultColors()
    {
        var defaults = MediaTypeDef.Defaults().ToDictionary(d => d.Id);
        var changed = false;
        foreach (var def in _library.Types)
        {
            if (defaults.TryGetValue(def.Id, out var current)
                && MediaTypeDef.PreviousDefaultColors.TryGetValue(def.Id, out var previous)
                && previous.Contains(def.Color, StringComparer.OrdinalIgnoreCase)
                && !string.Equals(def.Color, current.Color, StringComparison.OrdinalIgnoreCase))
            {
                def.Color = current.Color;
                changed = true;
            }
        }
        if (changed)
            Persist();
    }

    private void RebuildTypes()
    {
        var selectedId = NewType?.Id;
        _typeById.Clear();
        Types.Clear();
        foreach (var def in _library.Types)
        {
            var info = new MediaTypeInfo(def);
            _typeById[def.Id] = info;
            Types.Add(info);
        }
        if (selectedId is not null)
            NewType = _typeById.GetValueOrDefault(selectedId) ?? Types.FirstOrDefault();

        // Keep per-type sidebar lists in sync with renamed / recolored / deleted types.
        foreach (var (id, vm) in _typeLists.ToList())
        {
            if (_typeById.TryGetValue(id, out var info))
            {
                vm.Type = info;
            }
            else
            {
                _typeLists.Remove(id);
                NavItems.Remove(vm);
            }
        }
    }

    // ==========================================================================
    // Consoles (games)
    // ==========================================================================

    private readonly Dictionary<string, PlatformInfo> _platformByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Known console names, offered by the console pickers.</summary>
    public ObservableCollection<string> PlatformNames { get; } = [];

    private void RebuildPlatforms()
    {
        _platformByName.Clear();
        PlatformNames.Clear();
        foreach (var def in _library.Platforms)
        {
            _platformByName[def.Name] = new PlatformInfo(def);
            PlatformNames.Add(def.Name);
        }
    }

    private PlatformInfo? PlatformFor(string? name) =>
        name is { Length: > 0 } ? _platformByName.GetValueOrDefault(name) : null;

    /// <summary>
    /// Resolves typed console text to a known platform ("snes" → "Super Nintendo"),
    /// adding a new one when it isn't known yet. Returns <c>null</c> for empty text.
    /// </summary>
    private string? EnsurePlatform(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (Services.Platforms.Find(_library.Platforms, text) is { } known)
            return known.Name;
        var created = Services.Platforms.Create(text);
        if (Services.Platforms.Find(_library.Platforms, created.Name) is { } existing)
            return existing.Name;
        _library.Platforms.Add(created);
        RebuildPlatforms();
        Persist();
        return created.Name;
    }

    /// <summary>Console typed in the add bar (games only).</summary>
    [ObservableProperty]
    public partial string NewPlatform { get; set; } = "";

    public bool IsNewGame => Services.MediaCategories.Of(NewType) == Services.MediaCategory.Game;

    partial void OnNewPlatformChanged(string value)
    {
        // A retro console adds its box-art titles to the lookup: refresh open suggestions.
        if (NewSuggestions is { IsOpen: true })
            NewSuggestions.Update(NewName);
    }

    // ==========================================================================
    // Per-type lists under "All media"
    // ==========================================================================

    private readonly Dictionary<string, ListViewModel> _typeLists = new();

    private ListViewModel TypeList(MediaTypeInfo type)
    {
        if (!_typeLists.TryGetValue(type.Id, out var vm))
        {
            vm = ListViewModel.ForType(type, TogglePin);
            vm.IsPinned = _library.Settings.PinnedTypes.Contains(type.Id);
            _typeLists[type.Id] = vm;
        }
        return vm;
    }

    private void ToggleTypesExpanded()
    {
        Master.IsExpanded = !Master.IsExpanded;
        _library.Settings.TypesExpanded = Master.IsExpanded;
        Persist();
        // Collapsing hides an unpinned type list that may be selected.
        if (!Master.IsExpanded && SelectedNav is { IsTypeList: true, IsPinned: false })
            SelectedNav = Master;
        Refresh();
    }

    private void TogglePin(ListViewModel list)
    {
        if (list.Type is not { } type)
            return;
        list.IsPinned = !list.IsPinned;
        _library.Settings.PinnedTypes.Remove(type.Id);
        if (list.IsPinned)
            _library.Settings.PinnedTypes.Add(type.Id);
        Persist();
        Refresh();
    }

    /// <summary>
    /// Shows a list per type with entries right below "All media": all of them when
    /// expanded, only the pinned ones when collapsed.
    /// </summary>
    private void SyncTypeNav(IReadOnlyDictionary<string, int> perType)
    {
        var desired = Types
            .Where(t => perType.GetValueOrDefault(t.Id) > 0)
            .Select(TypeList)
            .Where(vm => Master.IsExpanded || vm.IsPinned)
            .ToList();

        foreach (var stale in NavItems.Where(n => n.IsTypeList && !desired.Contains(n)).ToList())
            NavItems.Remove(stale);
        for (var i = 0; i < desired.Count; i++)
        {
            var vm = desired[i];
            vm.Count = perType[vm.Type!.Id];
            var current = NavItems.IndexOf(vm);
            if (current == i + 1)
                continue;
            if (current >= 0)
                NavItems.Move(current, i + 1);
            else
                NavItems.Insert(i + 1, vm);
        }
    }

    private int TypeNavCount => NavItems.Skip(1).TakeWhile(n => n.IsTypeList).Count();

    // ==========================================================================
    // View modes
    // ==========================================================================

    private bool _loadingViewMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCardsMode), nameof(IsTilesMode), nameof(IsCompactMode))]
    public partial ViewMode CurrentViewMode { get; set; }

    public bool IsCardsMode => CurrentViewMode == ViewMode.Cards;
    public bool IsTilesMode => CurrentViewMode == ViewMode.Tiles;
    public bool IsCompactMode => CurrentViewMode == ViewMode.Compact;

    /// <summary>Number of tile columns; set by the view from the available width.</summary>
    [ObservableProperty]
    public partial int TileColumns { get; set; } = 4;

    [RelayCommand]
    private void SetViewMode(string mode)
    {
        if (Enum.TryParse<ViewMode>(mode, out var value))
            CurrentViewMode = value;
    }

    partial void OnCurrentViewModeChanged(ViewMode value)
    {
        if (_loadingViewMode || SelectedNav is null)
            return;
        _library.Settings.ViewModes[SelectedNav.Key] = value;
        Persist();
    }

    // ==========================================================================
    // Navigation
    // ==========================================================================

    public ListViewModel Master { get; }

    /// <summary>Sidebar items: master list, year lists (newest first), then custom lists.</summary>
    public ObservableCollection<ListViewModel> NavItems { get; } = [];

    /// <summary>Stored lists (year + custom): the possible targets for new entries.</summary>
    public ObservableCollection<ListViewModel> UserLists { get; } = [];

    public ObservableCollection<EntryViewModel> Entries { get; } = [];

    public ObservableCollection<TypeStatViewModel> Stats { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMasterSelected), nameof(NeedsTargetList), nameof(IsRenamableSelected), nameof(CanDeleteSelected))]
    [NotifyCanExecuteChangedFor(nameof(AddEntryCommand))]
    public partial ListViewModel? SelectedNav { get; set; }

    public bool IsMasterSelected => SelectedNav?.IsMaster == true;

    /// <summary>"All media" and type lists aren't stored lists: new entries need a target list.</summary>
    public bool NeedsTargetList => SelectedNav is { Model: null };

    /// <summary>Selected list can be renamed in place (custom lists only).</summary>
    public bool IsRenamableSelected => SelectedNav?.CanRename == true;

    /// <summary>Only custom lists can be deleted; year lists follow the entries' dates.</summary>
    public bool CanDeleteSelected => SelectedNav is { Model: not null, IsYearList: false };

    /// <summary>The automatic list for the current calendar year.</summary>
    public ListViewModel? CurrentYearList => YearList(DateTime.Today.Year);

    private ListViewModel? YearList(int year) => UserLists.FirstOrDefault(l => l.Model!.Year == year);

    /// <summary>Total number of entries in the selected scope (before search / type filter).</summary>
    [ObservableProperty]
    public partial int ScopeCount { get; set; }

    [ObservableProperty]
    public partial string CountText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string EmptyText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFiltered))]
    public partial string? TypeFilter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFiltered))]
    public partial string SearchText { get; set; } = "";

    public bool IsFiltered => TypeFilter is not null || !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>Whether the navigation pane is open (only meaningful in compact layout).</summary>
    [ObservableProperty]
    public partial bool IsPaneOpen { get; set; }

    partial void OnSelectedNavChanged(ListViewModel? value)
    {
        IsConfirmingDelete = false;
        if (value is not null)
        {
            _loadingViewMode = true;
            CurrentViewMode = _library.Settings.ViewModes.GetValueOrDefault(value.Key, ViewMode.Cards);
            _loadingViewMode = false;
        }
        if (value?.Type is { } type)
            NewType = type;
        if (value is { Model: not null })
            TargetList = value;
        else if (TargetList is null || !UserLists.Contains(TargetList))
            TargetList = CurrentYearList ?? UserLists.FirstOrDefault();
        Refresh();
    }

    partial void OnTypeFilterChanged(string? value) => Refresh();

    partial void OnSearchTextChanged(string value) => Refresh();

    [RelayCommand]
    private void ClearFilters()
    {
        TypeFilter = null;
        SearchText = "";
    }

    [RelayCommand]
    private void TogglePane() => IsPaneOpen = !IsPaneOpen;

    private void ToggleType(TypeStatViewModel stat) =>
        TypeFilter = TypeFilter == stat.Type.Id ? null : stat.Type.Id;

    // ==========================================================================
    // Entries
    // ==========================================================================

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddEntryCommand))]
    public partial string NewName { get; set; } = "";

    [ObservableProperty]
    public partial MediaTypeInfo? NewType { get; set; }

    // --- Title lookup while typing ---------------------------------------------------

    /// <summary>Online matches for the title being typed.</summary>
    public TitleSuggestionsViewModel NewSuggestions { get; private set; } = null!;

    /// <summary>Details (year, creator, length) of the picked match, saved with the new entry.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewDetails), nameof(NewDetailsText))]
    public partial MediaSuggestion? NewDetails { get; set; }

    public bool HasNewDetails => NewDetails is not null;
    public string NewDetailsText => NewDetails?.Details is { Length: > 0 } d ? d : "Matched online";

    private bool _applyingSuggestion;

    partial void OnNewNameChanged(string value)
    {
        if (_applyingSuggestion || NewSuggestions is null)
            return;
        // Editing the title after picking a match drops the match's details.
        if (NewDetails is not null && value.Trim() != NewDetails.Title)
            NewDetails = null;
        NewSuggestions.Update(value);
    }

    partial void OnNewTypeChanged(MediaTypeInfo? value)
    {
        OnPropertyChanged(nameof(IsNewGame));
        // Different type, different databases: look the title up again.
        if (NewSuggestions is { IsOpen: true })
            NewSuggestions.SearchNow(NewName);
    }

    private void ApplyNewSuggestion(MediaSuggestion suggestion, Avalonia.Media.Imaging.Bitmap? thumbnail)
    {
        _applyingSuggestion = true;
        NewName = suggestion.Title;
        _applyingSuggestion = false;
        NewDetails = suggestion;
        _ = EnrichNewDetailsAsync(suggestion);
        if (suggestion.Platform is { } platform && string.IsNullOrWhiteSpace(NewPlatform))
            NewPlatform = Services.Platforms.Find(_library.Platforms, platform)?.Name ?? platform;
        if (suggestion.ImageUrl is { } url)
        {
            NewCover.ImageUrl = url;
            NewCover.Image = thumbnail;
            if (thumbnail is null)
                _ = LoadNewCoverAsync(url);
        }
    }

    /// <summary>Completes a picked match's details (credits, year…) from Wikidata in the background.</summary>
    private async System.Threading.Tasks.Task EnrichNewDetailsAsync(MediaSuggestion suggestion)
    {
        var enriched = await MetadataService.EnrichAsync(suggestion, NewType, default);
        if (!ReferenceEquals(NewDetails, suggestion) || enriched == suggestion)
            return;
        NewDetails = enriched;
        if (string.IsNullOrWhiteSpace(NewPlatform) && enriched.Platform is { } platform)
            NewPlatform = Services.Platforms.Find(_library.Platforms, platform)?.Name ?? platform;
        if (NewCover.ImageUrl is null && enriched.ImageUrl is { } url)
        {
            NewCover.ImageUrl = url;
            _ = LoadNewCoverAsync(url);
        }
    }

    private async System.Threading.Tasks.Task LoadNewCoverAsync(string url)
    {
        var bitmap = await ImageCache.GetAsync(url);
        if (NewCover.ImageUrl == url)
            NewCover.Image = bitmap;
    }

    [RelayCommand]
    private void ClearNewDetails() => NewDetails = null;

    /// <summary>Cover chosen from the add bar for the next new entry.</summary>
    [ObservableProperty]
    public partial CoverPickerViewModel NewCover { get; set; } = null!;

    [ObservableProperty]
    public partial bool IsNewCoverOpen { get; set; }

    private string _newCoverQuery = "";

    private CoverPickerViewModel CreateNewCover() => new(null, null, "", () => NewType, () => NewPlatform);

    [RelayCommand]
    private void OpenNewCover()
    {
        // Search for the title being typed (unless the user changed the search themselves).
        var title = NewName.Trim();
        if (NewCover.SearchQuery.Length == 0 || NewCover.SearchQuery == _newCoverQuery)
        {
            if (NewCover.SearchQuery != title)
                NewCover.Results.Clear();
            NewCover.SearchQuery = title;
            _newCoverQuery = title;
        }
        IsNewCoverOpen = true;
        NewCover.SearchIfIdle();
    }

    [RelayCommand]
    private void CloseNewCover()
    {
        NewCover.CancelSearch();
        IsNewCoverOpen = false;
    }

    [ObservableProperty]
    public partial DateTime? NewDate { get; set; }

    /// <summary>Target list when adding from the master view.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddEntryCommand))]
    public partial ListViewModel? TargetList { get; set; }

    [ObservableProperty]
    public partial string? UndoText { get; set; }

    private bool CanAddEntry() =>
        !string.IsNullOrWhiteSpace(NewName) && (NeedsTargetList ? TargetList : SelectedNav) is { Model: not null };

    [RelayCommand(CanExecute = nameof(CanAddEntry))]
    private void AddEntry()
    {
        var target = NeedsTargetList ? TargetList : SelectedNav;
        if (target?.Model is null || string.IsNullOrWhiteSpace(NewName))
            return;

        var date = DateOnly.FromDateTime(NewDate ?? DateTime.Today);
        var type = NewType ?? Types[0];
        // Year lists hold the entries of their year: route to the list matching the date.
        if (target.IsYearList)
            target = EnsureYearList(date.Year);

        var imageUrl = NewCover.ImageUrl;
        var details = NewDetails;
        target.Model!.Entries.Add(new MediaEntry
        {
            Name = NewName.Trim(), Kind = type.Id, CompletedOn = date, ImageUrl = imageUrl,
            Year = details?.Year, Creator = details?.Creator, Length = details?.Length,
            Credits = details?.Credits is { Count: > 0 } credits ? credits.ToList() : null,
            Platform = IsNewGame ? EnsurePlatform(NewPlatform) : null,
        });
        if (imageUrl is not null)
            _ = ImageCache.SaveToDiskAsync(imageUrl);
        NewName = "";
        NewDetails = null;
        // The console is kept: several games for the same console are often logged in a row.
        NewSuggestions.Dismiss();
        NewCover.CancelSearch();
        NewCover = CreateNewCover();
        _newCoverQuery = "";
        // Make sure the newly added item is visible.
        if (TypeFilter is not null && TypeFilter != type.Id)
            TypeFilter = null;
        Persist();
        Refresh();
    }

    private void DeleteEntry(EntryViewModel entry)
    {
        var entries = entry.List.Model!.Entries;
        var index = entries.IndexOf(entry.Model);
        if (index < 0)
            return;
        entries.RemoveAt(index);
        _lastDeleted = (entry.Model, entry.List, index);
        UndoText = $"Removed “{entry.Name}”";
        Persist();
        Refresh();
    }

    [RelayCommand]
    private void Undo()
    {
        if (_lastDeleted is { } d)
        {
            var list = d.List;
            // An emptied year list is dropped automatically; bring it back for the restored entry.
            if (!_library.Lists.Contains(list.Model!) && list.Model!.Year is { } year)
                list = EnsureYearList(year);
            if (_library.Lists.Contains(list.Model!))
            {
                var entries = list.Model!.Entries;
                entries.Insert(Math.Min(d.Index, entries.Count), d.Entry);
                Persist();
                Refresh();
            }
        }
        DismissUndo();
    }

    [RelayCommand]
    private void DismissUndo()
    {
        _lastDeleted = null;
        UndoText = null;
    }

    // ==========================================================================
    // Refresh info for all entries
    // ==========================================================================

    private System.Threading.CancellationTokenSource? _refreshCts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshAllInfoCommand))]
    public partial bool IsRefreshing { get; set; }

    /// <summary>Progress / result text; <c>null</c> hides the status bar.</summary>
    [ObservableProperty]
    public partial string? RefreshStatus { get; set; }

    [ObservableProperty]
    public partial double RefreshProgress { get; set; }

    private bool CanRefreshAll() => !IsRefreshing;

    /// <summary>
    /// Looks every entry up online again and fills in what's missing (year, creator, length,
    /// credits, console, cover). Nothing already filled in is changed.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefreshAll))]
    private async System.Threading.Tasks.Task RefreshAllInfo()
    {
        var cts = _refreshCts = new System.Threading.CancellationTokenSource();
        var entries = AllEntries().Select(x => x.Entry).ToList();
        IsRefreshing = true;
        RefreshProgress = 0;
        Services.Wikimedia.Patient = true; // background work: wait out rate limits instead of giving up
        var updated = 0;
        var done = 0;
        try
        {
            foreach (var entry in entries)
            {
                cts.Token.ThrowIfCancellationRequested();
                RefreshStatus = $"Refreshing info {done + 1} of {entries.Count}: {entry.Name}";
                if (await RefreshEntryAsync(entry, cts.Token))
                {
                    updated++;
                    Persist();
                    Refresh();
                }
                done++;
                RefreshProgress = (double)done / entries.Count;
            }
            RefreshStatus = $"Done: updated {updated} of {entries.Count} entries.";
        }
        catch (OperationCanceledException)
        {
            RefreshStatus = $"Stopped: updated {updated} of {done} entries checked.";
        }
        finally
        {
            IsRefreshing = false;
            Services.Wikimedia.Patient = false;
        }
    }

    [RelayCommand]
    private void CancelRefresh() => _refreshCts?.Cancel();

    [RelayCommand]
    private void DismissRefresh() => RefreshStatus = null;

    private async System.Threading.Tasks.Task<bool> RefreshEntryAsync(MediaEntry entry, System.Threading.CancellationToken ct)
    {
        var type = TypeFor(entry.Kind);
        var category = Services.MediaCategories.Of(type);
        var wantsCredits = category is Services.MediaCategory.Game or Services.MediaCategory.Film;
        var wantsPlatform = category == Services.MediaCategory.Game;
        const int currentInfoVersion = 2;
        if (entry.Year is not null && entry.Creator is not null && entry.ImageUrl is not null
            && (!wantsCredits || (entry.Credits is { Count: > 0 } && entry.InfoVersion >= currentInfoVersion))
            && (!wantsPlatform || entry.Platform is not null))
            return false; // nothing missing

        var candidates = new List<MediaSuggestion>();
        await foreach (var batch in MetadataService.SuggestAsync(entry.Name, type, entry.Platform, ct))
            candidates.AddRange(batch);

        // A match must contain all the entry's words ("Frieren" → "Frieren: Beyond Journey's End");
        // among those, prefer the closest title, the same year and richer details.
        var best = candidates
            .Select(c => (Suggestion: c,
                Forward: Services.TitleMatch.Score(c.Title, entry.Name),
                Score: Services.TitleMatch.Score(c.Title, entry.Name) + Services.TitleMatch.Score(entry.Name, c.Title)
                       + (entry.Year is { } y && c.Year == y ? 0.3 : 0)
                       + (c.Year is not null || c.Creator is not null ? 0.05 : 0)))
            .Where(x => x.Forward >= 0.95 && x.Score >= 1.25)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Suggestion)
            .FirstOrDefault();
        if (best is null)
            return false;
        best = await MetadataService.EnrichAsync(best, type, ct);

        var changed = false;
        if (entry.Year is null && best.Year is { } year) { entry.Year = year; changed = true; }
        if (entry.Creator is null && best.Creator is { } creator) { entry.Creator = creator; changed = true; }
        if (entry.Length is null && best.Length is { } length) { entry.Length = length; changed = true; }
        // Credits: keep every role already there, add the missing ones (composer, programmer…).
        var mergedCredits = MetadataService.MergeCredits(entry.Credits, best.Credits);
        if ((mergedCredits?.Count ?? 0) > (entry.Credits?.Count ?? 0))
        {
            entry.Credits = mergedCredits!.ToList();
            changed = true;
        }
        if (wantsCredits && entry.InfoVersion != currentInfoVersion)
        {
            entry.InfoVersion = currentInfoVersion;
            changed = true;
        }
        if (wantsPlatform && entry.Platform is null && best.Platform is { } platform)
        {
            entry.Platform = EnsurePlatform(platform);
            changed = true;
        }
        var cover = best.ImageUrl;
        // Retro games: once the console is known, its box art is the best cover.
        if (entry.ImageUrl is null && wantsPlatform && Services.Platforms.BoxArtRepo(entry.Platform) is { } repo)
        {
            var boxArt = await Services.RetroBoxArt.SearchAsync(best.Title, repo, ct);
            cover = boxArt.FirstOrDefault()?.Url ?? cover;
        }
        if (entry.ImageUrl is null && cover is { } url)
        {
            entry.ImageUrl = url;
            _ = ImageCache.SaveToDiskAsync(url);
            changed = true;
        }
        return changed;
    }

    // ==========================================================================
    // Edit mode
    // ==========================================================================

    /// <summary>When on, every entry shows its edit / delete buttons.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditModeText), nameof(EditModeIcon))]
    public partial bool IsEditMode { get; set; }

    public string EditModeText => IsEditMode ? "Done" : "Edit";
    public Geometry EditModeIcon => IsEditMode ? Controls.Icons.Check : Controls.Icons.Pencil;

    [RelayCommand]
    private void ToggleEditMode() => IsEditMode = !IsEditMode;

    partial void OnIsEditModeChanged(bool value)
    {
        foreach (var entry in Entries)
            entry.ShowActions = value;
    }

    // ==========================================================================
    // Import
    // ==========================================================================

    /// <summary>
    /// Merges a MediaLog library file into this one: its types and consoles are added when new,
    /// year lists are filled by date, other lists by name; entries already present (same title,
    /// type and date) are skipped. Returns a short summary.
    /// </summary>
    public string ImportLibrary(string json)
    {
        if (LibraryStore.Parse(json) is not { } incoming)
            return "That file isn't a MediaLog library.";

        foreach (var type in incoming.Types.Where(t => _library.Types.All(x => x.Id != t.Id)))
            _library.Types.Add(type);
        RebuildTypes();
        foreach (var platform in incoming.Platforms.Where(p => Services.Platforms.Find(_library.Platforms, p.Name) is null))
            _library.Platforms.Add(platform);
        RebuildPlatforms();

        int added = 0, skipped = 0;
        foreach (var list in incoming.Lists)
        {
            foreach (var entry in list.Entries)
            {
                // Year lists hold entries by date; custom lists are matched by name.
                ListViewModel target;
                if (list.Year is not null)
                {
                    target = EnsureYearList(entry.CompletedOn.Year);
                }
                else
                {
                    target = UserLists.FirstOrDefault(l => !l.IsYearList
                                 && string.Equals(l.Model!.Name, list.Name, StringComparison.OrdinalIgnoreCase))
                             ?? AddListViewModel(AddCustomList(list.Name));
                }
                if (entry.Platform is { } platform)
                    entry.Platform = EnsurePlatform(platform);
                // Same title, type, date and console = already there (the same game on two consoles is two entries).
                var exists = AllEntries().Any(x => x.Entry.Kind == entry.Kind && x.Entry.CompletedOn == entry.CompletedOn
                    && string.Equals(x.Entry.Name, entry.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.Entry.Platform, entry.Platform, StringComparison.OrdinalIgnoreCase));
                if (exists)
                {
                    skipped++;
                    continue;
                }
                entry.Id = Guid.NewGuid();
                target.Model!.Entries.Add(entry);
                added++;
            }
        }
        Persist();
        Refresh();
        return skipped == 0
            ? $"Imported {added} entries."
            : $"Imported {added} entries ({skipped} already there).";
    }

    private MediaList AddCustomList(string name)
    {
        var list = new MediaList { Name = name };
        _library.Lists.Add(list);
        return list;
    }

    /// <summary>Shows a message in the bottom status bar (shared with Refresh all info).</summary>
    public void ShowStatus(string message)
    {
        RefreshProgress = 0;
        RefreshStatus = message;
    }

    // ==========================================================================
    // Details card (press and hold an entry)
    // ==========================================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailsOpen))]
    public partial EntryViewModel? DetailEntry { get; set; }

    public bool IsDetailsOpen => DetailEntry is not null;

    [RelayCommand]
    private void ShowDetails(EntryViewModel entry) => DetailEntry = entry;

    [RelayCommand]
    private void CloseDetails() => DetailEntry = null;

    // ==========================================================================
    // Entry editor
    // ==========================================================================

    /// <summary>The open "Edit entry" dialog, if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditorOpen))]
    public partial EntryEditorViewModel? Editor { get; set; }

    public bool IsEditorOpen => Editor is not null;

    private void OpenEditor(EntryViewModel entry) =>
        (DetailEntry, Editor) = (null, new EntryEditorViewModel(entry, Types, UserLists.ToList(), PlatformNames.ToList(), SaveEditor, DeleteEntry, () => Editor = null));

    private void SaveEditor(EntryEditorViewModel editor)
    {
        var entry = editor.Entry.Model;
        var date = DateOnly.FromDateTime(editor.Date ?? entry.CompletedOn.ToDateTime(TimeOnly.MinValue));
        entry.Name = editor.Name.Trim();
        entry.Kind = editor.Type.Id;
        entry.CompletedOn = date;
        entry.ImageUrl = editor.Cover.ImageUrl;
        entry.Year = int.TryParse(editor.YearText?.Trim(), out var year) ? year : null;
        entry.Creator = string.IsNullOrWhiteSpace(editor.Creator) ? null : editor.Creator.Trim();
        entry.Length = string.IsNullOrWhiteSpace(editor.Length) ? null : editor.Length.Trim();
        entry.Platform = editor.IsGame ? EnsurePlatform(editor.PlatformText) : null;
        entry.Credits = EntryEditorViewModel.ParseCredits(editor.CreditsText);
        if (entry.ImageUrl is { } url)
            _ = ImageCache.SaveToDiskAsync(url);

        // Move between lists; entries kept in a year list follow their date.
        var source = editor.Entry.List;
        var target = editor.List;
        if (target.IsYearList)
            target = EnsureYearList(date.Year);
        if (target != source && _library.Lists.Contains(source.Model!))
        {
            source.Model!.Entries.Remove(entry);
            target.Model!.Entries.Add(entry);
        }

        Editor = null;
        Persist();
        Refresh();
    }

    // ==========================================================================
    // Lists
    // ==========================================================================

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddListCommand))]
    public partial string NewListName { get; set; } = "";

    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; set; }

    private bool CanAddList() => !string.IsNullOrWhiteSpace(NewListName);

    [RelayCommand(CanExecute = nameof(CanAddList))]
    private void AddList()
    {
        var list = new MediaList { Name = NewListName.Trim() };
        _library.Lists.Add(list);
        NewListName = "";
        var vm = AddListViewModel(list);
        Persist();
        SelectedNav = vm;
    }

    [RelayCommand]
    private void RequestDeleteList() => IsConfirmingDelete = true;

    [RelayCommand]
    private void CancelDeleteList() => IsConfirmingDelete = false;

    [RelayCommand]
    private void ConfirmDeleteList()
    {
        IsConfirmingDelete = false;
        if (!CanDeleteSelected || SelectedNav is not { Model: { } model } vm)
            return;

        _library.Lists.Remove(model);
        RemoveListViewModel(vm);
        if (_lastDeleted?.List == vm)
            DismissUndo();
        Persist();
        SelectedNav = CurrentYearList;
    }

    /// <summary>
    /// Adds a list to the sidebar, keeping year lists (newest first) above custom lists.
    /// </summary>
    private ListViewModel AddListViewModel(MediaList list)
    {
        var vm = ListViewModel.ForList(list, OnListRenamed);
        var index = list.Year is { } year
            ? UserLists.TakeWhile(l => l.Model!.Year is { } y && y > year).Count()
            : UserLists.Count;
        UserLists.Insert(index, vm);
        NavItems.Insert(index + 1 + TypeNavCount, vm); // after "All media" and its type lists
        return vm;
    }

    private void RemoveListViewModel(ListViewModel vm)
    {
        if (TargetList == vm)
            TargetList = CurrentYearList ?? UserLists.FirstOrDefault(l => l != vm);
        UserLists.Remove(vm);
        NavItems.Remove(vm);
    }

    private ListViewModel EnsureYearList(int year)
    {
        if (YearList(year) is { } existing)
            return existing;
        var list = new MediaList { Name = year.ToString(CultureInfo.InvariantCulture), Year = year };
        _library.Lists.Add(list);
        return AddListViewModel(list);
    }

    /// <summary>
    /// Keeps one year list for the current year and for every year that has entries
    /// (in any list); drops empty year lists of past years.
    /// </summary>
    private void SyncYearLists()
    {
        var years = AllEntries().Select(x => x.Entry.CompletedOn.Year).ToHashSet();
        years.Add(DateTime.Today.Year);

        var changed = false;
        foreach (var year in years.Where(y => YearList(y) is null))
        {
            EnsureYearList(year);
            changed = true;
        }
        foreach (var stale in UserLists.Where(l => l.IsYearList && !years.Contains(l.Model!.Year!.Value) && l.Model.Entries.Count == 0).ToList())
        {
            _library.Lists.Remove(stale.Model!);
            RemoveListViewModel(stale);
            changed = true;
        }
        if (changed)
            Persist();
    }

    private void CheckYear()
    {
        if (CurrentYearList is not null)
            return;
        var wasOnLastYear = SelectedNav?.Model?.Year == DateTime.Today.Year - 1;
        Refresh();
        if (wasOnLastYear)
            SelectedNav = CurrentYearList;
        if (NewDate is { } d && d.Year < DateTime.Today.Year)
            NewDate = DateTime.Today;
    }

    private void OnListRenamed()
    {
        Persist();
        // Owning list names are shown on entries of other views.
        Refresh();
    }

    // ==========================================================================
    // Type manager
    // ==========================================================================

    public static IReadOnlyList<PaletteColor> Palette { get; } =
    [
        // the type defaults (chosen to be far apart) …
        new("#F00020"), new("#FF7A00"), new("#FFC800"), new("#8BC700"), new("#00D84A"), new("#00956A"),
        new("#18D4F0"), new("#0A6CFF"), new("#5B21FF"), new("#9C00B8"), new("#E600FF"), new("#FF4FA8"),
        // … and a few more vivid / deep options
        new("#FF5A36"), new("#B8860B"), new("#00B3B3"), new("#1A237E"), new("#C2185B"), new("#6D4C41"),
        new("#8C8C9E"), new("#37474F"),
    ];

    public static IReadOnlyList<TypeIcon> IconOptions => TypeIcons.All;

    public ObservableCollection<TypeRowViewModel> TypeRows { get; } = [];

    [ObservableProperty]
    public partial bool IsTypeManagerOpen { get; set; }

    /// <summary>Id of the type being edited, or <c>null</c> when creating a new one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingType), nameof(TypeEditorTitle), nameof(SaveTypeText))]
    public partial string? EditingTypeId { get; set; }

    public bool IsEditingType => EditingTypeId is not null;
    public string TypeEditorTitle => IsEditingType ? "Edit type" : "New type";
    public string SaveTypeText => IsEditingType ? "Save" : "Create type";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveTypeCommand))]
    [NotifyPropertyChangedFor(nameof(EditPreview))]
    public partial string EditTypeName { get; set; } = "";

    /// <summary>Hex color being edited (also editable as text).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveTypeCommand))]
    [NotifyPropertyChangedFor(nameof(EditPreview))]
    public partial string EditTypeColor { get; set; } = "";

    [ObservableProperty]
    public partial PaletteColor? EditPaletteColor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditPreview))]
    public partial TypeIcon? EditTypeIcon { get; set; }

    /// <summary>Live preview of the type being edited.</summary>
    public MediaTypeInfo EditPreview => new(new MediaTypeDef
    {
        Id = "preview",
        Label = string.IsNullOrWhiteSpace(EditTypeName) ? "New type" : EditTypeName.Trim(),
        Color = IsValidColor(EditTypeColor) ? EditTypeColor : "#78909C",
        Icon = EditTypeIcon?.Key ?? "dots",
    });

    partial void OnEditPaletteColorChanged(PaletteColor? value)
    {
        if (value is not null && !string.Equals(value.Hex, EditTypeColor, StringComparison.OrdinalIgnoreCase))
            EditTypeColor = value.Hex;
    }

    partial void OnEditTypeColorChanged(string value) =>
        EditPaletteColor = Palette.FirstOrDefault(p => string.Equals(p.Hex, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool IsValidColor(string? value) => !string.IsNullOrWhiteSpace(value) && Color.TryParse(value.Trim(), out _);

    [RelayCommand]
    private void OpenTypeManager()
    {
        RefreshTypeRows();
        StartNewType();
        IsTypeManagerOpen = true;
    }

    [RelayCommand]
    private void CloseTypeManager() => IsTypeManagerOpen = false;

    [RelayCommand]
    private void StartNewType()
    {
        EditingTypeId = null;
        EditTypeName = "";
        // Suggest a color that isn't used yet.
        EditTypeColor = (Palette.FirstOrDefault(p => !_library.Types.Any(t => string.Equals(t.Color, p.Hex, StringComparison.OrdinalIgnoreCase)))
                         ?? Palette[0]).Hex;
        EditTypeIcon = TypeIcons.Get("dots");
    }

    private void EditType(TypeRowViewModel row)
    {
        EditingTypeId = row.Type.Id;
        EditTypeName = row.Type.Label;
        EditTypeColor = row.Type.Def.Color;
        EditTypeIcon = TypeIcons.Get(row.Type.Def.Icon);
    }

    private bool CanSaveType() => !string.IsNullOrWhiteSpace(EditTypeName) && IsValidColor(EditTypeColor);

    [RelayCommand(CanExecute = nameof(CanSaveType))]
    private void SaveType()
    {
        var color = Color.Parse(EditTypeColor.Trim());
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        var def = EditingTypeId is { } id ? _library.Types.FirstOrDefault(t => t.Id == id) : null;
        if (def is null)
        {
            def = new MediaTypeDef { Id = "custom-" + Guid.NewGuid().ToString("N")[..12] };
            _library.Types.Add(def);
        }
        def.Label = EditTypeName.Trim();
        def.Color = hex;
        def.Icon = EditTypeIcon?.Key ?? "dots";

        var isNew = EditingTypeId is null;
        Persist();
        RebuildTypes();
        if (isNew)
            NewType = _typeById[def.Id];
        RefreshTypeRows();
        StartNewType();
        Refresh();
    }

    private void DeleteType(TypeRowViewModel row)
    {
        if (row.Count > 0 || _library.Types.Count <= 1)
            return;
        _library.Types.RemoveAll(t => t.Id == row.Type.Id);
        if (EditingTypeId == row.Type.Id)
            StartNewType();
        if (TypeFilter == row.Type.Id)
            TypeFilter = null;
        Persist();
        RebuildTypes();
        NewType ??= Types[0];
        RefreshTypeRows();
        Refresh();
    }

    private void RefreshTypeRows()
    {
        var usage = AllEntries().GroupBy(x => x.Entry.Kind).ToDictionary(g => g.Key, g => g.Count());
        TypeRows.Clear();
        foreach (var type in Types)
        {
            var count = usage.GetValueOrDefault(type.Id);
            TypeRows.Add(new TypeRowViewModel(type, count, count == 0 && Types.Count > 1, EditType, DeleteType));
        }
    }

    // ==========================================================================
    // Refresh
    // ==========================================================================

    private IEnumerable<(MediaEntry Entry, ListViewModel List)> AllEntries() =>
        UserLists.SelectMany(l => l.Model!.Entries.Select(e => (Entry: e, List: l)));

    /// <summary>Rebuilds year lists, counts, per-type stats and the visible entry list.</summary>
    private void Refresh()
    {
        if (_inRefresh)
            return;
        _inRefresh = true;
        try
        {
            SyncYearLists();
            if (SelectedNav is null || !NavItems.Contains(SelectedNav))
                SelectedNav = CurrentYearList;

            var all = AllEntries().ToList();
            var perYear = all.GroupBy(x => x.Entry.CompletedOn.Year).ToDictionary(g => g.Key, g => g.Count());
            foreach (var list in UserLists)
                list.Count = list.Model!.Year is { } y ? perYear.GetValueOrDefault(y) : list.Model.Entries.Count;
            Master.Count = all.Count;
            SyncTypeNav(all.GroupBy(x => x.Entry.Kind).ToDictionary(g => g.Key, g => g.Count()));
            if (SelectedNav is null || !NavItems.Contains(SelectedNav))
                SelectedNav = CurrentYearList;

            // Year lists collect every entry completed that year, whichever list it is stored in.
            var scope = SelectedNav switch
            {
                null => [],
                { IsMaster: true } => all,
                { Type: { } type } => all.Where(x => x.Entry.Kind == type.Id).ToList(),
                { Model.Year: { } year } => all.Where(x => x.Entry.CompletedOn.Year == year).ToList(),
                var l => all.Where(x => x.List == l).ToList(),
            };
            ScopeCount = scope.Count;

            Stats.Clear();
            foreach (var group in scope.GroupBy(x => x.Entry.Kind).OrderByDescending(g => g.Count()).ThenBy(g => TypeFor(g.Key).Label))
                Stats.Add(new TypeStatViewModel(TypeFor(group.Key), group.Count(), group.Key == TypeFilter, ToggleType));

            var search = SearchText.Trim();
            var visible = scope
                .Where(x => TypeFilter is null || x.Entry.Kind == TypeFilter)
                .Where(x => search.Length == 0 || x.Entry.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending(x => x.Entry.CompletedOn)
                .ThenBy(x => x.Entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            Entries.Clear();
            foreach (var (entry, list) in visible)
                Entries.Add(new EntryViewModel(entry, TypeFor(entry.Kind), list, list != SelectedNav, DeleteEntry, OpenEditor,
                    PlatformFor(entry.Platform))
                    { ShowActions = IsEditMode });

            var noun = ScopeCount == 1 ? "item" : "items";
            CountText = IsFiltered ? $"{visible.Count} of {ScopeCount} {noun}" : $"{ScopeCount} {noun}";
            IsEmpty = visible.Count == 0;
            EmptyText = ScopeCount == 0
                ? NeedsTargetList
                    ? "Nothing logged yet. Add the first thing you finished!"
                    : "This list is empty. Add the first thing you finished!"
                : "No entries match the current filter.";
        }
        finally
        {
            _inRefresh = false;
        }
    }

    private void Persist() => _store?.Save(_library);
}

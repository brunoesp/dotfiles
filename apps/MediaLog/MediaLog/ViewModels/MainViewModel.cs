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
        RebuildTypes();

        Master = ListViewModel.CreateMaster();
        NavItems.Add(Master);
        foreach (var list in _library.Lists)
            AddListViewModel(list);

        NewType = Types[0];
        NewDate = DateTime.Today;
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
    [NotifyPropertyChangedFor(nameof(IsMasterSelected), nameof(IsRenamableSelected), nameof(CanDeleteSelected))]
    [NotifyCanExecuteChangedFor(nameof(AddEntryCommand))]
    public partial ListViewModel? SelectedNav { get; set; }

    public bool IsMasterSelected => SelectedNav?.IsMaster == true;

    /// <summary>Selected list can be renamed in place (custom lists only).</summary>
    public bool IsRenamableSelected => SelectedNav?.CanRename == true;

    /// <summary>Only custom lists can be deleted; year lists follow the entries' dates.</summary>
    public bool CanDeleteSelected => SelectedNav is { IsMaster: false, IsYearList: false };

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
        if (value is { IsMaster: false })
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

    [ObservableProperty]
    public partial DateTime? NewDate { get; set; }

    /// <summary>Target list when adding from the master view.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddEntryCommand))]
    public partial ListViewModel? TargetList { get; set; }

    [ObservableProperty]
    public partial string? UndoText { get; set; }

    private bool CanAddEntry() =>
        !string.IsNullOrWhiteSpace(NewName) && (IsMasterSelected ? TargetList : SelectedNav) is { IsMaster: false };

    [RelayCommand(CanExecute = nameof(CanAddEntry))]
    private void AddEntry()
    {
        var target = IsMasterSelected ? TargetList : SelectedNav;
        if (target?.Model is null || string.IsNullOrWhiteSpace(NewName))
            return;

        var date = DateOnly.FromDateTime(NewDate ?? DateTime.Today);
        var type = NewType ?? Types[0];
        // Year lists hold the entries of their year: route to the list matching the date.
        if (target.IsYearList)
            target = EnsureYearList(date.Year);

        target.Model!.Entries.Add(new MediaEntry { Name = NewName.Trim(), Kind = type.Id, CompletedOn = date });
        NewName = "";
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
        NavItems.Insert(index + 1, vm); // + 1 for the master list
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
        new("#FF1744"), new("#FF4FA3"), new("#F000B8"), new("#D500F9"), new("#AA00FF"), new("#651FFF"),
        new("#2962FF"), new("#00B0FF"), new("#00E5FF"), new("#00BFA5"), new("#00C853"), new("#64DD17"),
        new("#9ACD00"), new("#FFC400"), new("#FF9100"), new("#FF6D00"), new("#DD2C00"), new("#8D6E63"),
        new("#78909C"), new("#455A64"),
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

            // Year lists collect every entry completed that year, whichever list it is stored in.
            var scope = SelectedNav switch
            {
                null => [],
                { IsMaster: true } => all,
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
                Entries.Add(new EntryViewModel(entry, TypeFor(entry.Kind), list, list != SelectedNav, DeleteEntry));

            var noun = ScopeCount == 1 ? "item" : "items";
            CountText = IsFiltered ? $"{visible.Count} of {ScopeCount} {noun}" : $"{ScopeCount} {noun}";
            IsEmpty = visible.Count == 0;
            EmptyText = ScopeCount == 0
                ? SelectedNav?.IsMaster == true
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

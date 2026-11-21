using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Models;
using MediaLog.Services;

namespace MediaLog.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly LibraryStore? _store;
    private readonly Library _library;
    private (MediaEntry Entry, ListViewModel List, int Index)? _lastDeleted;

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

        Master = ListViewModel.CreateMaster();
        NavItems.Add(Master);
        foreach (var list in _library.Lists)
            AddListViewModel(list);

        NewType = Types[0];
        NewDate = DateTime.Today;
        SelectedNav = NavItems.Count > 1 ? NavItems[1] : Master;
    }

    public IReadOnlyList<MediaTypeInfo> Types => MediaTypeInfo.All;

    public ListViewModel Master { get; }

    /// <summary>Sidebar items: the master list first, followed by every user list.</summary>
    public ObservableCollection<ListViewModel> NavItems { get; } = [];

    /// <summary>User lists only (targets for new entries).</summary>
    public ObservableCollection<ListViewModel> UserLists { get; } = [];

    public ObservableCollection<EntryViewModel> Entries { get; } = [];

    public ObservableCollection<TypeStatViewModel> Stats { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMasterSelected), nameof(IsUserListSelected))]
    [NotifyCanExecuteChangedFor(nameof(AddEntryCommand))]
    public partial ListViewModel? SelectedNav { get; set; }

    public bool IsMasterSelected => SelectedNav?.IsMaster == true;
    public bool IsUserListSelected => SelectedNav is { IsMaster: false };

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
    public partial MediaKind? TypeFilter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFiltered))]
    public partial string SearchText { get; set; } = "";

    public bool IsFiltered => TypeFilter is not null || !string.IsNullOrWhiteSpace(SearchText);

    // --- New entry form -------------------------------------------------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddEntryCommand))]
    public partial string NewName { get; set; } = "";

    [ObservableProperty]
    public partial MediaTypeInfo NewType { get; set; } = MediaTypeInfo.All[0];

    [ObservableProperty]
    public partial DateTime? NewDate { get; set; }

    /// <summary>Target list when adding from the master view.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddEntryCommand))]
    public partial ListViewModel? TargetList { get; set; }

    // --- List management --------------------------------------------------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddListCommand))]
    public partial string NewListName { get; set; } = "";

    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; set; }

    [ObservableProperty]
    public partial string? UndoText { get; set; }

    /// <summary>Whether the navigation pane is open (only meaningful in compact layout).</summary>
    [ObservableProperty]
    public partial bool IsPaneOpen { get; set; }

    partial void OnSelectedNavChanged(ListViewModel? value)
    {
        IsConfirmingDelete = false;
        if (value is { IsMaster: false })
            TargetList = value;
        else if (TargetList is null || !UserLists.Contains(TargetList))
            TargetList = UserLists.FirstOrDefault();
        Refresh();
    }

    partial void OnTypeFilterChanged(MediaKind? value) => Refresh();

    partial void OnSearchTextChanged(string value) => Refresh();

    private bool CanAddEntry() =>
        !string.IsNullOrWhiteSpace(NewName) && (IsMasterSelected ? TargetList : SelectedNav) is { IsMaster: false };

    [RelayCommand(CanExecute = nameof(CanAddEntry))]
    private void AddEntry()
    {
        var target = IsMasterSelected ? TargetList : SelectedNav;
        if (target?.Model is null || string.IsNullOrWhiteSpace(NewName))
            return;

        target.Model.Entries.Add(new MediaEntry
        {
            Name = NewName.Trim(),
            Kind = (NewType ?? Types[0]).Kind,
            CompletedOn = DateOnly.FromDateTime(NewDate ?? DateTime.Today),
        });
        NewName = "";
        // Make sure the newly added item is visible.
        if (TypeFilter is not null && TypeFilter != NewType?.Kind)
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
        if (_lastDeleted is { } d && UserLists.Contains(d.List))
        {
            var entries = d.List.Model!.Entries;
            entries.Insert(Math.Min(d.Index, entries.Count), d.Entry);
            Persist();
            Refresh();
        }
        DismissUndo();
    }

    [RelayCommand]
    private void DismissUndo()
    {
        _lastDeleted = null;
        UndoText = null;
    }

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
        if (SelectedNav?.Model is not { } model)
            return;

        var vm = SelectedNav;
        var navIndex = NavItems.IndexOf(vm);
        _library.Lists.Remove(model);
        UserLists.Remove(vm);
        NavItems.Remove(vm);
        if (TargetList == vm)
            TargetList = UserLists.FirstOrDefault();
        if (_lastDeleted?.List == vm)
            DismissUndo();
        Persist();
        SelectedNav = NavItems[Math.Clamp(navIndex - 1, 0, NavItems.Count - 1)];
    }

    [RelayCommand]
    private void ClearFilters()
    {
        TypeFilter = null;
        SearchText = "";
    }

    [RelayCommand]
    private void TogglePane() => IsPaneOpen = !IsPaneOpen;

    private void ToggleType(TypeStatViewModel stat) =>
        TypeFilter = TypeFilter == stat.Type.Kind ? null : stat.Type.Kind;

    private ListViewModel AddListViewModel(MediaList list)
    {
        var vm = ListViewModel.ForList(list, OnListRenamed);
        UserLists.Add(vm);
        NavItems.Add(vm);
        vm.Count = list.Entries.Count;
        return vm;
    }

    private void OnListRenamed()
    {
        Persist();
        // List names are shown on master-list entries.
        if (IsMasterSelected)
            Refresh();
    }

    /// <summary>Rebuilds counts, per-type stats and the visible entry list.</summary>
    private void Refresh()
    {
        foreach (var list in UserLists)
            list.Count = list.Model!.Entries.Count;
        Master.Count = UserLists.Sum(l => l.Count);

        var scope = SelectedNav switch
        {
            null => [],
            { IsMaster: true } => UserLists.SelectMany(l => l.Model!.Entries.Select(e => (Entry: e, List: l))).ToList(),
            var l => l.Model!.Entries.Select(e => (Entry: e, List: l)).ToList(),
        };
        ScopeCount = scope.Count;

        Stats.Clear();
        foreach (var group in scope.GroupBy(x => x.Entry.Kind).OrderByDescending(g => g.Count()).ThenBy(g => g.Key))
            Stats.Add(new TypeStatViewModel(MediaTypeInfo.For(group.Key), group.Count(), group.Key == TypeFilter, ToggleType));

        var search = SearchText.Trim();
        var visible = scope
            .Where(x => TypeFilter is null || x.Entry.Kind == TypeFilter)
            .Where(x => search.Length == 0 || x.Entry.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(x => x.Entry.CompletedOn)
            .ThenBy(x => x.Entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Entries.Clear();
        var showList = IsMasterSelected;
        foreach (var (entry, list) in visible)
            Entries.Add(new EntryViewModel(entry, list, showList, DeleteEntry));

        var noun = ScopeCount == 1 ? "item" : "items";
        CountText = IsFiltered ? $"{visible.Count} of {ScopeCount} {noun}" : $"{ScopeCount} {noun}";
        IsEmpty = visible.Count == 0;
        EmptyText = ScopeCount == 0
            ? IsMasterSelected
                ? UserLists.Count == 0 ? "Create a list to start logging." : "Nothing logged yet. Add the first thing you finished!"
                : "This list is empty. Add the first thing you finished!"
            : "No entries match the current filter.";
    }

    private void Persist() => _store?.Save(_library);
}

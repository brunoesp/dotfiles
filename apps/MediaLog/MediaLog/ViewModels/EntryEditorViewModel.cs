using System;
using System.Collections.Generic;
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
        Cover = new CoverPickerViewModel(entry.Model.ImageUrl, entry.Image, entry.Model.Name, () => Type);
        // No cover yet: start suggesting right away.
        if (!Cover.HasImage)
            Cover.SearchIfIdle();
    }

    public EntryViewModel Entry { get; }
    public IReadOnlyList<MediaTypeInfo> Types { get; }
    public IReadOnlyList<ListViewModel> Lists { get; }
    public CoverPickerViewModel Cover { get; }

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

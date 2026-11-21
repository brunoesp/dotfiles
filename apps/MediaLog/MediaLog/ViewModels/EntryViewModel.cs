using System;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Models;

namespace MediaLog.ViewModels;

public partial class EntryViewModel : ViewModelBase
{
    private readonly Action<EntryViewModel> _delete;

    public EntryViewModel(MediaEntry model, MediaTypeInfo type, ListViewModel list, bool showList, Action<EntryViewModel> delete)
    {
        Model = model;
        Type = type;
        List = list;
        ShowList = showList;
        _delete = delete;
    }

    public MediaEntry Model { get; }
    public ListViewModel List { get; }
    public MediaTypeInfo Type { get; }

    /// <summary>Whether the owning list name is shown (when it differs from the list being viewed).</summary>
    /// <summary>The list the entry is stored in.</summary>
    public bool ShowList { get; }

    public string Name => Model.Name;

    public string DateText => Model.CompletedOn.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    [RelayCommand]
    private void Delete() => _delete(this);
}

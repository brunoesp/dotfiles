using System;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Models;

namespace MediaLog.ViewModels;

public partial class EntryViewModel : ViewModelBase
{
    private readonly Action<EntryViewModel> _delete;

    public EntryViewModel(MediaEntry model, ListViewModel list, bool showList, Action<EntryViewModel> delete)
    {
        Model = model;
        List = list;
        ShowList = showList;
        _delete = delete;
        Type = MediaTypeInfo.For(model.Kind);
    }

    public MediaEntry Model { get; }
    public ListViewModel List { get; }
    public MediaTypeInfo Type { get; }

    /// <summary>Whether the owning list name is shown (only in the master list).</summary>
    public bool ShowList { get; }

    public string Name => Model.Name;

    public string DateText => Model.CompletedOn.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    [RelayCommand]
    private void Delete() => _delete(this);
}

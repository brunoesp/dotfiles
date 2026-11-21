using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaLog.Controls;
using MediaLog.Models;

namespace MediaLog.ViewModels;

/// <summary>
/// A navigation item: either a user list or the master list (which aggregates every list).
/// </summary>
public partial class ListViewModel : ViewModelBase
{
    private readonly Action? _changed;

    private ListViewModel(MediaList? model, string name, Action? changed)
    {
        Model = model;
        _name = name;
        _changed = changed;
    }

    public static ListViewModel CreateMaster() => new(null, "All media", null);

    public static ListViewModel ForList(MediaList model, Action changed) => new(model, model.Name, changed);

    /// <summary>The backing list, or <c>null</c> for the master list.</summary>
    public MediaList? Model { get; }

    public bool IsMaster => Model is null;

    /// <summary>Automatic list for a calendar year (name is fixed to the year).</summary>
    public bool IsYearList => Model?.Year is not null;

    /// <summary>User lists can be renamed; the master and year lists can't.</summary>
    public bool CanRename => !IsMaster && !IsYearList;

    public Geometry Icon => IsMaster ? Icons.Layers : IsYearList ? Icons.Calendar : Icons.List;

    [ObservableProperty]
    public partial int Count { get; set; }

    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            if (!CanRename || value == _name)
                return;
            // Keep the previous name if the user clears the field entirely.
            var trimmed = value.Trim();
            SetProperty(ref _name, value);
            if (trimmed.Length > 0)
            {
                Model!.Name = trimmed;
                _changed?.Invoke();
            }
        }
    }

    public override string ToString() => Name;
}

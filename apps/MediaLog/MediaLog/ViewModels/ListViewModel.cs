using System;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Controls;
using MediaLog.Models;

namespace MediaLog.ViewModels;

/// <summary>
/// A sidebar item: the master list ("All media"), a per-type list under it,
/// an automatic year list or a user-created list.
/// </summary>
public partial class ListViewModel : ViewModelBase
{
    private readonly Action? _changed;
    private readonly Action<ListViewModel>? _toggle;
    private string _name;

    private ListViewModel(MediaList? model, MediaTypeInfo? type, string name, Action? changed, Action<ListViewModel>? toggle)
    {
        Model = model;
        _type = type;
        _name = name;
        _changed = changed;
        _toggle = toggle;
    }

    /// <param name="toggleExpanded">Called when the expand arrow is clicked.</param>
    public static ListViewModel CreateMaster(Action<ListViewModel> toggleExpanded) =>
        new(null, null, "All media", null, toggleExpanded) { IsMaster = true };

    /// <param name="togglePin">Called when the pin button is clicked.</param>
    public static ListViewModel ForType(MediaTypeInfo type, Action<ListViewModel> togglePin) =>
        new(null, type, type.Label, null, togglePin);

    public static ListViewModel ForList(MediaList model, Action changed) => new(model, null, model.Name, changed, null);

    /// <summary>The backing stored list; <c>null</c> for the master and type lists.</summary>
    public MediaList? Model { get; }

    public bool IsMaster { get; private init; }

    public bool IsTypeList => _type is not null;

    /// <summary>Automatic list for a calendar year (name is fixed to the year).</summary>
    public bool IsYearList => Model?.Year is not null;

    /// <summary>Only user-created lists can be renamed.</summary>
    public bool CanRename => Model is not null && !IsYearList;

    /// <summary>Key used to remember per-list settings such as the view mode.</summary>
    public string Key => IsMaster ? "all" : _type is not null ? "type:" + _type.Id : "list:" + Model!.Id;

    private MediaTypeInfo? _type;

    /// <summary>The media type shown by a per-type list.</summary>
    public MediaTypeInfo? Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value) && value is not null)
            {
                _name = value.Label;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    public Geometry Icon => IsMaster ? Icons.Layers : IsYearList ? Icons.Calendar : Icons.List;

    /// <summary>Left indent in the sidebar (type lists are nested under "All media").</summary>
    public Thickness Indent => IsMaster ? default : IsTypeList ? new Thickness(36, 0, 0, 0) : new Thickness(28, 0, 0, 0);

    [ObservableProperty]
    public partial int Count { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronIcon))]
    public partial bool IsExpanded { get; set; }

    public Geometry ChevronIcon => IsExpanded ? Icons.ChevronDown : Icons.ChevronRight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinTip))]
    public partial bool IsPinned { get; set; }

    public string PinTip => IsPinned ? "Unpin" : "Pin (stays visible when collapsed)";

    [RelayCommand]
    private void Toggle() => _toggle?.Invoke(this);

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

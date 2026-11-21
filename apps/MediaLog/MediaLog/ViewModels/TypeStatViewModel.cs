using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Models;

namespace MediaLog.ViewModels;

/// <summary>Per-type counter chip; clicking it toggles a filter on that type.</summary>
public partial class TypeStatViewModel : ViewModelBase
{
    private readonly Action<TypeStatViewModel> _toggle;

    public TypeStatViewModel(MediaTypeInfo type, int count, bool isActive, Action<TypeStatViewModel> toggle)
    {
        Type = type;
        Count = count;
        IsActive = isActive;
        _toggle = toggle;
    }

    public MediaTypeInfo Type { get; }
    public int Count { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderBrush))]
    public partial bool IsActive { get; set; }

    /// <summary>Outline shown when this type is the active filter.</summary>
    public IBrush BorderBrush => IsActive ? Type.Brush : Brushes.Transparent;

    [RelayCommand]
    private void Toggle() => _toggle(this);
}

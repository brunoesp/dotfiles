using System;
using CommunityToolkit.Mvvm.Input;
using MediaLog.Models;

namespace MediaLog.ViewModels;

/// <summary>A row in the "Media types" manager.</summary>
public partial class TypeRowViewModel : ViewModelBase
{
    private readonly Action<TypeRowViewModel> _edit;
    private readonly Action<TypeRowViewModel> _delete;

    public TypeRowViewModel(MediaTypeInfo type, int count, bool canDelete,
        Action<TypeRowViewModel> edit, Action<TypeRowViewModel> delete)
    {
        Type = type;
        Count = count;
        CanDelete = canDelete;
        _edit = edit;
        _delete = delete;
    }

    public MediaTypeInfo Type { get; }
    public int Count { get; }
    public bool CanDelete { get; }

    public string UsageText => Count switch
    {
        0 => "unused",
        1 => "1 entry",
        _ => $"{Count} entries",
    };

    public string DeleteTip => CanDelete ? "Delete type" : "In use: can't be deleted";

    [RelayCommand]
    private void Edit() => _edit(this);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete() => _delete(this);
}

/// <summary>A color swatch in the type editor.</summary>
public sealed record PaletteColor(string Hex)
{
    public Avalonia.Media.IBrush Brush { get; } =
        new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Avalonia.Media.Color.Parse(Hex));
}

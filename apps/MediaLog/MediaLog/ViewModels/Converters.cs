using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MediaLog.ViewModels;

public static class Converters
{
    public static FuncValueConverter<bool, FontWeight> BoolToSemiBold { get; } =
        new(b => b ? FontWeight.SemiBold : FontWeight.Normal);
}

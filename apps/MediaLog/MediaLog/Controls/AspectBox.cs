using Avalonia;
using Avalonia.Controls;

namespace MediaLog.Controls;

/// <summary>Sizes its child to the available width and <c>width × <see cref="Ratio"/></c> height.</summary>
public sealed class AspectBox : Decorator
{
    public static readonly StyledProperty<double> RatioProperty =
        AvaloniaProperty.Register<AspectBox, double>(nameof(Ratio), 1.5);

    static AspectBox()
    {
        AffectsMeasure<AspectBox>(RatioProperty);
    }

    /// <summary>Height divided by width (1.5 = a 2:3 cover).</summary>
    public double Ratio
    {
        get => GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 160 : availableSize.Width;
        var size = new Size(width, width * Ratio);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = new Size(finalSize.Width, finalSize.Width * Ratio);
        Child?.Arrange(new Rect(size));
        return size;
    }
}

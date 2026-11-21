using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MediaLog.Controls;

/// <summary>
/// Draws a stroke-style icon whose geometry is authored on a 24×24 grid,
/// scaled uniformly to the control's size.
/// </summary>
public sealed class StrokeIcon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<StrokeIcon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<StrokeIcon, IBrush?>(nameof(Foreground), inherits: true);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<StrokeIcon, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<StrokeIcon, double>(nameof(StrokeThickness), 2.0);

    static StrokeIcon()
    {
        AffectsRender<StrokeIcon>(DataProperty, ForegroundProperty, FillProperty, StrokeThicknessProperty);
    }

    public StrokeIcon()
    {
        Width = 20;
        Height = 20;
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Optional fill (e.g. a "pinned" state).</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Data is null || Foreground is null)
            return;

        var scale = System.Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        if (scale <= 0)
            return;

        var offsetX = (Bounds.Width - 24 * scale) / 2;
        var offsetY = (Bounds.Height - 24 * scale) / 2;
        var pen = new Pen(Foreground, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(Fill, pen, Data);
        }
    }
}

using System;
using Avalonia;
using Avalonia.Controls;

namespace MediaLog.Controls;

/// <summary>
/// Fixed number of equal-width columns; each row is as tall as its tallest tile (not the
/// tallest tile overall, like <c>UniformGrid</c>), and every tile in a row is stretched to it.
/// </summary>
public sealed class TileGrid : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<TileGrid, int>(nameof(Columns), 4);

    static TileGrid()
    {
        AffectsMeasure<TileGrid>(ColumnsProperty);
    }

    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    private double[] _rowHeights = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Math.Max(1, Columns);
        var width = double.IsInfinity(availableSize.Width) ? 180 * columns : availableSize.Width;
        var columnWidth = width / columns;
        var rows = (Children.Count + columns - 1) / columns;
        _rowHeights = new double[rows];
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            var row = i / columns;
            _rowHeights[row] = Math.Max(_rowHeights[row], child.DesiredSize.Height);
        }
        var height = 0.0;
        foreach (var h in _rowHeights)
            height += h;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var columnWidth = finalSize.Width / columns;
        var top = 0.0;
        for (var row = 0; row < _rowHeights.Length; row++)
        {
            for (var col = 0; col < columns; col++)
            {
                var i = row * columns + col;
                if (i >= Children.Count)
                    break;
                Children[i].Arrange(new Rect(col * columnWidth, top, columnWidth, _rowHeights[row]));
            }
            top += _rowHeights[row];
        }
        return finalSize;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace MediaLog.Controls;

/// <summary>
/// Wraps items like a <c>WrapPanel</c>, but when they need more than one row it spreads them
/// evenly over the rows and stretches each row to the full width (wider, aligned pills).
/// A single row keeps its natural size.
/// </summary>
public sealed class BalancedWrapPanel : Panel
{
    private readonly List<(int Start, int Count, double Height)> _rows = [];
    private double[] _widths = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        _rows.Clear();
        var n = Children.Count;
        if (n == 0)
            return default;

        foreach (var child in Children)
            child.Measure(Size.Infinity);
        var natural = Children.Select(c => c.DesiredSize.Width).ToArray();
        var width = availableSize.Width;
        _widths = (double[])natural.Clone();

        if (double.IsInfinity(width) || natural.Sum() <= width)
        {
            _rows.Add((0, n, Children.Max(c => c.DesiredSize.Height)));
            return new Size(natural.Sum(), _rows[0].Height);
        }

        // Fewest rows such that an even split (sizes differ by at most one) fits every row.
        for (var rows = 2; rows <= n; rows++)
        {
            var chunks = Chunks(n, rows);
            if (chunks.All(c => natural.Skip(c.Start).Take(c.Count).Sum() <= width) || rows == n)
            {
                foreach (var (start, count) in chunks)
                {
                    var sum = natural.Skip(start).Take(count).Sum();
                    var extra = Math.Max(0, width - sum) / count;
                    var height = 0.0;
                    for (var i = start; i < start + count; i++)
                    {
                        _widths[i] = Math.Min(width, natural[i] + extra);
                        Children[i].Measure(new Size(_widths[i], double.PositiveInfinity));
                        height = Math.Max(height, Children[i].DesiredSize.Height);
                    }
                    _rows.Add((start, count, height));
                }
                break;
            }
        }
        return new Size(width, _rows.Sum(r => r.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = 0.0;
        foreach (var (start, count, height) in _rows)
        {
            var left = 0.0;
            for (var i = start; i < start + count; i++)
            {
                Children[i].Arrange(new Rect(left, top, _widths[i], height));
                left += _widths[i];
            }
            top += height;
        }
        return finalSize;
    }

    private static List<(int Start, int Count)> Chunks(int n, int rows)
    {
        var result = new List<(int, int)>();
        var start = 0;
        for (var r = 0; r < rows; r++)
        {
            var count = n / rows + (r < n % rows ? 1 : 0);
            result.Add((start, count));
            start += count;
        }
        return result;
    }
}

using Avalonia;
using Avalonia.Controls;
using MediaLog.ViewModels;

namespace MediaLog.Views;

public partial class MainView : UserControl
{
    /// <summary>Below this width the navigation pane becomes an overlay (phones, narrow windows).</summary>
    private const double CompactWidth = 720;

    private bool? _compact;

    public MainView()
    {
        InitializeComponent();
        Nav.SelectionChanged += (_, _) =>
        {
            if (_compact == true && DataContext is MainViewModel vm)
                vm.IsPaneOpen = false;
        };
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        var compact = width < CompactWidth;
        if (_compact == compact)
            return;
        _compact = compact;

        Split.DisplayMode = compact ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.Inline;
        PaneToggle.IsVisible = compact;
        if (DataContext is MainViewModel vm)
            vm.IsPaneOpen = !compact;
        else
            Split.IsPaneOpen = !compact;
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_compact is { } compact && DataContext is MainViewModel vm)
            vm.IsPaneOpen = !compact;
    }
}

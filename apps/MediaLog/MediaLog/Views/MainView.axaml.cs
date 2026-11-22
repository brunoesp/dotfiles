using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        // Press and hold an entry → details card (touch, mouse and pen alike).
        AddHandler(PointerPressedEvent, OnEntryPressed);
        AddHandler(PointerMovedEvent, OnEntryMoved, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, _) => CancelHold(), handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, _) => CancelHold(), handledEventsToo: true);
        DetailsBackdrop.PointerPressed += (_, e) =>
        {
            if (DataContext is MainViewModel vm)
                vm.CloseDetailsCommand.Execute(null);
            e.Handled = true;
        };
        Nav.SelectionChanged += (_, _) =>
        {
            if (_compact == true && DataContext is MainViewModel vm)
                vm.IsPaneOpen = false;
        };
    }

    private static readonly System.TimeSpan HoldTime = System.TimeSpan.FromMilliseconds(500);
    private const double HoldSlop = 10; // moving further than this is a scroll, not a hold

    private int _holdId;
    private Point _holdStart;
    private bool _holding;

    private async void OnEntryPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only presses on an entry itself (its buttons handle their own presses and don't get here).
        if (DataContext is not MainViewModel vm || vm.IsDetailsOpen || vm.Editor is not null
            || (e.Source as StyledElement)?.DataContext is not EntryViewModel entry)
            return;
        var id = ++_holdId;
        _holding = true;
        _holdStart = e.GetPosition(this);
        await System.Threading.Tasks.Task.Delay(HoldTime);
        if (_holding && id == _holdId)
        {
            _holding = false;
            vm.ShowDetailsCommand.Execute(entry);
        }
    }

    private void OnEntryMoved(object? sender, PointerEventArgs e)
    {
        if (!_holding)
            return;
        var delta = e.GetPosition(this) - _holdStart;
        if (System.Math.Abs(delta.X) > HoldSlop || System.Math.Abs(delta.Y) > HoldSlop)
            CancelHold();
    }

    private void CancelHold() => _holding = false;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLayoutMode(e.NewSize.Width);
        UpdateTileColumns(e.NewSize.Width);
    }

    /// <summary>Picks a tile column count so tiles stay roughly 170px wide.</summary>
    private void UpdateTileColumns(double width)
    {
        if (DataContext is not MainViewModel vm)
            return;
        var contentWidth = width - (_compact == true ? 0 : Split.OpenPaneLength) - 24;
        vm.TileColumns = System.Math.Clamp((int)(contentWidth / 170), 2, 8);
    }

    private void UpdateLayoutMode(double width)
    {
        var compact = width < CompactWidth;
        if (_compact == compact)
            return;
        _compact = compact;

        Split.DisplayMode = compact ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.Inline;
        PaneToggle.IsVisible = compact;
        Classes.Set("compact", compact);

        // Date / list pickers share the add bar's line on wide screens, and get their own row on phones.
        MetaWide.Child = null;
        MetaCompact.Child = null;
        (compact ? MetaCompact : MetaWide).Child = Meta;
        MetaCompact.IsVisible = compact;
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
        UpdateTileColumns(Bounds.Width);
    }
}

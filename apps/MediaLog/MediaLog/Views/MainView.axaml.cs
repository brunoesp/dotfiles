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
        // Tap an entry → details card (touch, mouse and pen alike).
        AddHandler(PointerPressedEvent, OnEntryPressed);
        AddHandler(PointerMovedEvent, OnEntryMoved, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnEntryReleased, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, _) => CancelHold(), handledEventsToo: true);
        ImportButton.Click += async (_, _) => await ImportAsync();
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

    private const double TapSlop = 10; // moving further than this is a scroll, not a tap

    private EntryViewModel? _tapEntry;
    private Point _tapStart;

    private void OnEntryPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only presses on an entry itself (its buttons handle their own presses and don't get here).
        _tapEntry = DataContext is MainViewModel { IsDetailsOpen: false, Editor: null }
            ? (e.Source as StyledElement)?.DataContext as EntryViewModel
            : null;
        _tapStart = e.GetPosition(this);
    }

    private void OnEntryMoved(object? sender, PointerEventArgs e)
    {
        if (_tapEntry is null)
            return;
        var delta = e.GetPosition(this) - _tapStart;
        if (System.Math.Abs(delta.X) > TapSlop || System.Math.Abs(delta.Y) > TapSlop)
            _tapEntry = null; // scrolling
    }

    private void OnEntryReleased(object? sender, PointerReleasedEventArgs e)
    {
        var entry = _tapEntry;
        _tapEntry = null;
        if (entry is not null && DataContext is MainViewModel vm
            && (e.Source as StyledElement)?.DataContext == entry)
            vm.ShowDetailsCommand.Execute(entry);
    }

    private void CancelHold() => _tapEntry = null;

    private async System.Threading.Tasks.Task ImportAsync()
    {
        if (DataContext is not MainViewModel vm || TopLevel.GetTopLevel(this) is not { } top)
            return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Import a MediaLog library",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("MediaLog library") { Patterns = ["*.json"], MimeTypes = ["application/json"] },
                Avalonia.Platform.Storage.FilePickerFileTypes.All,
            ],
        });
        if (files.Count == 0)
            return;
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new System.IO.StreamReader(stream);
            vm.ShowStatus(vm.ImportLibrary(await reader.ReadToEndAsync()));
        }
        catch (System.Exception ex)
        {
            vm.ShowStatus("Couldn't read that file: " + ex.Message);
        }
    }

    // --- On-screen keyboard: keep what's being typed visible ------------------------------------

    private Avalonia.Controls.Platform.IInputPane? _inputPane;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _inputPane = TopLevel.GetTopLevel(this)?.InputPane;
        if (_inputPane is not null)
            _inputPane.StateChanged += OnInputPaneChanged;
        AddHandler(GotFocusEvent, OnFieldFocused);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_inputPane is not null)
            _inputPane.StateChanged -= OnInputPaneChanged;
        _inputPane = null;
        RemoveHandler(GotFocusEvent, OnFieldFocused);
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// The keyboard opened or closed: leave room for it at the bottom (so the content and any
    /// open dialog shrink above it) and scroll the field being typed in into view. If the system
    /// already resized the window, the keyboard doesn't overlap it and nothing is added.
    /// </summary>
    private void OnInputPaneChanged(object? sender, Avalonia.Controls.Platform.InputPaneStateEventArgs e)
    {
        var overlap = 0.0;
        if (e.NewState == Avalonia.Controls.Platform.InputPaneState.Open && e.EndRect.Height > 0)
        {
            var top = TopLevel.GetTopLevel(this);
            var origin = top is not null ? this.TranslatePoint(default, top) ?? default : default;
            var bottom = origin.Y + Bounds.Height;
            overlap = System.Math.Max(0, bottom - e.EndRect.Top);
        }
        Root.Margin = new Thickness(0, 0, 0, overlap);
        if (overlap > 0)
            BringFocusedIntoView();
    }

    private void OnFieldFocused(object? sender, Avalonia.Input.FocusChangedEventArgs e)
    {
        if (e.Source is TextBox && _inputPane?.State == Avalonia.Controls.Platform.InputPaneState.Open)
            BringFocusedIntoView();
    }

    private void BringFocusedIntoView() =>
        // After the new bottom space has been laid out.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control)?.BringIntoView(),
            Avalonia.Threading.DispatcherPriority.Background);

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

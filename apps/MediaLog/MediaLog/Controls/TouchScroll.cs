using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;

namespace MediaLog.Controls;

/// <summary>
/// Calmer touch scrolling for a <see cref="ScrollViewer"/>. Avalonia's momentum starts on almost
/// any release (above 5 px/s), so the small movement while lifting a finger kept the list drifting.
/// With this:
/// <list type="bullet">
/// <item>a drag only starts scrolling after <see cref="StartDistance"/> px (a dead zone);</item>
/// <item>the built-in momentum is kept for real swipes, but cancelled when the release speed is
///   below <see cref="MinFlingSpeed"/> px/s, so stopping and lifting leaves the list where it is.</item>
/// </list>
/// Mouse wheel / scroll bar scrolling is unaffected.
/// </summary>
public static class TouchScroll
{
    public const int StartDistance = 12;
    public const double MinFlingSpeed = 500; // px/s

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("IsEnabled", typeof(TouchScroll));

    public static bool GetIsEnabled(ScrollViewer element) => element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(ScrollViewer element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static readonly AttachedProperty<State?> StateProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, State?>("State", typeof(TouchScroll));

    static TouchScroll()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>((viewer, e) =>
        {
            if (e.NewValue is true && viewer.GetValue(StateProperty) is null)
                Attach(viewer);
        });
    }

    private sealed class State
    {
        /// <summary>Gesture whose (too slow) momentum is being cancelled, and where to keep the list.</summary>
        public int? CancelGestureId;
        public Vector HoldOffset;
    }

    /// <summary>Whether a release at this speed (px/s) should glide.</summary>
    public static bool ShouldGlide(Vector inertia) =>
        Math.Abs(inertia.X) >= MinFlingSpeed || Math.Abs(inertia.Y) >= MinFlingSpeed;

    private static void Attach(ScrollViewer viewer)
    {
        var state = new State();
        viewer.SetValue(StateProperty, state);

        viewer.TemplateApplied += (_, _) => ConfigureRecognizer(viewer);
        ConfigureRecognizer(viewer);

        // Momentum is about to start: let real swipes glide, cancel the slow drift.
        viewer.AddHandler(InputElement.ScrollGestureInertiaStartingEvent, (_, e) =>
        {
            if (!ShouldGlide(e.Inertia))
            {
                state.CancelGestureId = e.Id;
                state.HoldOffset = viewer.Offset;
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        // First momentum step of a cancelled gesture: end it and undo that step.
        viewer.AddHandler(InputElement.ScrollGestureEvent, (_, e) =>
        {
            if (e.Id != state.CancelGestureId)
                return;
            e.ShouldEndScrollGesture = true;
            viewer.Offset = state.HoldOffset;
            state.CancelGestureId = null;
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        viewer.AddHandler(InputElement.ScrollGestureEndedEvent, (_, e) =>
        {
            if (e.Id == state.CancelGestureId)
                state.CancelGestureId = null;
        }, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Bigger dead zone on the built-in drag recognizer (momentum stays on).</summary>
    private static void ConfigureRecognizer(ScrollViewer viewer)
    {
        if (viewer.Presenter is ScrollContentPresenter presenter)
        {
            foreach (var recognizer in presenter.GestureRecognizers.OfType<ScrollGestureRecognizer>())
                recognizer.ScrollStartDistance = StartDistance;
        }
    }
}

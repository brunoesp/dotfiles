using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;

namespace MediaLog.Controls;

/// <summary>
/// Calmer touch scrolling for a <see cref="ScrollViewer"/>. The built-in momentum starts on almost
/// any release (above 5 px/s), so the list keeps drifting after the finger has stopped. With this:
/// <list type="bullet">
/// <item>a drag only starts scrolling after <see cref="StartDistance"/> px (a dead zone);</item>
/// <item>momentum only follows a real flick: the finger still moving fast at release
///   (≥ <see cref="MinFlingSpeed"/> px/s over the last <see cref="VelocityWindow"/>);
///   stopping and then lifting never glides;</item>
/// <item>a fling slows down smoothly and stops when it gets slow or hits the end.</item>
/// </list>
/// Mouse wheel / scroll bar scrolling is unaffected.
/// </summary>
public static class TouchScroll
{
    public const int StartDistance = 12;
    public const double MinFlingSpeed = 600;     // px/s
    public const double StopSpeed = 40;          // px/s
    public const double Friction = 0.04;         // remaining fraction of speed after 1 s
    public static readonly TimeSpan VelocityWindow = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan StillBeforeRelease = TimeSpan.FromMilliseconds(60);

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
        public readonly Queue<(long Ticks, double Y)> Samples = new();
        public IPointer? Touch;
        public double Velocity;        // px/s of the scroll offset during a fling
        public bool Flinging;
        public long LastFrameTicks;
    }

    private static void Attach(ScrollViewer viewer)
    {
        var state = new State();
        viewer.SetValue(StateProperty, state);
        viewer.IsScrollInertiaEnabled = false; // momentum is handled below

        viewer.TemplateApplied += (_, _) => ConfigureRecognizer(viewer);
        ConfigureRecognizer(viewer);

        viewer.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            state.Flinging = false; // touching stops a fling
            if (e.Pointer.Type is PointerType.Touch or PointerType.Pen)
            {
                state.Touch = e.Pointer;
                state.Samples.Clear();
                Add(state, e.GetPosition(viewer).Y);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        viewer.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (e.Pointer == state.Touch)
                Add(state, e.GetPosition(viewer).Y);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        viewer.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
        {
            if (e.Pointer != state.Touch)
                return;
            state.Touch = null;
            Add(state, e.GetPosition(viewer).Y);
            var fingerSpeed = FlingSpeed(state.Samples.ToList(), Stopwatch.GetTimestamp());
            state.Samples.Clear();
            if (fingerSpeed is { } speed)
                StartFling(viewer, state, -speed); // the content moves opposite to the finger
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Bigger dead zone on the built-in drag recognizer.</summary>
    private static void ConfigureRecognizer(ScrollViewer viewer)
    {
        if (viewer.Presenter is ScrollContentPresenter presenter)
        {
            foreach (var recognizer in presenter.GestureRecognizers.OfType<ScrollGestureRecognizer>())
            {
                recognizer.ScrollStartDistance = StartDistance;
                recognizer.IsScrollInertiaEnabled = false;
            }
        }
    }

    private static void Add(State state, double y)
    {
        var now = Stopwatch.GetTimestamp();
        state.Samples.Enqueue((now, y));
        while (state.Samples.Count > 2 && Stopwatch.GetElapsedTime(state.Samples.Peek().Ticks, now) > VelocityWindow * 2)
            state.Samples.Dequeue();
    }

    /// <summary>
    /// The finger's speed (px/s, + = moving down) at release if it was a real flick, otherwise
    /// <c>null</c>: finger held still just before lifting, or moving too slowly.
    /// </summary>
    public static double? FlingSpeed(IReadOnlyList<(long Ticks, double Y)> samples, long releaseTicks)
    {
        if (samples.Count < 2)
            return null;
        // Moved recently? Look at the last movement before release.
        var lastMove = samples.Count - 1;
        while (lastMove > 0 && Math.Abs(samples[lastMove].Y - samples[lastMove - 1].Y) < 0.5)
            lastMove--;
        if (lastMove == 0 || Stopwatch.GetElapsedTime(samples[lastMove].Ticks, releaseTicks) > StillBeforeRelease)
            return null;

        var end = samples[lastMove];
        var start = samples.Take(lastMove)
            .LastOrDefault(s => Stopwatch.GetElapsedTime(s.Ticks, end.Ticks) >= VelocityWindow, samples[0]);
        var seconds = Stopwatch.GetElapsedTime(start.Ticks, end.Ticks).TotalSeconds;
        if (seconds <= 0.005)
            return null;
        var speed = (end.Y - start.Y) / seconds;
        return Math.Abs(speed) >= MinFlingSpeed ? speed : null;
    }

    private static void StartFling(ScrollViewer viewer, State state, double velocity)
    {
        if (TopLevel.GetTopLevel(viewer) is not { } top)
            return;
        state.Velocity = velocity;
        state.Flinging = true;
        state.LastFrameTicks = Stopwatch.GetTimestamp();
        top.RequestAnimationFrame(_ => Frame(viewer, state, top));
    }

    private static void Frame(ScrollViewer viewer, State state, TopLevel top)
    {
        if (!state.Flinging)
            return;
        var now = Stopwatch.GetTimestamp();
        var dt = Math.Min(Stopwatch.GetElapsedTime(state.LastFrameTicks, now).TotalSeconds, 0.05);
        state.LastFrameTicks = now;

        var maxY = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
        var y = Math.Clamp(viewer.Offset.Y + state.Velocity * dt, 0, maxY);
        viewer.Offset = new Vector(viewer.Offset.X, y);
        state.Velocity *= Math.Pow(Friction, dt);

        if (Math.Abs(state.Velocity) < StopSpeed || y <= 0 || y >= maxY)
        {
            state.Flinging = false;
            return;
        }
        top.RequestAnimationFrame(_ => Frame(viewer, state, top));
    }
}

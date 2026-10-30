using System;
using MinimapMod;

internal static class Program
{
    private static int failures;
    private static int tests;

    private static void Main()
    {
        Run("closed-phone positions are sampled on schedule", () => {
            var q = NewQueue();
            q.BeginFrame(8);
            Equal(true, q.TryTake(0, 1, out int item)); Equal(10, item);
            q.BeginFrame(8); Equal(false, q.TryTake(0.05, 1, out item));
            q.BeginFrame(8); Equal(true, q.TryTake(0.1, 1, out item));
        });
        Run("overdue sampling does not create a catch-up burst", () => {
            var q = NewQueue(); q.BeginFrame(8);
            Equal(true, q.TryTake(100, 1, out int item));
            Equal(false, q.TryTake(100, 1, out item));
            q.BeginFrame(8); Equal(false, q.TryTake(100.01, 1, out item));
        });
        Run("disabled categories perform no sampling and resume immediately", () => {
            var q = NewQueue(); q.BeginFrame(8);
            Equal(false, q.TryTake(0, 0, out int item));
            q.BeginFrame(8); Equal(true, q.TryTake(0, 1, out item));
        });
        Run("NPC and co-op categories are independent", () => {
            var q = NewQueue(); q.Upsert(2, 20, 2, 0.05); q.BeginFrame(8);
            Equal(true, q.TryTake(0, 2, out int item)); Equal(20, item);
            Equal(false, q.TryTake(0, 2, out item));
        });
        Run("one frame has bounded work and later frames are fair", () => {
            var q = new TrackingQueue<int>();
            for (int i = 0; i < 1000; i++) q.Upsert(i, i, 1, 0.1);
            q.BeginFrame(2);
            Equal(true, q.TryTake(0, 1, out int a)); Equal(0, a);
            Equal(true, q.TryTake(0, 1, out int b)); Equal(1, b);
            Equal(false, q.TryTake(0, 1, out _));
            q.BeginFrame(2); Equal(true, q.TryTake(0, 1, out int c)); Equal(2, c);
        });
        Run("not-due entries cannot cause an unbounded search", () => {
            var q = new TrackingQueue<int>();
            for (int i = 0; i < 50; i++) q.Upsert(i, i, 1, 1);
            q.BeginFrame(50); while (q.TryTake(0, 1, out _)) { }
            q.BeginFrame(1); Equal(false, q.TryTake(0.05, 1, out _));
            q.Upsert(99, 99, 1, 1);
            q.BeginFrame(2); Equal(false, q.TryTake(0.1, 1, out _));
        });
        Run("reconciliation preserves the sample deadline", () => {
            var q = NewQueue(); q.BeginFrame(8); q.TryTake(0, 1, out _);
            q.Upsert(1, 11, 1, 0.1); Equal(1, q.Count);
            q.BeginFrame(8); Equal(false, q.TryTake(0.01, 1, out _));
            q.BeginFrame(8); Equal(true, q.TryTake(0.1, 1, out int item)); Equal(11, item);
        });
        Run("disconnect and reconnect cannot retain the old identity", () => {
            var q = NewQueue(); Equal(true, q.Remove(1)); Equal(0, q.Count);
            q.Upsert(2, 22, 1, 0.1); q.BeginFrame(8);
            Equal(true, q.TryTake(0, 1, out int item)); Equal(22, item);
        });
        Run("removal during a sampling pass remains usable", () => {
            var q = NewQueue(); q.Upsert(2, 20, 1, 0.1); q.BeginFrame(8);
            q.TryTake(0, 1, out _); q.Remove(1); q.BeginFrame(8);
            Equal(true, q.TryTake(0, 1, out int item)); Equal(20, item);
        });
        Run("reload clears pending work", () => {
            var q = NewQueue(); q.Clear(); q.BeginFrame(8);
            Equal(0, q.Count); Equal(false, q.TryTake(0, 1, out _));
        });
        Run("map projection preserves origin and does not double-apply zoom", () => {
            var p = new MapProjection(-9, 1, 1.25f, 0, 0, 1.25f);
            p.Project(8, 4, out float x, out float y); Equal(1f, x); Equal(6f, y);
            p.Project(0, 0, out x, out y); Equal(-9f, x); Equal(1f, y);
        });
        Run("projection supports changed map axes", () => {
            var p = new MapProjection(10, -4, 0, 2, -3, 0);
            p.Project(2, 3, out float x, out float y); Equal(1f, x); Equal(0f, y);
        });
        Run("transient sample failure retains the source and retries after cooldown", () => {
            var q = new TrackingQueue<MarkerBindingState>();
            var state = new MarkerBindingState(); state.Bind(12, 1, 1, new TestView());
            q.Upsert(7, state, 1, 0.1); q.BeginFrame(8);
            Equal(true, q.TryTake(0, 1, out var current)); current.Failed(0);
            Equal(false, current.CanSample(0.1, 1)); Equal(1, q.Count);
            q.BeginFrame(8); Equal(true, q.TryTake(1, 1, out current));
            Equal(true, current.CanSample(1, 1)); current.Succeeded();
            Equal(true, current.CanSample(1.1, 1));
        });
        Run("destroyed owner identity cannot preserve a previous portrait on rebind", () => {
            var view = new TestView(); var state = new MarkerBindingState();
            state.Bind(111, 1, 1, view);
            // The old native owner can already be destroyed; only its retained
            // identity is needed to invalidate its still-live portrait.
            state.Bind(222, 1, 1, view); Equal(false, view.HasPortrait);
        });
        Run("rebinding into a disabled category hides the existing view immediately", () => {
            var view = new TestView(); var state = new MarkerBindingState();
            state.Bind(111, 1, 1, view); state.Bind(111, 2, 1, view);
            Equal(false, view.Visible); Equal(false, state.CanSample(0, 1));
        });
        Run("dealer border remains 1.5 screen pixels at different zooms", () => {
            Equal(3f, MarkerAppearance.BorderInset(1.5f, 0.5f, 40f));
            Equal(0.5f, MarkerAppearance.BorderInset(1.5f, 3f, 40f));
        });
        Run("dealer border accounts for canvas scaling and preserves tiny portraits", () => {
            Equal(6f, MarkerAppearance.BorderInset(1.5f, 0.25f, 40f));
            Equal(2f, MarkerAppearance.BorderInset(1.5f, 0.1f, 8f));
        });
        Run("invalid or not-yet-laid-out border scales are harmless", () => {
            Equal(0f, MarkerAppearance.BorderInset(1.5f, 0, 40));
            Equal(0f, MarkerAppearance.BorderInset(1.5f, float.NaN, 40));
        });
        Console.WriteLine($"{tests - failures}/{tests} tests passed");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }

    private sealed class TestView : ITrackingView
    {
        public bool HasPortrait = true;
        public bool Visible = true;
        public void Invalidate() { HasPortrait = false; }
        public void Hide() { Visible = false; }
    }

    private static TrackingQueue<int> NewQueue()
    {
        var q = new TrackingQueue<int>(); q.Upsert(1, 10, 1, 0.1); return q;
    }
    private static void Equal<T>(T expected, T actual)
    {
        if (!Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}");
    }
    private static void Run(string name, Action test)
    {
        tests++;
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
}

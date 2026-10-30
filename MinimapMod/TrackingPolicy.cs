using System;
using System.Collections.Generic;

namespace MinimapMod
{
    internal interface ITrackingView
    {
        void Invalidate();
        void Hide();
    }

    internal sealed class MarkerBindingState
    {
        private long ownerId;
        private int category;
        private bool bound;
        private double retryAfter;

        public void Bind(long newOwnerId, int newCategory, int enabledCategories, ITrackingView view)
        {
            bool changed = bound && (ownerId != newOwnerId || category != newCategory);
            ownerId = newOwnerId;
            category = newCategory;
            bound = true;
            retryAfter = 0;
            // Raw retained identity still works after Unity considers the old
            // native object destroyed/null.
            if (changed) view.Invalidate();
            if ((category & enabledCategories) == 0) view.Hide();
        }
        public bool CanSample(double now, int enabledCategories)
            => now >= retryAfter && (category & enabledCategories) != 0;
        public void Failed(double now) { retryAfter = now + 1; }
        public void Succeeded() { retryAfter = 0; }
    }

    // Unity-free policy, shared with the offline regression executable.
    internal sealed class TrackingQueue<T>
    {
        private sealed class Entry
        {
            public long Id;
            public T Value;
            public int Category;
            public int Index;
            public double Interval;
            public double NextDue;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<long, Entry> byId = new Dictionary<long, Entry>();
        private int cursor;
        private int checksRemaining;

        public int Count => entries.Count;

        public void Upsert(long id, T value, int category, double interval)
        {
            if (byId.TryGetValue(id, out Entry entry))
            {
                entry.Value = value;
                entry.Category = category;
                entry.Interval = interval;
                return;
            }
            entry = new Entry { Id = id, Value = value, Category = category,
                Interval = interval, Index = entries.Count };
            entries.Add(entry);
            byId.Add(id, entry);
        }

        public bool Remove(long id)
        {
            if (!byId.TryGetValue(id, out Entry entry)) return false;
            int index = entry.Index;
            entries.RemoveAt(index);
            byId.Remove(id);
            for (int i = index; i < entries.Count; i++) entries[i].Index = i;
            if (index < cursor) cursor--;
            if (cursor >= entries.Count) cursor = 0;
            return true;
        }

        public void Clear()
        {
            entries.Clear();
            byId.Clear();
            cursor = checksRemaining = 0;
        }

        public void BeginFrame(int maximumChecks)
        {
            checksRemaining = Math.Min(Math.Max(0, maximumChecks), entries.Count);
        }
        public bool TryTake(double now, int enabledCategories, out T value)
        {
            while (checksRemaining > 0 && entries.Count > 0)
            {
                checksRemaining--;
                if (cursor >= entries.Count) cursor = 0;
                Entry entry = entries[cursor++];
                if ((entry.Category & enabledCategories) == 0 || now < entry.NextDue) continue;
                // Schedule from now, never replay all missed ticks after a pause.
                entry.NextDue = now + entry.Interval;
                value = entry.Value;
                return true;
            }
            value = default(T);
            return false;
        }
    }

    internal readonly struct MapProjection
    {
        private readonly float originX, originY, xx, xy, zx, zy;
        public MapProjection(float originX, float originY, float xx, float xy, float zx, float zy)
        {
            this.originX = originX; this.originY = originY;
            this.xx = xx; this.xy = xy; this.zx = zx; this.zy = zy;
        }
        public void Project(float x, float z, out float mapX, out float mapY)
        {
            mapX = originX + x * xx + z * zx;
            mapY = originY + x * xy + z * zy;
        }
    }
}

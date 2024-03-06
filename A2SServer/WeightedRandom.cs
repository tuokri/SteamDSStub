namespace A2SServer;

using System;
using System.Collections.Generic;

class WeightedRandomBag<T>
{
    private struct Entry
    {
        public float AccumulatedWeight;
        public T Item;
    }

    private readonly List<Entry> _entries = [];
    private float _accumulatedWeight;
    private readonly Random _rng = new();

    public WeightedRandomBag(List<Tuple<T, float>> entries)
    {
        foreach (var entry in entries)
        {
            AddEntry(entry.Item1, entry.Item2);
        }
    }

    public void AddEntry(T item, float weight)
    {
        _accumulatedWeight += weight;
        _entries.Add(new Entry { Item = item, AccumulatedWeight = _accumulatedWeight });
    }

    public T GetRandom()
    {
        var r = (float)_rng.NextDouble() * _accumulatedWeight;

        foreach (var entry in _entries.Where(entry => entry.AccumulatedWeight >= r))
        {
            return entry.Item;
        }

        throw new ArgumentException("no entries");
    }
}

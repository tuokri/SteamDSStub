/*
 * Copyright (C) 2023-2024  Tuomo Kriikkula
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

namespace A2SServer;

using System;
using System.Collections.Generic;

public class WeightedRandomBag<T>
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
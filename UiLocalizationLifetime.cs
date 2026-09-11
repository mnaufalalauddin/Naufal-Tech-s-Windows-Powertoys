using System;
using System.Collections.Generic;

namespace Naufal_Windows_Tech_s_Powertoys;

// A native control can outlive its managed wrapper. Keep authored wrappers (and
// thus their canonical-text state) alive while they belong to an open window.
// Before forgetting removed controls, the caller restores their canonical text
// and detaches callbacks so that reattaching them cannot adopt a translation.
internal sealed class UiLocalizationLifetime<T> where T : class
{
    private readonly HashSet<T> _retained = new(ReferenceEqualityComparer.Instance);

    internal int Count => _retained.Count;

    internal void Retain(T item) => _retained.Add(item);

    internal void Prune(ISet<T> current, Action<T> release)
    {
        foreach (T item in new List<T>(_retained))
        {
            if (current.Contains(item)) continue;
            release(item);
            _retained.Remove(item);
        }
    }

    internal void Clear(Action<T> release)
    {
        foreach (T item in new List<T>(_retained)) release(item);
        _retained.Clear();
    }
}

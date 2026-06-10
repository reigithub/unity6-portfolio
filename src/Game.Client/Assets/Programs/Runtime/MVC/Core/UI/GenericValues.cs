using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.UI
{
    [Serializable]
    public class GenericValues<T>
    {
        [SerializeField] private T[] _values;

        public T this[int index]
        {
            get => _values[index];
            // set => _values[index] = value;
        }

        public int this[T val]
        {
            get => Math.Max(0, Array.FindIndex(_values, x => EqualityComparer<T>.Default.Equals(x, val)));
        }

        public int Count => _values.Length;
    }
}

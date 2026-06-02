using UnityEngine;

namespace Game.Core.UI
{
    [System.Serializable]
    public class DropdownValues<T>
    {
        [SerializeField] private T[] _values;

        public T this[int index]
        {
            get => _values[index];
            set => _values[index] = value;
        }
    }
}

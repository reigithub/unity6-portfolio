using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Assets
{
    public class MultipleSprite : IReadOnlyList<Sprite>
    {
        private readonly IList<Sprite> _sprites;
        private readonly Dictionary<string, Sprite> _spritesDict;

        public int Count => _sprites.Count;

        public Sprite this[int index] => _sprites[index];

        public Sprite this[string name] => _spritesDict.GetValueOrDefault(name);

        public MultipleSprite(IList<Sprite> sprites)
        {
            _sprites = sprites;
            _spritesDict = new Dictionary<string, Sprite>(sprites.Count);
            foreach (var sprite in this) _spritesDict[sprite.name] = sprite;
        }

        public Enumerator GetEnumerator() => new(this);

        IEnumerator<Sprite> IEnumerable<Sprite>.GetEnumerator()
        {
            for (int i = 0; i < _sprites.Count; i++) yield return _sprites[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => _sprites.GetEnumerator();

        public struct Enumerator
        {
            private readonly MultipleSprite _range;
            private int _index;

            public Enumerator(MultipleSprite range)
            {
                _range = range;
                _index = -1;
            }

            public bool MoveNext() => ++_index < _range.Count;

            public Sprite Current => _range[_index];
        }
    }
}

using UnityEngine;

namespace Game.Core.UI
{
    [System.Serializable]
    public class ResolutionInfo
    {
        [SerializeField] private int _width;
        [SerializeField] private int _height;

        public int Width => _width;
        public int Height => _height;

        public ResolutionInfo(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public override string ToString() => $"{Width} x {Height}";
    }
}

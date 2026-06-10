using TMPro;
using UnityEngine;

namespace Game.Core.UI
{
    /// <summary>リアルタイム FPS をテキスト表示する。一定間隔で平均 FPS を更新（unscaled time）。</summary>
    public class FpsCounter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private float _updateInterval = 0.5f;

        private float _elapsed;
        private int _frames;

        private void OnEnable()
        {
            // 再表示時に前回の蓄積をリセット（古いサンプルでの初回表示を防ぐ）
            _elapsed = 0f;
            _frames = 0;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            _frames++;

            if (_elapsed < _updateInterval) return;

            if (_label != null)
                _label.SetText("{0:0} FPS", CalculateFps(_frames, _elapsed));

            _elapsed = 0f;
            _frames = 0;
        }

        /// <summary>区間のフレーム数と経過時間から平均 FPS を算出（純関数・テスト用）。</summary>
        public static float CalculateFps(int frames, float elapsed)
            => elapsed > 0f ? frames / elapsed : 0f;
    }
}

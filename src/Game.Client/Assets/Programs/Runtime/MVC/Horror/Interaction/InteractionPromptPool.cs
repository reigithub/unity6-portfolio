using System.Collections.Generic;
using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// <see cref="InteractionPromptView"/> の中央プール。画面に同時表示している対象の数だけ View を貸し出す。
    /// PromptCanvas 配下に配置し、生成した全インスタンスは常にこの transform 配下に留まる（返却時も非アクティブ化のみで破棄しない）。
    /// </summary>
    public class InteractionPromptPool : MonoBehaviour, IInteractionPromptPool
    {
        [Tooltip("貸出用プレハブ")]
        [SerializeField] private InteractionPromptView _promptPrefab;

        [Tooltip("起動時に事前生成しておく数。同時貸出数がこれを超えても Rent 時に追加生成して動作は継続する")]
        [SerializeField] private int _prewarmCount = 4;

        private readonly Queue<InteractionPromptView> _idle = new();
        private readonly HashSet<InteractionPromptView> _rented = new();

        public void Initialize()
        {
            for (int i = 0; i < _prewarmCount; i++)
            {
                _idle.Enqueue(CreateView());
            }
        }

        /// <summary>
        /// View を1つ貸し出す。待機列が空なら prewarm 数を超えて追加生成する（動作継続を優先）。
        /// 超過生成は prewarm 数の見直しシグナルとして Warning ログに残す。
        /// </summary>
        public InteractionPromptView Rent()
        {
            if (_idle.Count == 0)
            {
                Debug.LogWarning($"[InteractionPromptViewPool] prewarm 数({_prewarmCount})を超えて追加生成します。同時表示数に対して prewarm 数の見直しを検討してください。", this);
                _idle.Enqueue(CreateView());
            }

            var view = _idle.Dequeue();
            _rented.Add(view);
            return view;
        }

        /// <summary>
        /// View を返却する。<see cref="InteractionPromptView.Unbind"/> して待機列へ戻す。
        /// 貸出中リストに存在しない View（二重返却・他プール由来）は待機列へ戻さず LogError で顕在化する
        /// （無音で握りつぶすと他対象への誤った再貸出に繋がるため）。
        /// </summary>
        public void Return(InteractionPromptView view)
        {
            if (view == null)
            {
                Debug.LogError("[InteractionPromptViewPool] null が返却されました。呼び出し側の貸出参照管理に欠陥があります。", this);
                return;
            }

            if (!_rented.Remove(view))
            {
                Debug.LogError($"[InteractionPromptViewPool] 貸出中でない View が返却されました（二重返却の疑い）: {view.name}", this);
                return;
            }

            view.Unbind();
            _idle.Enqueue(view);
        }

        private InteractionPromptView CreateView()
        {
            var view = Instantiate(_promptPrefab, transform);
            view.Initialize();
            view.gameObject.SetActive(false);
            return view;
        }
    }
}

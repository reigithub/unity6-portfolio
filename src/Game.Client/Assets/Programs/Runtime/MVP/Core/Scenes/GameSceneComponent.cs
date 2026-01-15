using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVP.Core.Scenes
{
    /// <summary>
    /// シーンコンポーネント基底クラス
    /// MonoBehaviourを継承し、シーンのView層を担当
    /// DIはGamePrefabScene/GameDialogSceneがResolver.InjectGameObject()で注入
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))] // フェードインアウト用
    public abstract class GameSceneComponent : MonoBehaviour, IGameSceneComponent
    {
        private Button[] _buttons;

        protected Button[] Buttons => _buttons ??= gameObject.GetComponentsInChildren<Button>();

        public CompositeDisposable Disposables { get; } = new();

        protected virtual void OnDestroy()
        {
            Disposables?.Dispose();
        }

        #region IGameSceneComponent

        public virtual UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Sleep()
        {
            // 一時的な購読解除
            // Disposables.Clear();

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask Restart()
        {
            // スリープ復帰後の購読開始
            // .AddTo(Disposables);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                SetInteractables(true);
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Terminate()
        {
            return UniTask.CompletedTask;
        }

        public virtual void SetInteractables(bool interactable)
        {
            foreach (var button in Buttons)
            {
                button.interactable = interactable;
            }
        }

        #endregion
    }
}
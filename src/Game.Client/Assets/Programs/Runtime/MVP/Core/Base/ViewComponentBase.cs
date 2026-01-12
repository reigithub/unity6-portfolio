using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.ScoreTimeAttack.Base
{
    /// <summary>
    /// View基底コンポーネント
    /// MonoBehaviour + IView実装
    /// </summary>
    public abstract class ViewComponentBase : MonoBehaviour, IView, IInteractableView, IFadeableView //, IGameSceneComponent
    {
        [SerializeField] protected CanvasGroup canvasGroup;

        private Button[] _buttons = Array.Empty<Button>();

        protected virtual void Awake()
        {
            _buttons = GetComponentsInChildren<Button>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        #region IView

        public virtual void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        #endregion

        #region IInteractableView

        public virtual void SetInteractable(bool interactable)
        {
            foreach (var button in _buttons)
            {
                button.interactable = interactable;
            }
        }

        #endregion

        #region IFadeableView

        public virtual void FadeIn(float duration = 0.25f)
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(1f, duration);
            }
        }

        public virtual void FadeOut(float duration = 0.25f)
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, duration);
            }
        }

        #endregion

        #region IGameSceneComponent (既存システムとの互換性)

        public virtual UniTask Startup() => UniTask.CompletedTask;

        public virtual UniTask Ready() => UniTask.CompletedTask;

        public virtual UniTask Sleep()
        {
            SetVisible(false);
            return UniTask.CompletedTask;
        }

        public virtual UniTask Restart()
        {
            SetVisible(true);
            SetInteractable(true);
            return UniTask.CompletedTask;
        }

        public virtual UniTask Terminate() => UniTask.CompletedTask;

        #endregion
    }
}
using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.Enums;
using Game.Horror.Services.Interfaces;
using UnityEngine;

namespace Game.Horror.UI.Sound
{
    /// <summary>
    /// 画面の UI 効果音の受付。画面ルートに置き、Awake で配下へマーカーを装備し、
    /// マーカーからの再生要求（Play）を検証・解決してサービスへ渡す。
    /// 画面ごとの鳴らし方の上書きはインスペクタの _overrides で宣言する。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HorrorUISoundPlayer : MonoBehaviour
    {
        [SerializeField] private List<HorrorUISoundInfo> _overrides = new();

        private IHorrorUISoundService _soundService;
        private HorrorUISoundResolver _resolver;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            HorrorUISoundMarkerInstaller.Install(this);
        }

        /// <summary>
        /// マーカーからの再生要求。interactable ゲートは、初期化中（値復元・タブ初期化）・
        /// 背面・遷移中の画面から届く発火を落とすためのもの。
        /// </summary>
        public void Play(HorrorUISoundType type)
        {
            if (type == HorrorUISoundType.None)
            {
                Debug.LogError("[HorrorUISoundPlayer] None は再生要求に使えません（マーカー側でガードする契約）", this);
                return;
            }

            if (!_canvasGroup.interactable) return;

            _resolver ??= new HorrorUISoundResolver(GameServiceManager.Resolve<IInputSystemService>(), _overrides, this);

            var seAssetName = _resolver.Resolve(type);
            if (string.IsNullOrEmpty(seAssetName)) return;

            _soundService ??= GameServiceManager.Resolve<IHorrorUISoundService>();
            _soundService.Play(type, seAssetName);
        }
    }
}

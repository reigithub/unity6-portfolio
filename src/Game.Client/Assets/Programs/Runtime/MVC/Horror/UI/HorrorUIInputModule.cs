using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Constants;
using Game.Shared.Services;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Horror.UI
{
    /// <summary>
    /// Horror の UI 入力モジュール。UI 入力に横断的な処理の集約点（現在は操作効果音の一元再生のみ）。
    /// 選択音: Process() 前後の選択比較で再生する（Process 外のプログラム起因フォーカスでは鳴らさない）。
    /// 実行音: 新規選択された Button の onClick へ遅延登録する（実行が成立した時のみ鳴る）。
    /// キャンセル音: cancel アクション入力 + 選択GOあり（uGUI がキャンセルを配送する条件と同一）。
    /// </summary>
    public class HorrorUIInputModule : InputSystemUIInputModule
    {
        private IAudioService _audioService;
        private readonly HashSet<int> _hookedButtons = new(); // instanceID はセッション内で再利用されない
        private bool _submitPlayedInProcess;

        public override void Process()
        {
            _submitPlayedInProcess = false;
            var before = eventSystem.currentSelectedGameObject;
            base.Process();
            var after = eventSystem.currentSelectedGameObject;

            if (before != after && after != null)
            {
                TryHookSubmitSound(after);

                // 実行の結果としてフォーカスが移った場合（コンテキストメニュー等）は実行音を優先する
                if (!_submitPlayedInProcess)
                    Play(HorrorAudioConstants.UISelectSfx);
            }

            if (after != null && cancel?.action?.WasPerformedThisFrame() == true)
                Play(HorrorAudioConstants.UICancelSfx);
        }

        private void TryHookSubmitSound(GameObject selected)
        {
            if (!selected.TryGetComponent<Button>(out var button)) return;
            if (!_hookedButtons.Add(button.GetInstanceID())) return;
            button.onClick.AddListener(PlaySubmit);
        }

        private void PlaySubmit()
        {
            _submitPlayedInProcess = true;
            Play(HorrorAudioConstants.UISubmitSfx);
        }

        private void Play(string assetName)
        {
            _audioService ??= GameServiceManager.Resolve<IAudioService>();
            _audioService.PlaySfxOneShotAsync(assetName, destroyCancellationToken).Forget();
        }
    }
}

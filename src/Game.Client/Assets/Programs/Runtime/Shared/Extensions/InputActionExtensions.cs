using System;
using System.Threading;
using R3;
using UnityEngine.InputSystem;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// UnityEngine.InputSystem.InputAction のイベントを R3 Observable に変換する拡張。
    /// 「押下」検知は performed を使う（started は interaction 無し Button で performed と同押下で重複し、
    /// canceled はリリース／Action 無効化時の phase）。InputAction が Enable のときのみ発火する点は呼び出し側の前提。
    /// </summary>
    public static class InputActionExtensions
    {
        /// <summary>started（コントロールが default から作動した瞬間。主に interaction の開始フィードバック用）を Observable 化する。</summary>
        public static Observable<InputAction.CallbackContext> OnStartedAsObservable(this InputAction action, CancellationToken cancellationToken = default)
        {
            return Observable.FromEvent<Action<InputAction.CallbackContext>, InputAction.CallbackContext>(
                h => h, h => action.started += h, h => action.started -= h, cancellationToken);
        }

        /// <summary>performed（Button では押下しきい値を超えた瞬間）を Observable 化する。</summary>
        public static Observable<InputAction.CallbackContext> OnPerformedAsObservable(this InputAction action, CancellationToken cancellationToken = default)
        {
            return Observable.FromEvent<Action<InputAction.CallbackContext>, InputAction.CallbackContext>(
                h => h, h => action.performed += h, h => action.performed -= h, cancellationToken);
        }

        /// <summary>canceled（Button では押下しきい値を下回ったリリース時、または進行中に Disable された時）を Observable 化する。</summary>
        public static Observable<InputAction.CallbackContext> OnCanceledAsObservable(this InputAction action, CancellationToken cancellationToken = default)
        {
            return Observable.FromEvent<Action<InputAction.CallbackContext>, InputAction.CallbackContext>(
                h => h, h => action.canceled += h, h => action.canceled -= h, cancellationToken);
        }
    }
}

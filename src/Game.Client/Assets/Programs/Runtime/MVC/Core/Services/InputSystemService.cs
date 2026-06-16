using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Bootstrap;
using Game.Shared.Constants;
using Game.Shared.Input;
using Game.Shared.Localization;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Core.Services
{
    public class InputSystemService : IInputSystemService, IDisposable
    {
        private ProjectDefaultInputSystem _inputSystem;
        private bool _isInitialized;

        public ProjectDefaultInputSystem.PlayerActions Player => _inputSystem.Player;
        public ProjectDefaultInputSystem.UIActions UI => _inputSystem.UI;

        private string _controlScheme = InputConstants.DefaultControlScheme;
        private GameObject _selectedGameObject;

        #region Setup

        public InputSystemService()
        {
        }

        public void Startup()
        {
            if (_isInitialized) return;

            _inputSystem = new ProjectDefaultInputSystem();
            _inputSystem.Enable();

            // デフォルトでUI入力を有効化
            EnableUI();

            _isInitialized = true;
            Debug.Log("[InputService] Initialized");
        }

        public void Shutdown()
        {
            if (!_isInitialized) return;

            DisablePlayer();
            DisableUI();
            _inputSystem?.Dispose();
            _inputSystem = null;
            _isInitialized = false;

            Debug.Log("[InputService] Shutdown");
        }

        public void EnablePlayer()
        {
            if (Player.enabled) return;
            Player.Enable();
        }

        public void DisablePlayer()
        {
            if (!Player.enabled) return;
            Player.Disable();
        }

        public IDisposable BlockPlayer()
        {
            DisablePlayer();
            return Disposable.Create(() => EnablePlayer());
        }

        public IDisposable BlockUI()
        {
            DisableUI();
            return Disposable.Create(() => EnableUI());
        }

        public void EnableUI()
        {
            if (UI.enabled) return;
            UI.Enable();
        }

        public void DisableUI()
        {
            if (!UI.enabled) return;
            UI.Disable();
        }

        #endregion

        public void ResolveSelectable(GameObject selectedGameObject = null)
        {
            var allSelectables = InputSystemHelper.GetAllSelectables();
            if (allSelectables.Length > 0)
            {
                GameObject go = null;
                bool found = false;

                if (selectedGameObject != null)
                {
                    foreach (var selectable in allSelectables)
                    {
                        if (!selectable.IsSelectable()) continue;
                        if (selectable.gameObject == selectedGameObject)
                        {
                            go = selectable.gameObject;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    var firstSelectable = allSelectables.FirstOrDefault(x => x.IsSelectable());
                    if (firstSelectable != null) go = firstSelectable.gameObject;
                }

                SetSelectedGameObject(go);
                Debug.Log($"[InputService] Selected GameObject {go}");
                return;
            }

            SetSelectedGameObject(null);
            Debug.Log("[InputService] No Selectables found");
        }

        public GameObject GetSelectedGameObject()
        {
            return EventSystem.current.currentSelectedGameObject;
        }

        public void SetSelectedGameObject(GameObject go)
        {
            if (go == null) return;

            _selectedGameObject = go;

            if (EventSystem.current.currentSelectedGameObject == go) return;

            EventSystem.current.SetSelectedGameObject(go);
        }

        public void UpdateControlScheme(string device)
        {
            _controlScheme = device;
            ResolveControlScheme(_selectedGameObject);
        }

        public void ResolveControlScheme(GameObject selectedGameObject = null)
        {
            switch (_controlScheme)
            {
                case InputConstants.Gamepad:
                case InputConstants.Joystick:
                {
                    ApplicationEvents.HideCursor();
                    ResolveSelectable(selectedGameObject);
                    break;
                }
                case InputConstants.KeyboardAndMouse:
                case InputConstants.Touch:
                case InputConstants.XR:
                {
                    ApplicationEvents.ShowCursor();
                    ResolveSelectable(selectedGameObject);
                    break;
                }
            }
        }

        #region Rebinding

        public InputActionAsset Asset => _inputSystem?.asset;

        /// <summary>Player マップから指定名のアクションを解決する（露出対象は Player のみ）。</summary>
        private InputAction ResolveAction(string actionName)
        {
            if (_inputSystem == null || string.IsNullOrEmpty(actionName)) return null;
            var map = _inputSystem.asset.FindActionMap("Player", throwIfNotFound: false);
            return map?.FindAction(actionName, throwIfNotFound: false);
        }

        public string GetBindingDisplayString(string scheme, string actionName, string partName = null)
        {
            var action = ResolveAction(actionName);
            if (action == null) return string.Empty;

            var indices = ResolveSchemeBindingIndices(scheme, action, partName);
            if (indices.Count == 0) return string.Empty;

            var parts = new List<string>(indices.Count);
            foreach (var index in indices)
            {
                // 既定の英語表示・デバイスレイアウト・controlPath を取得し、family 別ローカライズ名へ変換（未登録は英語へフォールバック）
                var raw = action.GetBindingDisplayString(index, out var deviceLayoutName, out var controlPath);
                parts.Add(InputControlLocalizer.Localize(deviceLayoutName, controlPath, raw));
            }
            return string.Join("/", parts);
        }

        public string SaveBindingOverridesAsJson()
            => _inputSystem != null ? _inputSystem.asset.SaveBindingOverridesAsJson() : string.Empty;

        public void LoadBindingOverrides(string json)
        {
            if (_inputSystem == null || string.IsNullOrEmpty(json)) return;
            _inputSystem.asset.LoadBindingOverridesFromJson(json);
        }

        public void ResetAllBindings()
            => _inputSystem?.asset.RemoveAllBindingOverrides();

        public void ResetSchemeBindings(string scheme)
        {
            if (_inputSystem == null || string.IsNullOrEmpty(scheme)) return;
            // 全マップを走査し、指定スキームに属する binding（コンポジットパート含む）の override のみ解除する
            foreach (var map in _inputSystem.asset.actionMaps)
                foreach (var action in map.actions)
                    foreach (var index in ResolveSchemeBindingIndices(scheme, action))
                        action.RemoveBindingOverride(index);
        }

        public void ResetBinding(string scheme, string actionName, string partName = null)
        {
            var action = ResolveAction(actionName);
            if (action == null) return;
            foreach (var index in ResolveSchemeBindingIndices(scheme, action, partName))
                action.RemoveBindingOverride(index);
        }

        public IDisposable StartRebind(string scheme, string actionName, string partName, Action<string> onComplete, Action onCanceled)
        {
            var action = ResolveAction(actionName);
            if (action == null)
            {
                onCanceled?.Invoke();
                return Disposable.Empty;
            }

            var indices = ResolveSchemeBindingIndices(scheme, action, partName);
            if (indices.Count == 0)
            {
                onCanceled?.Invoke();
                return Disposable.Empty;
            }

            // リバインド中はゲーム入力・UI入力を停止する（誤発火・誤確定防止）。
            // enabled なアクションへのリバインドは InvalidOperationException になるためマップを無効化する。
            var wasEnabled = action.actionMap.enabled;
            action.actionMap.Disable();
            var uiBlock = BlockUI();

            InputActionRebindingExtensions.RebindingOperation currentOp = null;
            var finished = false;

            void Finish()
            {
                if (finished) return;
                finished = true;
                currentOp?.Dispose();
                currentOp = null;
                uiBlock.Dispose();
                if (wasEnabled) action.actionMap.Enable();
            }

            void RebindAt(int listIndex)
            {
                if (listIndex >= indices.Count)
                {
                    Finish();
                    onComplete?.Invoke(GetBindingDisplayString(scheme, actionName, partName));
                    return;
                }

                var bindingIndex = indices[listIndex];
                // swap 用にターゲットの旧 effectivePath（override 無しなら既定パス）を退避
                var originalEffectivePath = action.bindings[bindingIndex].effectivePath;

                currentOp?.Dispose();
                currentOp = action.PerformInteractiveRebinding(bindingIndex)
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/delta")
                    .WithControlsExcluding("<Gamepad>/leftStick")
                    .WithControlsExcluding("<Gamepad>/rightStick")
                    .WithCancelingThrough("<Keyboard>/escape")
                    .WithActionEventNotificationsBeingSuppressed();
                ApplySchemeFilter(currentOp, scheme);

                currentOp
                    .OnCancel(_ =>
                    {
                        Finish();
                        onCanceled?.Invoke();
                    })
                    .OnComplete(op =>
                    {
                        var newPath = action.bindings[bindingIndex].effectivePath;
                        if (TryFindConflict(_inputSystem.asset, scheme, action, bindingIndex, newPath, out var conflictAction, out var conflictIndex))
                        {
                            // 同一スキーム内で重複 → 相手へターゲットの旧キーを渡して入れ替える（swap）。
                            // 旧キーが相手の既定パスと一致するなら override を残さず解除する。
                            var conflictDefaultPath = conflictAction.bindings[conflictIndex].path;
                            if (originalEffectivePath == conflictDefaultPath)
                                conflictAction.RemoveBindingOverride(conflictIndex);
                            else
                                conflictAction.ApplyBindingOverride(conflictIndex, originalEffectivePath);
                            Debug.Log($"[InputService] Rebind swapped ({scheme}): {newPath} <-> {originalEffectivePath}");
                        }

                        op.Dispose();
                        currentOp = null;
                        RebindAt(listIndex + 1);
                    });

                currentOp.Start();
            }

            RebindAt(0);

            return Disposable.Create(() =>
            {
                if (currentOp != null && !finished)
                    currentOp.Cancel(); // OnCancel 経由で Finish される
                else
                    Finish();
            });
        }

        /// <summary>リバインド入力を当該スキームのデバイスに限定する。</summary>
        private static void ApplySchemeFilter(InputActionRebindingExtensions.RebindingOperation op, string scheme)
        {
            if (scheme == InputConstants.Gamepad)
            {
                op.WithControlsHavingToMatchPath("<Gamepad>");
            }
            else if (scheme == InputConstants.KeyboardAndMouse)
            {
                op.WithControlsHavingToMatchPath("<Keyboard>");
                op.WithControlsHavingToMatchPath("<Mouse>");
            }
        }

        /// <summary>
        /// 指定アクション・スキームに属するリバインド対象の binding index 群を返す。純粋関数（テスト対象）。
        /// <paramref name="partName"/> 指定時はコンポジット内の該当パート（name 一致）1つのみを返し、単体 binding は対象外。
        /// 未指定時は単体アクションは該当 binding、コンポジットは当該スキームの各パートを返す。
        /// </summary>
        public static IReadOnlyList<int> ResolveSchemeBindingIndices(string scheme, InputAction action, string partName = null)
        {
            var result = new List<int>();
            if (action == null) return result;

            var hasPart = !string.IsNullOrEmpty(partName);
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite)
                {
                    // コンポジットコンテナ自身はパス無し。直後のパート群を見る
                    for (var j = i + 1; j < bindings.Count && bindings[j].isPartOfComposite; j++)
                    {
                        if (!BelongsToScheme(bindings[j], scheme)) continue;
                        if (hasPart && !string.Equals(bindings[j].name, partName, StringComparison.OrdinalIgnoreCase)) continue;
                        result.Add(j);
                    }
                }
                else if (!binding.isPartOfComposite)
                {
                    if (hasPart) continue; // パート指定時は単体 binding を対象としない
                    if (BelongsToScheme(binding, scheme))
                        result.Add(i);
                }
            }

            return result;
        }

        /// <summary>
        /// 候補パスが同一スキーム内の他バインド（自分自身を除く）と衝突するか判定する。純粋関数（テスト対象）。
        /// </summary>
        public static bool WouldConflict(InputActionAsset asset, string scheme, InputAction targetAction, int targetBindingIndex, string candidatePath)
            => TryFindConflict(asset, scheme, targetAction, targetBindingIndex, candidatePath, out _, out _);

        /// <summary>
        /// 候補パスが同一スキーム内の他バインド（自分自身を除く）と衝突する場合、その相手バインドを返す。純粋関数（テスト対象）。
        /// swap（入れ替え）処理で相手バインドへターゲットの旧キーを渡すために用いる。
        /// invariant 上、衝突は高々1件のため最初にヒットしたものを返す。
        /// </summary>
        public static bool TryFindConflict(InputActionAsset asset, string scheme, InputAction targetAction, int targetBindingIndex, string candidatePath, out InputAction conflictAction, out int conflictBindingIndex)
        {
            conflictAction = null;
            conflictBindingIndex = -1;
            if (asset == null || targetAction == null || string.IsNullOrEmpty(candidatePath)) return false;

            var map = targetAction.actionMap;
            if (map == null) return false;

            foreach (var action in map.actions)
            {
                var bindings = action.bindings;
                for (var i = 0; i < bindings.Count; i++)
                {
                    if (action == targetAction && i == targetBindingIndex) continue;

                    var binding = bindings[i];
                    if (binding.isComposite) continue; // コンテナはパス無し
                    if (!BelongsToScheme(binding, scheme)) continue;
                    if (binding.effectivePath == candidatePath)
                    {
                        conflictAction = action;
                        conflictBindingIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>バインドが指定スキーム（groups）に属するか。groups は ';' 区切り。</summary>
        private static bool BelongsToScheme(InputBinding binding, string scheme)
        {
            if (string.IsNullOrEmpty(binding.groups) || string.IsNullOrEmpty(scheme)) return false;
            foreach (var group in binding.groups.Split(InputBinding.Separator))
            {
                if (group == scheme) return true;
            }
            return false;
        }

        #endregion

        public void Dispose()
        {
            Shutdown();
        }
    }
}

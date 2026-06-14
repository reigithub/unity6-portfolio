using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Bootstrap;
using Game.Shared.Constants;
using Game.Shared.Input;
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

        public string GetBindingDisplayString(string actionName, string scheme, string partName = null)
        {
            var action = ResolveAction(actionName);
            if (action == null) return string.Empty;

            var indices = ResolveSchemeBindingIndices(action, scheme, partName);
            if (indices.Count == 0) return string.Empty;

            var parts = new List<string>(indices.Count);
            foreach (var index in indices)
                parts.Add(action.GetBindingDisplayString(index));
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

        public void ResetBinding(string actionName, string scheme, string partName = null)
        {
            var action = ResolveAction(actionName);
            if (action == null) return;
            foreach (var index in ResolveSchemeBindingIndices(action, scheme, partName))
                action.RemoveBindingOverride(index);
        }

        public IDisposable StartRebind(string actionName, string scheme, string partName, Action<string> onComplete, Action onCanceled)
        {
            var action = ResolveAction(actionName);
            if (action == null)
            {
                onCanceled?.Invoke();
                return Disposable.Empty;
            }

            var indices = ResolveSchemeBindingIndices(action, scheme, partName);
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
                    onComplete?.Invoke(GetBindingDisplayString(actionName, scheme, partName));
                    return;
                }

                var bindingIndex = indices[listIndex];
                // 巻き戻し用に元の override 状態を退避（null/空 = override 無し）
                var originalOverridePath = action.bindings[bindingIndex].overridePath;

                currentOp?.Dispose();
                currentOp = action.PerformInteractiveRebinding(bindingIndex)
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/delta")
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
                        if (WouldConflict(_inputSystem.asset, scheme, action, bindingIndex, newPath))
                        {
                            // 同一スキーム内で重複 → 変更を巻き戻す（既定 or 直前の override へ）
                            if (string.IsNullOrEmpty(originalOverridePath))
                                action.RemoveBindingOverride(bindingIndex);
                            else
                                action.ApplyBindingOverride(bindingIndex, originalOverridePath);
                            Debug.Log($"[InputService] Rebind rejected (duplicate in {scheme}): {newPath}");
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
        public static IReadOnlyList<int> ResolveSchemeBindingIndices(InputAction action, string scheme, string partName = null)
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
        {
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
                    if (binding.effectivePath == candidatePath) return true;
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

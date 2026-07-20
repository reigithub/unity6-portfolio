using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Bootstrap;
using Game.Shared.Constants;
using Game.Shared.Input;
using Game.Shared.Services.Interfaces;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Core.Services
{
    public class InputSystemService : IInputSystemService, IDisposable
    {
        private readonly ILocalizationService _localizationService;

        private ProjectInputActions _inputActions;
        private bool _isInitialized;
        private GameObject _selectedGameObject;
        private int _playerBlockCount;
        private int _uiBlockCount;

        public ProjectInputActions.PlayerActions Player => _inputActions.Player;
        public ProjectInputActions.UIActions UI => _inputActions.UI;

        public string ControlScheme { get; private set; } = InputControlSchemes.DefaultControlScheme;

        private readonly Subject<string> _onControlSchemeChanged = new();
        public Observable<string> OnControlSchemeChanged => _onControlSchemeChanged;

        public Observable<InputDeviceChangeInfo> OnDeviceChanged
            => Observable.FromEvent<Action<InputDevice, InputDeviceChange>, InputDeviceChangeInfo>(
                h => (a, b) => h(new InputDeviceChangeInfo(a, b)),
                h => InputSystem.onDeviceChange += h,
                h => InputSystem.onDeviceChange -= h);

        private readonly Subject<InputAction> _onBindingChanged = new();
        public Observable<InputAction> OnBindingChanged => _onBindingChanged;

        #region Setup

        public InputSystemService(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        public void Startup()
        {
            if (_isInitialized) return;

            _inputActions = new ProjectInputActions();
            _inputActions.Enable();

            // foreach (var controlScheme in _inputActions.controlSchemes)
            // {
            //     var scheme = controlScheme.name;
            //
            //     foreach (var map in _inputActions.asset.actionMaps)
            //     {
            //         foreach (var action in map.actions)
            //         {
            //             var paths = GetBindingInfos(scheme, map.name, action.name);
            //             foreach (var path in paths)
            //             {
            //                 Debug.Log($"[InputSystemService] scheme:{scheme}, map:{map.name}, action:{action.name} path:{path.DeviceLayoutName} + {path.ControlPath}");
            //             }
            //         }
            //     }
            // }

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
            _inputActions?.Dispose();
            _inputActions = null;
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

        public IDisposable BlockPlayer()
        {
            if (_playerBlockCount++ <= 0) DisablePlayer();

            return Disposable.Create(() =>
            {
                if (--_playerBlockCount <= 0)
                {
                    EnablePlayer();
                    _playerBlockCount = 0;
                }
            });
        }

        public IDisposable BlockUI()
        {
            if (_uiBlockCount++ <= 0) DisableUI();

            return Disposable.Create(() =>
            {
                if (--_uiBlockCount <= 0)
                {
                    EnableUI();
                    _uiBlockCount = 0;
                }
            });
        }

        public IDisposable BlockInputActions(params InputAction[] actions)
        {
            foreach (var action in actions) action.Disable();

            return Disposable.Create(() =>
            {
                foreach (var action in actions) action.Enable();
            });
        }

        #endregion

        private void ResolveSelectable(GameObject selectedGameObject = null)
        {
            var allSelectables = GetAllSelectables();
            if (allSelectables.Length > 0)
            {
                GameObject go = null;
                bool found = false;

                if (selectedGameObject != null && selectedGameObject.activeSelf)
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

        private static Selectable[] GetAllSelectables()
        {
            Selectable[] allSelectables = Array.Empty<Selectable>();
            int allCount = Selectable.allSelectableCount;
            if (allCount > 0)
                allSelectables = new Selectable[allCount];
            else
                return allSelectables;

            int count = Selectable.AllSelectablesNoAlloc(allSelectables);
            if (count > 0) return allSelectables;

            allSelectables = Selectable.allSelectablesArray;
            return allSelectables;
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
            bool changed = ControlScheme != device;
            ControlScheme = device;
            if (changed) _onControlSchemeChanged.OnNext(device);
            ResolveControlScheme(_selectedGameObject);
        }

        public void ResolveControlScheme(GameObject selectedGameObject = null)
        {
            switch (ControlScheme)
            {
                case InputControlSchemes.Gamepad:
                case InputControlSchemes.Joystick:
                {
                    ApplicationEvents.HideCursor();
                    ResolveSelectable(selectedGameObject);
                    break;
                }
                case InputControlSchemes.KeyboardAndMouse:
                case InputControlSchemes.Touch:
                case InputControlSchemes.XR:
                {
                    ApplicationEvents.ShowCursor();
                    ResolveSelectable(selectedGameObject);
                    break;
                }
            }
        }

        #region Rebinding

        public InputAction FindInputAction(string actionMapName, string actionName)
        {
            if (_inputActions == null || string.IsNullOrEmpty(actionName)) return null;
            var map = _inputActions.asset.FindActionMap(actionMapName, throwIfNotFound: false);
            return map?.FindAction(actionName, throwIfNotFound: false);
        }

        public string GetBindingDisplayString(InputAction action, string partName = null)
        {
            return GetBindingInfo(ControlScheme, action.actionMap.name, action.name, partName).DisplayName;
        }

        public InputBindingInfo[] GetBindingInfos(string scheme, string actionMapName, string actionName, string partName = null)
        {
            var action = FindInputAction(actionMapName, actionName);
            if (action == null) return Array.Empty<InputBindingInfo>();

            // Compositeに対して、partName = nullを指定すると複数バインド情報が返る
            var indices = GetBindingIndicesByControlScheme(scheme, action, partName);
            if (indices.Count == 0) return Array.Empty<InputBindingInfo>();

            var parts = new InputBindingInfo[indices.Count];
            int partsIndex = 0;
            foreach (var info in indices)
            {
                var raw = action.GetBindingDisplayString(info.Index, out var deviceLayoutName, out var controlPath);
                parts[partsIndex] = new InputBindingInfo
                {
                    ControlScheme = scheme,
                    ActionMapName = actionMapName,
                    ActionName = actionName,
                    CompositePartName = partName,
                    DisplayName = _localizationService.GetStringByInputControls(deviceLayoutName, controlPath, raw),
                    DeviceLayoutName = deviceLayoutName,
                    ControlPath = controlPath,
                    BindingIndex = info.Index,
                    IsPartOfComposite = info.IsPartOfComposite
                };
                partsIndex++;
            }

            return parts;
        }

        public InputBindingInfo GetBindingInfo(string scheme, string actionMapName, string actionName, string partName = null, string fallbackScheme = null)
        {
            var paths = GetBindingInfos(scheme, actionMapName, actionName, partName);
            if (paths.Length == 0)
            {
                if (!string.IsNullOrEmpty(fallbackScheme))
                {
                    var fallbacks = GetBindingInfos(fallbackScheme, actionMapName, actionName, partName);
                    if (fallbacks.Length > 0) return fallbacks[0];
                }

                return new InputBindingInfo();
            }

            return paths[0];
        }

        public string SaveBindingOverridesAsJson()
            => _inputActions != null ? _inputActions.asset.SaveBindingOverridesAsJson() : string.Empty;

        public void LoadBindingOverrides(string json)
        {
            if (_inputActions == null || string.IsNullOrEmpty(json)) return;
            _inputActions.asset.LoadBindingOverridesFromJson(json);
        }

        public void ResetAllBindings()
            => _inputActions?.asset.RemoveAllBindingOverrides();

        public void ResetControlSchemeBindings(string scheme)
        {
            if (_inputActions == null || string.IsNullOrEmpty(scheme)) return;
            // 全マップを走査し、指定スキームに属する binding（コンポジットパート含む）の override のみ解除する
            foreach (var map in _inputActions.asset.actionMaps)
                foreach (var action in map.actions)
                    foreach (var info in GetBindingIndicesByControlScheme(scheme, action))
                        action.RemoveBindingOverride(info.Index);
        }

        public void ResetBinding(string scheme, string actionMapName, string actionName, string partName = null)
        {
            var action = FindInputAction(actionMapName, actionName);
            if (action == null) return;
            foreach (var info in GetBindingIndicesByControlScheme(scheme, action, partName))
                action.RemoveBindingOverride(info.Index);
        }

        public IDisposable StartRebinding(string scheme, string actionMapName, string actionName, string partName, Action onComplete, Action onCanceled)
        {
            var action = FindInputAction(actionMapName, actionName);
            if (action == null)
            {
                onCanceled?.Invoke();
                return Disposable.Empty;
            }

            var bindings = GetBindingIndicesByControlScheme(scheme, action, partName);
            if (bindings.Count == 0)
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
                if (listIndex >= bindings.Count)
                {
                    Finish();
                    onComplete?.Invoke();
                    return;
                }

                var bindingIndex = bindings[listIndex].Index;
                // swap 用にターゲットの旧 effectivePath（override 無しなら既定パス）を退避
                var originalEffectivePath = action.bindings[bindingIndex].effectivePath;

                currentOp?.Dispose();
                currentOp = action.PerformInteractiveRebinding(bindingIndex)
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/delta")
                    .WithControlsExcluding("<Gamepad>/leftStick")
                    .WithControlsExcluding("<Gamepad>/rightStick")
                    .WithCancelingThrough("<Keyboard>/escape")
                    // .WithCancelingThrough("<Gamepad>/start")
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
                        if (TryFindConflictAction(_inputActions.asset, scheme, action, bindingIndex, newPath, out var conflictAction, out var conflictIndex))
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

                        _onBindingChanged.OnNext(action);

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
            if (scheme == InputControlSchemes.Gamepad)
            {
                op.WithControlsHavingToMatchPath("<Gamepad>");
            }
            else if (scheme == InputControlSchemes.KeyboardAndMouse)
            {
                op.WithControlsHavingToMatchPath("<Keyboard>");
                op.WithControlsHavingToMatchPath("<Mouse>");
            }
        }

        /// <summary>
        /// 指定アクション・スキームに属するリバインド対象の binding index 群を返す。アセンブリ内部の純粋関数。
        /// <paramref name="partName"/> 指定時はコンポジット内の該当パート（name 一致）1つのみを返し、単体 binding は対象外。
        /// 未指定時は単体アクションは該当 binding、コンポジットは当該スキームの各パートを返す。
        /// </summary>
        internal static IReadOnlyList<InputBindingIndexInfo> GetBindingIndicesByControlScheme(string scheme, InputAction action, string partName = null)
        {
            var result = new List<InputBindingIndexInfo>();
            if (action == null) return result;

            var hasPart = !string.IsNullOrEmpty(partName);
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite)
                {
                    // コンポジットコンテナ自身はパス無し。直後のパート群を見る
                    for (int j = i + 1; j < bindings.Count && bindings[j].isPartOfComposite; j++)
                    {
                        if (!ExistsBingingByControlScheme(bindings[j], scheme)) continue;
                        if (hasPart && !string.Equals(bindings[j].name, partName, StringComparison.OrdinalIgnoreCase)) continue;
                        result.Add(new InputBindingIndexInfo { Index = j, IsPartOfComposite = true });
                    }
                }
                else if (!binding.isPartOfComposite)
                {
                    // if (hasPart) continue; // パート指定時は単体 binding を対象としない
                    if (ExistsBingingByControlScheme(binding, scheme))
                        result.Add(new InputBindingIndexInfo { Index = i, IsPartOfComposite = false });
                }
            }

            return result;
        }

        /// <summary>
        /// 候補パスが同一スキーム内の他バインド（自分自身を除く）と衝突する場合、その相手バインドを返す。アセンブリ内部の純粋関数。
        /// swap（入れ替え）処理で相手バインドへターゲットの旧キーを渡すために用いる。
        /// invariant 上、衝突は高々1件のため最初にヒットしたものを返す。
        /// </summary>
        internal static bool TryFindConflictAction(InputActionAsset asset, string scheme, InputAction targetAction, int targetBindingIndex, string candidatePath, out InputAction conflictAction, out int conflictBindingIndex)
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
                    if (!ExistsBingingByControlScheme(binding, scheme)) continue;
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
        private static bool ExistsBingingByControlScheme(InputBinding binding, string scheme)
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

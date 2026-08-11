using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.Enums;
using Game.Shared.Constants;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Horror.UI.Sound
{
    /// <summary>
    /// 種別→SEアセット名の解決器。画面上書き→既定表の順で行を走査し、
    /// 行の入力因果ゲート（アクションが本フレーム performed / allowPressed 行は押下・作動の継続中も可）を評価する。
    /// </summary>
    public class HorrorUISoundResolver
    {
        private readonly Dictionary<string, InputAction> _actionCache = new();
        private readonly IInputSystemService _inputService;
        private readonly IReadOnlyList<HorrorUISoundInfo> _overrides;
        private readonly Object _logContext;

        public HorrorUISoundResolver(IInputSystemService inputService, IReadOnlyList<HorrorUISoundInfo> overrides, Object logContext = null)
        {
            _inputService = inputService;
            _overrides = overrides;
            _logContext = logContext;
        }

        public string Resolve(HorrorUISoundType type)
        {
            return Resolve(type, _overrides) ?? Resolve(type, HorrorUISoundTable.DefaultRows);
        }

        private string Resolve(HorrorUISoundType type, IReadOnlyList<HorrorUISoundInfo> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Type != type) continue;

                // 空アクション行はゲートなし（種別→アセット名の解決のみ）
                if (string.IsNullOrEmpty(row.ActionName)) return row.SeAssetName;

                var action = FindAction(row.ActionName);
                if (action == null) continue;

                bool pass = action.WasPerformedThisFrame() || (row.AllowPressed && action.IsPressed());
                if (pass) return row.SeAssetName;
            }

            return null;
        }

        private InputAction FindAction(string actionName)
        {
            if (_actionCache.TryGetValue(actionName, out var cached)) return cached;

            var action = _inputService.FindInputAction(InputActionMaps.UI, actionName);
            if (action == null)
                Debug.LogError($"[HorrorUISoundResolver] UI アクションが見つかりません: {actionName}", _logContext);

            // null も登録して、失敗した名前の再探索と多重ログを防ぐ
            _actionCache[actionName] = action;
            return action;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class GameEnvironmentSettingsWindow : EditorWindow
    {
        private Dictionary<GameEnvironment, GameEnvironmentConfig> _configs = new();
        private GameEnvironment[] _envs;
        private string[] _envNames;
        private int _index;

        private void OnEnable()
        {
            titleContent = new GUIContent("ゲーム環境設定");

            _configs = GameEnvironmentSettings.Instance.AllConfigs.ToDictionary(x => x.Environment);
            _envs ??= _configs.Keys.ToArray();
            _envNames ??= _envs
                .Select(x => x.ToString())
                .ToArray();
            var env = GameEnvironmentSettings.Instance.Environment;
            _index = Math.Max(0, Array.IndexOf(_envs, env));
        }

        private void OnGUI()
        {
            var index = EditorGUILayout.Popup(_index, _envNames);
            if (_index != index)
            {
                _index = index;
                GameEnvironmentSettings.Instance.SetConfig(_envs[index]);
                EditorUtility.SetDirty(GameEnvironmentSettings.Instance);
                AssetDatabase.SaveAssetIfDirty(GameEnvironmentSettings.Instance);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                $"Current: {GameEnvironmentSettings.Instance.CurrentConfig}",
                MessageType.Info);
        }
    }
}
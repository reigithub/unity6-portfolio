using System;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class GameConfigEditorWindow : EditorWindow
    {
        private GameEnv[] _envs = null;
        private string[] _envNames = null;
        private int _index;

        private void OnEnable()
        {
            titleContent = new GUIContent("ゲーム環境設定");

            _envs ??= GameConfigManager.LoadAll()
                .Select(x => x.Env)
                .ToArray();

            _envNames ??= _envs
                .Select(x => x.ToString())
                .ToArray();

            var envName = GameConfigManager.GetEnv().ToString();
            _index = Math.Max(0, Array.IndexOf(_envs, envName));
        }

        private void OnGUI()
        {
            var index = EditorGUILayout.Popup(_index, _envNames);
            if (_index != index)
            {
                _index = index;
                GameConfigManager.SetEnv(_envs[_index]);
            }
        }
    }
}
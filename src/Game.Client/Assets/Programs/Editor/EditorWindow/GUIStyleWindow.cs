using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class GUIStyleWindow : EditorWindow
    {
        [MenuItem("Project/EditorWindow/GUIStyleWindow")]
        private static void Open()
        {
            var window = GetWindow<GUIStyleWindow>();
            window.titleContent = new GUIContent("GUIStyle");
            window.Show();
        }

        private List<GUIStyle> _editorGUIStyles;
        private Vector2 _position;

        private void Initialize()
        {
            if (_editorGUIStyles != null)
                return;

            _editorGUIStyles = new List<GUIStyle>();
            var e = GUI.skin.GetEnumerator();
            while (e.MoveNext())
            {
                try
                {
                    _editorGUIStyles.Add(e.Current as GUIStyle);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void OnGUI()
        {
            Initialize();
            using (var scroll = new GUILayout.ScrollViewScope(_position))
            {
                _position = scroll.scrollPosition;
                foreach (var style in _editorGUIStyles)
                {
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        EditorGUILayout.SelectableLabel(style.name);
                        GUILayout.Space(10);
                        EditorGUILayout.LabelField(style.name, style, GUILayout.ExpandWidth(true));
                    }
                }
            }
        }
    }
}
using UnityEditor;

namespace Game.Editor
{
    public static partial class ProjectEditorMenu
    {
        [MenuItem("Project/EditorWindow/Game Environment Settings")]
        public static void GameEnvConfigEditorWindow()
        {
            var window = EditorWindow.GetWindow<GameEnvironmentSettingsWindow>();
            window.Show();
        }
    }
}
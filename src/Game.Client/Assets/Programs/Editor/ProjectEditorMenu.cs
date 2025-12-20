using UnityEditor;

namespace Game.Editor
{
    public static partial class ProjectEditorMenu
    {
        [MenuItem("Project/EditorWindow/GameConfig")]
        public static void GameEnvConfigEditorWindow()
        {
            var window = EditorWindow.GetWindow<GameConfigEditorWindow>();
            window.Show();
        }
    }
}
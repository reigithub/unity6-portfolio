#if UNITY_EDITOR
namespace Game.Shared.Scriptable.Database
{
    /// <summary>
    /// コンテナ資産の固定パス。エディタツール・検証・テストが同じ資産を指すための単一定義。
    /// エディタツール（Assembly-CSharp-Editor）とテスト asmdef の双方から参照できる最下層が
    /// Game.Shared のため、ここに置く（asmdef からは Assembly-CSharp-Editor を参照できない）。
    /// </summary>
    public static class ScriptableDatabaseAssetPath
    {
        public const string EditorAssetPath = "Assets/ProjectAssets/Scriptable/Database/ScriptableDatabase.asset";
    }
}
#endif

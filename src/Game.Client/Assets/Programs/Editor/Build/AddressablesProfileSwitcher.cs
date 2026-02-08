#if UNITY_EDITOR
using Game.Editor.Build;
using Game.Shared;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Build
{
    /// <summary>
    /// Addressables Profile 切り替えメニュー
    /// AddressablesEnvironmentSwitcher を使用してProfile を切り替える
    /// </summary>
    public static class AddressablesProfileSwitcher
    {
        private const string MenuRoot = "Build/Addressables/Switch Profile/";

        [MenuItem(MenuRoot + "Local (Default)", false, 100)]
        public static void SwitchToLocal()
        {
            AddressablesEnvironmentSwitcher.SetActiveProfileFromEnvironment(GameEnvironment.Local, saveAsset: false);
        }

        [MenuItem(MenuRoot + "Develop", false, 101)]
        public static void SwitchToDevelop()
        {
            AddressablesEnvironmentSwitcher.SetActiveProfileFromEnvironment(GameEnvironment.Develop, saveAsset: false);
        }

        [MenuItem(MenuRoot + "Staging", false, 102)]
        public static void SwitchToStaging()
        {
            AddressablesEnvironmentSwitcher.SetActiveProfileFromEnvironment(GameEnvironment.Staging, saveAsset: false);
        }

        [MenuItem(MenuRoot + "Release", false, 103)]
        public static void SwitchToRelease()
        {
            AddressablesEnvironmentSwitcher.SetActiveProfileFromEnvironment(GameEnvironment.Release, saveAsset: false);
        }

        [MenuItem(MenuRoot + "Show Current Profile", false, 200)]
        public static void ShowCurrentProfile()
        {
            var profileName = AddressablesEnvironmentSwitcher.GetCurrentProfileName();
            Debug.Log($"[Addressables] Current Profile: {profileName}");
            EditorUtility.DisplayDialog(
                "Addressables Profile",
                $"Current Profile: {profileName}",
                "OK");
        }
    }
}
#endif

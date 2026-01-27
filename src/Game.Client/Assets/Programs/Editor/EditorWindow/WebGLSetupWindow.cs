using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor
{
    /// <summary>
    /// WebGLビルド用の設定を最適化するウィンドウ
    /// シェーダーエラーやメモリ問題を解決するための設定を適用します
    /// </summary>
    public class WebGLSetupWindow : EditorWindow
    {
        [MenuItem("Tools/WebGL Setup Helper")]
        public static void ShowWindow()
        {
            var window = GetWindow<WebGLSetupWindow>("WebGL Setup Helper");
            window.minSize = new Vector2(500, 600);
        }

        private Vector2 _scrollPosition;
        private UniversalRenderPipelineAsset _mobileRPAsset;
        private UniversalRenderPipelineAsset _currentWebGLAsset;

        private void OnEnable()
        {
            // Mobile_RPAssetを探す
            var guids = AssetDatabase.FindAssets("Mobile_RPAsset t:UniversalRenderPipelineAsset");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _mobileRPAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            }

            // 現在のグラフィックス設定を取得
            _currentWebGLAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("WebGL Setup Helper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "WebGLビルドで発生するシェーダーエラーやメモリ問題を解決するための設定を確認・適用します。",
                MessageType.Info);

            EditorGUILayout.Space(20);
            DrawCurrentStatus();

            EditorGUILayout.Space(20);
            DrawRecommendedFixes();

            EditorGUILayout.Space(20);
            DrawQuickActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCurrentStatus()
        {
            EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Color Space
                var colorSpace = PlayerSettings.colorSpace;
                DrawStatusRow("Color Space", colorSpace.ToString(),
                    colorSpace == ColorSpace.Gamma ? MessageType.Info : MessageType.Warning,
                    "WebGLではGammaが推奨されます（Linearは一部ブラウザで問題発生）");

                // Current Render Pipeline
                var rpName = _currentWebGLAsset != null ? _currentWebGLAsset.name : "None";
                var isMobileRP = _currentWebGLAsset == _mobileRPAsset;
                DrawStatusRow("Render Pipeline", rpName,
                    isMobileRP ? MessageType.Info : MessageType.Warning,
                    isMobileRP ? "Mobile設定を使用中（推奨）" : "PC設定を使用中（WebGLには高負荷）");

                // WebGL Memory
                var initialMemory = PlayerSettings.WebGL.memorySize;
                DrawStatusRow("Initial Memory", $"{initialMemory} MB",
                    initialMemory >= 64 ? MessageType.Info : MessageType.Warning,
                    initialMemory >= 64 ? "十分なメモリ" : "メモリが少ない可能性（64MB以上推奨）");

                // WebGL Exception Support
                var exceptionSupport = PlayerSettings.WebGL.exceptionSupport;
                DrawStatusRow("Exception Support", exceptionSupport.ToString(),
                    exceptionSupport == WebGLExceptionSupport.None ? MessageType.Info : MessageType.Info,
                    "Noneでビルドサイズ削減、Fullでデバッグ容易");

                // HDR
                var hdrEnabled = _mobileRPAsset != null && _mobileRPAsset.supportsHDR;
                DrawStatusRow("HDR", hdrEnabled ? "Enabled" : "Disabled",
                    hdrEnabled ? MessageType.Warning : MessageType.Info,
                    hdrEnabled ? "HDRはWebGLで問題発生の可能性（無効推奨）" : "HDR無効（推奨）");
            }
        }

        private void DrawStatusRow(string label, string value, MessageType type, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(150));

                var icon = type switch
                {
                    MessageType.Info => EditorGUIUtility.IconContent("console.infoicon.sml"),
                    MessageType.Warning => EditorGUIUtility.IconContent("console.warnicon.sml"),
                    MessageType.Error => EditorGUIUtility.IconContent("console.erroricon.sml"),
                    _ => null
                };

                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(18));
                }

                EditorGUILayout.LabelField(new GUIContent(value, tooltip));
            }
        }

        private void DrawRecommendedFixes()
        {
            EditorGUILayout.LabelField("Recommended Fixes", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("WebGLシェーダーエラーの主な原因と対策:", EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("1. Toon/Toon シェーダー", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("   → Tools > UnityChan Material Converter で変換", EditorStyles.miniLabel);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("2. Hidden/Universal/CoreBlit, UI/Default", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("   → URP設定をMobile版に変更（下のボタンで適用）", EditorStyles.miniLabel);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("3. CONTEXT_LOST_WEBGL", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("   → メモリ増加 + シェーダー最適化で解決", EditorStyles.miniLabel);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("4. Skybox/Cubemap", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("   → Skybox/6 Sided または Procedural を使用", EditorStyles.miniLabel);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("5. Hidden/CoreSRP, HDRDebugView 等", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("   → HDRを無効化、デバッグ機能をオフに", EditorStyles.miniLabel);
            }
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 1. Apply Mobile RP
                EditorGUILayout.LabelField("1. Render Pipeline設定", EditorStyles.miniBoldLabel);
                using (new EditorGUI.DisabledScope(_mobileRPAsset == null))
                {
                    if (GUILayout.Button("Mobile_RPAssetをデフォルトに設定", GUILayout.Height(30)))
                    {
                        ApplyMobileRenderPipeline();
                    }
                }

                EditorGUILayout.Space(10);

                // 2. Increase Memory
                EditorGUILayout.LabelField("2. WebGLメモリ設定", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("初期メモリを128MBに増加", GUILayout.Height(30)))
                {
                    IncreaseWebGLMemory();
                }

                EditorGUILayout.Space(10);

                // 3. Disable HDR
                EditorGUILayout.LabelField("3. HDR設定（URP内部シェーダーエラー対策）", EditorStyles.miniBoldLabel);
                using (new EditorGUI.DisabledScope(_mobileRPAsset == null))
                {
                    if (GUILayout.Button("HDRを無効化", GUILayout.Height(30)))
                    {
                        DisableHDR();
                    }
                }

                EditorGUILayout.Space(10);

                // 4. Apply All Recommended
                EditorGUILayout.LabelField("4. 一括適用", EditorStyles.miniBoldLabel);
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button("すべての推奨設定を適用", GUILayout.Height(40)))
                {
                    ApplyAllRecommendedSettings();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(10);

                // 5. Open other tools
                EditorGUILayout.LabelField("5. 関連ツール", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("UnityChan Material Converter", GUILayout.Height(25)))
                    {
                        UnityChanMaterialConverterWindow.ShowWindow();
                    }
                    if (GUILayout.Button("Graphics Settings", GUILayout.Height(25)))
                    {
                        SettingsService.OpenProjectSettings("Project/Graphics");
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Quality Settings", GUILayout.Height(25)))
                    {
                        SettingsService.OpenProjectSettings("Project/Quality");
                    }
                    if (GUILayout.Button("Player Settings (WebGL)", GUILayout.Height(25)))
                    {
                        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                        SettingsService.OpenProjectSettings("Project/Player");
                    }
                }
            }
        }

        private void ApplyMobileRenderPipeline()
        {
            if (_mobileRPAsset == null)
            {
                EditorUtility.DisplayDialog("Error", "Mobile_RPAssetが見つかりません。", "OK");
                return;
            }

            GraphicsSettings.defaultRenderPipeline = _mobileRPAsset;
            _currentWebGLAsset = _mobileRPAsset;

            // Quality Settingsでも設定（全プラットフォーム）
            var qualityLevels = QualitySettings.names;
            for (int i = 0; i < qualityLevels.Length; i++)
            {
                QualitySettings.SetQualityLevel(i);
                QualitySettings.renderPipeline = _mobileRPAsset;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[WebGLSetup] Applied Mobile_RPAsset as default render pipeline");
            EditorUtility.DisplayDialog("Complete", "Mobile_RPAssetをデフォルトRender Pipelineに設定しました。", "OK");
        }

        private void IncreaseWebGLMemory()
        {
            PlayerSettings.WebGL.memorySize = 128;
            Debug.Log("[WebGLSetup] Set WebGL initial memory to 128MB");
            EditorUtility.DisplayDialog("Complete", "WebGL初期メモリを128MBに設定しました。", "OK");
        }

        private void DisableHDR()
        {
            if (_mobileRPAsset == null)
            {
                EditorUtility.DisplayDialog("Error", "Mobile_RPAssetが見つかりません。", "OK");
                return;
            }

            // SerializedObjectを使用してHDRを無効化
            var so = new SerializedObject(_mobileRPAsset);
            var hdrProp = so.FindProperty("m_SupportsHDR");
            if (hdrProp != null)
            {
                hdrProp.boolValue = false;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_mobileRPAsset);
                AssetDatabase.SaveAssets();
                Debug.Log("[WebGLSetup] Disabled HDR in Mobile_RPAsset");
                EditorUtility.DisplayDialog("Complete",
                    "HDRを無効化しました。\n\n" +
                    "これによりHidden/Universal/HDRDebugView等の\nURP内部シェーダーエラーが解消される可能性があります。",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "HDR設定が見つかりません。", "OK");
            }
        }

        private void ApplyAllRecommendedSettings()
        {
            var changes = new System.Text.StringBuilder();

            // 1. Render Pipeline
            if (_mobileRPAsset != null && _currentWebGLAsset != _mobileRPAsset)
            {
                GraphicsSettings.defaultRenderPipeline = _mobileRPAsset;
                _currentWebGLAsset = _mobileRPAsset;
                changes.AppendLine("- Render Pipeline: Mobile_RPAsset に変更");
            }

            // 2. WebGL Memory
            if (PlayerSettings.WebGL.memorySize < 128)
            {
                PlayerSettings.WebGL.memorySize = 128;
                changes.AppendLine("- WebGL Memory: 128MB に増加");
            }

            // 3. Color Space (Gamma is more compatible)
            if (PlayerSettings.colorSpace != ColorSpace.Gamma)
            {
                // Note: Changing color space requires restart, so just warn
                changes.AppendLine("- Color Space: 現在Linear（Gammaへの変更を検討してください）");
            }

            // 4. Disable HDR
            if (_mobileRPAsset != null && _mobileRPAsset.supportsHDR)
            {
                var so = new SerializedObject(_mobileRPAsset);
                var hdrProp = so.FindProperty("m_SupportsHDR");
                if (hdrProp != null)
                {
                    hdrProp.boolValue = false;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_mobileRPAsset);
                    changes.AppendLine("- HDR: 無効化（URP内部シェーダーエラー対策）");
                }
            }

            // 5. Compression Format
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            changes.AppendLine("- Compression: Gzip に設定");

            // 6. Decompression Fallback
            PlayerSettings.WebGL.decompressionFallback = true;
            changes.AppendLine("- Decompression Fallback: 有効化");

            AssetDatabase.SaveAssets();

            if (changes.Length > 0)
            {
                Debug.Log($"[WebGLSetup] Applied recommended settings:\n{changes}");
                EditorUtility.DisplayDialog("Complete",
                    $"推奨設定を適用しました:\n\n{changes}\n\n" +
                    "注意: UnityChanのマテリアル変換は別途実行してください。\n" +
                    "(Tools > UnityChan Material Converter)",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Info", "すべての推奨設定が既に適用されています。", "OK");
            }
        }
    }
}

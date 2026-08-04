#if UNITY_EDITOR
using System;
using System.Linq;
using Game.Core.UI;
using Game.Horror.Dialogs;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Editor.MenuCommands
{
    /// <summary>
    /// HorrorItemDetailDialog の一括セットアップ用一時スクリプト。
    /// レイヤー追加 → ローカライズキー追加 → プレハブ構築 → Addressables 登録 を冪等に実行する。
    /// セットアップ完了・動作確認後にこのファイルは削除する。
    /// </summary>
    public static class HorrorItemDetailDialogBuilder
    {
        private const string MenuRoot = "Tools/Horror/Item Detail Dialog/";
        private const string PreviewLayerName = "AssetPreviewRenderTexture";
        private const string PrefabPath = "Assets/ProjectAssets/Horror/HorrorItemDetailDialog.prefab";
        private const string AddressableGroupName = "HorrorScenes";
        private const string AddressableAddress = "HorrorItemDetailDialog";
        private const string TmpFontGuid = "fec7acc7f723aa84a978ef106c434509"; // 既存ダイアログと同じ TMP フォント

        // ゲージ fill の配色は SliderBuilder の慣習に合わせる
        private static readonly Color GaugeFillColor = new(0f, 0.59f, 0.59f, 1f);
        private static readonly Color BackdropColor = new(0f, 0f, 0f, 0.5882353f); // HorrorEquipmentShortcutDialog の暗幕と同色

        [MenuItem(MenuRoot + "1. Add AssetPreviewRenderTexture Layer")]
        public static void AddLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == PreviewLayerName)
                {
                    Debug.Log($"[ItemDetailDialogBuilder] Layer '{PreviewLayerName}' は既に存在します (index {i})");
                    return;
                }
            }

            // ユーザーレイヤー領域 (11 以降) の先頭の空きへ追加する
            for (int i = 11; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = PreviewLayerName;
                    tagManager.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[ItemDetailDialogBuilder] Layer '{PreviewLayerName}' を index {i} に追加しました");
                    return;
                }
            }

            Debug.LogError("[ItemDetailDialogBuilder] 空きレイヤーがありません");
        }

        [MenuItem(MenuRoot + "2. Add Localization Entries")]
        public static void AddLocalizationEntries()
        {
            // UITexts: SPECS 欄ラベル(コードから GetStringByUITexts で解決)
            AddEntries("UITexts", new[]
            {
                ("ItemDetail_Specs", "SPECS", "SPECS"),
                ("ItemDetail_Power", "威力", "Power"),
                ("ItemDetail_Stability", "安定性", "Stability"),
                ("ItemDetail_Accuracy", "射撃精度", "Accuracy"),
                ("ItemDetail_FireRate", "連射速度", "Fire Rate"),
                ("ItemDetail_ReloadSpeed", "装填速度", "Reload Speed"),
                ("ItemDetail_Capacity", "装填数", "Capacity"),
            });

            // ContextActions: 操作ガイドラベル(プレハブの LocalizeStringEvent から解決)。Reset/Close は既存キーを尊重し不足時のみ追加
            AddEntries("ContextActions", new[]
            {
                ("Rotate", "回転", "Rotate"),
                ("Zoom", "ズーム", "Zoom"),
                ("Reset", "リセット", "Reset"),
                ("Close", "閉じる", "Close"),
            });

            AssetDatabase.SaveAssets();
            Debug.Log("[ItemDetailDialogBuilder] ローカライズキーの追加が完了しました");
        }

        private static void AddEntries(string collectionName, (string key, string ja, string en)[] entries)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
            if (collection == null)
            {
                Debug.LogError($"[ItemDetailDialogBuilder] StringTableCollection '{collectionName}' が見つかりません");
                return;
            }

            foreach (var (key, ja, en) in entries)
            {
                var shared = collection.SharedData.GetEntry(key) ?? collection.SharedData.AddKey(key);

                foreach (var table in collection.StringTables)
                {
                    var value = table.LocaleIdentifier.Code switch
                    {
                        "ja" => ja,
                        "en" => en,
                        _ => null,
                    };
                    if (value == null) continue;

                    // 既存の訳文は上書きしない(不足キーの追加のみ)
                    var entry = table.GetEntry(shared.Id);
                    if (entry == null || string.IsNullOrEmpty(entry.Value))
                        table.AddEntry(shared.Id, value);

                    EditorUtility.SetDirty(table);
                }
            }

            EditorUtility.SetDirty(collection.SharedData);
        }

        [MenuItem(MenuRoot + "3. Build Prefab")]
        public static void BuildPrefab()
        {
            int previewLayer = LayerMask.NameToLayer(PreviewLayerName);
            if (previewLayer < 0)
            {
                Debug.LogError($"[ItemDetailDialogBuilder] Layer '{PreviewLayerName}' が未追加です。先に 1. Add AssetPreviewRenderTexture Layer を実行してください");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(TmpFontGuid));
            if (font == null)
            {
                Debug.LogError("[ItemDetailDialogBuilder] TMP フォントアセットが見つかりません");
                return;
            }

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // ---- ルート (Canvas + Component) ----
            var root = new GameObject("HorrorItemDetailDialog", typeof(RectTransform));
            try
            {
                root.layer = 5; // UI
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.additionalShaderChannels = (AdditionalCanvasShaderChannels)31; // 既存ダイアログと同じ全チャンネル

                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                root.AddComponent<GraphicRaycaster>();
                var component = root.AddComponent<HorrorItemDetailDialogComponent>(); // RequireComponent で CanvasGroup も付与される

                // ---- 暗幕 ----
                var background = CreateUIObject("Background", root.transform);
                Stretch(background);
                var backgroundImage = background.gameObject.AddComponent<Image>();
                backgroundImage.color = BackdropColor;

                // ---- 3D プレビュー表示先 ----
                // 全画面に広げ、ズーム時のフラスタム外クロップ境界を画面端に一致させる（中央に切れ目を出さない）
                var previewImageRect = CreateUIObject("PreviewImage", root.transform);
                Stretch(previewImageRect);
                var previewImage = previewImageRect.gameObject.AddComponent<RawImage>();
                previewImage.raycastTarget = false;

                // ---- モデル未設定時のフォールバックアイコン ----
                var fallbackRect = CreateUIObject("FallbackIcon", root.transform);
                fallbackRect.sizeDelta = new Vector2(400f, 400f);
                fallbackRect.anchoredPosition = new Vector2(0f, 40f);
                var fallbackIcon = fallbackRect.gameObject.AddComponent<Image>();
                fallbackIcon.preserveAspect = true;
                fallbackIcon.raycastTarget = false;
                fallbackRect.gameObject.SetActive(false);

                // ---- SPECS パネル (左下) ----
                var specsView = BuildSpecsPanel(root.transform, font, uiSprite);

                // ---- 名前 / 説明パネル (右下) ----
                var infoPanel = CreateUIObject("InfoPanel", root.transform);
                SetAnchor(infoPanel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
                infoPanel.anchoredPosition = new Vector2(-80f, 120f);
                infoPanel.sizeDelta = new Vector2(520f, 360f);
                var infoLayout = infoPanel.gameObject.AddComponent<VerticalLayoutGroup>();
                infoLayout.spacing = 16f;
                infoLayout.childControlWidth = true;
                infoLayout.childControlHeight = true;
                infoLayout.childForceExpandWidth = true;
                infoLayout.childForceExpandHeight = false;

                var nameText = CreateText("NameText", infoPanel, font, 40f, string.Empty);
                var descriptionText = CreateText("DescriptionText", infoPanel, font, 24f, string.Empty);
                descriptionText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

                // ---- 操作ガイド (下部中央) ----
                var guide = BuildInputActionGuide(root.transform, font);

                // ---- 3D プレビューリグ (実行時に Canvas 外へ切り離される) ----
                var previewView = BuildPreviewRig(root.transform, previewLayer);

                // ---- SerializeField 配線 ----
                SetRefs(component,
                    ("_previewView", previewView),
                    ("_specsView", specsView),
                    ("_previewImage", previewImage),
                    ("_fallbackIcon", fallbackIcon),
                    ("_nameText", nameText),
                    ("_descriptionText", descriptionText),
                    ("_inputActionGuide", guide));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[ItemDetailDialogBuilder] プレハブを保存しました: {PrefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static HorrorWeaponSpecsView BuildSpecsPanel(Transform parent, TMP_FontAsset font, Sprite uiSprite)
        {
            var panel = CreateUIObject("SpecsPanel", parent);
            SetAnchor(panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            panel.anchoredPosition = new Vector2(80f, 120f);
            panel.sizeDelta = new Vector2(420f, 340f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var specsView = panel.gameObject.AddComponent<HorrorWeaponSpecsView>();

            var specsLabel = CreateText("SpecsLabel", panel, font, 30f, "SPECS");

            (TextMeshProUGUI label, Slider gauge) BuildGaugeRow(string name)
            {
                var row = CreateUIObject(name, panel);
                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 12f;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;
                rowLayout.childAlignment = TextAnchor.MiddleLeft;

                var label = CreateText("Label", row, font, 24f, string.Empty);
                AddLayoutElement(label.gameObject, 160f, 30f);

                var gauge = BuildGauge("Gauge", row, uiSprite);
                return (label, gauge);
            }

            var power = BuildGaugeRow("PowerRow");
            var stability = BuildGaugeRow("StabilityRow");
            var accuracy = BuildGaugeRow("AccuracyRow");
            var fireRate = BuildGaugeRow("FireRateRow");
            var reloadSpeed = BuildGaugeRow("ReloadSpeedRow");

            // 装填数のみ数値表示
            var capacityRow = CreateUIObject("CapacityRow", panel);
            var capacityLayout = capacityRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            capacityLayout.spacing = 12f;
            capacityLayout.childControlWidth = true;
            capacityLayout.childControlHeight = true;
            capacityLayout.childForceExpandWidth = false;
            capacityLayout.childForceExpandHeight = false;
            capacityLayout.childAlignment = TextAnchor.MiddleLeft;
            var capacityLabel = CreateText("Label", capacityRow, font, 24f, string.Empty);
            AddLayoutElement(capacityLabel.gameObject, 160f, 30f);
            var capacityValue = CreateText("Value", capacityRow, font, 26f, string.Empty);
            AddLayoutElement(capacityValue.gameObject, 100f, 30f);

            SetRefs(specsView,
                ("_powerGauge", power.gauge),
                ("_stabilityGauge", stability.gauge),
                ("_accuracyGauge", accuracy.gauge),
                ("_fireRateGauge", fireRate.gauge),
                ("_reloadSpeedGauge", reloadSpeed.gauge),
                ("_specsLabel", specsLabel),
                ("_powerLabel", power.label),
                ("_stabilityLabel", stability.label),
                ("_accuracyLabel", accuracy.label),
                ("_fireRateLabel", fireRate.label),
                ("_reloadSpeedLabel", reloadSpeed.label),
                ("_capacityLabel", capacityLabel),
                ("_capacityValueText", capacityValue));

            return specsView;
        }

        // 表示専用ゲージ: ハンドル無し・非インタラクティブの Slider(fill 配色は SliderBuilder の慣習)
        private static Slider BuildGauge(string name, RectTransform parent, Sprite uiSprite)
        {
            var sliderRect = CreateUIObject(name, parent);
            AddLayoutElement(sliderRect.gameObject, 220f, 14f);

            var background = CreateUIObject("Background", sliderRect);
            Stretch(background);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.sprite = uiSprite;
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.color = new Color(1f, 1f, 1f, 0.12f);
            backgroundImage.raycastTarget = false;

            var fillArea = CreateUIObject("Fill Area", sliderRect);
            Stretch(fillArea);

            var fill = CreateUIObject("Fill", fillArea);
            Stretch(fill); // Slider はアンカーのみ駆動するため、sizeDelta を 0 にしないと矩形が既定サイズぶん膨らむ
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = uiSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = GaugeFillColor;
            fillImage.raycastTarget = false;

            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        private static InputActionGuildView BuildInputActionGuide(Transform parent, TMP_FontAsset font)
        {
            var guideRect = CreateUIObject("InputActionGuide", parent);
            SetAnchor(guideRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            guideRect.anchoredPosition = new Vector2(0f, 40f);
            var layout = guideRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 32f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            var fitter = guideRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var guideView = guideRect.gameObject.AddComponent<InputActionGuildView>();

            // 行ルートに InputActionView を置く(SetInputActions で行ごと表示切替するため)。
            // 追加アイコンは行内の子に独立した InputActionView を持たせる
            (RectTransform row, InputActionView view) BuildRow(string name, string actionName, string localizeKey, string controlScheme = "")
            {
                var row = CreateUIObject(name, guideRect);
                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 8f;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;
                rowLayout.childAlignment = TextAnchor.MiddleCenter;

                var icon = CreateIcon("Icon", row);
                var view = row.gameObject.AddComponent<InputActionView>();
                WireInputActionView(view, icon.GetComponent<Image>(), actionName, controlScheme);

                var label = CreateText("Label", row, font, 24f, string.Empty);
                WireLocalizedLabel(label, localizeKey);

                return (row, view);
            }

            var (rotateRow, rotateView) = BuildRow("RotateRow", "Previous", "Rotate");
            // マウス移動・ゲームパッドスティック・E キーのアイコンを回転行に追加(各自の InputActionView で個別切替)
            var padIcon = CreateIcon("IconPad", rotateRow);
            padIcon.transform.SetSiblingIndex(0);
            var padView = padIcon.gameObject.AddComponent<InputActionView>();
            WireInputActionView(padView, padIcon.GetComponent<Image>(), "Navigate", "Gamepad");
            var iconE = CreateIcon("IconE", rotateRow);
            iconE.transform.SetSiblingIndex(2);
            var nextView = iconE.gameObject.AddComponent<InputActionView>();
            WireInputActionView(nextView, iconE.GetComponent<Image>(), "Next", string.Empty);
            // マウス回転はキーボード＆マウス時のみ案内する(パッド時の回転案内は IconPad が担う)
            var mouseIcon = CreateIcon("IconMouse", rotateRow);
            mouseIcon.transform.SetSiblingIndex(0);
            var mouseView = mouseIcon.gameObject.AddComponent<InputActionView>();
            WireInputActionView(mouseView, mouseIcon.GetComponent<Image>(), "PointDelta", "Keyboard&Mouse");

            var (_, zoomView) = BuildRow("ZoomRow", "ScrollWheel", "Zoom");
            var (_, resetView) = BuildRow("ResetRow", "Reset", "Reset");
            var (_, closeView) = BuildRow("CloseRow", "Cancel", "Close");

            var so = new SerializedObject(guideView);
            var array = so.FindProperty("_inputActionViews");
            var views = new InputActionView[] { mouseView, rotateView, padView, nextView, zoomView, resetView, closeView };
            array.arraySize = views.Length;
            for (int i = 0; i < views.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return guideView;
        }

        private static RectTransform CreateIcon(string name, RectTransform parent)
        {
            var icon = CreateUIObject(name, parent);
            var image = icon.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            AddLayoutElement(icon.gameObject, 40f, 40f);
            return icon;
        }

        private static void WireInputActionView(InputActionView view, Image icon, string actionName, string controlScheme)
        {
            var so = new SerializedObject(view);
            so.FindProperty("_actionIcon").objectReferenceValue = icon;
            so.FindProperty("_actionMapName").stringValue = "UI";
            so.FindProperty("_actionName").stringValue = actionName;
            so.FindProperty("_controlScheme").stringValue = controlScheme;
            so.FindProperty("_initializeOnStart").boolValue = false; // Component が guide.Initialize() を明示的に呼ぶ
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ガイドラベルはロケール変更へ自動追従させるため LocalizeStringEvent(ContextActions)で解決する
        private static void WireLocalizedLabel(TextMeshProUGUI label, string key)
        {
            var locEvent = label.gameObject.AddComponent<LocalizeStringEvent>();
            locEvent.StringReference.SetReference("ContextActions", key);

            var setter = (UnityAction<string>)Delegate.CreateDelegate(
                typeof(UnityAction<string>), label, typeof(TMP_Text).GetProperty("text")!.GetSetMethod());
            UnityEventTools.AddPersistentListener(locEvent.OnUpdateString, setter);
        }

        private static HorrorItemPreviewView BuildPreviewRig(Transform parent, int previewLayer)
        {
            var rig = new GameObject("PreviewRig");
            rig.transform.SetParent(parent, false);
            var previewView = rig.AddComponent<HorrorItemPreviewView>();

            var cameraGo = new GameObject("PreviewCamera");
            cameraGo.transform.SetParent(rig.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -3.3f); // 全画面化で見かけサイズが増すぶん引く
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 透過クリアで暗幕上に合成する
            camera.cullingMask = 1 << previewLayer;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false; // シーンのビネット等がプレビューへ混入しないようにする
            cameraData.renderShadows = false;

            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(rig.transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(30f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.cullingMask = 1 << previewLayer;
            light.shadows = LightShadows.None;

            var anchor = new GameObject("ModelAnchor");
            anchor.transform.SetParent(rig.transform, false);

            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = previewLayer;

            SetRefs(previewView,
                ("_previewCamera", camera),
                ("_previewLight", light),
                ("_modelAnchor", anchor.transform));

            return previewView;
        }

        [MenuItem(MenuRoot + "4. Register Addressable")]
        public static void RegisterAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[ItemDetailDialogBuilder] AddressableAssetSettings が見つかりません");
                return;
            }

            var group = settings.FindGroup(AddressableGroupName);
            if (group == null)
            {
                Debug.LogError($"[ItemDetailDialogBuilder] グループ '{AddressableGroupName}' が見つかりません");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[ItemDetailDialogBuilder] プレハブが見つかりません: {PrefabPath}。先に 3. Build Prefab を実行してください");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
            entry.address = AddressableAddress;
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemDetailDialogBuilder] Addressable 登録完了: {AddressableAddress} → {PrefabPath}");
        }

        [MenuItem(MenuRoot + "Run All")]
        public static void RunAll()
        {
            AddLayer();
            AddLocalizationEntries();
            BuildPrefab();
            RegisterAddressable();
        }

        // ---- 共通ヘルパー ----

        private static RectTransform CreateUIObject(string name, Component parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent.transform, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetAnchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, TMP_FontAsset font, float fontSize, string text)
        {
            var rect = CreateUIObject(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.text = text;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void AddLayoutElement(GameObject go, float width, float height)
        {
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
        }

        private static void SetRefs(Component target, params (string field, UnityEngine.Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogError($"[ItemDetailDialogBuilder] {target.GetType().Name} にフィールド {field} が見つかりません");
                    continue;
                }

                prop.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif

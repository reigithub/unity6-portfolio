using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public class WelcomeWindow : EditorWindow
{
    // ==== Styles ====
    private GUIStyle textureButton;
    private GUIStyle headingText;
    private GUIStyle commonText;

    // ==== Assets ====
    private Texture2D top;
    private Texture2D image1;
    private Texture2D logo;

    private Vector2 scrollIndex;

    // Klucz per-projekt (MD5 z Application.dataPath)
    private const string BaseKey = "EmaceArt_WelcomeWindowShown_";
    private static string ProjectKey => BaseKey + Md5(Application.dataPath);

    private static string Md5(string s)
    {
        using var md5 = MD5.Create();
        var data = md5.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
        var sb = new StringBuilder(data.Length * 2);
        foreach (var b in data) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static bool HasShown
    {
        get => EditorPrefs.GetBool(ProjectKey, false);
        set => EditorPrefs.SetBool(ProjectKey, value);
    }

    // Auto-otwieranie raz na projekt
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OpenWindowOnUnityStart()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!HasShown)
        {
            EditorApplication.delayCall += () =>
            {
                if (!HasShown)
                {
                    OpenWindow();
                    HasShown = true;
                }
            };
        }
    }

    [MenuItem("Tools/EmaceArt/Welcome Window")]
    private static void OpenWindow()
    {
        var panel = GetWindow<WelcomeWindow>();
        panel.titleContent = new GUIContent("Hello Developer", Resources.Load<Texture2D>("Favi_top"));
        panel.minSize = new Vector2(560, 420);
        panel.Show();
    }

    // === Reset (pokaz przy nastêpnym starcie) ===
    [MenuItem("Tools/EmaceArt/Reset Welcome Window (next start)")]
    private static void ResetWelcomeWindowNextStart()
    {
        EditorPrefs.DeleteKey(ProjectKey);
        EditorUtility.DisplayDialog(
            "EmaceArt",
            "Zresetowano stan okna powitalnego dla TEGO projektu.\n" +
            "Okno poka¿e siê przy nastêpnym prze³adowaniu skryptów lub restarcie edytora.\n\n" +
            "—\n\n" +
            "Welcome window state has been RESET for THIS project.\n" +
            "It will show up on the next script reload or after restarting the editor.",
            "OK"
        );
    }

    // === Reset & natychmiast poka¿ teraz ===
    [MenuItem("Tools/EmaceArt/Reset & Show Welcome Window NOW")]
    private static void ResetAndShowNow()
    {
        EditorPrefs.DeleteKey(ProjectKey);
        OpenWindow();
        HasShown = true;
        EditorUtility.DisplayDialog(
            "EmaceArt",
            "Okno powitalne zosta³o zresetowane i pokazane TERAZ.\n" +
            "Nie wyskoczy ponownie po restarcie, dopóki nie zresetujesz go ponownie.\n\n" +
            "—\n\n" +
            "The welcome window has been RESET and shown NOW.\n" +
            "It won’t pop up again on next start unless you reset it again.",
            "OK"
        );
    }

    private void OnEnable()
    {
        // GUISkin fallback
        var style = Resources.Load<GUISkin>("GUISkin");
        textureButton = style ? style.GetStyle("textureButton") : new GUIStyle(GUI.skin.button) { imagePosition = ImagePosition.ImageOnly };
        headingText = style ? style.GetStyle("headingText") : new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, wordWrap = true };
        commonText = style ? style.GetStyle("commonText") : new GUIStyle(EditorStyles.label) { wordWrap = true };

        // Textures fallback
        top = Resources.Load<Texture2D>("EA_Top") ?? Texture2D.grayTexture;
        image1 = Resources.Load<Texture2D>("Btn_01") ?? Texture2D.whiteTexture;
        logo = Resources.Load<Texture2D>("Logo") ?? Texture2D.blackTexture;
    }

    private void OnGUI()
    {
        scrollIndex = GUILayout.BeginScrollView(scrollIndex);
        GUILayout.BeginVertical();

        DrawHeader();
        GUILayout.Space(20f);
        DrawBody();
        GUILayout.Space(20f);
        DrawFooter();

        GUILayout.EndVertical();
        GUILayout.EndScrollView();

        // Pasek narzêdzi w oknie
        GUILayout.Space(8);
        GUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Utilities:", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset (show on NEXT start)"))
        {
            ResetWelcomeWindowNextStart();
        }

        if (GUILayout.Button("Reset & SHOW NOW"))
        {
            ResetAndShowNow();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawHeader()
    {
        if (GUILayout.Button(top, textureButton))
            Application.OpenURL("https://assetstore.unity.com/packages/3d/environments/urban/stylized-fantasy-graveyard-huuuge-world-144129");
    }

    private void DrawBody()
    {
        // Tytu³ + opis
        GUILayout.Label("Thanks for checking NecroPOLY Lite", headingText);
        GUILayout.Space(2f);
        GUILayout.Label("This pack perfectly matches the NecroPOLY FULL!", commonText);
        GUILayout.Space(10f);

        // Pasek: logo + FREE ZONE (zielony)
        GUILayout.BeginHorizontal();

        if (GUILayout.Button(logo, textureButton, GUILayout.Width(72), GUILayout.Height(72)))
            Application.OpenURL("https://www.emaceart.com");

        GUILayout.BeginVertical();
        GUILayout.Space(2f);
        GUILayout.Label("Visit my free zone. If you like this content, don't forget to leave a review :)", commonText);
        GUILayout.Space(6f);

        // Zielony przycisk
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.18f, 0.72f, 0.36f, 1f);
        if (GUILayout.Button("FREE ZONE!", GUILayout.Height(28)))
            Application.OpenURL("https://assetstore.unity.com/lists/free-zone-178789");
        GUI.backgroundColor = prevBg;

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(16f);

        // Dolny obrazek (baner)
        if (GUILayout.Button(image1, textureButton))
            Application.OpenURL("https://assetstore.unity.com/packages/3d/environments/urban/stylized-fantasy-graveyard-huuuge-world-144129");
    }

    private void DrawFooter()
    {
        // zostawione puste (FREE ZONE przeniesione wy¿ej)
    }
}

using UnityEditor;
using UnityEngine;

namespace CodyDreams
{
    namespace Developer
    {
        
        public class FeedbackWindowLPSN : EditorWindow
        {
            private const string AssetStoreFeedbackUrl = "https://assetstore.unity.com/packages/3d/environments/low-poly-stylized-nature-281338"; // Replace with your asset store URL
            private const float WindowWidth = 600f;  // Fixed width
            private const float WindowHeight = 300f; // Fixed height
            private static FeedbackWindowLPSN window;
            private static bool showOnStartup;
            
 
            private void OnEnable()
            {
                // Ensure the preference is up-to-date
                showOnStartup = EditorPrefs.GetBool("ShowFeedbackWindowOnStartupLPSN", true);
            }
            [InitializeOnLoadMethod]
            public static void LoadOnCall()
            {
                if(EditorPrefs.GetBool("ShowFeedbackWindowOnStartupLPSN", true))
                ShowWindow();
            }
            
            [MenuItem("Window/Feedback Window/Low poly stylized nature")]
            public static void ShowWindow()
            {
                if (window == null )
                {
                    window = GetWindow<FeedbackWindowLPSN>("Feedback Low poly stylized nature Asset pack");
                    window.minSize = new Vector2(WindowWidth, WindowHeight);
                    window.maxSize = new Vector2(WindowWidth, WindowHeight);
                }
                else
                {
                    FocusWindowIfItsOpen<FeedbackWindowLPSN>();
                    window.minSize = new Vector2(WindowWidth, WindowHeight);
                    window.maxSize = new Vector2(WindowWidth, WindowHeight);
                }
 
            }

            private void OnGUI()
            {
                // Centering the content both vertically and horizontally
                EditorGUILayout.BeginVertical(GUILayout.Width(WindowWidth), GUILayout.Height(WindowHeight));
                GUILayout.FlexibleSpace(); // Push content to center vertically

                // Centering the label and button
                GUILayout.Label("We'd love your feedback!", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                GUILayout.Label("Please take a moment to provide feedback on this asset.", GUILayout.ExpandWidth(true));

                if (GUILayout.Button("Give Feedback", GUILayout.ExpandWidth(true)))
                {
                    OpenFeedbackUrl();
                }

                GUILayout.Space(10); // Space before the toggle

                // Display and update the toggle for showing the window on startup
                bool newShowOnStartup = EditorGUILayout.Toggle("Show this window again", showOnStartup, GUILayout.ExpandWidth(true));
                if (newShowOnStartup != showOnStartup)
                {
                    // Save the new state if it has changed
                    showOnStartup = newShowOnStartup;
                    EditorPrefs.SetBool("ShowFeedbackWindowOnStartupLPSN", showOnStartup);
                }

                GUILayout.FlexibleSpace(); // Push content to center vertically
                EditorGUILayout.EndVertical();
            }

            private void OpenFeedbackUrl()
            {
                Application.OpenURL(AssetStoreFeedbackUrl);
            }

            private void OnDestroy()
            {
                // Save the preference when the window is closed
                EditorPrefs.SetBool("ShowFeedbackWindowOnStartupLPSN", showOnStartup);
            }
        }
    }
}

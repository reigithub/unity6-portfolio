using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class LargeFileSizeWindow : EditorWindow
    {
        [MenuItem("Project/EditorWindow/LargeFileSizeWindow")]
        private static void Open()
        {
            var window = GetWindow<LargeFileSizeWindow>();
            window.titleContent = new GUIContent("ファイルサイズ上限チェック");
            window.Show();
        }

        private const string DefaultDirectory = @"E:\UnityProjects\Unity6Sample\Assets\StoreAssets";
        private const string DefaultMaxFileSize = "100";
        private const int Mb = 1024 * 1024;
        private string _directory;
        private string _maxFileSize;
        private Vector2 _scrollPosition;
        private FileInfo[] _largeFiles = Array.Empty<FileInfo>();

        private void Initialize()
        {
            FileSize();
        }

        private void OnGUI()
        {
            Initialize();

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(560)))
            {
                EditorGUILayout.LabelField(_directory ??= DefaultDirectory);

                if (GUILayout.Button("フォルダを選択"))
                {
                    _directory = EditorUtility.OpenFolderPanel("Select Folder", "", "");
                }
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(560)))
            {
                EditorGUILayout.LabelField("ファイルサイズ上限: ");

                _maxFileSize = EditorGUILayout.TextField(_maxFileSize ??= DefaultMaxFileSize);

                EditorGUILayout.LabelField("MB");
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("ファイルサイズを取得"))
            {
                FileSize();
                Repaint();
            }

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledScope(_largeFiles.Length > 0))
            {
                if (_largeFiles.Length <= 0)
                {
                    EditorGUILayout.LabelField($"Large File NotFound. FileSize(Max): {_maxFileSize}MB.");
                }
            }

            EditorGUILayout.Space(10);

            GUILayout.Label("Large Files:");

            using (new EditorGUI.DisabledScope(_largeFiles.Length <= 0))
            {
                if (_largeFiles.Length > 0)
                {
                    using (var scroll = new GUILayout.ScrollViewScope(_scrollPosition, "box"))
                    {
                        _scrollPosition = scroll.scrollPosition;

                        foreach (var file in _largeFiles)
                        {
                            double size = Math.Round(file.Length / (float)Mb, 2);
                            // Debug.LogError($"{sizeInMB} MB - {file.FullName}");
                            // Console.WriteLine($"{sizeInMB} MB - {file.FullName}");
                            EditorGUILayout.SelectableLabel($"FileSize: {size}MB, MaxSize: {_maxFileSize}, Path: {file.FullName}");
                        }
                    }
                }
            }
        }

        private void FileSize()
        {
            // 検索対象のルートディレクトリを指定します
            string rootDirectory = _directory ??= DefaultDirectory;
            var maxFileSize = _maxFileSize ??= DefaultMaxFileSize;
            long sizeThresholdBytes = 100 * Mb;
            if (int.TryParse(maxFileSize, out var maxFileSizeInt))
            {
                sizeThresholdBytes = maxFileSizeInt * Mb;
            }

            // Debug.LogError($"Searching for files larger than 100MB in: {rootDirectory}");
            // Console.WriteLine($"Searching for files larger than 100MB in: {rootDirectory}");

            try
            {
                // EnumerateFilesを使用して再帰的にファイルを取得し、Linqでフィルタリング
                _largeFiles = Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(file => new FileInfo(file))
                    .Where(fileInfo => fileInfo.Length >= sizeThresholdBytes)
                    .OrderByDescending(fileInfo => fileInfo.Length)
                    .ToArray();

                // Debug.LogError($"Found {largeFiles.Length} large files:");
                // Console.WriteLine($"Found {largeFiles.Length} large files:");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogError($"Access denied to some paths. Error: {ex.Message}");
                // Console.WriteLine($"Access denied to some paths. Error: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                Debug.LogError($"Directory not found. Error: {ex.Message}");
                // Console.WriteLine($"Directory not found. Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred: {ex.Message}");
                // Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}
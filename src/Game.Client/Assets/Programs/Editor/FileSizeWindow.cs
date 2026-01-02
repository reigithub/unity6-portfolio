using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class FileSizeWindow : EditorWindow
    {
        [MenuItem("Project/EditorWindow/FileSizeWindow")]
        private static void Open()
        {
            var window = GetWindow<FileSizeWindow>();
            window.titleContent = new GUIContent("ファイルサイズ");
            window.Show();
        }

        private Vector2 _scrollPosition;

        private void Initialize()
        {
        }

        private void OnGUI()
        {
            Initialize();
            using (var scroll = new GUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;

                if (GUILayout.Button("ファイルサイズを取得"))
                {
                    FileSize();
                }
            }
        }

        private void FileSize()
        {
            // 検索対象のルートディレクトリを指定します
            string rootDirectory = @"E:\UnityProjects\Unity6Sample\Assets\StoreAssets";
            long sizeThresholdBytes = 100 * 1024 * 1024; // 100 MB をバイトに変換

            Debug.LogError($"Searching for files larger than 100MB in: {rootDirectory}");
            // Console.WriteLine($"Searching for files larger than 100MB in: {rootDirectory}");

            try
            {
                // EnumerateFilesを使用して再帰的にファイルを取得し、Linqでフィルタリング
                var largeFiles = Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
                    .Select(file => new FileInfo(file))
                    .Where(fileInfo => fileInfo.Length >= sizeThresholdBytes)
                    .OrderByDescending(fileInfo => fileInfo.Length)
                    .ToArray();

                Debug.LogError($"Found {largeFiles.Length} large files:");
                // Console.WriteLine($"Found {largeFiles.Length} large files:");

                foreach (var file in largeFiles)
                {
                    // バイトをMBに変換して表示
                    double sizeInMB = Math.Round(file.Length / (1024.0 * 1024.0), 2);
                    Debug.LogError($"{sizeInMB} MB - {file.FullName}");
                    // Console.WriteLine($"{sizeInMB} MB - {file.FullName}");
                }
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
            finally
            {
                // using (new EditorGUILayout.HorizontalScope("box"))
                // {
                //     EditorGUILayout.SelectableLabel(style.name);
                //     GUILayout.Space(10);
                //     EditorGUILayout.LabelField(style.name, style, GUILayout.ExpandWidth(true));
                // }
            }
        }
    }
}
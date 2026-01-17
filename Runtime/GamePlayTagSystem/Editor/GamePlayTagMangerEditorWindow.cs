using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Skddkkkk.DevelopKit.Framework.GamePlayTagSystem.Editor
{
    public class GamePlayTagMangerEditorWindow : EditorWindow
    {
        private List<string> _tags = new();
        private string _enumFilePath = "Assets/GamePlayTagSystem/Code/Runtime/GamePlayTagEnum.cs";

        [MenuItem("Tools/GamePlay Tag Enum Editor")]
        public static void ShowWindow()
        {
            GamePlayTagMangerEditorWindow window = GetWindow<GamePlayTagMangerEditorWindow>("GamePlay Tag Editor");
            window.Show();
        }

        private void OnEnable()
        {
            LoadEnum();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("GamePlay Tags (Enum)", EditorStyles.boldLabel);

            for (int i = 0; i < _tags.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _tags[i] = EditorGUILayout.TextField(_tags[i]);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    _tags.RemoveAt(i);
                    EditorGUI.FocusTextInControl("");
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Tag"))
            {
                _tags.Add("NewTag");
                EditorGUI.FocusTextInControl("");
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Save Enum File"))
            {
                SaveEnum();
            }
        }

        private void LoadEnum()
        {
            _tags.Clear();

            if (!File.Exists(_enumFilePath))
            {
                Debug.LogError("Enum file not found at path: " + _enumFilePath);
                return;
            }

            string[] lines = File.ReadAllLines(_enumFilePath);

            bool inEnum = false;
            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("public enum") || trimmed.StartsWith("enum"))
                {
                    inEnum = true;
                    continue;
                }

                if (inEnum)
                {
                    if (trimmed.StartsWith("}"))
                        break;

                    // 태그만 추출 (줄 끝에 ,나 =값이 붙을 수 있음)
                    var match = Regex.Match(trimmed, @"^([A-Za-z_][A-Za-z0-9_]*)");
                    if (match.Success)
                    {
                        if (match.Groups[1].Value == "None")
                            continue;

                        _tags.Add(match.Groups[1].Value);
                    }
                }
            }
        }

        private void SaveEnum()
        {
            using StreamWriter writer = new StreamWriter(_enumFilePath);
            writer.WriteLine("using System;");
            writer.WriteLine();
            writer.WriteLine("namespace Skddkkkk.DevelopKit.Framework.GamePlayTagSystem.Runtime");
            writer.WriteLine("{");
            writer.WriteLine("    public enum GamePlayTagEnum");
            writer.WriteLine("    {");
            writer.WriteLine($"        None = 0,");

            for (int i = 0; i < _tags.Count; i++)
            {
                string tag = _tags[i].Trim();

                // 비어 있거나 유효하지 않은 이름이면 무시
                if (string.IsNullOrEmpty(tag) || !Regex.IsMatch(tag, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                    continue;

                string value = $"1 << {i}";
                string comma = (i < _tags.Count - 1) ? "," : "";

                writer.WriteLine($"        {tag} = {value}{comma}");
            }

            writer.WriteLine("    }");
            writer.WriteLine("}");

            writer.Flush();
            AssetDatabase.Refresh();

            Debug.Log("Enum saved and refreshed.");
        }
    }
}
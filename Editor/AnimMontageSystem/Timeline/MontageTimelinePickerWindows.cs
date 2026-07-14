using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageObjectPickerWindow : EditorWindow
    {
        private const float RowHeight = 22f;
        private const double LoadBudgetSeconds = 0.006d;
        private const int MinPathsPerUpdate = 4;

        private readonly List<UnityEngine.Object> assets = new();
        private readonly List<UnityEngine.Object> filteredAssets = new();
        private readonly HashSet<UnityEngine.Object> seenAssets = new();
        private Type objectType;
        private Action<UnityEngine.Object> onPick;
        private Predicate<UnityEngine.Object> assetFilter;
        private string searchText = string.Empty;
        private string previousSearchText = string.Empty;
        private Vector2 scroll;
        private bool consumed;
        private string actionLabel = "Select";
        private UnityEngine.Object selectedAsset;
        private UnityEngine.Object previewAsset;
        private Editor previewEditor;
        private string[] pendingGuids = Array.Empty<string>();
        private int pendingGuidIndex;
        private bool assetSearchQueued;
        private bool isLoading;
        private bool filteredAssetsDirty = true;

        public static void Show<T>(string title, Action<T> onPick, Predicate<T> filter = null)
            where T : UnityEngine.Object
        {
            MontageObjectPickerWindow window = CreateInstance<MontageObjectPickerWindow>();
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(340f, 420f);
            window.position = new Rect(160f, 160f, 420f, 480f);
            window.actionLabel = title.StartsWith("Replace", StringComparison.OrdinalIgnoreCase)
                ? "Replace"
                : "Create";
            window.Initialize(typeof(T), picked =>
            {
                if (picked is T typed)
                    onPick?.Invoke(typed);
            }, filter != null ? asset => asset is T typed && filter(typed) : null);
            window.ShowAuxWindow();
            window.Focus();
        }

        private void Initialize(
            Type type,
            Action<UnityEngine.Object> pickCallback,
            Predicate<UnityEngine.Object> filter)
        {
            objectType = type;
            onPick = pickCallback;
            assetFilter = filter;
            assets.Clear();
            filteredAssets.Clear();
            seenAssets.Clear();
            pendingGuids = Array.Empty<string>();
            pendingGuidIndex = 0;
            assetSearchQueued = true;
            isLoading = true;
            filteredAssetsDirty = true;
            EditorApplication.update -= LoadAssetBatch;
            EditorApplication.update += LoadAssetBatch;
        }

        private void LoadAssetBatch()
        {
            if (this == null)
            {
                EditorApplication.update -= LoadAssetBatch;
                return;
            }

            if (assetSearchQueued)
            {
                pendingGuids = AssetDatabase.FindAssets($"t:{objectType.Name}");
                pendingGuidIndex = 0;
                assetSearchQueued = false;
            }

            double startTime = EditorApplication.timeSinceStartup;
            int processedPathCount = 0;
            while (pendingGuidIndex < pendingGuids.Length
                   && (processedPathCount < MinPathsPerUpdate
                       || EditorApplication.timeSinceStartup - startTime < LoadBudgetSeconds))
            {
                LoadAssetsFromGuid(pendingGuids[pendingGuidIndex]);
                pendingGuidIndex++;
                processedPathCount++;
            }

            if (pendingGuidIndex >= pendingGuids.Length)
            {
                EditorApplication.update -= LoadAssetBatch;
                isLoading = false;
                assets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                filteredAssetsDirty = true;
            }

            Repaint();
        }

        private void LoadAssetsFromGuid(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return;

            UnityEngine.Object[] loadedAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < loadedAssets.Length; i++)
            {
                UnityEngine.Object asset = loadedAssets[i];
                if (asset == null || !objectType.IsInstanceOfType(asset) || IsInternalPreviewAsset(asset))
                    continue;
                if (!seenAssets.Add(asset))
                    continue;
                if (assetFilter != null && !assetFilter(asset))
                    continue;

                assets.Add(asset);
                filteredAssetsDirty = true;
            }
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.5f)))
                {
                    DrawSearchField();
                    DrawAssetList();
                    DrawLoadingStatus();
                }

                using (new EditorGUILayout.VerticalScope())
                    DrawAssetPreview();
            }
        }

        private void DrawSearchField()
        {
            searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
            if (searchText == previousSearchText)
                return;

            previousSearchText = searchText;
            filteredAssetsDirty = true;
            scroll = Vector2.zero;
        }

        private void DrawAssetList()
        {
            RebuildFilteredAssetsIfNeeded();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            int count = filteredAssets.Count;
            int firstIndex = Mathf.Clamp(Mathf.FloorToInt(scroll.y / RowHeight), 0, Mathf.Max(0, count - 1));
            int visibleCount = Mathf.CeilToInt(Mathf.Max(80f, position.height - 78f) / RowHeight) + 4;
            int lastIndex = Mathf.Min(count, firstIndex + visibleCount);

            GUILayout.Space(firstIndex * RowHeight);
            for (int i = firstIndex; i < lastIndex; i++)
            {
                UnityEngine.Object asset = filteredAssets[i];
                if (asset == null)
                {
                    filteredAssetsDirty = true;
                    continue;
                }

                DrawAssetRow(asset);
            }

            GUILayout.Space(Mathf.Max(0, count - lastIndex) * RowHeight);
            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetRow(UnityEngine.Object asset)
        {
            GUIContent content = new(asset.name, AssetDatabase.GetAssetPath(asset));
            Rect rowRect = GUILayoutUtility.GetRect(content, EditorStyles.objectField, GUILayout.Height(RowHeight));
            bool selected = selectedAsset == asset;
            if (selected && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rowRect, new Color(0.28f, 0.48f, 0.78f, 0.38f));

            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && Event.current.clickCount == 2
                && rowRect.Contains(Event.current.mousePosition))
            {
                SelectAsset(asset);
                Pick(asset);
                Event.current.Use();
                GUIUtility.ExitGUI();
            }

            if (GUI.Button(rowRect, content, EditorStyles.objectField))
                SelectAsset(asset);

            if (selected && Event.current.type == EventType.Repaint)
                GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);
        }

        private void DrawLoadingStatus()
        {
            string label = isLoading
                ? $"Loading clips... {Mathf.Min(pendingGuidIndex, pendingGuids.Length)}/{pendingGuids.Length}"
                : $"{filteredAssets.Count}/{assets.Count} clips";
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
        }

        private void RebuildFilteredAssetsIfNeeded()
        {
            if (!filteredAssetsDirty)
                return;

            filteredAssetsDirty = false;
            filteredAssets.Clear();
            for (int i = 0; i < assets.Count; i++)
            {
                UnityEngine.Object asset = assets[i];
                if (asset != null && MatchesSearch(asset))
                    filteredAssets.Add(asset);
            }
        }

        private bool MatchesSearch(UnityEngine.Object asset) =>
            string.IsNullOrWhiteSpace(searchText)
            || asset.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsInternalPreviewAsset(UnityEngine.Object asset) =>
            asset.name.IndexOf("__preview__", StringComparison.OrdinalIgnoreCase) >= 0;

        private void SelectAsset(UnityEngine.Object asset)
        {
            selectedAsset = asset;
            SetPreviewAsset(asset);
        }

        private void SetPreviewAsset(UnityEngine.Object asset)
        {
            if (previewAsset == asset)
                return;

            DestroyPreviewEditor();
            previewAsset = asset;
            if (previewAsset != null)
                previewEditor = Editor.CreateEditor(previewAsset);
        }

        private void DrawAssetPreview()
        {
            GUILayout.Label(previewAsset != null ? previewAsset.name : "Preview", EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetRect(
                160f,
                Mathf.Max(120f, position.height - 112f),
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            if (previewEditor != null && previewEditor.HasPreviewGUI())
            {
                previewEditor.OnInteractivePreviewGUI(previewRect, GUIStyle.none);
                Repaint();
            }
            else
            {
                Texture2D preview = previewAsset != null ? AssetPreview.GetAssetPreview(previewAsset) : null;
                if (preview == null && previewAsset != null)
                    preview = AssetPreview.GetMiniThumbnail(previewAsset);

                if (preview != null)
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);
                else
                    EditorGUI.HelpBox(
                        previewRect,
                        isLoading ? "Loading animation clips..." : "Select an asset to preview it.",
                        MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(selectedAsset == null || consumed))
                {
                    if (GUILayout.Button(actionLabel, GUILayout.Width(96f), GUILayout.Height(24f)))
                        Pick(selectedAsset);
                }
            }
        }

        private void Pick(UnityEngine.Object asset)
        {
            if (consumed)
                return;

            consumed = true;
            onPick?.Invoke(asset);
            Close();
        }

        private void OnDisable()
        {
            EditorApplication.update -= LoadAssetBatch;
            DestroyPreviewEditor();
        }

        private void DestroyPreviewEditor()
        {
            if (previewEditor == null)
                return;

            DestroyImmediate(previewEditor);
            previewEditor = null;
        }
    }

    internal sealed class MontageTypePickerWindow : EditorWindow
    {
        private readonly List<Type> types = new();
        private Action<Type> onPick;
        private string searchText = string.Empty;
        private Vector2 scroll;
        private bool consumed;
        private string actionLabel = "Create";
        private Type selectedType;

        public static void Show<T>(string title, Action<T> onPick, Func<Type, bool> typeFilter = null)
            where T : class
        {
            MontageTypePickerWindow window = CreateInstance<MontageTypePickerWindow>();
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(300f, 360f);
            window.position = new Rect(180f, 180f, 340f, 420f);
            window.actionLabel = title.StartsWith("Replace", StringComparison.OrdinalIgnoreCase)
                ? "Replace"
                : "Create";
            window.Initialize(typeof(T), picked =>
            {
                if (Activator.CreateInstance(picked) is T instance)
                    onPick?.Invoke(instance);
            }, typeFilter);
            window.ShowAuxWindow();
            window.Focus();
        }

        private void Initialize(Type baseType, Action<Type> pickCallback, Func<Type, bool> typeFilter)
        {
            onPick = pickCallback;
            types.Clear();
            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (type.IsAbstract || type.IsGenericType || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                if (typeFilter != null && !typeFilter(type))
                    continue;

                types.Add(type);
            }

            types.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        private void OnGUI()
        {
            searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                if (!MatchesSearch(type))
                    continue;

                GUIContent content = new(type.Name, type.FullName);
                Rect rowRect = GUILayoutUtility.GetRect(content, EditorStyles.objectField, GUILayout.Height(20f));
                bool selected = selectedType == type;
                if (selected && Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rowRect, new Color(0.28f, 0.48f, 0.78f, 0.38f));

                if (Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && Event.current.clickCount == 2
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    selectedType = type;
                    Pick(type);
                    Event.current.Use();
                    GUIUtility.ExitGUI();
                }

                if (GUI.Button(rowRect, content, EditorStyles.objectField))
                    selectedType = type;

                if (selected && Event.current.type == EventType.Repaint)
                    GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);
            }

            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(selectedType == null || consumed))
                {
                    if (GUILayout.Button(actionLabel, GUILayout.Width(96f), GUILayout.Height(24f)))
                        Pick(selectedType);
                }
            }
        }

        private bool MatchesSearch(Type type) =>
            string.IsNullOrWhiteSpace(searchText)
            || type.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
            || type.FullName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

        private void Pick(Type type)
        {
            if (consumed)
                return;

            consumed = true;
            onPick?.Invoke(type);
            Close();
        }
    }
}

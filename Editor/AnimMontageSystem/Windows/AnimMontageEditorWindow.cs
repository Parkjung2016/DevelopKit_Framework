using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class AnimMontageEditorWindow : EditorWindow
    {
        private MontageEditorContext context;
        private MontageUnifiedPreviewController previewController;
        private MontageTimelineView timelineView;
        private MontagePreviewViewportPanel viewportPanel;
        private MontageTransportBar transportBar;
        private MontageSelectionInspectorPanel inspectorPanel;
        private ObjectField montageField;
        private ObjectField montageLibraryField;
        private ObjectField previewModelField;
        private Label statusLabel;
        private double lastEditorTime;
        private bool animationModeStarted;
        private AnimationModeDriver previewDriver;
        private readonly MontagePlaybackState editorNotifyPlayback = new();
        private readonly MontageNotifyDispatcher editorNotifyDispatcher = new();
        private readonly List<AnimNotifyPlacement> editorScrubNotifyBuffer = new();
        private readonly List<AnimNotifyStatePlacement> editorScrubBeginBuffer = new();
        private readonly List<AnimNotifyStatePlacement> editorScrubEndBuffer = new();
        private readonly List<AnimNotifyStatePlacement> editorScrubTickBuffer = new();
        private bool editorNotifyPlaybackActive;
        private bool suppressEditorScrubNotify;
        private GameObject editorNotifyFallbackOwner;

        [MenuItem("PJDev/Animation/Montage Editor", priority = -9750)]
        public static void Open()
        {
            var window = GetWindow<AnimMontageEditorWindow>();
            window.titleContent = new GUIContent("Montage Editor");
            window.minSize = new Vector2(1200, 680);
            window.Show();
        }

        public static void Open(AnimMontageSO montage)
        {
            var window = GetWindow<AnimMontageEditorWindow>();
            window.titleContent = new GUIContent("Montage Editor");
            window.minSize = new Vector2(1200, 680);
            window.Show();
            window.SetMontageWithoutEditorScrubNotify(montage);
            if (window.montageField != null)
                window.montageField.SetValueWithoutNotify(montage);
        }

        public static void Open(AnimMontageLibrarySO library)
        {
            var window = GetWindow<AnimMontageEditorWindow>();
            window.titleContent = new GUIContent("Montage Editor");
            window.minSize = new Vector2(1200, 680);
            window.Show();
            window.context ??= new MontageEditorContext();
            window.context.SetMontageLibrary(library);
            if (window.montageLibraryField != null)
                window.montageLibraryField.SetValueWithoutNotify(library);
            if (window.previewModelField != null)
                window.previewModelField.SetValueWithoutNotify(library != null ? library.PreviewModel : null);
        }
        private void OnEnable()
        {
            context ??= new MontageEditorContext();
            previewController ??= new MontageUnifiedPreviewController();
            previewController.Bind(context);
            RestorePreviewModelIfNeeded();
            StartPreviewAnimationMode();
            lastEditorTime = EditorApplication.timeSinceStartup;
            MontageViewportInput.SetPlaybackToggleHandler(TryTogglePlaybackShortcut);
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            EndEditorNotifyPlayback();
            context?.SetPlaying(false);
            previewController?.HandlePlayModeStateChanged(state);
            inspectorPanel?.RefreshPlayModeReadonly();
            transportBar?.Refresh();
            RequestPreviewRepaint();
        }
        internal void HandleBeforeAssemblyReload()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EndEditorNotifyPlayback();
            StopPreviewAnimationMode(force: true);
            previewController?.Dispose();
            previewController = null;
            DestroyEditorNotifyFallbackOwner();
        }

        internal Object GetPreferredSelectionForReload() => context?.Montage;

        private void RestorePreviewModelIfNeeded()
        {
            GameObject previewPrefab = context?.PreviewModel;
            if (previewPrefab == null && previewModelField?.value is GameObject fieldPrefab)
                previewPrefab = fieldPrefab;

            if (previewPrefab == null)
                return;

            context.SetPreviewModel(previewPrefab);
            previewController.SetPreviewModel(previewPrefab);
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            MontageViewportInput.SetPlaybackToggleHandler(null);
            EndEditorNotifyPlayback();
            StopPreviewAnimationMode(force: true);
            previewController?.Dispose();
            previewController = null;
            DestroyEditorNotifyFallbackOwner();
        }

        private void CreateGUI()
        {
            context ??= new MontageEditorContext();
            previewController ??= new MontageUnifiedPreviewController();
            previewController.Bind(context);
            RestorePreviewModelIfNeeded();
            StartPreviewAnimationMode();

            VisualElement root = rootVisualElement;
            root.AddToClassList(AnimMontageEditorStyles.WindowRootClass);
            AnimMontageEditorStyleSheet.Apply(root);
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;
            root.style.minHeight = 0;
            root.style.minWidth = 0;
            root.style.overflow = Overflow.Hidden;
            BuildToolbar(root);
            BuildMainLayout(root);

            statusLabel = new Label("Ready");
            statusLabel.AddToClassList(AnimMontageEditorStyles.StatusBarClass);
            root.Add(statusLabel);

            rootVisualElement.focusable = true;
            RegisterPlaybackShortcutHandler(rootVisualElement);

            context.Changed -= UpdateStatus;
            context.MontageChanged -= OnMontageChanged;
            context.PlayheadChanged -= OnPlayheadChanged;
            context.SelectionChanged -= UpdateStatus;
            context.Changed += UpdateStatus;
            context.MontageChanged += OnMontageChanged;
            context.PlayheadChanged += OnPlayheadChanged;
            context.SelectionChanged += UpdateStatus;
            MontageViewportInput.SetPlaybackToggleHandler(TryTogglePlaybackShortcut);
            UpdateStatus();
            RefreshPlayheadViewWithoutEditorScrubNotify();
        }

        private void BuildMainLayout(VisualElement root)
        {
            var browserSplit = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal);
            MontageEditorLayoutHelper.ConfigureSplit(browserSplit, "am-split-browser");
            root.Add(browserSplit);

            var browserPanel = new MontageAssetBrowserPanel(context, CreateMontageAsset, CreateMontageLibraryAsset);
            MontageEditorLayoutHelper.ConfigurePane(browserPanel);
            RegisterPlaybackShortcutHandler(browserPanel);
            browserSplit.Add(browserPanel);

            var centerInspectorSplit = new TwoPaneSplitView(1, 320, TwoPaneSplitViewOrientation.Horizontal);
            MontageEditorLayoutHelper.ConfigureSplit(centerInspectorSplit, "am-split-inspector");
            browserSplit.Add(centerInspectorSplit);

            var previewTimelineSplit = new TwoPaneSplitView(1, 280, TwoPaneSplitViewOrientation.Vertical);
            MontageEditorLayoutHelper.ConfigureSplit(previewTimelineSplit, "am-split-timeline");
            centerInspectorSplit.Add(previewTimelineSplit);

            viewportPanel = new MontagePreviewViewportPanel((rect, requestRepaint) =>
                previewController?.DrawPreview(rect, requestRepaint));
            MontageEditorLayoutHelper.ConfigurePane(viewportPanel);
            RegisterPlaybackShortcutHandler(viewportPanel);
            previewTimelineSplit.Add(viewportPanel);

            timelineView = new MontageTimelineView(context);
            MontageEditorLayoutHelper.ConfigurePane(timelineView);

            var timelinePanel = new VisualElement();
            MontageEditorLayoutHelper.ConfigurePane(timelinePanel);
            timelinePanel.style.flexDirection = FlexDirection.Column;
            timelinePanel.focusable = true;

            RegisterPreviewFocusRelease(timelinePanel);
            RegisterPreviewFocusRelease(timelineView);

            transportBar = new MontageTransportBar(
                context,
                TogglePlayPause,
                StopPlayback,
                () => timelineView?.SplitSelectedSegmentAtPlayhead(),
                () => timelineView?.ReplaceSelectedSegmentClip(),
                () => timelineView?.ResetSelectedSegmentTrim(),
                () => timelineView?.HasSelectedSegment() == true,
                () => timelineView?.CanSplitSelectedSegmentAtPlayhead() == true);
            RegisterPlaybackShortcutHandler(transportBar);
            RegisterPreviewFocusRelease(transportBar);
            timelinePanel.Add(transportBar);
            timelinePanel.Add(timelineView);
            RegisterPlaybackShortcutHandler(timelinePanel);
            RegisterPlaybackShortcutHandler(timelineView);

            previewTimelineSplit.Add(timelinePanel);

            var inspectorLogSplit = new TwoPaneSplitView(1, 220, TwoPaneSplitViewOrientation.Vertical);
            MontageEditorLayoutHelper.ConfigureSplit(inspectorLogSplit, "am-split-log-viewer");
            centerInspectorSplit.Add(inspectorLogSplit);

            inspectorPanel = new MontageSelectionInspectorPanel(context);
            MontageEditorLayoutHelper.ConfigurePane(inspectorPanel);
            RegisterPlaybackShortcutHandler(inspectorPanel);
            inspectorLogSplit.Add(inspectorPanel);

            var logViewerPanel = new MontageLogViewerPanel();
            MontageEditorLayoutHelper.ConfigurePane(logViewerPanel);
            RegisterPlaybackShortcutHandler(logViewerPanel);
            inspectorLogSplit.Add(logViewerPanel);
        }

        private static void RegisterPreviewFocusRelease(VisualElement element)
        {
            element.RegisterCallback<PointerEnterEvent>(_ => MontageViewportInput.CancelInteraction(), TrickleDown.TrickleDown);
            element.RegisterCallback<PointerMoveEvent>(_ => MontageViewportInput.CancelInteraction(), TrickleDown.TrickleDown);
            element.RegisterCallback<FocusInEvent>(_ => MontageViewportInput.CancelInteraction(), TrickleDown.TrickleDown);
        }
        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList(AnimMontageEditorStyles.ToolbarClass);

            var assetGroup = new VisualElement();
            assetGroup.AddToClassList(AnimMontageEditorStyles.AssetToolbarGroupClass);

            var createMenu = new ToolbarMenu { text = "Create" };
            createMenu.style.flexShrink = 0;
            createMenu.menu.AppendAction("Montage", _ => CreateMontageAsset());
            createMenu.menu.AppendAction("Montage Library", _ => CreateMontageLibraryAsset());
            assetGroup.Add(createMenu);

            montageLibraryField = new ObjectField("Montage Library")
            {
                objectType = typeof(AnimMontageLibrarySO),
                allowSceneObjects = false
            };
            montageLibraryField.style.width = 260;
            montageLibraryField.style.flexShrink = 0;
            montageLibraryField.RegisterValueChangedCallback(evt =>
            {
                bool wasPlaying = context.IsPlaying;
                EndEditorNotifyPlayback();
                context.SetMontageLibrary(evt.newValue as AnimMontageLibrarySO);
                previewModelField?.SetValueWithoutNotify(context.PreviewModel);
                previewController?.SetPreviewModel(context.PreviewModel);
                if (wasPlaying)
                    BeginEditorNotifyPlayback();
                RequestPreviewRepaint();
                RefreshPlayheadViewWithoutEditorScrubNotify();
            });
            assetGroup.Add(montageLibraryField);

            previewModelField = new ObjectField("Preview Mesh")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            previewModelField.style.width = 220;
            previewModelField.style.flexShrink = 0;
            previewModelField.RegisterValueChangedCallback(evt =>
            {
                bool wasPlaying = context.IsPlaying;
                EndEditorNotifyPlayback();
                context.SetPreviewModel(evt.newValue as GameObject);
                SetCurrentLibraryPreviewModel(context.PreviewModel);
                previewController?.SetPreviewModel(context.PreviewModel);
                if (wasPlaying)
                    BeginEditorNotifyPlayback();
                RequestPreviewRepaint();
                RefreshPlayheadViewWithoutEditorScrubNotify();
            });
            assetGroup.Add(previewModelField);

            toolbar.Add(assetGroup);
            root.Add(toolbar);
        }

        private void CreateMontageAsset()
        {
            string path = ChooseAssetCreationPath(
                "Create Montage",
                "Montage_New",
                "Choose where to create the montage asset.");
            if (string.IsNullOrEmpty(path))
                return;

            var montage = CreateInstance<AnimMontageSO>();
            AssetDatabase.CreateAsset(montage, path);
            AddMontageToCurrentLibrary(montage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(montage);
            Selection.activeObject = montage;
            montageField?.SetValueWithoutNotify(montage);
            SetMontageWithoutEditorScrubNotify(montage);
            UpdateStatus();
            RefreshPlayheadViewWithoutEditorScrubNotify();
        }

        private void CreateMontageLibraryAsset()
        {
            string path = ChooseAssetCreationPath(
                "Create Montage Library",
                "MontageLibrary_New",
                "Choose where to create the montage library asset.");
            if (string.IsNullOrEmpty(path))
                return;

            var library = CreateInstance<AnimMontageLibrarySO>();
            AssetDatabase.CreateAsset(library, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(library);
            Selection.activeObject = library;
            montageLibraryField?.SetValueWithoutNotify(library);
            context.SetMontageLibrary(library);
            previewModelField?.SetValueWithoutNotify(context.PreviewModel);
            previewController?.SetPreviewModel(context.PreviewModel);
            UpdateStatus();
            RefreshPlayheadViewWithoutEditorScrubNotify();
        }

        private static string ChooseAssetCreationPath(string title, string defaultName, string message)
        {
            string directory = GetSelectedProjectDirectory();
            string path = EditorUtility.SaveFilePanelInProject(
                title,
                defaultName,
                "asset",
                message,
                directory);
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/"))
            {
                EditorUtility.DisplayDialog(
                    title,
                    "Assets must be created inside the Assets folder.",
                    "OK");
                return string.Empty;
            }

            return AssetDatabase.GenerateUniqueAssetPath(path);
        }

        private void AddMontageToCurrentLibrary(AnimMontageSO montage)
        {
            AnimMontageLibrarySO library = context?.MontageLibrary;
            if (library == null || montage == null || library.Contains(montage))
                return;

            Undo.RecordObject(library, "Add Montage To Library");
            SerializedObject so = new(library);
            SerializedProperty montages = so.FindProperty("montages");
            int index = montages.arraySize;
            montages.InsertArrayElementAtIndex(index);
            montages.GetArrayElementAtIndex(index).objectReferenceValue = montage;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
        }

        private void SetCurrentLibraryPreviewModel(GameObject previewModel)
        {
            AnimMontageLibrarySO library = context?.MontageLibrary;
            if (library == null)
                return;

            SerializedObject so = new(library);
            SerializedProperty property = so.FindProperty("previewModel");
            if (property == null || property.objectReferenceValue == previewModel)
                return;

            Undo.RecordObject(library, "Set Montage Library Preview Model");
            property.objectReferenceValue = previewModel;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
        }

        private static string GetSelectedProjectDirectory()
        {
            Object selected = Selection.activeObject;
            if (selected == null)
                return "Assets";

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
                return "Assets";

            if (AssetDatabase.IsValidFolder(path))
                return path;

            int slashIndex = path.LastIndexOf('/');
            return slashIndex > 0 ? path[..slashIndex] : "Assets";
        }

        private void RegisterPlaybackShortcutHandler(VisualElement element)
        {
            element.RegisterCallback<KeyDownEvent>(OnPlaybackKeyDown, TrickleDown.TrickleDown);
        }

        private void OnPlaybackKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Space || IsShortcutInputBlocked())
                return;

            if (MontageViewportInput.TryInvokePlaybackToggle())
                evt.StopImmediatePropagation();
        }

        private void OnFocus() => rootVisualElement?.Focus();

        private void OnUndoRedoPerformed()
        {
            EndEditorNotifyPlayback();
            context?.NotifyUndoRedo();
            UpdateStatus();
            transportBar?.Refresh();
            timelineView?.MarkDirtyRepaint();
            RequestPreviewRepaint();
            RefreshPlayheadViewWithoutEditorScrubNotify();
        }

        private bool IsShortcutInputBlocked()
        {
            if (EditorGUIUtility.editingTextField)
                return true;

            for (VisualElement focused = rootVisualElement.panel?.focusController?.focusedElement as VisualElement;
                 focused != null;
                 focused = focused.parent)
            {
                if (focused is TextField or FloatField or IntegerField or DoubleField or LongField)
                    return true;

                string typeName = focused.GetType().Name;
                if (typeName.Contains("TextInput") || typeName.Contains("SearchField"))
                    return true;
            }

            return false;
        }

        private void StartPreviewAnimationMode()
        {
            if (animationModeStarted)
                return;

            if (previewDriver == null)
                previewDriver = ScriptableObject.CreateInstance<AnimationModeDriver>();

            if (!AnimationMode.InAnimationMode(previewDriver))
                AnimationMode.StartAnimationMode(previewDriver);

            animationModeStarted = true;
        }

        private void StopPreviewAnimationMode(bool force = false)
        {
            if (!animationModeStarted && !force)
                return;

            try
            {
            if (previewDriver != null && AnimationMode.InAnimationMode(previewDriver))
                    AnimationMode.StopAnimationMode(previewDriver);
            }
            catch
            {
                // ignored during domain reload / assembly reload
            }

            if (previewDriver != null)
            {
                Object.DestroyImmediate(previewDriver);
                previewDriver = null;
            }

            animationModeStarted = false;
        }

        private void TogglePlayPause()
        {
            if (EditorApplication.isPlaying)
                return;

            if (context.Montage == null)
                return;

            bool shouldPlay = !context.IsPlaying;
            context.SetPlaying(shouldPlay);
            if (shouldPlay)
                BeginEditorNotifyPlayback();
            else
                EndEditorNotifyPlayback();

            transportBar?.Refresh();
            UpdateStatus();
        }

        private void StopPlayback()
        {
            EndEditorNotifyPlayback();
            context.SetPlaying(false);
            SetPlayheadWithoutEditorScrubNotify(0f);
            previewController?.ResetRootMotionPreviewPose();
            transportBar?.Refresh();
            UpdateStatus();
            RequestPreviewRepaint();
        }

        private void RequestPreviewRepaint() => viewportPanel?.RequestRepaint();

        private void OnMontageChanged()
        {
            EndEditorNotifyPlayback();
            previewController?.ResetRootMotionPreviewPose();
            transportBar?.Refresh();
            RequestPreviewRepaint();
        }

        private void PollPlaybackShortcut()
        {
            if (context == null || context.Montage == null)
                return;

            if (!IsEditorShortcutContext())
                return;

            if (IsShortcutInputBlocked())
                return;

            if (!MontageEditorKeyboardInput.WasSpacePressedThisFrame())
                return;

            MontageViewportInput.TryInvokePlaybackToggle();
        }

        private bool IsEditorShortcutContext()
        {
            if (EditorWindow.focusedWindow == this || EditorWindow.mouseOverWindow == this)
                return true;

            return MontageViewportInput.IsViewportEngaged;
        }

        private bool IsTimelineAreaFocused()
        {
            VisualElement focused = rootVisualElement?.panel?.focusController?.focusedElement as VisualElement;
            if (focused == null)
                return false;

            if (timelineView != null && (focused == timelineView || timelineView.Contains(focused)))
                return true;

            return transportBar != null && (focused == transportBar || transportBar.Contains(focused));
        }

        private bool TryTogglePlaybackShortcut()
        {
            if (EditorApplication.isPlaying)
                return false;

            if (context == null || context.Montage == null || IsShortcutInputBlocked())
                return false;

            TogglePlayPause();
            return true;
        }

        private void OnEditorUpdate()
        {
            PollPlaybackShortcut();

            if (EditorApplication.isPlaying && context != null && context.IsPlaying)
            {
                EndEditorNotifyPlayback();
                context.SetPlaying(false);
                inspectorPanel?.RefreshPlayModeReadonly();
                transportBar?.Refresh();
                UpdateStatus();
                RequestPreviewRepaint();
            }

            if (context == null || !context.IsPlaying || context.Montage == null)
            {
                lastEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - lastEditorTime);
            lastEditorTime = now;

            MontageTimelineElementEvaluation timelineEvaluation =
                MontageTimelineElementEvaluator.Evaluate(context.Montage, context.PlayheadTime);
            float length = context.Montage.Length;
            float nextTime = context.PlayheadTime
                + delta * context.EffectivePlaybackSpeed * timelineEvaluation.SpeedMultiplier;

            if (nextTime >= length)
            {
                if (context.Loop && length > 0f)
                {
                    AdvanceEditorNotifyPlayback(length);
                    EndEditorNotifyPlayback();
                    nextTime = 0f;
                    context.SetPlayhead(nextTime);
                    BeginEditorNotifyPlayback();
                    UpdateStatus();
                    RequestPreviewRepaint();
                    return;
                }
                else
                {
                    AdvanceEditorNotifyPlayback(length);
                    context.SetPlayhead(length);
                    EndEditorNotifyPlayback();
                    context.SetPlaying(false);
                    transportBar?.Refresh();
                    UpdateStatus();
                    RequestPreviewRepaint();
                    return;
                }
            }

            AdvanceEditorNotifyPlayback(nextTime);
            context.SetPlayhead(nextTime);
            UpdateStatus();
            RequestPreviewRepaint();
        }

        private void BeginEditorNotifyPlayback()
        {
            if (context?.Montage == null)
            {
                ResetEditorNotifyPlayback();
                return;
            }

            editorNotifyPlayback.Begin(context.Montage, context.PlayheadTime);
            editorNotifyDispatcher.Reset();
            editorNotifyPlaybackActive = true;
            DispatchEditorNotifyPlayback();
        }

        private void PauseEditorNotifyPlayback()
        {
            if (!editorNotifyPlaybackActive)
                return;

            editorNotifyPlayback.Pause(true);
        }

        private void EndEditorNotifyPlayback()
        {
            if (!editorNotifyPlaybackActive)
            {
                ResetEditorNotifyPlayback();
                return;
            }

            GameObject owner = GetEditorNotifyOwner();
            editorNotifyDispatcher.EndActiveStates(
                owner,
                previewController?.NotifyAnimator,
                editorNotifyPlayback.Montage ?? context?.Montage,
                editorNotifyPlayback.CurrentTime);
            ResetEditorNotifyPlayback();
        }

        private void ResyncEditorNotifyPlayback()
        {
            if (context == null || !context.IsPlaying)
            {
                ResetEditorNotifyPlayback();
                return;
            }

            BeginEditorNotifyPlayback();
        }

        private void ResetEditorNotifyPlayback()
        {
            editorNotifyPlayback.Stop();
            editorNotifyDispatcher.Reset();
            editorNotifyPlaybackActive = false;
        }

        private void AdvanceEditorNotifyPlayback(float nextTime)
        {
            if (context?.Montage == null)
                return;

            if (!editorNotifyPlaybackActive
                || editorNotifyPlayback.Montage != context.Montage
                || Mathf.Abs(editorNotifyPlayback.CurrentTime - context.PlayheadTime) > 0.001f)
            {
                BeginEditorNotifyPlayback();
            }

            editorNotifyPlayback.SetTime(nextTime);

            DispatchEditorNotifyPlayback();
        }

        private void DispatchEditorNotifyPlayback()
        {
            if (!editorNotifyPlaybackActive || editorNotifyPlayback.Montage == null)
                return;

            editorNotifyDispatcher.Dispatch(
                editorNotifyPlayback,
                GetEditorNotifyOwner(),
                previewController?.NotifyAnimator,
                null);
        }

        private GameObject GetEditorNotifyOwner()
        {
            GameObject previewOwner = previewController?.NotifyOwner;
            if (previewOwner != null)
                return previewOwner;

            if (editorNotifyFallbackOwner == null)
            {
                editorNotifyFallbackOwner = new GameObject("Montage Editor Notify Owner")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return editorNotifyFallbackOwner;
        }

        private void DestroyEditorNotifyFallbackOwner()
        {
            if (editorNotifyFallbackOwner == null)
                return;

            Object.DestroyImmediate(editorNotifyFallbackOwner);
            editorNotifyFallbackOwner = null;
        }

        private void SetMontageWithoutEditorScrubNotify(AnimMontageSO montage)
        {
            suppressEditorScrubNotify = true;
            try
            {
                context?.SetMontage(montage);
            }
            finally
            {
                suppressEditorScrubNotify = false;
            }
        }

        private void SetPlayheadWithoutEditorScrubNotify(float time)
        {
            suppressEditorScrubNotify = true;
            try
            {
                context?.SetPlayhead(time);
            }
            finally
            {
                suppressEditorScrubNotify = false;
            }
        }

        private void RefreshPlayheadViewWithoutEditorScrubNotify()
        {
            suppressEditorScrubNotify = true;
            try
            {
                OnPlayheadChanged();
            }
            finally
            {
                suppressEditorScrubNotify = false;
            }
        }

        private void OnPlayheadChanged()
        {
            DispatchEditorScrubNotifies();
            transportBar?.Refresh();
            previewController?.Sample(context);
            timelineView?.MarkDirtyRepaint();
            RequestPreviewRepaint();
        }

        private void DispatchEditorScrubNotifies()
        {
            if (suppressEditorScrubNotify || context == null || context.IsPlaying || context.Montage == null)
                return;

            AnimMontageSO montage = context.Montage;
            float previousTime = context.PreviousPlayheadTime;
            float currentTime = context.PlayheadTime;
            float deltaTime = currentTime - previousTime;
            GameObject owner = GetEditorNotifyOwner();
            Animator animator = previewController?.NotifyAnimator;

            MontageEvaluator.CollectNotifyEvents(montage, previousTime, currentTime, editorScrubNotifyBuffer);
            for (int i = 0; i < editorScrubNotifyBuffer.Count; i++)
            {
                AnimNotifyPlacement placement = editorScrubNotifyBuffer[i];
                AnimNotify notify = placement.Notify;
                if (notify == null || !notify.TriggerInEditorScrub)
                    continue;

                var notifyContext = new AnimNotifyContext(owner, animator, montage, placement.Time, deltaTime);
                notify.OnNotify(notifyContext);
            }

            MontageEvaluator.CollectNotifyStateTransitions(
                montage,
                previousTime,
                currentTime,
                editorScrubBeginBuffer,
                editorScrubEndBuffer,
                editorScrubTickBuffer);

            for (int i = 0; i < editorScrubEndBuffer.Count; i++)
            {
                AnimNotifyStatePlacement placement = editorScrubEndBuffer[i];
                AnimNotifyState state = placement.NotifyState;
                if (state == null || !state.TriggerInEditorScrub)
                    continue;

                var endContext = new AnimNotifyContext(owner, animator, montage, placement.EndTime, deltaTime);
                state.OnEnd(endContext);
            }

            for (int i = 0; i < editorScrubBeginBuffer.Count; i++)
            {
                AnimNotifyStatePlacement placement = editorScrubBeginBuffer[i];
                AnimNotifyState state = placement.NotifyState;
                if (state == null || !state.TriggerInEditorScrub)
                    continue;

                var beginContext = new AnimNotifyContext(owner, animator, montage, placement.StartTime, deltaTime);
                state.OnBegin(beginContext);
            }

            for (int i = 0; i < editorScrubTickBuffer.Count; i++)
            {
                AnimNotifyStatePlacement placement = editorScrubTickBuffer[i];
                AnimNotifyState state = placement.NotifyState;
                if (state == null || !state.TriggerInEditorScrub)
                    continue;

                var tickContext = new AnimNotifyContext(owner, animator, montage, currentTime, deltaTime);
                state.OnTick(tickContext, Mathf.Abs(deltaTime));
            }
        }

        private void UpdateStatus()
        {
            if (statusLabel == null || context == null)
                return;

            SyncToolbarFields();

            AnimMontageSO montage = context.Montage;
            if (montage == null)
            {
                statusLabel.text = "Select or create a montage asset.";
                transportBar?.Refresh();
                return;
            }

            montage.TryGetSegmentAtTime(context.PlayheadTime, out MontageSegment segment, out _);
            string section = segment != null ? segment.SectionName : "-";
            string clip = segment?.Clip != null ? segment.Clip.name : "-";
            string playState = context.IsPlaying ? "Playing" : "Paused";
            statusLabel.text =
                $"{playState} | Section: {section} | Clip: {clip} | Notifies: {montage.Notifies.Count} | States: {montage.NotifyStates.Count}";
        }

        private void SyncToolbarFields()
        {
            if (montageLibraryField != null && montageLibraryField.value != context.MontageLibrary)
                montageLibraryField.SetValueWithoutNotify(context.MontageLibrary);

            if (previewModelField != null && previewModelField.value != context.PreviewModel)
            {
                previewModelField.SetValueWithoutNotify(context.PreviewModel);
                previewController?.SetPreviewModel(context.PreviewModel);
                RequestPreviewRepaint();
            }
        }
    }
}
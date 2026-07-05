using PJDev.DevelopKit.Editors;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class AnimMontageEditorWindow : EditorWindow
    {
        private MontageEditorContext context;
        private MontageUnifiedPreviewController previewController;
        private MontageTimelineView timelineView;
        private MontagePreviewViewportPanel viewportPanel;
        private MontageTransportBar transportBar;
        private ObjectField montageField;
        private ObjectField previewModelField;
        private Label statusLabel;
        private double lastEditorTime;
        private bool animationModeStarted;
        private AnimationModeDriver previewDriver;

        [MenuItem("PJDev/Animation/Montage Editor", priority = PJDevMenuPriority.AnimMontage)]
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
            window.context?.SetMontage(montage);
            if (window.montageField != null)
                window.montageField.SetValueWithoutNotify(montage);
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
            EditorApplication.update += OnEditorUpdate;
        }

        internal void HandleBeforeAssemblyReload()
        {
            EditorApplication.update -= OnEditorUpdate;
            StopPreviewAnimationMode(force: true);
            previewController?.Dispose();
            previewController = null;
        }

        internal Object GetPreferredSelectionForReload() => context?.Montage;

        private void RestorePreviewModelIfNeeded()
        {
            GameObject previewPrefab = context?.PreviewModel;
            if (previewPrefab == null && previewModelField?.value is GameObject fieldPrefab)
                previewPrefab = fieldPrefab;

            if (previewPrefab == null)
                return;

            context.PreviewModel = previewPrefab;
            previewController.SetPreviewModel(previewPrefab);
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            MontageViewportInput.SetPlaybackToggleHandler(null);
            StopPreviewAnimationMode(force: true);
            previewController?.Dispose();
            previewController = null;
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

            RegisterPlaybackShortcutHandler(rootVisualElement);

            context.Changed += UpdateStatus;
            context.PlayheadChanged += OnPlayheadChanged;
            context.SelectionChanged += UpdateStatus;
            MontageViewportInput.SetPlaybackToggleHandler(TryTogglePlaybackShortcut);
            UpdateStatus();
            OnPlayheadChanged();
        }

        private void BuildMainLayout(VisualElement root)
        {
            var browserSplit = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal);
            MontageEditorLayoutHelper.ConfigureSplit(browserSplit, "am-split-browser");
            root.Add(browserSplit);

            var browserPanel = new MontageAssetBrowserPanel(context);
            MontageEditorLayoutHelper.ConfigurePane(browserPanel);
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
            previewTimelineSplit.Add(viewportPanel);

            timelineView = new MontageTimelineView(context);
            MontageEditorLayoutHelper.ConfigurePane(timelineView);

            var timelinePanel = new VisualElement();
            MontageEditorLayoutHelper.ConfigurePane(timelinePanel);
            timelinePanel.style.flexDirection = FlexDirection.Column;
            timelinePanel.focusable = true;

            transportBar = new MontageTransportBar(context, TogglePlayPause, StopPlayback);
            RegisterPlaybackShortcutHandler(transportBar);
            transportBar.RegisterCallback<PointerDownEvent>(_ =>
            {
                MontageViewportInput.CancelInteraction();
                root.Focus();
                transportBar.Focus();
            });
            timelinePanel.Add(transportBar);
            timelinePanel.Add(timelineView);
            timelineView.RegisterCallback<PointerDownEvent>(_ =>
            {
                MontageViewportInput.CancelInteraction();
                root.Focus();
                timelineView.Focus();
            });
            RegisterPlaybackShortcutHandler(timelinePanel);
            RegisterPlaybackShortcutHandler(timelineView);

            previewTimelineSplit.Add(timelinePanel);

            var inspectorPanel = new MontageSelectionInspectorPanel(context);
            MontageEditorLayoutHelper.ConfigurePane(inspectorPanel);
            centerInspectorSplit.Add(inspectorPanel);
        }

        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList(AnimMontageEditorStyles.ToolbarClass);

            var assetGroup = new VisualElement();
            assetGroup.AddToClassList(AnimMontageEditorStyles.AssetToolbarGroupClass);

            montageField = new ObjectField("Montage")
            {
                objectType = typeof(AnimMontageSO),
                allowSceneObjects = false
            };
            montageField.style.width = 280;
            montageField.style.flexShrink = 0;
            montageField.RegisterValueChangedCallback(evt =>
            {
                context.SetMontage(evt.newValue as AnimMontageSO);
                UpdateStatus();
                OnPlayheadChanged();
            });
            assetGroup.Add(montageField);

            previewModelField = new ObjectField("Preview Mesh")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            previewModelField.style.width = 220;
            previewModelField.style.flexShrink = 0;
            previewModelField.RegisterValueChangedCallback(evt =>
            {
                context.PreviewModel = evt.newValue as GameObject;
                previewController?.SetPreviewModel(context.PreviewModel);
                RequestPreviewRepaint();
                OnPlayheadChanged();
            });
            assetGroup.Add(previewModelField);

            toolbar.Add(assetGroup);
            root.Add(toolbar);
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
            if (context.Montage == null)
                return;

            context.SetPlaying(!context.IsPlaying);
            transportBar?.Refresh();
            UpdateStatus();
        }

        private void StopPlayback()
        {
            context.SetPlaying(false);
            context.SetPlayhead(0f);
            transportBar?.Refresh();
            UpdateStatus();
            RequestPreviewRepaint();
        }

        private void RequestPreviewRepaint() => viewportPanel?.RequestRepaint();

        private void PollPlaybackShortcut()
        {
            if (context == null || context.Montage == null)
                return;

            if (!IsEditorShortcutContext())
                return;

            if (MontageViewportInput.IsViewportEngaged && !IsTimelineAreaFocused())
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
            if (context == null || context.Montage == null || IsShortcutInputBlocked())
                return false;

            TogglePlayPause();
            return true;
        }

        private void OnEditorUpdate()
        {
            PollPlaybackShortcut();

            if (context == null || !context.IsPlaying || context.Montage == null)
            {
                lastEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - lastEditorTime);
            lastEditorTime = now;

            float length = context.Montage.Length;
            float nextTime = context.PlayheadTime + delta * context.PlaybackSpeed;

            if (nextTime >= length)
            {
                if (context.Loop && length > 0f)
                    nextTime = 0f;
                else
                {
                    context.SetPlayhead(length);
                    context.SetPlaying(false);
                    transportBar?.Refresh();
                    UpdateStatus();
                    RequestPreviewRepaint();
                    return;
                }
            }

            context.SetPlayhead(nextTime);
            UpdateStatus();
            RequestPreviewRepaint();
        }

        private void OnPlayheadChanged()
        {
            transportBar?.Refresh();
            previewController?.Sample(context);
            timelineView?.MarkDirtyRepaint();
            RequestPreviewRepaint();
        }

        private void UpdateStatus()
        {
            if (statusLabel == null || context == null)
                return;

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
    }
}

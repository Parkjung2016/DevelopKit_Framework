using System;
using System.Collections.Generic;
using System.Reflection;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class AnimationStateMachineEditorWindow : EditorWindow
    {
        private const float ToolbarHeight = 26f;
        private const float PreferredParameterWidth = 230f;
        private const float PreferredInspectorWidth = 310f;
        private const float MinimumGraphWidth = 220f;
        private const float MinimumParameterWidth = 170f;
        private const float MinimumDetailsWidth = 220f;
        private const float PanelSplitterWidth = 5f;
        private const float MinimumGraphZoom = 0.65f;
        private const float MaximumGraphZoom = 1.5f;
        private const string EntryNodeId = "__Entry";
        private const string RuleResultNodeId = "__RuleResult";
        private static readonly Color GraphBackground = new(0.09f, 0.095f, 0.11f);
        private static readonly Color PanelBackground = new(0.135f, 0.14f, 0.155f);
        private static readonly Color SelectionColor = new(1f, 0.72f, 0.2f);
        private static readonly Color ParameterNodeColor = new(0.16f, 0.28f, 0.47f);
        private static readonly Color OwnerNodeColor = new(0.08f, 0.34f, 0.42f);
        private static readonly Color ResultNodeColor = new(0.14f, 0.43f, 0.22f);
        private static readonly Vector2 NodeSize = new(196f, 76f);
        private static readonly Vector2 RuleNodeSize = new(220f, 76f);
        private static readonly Vector2 RuleOperatorSize = new(132f, 58f);
        private static readonly Vector2 RuleResultSize = new(190f, 68f);
        private static readonly Vector2 GraphUp = new(0f, -1f);
        private static readonly Vector2 GraphDown = new(0f, 1f);
        private static readonly Vector2[] TransitionDirections =
            { Vector2.left, Vector2.right, GraphUp, GraphDown };

        private AnimStateMachineSO stateMachine;
        private AnimMontageLibrarySO library;
        private AnimationStateMachinePlayer debugPlayer;
        private bool followDebugPlayer = true;
        private double nextDebugRepaintTime;
        private double nextDebugSearchTime;
        private string selectedNodeId;
        private string currentStateMachineId = string.Empty;
        private string selectedTransitionId;
        private string editingTransitionId;
        private readonly List<NavigationEntry> navigationHistory = new();
        private readonly List<AnimStateMachineNode> breadcrumbStateMachines = new();
        private int navigationHistoryIndex = -1;
        private string connectingFromId;
        private Vector2 connectingDirection = Vector2.right;
        private string draggingStateId;
        private bool draggingTransition;
        private bool rightMousePressed;
        private bool rightMousePanning;
        private Vector2 rightMouseDownPosition;
        private Vector2 rightMouseLastPosition;
        private Vector2 dragOffset;
        private Vector2 pan = new(20f, 20f);
        private Vector2 parameterScroll;
        private Vector2 inspectorScroll;
        private Vector2 ruleScroll;
        private Vector2 ruleViewportSize = new(500f, 400f);
        private int selectedRuleConditionIndex = -1;
        private string selectedRuleNodeId;
        private readonly HashSet<string> selectedRuleIds = new();
        private readonly Dictionary<string, Vector2> ruleDragStartPositions = new();
        private bool draggingRuleSelection;
        private bool ruleBoxSelecting;
        private bool ruleBoxSelectionAdditive;
        private Vector2 ruleBoxStart;
        private Vector2 ruleBoxEnd;
        private Vector2 ruleDragStartMouse;
        private readonly Dictionary<AnimStateCondition, Rect> ruleConditionRects = new();
        private readonly Dictionary<string, Rect> ruleOperatorRects = new();
        private Vector2 rulePan = new(28f, 28f);
        private float ruleZoom = 1f;
        private string connectingRuleSourceId;
        private bool ruleRightMousePressed;
        private bool ruleRightMousePanning;
        private Vector2 ruleRightMouseDown;
        private Vector2 ruleRightMouseLast;
        private bool showParameters = true;
        private bool showInspector = true;
        private float parameterPanelWidth = PreferredParameterWidth;
        private float detailsPanelWidth = PreferredInspectorWidth;
        private int resizingPanel;
        private Vector2 graphViewportSize = new(500f, 400f);
        private readonly Dictionary<string, Rect> nodeRects = new();
        private readonly Dictionary<string, string> nodeValidationErrors = new();
        private readonly Dictionary<string, string> transitionValidationErrors = new();
        private readonly HashSet<string> selectedNodeIds = new();
        private readonly HashSet<string> selectedTransitionIds = new();
        private readonly Dictionary<string, Vector2> dragStartPositions = new();
        private int moveUndoGroup = -1;
        private bool boxSelecting;
        private bool boxSelectionAdditive;
        private Vector2 boxStart;
        private Vector2 boxEnd;
        private Vector2 nodeDragStartMouse;
        private bool snapToGrid = true;
        private float graphGridSize = 24f;
        private float graphZoom = 1f;
        private AnimStateMachineSO validatedStateMachine;
        private bool validationDirty = true;

        private static GUIStyle CenteredMiniLabel => centeredMiniLabel ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        private static GUIStyle CenteredBoldLabel => centeredBoldLabel ??= new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        private static GUIStyle RightMiniLabel => rightMiniLabel ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            fontStyle = FontStyle.Bold
        };
        private static GUIStyle WrappedMiniLabel => wrappedMiniLabel ??= new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            clipping = TextClipping.Clip
        };
        private static List<Type> ownerComponentTypes;
        private static StateGraphClipboard stateClipboard;
        private static RuleGraphClipboard ruleClipboard;
        private static GUIStyle centeredMiniLabel;
        private static GUIStyle centeredBoldLabel;
        private static GUIStyle rightMiniLabel;
        private static GUIStyle wrappedMiniLabel;
        private static GUIStyle breadcrumbLabel;
        private static GUIStyle ruleBadgeLabel;

        private static GUIStyle BreadcrumbLabel => breadcrumbLabel ??= new GUIStyle(EditorStyles.toolbarButton)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold
        };

        private static GUIStyle RuleBadgeLabel => ruleBadgeLabel ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.55f, 0.9f, 0.66f) }
        };

        private sealed class NavigationEntry
        {
            public string StateMachineId;
            public string TransitionId;
            public Vector2 ViewPosition;
            public float Zoom;
        }

        private enum DebugNodeState
        {
            None,
            Current,
            Next
        }

        public static void Open(AnimStateMachineSO value, AnimMontageLibrarySO ownerLibrary = null)
        {
            AnimationStateMachineEditorWindow window = GetWindow<AnimationStateMachineEditorWindow>();
            window.titleContent = new GUIContent(value != null
                ? $"State Machine - {value.name}"
                : "Animation State Machine");
            window.minSize = new Vector2(620f, 420f);
            window.stateMachine = value;
            window.library = ownerLibrary;
            window.ClearSelection();
            window.debugPlayer = null;
            window.nextDebugSearchTime = 0d;
            window.editingTransitionId = null;
            window.currentStateMachineId = string.Empty;
            window.validationDirty = true;
            window.ResetNavigationHistory();
            window.Show();
            window.Focus();
        }

        public static void Open(AnimationStateMachinePlayer player)
        {
            if (player?.StateMachine == null)
                return;

            Open(player.StateMachine);
            AnimationStateMachineEditorWindow window = GetWindow<AnimationStateMachineEditorWindow>();
            window.debugPlayer = player;
            window.followDebugPlayer = true;
            window.Repaint();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnEditorDataChanged;
            EditorApplication.projectChanged += OnEditorDataChanged;
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnHierarchySelectionChanged;
            EditorApplication.delayCall += OnHierarchySelectionChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnEditorDataChanged;
            EditorApplication.projectChanged -= OnEditorDataChanged;
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnHierarchySelectionChanged;
            EditorApplication.delayCall -= OnHierarchySelectionChanged;
            CancelAllInteractions();
        }

        private void OnHierarchySelectionChanged()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null || !selectedObject.scene.IsValid())
                return;

            AnimationStateMachinePlayer player =
                selectedObject.GetComponent<AnimationStateMachinePlayer>();
            player ??= selectedObject.GetComponentInParent<AnimationStateMachinePlayer>(true);
            player ??= selectedObject.GetComponentInChildren<AnimationStateMachinePlayer>(true);
            if (player?.StateMachine == null)
                return;

            if (EditorApplication.isPlaying)
                SetDebugPlayer(player);
            else
                SwitchStateMachine(player.StateMachine);

            Repaint();
        }


        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying || debugPlayer == null
                || EditorApplication.timeSinceStartup < nextDebugRepaintTime)
                return;

            nextDebugRepaintTime = EditorApplication.timeSinceStartup + 1d / 30d;
            Repaint();
        }

        private void OnEditorDataChanged()
        {
            validationDirty = true;
            Repaint();
        }

        private void OnLostFocus()
        {
            CancelAllInteractions();
            Repaint();
        }

        private void CancelAllInteractions()
        {
            CancelStateGraphInteractions();
            CancelRuleGraphInteractions();
            resizingPanel = 0;
        }

        private void CancelStateGraphInteractions()
        {
            FinishMoveUndo();
            draggingStateId = null;
            draggingTransition = false;
            connectingFromId = null;
            rightMousePressed = false;
            rightMousePanning = false;
            boxSelecting = false;
            dragStartPositions.Clear();
        }

        private void CancelRuleGraphInteractions()
        {
            FinishMoveUndo();
            connectingRuleSourceId = null;
            draggingRuleSelection = false;
            ruleBoxSelecting = false;
            ruleRightMousePressed = false;
            ruleRightMousePanning = false;
            ruleDragStartPositions.Clear();
        }

        private void OnGUI()
        {
            ResolveDebugPlayer();
            FollowCurrentDebugState();
            HandleNavigationMouseButtons();
            if (string.IsNullOrEmpty(editingTransitionId))
                DrawToolbar();
            else
                DrawRuleToolbar();
            if (stateMachine == null)
            {
                EditorGUI.HelpBox(
                    new Rect(12f, ToolbarHeight + 12f, Mathf.Max(120f, position.width - 24f), 42f),
                    "Animation State Machine을 선택하세요.",
                    MessageType.Info);
                return;
            }

            if (!string.IsNullOrEmpty(editingTransitionId))
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    DrawTransitionRuleWorkspace(new Rect(0f, ToolbarHeight, position.width,
                        Mathf.Max(1f, position.height - ToolbarHeight)));
                }
                HandleRuleKeyboard();
                return;
            }

            bool hasSelection = selectedNodeIds.Count > 0 || selectedTransitionIds.Count > 0;
            GetPanelWidths(hasSelection, out float leftWidth, out float rightWidth);
            float contentHeight = Mathf.Max(100f, position.height - ToolbarHeight);
            Rect parametersRect = new(0f, ToolbarHeight, leftWidth, contentHeight);
            Rect graphRect = new(leftWidth, ToolbarHeight,
                Mathf.Max(1f, position.width - leftWidth - rightWidth), contentHeight);
            Rect inspectorRect = new(graphRect.xMax, ToolbarHeight, rightWidth, contentHeight);
            graphViewportSize = graphRect.size;

            HandlePanelResize(graphRect, leftWidth > 0f, rightWidth > 0f);
            if (leftWidth > 0f)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    DrawParameters(parametersRect);
            }
            DrawGraph(graphRect);
            if (rightWidth > 0f)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    DrawInspector(inspectorRect);
            }
            DrawPanelSplitters(graphRect, leftWidth > 0f, rightWidth > 0f);
            HandleKeyboard();
        }

        private void GetPanelWidths(bool hasSelection, out float leftWidth, out float rightWidth)
        {
            bool effectiveParameters = showParameters;
            bool effectiveInspector = showInspector;
            if (position.width < 820f && effectiveParameters && effectiveInspector)
            {
                effectiveParameters = !hasSelection;
                effectiveInspector = hasSelection;
            }

            leftWidth = effectiveParameters
                ? Mathf.Clamp(parameterPanelWidth, MinimumParameterWidth,
                    Mathf.Max(MinimumParameterWidth, position.width - MinimumGraphWidth))
                : 0f;
            rightWidth = effectiveInspector
                ? Mathf.Clamp(detailsPanelWidth, MinimumDetailsWidth,
                    Mathf.Max(MinimumDetailsWidth, position.width - MinimumGraphWidth))
                : 0f;

            float excess = leftWidth + rightWidth + MinimumGraphWidth - position.width;
            if (excess <= 0f)
                return;
            if (leftWidth > 0f && rightWidth > 0f)
            {
                float leftShare = excess * 0.5f;
                leftWidth = Mathf.Max(MinimumParameterWidth, leftWidth - leftShare);
                rightWidth = Mathf.Max(MinimumDetailsWidth,
                    position.width - MinimumGraphWidth - leftWidth);
                return;
            }
            if (leftWidth > 0f)
                leftWidth = Mathf.Max(0f, position.width - MinimumGraphWidth);
            if (rightWidth > 0f)
                rightWidth = Mathf.Max(0f, position.width - MinimumGraphWidth);
        }

        private void HandlePanelResize(Rect graphRect, bool hasLeft, bool hasRight)
        {
            Event evt = Event.current;
            Rect leftSplitter = new(graphRect.xMin - PanelSplitterWidth * 0.5f, graphRect.y,
                PanelSplitterWidth, graphRect.height);
            Rect rightSplitter = new(graphRect.xMax - PanelSplitterWidth * 0.5f, graphRect.y,
                PanelSplitterWidth, graphRect.height);

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (hasLeft && leftSplitter.Contains(evt.mousePosition))
                    resizingPanel = -1;
                else if (hasRight && rightSplitter.Contains(evt.mousePosition))
                    resizingPanel = 1;
                if (resizingPanel != 0)
                {
                    evt.Use();
                    return;
                }
            }
            if (evt.type == EventType.MouseDrag && resizingPanel != 0)
            {
                if (resizingPanel < 0)
                    parameterPanelWidth = Mathf.Clamp(evt.mousePosition.x,
                        MinimumParameterWidth, position.width - MinimumGraphWidth);
                else
                    detailsPanelWidth = Mathf.Clamp(position.width - evt.mousePosition.x,
                        MinimumDetailsWidth, position.width - MinimumGraphWidth);
                evt.Use();
                Repaint();
                return;
            }
            if (evt.rawType == EventType.MouseUp && resizingPanel != 0)
            {
                resizingPanel = 0;
                Repaint();
            }
        }

        private static void DrawPanelSplitters(Rect graphRect, bool hasLeft, bool hasRight)
        {
            Color line = new(0f, 0f, 0f, 0.55f);
            if (hasLeft)
            {
                Rect splitter = new(graphRect.xMin - PanelSplitterWidth * 0.5f, graphRect.y,
                    PanelSplitterWidth, graphRect.height);
                EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);
                EditorGUI.DrawRect(new Rect(graphRect.xMin, graphRect.y, 1f, graphRect.height), line);
            }
            if (hasRight)
            {
                Rect splitter = new(graphRect.xMax - PanelSplitterWidth * 0.5f, graphRect.y,
                    PanelSplitterWidth, graphRect.height);
                EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);
                EditorGUI.DrawRect(new Rect(graphRect.xMax - 1f, graphRect.y, 1f, graphRect.height), line);
            }
        }

        private void HandleNavigationMouseButtons()
        {
            Event evt = Event.current;
            if (evt.type != EventType.MouseDown)
                return;

            if (evt.button == 3 && NavigateBack())
                evt.Use();
            else if (evt.button == 4 && NavigateForward())
                evt.Use();
        }

        private void DrawRuleToolbar()
        {
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, ToolbarHeight), EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            DrawHistoryButtons();

            AnimStateTransition transition = stateMachine != null ? FindTransition(editingTransitionId) : null;
            AnimStateNode from = transition != null ? stateMachine.FindNode(transition.FromStateId) : null;
            AnimStateNode to = transition != null ? stateMachine.FindNode(transition.ToStateId) : null;
            bool compact = position.width < 760f;
            DrawRuleBreadcrumb(from, to, compact);
            GUILayout.FlexibleSpace();
            if (transition != null)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    if (GUILayout.Button(new GUIContent(compact ? "+" : "+ Node",
                                "조건 또는 논리 노드 추가"),
                            EditorStyles.toolbarDropDown, GUILayout.Width(compact ? 28f : 64f)))
                        ShowAddRuleConditionMenu(transition);
                    using (new EditorGUI.DisabledScope(selectedRuleIds.Count < 2))
                    {
                        if (GUILayout.Button(new GUIContent(compact ? "A" : "Arrange",
                                    "선택한 Rule 노드 정렬"),
                                EditorStyles.toolbarDropDown, GUILayout.Width(compact ? 28f : 58f)))
                            ShowRuleArrangeMenu(transition);
                    }
                }
                if (GUILayout.Button(new GUIContent(compact ? "F" : "Frame",
                            "전체 Rule 그래프 보기 (F)"),
                        EditorStyles.toolbarButton, GUILayout.Width(compact ? 28f : 46f)))
                    FrameRuleGraph(transition);
                GUILayout.Label($"{transition.Conditions.Count + transition.RuleNodes.Count}",
                    EditorStyles.miniLabel, GUILayout.Width(24f));
            }
            DrawPanelToggles(position.width < 760f);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawRuleBreadcrumb(AnimStateNode from, AnimStateNode to, bool compact)
        {
            breadcrumbStateMachines.Clear();
            AnimStateMachineNode machine = stateMachine?.FindStateMachine(currentStateMachineId);
            while (machine != null)
            {
                breadcrumbStateMachines.Add(machine);
                machine = stateMachine.FindStateMachine(machine.ParentStateMachineId);
            }
            breadcrumbStateMachines.Reverse();

            DrawBreadcrumbSeparator();
            if (GUILayout.Button(new GUIContent("Root", "Root State Machine으로 이동"),
                    EditorStyles.toolbarButton, GUILayout.Width(42f)))
                NavigateFromToolbar(string.Empty, null);

            int firstMachine = compact
                ? Mathf.Max(0, breadcrumbStateMachines.Count - 1)
                : 0;
            if (compact && breadcrumbStateMachines.Count > 1)
            {
                DrawBreadcrumbSeparator();
                GUILayout.Label("...", EditorStyles.miniLabel, GUILayout.Width(18f));
            }
            for (int i = firstMachine; i < breadcrumbStateMachines.Count; i++)
            {
                AnimStateMachineNode stateMachineNode = breadcrumbStateMachines[i];
                DrawBreadcrumbSeparator();
                float width = Mathf.Clamp(
                    EditorStyles.toolbarButton.CalcSize(new GUIContent(stateMachineNode.Name)).x + 8f,
                    44f,
                    compact ? 78f : 120f);
                if (GUILayout.Button(new GUIContent(stateMachineNode.Name, stateMachineNode.Name),
                        EditorStyles.toolbarButton, GUILayout.Width(width)))
                    NavigateFromToolbar(stateMachineNode.Id, null);
            }

            DrawBreadcrumbSeparator();
            string route = from == null || to == null
                ? "Missing Transition"
                : $"{from.Name} -> {to.Name}";
            float routeWidth = Mathf.Clamp(BreadcrumbLabel.CalcSize(new GUIContent(route)).x + 10f,
                80f, compact ? 130f : 220f);
            GUILayout.Label(new GUIContent(route, route), BreadcrumbLabel, GUILayout.Width(routeWidth));
            GUILayout.Label("RULE", RuleBadgeLabel, GUILayout.Width(34f));
        }

        private static void DrawBreadcrumbSeparator() =>
            GUILayout.Label("/", EditorStyles.miniLabel, GUILayout.Width(9f));

        private void NavigateFromToolbar(string stateMachineId, string transitionId)
        {
            NavigateToLocation(stateMachineId, transitionId);
            GUIUtility.ExitGUI();
        }

        private void OpenTransitionRule(string transitionId)
        {
            if (stateMachine == null || FindTransition(transitionId) == null)
                return;

            NavigateToLocation(currentStateMachineId, transitionId);
        }

        private void CloseTransitionRule() =>
            NavigateToLocation(currentStateMachineId, null);

        private bool NavigateBack() => NavigateHistory(-1);

        private bool NavigateForward() => NavigateHistory(1);

        private void DrawHistoryButtons()
        {
            using (new EditorGUI.DisabledScope(FindHistoryIndex(-1) < 0))
            {
                if (GUILayout.Button(new GUIContent("<", "뒤로 (Alt+Left)"),
                        EditorStyles.toolbarButton, GUILayout.Width(26f)) && NavigateBack())
                    GUIUtility.ExitGUI();
            }
            using (new EditorGUI.DisabledScope(FindHistoryIndex(1) < 0))
            {
                if (GUILayout.Button(new GUIContent(">", "앞으로 (Alt+Right)"),
                        EditorStyles.toolbarButton, GUILayout.Width(26f)) && NavigateForward())
                    GUIUtility.ExitGUI();
            }
        }

        private void ResetNavigationHistory()
        {
            navigationHistory.Clear();
            navigationHistoryIndex = -1;
            if (stateMachine == null)
                return;

            navigationHistory.Add(CaptureCurrentLocation());
            navigationHistoryIndex = 0;
        }

        private void NavigateToLocation(string stateMachineId, string transitionId)
        {
            if (stateMachine == null)
                return;

            EnsureNavigationHistory();
            string targetStateMachineId = NormalizeStateMachineId(stateMachineId);
            if (!string.IsNullOrEmpty(transitionId) && FindTransition(transitionId) == null)
                return;
            if (currentStateMachineId == targetStateMachineId
                && editingTransitionId == transitionId)
                return;

            SaveCurrentNavigationView();
            int forwardCount = navigationHistory.Count - navigationHistoryIndex - 1;
            if (forwardCount > 0)
                navigationHistory.RemoveRange(navigationHistoryIndex + 1, forwardCount);

            ApplyNewLocation(targetStateMachineId, transitionId);
            navigationHistory.Add(CaptureCurrentLocation());
            navigationHistoryIndex = navigationHistory.Count - 1;
        }

        private void EnsureNavigationHistory()
        {
            if (navigationHistoryIndex >= 0
                && navigationHistoryIndex < navigationHistory.Count)
            {
                NavigationEntry current = navigationHistory[navigationHistoryIndex];
                if (current.StateMachineId == currentStateMachineId
                    && current.TransitionId == editingTransitionId)
                    return;
            }

            navigationHistory.Clear();
            navigationHistory.Add(CaptureCurrentLocation());
            navigationHistoryIndex = 0;
        }

        private void ApplyNewLocation(string stateMachineId, string transitionId)
        {
            bool stateMachineChanged = currentStateMachineId != stateMachineId;
            CancelAllInteractions();
            currentStateMachineId = stateMachineId;
            editingTransitionId = transitionId;
            if (stateMachineChanged)
                ClearSelection();
            ClearRuleSelection();
            GUI.FocusControl(null);

            if (!string.IsNullOrEmpty(transitionId))
            {
                rulePan = new Vector2(28f, 28f);
                ruleZoom = 1f;
                FrameRuleGraph(FindTransition(transitionId));
            }
            else
            {
                pan = new Vector2(20f, 20f);
                graphZoom = 1f;
                FrameStates();
            }

            Repaint();
        }

        private bool NavigateHistory(int direction)
        {
            int targetIndex = FindHistoryIndex(direction);
            if (targetIndex < 0)
                return false;

            SaveCurrentNavigationView();
            navigationHistoryIndex = targetIndex;
            ApplyHistoryLocation(navigationHistory[targetIndex]);
            return true;
        }

        private int FindHistoryIndex(int direction)
        {
            if (direction == 0 || navigationHistory.Count == 0)
                return -1;

            for (int i = navigationHistoryIndex + direction;
                 i >= 0 && i < navigationHistory.Count;
                 i += direction)
            {
                if (IsValidLocation(navigationHistory[i]))
                    return i;
            }

            return -1;
        }

        private bool IsValidLocation(NavigationEntry entry)
        {
            if (stateMachine == null || entry == null)
                return false;
            if (!string.IsNullOrEmpty(entry.StateMachineId)
                && stateMachine.FindStateMachine(entry.StateMachineId) == null)
                return false;

            return string.IsNullOrEmpty(entry.TransitionId)
                   || FindTransition(entry.TransitionId) != null;
        }

        private void ApplyHistoryLocation(NavigationEntry entry)
        {
            bool stateMachineChanged = currentStateMachineId != entry.StateMachineId;
            CancelAllInteractions();
            currentStateMachineId = entry.StateMachineId;
            editingTransitionId = entry.TransitionId;
            if (stateMachineChanged)
                ClearSelection();
            ClearRuleSelection();

            if (string.IsNullOrEmpty(entry.TransitionId))
            {
                pan = entry.ViewPosition;
                graphZoom = Mathf.Clamp(entry.Zoom, MinimumGraphZoom, MaximumGraphZoom);
            }
            else
            {
                rulePan = entry.ViewPosition;
                ruleZoom = Mathf.Clamp(entry.Zoom, MinimumGraphZoom, MaximumGraphZoom);
            }

            GUI.FocusControl(null);
            Repaint();
        }

        private NavigationEntry CaptureCurrentLocation() => new()
        {
            StateMachineId = currentStateMachineId,
            TransitionId = editingTransitionId,
            ViewPosition = string.IsNullOrEmpty(editingTransitionId) ? pan : rulePan,
            Zoom = string.IsNullOrEmpty(editingTransitionId) ? graphZoom : ruleZoom
        };

        private void SaveCurrentNavigationView()
        {
            if (navigationHistoryIndex < 0 || navigationHistoryIndex >= navigationHistory.Count)
                return;

            NavigationEntry entry = navigationHistory[navigationHistoryIndex];
            if (entry.StateMachineId != currentStateMachineId
                || entry.TransitionId != editingTransitionId)
                return;

            entry.ViewPosition = string.IsNullOrEmpty(editingTransitionId) ? pan : rulePan;
            entry.Zoom = string.IsNullOrEmpty(editingTransitionId) ? graphZoom : ruleZoom;
        }

        private string NormalizeStateMachineId(string stateMachineId) =>
            !string.IsNullOrEmpty(stateMachineId)
            && stateMachine.FindStateMachine(stateMachineId) != null
                ? stateMachineId
                : string.Empty;

        private void ResolveDebugPlayer()
        {
            if (!EditorApplication.isPlaying || stateMachine == null)
                return;
            if (debugPlayer != null && debugPlayer.StateMachine == stateMachine)
                return;
            if (EditorApplication.timeSinceStartup < nextDebugSearchTime)
                return;

            nextDebugSearchTime = EditorApplication.timeSinceStartup + 1d;
            debugPlayer = null;
            AnimationStateMachinePlayer[] players =
                UnityEngine.Object.FindObjectsByType<AnimationStateMachinePlayer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].StateMachine != stateMachine)
                    continue;
                debugPlayer = players[i];
                nextDebugSearchTime = 0d;
                break;
            }
        }

        private void FollowCurrentDebugState()
        {
            if (!EditorApplication.isPlaying || !followDebugPlayer || debugPlayer == null
                || !string.IsNullOrEmpty(editingTransitionId))
                return;
            AnimSequenceState current = debugPlayer.CurrentState;
            if (current == null || current.ParentStateMachineId == currentStateMachineId)
                return;

            CancelAllInteractions();
            currentStateMachineId = current.ParentStateMachineId;
            ClearSelection();
            ClearRuleSelection();
            pan = new Vector2(20f, 20f);
            graphZoom = 1f;
            FrameStates();
            ResetNavigationHistory();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, ToolbarHeight), EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            bool compactToolbar = position.width < 980f;
            DrawHistoryButtons();
            float assetWidth = compactToolbar
                ? 120f
                : Mathf.Clamp(position.width * 0.28f, 120f, 240f);
            AnimStateMachineSO selected = (AnimStateMachineSO)EditorGUILayout.ObjectField(
                stateMachine,
                typeof(AnimStateMachineSO),
                false,
                GUILayout.Width(assetWidth));
            if (selected != stateMachine)
                SwitchStateMachine(selected);

            if (EditorApplication.isPlaying)
            {
                DrawLiveDebugToolbar(compactToolbar);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent(compactToolbar ? "F" : "Frame", "Frame active State (F)"),
                        EditorStyles.toolbarButton, GUILayout.Width(compactToolbar ? 28f : 48f)))
                    FrameDebugState();
                DrawPanelToggles(compactToolbar);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || stateMachine == null))
            {
                GUIContent addNodeContent = new(compactToolbar ? "+" : "+ Node", "Add node");
                if (GUILayout.Button(addNodeContent, EditorStyles.toolbarDropDown,
                        GUILayout.Width(compactToolbar ? 30f : 66f)))
                    ShowAddStateMenu(ScreenToGraph(graphViewportSize * 0.5f) - NodeSize * 0.5f);
            }
            DrawStateMachineBreadcrumb(compactToolbar);
            if (GUILayout.Button(new GUIContent(compactToolbar ? "F" : "Frame", "Frame selection (F)"),
                    EditorStyles.toolbarButton, GUILayout.Width(compactToolbar ? 28f : 48f)))
                FrameSelectionOrAll();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || stateMachine == null))
            {
                snapToGrid = GUILayout.Toggle(snapToGrid,
                    new GUIContent("Snap", "Snap moved nodes to the graph grid"),
                    EditorStyles.toolbarButton, GUILayout.Width(42f));
                if (GUILayout.Button(new GUIContent($"{graphGridSize:0}", "Grid size"),
                        EditorStyles.toolbarDropDown, GUILayout.Width(42f)))
                    ShowGridSizeMenu();
                if (GUILayout.Button(new GUIContent($"{graphZoom * 100f:0}%", "Reset zoom"),
                        EditorStyles.toolbarButton, GUILayout.Width(46f)))
                    ResetZoom();
                using (new EditorGUI.DisabledScope(selectedNodeIds.Count < 2))
                {
                    if (GUILayout.Button(new GUIContent(compactToolbar ? "A" : "Arrange",
                                "Align and distribute selected nodes"),
                            EditorStyles.toolbarDropDown, GUILayout.Width(compactToolbar ? 30f : 58f)))
                        ShowArrangeMenu();
                }
            }

            GUILayout.FlexibleSpace();
            DrawPanelToggles(compactToolbar);
            if (connectingFromId != null && GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                connectingFromId = null;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawStateMachineBreadcrumb(bool compact)
        {
            AnimStateMachineNode current = stateMachine?.FindStateMachine(currentStateMachineId);
            if (compact)
            {
                if (current == null)
                {
                    GUILayout.Label("Root", EditorStyles.miniLabel, GUILayout.Width(34f));
                    return;
                }

                if (GUILayout.Button(new GUIContent("Up", "부모 State Machine으로 이동"),
                        EditorStyles.toolbarButton, GUILayout.Width(28f)))
                    EnterStateMachine(current.ParentStateMachineId);
                GUILayout.Label(new GUIContent(current.Name, current.Name),
                    EditorStyles.miniLabel, GUILayout.MaxWidth(72f));
                return;
            }

            breadcrumbStateMachines.Clear();
            for (AnimStateMachineNode machine = current; machine != null;
                 machine = stateMachine.FindStateMachine(machine.ParentStateMachineId))
                breadcrumbStateMachines.Add(machine);
            breadcrumbStateMachines.Reverse();

            if (GUILayout.Button(new GUIContent("Root", "Root State Machine으로 이동"),
                    EditorStyles.toolbarButton, GUILayout.Width(40f)))
                EnterStateMachine(string.Empty);

            int first = Mathf.Max(0, breadcrumbStateMachines.Count - 2);
            if (first > 0)
                GUILayout.Label("/ ...", EditorStyles.miniLabel, GUILayout.Width(28f));
            for (int i = first; i < breadcrumbStateMachines.Count; i++)
            {
                AnimStateMachineNode machine = breadcrumbStateMachines[i];
                GUILayout.Label("/", EditorStyles.miniLabel, GUILayout.Width(8f));
                float width = Mathf.Clamp(
                    EditorStyles.toolbarButton.CalcSize(new GUIContent(machine.Name)).x + 8f,
                    44f, 100f);
                if (GUILayout.Button(new GUIContent(machine.Name, machine.Name),
                        EditorStyles.toolbarButton, GUILayout.Width(width)))
                    EnterStateMachine(machine.Id);
            }
        }

        private void SwitchStateMachine(AnimStateMachineSO selected)
        {
            if (stateMachine == selected)
                return;

            stateMachine = selected;
            if (library != null && library.StateMachine != stateMachine)
                library = null;
            debugPlayer = null;
            nextDebugSearchTime = 0d;
            ClearSelection();
            ClearRuleSelection();
            editingTransitionId = null;
            currentStateMachineId = string.Empty;
            pan = new Vector2(20f, 20f);
            graphZoom = 1f;
            validationDirty = true;
            ResetNavigationHistory();
            FrameStates();
            SaveCurrentNavigationView();
            titleContent = new GUIContent(stateMachine != null
                ? $"State Machine - {stateMachine.name}"
                : "Animation State Machine");
        }


        private void DrawLiveDebugToolbar(bool compact)
        {
            GUILayout.Label(new GUIContent("LIVE", "Play Mode State Machine debugger"),
                RuleBadgeLabel, GUILayout.Width(34f));
            float playerWidth = compact ? 105f : 170f;
            AnimationStateMachinePlayer next = (AnimationStateMachinePlayer)EditorGUILayout.ObjectField(
                debugPlayer, typeof(AnimationStateMachinePlayer), true, GUILayout.Width(playerWidth));
            if (next != debugPlayer)
                SetDebugPlayer(next);

            followDebugPlayer = GUILayout.Toggle(followDebugPlayer,
                new GUIContent(compact ? "Follow" : "Follow Active", "현재 State가 있는 하위 그래프를 자동으로 엽니다."),
                EditorStyles.toolbarButton, GUILayout.Width(compact ? 46f : 82f));

            string status = debugPlayer == null
                ? "No Player"
                : debugPlayer.CurrentState == null
                    ? "Waiting"
                    : debugPlayer.IsTransitioning
                        ? $"{debugPlayer.CurrentState.Name} -> {debugPlayer.NextState?.Name ?? "?"}"
                        : $"{debugPlayer.CurrentState.Name}  {debugPlayer.StateNormalizedTime:0.00}";
            GUILayout.Label(new GUIContent(status, status), EditorStyles.miniLabel,
                GUILayout.MaxWidth(compact ? 110f : 190f));
        }

        private void SetDebugPlayer(AnimationStateMachinePlayer player)
        {
            debugPlayer = player;
            titleContent = new GUIContent(player?.StateMachine != null
                ? $"State Machine - {player.StateMachine.name}"
                : "Animation State Machine");
            if (player?.StateMachine == null || player.StateMachine == stateMachine)
                return;

            stateMachine = player.StateMachine;
            currentStateMachineId = string.Empty;
            editingTransitionId = null;
            ClearSelection();
            ClearRuleSelection();
            validationDirty = true;
            ResetNavigationHistory();
            FrameStates();
        }

        private void FrameDebugState()
        {
            AnimSequenceState current = debugPlayer?.CurrentState;
            if (current == null)
            {
                FrameStates();
                return;
            }

            if (current.ParentStateMachineId != currentStateMachineId)
            {
                currentStateMachineId = current.ParentStateMachineId;
                ClearSelection();
            }
            selectedNodeIds.Clear();
            selectedNodeIds.Add(current.Id);
            selectedNodeId = current.Id;
            FrameSelectionOrAll();
        }

        private void DrawPanelToggles(bool compact)
        {
            bool nextParameters = GUILayout.Toggle(showParameters,
                new GUIContent(compact ? "P" : "Parameters", "Show Parameters"),
                EditorStyles.toolbarButton, GUILayout.Width(compact ? 30f : 78f));
            bool nextInspector = GUILayout.Toggle(showInspector,
                new GUIContent(compact ? "D" : "Details", "Show Details"),
                EditorStyles.toolbarButton, GUILayout.Width(compact ? 30f : 56f));
            if (nextParameters != showParameters)
            {
                showParameters = nextParameters;
                if (position.width < 820f && showParameters)
                    showInspector = false;
            }
            if (nextInspector != showInspector)
            {
                showInspector = nextInspector;
                if (position.width < 820f && showInspector)
                    showParameters = false;
            }
        }
        private void DrawParameters(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBackground);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.55f));
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Parameters  {stateMachine.Parameters.Count}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+", "Add parameter"), GUILayout.Width(26f), GUILayout.Height(20f)))
                ShowAddParameterMenu();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            parameterScroll = GUILayout.BeginScrollView(parameterScroll);
            if (stateMachine.Parameters.Count == 0)
                EditorGUILayout.HelpBox("Transition 조건에 사용할 Parameter를 추가하세요.", MessageType.None);
            for (int i = 0; i < stateMachine.Parameters.Count; i++)
                DrawParameter(i, stateMachine.Parameters[i]);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawParameter(int index, AnimStateParameter parameter)
        {
            if (parameter == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.BeginHorizontal();
                string nextName = EditorGUILayout.DelayedTextField(parameter.Name);
                AnimStateParameterType nextType = (AnimStateParameterType)EditorGUILayout.EnumPopup(
                    parameter.Type, GUILayout.Width(68f));
                if (GUILayout.Button(new GUIContent("-", "Remove parameter"), GUILayout.Width(22f)))
                {
                    Undo.RecordObject(stateMachine, "Remove Animation Parameter");
                    stateMachine.RemoveParameterAt(index);
                    Save();
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();

                float nextFloat = parameter.DefaultFloat;
                int nextInt = parameter.DefaultInt;
                bool nextBool = parameter.DefaultBool;
                switch (nextType)
                {
                    case AnimStateParameterType.Float:
                        nextFloat = EditorGUILayout.FloatField("Default", nextFloat);
                        break;
                    case AnimStateParameterType.Int:
                        nextInt = EditorGUILayout.IntField("Default", nextInt);
                        break;
                    case AnimStateParameterType.Bool:
                        nextBool = EditorGUILayout.Toggle("Default", nextBool);
                        break;
                    case AnimStateParameterType.Trigger:
                        EditorGUILayout.LabelField("Trigger", "Reset after transition", EditorStyles.miniLabel);
                        break;
                }

                if (nextName != parameter.Name || nextType != parameter.Type
                    || !Mathf.Approximately(nextFloat, parameter.DefaultFloat)
                    || nextInt != parameter.DefaultInt || nextBool != parameter.DefaultBool)
                {
                    Undo.RecordObject(stateMachine, "Edit Animation Parameter");
                    if (nextName != parameter.Name)
                        stateMachine.RenameParameterAt(index, nextName);
                    parameter.Type = nextType;
                    parameter.DefaultFloat = nextFloat;
                    parameter.DefaultInt = nextInt;
                    parameter.DefaultBool = nextBool;
                    Save();
                }
            }
        }
        private void DrawGraph(Rect graphRect)
        {
            EnsureValidationCache();
            GUI.BeginGroup(graphRect);
            Rect localRect = new(0f, 0f, graphRect.width, graphRect.height);
            EditorGUI.DrawRect(localRect, GraphBackground);
            DrawGrid(localRect, graphGridSize * graphZoom, new Color(1f, 1f, 1f, 0.035f), pan);
            DrawGrid(localRect, graphGridSize * graphZoom * 5f, new Color(1f, 1f, 1f, 0.07f), pan);
            if (EditorApplication.isPlaying)
                DrawLiveStatusOverlay();

            nodeRects.Clear();
            Rect entryRect = ToGraphRect(stateMachine.GetEntryPosition(currentStateMachineId), new Vector2(132f, 48f));
            nodeRects[EntryNodeId] = entryRect;
            AddVisibleNodeRects(stateMachine.States);
            AddVisibleNodeRects(stateMachine.Conduits);
            AddVisibleNodeRects(stateMachine.Aliases);
            AddVisibleNodeRects(stateMachine.StateMachines);

            DrawEntryTransition(entryRect);
            DrawTransitions();
            DrawNode(entryRect, "Entry", "Default route", false, false, false, false, false,
                new Color(0.18f, 0.38f, 0.22f), null, DebugNodeState.None);

            string defaultId = stateMachine.GetDefaultNodeId(currentStateMachineId);
            for (int i = 0; i < stateMachine.States.Count; i++)
            {
                AnimSequenceState state = stateMachine.States[i];
                if (state == null || !nodeRects.TryGetValue(state.Id, out Rect rect))
                    continue;
                bool isDefault = state.Id == defaultId;
                string sequenceName = state.Sequence != null ? state.Sequence.name : "Sequence not assigned";
                string meta = state.Sequence?.Clip != null
                    ? $"{state.Sequence.Length:0.##}s" + (state.Loop ? "  Loop" : string.Empty)
                    : "Drop a Sequence here";
                DrawNode(rect, state.Name, $"{sequenceName}   {meta}", IsNodeSelected(state.Id),
                    connectingFromId == state.Id, IsConnectionTarget(state, rect), isDefault, state.Loop,
                    isDefault ? new Color(0.16f, 0.42f, 0.24f) : new Color(0.16f, 0.27f, 0.43f),
                    GetNodeValidationError(state.Id), GetDebugNodeState(state.Id));
            }
            for (int i = 0; i < stateMachine.Conduits.Count; i++)
            {
                AnimStateConduit node = stateMachine.Conduits[i];
                if (node == null || !nodeRects.TryGetValue(node.Id, out Rect rect))
                    continue;
                DrawNode(rect, GetNodeDisplayName(node), $"FLOW  |  {CountOutgoing(node.Id)} conditional routes",
                    IsNodeSelected(node.Id), connectingFromId == node.Id, IsConnectionTarget(node, rect),
                    node.Id == defaultId, false, new Color(0.42f, 0.27f, 0.10f),
                    GetNodeValidationError(node.Id), GetDebugNodeState(node.Id));
            }
            for (int i = 0; i < stateMachine.Aliases.Count; i++)
            {
                AnimStateAlias node = stateMachine.Aliases[i];
                if (node == null || !nodeRects.TryGetValue(node.Id, out Rect rect))
                    continue;
                DrawNode(rect, GetNodeDisplayName(node),
                    $"SHARED  |  {node.SourceNodeIds.Count} states  |  {CountOutgoing(node.Id)} routes",
                    IsNodeSelected(node.Id), connectingFromId == node.Id, false,
                    false, false, new Color(0.29f, 0.20f, 0.43f),
                    GetNodeValidationError(node.Id), GetDebugNodeState(node.Id));
            }
            for (int i = 0; i < stateMachine.StateMachines.Count; i++)
            {
                AnimStateMachineNode node = stateMachine.StateMachines[i];
                if (node == null || !nodeRects.TryGetValue(node.Id, out Rect rect))
                    continue;
                DrawNode(rect, node.Name, $"{CountChildren(node.Id)} nodes - Double-click to open",
                    IsNodeSelected(node.Id), connectingFromId == node.Id, IsConnectionTarget(node, rect),
                    node.Id == defaultId, false, new Color(0.12f, 0.38f, 0.39f),
                    GetNodeValidationError(node.Id), GetDebugNodeState(node.Id));
            }

            DrawValidationSummary(localRect);
            DrawSelectionBox();
            DrawShortcutOverlay(localRect);
            HandleGraphInput(localRect);
            GUI.EndGroup();
        }

        private DebugNodeState GetDebugNodeState(string nodeId)
        {
            if (!EditorApplication.isPlaying || debugPlayer == null
                || debugPlayer.StateMachine != stateMachine)
                return DebugNodeState.None;
            if (debugPlayer.CurrentState?.Id == nodeId)
                return DebugNodeState.Current;
            if (debugPlayer.NextState?.Id == nodeId)
                return DebugNodeState.Next;
            return DebugNodeState.None;
        }

        private void DrawLiveStatusOverlay()
        {
            const float width = 270f;
            Rect panel = new(10f, 10f, Mathf.Min(width, graphViewportSize.x - 20f), 48f);
            EditorGUI.DrawRect(panel, new Color(0.055f, 0.075f, 0.07f, 0.96f));
            EditorGUI.DrawRect(new Rect(panel.x, panel.y, 3f, panel.height),
                new Color(0.28f, 1f, 0.56f));

            string stateText = debugPlayer?.CurrentState != null
                ? debugPlayer.IsTransitioning
                    ? $"{debugPlayer.CurrentState.Name}  ->  {debugPlayer.NextState?.Name ?? "?"}"
                    : debugPlayer.CurrentState.Name
                : debugPlayer == null ? "No matching Player" : "Waiting for State";
            GUI.Label(new Rect(panel.x + 10f, panel.y + 4f, panel.width - 18f, 18f),
                new GUIContent("LIVE  " + stateText, stateText), EditorStyles.miniBoldLabel);

            float progress = debugPlayer == null
                ? 0f
                : debugPlayer.IsTransitioning
                    ? debugPlayer.TransitionProgress
                    : Mathf.Repeat(debugPlayer.StateNormalizedTime, 1f);
            string detail = debugPlayer == null
                ? "Select a Player from the toolbar"
                : debugPlayer.IsTransitioning
                    ? $"Transition  {progress * 100f:0}%"
                    : $"State Time  {debugPlayer.StateTime:0.00}s";
            GUI.Label(new Rect(panel.x + 10f, panel.y + 23f, panel.width - 18f, 16f),
                detail, EditorStyles.miniLabel);
            Rect progressTrack = new(panel.x + 10f, panel.yMax - 6f, panel.width - 20f, 2f);
            EditorGUI.DrawRect(progressTrack, new Color(1f, 1f, 1f, 0.14f));
            EditorGUI.DrawRect(new Rect(progressTrack.x, progressTrack.y,
                    progressTrack.width * Mathf.Clamp01(progress), progressTrack.height),
                new Color(0.28f, 1f, 0.56f));
        }

        private void AddVisibleNodeRects<T>(IReadOnlyList<T> nodes) where T : AnimStateNode
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                T node = nodes[i];
                if (node != null && node.ParentStateMachineId == currentStateMachineId)
                    nodeRects[node.Id] = ToGraphRect(node.Position, NodeSize);
            }
        }

        private bool IsConnectionTarget(AnimStateNode node, Rect rect) =>
            connectingFromId != null && connectingFromId != node.Id && node is not AnimStateAlias
            && rect.Contains(Event.current.mousePosition);

        private void EnsureValidationCache()
        {
            if (!validationDirty && validatedStateMachine == stateMachine)
                return;

            validationDirty = false;
            validatedStateMachine = stateMachine;
            nodeValidationErrors.Clear();
            transitionValidationErrors.Clear();
            if (stateMachine == null)
                return;

            ValidateNodes(stateMachine.States);
            ValidateNodes(stateMachine.Conduits);
            ValidateNodes(stateMachine.Aliases);
            ValidateNodes(stateMachine.StateMachines);
            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                AnimStateTransition transition = stateMachine.Transitions[i];
                string error = GetTransitionError(transition);
                if (transition != null && !string.IsNullOrEmpty(error))
                    transitionValidationErrors[transition.Id] = error;
            }
        }

        private void ValidateNodes<T>(IReadOnlyList<T> nodes) where T : AnimStateNode
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                AnimStateNode node = nodes[i];
                string error = GetNodeError(node);
                if (node != null && !string.IsNullOrEmpty(error))
                    nodeValidationErrors[node.Id] = error;
            }
        }

        private string GetNodeError(AnimStateNode node) => node switch
        {
            null => "Node data is missing.",
            AnimSequenceState { Sequence: null } => "Animation Sequence is not assigned.",
            AnimSequenceState state when state.Sequence.Clip == null =>
                "Animation Clip is not assigned to the Sequence.",
            AnimStateConduit conduit when CountOutgoing(conduit.Id) == 0 =>
                "Conduit has no outgoing Transition.",
            AnimStateAlias alias when alias.SourceNodeIds.Count == 0 =>
                "Alias has no shared State.",
            AnimStateMachineNode machine when CountChildren(machine.Id) == 0 =>
                "State Machine is empty.",
            _ => string.Empty
        };

        private string GetTransitionError(AnimStateTransition transition)
        {
            if (transition == null)
                return "Transition data is missing.";

            AnimStateNode from = stateMachine.FindNode(transition.FromStateId);
            AnimStateNode to = stateMachine.FindNode(transition.ToStateId);
            if (from == null || to == null)
                return "Source or destination State is missing.";
            if (from.ParentStateMachineId != to.ParentStateMachineId)
                return "Transition crosses State Machine boundaries.";
            if (transition.Timing == AnimStateTransitionTiming.AnimationEnd
                && from is AnimSequenceState { Loop: true })
                return "Animation End cannot be reached by a looping State.";

            bool hasRuleItems = transition.Conditions.Count > 0 || transition.RuleNodes.Count > 0;
            if (!hasRuleItems)
                return string.Empty;
            if (string.IsNullOrEmpty(transition.RuleResultSourceId))
                return "Rule is not connected to Result.";
            if (!HasRuleItem(transition, transition.RuleResultSourceId))
                return "Result is connected to a missing Rule node.";

            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition == null)
                    return "Rule contains a missing Condition.";
                if (condition.Source == AnimStateConditionSource.Parameter)
                {
                    if (FindParameter(condition.Parameter) == null)
                        return $"Parameter '{condition.Parameter}' does not exist.";
                }
                else if (!IsOwnerConditionValid(condition))
                    return "Owner Condition has a missing component type or member.";

                string linkError = GetRuleLinkError(
                    transition, condition.Id, condition.RuleTargetId);
                if (!string.IsNullOrEmpty(linkError))
                    return linkError;
            }

            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node == null)
                    return "Rule contains a missing logic node.";
                string linkError = GetRuleLinkError(transition, node.Id, node.TargetId);
                if (!string.IsNullOrEmpty(linkError))
                    return linkError;

                int inputCount = CountRuleInputs(transition, node.Id);
                int minimumInputs = node.Operation == AnimStateRuleOperator.Not ? 1 : 2;
                if (inputCount < minimumInputs)
                    return $"{node.Operation} requires at least {minimumInputs} input(s).";
                if (node.Operation == AnimStateRuleOperator.Not && inputCount > 1)
                    return "NOT accepts only one input.";
            }

            return string.Empty;
        }

        private static bool HasRuleItem(AnimStateTransition transition, string id)
        {
            for (int i = 0; i < transition.Conditions.Count; i++)
                if (transition.Conditions[i]?.Id == id)
                    return true;
            for (int i = 0; i < transition.RuleNodes.Count; i++)
                if (transition.RuleNodes[i]?.Id == id)
                    return true;
            return false;
        }

        private static bool HasRuleNode(AnimStateTransition transition, string id)
        {
            for (int i = 0; i < transition.RuleNodes.Count; i++)
                if (transition.RuleNodes[i]?.Id == id)
                    return true;
            return false;
        }

        private static string GetRuleLinkError(
            AnimStateTransition transition,
            string sourceId,
            string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
                return transition.RuleResultSourceId == sourceId
                    ? string.Empty
                    : "Rule contains a node that is not connected to Result.";
            return HasRuleNode(transition, targetId)
                ? string.Empty
                : "Rule contains a link to a missing logic node.";
        }

        private static bool IsOwnerConditionValid(AnimStateCondition condition)
        {
            if (string.IsNullOrEmpty(condition.OwnerType)
                || string.IsNullOrEmpty(condition.OwnerMember))
                return false;
            Type type = Type.GetType(condition.OwnerType, false);
            if (type == null || !typeof(Component).IsAssignableFrom(type)
                || condition.OwnerMember.Length < 3 || condition.OwnerMember[1] != ':')
                return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
                                       | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            char kind = condition.OwnerMember[0];
            string memberName = condition.OwnerMember.Substring(2);
            for (Type current = type;
                 current != null && current != typeof(MonoBehaviour);
                 current = current.BaseType)
            {
                if (kind == 'F' && current.GetField(memberName, flags) != null
                    || kind == 'P' && current.GetProperty(memberName, flags) != null)
                    return true;
                if (kind != 'M')
                    continue;
                MethodInfo[] methods = current.GetMethods(flags);
                for (int i = 0; i < methods.Length; i++)
                    if (methods[i].Name == memberName && methods[i].GetParameters().Length == 0)
                        return true;
            }
            return false;
        }

        private string GetNodeValidationError(string nodeId) =>
            nodeValidationErrors.TryGetValue(nodeId, out string error) ? error : string.Empty;

        private string GetTransitionValidationError(string transitionId) =>
            transitionValidationErrors.TryGetValue(transitionId, out string error) ? error : string.Empty;

        private int CountOutgoing(string nodeId)
        {
            int count = 0;
            for (int i = 0; i < stateMachine.Transitions.Count; i++)
                if (stateMachine.Transitions[i].FromStateId == nodeId)
                    count++;
            return count;
        }

        private int CountChildren(string parentId)
        {
            int count = 0;
            for (int i = 0; i < stateMachine.States.Count; i++)
                if (stateMachine.States[i].ParentStateMachineId == parentId) count++;
            for (int i = 0; i < stateMachine.Conduits.Count; i++)
                if (stateMachine.Conduits[i].ParentStateMachineId == parentId) count++;
            for (int i = 0; i < stateMachine.Aliases.Count; i++)
                if (stateMachine.Aliases[i].ParentStateMachineId == parentId) count++;
            for (int i = 0; i < stateMachine.StateMachines.Count; i++)
                if (stateMachine.StateMachines[i].ParentStateMachineId == parentId) count++;
            return count;
        }

        private void DrawEntryTransition(Rect entryRect)
        {
            if (!nodeRects.TryGetValue(stateMachine.GetDefaultNodeId(currentStateMachineId), out Rect defaultRect))
                return;
            Handles.BeginGUI();
            GetTransitionCurve(entryRect, defaultRect, out Vector3 start, out Vector3 end,
                out Vector3 startTangent, out Vector3 endTangent);
            Color color = new(0.35f, 0.78f, 0.45f);
            Handles.DrawBezier(start, end, startTangent, endTangent, color, null, 2.5f);
            DrawArrow(end, end - endTangent, color, 9f);
            Handles.EndGUI();
        }
        private void DrawTransitions()
        {
            Handles.BeginGUI();
            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                AnimStateTransition transition = stateMachine.Transitions[i];
                if (transition == null
                    || !nodeRects.TryGetValue(transition.FromStateId, out Rect from)
                    || !nodeRects.TryGetValue(transition.ToStateId, out Rect to))
                    continue;

                float routeOffset = HasReverseTransition(transition) ? 14f : 0f;
                GetTransitionCurve(from, to, out Vector3 start, out Vector3 end,
                    out Vector3 startTangent, out Vector3 endTangent, routeOffset);
                bool selected = selectedTransitionIds.Contains(transition.Id);
                bool runtimeActive = EditorApplication.isPlaying && debugPlayer != null
                                     && debugPlayer.ActiveTransition?.Id == transition.Id;
                string validationError = GetTransitionValidationError(transition.Id);
                bool invalid = !string.IsNullOrEmpty(validationError);
                Color lineColor = invalid
                    ? new Color(0.95f, 0.25f, 0.24f)
                    : runtimeActive ? new Color(0.3f, 1f, 0.58f)
                    : selected ? SelectionColor : new Color(0.57f, 0.66f, 0.8f);
                float lineWidth = runtimeActive ? 5f : selected ? 4f : 2.2f;
                Handles.DrawBezier(start, end, startTangent, endTangent, lineColor, null, lineWidth);
                DrawArrow(end, end - endTangent, lineColor, runtimeActive || selected ? 11f : 9f);
                if (runtimeActive)
                    DrawRuntimeTransitionPulse(start, startTangent, endTangent, end);

                string timingLabel = transition.Timing switch
                {
                    AnimStateTransitionTiming.AnimationEnd => "End",
                    AnimStateTransitionTiming.ExitTime => $"Exit {transition.ExitTime:0.##}",
                    _ => "Now"
                };
                string ruleLabel = string.IsNullOrEmpty(transition.RuleResultSourceId) ? "" : " | Rule";
                string tooltip = (invalid ? $"ERROR: {validationError}\n" : string.Empty)
                                 + $"{timingLabel}{ruleLabel} | Blend {transition.Duration:0.##}s\n"
                                 + "Click to select. Double-click to open Rule.";
                Rect selectorRect = DrawTransitionSelector(
                    start, startTangent, endTangent, end, selected, invalid, runtimeActive, tooltip);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && selectorRect.Contains(Event.current.mousePosition))
                {
                    bool toggle = Event.current.control || Event.current.command || Event.current.shift;
                    UpdateClickedTransitionSelection(transition.Id, toggle);
                    if (Event.current.clickCount == 2)
                    {
                        OpenTransitionRule(transition.Id);
                    }
                    showInspector = true;
                    Event.current.Use();
                    Repaint();
                }
            }

            if (connectingFromId != null && nodeRects.TryGetValue(connectingFromId, out Rect sourceRect))
            {
                Vector3 start = GetPortCenter(sourceRect, connectingDirection);
                Vector3 end = Event.current.mousePosition;
                Vector2 targetDirection = GetClosestSideDirection(end, sourceRect.center);
                float tangent = Mathf.Max(45f, Vector2.Distance(start, end) * 0.35f);
                Vector3 startTangent = start + (Vector3)connectingDirection * tangent;
                Vector3 endTangent = end + (Vector3)targetDirection * tangent;
                Handles.DrawBezier(start, end, startTangent, endTangent,
                    new Color(1f, 0.72f, 0.2f), null, 3f);
                DrawArrow(end, end - endTangent, new Color(1f, 0.72f, 0.2f), 9f);
            }
            Handles.EndGUI();
        }

        private static Rect DrawTransitionSelector(
            Vector3 start,
            Vector3 startTangent,
            Vector3 endTangent,
            Vector3 end,
            bool selected,
            bool invalid,
            bool runtimeActive,
            string tooltip)
        {
            Vector2 center = EvaluateBezier(start, startTangent, endTangent, end, 0.5f);
            Vector2 direction = EvaluateBezierTangent(
                start, startTangent, endTangent, end, 0.5f).normalized;
            const float radius = 11f;
            Rect rect = new(center.x - radius, center.y - radius, radius * 2f, radius * 2f);

            Handles.color = runtimeActive
                ? new Color(0.3f, 1f, 0.58f)
                : selected
                ? SelectionColor
                : new Color(0.02f, 0.025f, 0.035f, 0.96f);
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Color fill = invalid
                ? new Color(0.95f, 0.25f, 0.24f)
                : runtimeActive ? new Color(0.12f, 0.52f, 0.3f)
                : selected ? SelectionColor : new Color(0.42f, 0.55f, 0.72f);
            Handles.color = fill;
            Handles.DrawSolidDisc(center, Vector3.forward, radius - 2f);
            DrawArrow(center + direction * 5f, direction, Color.white, 7f);
            GUI.Label(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
            Handles.color = Color.white;
            return rect;
        }

        private static void DrawRuntimeTransitionPulse(
            Vector3 start,
            Vector3 startTangent,
            Vector3 endTangent,
            Vector3 end)
        {
            float time = Mathf.Repeat((float)EditorApplication.timeSinceStartup * 0.9f, 1f);
            Vector2 position = EvaluateBezier(start, startTangent, endTangent, end, time);
            Handles.color = new Color(0.72f, 1f, 0.82f, 0.95f);
            Handles.DrawSolidDisc(position, Vector3.forward, 4.5f);
            Handles.color = Color.white;
        }

        private bool HasReverseTransition(AnimStateTransition transition)
        {
            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                AnimStateTransition candidate = stateMachine.Transitions[i];
                if (candidate != null
                    && candidate.FromStateId == transition.ToStateId
                    && candidate.ToStateId == transition.FromStateId)
                    return true;
            }

            return false;
        }

        private static void GetTransitionCurve(
            Rect from,
            Rect to,
            out Vector3 start,
            out Vector3 end,
            out Vector3 startTangent,
            out Vector3 endTangent,
            float routeOffset = 0f)
        {
            Vector2 sourceDirection = GetClosestSideDirection(from.center, to.center);
            Vector2 targetDirection = -sourceDirection;
            Vector2 routeShift = new Vector2(-sourceDirection.y, sourceDirection.x) * routeOffset;
            start = GetPortCenter(from, sourceDirection) + routeShift;
            end = GetPortCenter(to, targetDirection) + routeShift;
            float tangent = Mathf.Max(45f, Vector2.Distance(start, end) * 0.35f);
            startTangent = start + (Vector3)sourceDirection * tangent;
            endTangent = end + (Vector3)targetDirection * tangent;
        }

        private static Vector2 GetClosestSideDirection(Vector2 origin, Vector2 target)
        {
            Vector2 delta = target - origin;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x >= 0f ? Vector2.right : Vector2.left;
            return delta.y >= 0f ? GraphDown : GraphUp;
        }
        private static void DrawArrow(Vector3 tip, Vector3 direction, Color color, float size)
        {
            if (direction.sqrMagnitude < 0.001f)
                return;
            Vector3 forward = direction.normalized;
            Vector3 side = new(-forward.y, forward.x, 0f);
            Handles.color = color;
            Handles.DrawAAConvexPolygon(
                tip,
                tip - forward * size + side * size * 0.52f,
                tip - forward * size - side * size * 0.52f);
            Handles.color = Color.white;
        }

        private static Vector2 EvaluateBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * a
                + 3f * inverse * inverse * t * b
                + 3f * inverse * t * t * c
                + t * t * t * d;
        }

        private static Vector2 EvaluateBezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float inverse = 1f - t;
            return 3f * inverse * inverse * (b - a)
                + 6f * inverse * t * (c - b)
                + 3f * t * t * (d - c);
        }

        private static void DrawNode(
            Rect rect,
            string title,
            string subtitle,
            bool selected,
            bool connectionSource,
            bool connectionTarget,
            bool isDefault,
            bool loop,
            Color color,
            string validationError,
            DebugNodeState debugState)
        {
            bool invalid = !string.IsNullOrEmpty(validationError);
            Color border = invalid
                ? selected ? SelectionColor : new Color(0.98f, 0.2f, 0.18f)
                : debugState == DebugNodeState.Current
                    ? new Color(0.28f, 1f, 0.56f)
                    : debugState == DebugNodeState.Next
                        ? new Color(0.22f, 0.82f, 1f)
                : connectionTarget
                ? new Color(0.24f, 0.88f, 0.56f)
                : selected
                    ? SelectionColor
                    : connectionSource
                        ? new Color(0.95f, 0.58f, 0.2f)
                        : new Color(0f, 0f, 0f, 0.7f);
            EditorGUI.DrawRect(rect, border);
            Rect body = new(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            Color bodyColor = invalid
                ? Color.Lerp(color, new Color(0.48f, 0.06f, 0.07f), 0.62f)
                : debugState == DebugNodeState.Current
                    ? Color.Lerp(color, new Color(0.08f, 0.45f, 0.22f), 0.45f)
                    : debugState == DebugNodeState.Next
                        ? Color.Lerp(color, new Color(0.06f, 0.34f, 0.5f), 0.38f)
                : color;
            EditorGUI.DrawRect(body, bodyColor);
            float headerHeight = Mathf.Min(28f, body.height);
            Rect header = new(body.x, body.y, body.width, headerHeight);
            EditorGUI.DrawRect(header, Color.Lerp(bodyColor, Color.black, 0.16f));

            string badge = invalid ? "ERROR"
                : debugState == DebugNodeState.Current ? "ACTIVE"
                : debugState == DebugNodeState.Next ? "NEXT"
                : isDefault ? "DEFAULT" : loop ? "LOOP" : string.Empty;
            GUIContent badgeContent = new(badge);
            float badgeWidth = string.IsNullOrEmpty(badge)
                ? 0f
                : Mathf.Ceil(RightMiniLabel.CalcSize(badgeContent).x) + 10f;
            bool showBadge = badgeWidth > 0f && header.width >= badgeWidth + 78f;
            float titleRightPadding = showBadge ? badgeWidth + 12f : 12f;
            GUI.Label(new Rect(header.x + 9f, header.y + 4f,
                    Mathf.Max(1f, header.width - titleRightPadding), Mathf.Min(20f, header.height)),
                new GUIContent(title, title), EditorStyles.boldLabel);
            if (showBadge)
            {
                Rect badgeRect = new(header.xMax - badgeWidth - 5f, header.y + 5f, badgeWidth, 18f);
                GUI.Label(badgeRect, badgeContent, RightMiniLabel);
            }
            if (body.height >= 58f)
            {
                GUI.Label(new Rect(body.x + 9f, body.y + 34f, body.width - 18f, body.height - 38f),
                    new GUIContent(subtitle, subtitle), WrappedMiniLabel);
            }
            if (invalid)
                GUI.Label(rect, new GUIContent(string.Empty, validationError), GUIStyle.none);

            Rect portHoverRect = new(rect.x - 10f, rect.y - 10f, rect.width + 20f, rect.height + 20f);
            bool showPorts = selected || connectionSource || connectionTarget
                || portHoverRect.Contains(Event.current.mousePosition);
            if (title != "Entry" && showPorts)
            {
                Color portColor = connectionTarget
                    ? new Color(0.24f, 0.88f, 0.56f)
                    : connectionSource
                        ? new Color(1f, 0.72f, 0.2f)
                        : new Color(0.72f, 0.79f, 0.9f);
                DrawTransitionPorts(rect, portColor);
            }
        }

        private void DrawValidationSummary(Rect graphRect)
        {
            int count = 0;
            foreach (KeyValuePair<string, Rect> pair in nodeRects)
            {
                if (pair.Key != EntryNodeId && nodeValidationErrors.ContainsKey(pair.Key))
                    count++;
            }
            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                AnimStateTransition transition = stateMachine.Transitions[i];
                if (transition != null && transitionValidationErrors.ContainsKey(transition.Id)
                    && nodeRects.ContainsKey(transition.FromStateId)
                    && nodeRects.ContainsKey(transition.ToStateId))
                    count++;
            }
            if (count == 0)
                return;

            string text = count == 1 ? "1 Issue" : $"{count} Issues";
            Rect badge = new(graphRect.xMax - 96f, 10f, 86f, 22f);
            EditorGUI.DrawRect(badge, new Color(0.52f, 0.08f, 0.09f, 0.96f));
            GUI.Label(badge, new GUIContent(text, "Red nodes and transitions need attention."),
                CenteredMiniLabel);
        }
        private static void DrawTransitionPorts(Rect rect, Color color)
        {
            DrawPort(GetPortRect(rect, Vector2.left), color);
            DrawPort(GetPortRect(rect, Vector2.right), color);
            DrawPort(GetPortRect(rect, GraphUp), color);
            DrawPort(GetPortRect(rect, GraphDown), color);
        }
        private static void DrawPort(Rect rect, Color color)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0f, 0f, 0f, 0.85f);
            Handles.DrawSolidDisc(rect.center, Vector3.forward, 7f);
            Handles.color = color;
            Handles.DrawSolidDisc(rect.center, Vector3.forward, 5f);
            Handles.color = Color.white;
            Handles.EndGUI();
        }
        private void HandleGraphInput(Rect graphRect)
        {
            Event evt = Event.current;
            if (EditorApplication.isPlaying)
                return;

            if (evt.type == EventType.MouseLeaveWindow)
            {
                CancelStateGraphInteractions();
                Repaint();
                return;
            }
            if (!graphRect.Contains(evt.mousePosition))
            {
                if (boxSelecting || !string.IsNullOrEmpty(draggingStateId)
                    || draggingTransition || rightMousePressed)
                {
                    CancelStateGraphInteractions();
                    Repaint();
                }
                return;
            }

            if (evt.type == EventType.ScrollWheel)
            {
                ZoomAt(evt.mousePosition, -evt.delta.y * 0.05f);
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type is EventType.DragUpdated or EventType.DragPerform)
            {
                AnimSequenceSO sequence = GetDraggedSequence();
                if (sequence != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        AddState(sequence, ScreenToGraph(evt.mousePosition) - NodeSize * 0.5f);
                    }
                    evt.Use();
                    return;
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                rightMousePressed = true;
                rightMousePanning = false;
                rightMouseDownPosition = evt.mousePosition;
                rightMouseLastPosition = evt.mousePosition;
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 1 && rightMousePressed)
            {
                if (!rightMousePanning
                    && (evt.mousePosition - rightMouseDownPosition).sqrMagnitude >= 16f)
                    rightMousePanning = true;
                if (rightMousePanning)
                {
                    pan += evt.mousePosition - rightMouseLastPosition;
                    Repaint();
                }
                rightMouseLastPosition = evt.mousePosition;
                evt.Use();
                return;
            }

            if ((evt.type == EventType.MouseUp && evt.button == 1
                 || evt.type == EventType.ContextClick) && rightMousePressed)
            {
                bool openMenu = !rightMousePanning;
                rightMousePressed = false;
                rightMousePanning = false;
                if (openMenu)
                {
                    AnimStateNode clicked = FindNodeAt(evt.mousePosition);
                    if (clicked != null)
                    {
                        if (!IsNodeSelected(clicked.Id))
                            SelectOnlyNode(clicked.Id);
                        ShowNodeContextMenu(clicked);
                    }
                    else
                        ShowGraphContextMenu(ScreenToGraph(evt.mousePosition));
                }
                evt.Use();
                Repaint();
                return;
            }
            if (evt.type == EventType.ContextClick)
            {
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                foreach (KeyValuePair<string, Rect> pair in nodeRects)
                {
                    if (pair.Key == EntryNodeId
                        || !TryGetTransitionPort(pair.Value, evt.mousePosition, out Vector2 direction))
                        continue;

                    SelectOnlyNode(pair.Key);
                    connectingFromId = pair.Key;
                    connectingDirection = direction;
                    draggingTransition = true;
                    evt.Use();
                    Repaint();
                    return;
                }

                Rect entryRect = nodeRects[EntryNodeId];
                if (entryRect.Contains(evt.mousePosition))
                {
                    ClearSelection();
                    FinishMoveUndo();
                    draggingStateId = EntryNodeId;
                    dragOffset = (evt.mousePosition - entryRect.position) / graphZoom;
                    evt.Use();
                    Repaint();
                    return;
                }

                AnimStateNode node = FindNodeAt(evt.mousePosition);
                if (node != null)
                {
                    if (connectingFromId != null && !draggingTransition && node is not AnimStateAlias)
                    {
                        CreateTransition(connectingFromId, node.Id);
                        connectingFromId = null;
                    }
                    else
                    {
                        bool toggle = evt.control || evt.command || evt.shift;
                        if (!toggle)
                            ClearTransitionSelection();
                        bool selectedAfterClick = UpdateClickedSelection(node.Id, toggle);
                        showInspector = true;
                        if (selectedAfterClick)
                            BeginNodeDrag(node.Id, evt.mousePosition);

                        if (evt.clickCount == 2 && selectedAfterClick)
                        {
                            if (node is AnimStateMachineNode)
                                EnterStateMachine(node.Id);
                            else if (node is AnimSequenceState state && state.Sequence != null)
                                AnimationEditorWindow.Open(state.Sequence, library);
                        }
                    }
                    evt.Use();
                    Repaint();
                    return;
                }

                boxSelecting = true;
                boxSelectionAdditive = evt.control || evt.command || evt.shift;
                boxStart = evt.mousePosition;
                boxEnd = boxStart;
                if (!boxSelectionAdditive)
                    ClearSelection();
                GUI.FocusControl(null);
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                if (draggingTransition)
                {
                    evt.Use();
                    Repaint();
                    return;
                }
                if (draggingStateId == EntryNodeId)
                {
                    Vector2 next = ScreenToGraph(evt.mousePosition) - dragOffset;
                    Vector2 target = SnapPosition(next);
                    if (stateMachine.GetEntryPosition(currentStateMachineId) == target)
                    {
                        evt.Use();
                        return;
                    }
                    BeginMoveUndo("Move Entry");
                    stateMachine.SetEntryPosition(currentStateMachineId, target);
                    Save();
                    evt.Use();
                    return;
                }
                if (!string.IsNullOrEmpty(draggingStateId))
                {
                    MoveSelectedNodes(evt.mousePosition);
                    evt.Use();
                    return;
                }
                if (boxSelecting)
                {
                    boxEnd = evt.mousePosition;
                    evt.Use();
                    Repaint();
                    return;
                }
            }

            if (evt.type == EventType.MouseUp && evt.button == 0 && draggingTransition)
            {
                AnimStateNode target = FindNodeAt(evt.mousePosition);
                if (target is not null and not AnimStateAlias)
                    CreateTransition(connectingFromId, target.Id);
                connectingFromId = null;
                draggingTransition = false;
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseUp && evt.button == 0
                && !string.IsNullOrEmpty(draggingStateId))
            {
                FinishMoveUndo();
                draggingStateId = null;
                dragStartPositions.Clear();
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseUp && evt.button == 0 && boxSelecting)
            {
                boxEnd = evt.mousePosition;
                FinishBoxSelection();
                boxSelecting = false;
                evt.Use();
                Repaint();
            }
        }

        private static void DrawShortcutOverlay(Rect graphRect)
        {
            string text = graphRect.width >= 720f
                ? "Wheel Zoom  |  RMB Pan  |  Ctrl+C/V  |  Ctrl+A All  |  F Frame  |  G Grid  |  Shift+Alt Align  |  Del"
                : "Wheel Zoom  |  RMB Pan  |  Ctrl+C/V  |  Ctrl+A  |  F Frame  |  Del";
            const string tooltip =
                "Wheel: Zoom\nRMB Drag: Pan\nCtrl+A: Select all\nF: Frame selection\nG: Grid layout\nShift+S: Snap\nAlt+Arrows: Align edges\nCtrl+Shift+H/V: Align centers\nShift+H/V: Distribute\nDelete: Delete selection";
            float width = Mathf.Min(graphRect.width - 16f,
                EditorStyles.miniLabel.CalcSize(new GUIContent(text)).x + 18f);
            if (width <= 24f)
                return;

            Rect overlay = new(8f, graphRect.height - 27f, width, 20f);
            EditorGUI.DrawRect(overlay, new Color(0.055f, 0.06f, 0.07f, 0.88f));
            GUI.Label(overlay, new GUIContent(text, tooltip), CenteredMiniLabel);
        }
        private void DrawSelectionBox()
        {
            if (!boxSelecting || Event.current.type != EventType.Repaint)
                return;
            Rect rect = GetSelectionRect();
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.55f, 0.95f, 0.12f));
            Handles.BeginGUI();
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, new Color(0.35f, 0.7f, 1f, 0.9f));
            Handles.EndGUI();
        }

        private Rect GetSelectionRect() => Rect.MinMaxRect(
            Mathf.Min(boxStart.x, boxEnd.x),
            Mathf.Min(boxStart.y, boxEnd.y),
            Mathf.Max(boxStart.x, boxEnd.x),
            Mathf.Max(boxStart.y, boxEnd.y));

        private void FinishBoxSelection()
        {
            Rect selection = GetSelectionRect();
            if (selection.width < 3f && selection.height < 3f)
                return;

            foreach (KeyValuePair<string, Rect> pair in nodeRects)
            {
                if (pair.Key == EntryNodeId || !selection.Overlaps(pair.Value, true))
                    continue;
                selectedNodeIds.Add(pair.Key);
                selectedNodeId = pair.Key;
            }

            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                AnimStateTransition transition = stateMachine.Transitions[i];
                if (transition == null
                    || !nodeRects.TryGetValue(transition.FromStateId, out Rect from)
                    || !nodeRects.TryGetValue(transition.ToStateId, out Rect to)
                    || !TransitionOverlapsSelection(selection, from, to,
                        HasReverseTransition(transition) ? 14f : 0f))
                    continue;
                selectedTransitionIds.Add(transition.Id);
                selectedTransitionId = transition.Id;
            }
            showInspector = selectedNodeIds.Count + selectedTransitionIds.Count > 0;
        }

        private static bool TransitionOverlapsSelection(
            Rect selection, Rect from, Rect to, float routeOffset)
        {
            GetTransitionCurve(from, to, out Vector3 start, out Vector3 end,
                out Vector3 startTangent, out Vector3 endTangent, routeOffset);
            const int sampleCount = 32;
            for (int i = 0; i <= sampleCount; i++)
            {
                float time = i / (float)sampleCount;
                if (selection.Contains(EvaluateBezier(start, startTangent, endTangent, end, time)))
                    return true;
            }
            return false;
        }

        private bool UpdateClickedSelection(string nodeId, bool toggle)
        {
            if (toggle)
            {
                if (selectedNodeIds.Remove(nodeId))
                {
                    selectedNodeId = GetAnySelectedNodeId();
                    return false;
                }
                selectedNodeIds.Add(nodeId);
                selectedNodeId = nodeId;
                return true;
            }

            if (!selectedNodeIds.Contains(nodeId))
                SelectOnlyNode(nodeId);
            else
                selectedNodeId = nodeId;
            return true;
        }

        private void SelectOnlyNode(string nodeId)
        {
            ClearTransitionSelection();
            selectedNodeIds.Clear();
            if (!string.IsNullOrEmpty(nodeId))
                selectedNodeIds.Add(nodeId);
            selectedNodeId = nodeId;
        }

        private void UpdateClickedTransitionSelection(string transitionId, bool toggle)
        {
            if (!toggle)
            {
                ClearNodeSelection();
                selectedTransitionIds.Clear();
                selectedTransitionIds.Add(transitionId);
                selectedTransitionId = transitionId;
                return;
            }

            if (selectedTransitionIds.Remove(transitionId))
                selectedTransitionId = GetAnySelectedTransitionId();
            else
            {
                selectedTransitionIds.Add(transitionId);
                selectedTransitionId = transitionId;
            }
        }

        private void SelectOnlyTransition(string transitionId)
        {
            ClearNodeSelection();
            selectedTransitionIds.Clear();
            if (!string.IsNullOrEmpty(transitionId))
                selectedTransitionIds.Add(transitionId);
            selectedTransitionId = transitionId;
        }

        private void ClearNodeSelection()
        {
            selectedNodeIds.Clear();
            selectedNodeId = null;
        }

        private void ClearTransitionSelection()
        {
            selectedTransitionIds.Clear();
            selectedTransitionId = null;
        }

        private void ClearSelection()
        {
            ClearNodeSelection();
            ClearTransitionSelection();
        }

        private bool IsNodeSelected(string nodeId) => selectedNodeIds.Contains(nodeId);

        private string GetAnySelectedTransitionId()
        {
            foreach (string id in selectedTransitionIds)
                return id;
            return null;
        }

        private string GetAnySelectedNodeId()
        {
            foreach (string id in selectedNodeIds)
                return id;
            return null;
        }

        private void BeginNodeDrag(string nodeId, Vector2 mousePosition)
        {
            FinishMoveUndo();
            draggingStateId = nodeId;
            nodeDragStartMouse = mousePosition;
            dragStartPositions.Clear();
            foreach (string selectedId in selectedNodeIds)
            {
                AnimStateNode selected = stateMachine.FindNode(selectedId);
                if (selected != null)
                    dragStartPositions[selectedId] = selected.Position;
            }
        }

        private void MoveSelectedNodes(Vector2 mousePosition)
        {
            if (!dragStartPositions.TryGetValue(draggingStateId, out Vector2 primaryStart))
                return;

            Vector2 delta = (mousePosition - nodeDragStartMouse) / graphZoom;
            Vector2 primaryTarget = SnapPosition(primaryStart + delta);
            Vector2 appliedDelta = primaryTarget - primaryStart;
            bool changed = false;
            foreach (KeyValuePair<string, Vector2> item in dragStartPositions)
            {
                AnimStateNode node = stateMachine.FindNode(item.Key);
                if (node != null && node.Position != item.Value + appliedDelta)
                {
                    changed = true;
                    break;
                }
            }
            if (!changed)
                return;

            BeginMoveUndo(selectedNodeIds.Count > 1
                ? "Move Animation Nodes"
                : "Move Animation Node");
            foreach (KeyValuePair<string, Vector2> item in dragStartPositions)
            {
                AnimStateNode node = stateMachine.FindNode(item.Key);
                if (node != null)
                    node.Position = item.Value + appliedDelta;
            }
            Save();
        }

        private void BeginMoveUndo(string name)
        {
            if (moveUndoGroup >= 0 || stateMachine == null)
                return;

            Undo.IncrementCurrentGroup();
            moveUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            Undo.RegisterCompleteObjectUndo(stateMachine, name);
        }

        private void FinishMoveUndo()
        {
            if (moveUndoGroup < 0)
                return;

            Undo.CollapseUndoOperations(moveUndoGroup);
            moveUndoGroup = -1;
        }

        private Vector2 SnapPosition(Vector2 value)
        {
            return snapToGrid ? GetGridPosition(value) : value;
        }

        private Vector2 GetGridPosition(Vector2 value)
        {
            if (graphGridSize <= 0f)
                return value;
            return new Vector2(
                Mathf.Round(value.x / graphGridSize) * graphGridSize,
                Mathf.Round(value.y / graphGridSize) * graphGridSize);
        }
        private AnimStateNode FindNodeAt(Vector2 mousePosition)
        {
            foreach (KeyValuePair<string, Rect> pair in nodeRects)
            {
                if (pair.Key != EntryNodeId && pair.Value.Contains(mousePosition))
                    return stateMachine.FindNode(pair.Key);
            }
            return null;
        }
        private void DrawInspector(Rect rect)
        {
            EnsureValidationCache();
            EditorGUI.DrawRect(rect, PanelBackground);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.55f));
            GUILayout.BeginArea(new Rect(rect.x + 9f, rect.y + 8f, rect.width - 18f, rect.height - 16f));
            AnimStateNode node = stateMachine.FindNode(GetAnySelectedNodeId());
            AnimStateTransition transition = FindTransition(selectedTransitionId);
            int selectionCount = selectedNodeIds.Count + selectedTransitionIds.Count;
            bool multiSelection = selectionCount > 1;
            string title = multiSelection ? selectionCount + " Items Selected"
                : node != null ? GetNodeTypeName(node) + " Details"
                : transition != null ? "Transition Details" : "Details";
            GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.Space(4f);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(rect.width * 0.34f, 72f, 96f);
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);
            if (multiSelection)
            {
                int invalidCount = CountInvalidSelection();
                if (invalidCount > 0)
                    EditorGUILayout.HelpBox($"{invalidCount} selected item(s) need attention.",
                        MessageType.Error);
            }
            else
            {
                string validationError = node != null
                    ? GetNodeValidationError(node.Id)
                    : transition != null
                        ? GetTransitionValidationError(transition.Id)
                        : string.Empty;
                if (!string.IsNullOrEmpty(validationError))
                    EditorGUILayout.HelpBox(validationError, MessageType.Error);
            }
            if (multiSelection)
                DrawMultiSelectionInspector();
            else if (node is AnimSequenceState state)
                DrawStateInspector(state);
            else if (node is AnimStateConduit conduit)
                DrawSimpleNodeInspector(conduit,
                    "실행 흐름이 이 노드에 들어오면 조건을 만족한 첫 번째 Transition으로 즉시 이동합니다.", true);
            else if (node is AnimStateAlias alias)
                DrawAliasInspector(alias);
            else if (node is AnimStateMachineNode machine)
                DrawStateMachineInspector(machine);
            else if (transition != null)
                DrawTransitionInspector(transition);
            else
                EditorGUILayout.HelpBox("Select a node or transition.", MessageType.None);
            GUILayout.EndScrollView();
            EditorGUIUtility.labelWidth = previousLabelWidth;
            GUILayout.EndArea();
        }

        private int CountInvalidSelection()
        {
            int count = 0;
            foreach (string nodeId in selectedNodeIds)
                if (!string.IsNullOrEmpty(GetNodeValidationError(nodeId)))
                    count++;
            foreach (string transitionId in selectedTransitionIds)
                if (!string.IsNullOrEmpty(GetTransitionValidationError(transitionId)))
                    count++;
            return count;
        }

        private void DrawMultiSelectionInspector()
        {
            EditorGUILayout.HelpBox(
                $"Nodes {selectedNodeIds.Count}  |  Transitions {selectedTransitionIds.Count}",
                MessageType.None);
            using (new EditorGUI.DisabledScope(selectedNodeIds.Count < 2))
            {
                if (GUILayout.Button("Arrange", GUILayout.Height(25f)))
                    ShowArrangeMenu();
            }
            using (new EditorGUI.DisabledScope(selectedNodeIds.Count == 0))
            {
                if (GUILayout.Button("Snap To Grid", GUILayout.Height(24f)))
                    SnapSelectedNodes();
            }
            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Delete Selected", GUILayout.Height(24f)))
            {
                DeleteSelectedItems();
                GUIUtility.ExitGUI();
            }
        }
        private void DrawSimpleNodeInspector(AnimStateNode node, string help, bool canBeDefault)
        {
            DrawNodeName(node);
            EditorGUILayout.HelpBox(help, MessageType.Info);
            DrawNodeActions(node, canBeDefault);
        }

        private void DrawAliasInspector(AnimStateAlias alias)
        {
            DrawNodeName(alias);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Alias States", EditorStyles.miniBoldLabel);
            bool hasSource = false;
            for (int i = 0; i < stateMachine.States.Count; i++)
            {
                AnimSequenceState source = stateMachine.States[i];
                if (source.ParentStateMachineId != currentStateMachineId)
                    continue;
                hasSource = true;
                DrawAliasSource(alias, source);
            }
            for (int i = 0; i < stateMachine.StateMachines.Count; i++)
            {
                AnimStateMachineNode source = stateMachine.StateMachines[i];
                if (source.ParentStateMachineId != currentStateMachineId)
                    continue;
                hasSource = true;
                DrawAliasSource(alias, source);
            }
            if (!hasSource)
                EditorGUILayout.HelpBox("Alias에 포함할 State 또는 State Machine이 없습니다.", MessageType.Warning);
            EditorGUILayout.HelpBox(
                "선택한 State들은 이 그룹에서 나가는 Transition을 함께 검사합니다. "
                + "실행 흐름이 이 노드를 직접 통과하지는 않습니다.", MessageType.Info);
            DrawNodeActions(alias, false);
        }

        private void DrawAliasSource(AnimStateAlias alias, AnimStateNode source)
        {
            bool current = alias.Contains(source.Id);
            bool next = EditorGUILayout.ToggleLeft(source.Name, current);
            if (next == current)
                return;
            Undo.RecordObject(stateMachine, "Edit Alias");
            if (next)
                alias.AddSource(source.Id);
            else
                alias.RemoveSource(source.Id);
            Save();
        }

        private void DrawStateMachineInspector(AnimStateMachineNode machine)
        {
            DrawNodeName(machine);
            EditorGUILayout.HelpBox(
                $"{CountChildren(machine.Id)} nodes. This State Machine has its own Entry and default route.",
                MessageType.None);
            if (GUILayout.Button("Open State Machine", GUILayout.Height(25f)))
            {
                EnterStateMachine(machine.Id);
                GUIUtility.ExitGUI();
            }
            DrawNodeActions(machine, true);
        }

        private void DrawNodeName(AnimStateNode node)
        {
            string nextName = EditorGUILayout.DelayedTextField("Name", node.Name);
            if (nextName == node.Name)
                return;
            Undo.RecordObject(stateMachine, "Rename Animation Node");
            node.Name = nextName;
            Save();
        }

        private void DrawNodeActions(AnimStateNode node, bool canBeDefault)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Actions", EditorStyles.miniBoldLabel);
            if (canBeDefault)
            {
                using (new EditorGUI.DisabledScope(
                           node.Id == stateMachine.GetDefaultNodeId(currentStateMachineId)))
                {
                    if (GUILayout.Button("Set as Default State", GUILayout.Height(24f)))
                    {
                        Undo.RecordObject(stateMachine, "Set Default Animation Node");
                        stateMachine.SetDefaultNode(currentStateMachineId, node.Id);
                        Save();
                    }
                }
            }
            if (GUILayout.Button($"Delete {GetNodeTypeName(node)}", GUILayout.Height(24f)))
            {
                DeleteNode(node.Id);
                GUIUtility.ExitGUI();
            }
        }
        private void DrawStateInspector(AnimSequenceState state)
        {
            DrawNodeName(state);
            AnimSequenceSO nextSequence = (AnimSequenceSO)EditorGUILayout.ObjectField(
                "Sequence", state.Sequence, typeof(AnimSequenceSO), false);
            if (nextSequence != null)
            {
                string clipName = nextSequence.Clip != null ? nextSequence.Clip.name : "Clip not assigned";
                EditorGUILayout.HelpBox($"{clipName}   |   {nextSequence.Length:0.###}s", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Assign or drag an Animation Sequence.", MessageType.Warning);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Playback", EditorStyles.miniBoldLabel);
            float nextSpeed = EditorGUILayout.FloatField("Speed", state.Speed);
            bool nextLoop = EditorGUILayout.Toggle("Loop", state.Loop);
            if (nextSequence != state.Sequence || !Mathf.Approximately(nextSpeed, state.Speed)
                || nextLoop != state.Loop)
            {
                Undo.RecordObject(stateMachine, "Edit Animation State");
                state.Sequence = nextSequence;
                state.Speed = nextSpeed;
                state.Loop = nextLoop;
                Save();
            }
            DrawNodeActions(state, true);
        }
        private void DrawTransitionRuleWorkspace(Rect rect)
        {
            AnimStateTransition transition = FindTransition(editingTransitionId);
            if (transition == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.085f, 0.09f, 0.105f));
                EditorGUI.HelpBox(new Rect(16f, rect.y + 16f, Mathf.Max(120f, rect.width - 32f), 42f),
                    "Transition을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            bool hasSelection = selectedRuleIds.Count > 0;
            GetPanelWidths(hasSelection, out float leftWidth, out float rightWidth);
            Rect parametersRect = new(rect.x, rect.y, leftWidth, rect.height);
            Rect graphRect = new(rect.x + leftWidth, rect.y,
                Mathf.Max(1f, rect.width - leftWidth - rightWidth), rect.height);
            Rect detailsRect = new(graphRect.xMax, rect.y, rightWidth, rect.height);

            HandlePanelResize(graphRect, leftWidth > 0f, rightWidth > 0f);
            if (leftWidth > 0f)
                DrawParameters(parametersRect);
            DrawRuleGraph(graphRect, transition);
            if (rightWidth > 0f)
                DrawRuleDetails(detailsRect, transition);
            DrawPanelSplitters(graphRect, leftWidth > 0f, rightWidth > 0f);
        }

        private void DrawRuleGraph(Rect rect, AnimStateTransition transition)
        {
            ruleViewportSize = rect.size;
            GUI.BeginGroup(rect);
            Rect localRect = new(0f, 0f, rect.width, rect.height);
            EditorGUI.DrawRect(localRect, GraphBackground);
            DrawGrid(localRect, graphGridSize * ruleZoom,
                new Color(1f, 1f, 1f, 0.03f), rulePan);
            DrawGrid(localRect, graphGridSize * ruleZoom * 5f,
                new Color(1f, 1f, 1f, 0.06f), rulePan);

            BuildRuleNodeRects(transition);
            Rect resultRect = ToRuleRect(transition.RuleResultPosition, RuleResultSize);
            DrawRuleConnections(transition, resultRect);

            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition == null || !ruleConditionRects.TryGetValue(condition, out Rect nodeRect))
                    continue;
                DrawRuleConditionNode(nodeRect, condition, selectedRuleIds.Contains(condition.Id));
            }

            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node == null || !ruleOperatorRects.TryGetValue(node.Id, out Rect nodeRect))
                    continue;
                DrawRuleOperatorNode(nodeRect, node, CountRuleInputs(transition, node.Id),
                    selectedRuleIds.Contains(node.Id));
            }

            DrawRuleResultNode(resultRect,
                string.IsNullOrEmpty(transition.RuleResultSourceId) ? 0 : 1,
                selectedRuleIds.Contains(RuleResultNodeId));
            DrawRuleSelectionBox();
            DrawRuleShortcutOverlay(localRect);
            HandleRuleGraphInput(localRect, transition, resultRect);
            GUI.EndGroup();
        }

        private void BuildRuleNodeRects(AnimStateTransition transition)
        {
            ruleConditionRects.Clear();
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition != null)
                    ruleConditionRects[condition] = ToRuleRect(GetRulePosition(condition, i), RuleNodeSize);
            }

            ruleOperatorRects.Clear();
            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node != null)
                    ruleOperatorRects[node.Id] = ToRuleRect(node.Position, RuleOperatorSize);
            }
        }

        private void DrawRuleConnections(AnimStateTransition transition, Rect resultRect)
        {
            Handles.BeginGUI();
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition == null || !ruleConditionRects.TryGetValue(condition, out Rect sourceRect))
                    continue;
                if (transition.RuleResultSourceId == condition.Id)
                    DrawRuleConnection(transition, condition.Id, string.Empty,
                        sourceRect, resultRect, selectedRuleIds.Contains(condition.Id));
                else if (!string.IsNullOrEmpty(condition.RuleTargetId))
                    DrawRuleConnection(transition, condition.Id, condition.RuleTargetId,
                        sourceRect, resultRect, selectedRuleIds.Contains(condition.Id));
            }

            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node == null || !ruleOperatorRects.TryGetValue(node.Id, out Rect sourceRect))
                    continue;
                if (transition.RuleResultSourceId == node.Id)
                    DrawRuleConnection(transition, node.Id, string.Empty,
                        sourceRect, resultRect, selectedRuleIds.Contains(node.Id));
                else if (!string.IsNullOrEmpty(node.TargetId))
                    DrawRuleConnection(transition, node.Id, node.TargetId,
                        sourceRect, resultRect, selectedRuleIds.Contains(node.Id));
            }

            if (!string.IsNullOrEmpty(connectingRuleSourceId)
                && TryGetRuleSourceRect(transition, connectingRuleSourceId, out Rect source))
            {
                Vector3 start = new(source.xMax, source.center.y);
                Vector3 end = Event.current.mousePosition;
                float tangent = Mathf.Max(55f, Mathf.Abs(end.x - start.x) * 0.4f);
                Color preview = new(1f, 0.72f, 0.2f);
                Handles.DrawBezier(start, end, start + Vector3.right * tangent,
                    end + Vector3.left * tangent, preview, null, 2.5f);
                Handles.DrawSolidDisc(start, Vector3.forward, 4f);
            }
            Handles.EndGUI();
        }

        private void DrawRuleConnection(
            AnimStateTransition transition,
            string sourceId,
            string targetId,
            Rect sourceRect,
            Rect resultRect,
            bool selected)
        {
            if (!TryGetRuleTargetRect(targetId, resultRect, out Rect targetRect))
                return;
            GetRuleInputSlot(transition, targetId, sourceId, out int slot, out int count);
            Vector3 start = new(sourceRect.xMax, sourceRect.center.y);
            Vector3 end = new(targetRect.xMin,
                targetRect.y + targetRect.height * (slot + 1f) / (count + 1f));
            float tangent = Mathf.Max(55f, Mathf.Abs(end.x - start.x) * 0.45f);
            Color color = selected ? new Color(1f, 0.72f, 0.2f) : new Color(0.52f, 0.67f, 0.86f);
            Vector3 startTangent = start + Vector3.right * tangent;
            Vector3 endTangent = end + Vector3.left * tangent;
            Handles.DrawBezier(start, end, startTangent, endTangent, color, null, selected ? 3f : 2.2f);
            DrawArrow(end, end - endTangent, color, 7f);
            Handles.color = color;
            Handles.DrawSolidDisc(start, Vector3.forward, 3.5f);
            Handles.DrawSolidDisc(end, Vector3.forward, 3.5f);
        }

        private bool TryGetRuleTargetRect(string targetId, Rect resultRect, out Rect rect)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                rect = resultRect;
                return true;
            }
            return ruleOperatorRects.TryGetValue(targetId, out rect);
        }

        private void GetRuleInputSlot(
            AnimStateTransition transition,
            string targetId,
            string sourceId,
            out int slot,
            out int count)
        {
            slot = 0;
            count = 0;
            if (string.IsNullOrEmpty(targetId))
            {
                count = 1;
                return;
            }
            TryGetRuleSourceRect(transition, sourceId, out Rect selectedRect);
            float selectedY = selectedRect.center.y;

            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition == null || condition.RuleTargetId != targetId
                    || !ruleConditionRects.TryGetValue(condition, out Rect rect))
                    continue;
                count++;
                if (rect.center.y < selectedY
                    || Mathf.Approximately(rect.center.y, selectedY)
                    && string.CompareOrdinal(condition.Id, sourceId) < 0)
                    slot++;
            }
            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node == null || node.TargetId != targetId
                    || !ruleOperatorRects.TryGetValue(node.Id, out Rect rect))
                    continue;
                count++;
                if (rect.center.y < selectedY
                    || Mathf.Approximately(rect.center.y, selectedY)
                    && string.CompareOrdinal(node.Id, sourceId) < 0)
                    slot++;
            }
            count = Mathf.Max(1, count);
        }

        private static void DrawRuleConditionNode(Rect rect, AnimStateCondition condition, bool selected)
        {
            Color border = selected ? SelectionColor : new Color(0f, 0f, 0f, 0.75f);
            Color bodyColor = condition.Source == AnimStateConditionSource.OwnerMember
                ? OwnerNodeColor
                : ParameterNodeColor;
            EditorGUI.DrawRect(rect, border);
            Rect body = new(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            EditorGUI.DrawRect(body, bodyColor);
            Rect header = new(body.x, body.y, body.width, Mathf.Min(27f, body.height));
            EditorGUI.DrawRect(header, Color.Lerp(bodyColor, Color.black, 0.2f));
            string source = condition.Source == AnimStateConditionSource.OwnerMember ? "OWNER" : "PARAMETER";
            GUI.Label(new Rect(header.x + 8f, header.y + 4f, header.width - 16f, 19f),
                source, EditorStyles.miniBoldLabel);
            if (body.height >= 52f)
            {
                string label = GetRuleConditionLabel(condition);
                GUI.Label(new Rect(body.x + 8f, body.y + 32f, body.width - 16f, body.height - 36f),
                    new GUIContent(label, label), WrappedMiniLabel);
            }
            EditorGUI.DrawRect(new Rect(rect.xMax - 4f, rect.center.y - 4f, 8f, 8f),
                selected ? new Color(1f, 0.72f, 0.2f) : new Color(0.62f, 0.76f, 0.96f));
        }

        private static void DrawRuleOperatorNode(
            Rect rect,
            AnimStateRuleNode node,
            int inputCount,
            bool selected)
        {
            Color border = selected ? SelectionColor : new Color(0f, 0f, 0f, 0.8f);
            Color body = node.Operation switch
            {
                AnimStateRuleOperator.Or => new Color(0.34f, 0.23f, 0.42f),
                AnimStateRuleOperator.Not => new Color(0.42f, 0.24f, 0.22f),
                _ => new Color(0.28f, 0.31f, 0.34f)
            };
            EditorGUI.DrawRect(rect, border);
            Rect inner = new(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            EditorGUI.DrawRect(inner, body);
            GUI.Label(new Rect(inner.x + 8f, inner.y + 6f, inner.width - 16f, 21f),
                node.Operation.ToString().ToUpperInvariant(), CenteredBoldLabel);
            GUI.Label(new Rect(inner.x + 8f, inner.y + 30f, inner.width - 16f, 18f),
                node.Operation == AnimStateRuleOperator.Not
                    ? $"{inputCount}/1 input"
                    : $"{inputCount} inputs",
                CenteredMiniLabel);
            EditorGUI.DrawRect(new Rect(rect.xMin - 4f, rect.center.y - 4f, 8f, 8f),
                new Color(0.82f, 0.82f, 0.86f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 4f, rect.center.y - 4f, 8f, 8f),
                selected ? new Color(1f, 0.72f, 0.2f) : new Color(0.82f, 0.82f, 0.86f));
        }

        private static void DrawRuleResultNode(Rect rect, int inputCount, bool selected)
        {
            EditorGUI.DrawRect(rect, selected ? SelectionColor : new Color(0f, 0f, 0f, 0.8f));
            Rect body = new(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            EditorGUI.DrawRect(body, ResultNodeColor);
            GUI.Label(new Rect(body.x + 8f, body.y + 7f, body.width - 16f, 20f),
                "RESULT", EditorStyles.boldLabel);
            GUI.Label(new Rect(body.x + 8f, body.y + 31f, body.width - 16f, 22f),
                inputCount == 0 ? "Timing only" : $"All {inputCount} inputs must pass",
                EditorStyles.miniLabel);
            EditorGUI.DrawRect(new Rect(rect.xMin - 4f, rect.center.y - 4f, 8f, 8f),
                new Color(0.56f, 0.9f, 0.62f));
        }

        private void DrawRuleDetails(Rect rect, AnimStateTransition transition)
        {
            EditorGUI.DrawRect(rect, PanelBackground);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.6f));
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 10f,
                Mathf.Max(1f, rect.width - 24f), rect.height - 20f));
            GUILayout.Label("Rule Details", EditorStyles.boldLabel);

            if (selectedRuleIds.Count > 1)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField($"{selectedRuleIds.Count} Nodes Selected",
                    EditorStyles.largeLabel);
                GUILayout.EndArea();
                return;
            }

            if (selectedRuleIds.Count == 1 && selectedRuleIds.Contains(RuleResultNodeId))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Result", EditorStyles.largeLabel);
                EditorGUILayout.LabelField("Input",
                    GetRuleSourceDisplayName(transition, transition.RuleResultSourceId));
                GUILayout.EndArea();
                return;
            }

            AnimStateRuleNode selectedNode = FindRuleNode(transition, selectedRuleNodeId);
            if (selectedNode != null)
            {
                DrawRuleOperatorDetails(transition, selectedNode);
                GUILayout.EndArea();
                return;
            }

            if (selectedRuleConditionIndex >= 0
                && selectedRuleConditionIndex < transition.Conditions.Count)
            {
                GUILayout.Label($"Condition {selectedRuleConditionIndex + 1}", EditorStyles.miniBoldLabel);
                ruleScroll = EditorGUILayout.BeginScrollView(ruleScroll);
                DrawRuleCondition(transition, selectedRuleConditionIndex,
                    transition.Conditions[selectedRuleConditionIndex]);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    transition.Conditions.Count == 0 && transition.RuleNodes.Count == 0
                        ? "우클릭하거나 상단 + Node 버튼으로 Rule을 추가하세요."
                        : "편집할 조건 또는 논리 노드를 선택하세요.",
                    MessageType.None);
            }
            GUILayout.EndArea();
        }

        private static string GetRuleSourceDisplayName(
            AnimStateTransition transition,
            string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
                return "None";
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition?.Id == sourceId)
                    return GetRuleConditionLabel(condition);
            }
            AnimStateRuleNode node = FindRuleNode(transition, sourceId);
            return node != null ? node.Operation.ToString().ToUpperInvariant() : "Missing";
        }

        private void DrawRuleOperatorDetails(
            AnimStateTransition transition,
            AnimStateRuleNode node)
        {
            GUILayout.Label("Boolean", EditorStyles.miniBoldLabel);
            AnimStateRuleOperator next = (AnimStateRuleOperator)EditorGUILayout.EnumPopup(
                "Operation", node.Operation);
            if (next != node.Operation)
            {
                Undo.RecordObject(stateMachine, "Change Rule Operation");
                node.Operation = next;
                if (next == AnimStateRuleOperator.Not)
                    KeepSingleRuleInput(transition, node.Id);
                Save();
            }

            int inputCount = CountRuleInputs(transition, node.Id);
            EditorGUILayout.LabelField("Inputs", next == AnimStateRuleOperator.Not
                ? $"{inputCount} / 1"
                : inputCount.ToString());
            EditorGUILayout.Space(8f);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove Node", GUILayout.Height(25f)))
            {
                Undo.RecordObject(stateMachine, "Remove Rule Node");
                transition.RemoveRuleNode(node.Id);
                selectedRuleIds.Remove(node.Id);
                UpdateRulePrimaryFromSelection(transition);
                Save();
                GUIUtility.ExitGUI();
            }
        }

        private static void DrawRuleShortcutOverlay(Rect rect)
        {
            const string text = "Wheel Zoom  |  RMB Pan  |  Box Select  |  Ctrl+C/V  |  Ctrl+A  |  F Frame  |  Del";
            float width = Mathf.Min(rect.width - 16f,
                EditorStyles.miniLabel.CalcSize(new GUIContent(text)).x + 18f);
            if (width <= 24f)
                return;
            Rect overlay = new(8f, rect.height - 27f, width, 20f);
            EditorGUI.DrawRect(overlay, new Color(0.04f, 0.045f, 0.055f, 0.9f));
            GUI.Label(overlay, text, CenteredMiniLabel);
        }

        private void HandleRuleGraphInput(Rect rect, AnimStateTransition transition, Rect resultRect)
        {
            Event evt = Event.current;
            if (EditorApplication.isPlaying)
                return;

            if (evt.type == EventType.MouseLeaveWindow)
            {
                CancelRuleGraphInteractions();
                Repaint();
                return;
            }
            if (!rect.Contains(evt.mousePosition))
            {
                if (ruleBoxSelecting || draggingRuleSelection
                    || !string.IsNullOrEmpty(connectingRuleSourceId) || ruleRightMousePressed)
                {
                    CancelRuleGraphInteractions();
                    Repaint();
                }
                return;
            }

            if (evt.type == EventType.ScrollWheel)
            {
                ZoomRuleAt(evt.mousePosition, -evt.delta.y * 0.05f);
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                ruleRightMousePressed = true;
                ruleRightMousePanning = false;
                ruleRightMouseDown = evt.mousePosition;
                ruleRightMouseLast = evt.mousePosition;
                evt.Use();
                return;
            }
            if (evt.type == EventType.MouseDrag && evt.button == 1 && ruleRightMousePressed)
            {
                if (!ruleRightMousePanning && (evt.mousePosition - ruleRightMouseDown).sqrMagnitude >= 16f)
                    ruleRightMousePanning = true;
                if (ruleRightMousePanning)
                    rulePan += evt.mousePosition - ruleRightMouseLast;
                ruleRightMouseLast = evt.mousePosition;
                evt.Use();
                Repaint();
                return;
            }
            if (evt.type == EventType.MouseUp && evt.button == 1 && ruleRightMousePressed)
            {
                bool showMenu = !ruleRightMousePanning;
                ruleRightMousePressed = false;
                ruleRightMousePanning = false;
                if (showMenu)
                    ShowAddRuleConditionMenu(transition, RuleScreenToGraph(evt.mousePosition));
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (TryGetRuleOutputAt(transition, evt.mousePosition, out string sourceId))
                {
                    if (!selectedRuleIds.Contains(sourceId))
                        SelectOnlyRuleItem(transition, sourceId);
                    else
                        SetPrimaryRuleItem(transition, sourceId);
                    connectingRuleSourceId = sourceId;
                    evt.Use();
                    Repaint();
                    return;
                }

                if (TryGetRuleItemAt(transition, resultRect, evt.mousePosition, out string itemId))
                {
                    bool toggle = evt.control || evt.command || evt.shift;
                    if (UpdateRuleClickedSelection(transition, itemId, toggle))
                    {
                        FinishMoveUndo();
                        CaptureRuleDragStarts(transition);
                        draggingRuleSelection = true;
                        ruleDragStartMouse = RuleScreenToGraph(evt.mousePosition);
                    }
                    evt.Use();
                    Repaint();
                    return;
                }

                bool additive = evt.control || evt.command || evt.shift;
                if (!additive)
                    ClearRuleSelection();
                ruleBoxSelecting = true;
                ruleBoxSelectionAdditive = additive;
                ruleBoxStart = evt.mousePosition;
                ruleBoxEnd = evt.mousePosition;
                GUI.FocusControl(null);
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                if (!string.IsNullOrEmpty(connectingRuleSourceId))
                {
                    evt.Use();
                    Repaint();
                    return;
                }

                if (draggingRuleSelection)
                {
                    Vector2 delta = RuleScreenToGraph(evt.mousePosition) - ruleDragStartMouse;
                    if (WillMoveSelectedRuleItems(transition, delta))
                    {
                        BeginMoveUndo(selectedRuleIds.Count > 1
                            ? "Move Rule Nodes"
                            : "Move Rule Node");
                        MoveSelectedRuleItems(transition, delta);
                        Save();
                    }
                    evt.Use();
                    return;
                }

                if (ruleBoxSelecting)
                {
                    ruleBoxEnd = evt.mousePosition;
                    evt.Use();
                    Repaint();
                    return;
                }
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                if (!string.IsNullOrEmpty(connectingRuleSourceId))
                {
                    TryFinishRuleConnection(transition, resultRect, evt.mousePosition);
                    connectingRuleSourceId = null;
                    evt.Use();
                    Repaint();
                    return;
                }

                if (draggingRuleSelection)
                {
                    FinishMoveUndo();
                    draggingRuleSelection = false;
                    ruleDragStartPositions.Clear();
                    evt.Use();
                    return;
                }

                if (ruleBoxSelecting)
                {
                    FinishRuleBoxSelection(transition, resultRect);
                    ruleBoxSelecting = false;
                    evt.Use();
                    Repaint();
                }
            }
        }

        private bool TryGetRuleItemAt(
            AnimStateTransition transition,
            Rect resultRect,
            Vector2 mouse,
            out string itemId)
        {
            for (int i = transition.RuleNodes.Count - 1; i >= 0; i--)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node != null && ruleOperatorRects.TryGetValue(node.Id, out Rect rect)
                    && rect.Contains(mouse))
                {
                    itemId = node.Id;
                    return true;
                }
            }
            for (int i = transition.Conditions.Count - 1; i >= 0; i--)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition != null && ruleConditionRects.TryGetValue(condition, out Rect rect)
                    && rect.Contains(mouse))
                {
                    itemId = condition.Id;
                    return true;
                }
            }
            if (resultRect.Contains(mouse))
            {
                itemId = RuleResultNodeId;
                return true;
            }
            itemId = null;
            return false;
        }

        private bool UpdateRuleClickedSelection(
            AnimStateTransition transition,
            string itemId,
            bool toggle)
        {
            if (toggle)
            {
                if (!selectedRuleIds.Add(itemId))
                {
                    selectedRuleIds.Remove(itemId);
                    UpdateRulePrimaryFromSelection(transition);
                    return false;
                }
            }
            else if (!selectedRuleIds.Contains(itemId))
            {
                selectedRuleIds.Clear();
                selectedRuleIds.Add(itemId);
            }

            SetPrimaryRuleItem(transition, itemId);
            return selectedRuleIds.Contains(itemId);
        }

        private void SelectOnlyRuleItem(AnimStateTransition transition, string itemId)
        {
            selectedRuleIds.Clear();
            if (!string.IsNullOrEmpty(itemId))
                selectedRuleIds.Add(itemId);
            SetPrimaryRuleItem(transition, itemId);
        }

        private void SetPrimaryRuleItem(AnimStateTransition transition, string itemId)
        {
            selectedRuleConditionIndex = -1;
            selectedRuleNodeId = null;
            if (string.IsNullOrEmpty(itemId) || itemId == RuleResultNodeId)
                return;

            AnimStateRuleNode node = FindRuleNode(transition, itemId);
            if (node != null)
            {
                selectedRuleNodeId = node.Id;
                return;
            }

            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                if (transition.Conditions[i]?.Id == itemId)
                {
                    selectedRuleConditionIndex = i;
                    return;
                }
            }
        }

        private void UpdateRulePrimaryFromSelection(AnimStateTransition transition)
        {
            foreach (string itemId in selectedRuleIds)
            {
                SetPrimaryRuleItem(transition, itemId);
                return;
            }
            SetPrimaryRuleItem(transition, null);
        }

        private void ClearRuleSelection()
        {
            selectedRuleIds.Clear();
            selectedRuleConditionIndex = -1;
            selectedRuleNodeId = null;
        }

        private void CaptureRuleDragStarts(AnimStateTransition transition)
        {
            ruleDragStartPositions.Clear();
            foreach (string itemId in selectedRuleIds)
            {
                if (TryGetRuleItemPosition(transition, itemId, out Vector2 position))
                    ruleDragStartPositions[itemId] = position;
            }
        }

        private bool TryGetRuleItemPosition(
            AnimStateTransition transition,
            string itemId,
            out Vector2 position)
        {
            if (itemId == RuleResultNodeId)
            {
                position = transition.RuleResultPosition;
                return true;
            }
            AnimStateRuleNode node = FindRuleNode(transition, itemId);
            if (node != null)
            {
                position = node.Position;
                return true;
            }
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition?.Id != itemId)
                    continue;
                position = GetRulePosition(condition, i);
                return true;
            }
            position = default;
            return false;
        }

        private void SetRuleItemPosition(
            AnimStateTransition transition,
            string itemId,
            Vector2 position)
        {
            position = SnapRulePosition(position);
            if (itemId == RuleResultNodeId)
            {
                transition.RuleResultPosition = position;
                return;
            }
            AnimStateRuleNode node = FindRuleNode(transition, itemId);
            if (node != null)
            {
                node.Position = position;
                return;
            }
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition?.Id == itemId)
                {
                    condition.RulePosition = position;
                    return;
                }
            }
        }

        private void MoveSelectedRuleItems(AnimStateTransition transition, Vector2 delta)
        {
            foreach (KeyValuePair<string, Vector2> item in ruleDragStartPositions)
                SetRuleItemPosition(transition, item.Key, item.Value + delta);
        }

        private bool WillMoveSelectedRuleItems(AnimStateTransition transition, Vector2 delta)
        {
            foreach (KeyValuePair<string, Vector2> item in ruleDragStartPositions)
            {
                if (TryGetRuleItemPosition(transition, item.Key, out Vector2 current)
                    && current != SnapRulePosition(item.Value + delta))
                    return true;
            }
            return false;
        }

        private void DrawRuleSelectionBox()
        {
            if (!ruleBoxSelecting || Event.current.type != EventType.Repaint)
                return;
            Rect rect = GetRuleSelectionRect();
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.55f, 0.95f, 0.12f));
            Handles.BeginGUI();
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear,
                new Color(0.35f, 0.7f, 1f, 0.9f));
            Handles.EndGUI();
        }

        private Rect GetRuleSelectionRect() => Rect.MinMaxRect(
            Mathf.Min(ruleBoxStart.x, ruleBoxEnd.x),
            Mathf.Min(ruleBoxStart.y, ruleBoxEnd.y),
            Mathf.Max(ruleBoxStart.x, ruleBoxEnd.x),
            Mathf.Max(ruleBoxStart.y, ruleBoxEnd.y));

        private void FinishRuleBoxSelection(AnimStateTransition transition, Rect resultRect)
        {
            Rect selection = GetRuleSelectionRect();
            if (selection.width < 3f && selection.height < 3f)
                return;
            if (!ruleBoxSelectionAdditive)
                ClearRuleSelection();

            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition != null && ruleConditionRects.TryGetValue(condition, out Rect rect)
                    && selection.Overlaps(rect, true))
                    selectedRuleIds.Add(condition.Id);
            }
            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node != null && ruleOperatorRects.TryGetValue(node.Id, out Rect rect)
                    && selection.Overlaps(rect, true))
                    selectedRuleIds.Add(node.Id);
            }
            if (selection.Overlaps(resultRect, true))
                selectedRuleIds.Add(RuleResultNodeId);
            UpdateRulePrimaryFromSelection(transition);
        }

        private bool TryGetRuleOutputAt(
            AnimStateTransition transition,
            Vector2 mouse,
            out string sourceId)
        {
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition != null && ruleConditionRects.TryGetValue(condition, out Rect rect)
                    && new Rect(rect.xMax - 9f, rect.center.y - 9f, 18f, 18f).Contains(mouse))
                {
                    sourceId = condition.Id;
                    return true;
                }
            }

            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node != null && ruleOperatorRects.TryGetValue(node.Id, out Rect rect)
                    && new Rect(rect.xMax - 9f, rect.center.y - 9f, 18f, 18f).Contains(mouse))
                {
                    sourceId = node.Id;
                    return true;
                }
            }

            sourceId = null;
            return false;
        }

        private void TryFinishRuleConnection(
            AnimStateTransition transition,
            Rect resultRect,
            Vector2 mouse)
        {
            string targetId = null;
            bool hasTarget = resultRect.Contains(mouse);
            if (!hasTarget)
            {
                foreach (KeyValuePair<string, Rect> item in ruleOperatorRects)
                {
                    if (!item.Value.Contains(mouse))
                        continue;
                    targetId = item.Key;
                    hasTarget = true;
                    break;
                }
            }
            if (!hasTarget || targetId == connectingRuleSourceId
                || WouldCreateRuleCycle(transition, connectingRuleSourceId, targetId))
                return;

            Undo.RecordObject(stateMachine, "Connect Rule Nodes");
            if (string.IsNullOrEmpty(targetId))
            {
                SetRuleSourceTarget(transition, connectingRuleSourceId, string.Empty);
                transition.RuleResultSourceId = connectingRuleSourceId;
            }
            else
            {
                if (transition.RuleResultSourceId == connectingRuleSourceId)
                    transition.RuleResultSourceId = string.Empty;
                AnimStateRuleNode target = FindRuleNode(transition, targetId);
                if (target?.Operation == AnimStateRuleOperator.Not)
                    DisconnectOtherRuleInputs(transition, targetId, connectingRuleSourceId);
                SetRuleSourceTarget(transition, connectingRuleSourceId, targetId);
            }
            Save();
        }

        private static bool WouldCreateRuleCycle(
            AnimStateTransition transition,
            string sourceId,
            string targetId)
        {
            string current = targetId;
            int remaining = transition.RuleNodes.Count + 1;
            while (!string.IsNullOrEmpty(current) && remaining-- > 0)
            {
                if (current == sourceId)
                    return true;
                AnimStateRuleNode node = FindRuleNode(transition, current);
                if (node == null)
                    return false;
                current = node.TargetId;
            }
            return !string.IsNullOrEmpty(current);
        }

        private static void SetRuleSourceTarget(
            AnimStateTransition transition,
            string sourceId,
            string targetId)
        {
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition?.Id != sourceId)
                    continue;
                condition.RuleTargetId = targetId;
                return;
            }

            AnimStateRuleNode node = FindRuleNode(transition, sourceId);
            if (node != null)
                node.TargetId = targetId;
        }

        private static void DisconnectOtherRuleInputs(
            AnimStateTransition transition,
            string targetId,
            string keepSourceId)
        {
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition != null && condition.Id != keepSourceId
                    && condition.RuleTargetId == targetId)
                    condition.RuleTargetId = string.Empty;
            }
            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node != null && node.Id != keepSourceId && node.TargetId == targetId)
                    node.TargetId = string.Empty;
            }
        }

        private static void KeepSingleRuleInput(AnimStateTransition transition, string targetId)
        {
            string keep = null;
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition == null || condition.RuleTargetId != targetId)
                    continue;
                if (keep == null)
                    keep = condition.Id;
                else
                    condition.RuleTargetId = string.Empty;
            }
            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node == null || node.TargetId != targetId)
                    continue;
                if (keep == null)
                    keep = node.Id;
                else
                    node.TargetId = string.Empty;
            }
        }

        private bool TryGetRuleSourceRect(
            AnimStateTransition transition,
            string sourceId,
            out Rect rect)
        {
            if (ruleOperatorRects.TryGetValue(sourceId, out rect))
                return true;
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition?.Id == sourceId)
                    return ruleConditionRects.TryGetValue(condition, out rect);
            }
            rect = default;
            return false;
        }

        private static AnimStateRuleNode FindRuleNode(
            AnimStateTransition transition,
            string nodeId)
        {
            if (transition == null || string.IsNullOrEmpty(nodeId))
                return null;
            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node?.Id == nodeId)
                    return node;
            }
            return null;
        }

        private static int CountRuleInputs(AnimStateTransition transition, string targetId)
        {
            int count = 0;
            for (int i = 0; i < transition.Conditions.Count; i++)
                if (transition.Conditions[i]?.RuleTargetId == targetId)
                    count++;
            for (int i = 0; i < transition.RuleNodes.Count; i++)
                if (transition.RuleNodes[i]?.TargetId == targetId)
                    count++;
            return count;
        }

        private Vector2 GetRulePosition(AnimStateCondition condition, int index)
        {
            return condition.RulePosition == Vector2.zero
                ? GetDefaultRulePosition(index)
                : condition.RulePosition;
        }

        private int GetRuleConditionIndex(AnimStateTransition transition, AnimStateCondition condition)
        {
            for (int i = 0; i < transition.Conditions.Count; i++)
                if (ReferenceEquals(transition.Conditions[i], condition))
                    return i;
            return -1;
        }

        private Rect ToRuleRect(Vector2 graphPosition, Vector2 size) =>
            new(rulePan + graphPosition * ruleZoom, size * ruleZoom);

        private Vector2 RuleScreenToGraph(Vector2 screenPosition) =>
            (screenPosition - rulePan) / ruleZoom;

        private void ZoomRuleAt(Vector2 screenPosition, float amount)
        {
            Vector2 graphPosition = RuleScreenToGraph(screenPosition);
            ruleZoom = Mathf.Clamp(ruleZoom + amount, MinimumGraphZoom, MaximumGraphZoom);
            rulePan = screenPosition - graphPosition * ruleZoom;
        }

        private Vector2 SnapRulePosition(Vector2 position) => snapToGrid
            ? new Vector2(Mathf.Round(position.x / graphGridSize) * graphGridSize,
                Mathf.Round(position.y / graphGridSize) * graphGridSize)
            : position;

        private void FrameRuleGraph(AnimStateTransition transition)
        {
            Vector2 min = transition.RuleResultPosition;
            Vector2 max = min + RuleResultSize;
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition == null)
                    continue;
                Vector2 position = GetRulePosition(condition, i);
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position + RuleNodeSize);
            }
            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node == null)
                    continue;
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position + RuleOperatorSize);
            }
            Vector2 available = new(Mathf.Max(1f, ruleViewportSize.x - 100f),
                Mathf.Max(1f, ruleViewportSize.y - 100f));
            Vector2 size = max - min;
            ruleZoom = Mathf.Clamp(Mathf.Min(available.x / Mathf.Max(1f, size.x),
                available.y / Mathf.Max(1f, size.y)), MinimumGraphZoom, MaximumGraphZoom);
            rulePan = ruleViewportSize * 0.5f - (min + max) * 0.5f * ruleZoom;
            Repaint();
        }

        private static string GetRuleConditionLabel(AnimStateCondition condition)
        {
            if (condition == null)
                return "Missing Condition";

            string target;
            if (condition.Source == AnimStateConditionSource.Parameter)
            {
                target = string.IsNullOrEmpty(condition.Parameter)
                    ? "Parameter not selected"
                    : condition.Parameter;
            }
            else
            {
                if (string.IsNullOrEmpty(condition.OwnerType))
                    return "Owner type not selected";
                string member = GetOwnerMemberDisplayName(condition.OwnerMember);
                Type type = Type.GetType(condition.OwnerType, false);
                target = $"{type?.Name ?? "Missing Type"}.{member}";
            }

            if (condition.ValueType is AnimStateParameterType.Bool or AnimStateParameterType.Trigger)
                return $"{target} is {(condition.Mode == AnimStateConditionMode.IfNot ? "False" : "True")}";

            string comparison = condition.Mode switch
            {
                AnimStateConditionMode.Greater => ">",
                AnimStateConditionMode.GreaterOrEqual => ">=",
                AnimStateConditionMode.Less => "<",
                AnimStateConditionMode.LessOrEqual => "<=",
                AnimStateConditionMode.NotEqual => "!=",
                _ => "=="
            };
            string value = condition.ValueType == AnimStateParameterType.Int
                ? Mathf.RoundToInt(condition.Threshold).ToString()
                : condition.Threshold.ToString("0.###");
            return $"{target} {comparison} {value}";
        }

        private void DrawRuleCondition(AnimStateTransition transition, int index, AnimStateCondition condition)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    AnimStateConditionSource nextSource = (AnimStateConditionSource)EditorGUILayout.EnumPopup(
                        "Source", condition.Source);
                    if (GUILayout.Button(new GUIContent("-", "Remove condition"), GUILayout.Width(24f)))
                    {
                        Undo.RecordObject(stateMachine, "Remove Transition Condition");
                        selectedRuleIds.Remove(condition.Id);
                        transition.RemoveConditionAt(index);
                        UpdateRulePrimaryFromSelection(transition);
                        Save();
                        GUIUtility.ExitGUI();
                    }
                    if (nextSource != condition.Source)
                    {
                        Undo.RecordObject(stateMachine, "Change Transition Condition Source");
                        condition.Source = nextSource;
                        Save();
                    }
                }

                if (condition.Source == AnimStateConditionSource.OwnerMember)
                    DrawOwnerCondition(condition);
                else
                    DrawParameterCondition(condition);
            }
        }

        private void DrawParameterCondition(AnimStateCondition condition)
        {
            string[] names = GetParameterNames();
            if (names.Length == 0)
            {
                EditorGUILayout.HelpBox("Parameters 패널에서 Parameter를 먼저 추가하세요.", MessageType.Info);
                return;
            }

            int parameterIndex = Mathf.Max(0, Array.IndexOf(names, condition.Parameter));
            int nextIndex = EditorGUILayout.Popup("Parameter", parameterIndex, names);
            AnimStateParameter parameter = FindParameter(names[nextIndex]);
            AnimStateConditionMode nextMode = DrawConditionMode(parameter, condition.Mode);
            float nextThreshold = condition.Threshold;
            if (parameter?.Type == AnimStateParameterType.Float)
                nextThreshold = EditorGUILayout.FloatField("Value", nextThreshold);
            else if (parameter?.Type == AnimStateParameterType.Int)
                nextThreshold = EditorGUILayout.IntField("Value", Mathf.RoundToInt(nextThreshold));

            if (condition.Parameter == names[nextIndex] && condition.Mode == nextMode
                && Mathf.Approximately(condition.Threshold, nextThreshold))
                return;
            Undo.RecordObject(stateMachine, "Edit Parameter Condition");
            condition.Parameter = names[nextIndex];
            condition.ValueType = parameter?.Type ?? AnimStateParameterType.Float;
            condition.Mode = nextMode;
            condition.Threshold = nextThreshold;
            Save();
        }

        private void DrawOwnerCondition(AnimStateCondition condition)
        {
            Type ownerType = string.IsNullOrEmpty(condition.OwnerType)
                ? null
                : Type.GetType(condition.OwnerType, false);
            EditorGUILayout.LabelField("Owner Component", EditorStyles.miniBoldLabel);
            string typeLabel = ownerType != null
                ? ownerType.Name
                : "Select Component...";
            if (GUILayout.Button(new GUIContent(typeLabel,
                    ownerType?.FullName ?? "Search MonoBehaviour components"), GUILayout.Height(24f)))
                ShowOwnerTypeMenu(condition, GUILayoutUtility.GetLastRect());
            if (ownerType != null && !string.IsNullOrEmpty(ownerType.Namespace))
                EditorGUILayout.LabelField(ownerType.Namespace, EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(ownerType == null))
            {
                string memberLabel = GetOwnerMemberDisplayName(condition.OwnerMember);
                if (GUILayout.Button(new GUIContent(memberLabel, condition.OwnerMember), GUILayout.Height(22f)))
                    ShowOwnerMemberMenu(condition, ownerType);
            }

            if (ownerType == null || string.IsNullOrEmpty(condition.OwnerMember))
            {
                EditorGUILayout.HelpBox("Owner 컴포넌트와 값을 읽을 멤버를 선택하세요.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Value Type", condition.ValueType.ToString());
            AnimStateParameter pseudoParameter = new() { Type = condition.ValueType };
            AnimStateConditionMode nextMode = DrawConditionMode(pseudoParameter, condition.Mode);
            float nextThreshold = condition.Threshold;
            if (condition.ValueType == AnimStateParameterType.Float)
                nextThreshold = EditorGUILayout.FloatField("Value", nextThreshold);
            else if (condition.ValueType == AnimStateParameterType.Int)
                nextThreshold = EditorGUILayout.IntField("Value", Mathf.RoundToInt(nextThreshold));
            if (nextMode == condition.Mode && Mathf.Approximately(nextThreshold, condition.Threshold))
                return;

            Undo.RecordObject(stateMachine, "Edit Owner Condition");
            condition.Mode = nextMode;
            condition.Threshold = nextThreshold;
            Save();
        }

        private void ShowAddRuleConditionMenu(AnimStateTransition transition, Vector2? graphPosition = null)
        {
            GenericMenu menu = new();
            if (stateMachine.Parameters.Count > 0)
            {
                menu.AddItem(new GUIContent("Condition/Parameter"), false, () =>
                {
                    AnimStateParameter parameter = stateMachine.Parameters[0];
                    Undo.RecordObject(stateMachine, "Add Parameter Condition");
                    transition.AddCondition(new AnimStateCondition
                    {
                        Source = AnimStateConditionSource.Parameter,
                        Parameter = parameter.Name,
                        ValueType = parameter.Type,
                        Mode = parameter.Type is AnimStateParameterType.Bool or AnimStateParameterType.Trigger
                            ? AnimStateConditionMode.If
                            : AnimStateConditionMode.Greater,
                        RulePosition = graphPosition ?? GetDefaultRulePosition(transition.Conditions.Count)
                    });
                    SelectOnlyRuleItem(transition,
                        transition.Conditions[transition.Conditions.Count - 1].Id);
                    Save();
                });
            }
            else
                menu.AddDisabledItem(new GUIContent("Condition/Parameter"));

            menu.AddItem(new GUIContent("Condition/Owner Member"), false, () =>
            {
                Undo.RecordObject(stateMachine, "Add Owner Condition");
                transition.AddCondition(new AnimStateCondition
                {
                    Source = AnimStateConditionSource.OwnerMember,
                    Mode = AnimStateConditionMode.If,
                    ValueType = AnimStateParameterType.Bool,
                    RulePosition = graphPosition ?? GetDefaultRulePosition(transition.Conditions.Count)
                });
                SelectOnlyRuleItem(transition,
                    transition.Conditions[transition.Conditions.Count - 1].Id);
                Save();
            });

            menu.AddSeparator(string.Empty);
            AddRuleOperatorMenuItem(menu, transition, AnimStateRuleOperator.And, graphPosition);
            AddRuleOperatorMenuItem(menu, transition, AnimStateRuleOperator.Or, graphPosition);
            AddRuleOperatorMenuItem(menu, transition, AnimStateRuleOperator.Not, graphPosition);
            menu.ShowAsContext();
        }

        private void AddRuleOperatorMenuItem(
            GenericMenu menu,
            AnimStateTransition transition,
            AnimStateRuleOperator operation,
            Vector2? graphPosition)
        {
            menu.AddItem(new GUIContent($"Boolean/{operation.ToString().ToUpperInvariant()}"), false, () =>
            {
                Undo.RecordObject(stateMachine, "Add Rule Operator");
                string selectedSource = GetSelectedRuleSourceId(transition);
                bool selectedWasResult = transition.RuleResultSourceId == selectedSource;
                Vector2 position = graphPosition
                                   ?? new Vector2(340f, 70f + transition.RuleNodes.Count * 90f);
                AnimStateRuleNode node = transition.AddRuleNode(operation, position);
                if (!string.IsNullOrEmpty(selectedSource)
                    && !WouldCreateRuleCycle(transition, selectedSource, node.Id))
                {
                    SetRuleSourceTarget(transition, selectedSource, node.Id);
                    if (selectedWasResult)
                        transition.RuleResultSourceId = node.Id;
                }
                SelectOnlyRuleItem(transition, node.Id);
                Save();
            });
        }

        private string GetSelectedRuleSourceId(AnimStateTransition transition)
        {
            if (!string.IsNullOrEmpty(selectedRuleNodeId))
                return selectedRuleNodeId;
            return selectedRuleConditionIndex >= 0
                   && selectedRuleConditionIndex < transition.Conditions.Count
                ? transition.Conditions[selectedRuleConditionIndex]?.Id
                : null;
        }

        private static Vector2 GetDefaultRulePosition(int index) =>
            new(60f, 60f + index * 110f);

        private void ShowOwnerTypeMenu(AnimStateCondition condition, Rect buttonRect)
        {
            var dropdown = new OwnerTypeDropdown(new AdvancedDropdownState(),
                GetOwnerComponentTypes(), condition.OwnerType, selectedType =>
                {
                    Undo.RecordObject(stateMachine, "Select Owner Type");
                    condition.OwnerType = selectedType?.AssemblyQualifiedName ?? string.Empty;
                    condition.OwnerMember = string.Empty;
                    Save();
                    Repaint();
                });
            dropdown.Show(buttonRect);
        }

        private static IReadOnlyList<Type> GetOwnerComponentTypes()
        {
            if (ownerComponentTypes != null)
                return ownerComponentTypes;
            ownerComponentTypes = new List<Type>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (!type.IsAbstract && !type.IsGenericTypeDefinition)
                    ownerComponentTypes.Add(type);
            }
            ownerComponentTypes.Sort((left, right) =>
                string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            return ownerComponentTypes;
        }

        private void ShowOwnerMemberMenu(AnimStateCondition condition, Type ownerType)
        {
            List<OwnerMemberOption> members = GetOwnerMembers(ownerType);
            GenericMenu menu = new();
            for (int i = 0; i < members.Count; i++)
            {
                OwnerMemberOption captured = members[i];
                menu.AddItem(new GUIContent(captured.Path), captured.Key == condition.OwnerMember, () =>
                {
                    Undo.RecordObject(stateMachine, "Select Owner Member");
                    condition.OwnerMember = captured.Key;
                    condition.ValueType = captured.ValueType;
                    condition.Mode = captured.ValueType == AnimStateParameterType.Bool
                        ? AnimStateConditionMode.If
                        : AnimStateConditionMode.Greater;
                    Save();
                });
            }
            if (members.Count == 0)
                menu.AddDisabledItem(new GUIContent("No supported members"));
            menu.ShowAsContext();
        }

        private static List<OwnerMemberOption> GetOwnerMembers(Type ownerType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
                                       | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var result = new List<OwnerMemberOption>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (Type type = ownerType; type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field.IsStatic || !field.IsPublic && field.GetCustomAttribute<SerializeField>() == null
                        || !TryGetConditionValueType(field.FieldType, out AnimStateParameterType valueType))
                        continue;
                    AddOwnerMember(result, keys, "Fields/" + field.Name, "F:" + field.Name, valueType);
                }

                PropertyInfo[] properties = type.GetProperties(flags);
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    MethodInfo getter = property.GetGetMethod(true);
                    if (getter == null || !getter.IsPublic || getter.IsStatic || property.GetIndexParameters().Length != 0
                        || !TryGetConditionValueType(property.PropertyType, out AnimStateParameterType valueType))
                        continue;
                    AddOwnerMember(result, keys, "Properties/" + property.Name, "P:" + property.Name, valueType);
                }

                MethodInfo[] methods = type.GetMethods(flags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!method.IsPublic || method.IsStatic || method.IsSpecialName || method.GetParameters().Length != 0
                        || !TryGetConditionValueType(method.ReturnType, out AnimStateParameterType valueType))
                        continue;
                    AddOwnerMember(result, keys, "Methods/" + method.Name + "()", "M:" + method.Name, valueType);
                }
            }
            result.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
            return result;
        }

        private static void AddOwnerMember(
            List<OwnerMemberOption> result,
            HashSet<string> keys,
            string path,
            string key,
            AnimStateParameterType valueType)
        {
            if (keys.Add(key))
                result.Add(new OwnerMemberOption(path, key, valueType));
        }

        private static bool TryGetConditionValueType(Type type, out AnimStateParameterType valueType)
        {
            if (type == typeof(bool))
            {
                valueType = AnimStateParameterType.Bool;
                return true;
            }
            if (type.IsEnum || type == typeof(byte) || type == typeof(sbyte) || type == typeof(short)
                || type == typeof(ushort) || type == typeof(int) || type == typeof(uint)
                || type == typeof(long) || type == typeof(ulong))
            {
                valueType = AnimStateParameterType.Int;
                return true;
            }
            if (type == typeof(float) || type == typeof(double))
            {
                valueType = AnimStateParameterType.Float;
                return true;
            }
            valueType = default;
            return false;
        }

        private static string GetOwnerMemberDisplayName(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 3)
                return "Select Field, Property, or Method";
            string suffix = key[0] == 'M' ? "()" : string.Empty;
            return key.Substring(2) + suffix;
        }

        private enum ClipboardNodeType
        {
            State,
            Conduit,
            Alias,
            StateMachine
        }

        private sealed class StateGraphClipboard
        {
            public readonly List<StateNodeCopy> Nodes = new();
            public readonly List<TransitionCopy> Transitions = new();
            public int PasteCount;
        }

        private sealed class StateNodeCopy
        {
            public string SourceId;
            public ClipboardNodeType Type;
            public string Name;
            public Vector2 Position;
            public AnimSequenceSO Sequence;
            public float Speed;
            public bool Loop;
            public Vector2 EntryPosition;
            public string DefaultNodeId;
            public readonly List<string> AliasSources = new();
        }

        private sealed class TransitionCopy
        {
            public string FromId;
            public string ToId;
            public AnimStateTransitionTiming Timing;
            public float ExitTime;
            public float Duration;
            public RuleGraphClipboard Rule;
        }

        private sealed class RuleGraphClipboard
        {
            public readonly List<RuleConditionCopy> Conditions = new();
            public readonly List<RuleOperatorCopy> Operators = new();
            public string ResultSourceId;
            public Vector2 ResultPosition;
            public int PasteCount;
        }

        private sealed class RuleConditionCopy
        {
            public string SourceId;
            public AnimStateConditionSource Source;
            public string Parameter;
            public string OwnerType;
            public string OwnerMember;
            public AnimStateParameterType ValueType;
            public AnimStateConditionMode Mode;
            public float Threshold;
            public Vector2 Position;
            public string TargetId;
        }

        private sealed class RuleOperatorCopy
        {
            public string SourceId;
            public AnimStateRuleOperator Operation;
            public Vector2 Position;
            public string TargetId;
        }
        private sealed class OwnerTypeDropdown : AdvancedDropdown
        {
            private readonly IReadOnlyList<Type> types;
            private readonly string selectedTypeName;
            private readonly Action<Type> onSelected;

            public OwnerTypeDropdown(AdvancedDropdownState state, IReadOnlyList<Type> types,
                string selectedTypeName, Action<Type> onSelected) : base(state)
            {
                this.types = types;
                this.selectedTypeName = selectedTypeName;
                this.onSelected = onSelected;
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Component Type");
                for (int i = 0; i < types.Count; i++)
                {
                    Type type = types[i];
                    string suffix = string.IsNullOrEmpty(type.Namespace)
                        ? string.Empty
                        : $"  ({type.Namespace})";
                    var item = new AdvancedDropdownItem(type.Name + suffix) { id = i + 1 };
                    if (type.AssemblyQualifiedName == selectedTypeName)
                        item.icon = EditorGUIUtility.IconContent("FilterSelectedOnly").image as Texture2D;
                    root.AddChild(item);
                }
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                int index = item.id - 1;
                if (index >= 0 && index < types.Count)
                    onSelected?.Invoke(types[index]);
            }
        }
        private readonly struct OwnerMemberOption
        {
            public OwnerMemberOption(string path, string key, AnimStateParameterType valueType)
            {
                Path = path;
                Key = key;
                ValueType = valueType;
            }

            public string Path { get; }
            public string Key { get; }
            public AnimStateParameterType ValueType { get; }
        }
        private void DrawTransitionInspector(AnimStateTransition transition)
        {
            AnimStateNode from = stateMachine.FindNode(transition.FromStateId);
            AnimStateNode to = stateMachine.FindNode(transition.ToStateId);
            EditorGUILayout.LabelField("Route", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(from?.Name ?? "Missing", to?.Name ?? "Missing");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Timing", EditorStyles.miniBoldLabel);
            AnimStateTransitionTiming timing = (AnimStateTransitionTiming)EditorGUILayout.EnumPopup(
                "Transition Timing", transition.Timing);
            float exit = transition.ExitTime;
            if (timing == AnimStateTransitionTiming.ExitTime)
                exit = EditorGUILayout.Slider("Exit Time", exit, 0f, 1f);
            else if (timing == AnimStateTransitionTiming.AnimationEnd)
            {
                EditorGUILayout.HelpBox(
                    "State 애니메이션이 끝난 뒤 전환합니다. Loop State에서는 실행되지 않습니다.",
                    MessageType.None);
                if (from is AnimSequenceState { Loop: true })
                    EditorGUILayout.HelpBox("현재 State가 Loop라서 Animation End에 도달하지 않습니다.",
                        MessageType.Warning);
            }
            float duration = Mathf.Max(0f, EditorGUILayout.FloatField("Blend", transition.Duration));
            if (timing != transition.Timing || !Mathf.Approximately(exit, transition.ExitTime)
                || !Mathf.Approximately(duration, transition.Duration))
            {
                Undo.RecordObject(stateMachine, "Edit Animation Transition");
                transition.Timing = timing;
                transition.ExitTime = exit;
                transition.Duration = duration;
                Save();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Rule", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Conditions", transition.Conditions.Count.ToString());
            if (GUILayout.Button("Open Transition Rule", GUILayout.Height(25f)))
            {
                OpenTransitionRule(transition.Id);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Delete Transition", GUILayout.Height(24f)))
            {
                Undo.RecordObject(stateMachine, "Delete Animation Transition");
                stateMachine.RemoveTransition(transition.Id);
                ClearTransitionSelection();
                Save();
                GUIUtility.ExitGUI();
            }
        }
        private static AnimStateConditionMode DrawConditionMode(
            AnimStateParameter parameter,
            AnimStateConditionMode current)
        {
            if (parameter == null)
                return current;

            if (parameter.Type is AnimStateParameterType.Bool or AnimStateParameterType.Trigger)
            {
                string[] labels = { "Is True", "Is False" };
                int selected = current == AnimStateConditionMode.IfNot ? 1 : 0;
                return EditorGUILayout.Popup("Condition", selected, labels) == 0
                    ? AnimStateConditionMode.If
                    : AnimStateConditionMode.IfNot;
            }

            string[] numberLabels = { ">", ">=", "<", "<=", "==", "!=" };
            AnimStateConditionMode[] numberModes =
            {
                AnimStateConditionMode.Greater,
                AnimStateConditionMode.GreaterOrEqual,
                AnimStateConditionMode.Less,
                AnimStateConditionMode.LessOrEqual,
                AnimStateConditionMode.Equals,
                AnimStateConditionMode.NotEqual
            };
            int index = Array.IndexOf(numberModes, current);
            index = EditorGUILayout.Popup("Condition", Mathf.Max(0, index), numberLabels);
            return numberModes[index];
        }

        private static AnimSequenceSO GetDraggedSequence()
        {
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            {
                if (DragAndDrop.objectReferences[i] is AnimSequenceSO sequence)
                    return sequence;
            }
            return null;
        }

        private void ShowNodeContextMenu(AnimStateNode node)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Make Transition"), false, () =>
            {
                connectingFromId = node.Id;
                draggingTransition = false;
                Repaint();
            });
            if (node is not AnimStateAlias)
            {
                bool isDefault = node.Id == stateMachine.GetDefaultNodeId(currentStateMachineId);
                if (isDefault)
                    menu.AddDisabledItem(new GUIContent("Set as Default State"));
                else
                    menu.AddItem(new GUIContent("Set as Default State"), false, () =>
                    {
                        SelectOnlyNode(node.Id);
                        TrySetSelectedAsDefault();
                    });
            }
            if (node is AnimStateMachineNode)
                menu.AddItem(new GUIContent("Open"), false, () => EnterStateMachine(node.Id));
            else if (node is AnimSequenceState state && state.Sequence != null)
                menu.AddItem(new GUIContent("Open Sequence"), false,
                    () => AnimationEditorWindow.Open(state.Sequence, library));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(selectedNodeIds.Count + selectedTransitionIds.Count > 1 ? "Delete Selected" : "Delete"), false,
                () =>
                {
                    if (IsNodeSelected(node.Id) && selectedNodeIds.Count + selectedTransitionIds.Count > 1)
                        DeleteSelectedItems();
                    else
                        DeleteNode(node.Id);
                });
            menu.ShowAsContext();
        }

        private void ShowGraphContextMenu(Vector2 graphPosition) => ShowAddNodeMenu(graphPosition);

        private void ShowAddStateMenu(Vector2 graphPosition) => ShowAddNodeMenu(graphPosition);

        private void ShowGridSizeMenu()
        {
            GenericMenu menu = new();
            AddGridSizeMenuItem(menu, 12f);
            AddGridSizeMenuItem(menu, 24f);
            AddGridSizeMenuItem(menu, 48f);
            menu.ShowAsContext();
        }

        private void AddGridSizeMenuItem(GenericMenu menu, float size)
        {
            menu.AddItem(new GUIContent(size.ToString("0")),
                Mathf.Approximately(graphGridSize, size), () =>
                {
                    graphGridSize = size;
                    Repaint();
                });
        }

        private void ShowRuleArrangeMenu(AnimStateTransition transition)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Align/Left"), false, () => AlignSelectedRuleItems(transition, 0));
            menu.AddItem(new GUIContent("Align/Horizontal Center"), false, () => AlignSelectedRuleItems(transition, 1));
            menu.AddItem(new GUIContent("Align/Right"), false, () => AlignSelectedRuleItems(transition, 2));
            menu.AddItem(new GUIContent("Align/Top"), false, () => AlignSelectedRuleItems(transition, 3));
            menu.AddItem(new GUIContent("Align/Vertical Middle"), false, () => AlignSelectedRuleItems(transition, 4));
            menu.AddItem(new GUIContent("Align/Bottom"), false, () => AlignSelectedRuleItems(transition, 5));
            menu.AddSeparator(string.Empty);
            if (selectedRuleIds.Count >= 3)
            {
                menu.AddItem(new GUIContent("Distribute/Horizontally"), false,
                    () => DistributeSelectedRuleItems(transition, true));
                menu.AddItem(new GUIContent("Distribute/Vertically"), false,
                    () => DistributeSelectedRuleItems(transition, false));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Distribute/Horizontally"));
                menu.AddDisabledItem(new GUIContent("Distribute/Vertically"));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Layout/Grid    G"), false,
                () => ArrangeSelectedRuleItemsAsGrid(transition));
            menu.AddItem(new GUIContent("Snap To Grid    Shift+S"), false,
                () => SnapSelectedRuleItems(transition));
            menu.ShowAsContext();
        }

        private List<string> GetSelectedRuleItems(AnimStateTransition transition)
        {
            var result = new List<string>(selectedRuleIds.Count);
            foreach (string itemId in selectedRuleIds)
                if (TryGetRuleItemPosition(transition, itemId, out _))
                    result.Add(itemId);
            return result;
        }

        private static Vector2 GetRuleItemSize(
            AnimStateTransition transition,
            string itemId)
        {
            if (itemId == RuleResultNodeId)
                return RuleResultSize;
            return FindRuleNode(transition, itemId) != null
                ? RuleOperatorSize
                : RuleNodeSize;
        }

        private void AlignSelectedRuleItems(AnimStateTransition transition, int mode)
        {
            List<string> items = GetSelectedRuleItems(transition);
            if (items.Count < 2)
                return;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < items.Count; i++)
            {
                TryGetRuleItemPosition(transition, items[i], out Vector2 position);
                Vector2 size = GetRuleItemSize(transition, items[i]);
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x + size.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y + size.y);
            }

            Undo.RecordObject(stateMachine, "Align Rule Nodes");
            for (int i = 0; i < items.Count; i++)
            {
                string itemId = items[i];
                TryGetRuleItemPosition(transition, itemId, out Vector2 position);
                Vector2 size = GetRuleItemSize(transition, itemId);
                switch (mode)
                {
                    case 0: position.x = minX; break;
                    case 1: position.x = (minX + maxX - size.x) * 0.5f; break;
                    case 2: position.x = maxX - size.x; break;
                    case 3: position.y = minY; break;
                    case 4: position.y = (minY + maxY - size.y) * 0.5f; break;
                    case 5: position.y = maxY - size.y; break;
                }
                SetRuleItemPosition(transition, itemId, position);
            }
            Save();
        }

        private void DistributeSelectedRuleItems(
            AnimStateTransition transition,
            bool horizontal)
        {
            List<string> items = GetSelectedRuleItems(transition);
            if (items.Count < 3)
                return;
            items.Sort((left, right) =>
            {
                TryGetRuleItemPosition(transition, left, out Vector2 leftPosition);
                TryGetRuleItemPosition(transition, right, out Vector2 rightPosition);
                return horizontal
                    ? leftPosition.x.CompareTo(rightPosition.x)
                    : leftPosition.y.CompareTo(rightPosition.y);
            });

            TryGetRuleItemPosition(transition, items[0], out Vector2 first);
            TryGetRuleItemPosition(transition, items[^1], out Vector2 last);
            float start = horizontal ? first.x : first.y;
            float end = horizontal ? last.x : last.y;
            float step = (end - start) / (items.Count - 1);
            Undo.RecordObject(stateMachine, "Distribute Rule Nodes");
            for (int i = 1; i < items.Count - 1; i++)
            {
                TryGetRuleItemPosition(transition, items[i], out Vector2 position);
                if (horizontal)
                    position.x = start + step * i;
                else
                    position.y = start + step * i;
                SetRuleItemPosition(transition, items[i], position);
            }
            Save();
        }

        private void ArrangeSelectedRuleItemsAsGrid(AnimStateTransition transition)
        {
            List<string> items = GetSelectedRuleItems(transition);
            if (items.Count < 2)
                return;
            items.Sort((left, right) =>
            {
                TryGetRuleItemPosition(transition, left, out Vector2 leftPosition);
                TryGetRuleItemPosition(transition, right, out Vector2 rightPosition);
                int row = leftPosition.y.CompareTo(rightPosition.y);
                return row != 0 ? row : leftPosition.x.CompareTo(rightPosition.x);
            });

            Vector2 anchor = new(float.MaxValue, float.MaxValue);
            for (int i = 0; i < items.Count; i++)
            {
                TryGetRuleItemPosition(transition, items[i], out Vector2 position);
                anchor = Vector2.Min(anchor, position);
            }
            anchor = SnapRulePosition(anchor);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(items.Count));
            Vector2 spacing = new(RuleNodeSize.x + graphGridSize * 2f,
                RuleNodeSize.y + graphGridSize * 2f);
            Undo.RecordObject(stateMachine, "Arrange Rule Nodes");
            for (int i = 0; i < items.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                SetRuleItemPosition(transition, items[i],
                    anchor + new Vector2(column * spacing.x, row * spacing.y));
            }
            Save();
        }

        private void SnapSelectedRuleItems(AnimStateTransition transition)
        {
            List<string> items = GetSelectedRuleItems(transition);
            if (items.Count == 0)
                return;
            Undo.RecordObject(stateMachine, "Snap Rule Nodes");
            for (int i = 0; i < items.Count; i++)
            {
                TryGetRuleItemPosition(transition, items[i], out Vector2 position);
                SetRuleItemPosition(transition, items[i], position);
            }
            Save();
        }

        private void ShowArrangeMenu()
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Align/Left    Shift+Alt+Left"), false, () => AlignSelectedNodes(0));
            menu.AddItem(new GUIContent("Align/Horizontal Center    Ctrl+Shift+H"), false, () => AlignSelectedNodes(1));
            menu.AddItem(new GUIContent("Align/Right    Shift+Alt+Right"), false, () => AlignSelectedNodes(2));
            menu.AddItem(new GUIContent("Align/Top    Shift+Alt+Up"), false, () => AlignSelectedNodes(3));
            menu.AddItem(new GUIContent("Align/Vertical Middle    Ctrl+Shift+V"), false, () => AlignSelectedNodes(4));
            menu.AddItem(new GUIContent("Align/Bottom    Shift+Alt+Down"), false, () => AlignSelectedNodes(5));
            menu.AddSeparator("");
            if (selectedNodeIds.Count >= 3)
            {
                menu.AddItem(new GUIContent("Distribute/Horizontally    Shift+H"), false,
                    () => DistributeSelectedNodes(true));
                menu.AddItem(new GUIContent("Distribute/Vertically    Shift+V"), false,
                    () => DistributeSelectedNodes(false));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Distribute/Horizontally    Shift+H"));
                menu.AddDisabledItem(new GUIContent("Distribute/Vertically    Shift+V"));
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Layout/Grid    G"), false, ArrangeSelectedAsGrid);
            menu.AddItem(new GUIContent("Snap To Grid Now    Shift+S"), false, SnapSelectedNodes);
            menu.ShowAsContext();
        }

        private List<AnimStateNode> GetSelectedNodes()
        {
            var nodes = new List<AnimStateNode>(selectedNodeIds.Count);
            foreach (string id in selectedNodeIds)
            {
                AnimStateNode node = stateMachine.FindNode(id);
                if (node != null && node.ParentStateMachineId == currentStateMachineId)
                    nodes.Add(node);
            }
            return nodes;
        }

        private void AlignSelectedNodes(int mode)
        {
            List<AnimStateNode> nodes = GetSelectedNodes();
            if (nodes.Count < 2)
                return;
            Undo.RecordObject(stateMachine, "Align Animation Nodes");

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                minX = Mathf.Min(minX, nodes[i].Position.x);
                maxX = Mathf.Max(maxX, nodes[i].Position.x + NodeSize.x);
                minY = Mathf.Min(minY, nodes[i].Position.y);
                maxY = Mathf.Max(maxY, nodes[i].Position.y + NodeSize.y);
            }

            float centerX = (minX + maxX - NodeSize.x) * 0.5f;
            float centerY = (minY + maxY - NodeSize.y) * 0.5f;
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 position = nodes[i].Position;
                switch (mode)
                {
                    case 0: position.x = minX; break;
                    case 1: position.x = centerX; break;
                    case 2: position.x = maxX - NodeSize.x; break;
                    case 3: position.y = minY; break;
                    case 4: position.y = centerY; break;
                    case 5: position.y = maxY - NodeSize.y; break;
                }
                nodes[i].Position = snapToGrid ? SnapPosition(position) : position;
            }
            Save();
        }

        private void DistributeSelectedNodes(bool horizontal)
        {
            List<AnimStateNode> nodes = GetSelectedNodes();
            if (nodes.Count < 3)
                return;
            nodes.Sort((left, right) => horizontal
                ? left.Position.x.CompareTo(right.Position.x)
                : left.Position.y.CompareTo(right.Position.y));
            Undo.RecordObject(stateMachine, "Distribute Animation Nodes");

            float start = horizontal ? nodes[0].Position.x : nodes[0].Position.y;
            float end = horizontal ? nodes[^1].Position.x : nodes[^1].Position.y;
            float step = (end - start) / (nodes.Count - 1);
            for (int i = 1; i < nodes.Count - 1; i++)
            {
                Vector2 position = nodes[i].Position;
                if (horizontal)
                    position.x = start + step * i;
                else
                    position.y = start + step * i;
                nodes[i].Position = snapToGrid ? SnapPosition(position) : position;
            }
            Save();
        }

        private void ArrangeSelectedAsGrid()
        {
            List<AnimStateNode> nodes = GetSelectedNodes();
            if (nodes.Count < 2)
                return;
            nodes.Sort((left, right) =>
            {
                int row = left.Position.y.CompareTo(right.Position.y);
                return row != 0 ? row : left.Position.x.CompareTo(right.Position.x);
            });
            Undo.RecordObject(stateMachine, "Arrange Animation Nodes");

            Vector2 anchor = nodes[0].Position;
            for (int i = 1; i < nodes.Count; i++)
                anchor = Vector2.Min(anchor, nodes[i].Position);
            anchor = GetGridPosition(anchor);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(nodes.Count));
            Vector2 spacing = new(NodeSize.x + graphGridSize * 2f, NodeSize.y + graphGridSize * 2f);
            for (int i = 0; i < nodes.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                nodes[i].Position = GetGridPosition(anchor + new Vector2(column * spacing.x, row * spacing.y));
            }
            Save();
        }

        private void SnapSelectedNodes()
        {
            List<AnimStateNode> nodes = GetSelectedNodes();
            if (nodes.Count == 0)
                return;
            Undo.RecordObject(stateMachine, "Snap Animation Nodes");
            for (int i = 0; i < nodes.Count; i++)
                nodes[i].Position = GetGridPosition(nodes[i].Position);
            Save();
        }
        private void ShowAddNodeMenu(Vector2 graphPosition)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Sequence State..."), false, () => ShowSequencePicker(graphPosition));
            menu.AddItem(new GUIContent("Empty State"), false, () => AddState(null, graphPosition));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Flow/Conduit", "조건에 따라 다음 실행 State를 고릅니다."),
                false, () => AddConduit(graphPosition));
            menu.AddItem(new GUIContent("Rules/Alias", "여러 State가 같은 Transition을 공유합니다."),
                false, () => AddAlias(graphPosition));
            menu.AddItem(new GUIContent("State Machine"), false, () => AddStateMachine(graphPosition));
            menu.ShowAsContext();
        }

        private void ShowSequencePicker(Vector2 graphPosition)
        {
            Predicate<AnimSequenceSO> filter = library == null ? null : sequence => library.Contains(sequence);
            MontageObjectPickerWindow.Show<AnimSequenceSO>(
                "Add Sequence State", sequence => AddState(sequence, graphPosition), filter,
                "Empty State", () => AddState(null, graphPosition));
        }
        private void ShowAddParameterMenu()
        {
            GenericMenu menu = new();
            foreach (AnimStateParameterType type in Enum.GetValues(typeof(AnimStateParameterType)))
            {
                AnimStateParameterType captured = type;
                menu.AddItem(new GUIContent(type.ToString()), false, () =>
                {
                    Undo.RecordObject(stateMachine, "Add Animation Parameter");
                    stateMachine.AddParameter(type.ToString(), captured);
                    Save();
                });
            }
            menu.ShowAsContext();
        }

        private void AddState(AnimSequenceSO sequence, Vector2 graphPosition)
        {
            Undo.RecordObject(stateMachine, "Add Animation State");
            SelectAddedNode(stateMachine.AddState(sequence, graphPosition, currentStateMachineId));
        }

        private void AddConduit(Vector2 graphPosition)
        {
            Undo.RecordObject(stateMachine, "Add Conduit");
            SelectAddedNode(stateMachine.AddConduit(graphPosition, currentStateMachineId));
        }

        private void AddAlias(Vector2 graphPosition)
        {
            Undo.RecordObject(stateMachine, "Add Alias");
            SelectAddedNode(stateMachine.AddAlias(graphPosition, currentStateMachineId));
        }

        private void AddStateMachine(Vector2 graphPosition)
        {
            Undo.RecordObject(stateMachine, "Add State Machine");
            SelectAddedNode(stateMachine.AddStateMachine(graphPosition, currentStateMachineId));
        }

        private void SelectAddedNode(AnimStateNode node)
        {
            SelectOnlyNode(node.Id);
            showInspector = true;
            Save();
        }
        private void CreateTransition(string fromNodeId, string toNodeId)
        {
            Undo.RecordObject(stateMachine, "Add Animation Transition");
            AnimStateTransition transition = stateMachine.AddTransition(fromNodeId, toNodeId);
            if (transition == null)
                return;
            SelectOnlyTransition(transition.Id);
            showInspector = true;
            Save();
        }
        private void CopyStateSelection()
        {
            List<AnimStateNode> nodes = GetSelectedNodes();
            if (nodes.Count == 0)
                return;

            var clipboard = new StateGraphClipboard();
            var copiedIds = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                AnimStateNode node = nodes[i];
                copiedIds.Add(node.Id);
                var copy = new StateNodeCopy
                {
                    SourceId = node.Id,
                    Name = node.Name,
                    Position = node.Position
                };
                switch (node)
                {
                    case AnimSequenceState state:
                        copy.Type = ClipboardNodeType.State;
                        copy.Sequence = state.Sequence;
                        copy.Speed = state.Speed;
                        copy.Loop = state.Loop;
                        break;
                    case AnimStateConduit:
                        copy.Type = ClipboardNodeType.Conduit;
                        break;
                    case AnimStateAlias alias:
                        copy.Type = ClipboardNodeType.Alias;
                        for (int sourceIndex = 0; sourceIndex < alias.SourceNodeIds.Count; sourceIndex++)
                            copy.AliasSources.Add(alias.SourceNodeIds[sourceIndex]);
                        break;
                    case AnimStateMachineNode machine:
                        copy.Type = ClipboardNodeType.StateMachine;
                        copy.EntryPosition = machine.EntryPosition;
                        copy.DefaultNodeId = machine.DefaultNodeId;
                        break;
                }
                clipboard.Nodes.Add(copy);
            }

            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                AnimStateTransition transition = stateMachine.Transitions[i];
                if (transition == null || !copiedIds.Contains(transition.FromStateId)
                    || !copiedIds.Contains(transition.ToStateId))
                    continue;
                clipboard.Transitions.Add(new TransitionCopy
                {
                    FromId = transition.FromStateId,
                    ToId = transition.ToStateId,
                    Timing = transition.Timing,
                    ExitTime = transition.ExitTime,
                    Duration = transition.Duration,
                    Rule = CaptureRule(transition, null)
                });
            }
            stateClipboard = clipboard;
        }

        private void PasteStateSelection()
        {
            if (stateClipboard == null || stateClipboard.Nodes.Count == 0)
                return;

            Undo.RecordObject(stateMachine, "Paste Animation Nodes");
            stateClipboard.PasteCount++;
            Vector2 offset = Vector2.one * (32f * stateClipboard.PasteCount);
            var idMap = new Dictionary<string, string>();
            var pastedNodes = new List<AnimStateNode>();

            for (int i = 0; i < stateClipboard.Nodes.Count; i++)
            {
                StateNodeCopy copy = stateClipboard.Nodes[i];
                AnimStateNode node = copy.Type switch
                {
                    ClipboardNodeType.State => stateMachine.AddState(copy.Sequence,
                        copy.Position + offset, currentStateMachineId),
                    ClipboardNodeType.Conduit => stateMachine.AddConduit(
                        copy.Position + offset, currentStateMachineId),
                    ClipboardNodeType.Alias => stateMachine.AddAlias(
                        copy.Position + offset, currentStateMachineId),
                    ClipboardNodeType.StateMachine => stateMachine.AddStateMachine(
                        copy.Position + offset, currentStateMachineId),
                    _ => null
                };
                if (node == null)
                    continue;
                node.Name = copy.Name + " Copy";
                if (node is AnimSequenceState state)
                {
                    state.Speed = copy.Speed;
                    state.Loop = copy.Loop;
                }
                else if (node is AnimStateMachineNode machine)
                    machine.EntryPosition = copy.EntryPosition;
                idMap[copy.SourceId] = node.Id;
                pastedNodes.Add(node);
            }

            for (int i = 0; i < stateClipboard.Nodes.Count; i++)
            {
                StateNodeCopy copy = stateClipboard.Nodes[i];
                if (!idMap.TryGetValue(copy.SourceId, out string newId))
                    continue;
                if (stateMachine.FindNode(newId) is AnimStateAlias alias)
                {
                    for (int sourceIndex = 0; sourceIndex < copy.AliasSources.Count; sourceIndex++)
                        if (idMap.TryGetValue(copy.AliasSources[sourceIndex], out string sourceId))
                            alias.AddSource(sourceId);
                }
                else if (stateMachine.FindNode(newId) is AnimStateMachineNode
                         && idMap.TryGetValue(copy.DefaultNodeId, out string defaultId))
                    stateMachine.SetDefaultNode(newId, defaultId);
            }

            for (int i = 0; i < stateClipboard.Transitions.Count; i++)
            {
                TransitionCopy copy = stateClipboard.Transitions[i];
                if (!idMap.TryGetValue(copy.FromId, out string fromId)
                    || !idMap.TryGetValue(copy.ToId, out string toId))
                    continue;
                AnimStateTransition transition = stateMachine.AddTransition(fromId, toId);
                if (transition == null)
                    continue;
                transition.Timing = copy.Timing;
                transition.ExitTime = copy.ExitTime;
                transition.Duration = copy.Duration;
                PasteRule(transition, copy.Rule, Vector2.zero, false);
            }

            ClearSelection();
            for (int i = 0; i < pastedNodes.Count; i++)
                selectedNodeIds.Add(pastedNodes[i].Id);
            selectedNodeId = pastedNodes.Count > 0 ? pastedNodes[0].Id : null;
            showInspector = pastedNodes.Count > 0;
            Save();
        }

        private void CopyRuleSelection(AnimStateTransition transition)
        {
            var copiedIds = new HashSet<string>(selectedRuleIds);
            copiedIds.Remove(RuleResultNodeId);
            if (copiedIds.Count == 0)
                return;
            ruleClipboard = CaptureRule(transition, copiedIds);
        }

        private void PasteRuleSelection(AnimStateTransition transition)
        {
            if (ruleClipboard == null
                || ruleClipboard.Conditions.Count + ruleClipboard.Operators.Count == 0)
                return;
            Undo.RecordObject(stateMachine, "Paste Rule Nodes");
            ruleClipboard.PasteCount++;
            PasteRule(transition, ruleClipboard,
                Vector2.one * (32f * ruleClipboard.PasteCount), true);
            Save();
        }

        private static RuleGraphClipboard CaptureRule(
            AnimStateTransition transition,
            HashSet<string> includedIds)
        {
            var clipboard = new RuleGraphClipboard
            {
                ResultPosition = transition.RuleResultPosition,
                ResultSourceId = transition.RuleResultSourceId
            };
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition == null || includedIds != null && !includedIds.Contains(condition.Id))
                    continue;
                clipboard.Conditions.Add(new RuleConditionCopy
                {
                    SourceId = condition.Id,
                    Source = condition.Source,
                    Parameter = condition.Parameter,
                    OwnerType = condition.OwnerType,
                    OwnerMember = condition.OwnerMember,
                    ValueType = condition.ValueType,
                    Mode = condition.Mode,
                    Threshold = condition.Threshold,
                    Position = condition.RulePosition,
                    TargetId = condition.RuleTargetId
                });
            }
            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node == null || includedIds != null && !includedIds.Contains(node.Id))
                    continue;
                clipboard.Operators.Add(new RuleOperatorCopy
                {
                    SourceId = node.Id,
                    Operation = node.Operation,
                    Position = node.Position,
                    TargetId = node.TargetId
                });
            }
            return clipboard;
        }

        private void PasteRule(AnimStateTransition transition, RuleGraphClipboard clipboard,
            Vector2 offset, bool selectPasted)
        {
            if (clipboard == null)
                return;
            bool hadResult = !string.IsNullOrEmpty(transition.RuleResultSourceId);
            var idMap = new Dictionary<string, string>();
            var pastedIds = new List<string>();

            for (int i = 0; i < clipboard.Conditions.Count; i++)
            {
                RuleConditionCopy copy = clipboard.Conditions[i];
                var condition = new AnimStateCondition
                {
                    Source = copy.Source,
                    Parameter = copy.Parameter,
                    OwnerType = copy.OwnerType,
                    OwnerMember = copy.OwnerMember,
                    ValueType = copy.ValueType,
                    Mode = copy.Mode,
                    Threshold = copy.Threshold,
                    RulePosition = copy.Position + offset
                };
                transition.AddCondition(condition);
                idMap[copy.SourceId] = condition.Id;
                pastedIds.Add(condition.Id);
            }
            for (int i = 0; i < clipboard.Operators.Count; i++)
            {
                RuleOperatorCopy copy = clipboard.Operators[i];
                AnimStateRuleNode node = transition.AddRuleNode(copy.Operation, copy.Position + offset);
                idMap[copy.SourceId] = node.Id;
                pastedIds.Add(node.Id);
            }
            for (int i = 0; i < clipboard.Conditions.Count; i++)
            {
                RuleConditionCopy copy = clipboard.Conditions[i];
                if (idMap.TryGetValue(copy.SourceId, out string id))
                    FindRuleCondition(transition, id).RuleTargetId =
                        idMap.TryGetValue(copy.TargetId, out string targetId) ? targetId : string.Empty;
            }
            for (int i = 0; i < clipboard.Operators.Count; i++)
            {
                RuleOperatorCopy copy = clipboard.Operators[i];
                if (idMap.TryGetValue(copy.SourceId, out string id))
                    FindRuleNode(transition, id).TargetId =
                        idMap.TryGetValue(copy.TargetId, out string targetId) ? targetId : string.Empty;
            }
            if (!hadResult && idMap.TryGetValue(clipboard.ResultSourceId, out string resultId))
                transition.RuleResultSourceId = resultId;
            if (!selectPasted)
            {
                transition.RuleResultPosition = clipboard.ResultPosition;
                return;
            }
            ClearRuleSelection();
            for (int i = 0; i < pastedIds.Count; i++)
                selectedRuleIds.Add(pastedIds[i]);
            UpdateRulePrimaryFromSelection(transition);
        }

        private static AnimStateCondition FindRuleCondition(
            AnimStateTransition transition,
            string conditionId)
        {
            for (int i = 0; i < transition.Conditions.Count; i++)
                if (transition.Conditions[i]?.Id == conditionId)
                    return transition.Conditions[i];
            return null;
        }
        private void DeleteNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || nodeId == EntryNodeId)
                return;
            Undo.RecordObject(stateMachine, "Delete Animation Node");
            stateMachine.RemoveNode(nodeId);
            selectedNodeIds.Remove(nodeId);
            selectedNodeId = GetAnySelectedNodeId();
            ClearTransitionSelection();
            Save();
        }
        private void HandleRuleKeyboard()
        {
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown)
                return;

            if (evt.keyCode == KeyCode.Escape && !string.IsNullOrEmpty(connectingRuleSourceId))
            {
                connectingRuleSourceId = null;
                UseShortcut(evt);
                return;
            }

            if (EditorGUIUtility.editingTextField)
                return;

            if (evt.keyCode == KeyCode.Escape
                || (evt.alt && !evt.shift && evt.keyCode == KeyCode.LeftArrow))
            {
                if (!NavigateBack())
                    CloseTransitionRule();
                evt.Use();
                return;
            }

            if (evt.alt && !evt.shift && evt.keyCode == KeyCode.RightArrow)
            {
                if (NavigateForward())
                    evt.Use();
                return;
            }


            AnimStateTransition transition = FindTransition(editingTransitionId);
            if (transition == null)
                return;

            bool actionKey = evt.control || evt.command;
            if (actionKey && evt.keyCode == KeyCode.C)
            {
                CopyRuleSelection(transition);
                UseShortcut(evt);
                return;
            }
            if (actionKey && evt.keyCode == KeyCode.V && !EditorApplication.isPlaying)
            {
                PasteRuleSelection(transition);
                UseShortcut(evt);
                return;
            }
            if (actionKey && evt.keyCode == KeyCode.A)
            {
                SelectAllRuleItems(transition);
                UseShortcut(evt);
                return;
            }
            if (evt.keyCode == KeyCode.F && !actionKey && !evt.alt)
            {
                FrameRuleGraph(transition);
                UseShortcut(evt);
                return;
            }
            if (EditorApplication.isPlaying)
                return;

            if (evt.keyCode == KeyCode.G && !actionKey && !evt.alt
                && selectedRuleIds.Count >= 2)
            {
                ArrangeSelectedRuleItemsAsGrid(transition);
                UseShortcut(evt);
                return;
            }
            if (evt.shift && !actionKey && evt.keyCode == KeyCode.S
                && selectedRuleIds.Count > 0)
            {
                SnapSelectedRuleItems(transition);
                UseShortcut(evt);
                return;
            }
            if (evt.alt && evt.shift && selectedRuleIds.Count >= 2
                && TryGetArrowAlignment(evt.keyCode, out int alignment))
            {
                AlignSelectedRuleItems(transition, alignment);
                UseShortcut(evt);
                return;
            }
            if (evt.shift && actionKey && selectedRuleIds.Count >= 2
                && evt.keyCode is KeyCode.H or KeyCode.V)
            {
                AlignSelectedRuleItems(transition, evt.keyCode == KeyCode.H ? 1 : 4);
                UseShortcut(evt);
                return;
            }
            if (evt.shift && !actionKey && selectedRuleIds.Count >= 3
                && evt.keyCode is KeyCode.H or KeyCode.V)
            {
                DistributeSelectedRuleItems(transition, evt.keyCode == KeyCode.H);
                UseShortcut(evt);
                return;
            }

            if (evt.keyCode is not (KeyCode.Delete or KeyCode.Backspace))
                return;
            DeleteSelectedRuleItems(transition);
            UseShortcut(evt);
        }

        private void SelectAllRuleItems(AnimStateTransition transition)
        {
            selectedRuleIds.Clear();
            for (int i = 0; i < transition.Conditions.Count; i++)
                if (transition.Conditions[i] != null)
                    selectedRuleIds.Add(transition.Conditions[i].Id);
            for (int i = 0; i < transition.RuleNodes.Count; i++)
                if (transition.RuleNodes[i] != null)
                    selectedRuleIds.Add(transition.RuleNodes[i].Id);
            selectedRuleIds.Add(RuleResultNodeId);
            UpdateRulePrimaryFromSelection(transition);
        }

        private void DeleteSelectedRuleItems(AnimStateTransition transition)
        {
            bool hasDeletable = false;
            for (int i = 0; i < transition.Conditions.Count; i++)
                hasDeletable |= transition.Conditions[i] != null
                                && selectedRuleIds.Contains(transition.Conditions[i].Id);
            for (int i = 0; i < transition.RuleNodes.Count; i++)
                hasDeletable |= transition.RuleNodes[i] != null
                                && selectedRuleIds.Contains(transition.RuleNodes[i].Id);
            if (!hasDeletable)
                return;

            Undo.RecordObject(stateMachine, "Remove Rule Nodes");
            for (int i = transition.Conditions.Count - 1; i >= 0; i--)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition != null && selectedRuleIds.Contains(condition.Id))
                    transition.RemoveConditionAt(i);
            }
            for (int i = transition.RuleNodes.Count - 1; i >= 0; i--)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node != null && selectedRuleIds.Contains(node.Id))
                    transition.RemoveRuleNode(node.Id);
            }
            ClearRuleSelection();
            Save();
        }
        private void HandleKeyboard()
        {
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown || stateMachine == null)
                return;

            if (evt.keyCode == KeyCode.Escape && connectingFromId != null)
            {
                connectingFromId = null;
                draggingTransition = false;
                evt.Use();
                Repaint();
                return;
            }

            if (EditorGUIUtility.editingTextField)
                return;

            if (evt.alt && !evt.shift
                && evt.keyCode is KeyCode.LeftArrow or KeyCode.RightArrow)
            {
                bool moved = evt.keyCode == KeyCode.LeftArrow
                    ? NavigateBack()
                    : NavigateForward();
                if (moved)
                    evt.Use();
                return;
            }

            if (EditorApplication.isPlaying)
                return;

            bool actionKey = evt.control || evt.command;
            if (actionKey && evt.keyCode == KeyCode.C)
            {
                CopyStateSelection();
                UseShortcut(evt);
                return;
            }
            if (actionKey && evt.keyCode == KeyCode.V)
            {
                PasteStateSelection();
                UseShortcut(evt);
                return;
            }
            if (actionKey && evt.keyCode == KeyCode.A)
            {
                SelectAllVisibleItems();
                showInspector = true;
                UseShortcut(evt);
                return;
            }

            if (evt.keyCode == KeyCode.F && !actionKey && !evt.alt)
            {
                FrameSelectionOrAll();
                UseShortcut(evt);
                return;
            }

            if (evt.keyCode == KeyCode.G && !actionKey && !evt.alt && selectedNodeIds.Count >= 2)
            {
                ArrangeSelectedAsGrid();
                UseShortcut(evt);
                return;
            }

            if (evt.shift && !actionKey && evt.keyCode == KeyCode.S && selectedNodeIds.Count > 0)
            {
                SnapSelectedNodes();
                UseShortcut(evt);
                return;
            }

            if (evt.alt && evt.shift && selectedNodeIds.Count >= 2 && TryGetArrowAlignment(evt.keyCode, out int alignment))
            {
                AlignSelectedNodes(alignment);
                UseShortcut(evt);
                return;
            }

            if (evt.shift && actionKey && selectedNodeIds.Count >= 2
                && evt.keyCode is KeyCode.H or KeyCode.V)
            {
                AlignSelectedNodes(evt.keyCode == KeyCode.H ? 1 : 4);
                UseShortcut(evt);
                return;
            }

            if (evt.shift && !actionKey && selectedNodeIds.Count >= 3
                && evt.keyCode is KeyCode.H or KeyCode.V)
            {
                DistributeSelectedNodes(evt.keyCode == KeyCode.H);
                UseShortcut(evt);
                return;
            }

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;

            if (selectedNodeIds.Count + selectedTransitionIds.Count > 0)
                DeleteSelectedItems();
            UseShortcut(evt);
        }

        private static bool TryGetArrowAlignment(KeyCode keyCode, out int alignment)
        {
            alignment = keyCode switch
            {
                KeyCode.LeftArrow => 0,
                KeyCode.RightArrow => 2,
                KeyCode.UpArrow => 3,
                KeyCode.DownArrow => 5,
                _ => -1
            };
            return alignment >= 0;
        }

        private void UseShortcut(Event evt)
        {
            evt.Use();
            Repaint();
        }

        private void SelectAllVisibleItems()
        {
            selectedNodeIds.Clear();
            selectedTransitionIds.Clear();
            foreach (KeyValuePair<string, Rect> pair in nodeRects)
            {
                if (pair.Key != EntryNodeId)
                    selectedNodeIds.Add(pair.Key);
            }
            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                AnimStateTransition transition = stateMachine.Transitions[i];
                if (transition != null
                    && nodeRects.ContainsKey(transition.FromStateId)
                    && nodeRects.ContainsKey(transition.ToStateId))
                    selectedTransitionIds.Add(transition.Id);
            }
            selectedNodeId = GetAnySelectedNodeId();
            selectedTransitionId = GetAnySelectedTransitionId();
        }

        private void DeleteSelectedItems()
        {
            if (selectedNodeIds.Count + selectedTransitionIds.Count == 0)
                return;

            string[] nodeIds = new string[selectedNodeIds.Count];
            selectedNodeIds.CopyTo(nodeIds);
            string[] transitionIds = new string[selectedTransitionIds.Count];
            selectedTransitionIds.CopyTo(transitionIds);
            Undo.RecordObject(stateMachine, "Delete Animation Graph Selection");
            for (int i = 0; i < transitionIds.Length; i++)
                stateMachine.RemoveTransition(transitionIds[i]);
            for (int i = 0; i < nodeIds.Length; i++)
                stateMachine.RemoveNode(nodeIds[i]);
            ClearSelection();
            Save();
        }
        private bool TrySetSelectedAsDefault()
        {
            if (selectedNodeIds.Count != 1)
                return false;

            AnimStateNode node = stateMachine.FindNode(GetAnySelectedNodeId());
            if (node == null || node is AnimStateAlias
                || node.ParentStateMachineId != currentStateMachineId)
                return false;

            string currentDefault = stateMachine.GetDefaultNodeId(currentStateMachineId);
            if (currentDefault == node.Id)
                return false;

            Undo.RecordObject(stateMachine, "Set Default Animation Node");
            stateMachine.SetDefaultNode(currentStateMachineId, node.Id);
            Save();
            return true;
        }

        private void FrameSelectionOrAll()
        {
            List<AnimStateNode> nodes = GetSelectedNodes();
            if (nodes.Count == 0)
            {
                FrameStates();
                return;
            }

            Vector2 min = nodes[0].Position;
            Vector2 max = nodes[0].Position + NodeSize;
            for (int i = 1; i < nodes.Count; i++)
            {
                min = Vector2.Min(min, nodes[i].Position);
                max = Vector2.Max(max, nodes[i].Position + NodeSize);
            }
            FrameBounds(min, max);
        }
        private void EnterStateMachine(string machineId) =>
            NavigateToLocation(machineId, null);
        private void FrameStates()
        {
            if (stateMachine == null)
                return;
            Vector2 min = stateMachine.GetEntryPosition(currentStateMachineId);
            Vector2 max = min + new Vector2(132f, 48f);
            IncludeVisibleBounds(stateMachine.States, ref min, ref max);
            IncludeVisibleBounds(stateMachine.Conduits, ref min, ref max);
            IncludeVisibleBounds(stateMachine.Aliases, ref min, ref max);
            IncludeVisibleBounds(stateMachine.StateMachines, ref min, ref max);
            FrameBounds(min, max);
        }

        private void FrameBounds(Vector2 min, Vector2 max)
        {
            Vector2 boundsSize = max - min;
            Vector2 available = new(
                Mathf.Max(1f, graphViewportSize.x - 100f),
                Mathf.Max(1f, graphViewportSize.y - 100f));
            float fitZoom = Mathf.Min(
                available.x / Mathf.Max(1f, boundsSize.x),
                available.y / Mathf.Max(1f, boundsSize.y));
            graphZoom = Mathf.Clamp(fitZoom, MinimumGraphZoom, MaximumGraphZoom);
            pan = graphViewportSize * 0.5f - (min + max) * 0.5f * graphZoom;
            Repaint();
        }
        private void IncludeVisibleBounds<T>(IReadOnlyList<T> nodes, ref Vector2 min, ref Vector2 max)
            where T : AnimStateNode
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                T node = nodes[i];
                if (node.ParentStateMachineId != currentStateMachineId)
                    continue;
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position + NodeSize);
            }
        }

        private static string GetNodeDisplayName(AnimStateNode node) => node switch
        {
            AnimStateConduit when node.Name is "Decision" or "Branch" => "Conduit",
            AnimStateAlias when node.Name is "State Alias" or "State Group" or "Transition Group" => "Alias",
            _ => node.Name
        };

        private static string GetNodeTypeName(AnimStateNode node) => node switch
        {
            AnimSequenceState => "State",
            AnimStateConduit => "Conduit",
            AnimStateAlias => "Alias",
            AnimStateMachineNode => "State Machine",
            _ => "Node"
        };
        private AnimStateTransition FindTransition(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                if (stateMachine.Transitions[i].Id == id)
                    return stateMachine.Transitions[i];
            }
            return null;
        }

        private AnimStateParameter FindParameter(string parameterName)
        {
            for (int i = 0; i < stateMachine.Parameters.Count; i++)
            {
                if (stateMachine.Parameters[i].Name == parameterName)
                    return stateMachine.Parameters[i];
            }
            return null;
        }

        private string[] GetParameterNames()
        {
            string[] names = new string[stateMachine.Parameters.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = stateMachine.Parameters[i].Name;
            return names;
        }

        private void Save()
        {
            validationDirty = true;
            EditorUtility.SetDirty(stateMachine);
            Repaint();
        }

        private Rect ToGraphRect(Vector2 graphPosition, Vector2 size) =>
            new(pan + graphPosition * graphZoom, size * graphZoom);

        private Vector2 ScreenToGraph(Vector2 screenPosition) =>
            (screenPosition - pan) / graphZoom;

        private void ZoomAt(Vector2 screenPosition, float amount)
        {
            Vector2 graphPosition = ScreenToGraph(screenPosition);
            graphZoom = Mathf.Clamp(graphZoom + amount, MinimumGraphZoom, MaximumGraphZoom);
            pan = screenPosition - graphPosition * graphZoom;
        }

        private void ResetZoom()
        {
            Vector2 center = graphViewportSize * 0.5f;
            Vector2 graphCenter = ScreenToGraph(center);
            graphZoom = 1f;
            pan = center - graphCenter;
            Repaint();
        }

        private static Rect GetPortRect(Rect nodeRect, Vector2 direction)
        {
            Vector2 center = GetPortCenter(nodeRect, direction);
            return new Rect(center.x - 9f, center.y - 9f, 18f, 18f);
        }

        private static Vector2 GetPortCenter(Rect nodeRect, Vector2 direction)
        {
            if (direction == Vector2.left)
                return new Vector2(nodeRect.xMin, nodeRect.center.y);
            if (direction == Vector2.right)
                return new Vector2(nodeRect.xMax, nodeRect.center.y);
            if (direction == GraphUp)
                return new Vector2(nodeRect.center.x, nodeRect.yMin);
            return new Vector2(nodeRect.center.x, nodeRect.yMax);
        }

        private static bool TryGetTransitionPort(Rect nodeRect, Vector2 mousePosition, out Vector2 direction)
        {
            for (int i = 0; i < TransitionDirections.Length; i++)
            {
                if (!GetPortRect(nodeRect, TransitionDirections[i]).Contains(mousePosition))
                    continue;
                direction = TransitionDirections[i];
                return true;
            }
            direction = Vector2.zero;
            return false;
        }
        private static void DrawGrid(Rect rect, float spacing, Color color, Vector2 offset)
        {
            Handles.BeginGUI();
            Handles.color = color;
            float startX = rect.x + Mathf.Repeat(offset.x, spacing);
            float startY = rect.y + Mathf.Repeat(offset.y, spacing);
            for (float x = startX; x < rect.xMax; x += spacing)
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
            for (float y = startY; y < rect.yMax; y += spacing)
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            Handles.color = Color.white;
            Handles.EndGUI();
        }
    }
}

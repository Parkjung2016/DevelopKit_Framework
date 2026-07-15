using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    public sealed class MontageRuntimeMonitorWindow : EditorWindow
    {
        private const double RefreshInterval = 0.1d;
        private readonly List<ObjectAnimMontagePlayer> players = new();
        private readonly Dictionary<string, bool> trackFoldoutStates = new();
        private Label modeLabel;
        private Label playerCountLabel;
        private Label playingCountLabel;
        private Label selectedLabel;
        private Toggle autoRefreshToggle;
        private VisualElement listContent;
        private VisualElement trackContent;
        private Label emptyLabel;
        private Label emptyTrackLabel;
        private double nextRefreshTime;
        private ObjectAnimMontagePlayer selectedPlayer;

        [MenuItem("PJDev/Animation/Montage Runtime Monitor", priority = 101)]
        public static void Open()
        {
            var window = GetWindow<MontageRuntimeMonitorWindow>("Montage Runtime");
            window.minSize = new Vector2(360f, 420f);
        }

        private void OnEnable() => EditorApplication.update += OnEditorUpdate;
        private void OnDisable() => EditorApplication.update -= OnEditorUpdate;

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.15f, 0.15f, 0.15f)
                : new Color(0.86f, 0.86f, 0.86f);

            var rootScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            var root = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 10,
                    paddingRight = 10
                }
            };
            rootScroll.Add(root);
            rootVisualElement.Add(rootScroll);

            BuildHeader(root);
            BuildSummary(root);
            BuildPlayerList(root);
            BuildTrackDetails(root);
            Refresh();
        }

        private void BuildHeader(VisualElement parent)
        {
            var header = new VisualElement { style = { marginBottom = 10 } };
            header.Add(new Label("Montage Runtime Monitor")
            {
                style =
                {
                    fontSize = 18,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 3
                }
            });
            header.Add(new Label("씬의 ObjectAnimMontagePlayer 재생 상태와 현재 Montage 트랙을 확인합니다.")
            {
                style =
                {
                    fontSize = 11,
                    whiteSpace = WhiteSpace.Normal,
                    color = MutedTextColor
                }
            });

            var actions = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    alignItems = Align.Center,
                    marginTop = 10
                }
            };
            autoRefreshToggle = new Toggle("Auto") { value = true, tooltip = "자동 새로고침" };
            autoRefreshToggle.style.height = 24;
            autoRefreshToggle.style.marginRight = 8;
            autoRefreshToggle.style.marginBottom = 6;
            actions.Add(autoRefreshToggle);

            Button refreshButton = CreateButton("Refresh");
            refreshButton.clicked += Refresh;
            actions.Add(refreshButton);

            Button selectButton = CreateButton("Select");
            selectButton.tooltip = "선택된 플레이어 GameObject를 Hierarchy에서 선택합니다.";
            selectButton.clicked += SelectCurrentPlayer;
            actions.Add(selectButton);

            Button pingMontageButton = CreateButton("Ping Montage");
            pingMontageButton.tooltip = "선택된 플레이어의 현재 Montage 에셋을 Project 창에서 찾습니다.";
            pingMontageButton.clicked += PingSelectedMontage;
            actions.Add(pingMontageButton);

            header.Add(actions);
            parent.Add(header);
        }

        private void BuildSummary(VisualElement parent)
        {
            var grid = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 10
                }
            };
            modeLabel = AddMetric(grid, "Mode", "Edit");
            playerCountLabel = AddMetric(grid, "Players", "0");
            playingCountLabel = AddMetric(grid, "Playing", "0");
            selectedLabel = AddMetric(grid, "Selected", "None");
            parent.Add(grid);
        }

        private void BuildPlayerList(VisualElement parent)
        {
            var group = CreateGroup("Players", "현재 로드된 씬의 플레이어 목록입니다. 더블클릭하면 Hierarchy에서 선택합니다.");
            AddListHeader(group);

            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                style =
                {
                    minHeight = 150,
                    maxHeight = 250,
                    backgroundColor = ListBackground,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = BorderColor,
                    borderBottomColor = BorderColor,
                    borderLeftColor = BorderColor,
                    borderRightColor = BorderColor,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6
                }
            };
            listContent = new VisualElement { style = { paddingTop = 4, paddingBottom = 4, minWidth = 620 } };
            scroll.Add(listContent);
            group.Add(scroll);

            emptyLabel = CreateEmptyLabel("No ObjectAnimMontagePlayer found in loaded scenes.");
            listContent.Add(emptyLabel);
            parent.Add(group);
        }

        private void BuildTrackDetails(VisualElement parent)
        {
            var group = CreateGroup("Selected Montage Tracks", "선택된 플레이어의 현재 Montage 트랙을 카드 형태로 표시합니다.");
            group.style.marginTop = 10;

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    minHeight = 180,
                    backgroundColor = ListBackground,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = BorderColor,
                    borderBottomColor = BorderColor,
                    borderLeftColor = BorderColor,
                    borderRightColor = BorderColor,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6
                }
            };
            trackContent = new VisualElement { style = { paddingTop = 6, paddingBottom = 6 } };
            emptyTrackLabel = CreateEmptyLabel("Select a player with a Montage to inspect tracks.");
            trackContent.Add(emptyTrackLabel);
            scroll.Add(trackContent);
            group.Add(scroll);
            parent.Add(group);
        }

        private static VisualElement CreateGroup(string title, string subtitle)
        {
            var group = new VisualElement
            {
                style =
                {
                    flexShrink = 0,
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 10,
                    paddingRight = 10,
                    backgroundColor = GroupBackground,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = BorderColor,
                    borderBottomColor = BorderColor,
                    borderLeftColor = BorderColor,
                    borderRightColor = BorderColor,
                    borderTopLeftRadius = 7,
                    borderTopRightRadius = 7,
                    borderBottomLeftRadius = 7,
                    borderBottomRightRadius = 7
                }
            };
            group.Add(new Label(title)
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 2
                }
            });
            group.Add(new Label(subtitle)
            {
                style =
                {
                    fontSize = 10,
                    color = MutedTextColor,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 8
                }
            });
            return group;
        }

        private static Label CreateEmptyLabel(string text) => new(text)
        {
            style =
            {
                unityTextAlign = TextAnchor.MiddleCenter,
                color = MutedTextColor,
                paddingTop = 22,
                paddingBottom = 22,
                whiteSpace = WhiteSpace.Normal
            }
        };

        private static Label AddMetric(VisualElement parent, string title, string value)
        {
            var card = new VisualElement
            {
                style =
                {
                    minWidth = 88,
                    flexGrow = 1,
                    flexShrink = 1,
                    marginRight = 8,
                    marginBottom = 8,
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 10,
                    paddingRight = 10,
                    backgroundColor = CardBackground,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = BorderColor,
                    borderBottomColor = BorderColor,
                    borderLeftColor = BorderColor,
                    borderRightColor = BorderColor,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6
                }
            };
            card.Add(new Label(title)
            {
                style =
                {
                    fontSize = 10,
                    color = MutedTextColor,
                    marginBottom = 3
                }
            });
            var valueLabel = new Label(value)
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    whiteSpace = WhiteSpace.NoWrap
                }
            };
            card.Add(valueLabel);
            parent.Add(card);
            return valueLabel;
        }

        private static void AddListHeader(VisualElement parent)
        {
            var row = CreateRowBase();
            row.style.backgroundColor = HeaderBackground;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = BorderColor;
            AddHeaderCell(row, "Object", 1.4f);
            AddHeaderCell(row, "State", 0.75f);
            AddHeaderCell(row, "Montage", 1.25f);
            AddHeaderCell(row, "Time", 0.9f);
            AddHeaderCell(row, "Sample", 0.85f);
            AddHeaderCell(row, "Root", 0.65f);
            parent.Add(row);
        }

        private void OnEditorUpdate()
        {
            if (autoRefreshToggle == null || !autoRefreshToggle.value)
                return;
            if (EditorApplication.timeSinceStartup < nextRefreshTime)
                return;

            nextRefreshTime = EditorApplication.timeSinceStartup + RefreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            if (listContent == null || trackContent == null)
                return;

            CollectPlayers();
            int playingCount = 0;
            foreach (ObjectAnimMontagePlayer player in players)
            {
                if (player != null && player.IsPlaying && !player.IsPaused)
                    playingCount++;
            }

            modeLabel.text = EditorApplication.isPlaying ? "Play" : "Edit";
            playerCountLabel.text = players.Count.ToString();
            playingCountLabel.text = playingCount.ToString();
            selectedLabel.text = selectedPlayer != null ? selectedPlayer.name : "None";

            listContent.Clear();
            if (players.Count == 0)
                listContent.Add(emptyLabel);
            else
            {
                for (int i = 0; i < players.Count; i++)
                    AddPlayerRow(players[i], i);
            }

            RebuildTrackDetails();
        }

        private void CollectPlayers()
        {
            players.Clear();
            ObjectAnimMontagePlayer[] found = Resources.FindObjectsOfTypeAll<ObjectAnimMontagePlayer>();
            foreach (ObjectAnimMontagePlayer player in found)
            {
                if (player == null || EditorUtility.IsPersistent(player))
                    continue;

                GameObject go = player.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                players.Add(player);
            }

            players.Sort((a, b) => string.CompareOrdinal(GetHierarchyPath(a.transform), GetHierarchyPath(b.transform)));
            if (selectedPlayer != null && !players.Contains(selectedPlayer))
                selectedPlayer = null;
        }

        private void AddPlayerRow(ObjectAnimMontagePlayer player, int index)
        {
            var row = CreateRowBase();
            row.style.backgroundColor = player == selectedPlayer
                ? SelectedRowColor
                : index % 2 == 0 ? RowEvenColor : RowOddColor;
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                selectedPlayer = player;
                if (evt.clickCount >= 2)
                    SelectCurrentPlayer();
                Refresh();
            });

            AnimMontageSO montage = player.CurrentMontage;
            float length = player.CurrentLength;
            string time = length > 0f
                ? $"{player.CurrentTime:0.###}/{length:0.###} ({player.NormalizedTime * 100f:0.#}%)"
                : $"{player.CurrentTime:0.###}/-";
            string sample = length > 0f
                ? $"{player.AnimationSampleTime:0.###} ({player.AnimationSampleNormalizedTime * 100f:0.#}%)"
                : $"{player.AnimationSampleTime:0.###}";

            AddCell(row, GetHierarchyPath(player.transform), 1.4f, FontStyle.Normal, player.gameObject.activeInHierarchy ? null : MutedTextColor);
            AddCell(row, GetStateText(player), 0.75f, FontStyle.Bold, GetStateColor(player));
            AddCell(row, montage != null ? montage.name : "None", 1.25f, FontStyle.Normal, montage != null ? null : MutedTextColor);
            AddCell(row, time, 0.9f);
            AddCell(row, sample, 0.85f);
            AddCell(row, player.RootMotionMode.ToString(), 0.65f);
            listContent.Add(row);
        }

        private void RebuildTrackDetails()
        {
            trackContent.Clear();
            AnimMontageSO montage = selectedPlayer != null ? selectedPlayer.CurrentMontage : null;
            if (montage == null)
            {
                trackContent.Add(emptyTrackLabel);
                return;
            }

            float currentTime = selectedPlayer.CurrentTime;
            AddTrackSection("Animation Tracks", montage.AnimationTracks, trackId => CountSegments(montage, trackId),
                trackId => BuildSegmentSummary(montage, trackId, currentTime), "Animation");
            AddTrackSection("Notify Tracks", montage.NotifyTracks, trackId => CountNotifies(montage, trackId),
                trackId => BuildNotifySummary(montage, trackId, currentTime), "Notify");
            AddTrackSection("Notify State Tracks", montage.NotifyStateTracks, trackId => CountNotifyStates(montage, trackId),
                trackId => BuildNotifyStateSummary(montage, trackId, currentTime), "State");
        }

        private void AddTrackSection(string title, IReadOnlyList<string> tracks, System.Func<string, int> count,
            System.Func<string, string> detail, string kind)
        {
            VisualElement section = CreateTrackSection(title, tracks.Count, title, out bool expanded);
            if (expanded)
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    string trackId = string.IsNullOrEmpty(tracks[i]) ? "Default" : tracks[i];
                    AddTrackCard(section, trackId, kind, count(trackId), detail(trackId));
                }
            }
            trackContent.Add(section);
        }

        private VisualElement CreateTrackSection(string title, int count, string key, out bool expanded)
        {
            if (!trackFoldoutStates.TryGetValue(key, out expanded))
            {
                expanded = true;
                trackFoldoutStates[key] = true;
            }

            var section = new VisualElement
            {
                style =
                {
                    marginBottom = 6,
                    backgroundColor = HeaderBackground,
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5,
                    borderBottomRightRadius = 5
                }
            };

            var header = new VisualElement
            {
                style =
                {
                    height = 24,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = HeaderBackground,
                    marginBottom = expanded ? 4 : 0,
                    paddingLeft = 6,
                    paddingRight = 6
                }
            };
            header.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                trackFoldoutStates[key] = !trackFoldoutStates[key];
                evt.StopImmediatePropagation();
                Refresh();
            }, TrickleDown.TrickleDown);

            header.Add(new Label($"{(expanded ? "▼" : "▶")} {title} ({count})")
            {
                style =
                {
                    flexGrow = 1,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    color = LabelTextColor
                }
            });
            section.Add(header);
            return section;
        }

        private static void AddTrackCard(VisualElement parent, string trackName, string kind, int count, string detail)
        {
            var card = new VisualElement
            {
                style =
                {
                    marginLeft = 12,
                    marginRight = 6,
                    marginTop = 5,
                    marginBottom = 5,
                    paddingTop = 7,
                    paddingBottom = 7,
                    paddingLeft = 9,
                    paddingRight = 9,
                    backgroundColor = count > 0 ? RowEvenColor : RowOddColor,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = BorderColor,
                    borderBottomColor = BorderColor,
                    borderLeftColor = BorderColor,
                    borderRightColor = BorderColor,
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5,
                    borderBottomRightRadius = 5
                }
            };

            var top = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    alignItems = Align.Center,
                    marginBottom = 3
                }
            };
            top.Add(new Label(trackName)
            {
                tooltip = trackName,
                style =
                {
                    flexGrow = 1,
                    minWidth = 110,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    whiteSpace = WhiteSpace.Normal,
                    fontSize = 12
                }
            });
            top.Add(new Label($"{kind} / {count} item{(count == 1 ? string.Empty : "s")}")
            {
                style =
                {
                    color = MutedTextColor,
                    fontSize = 10,
                    whiteSpace = WhiteSpace.NoWrap,
                    marginLeft = 8
                }
            });

            card.Add(top);
            card.Add(new Label(detail)
            {
                tooltip = detail,
                style =
                {
                    color = string.IsNullOrEmpty(detail) ? MutedTextColor : LabelTextColor,
                    fontSize = 11,
                    whiteSpace = WhiteSpace.Normal
                }
            });
            parent.Add(card);
        }

        private static int CountSegments(AnimMontageSO montage, string trackId)
        {
            int count = 0;
            foreach (MontageSegment segment in montage.Segments)
            {
                if (segment != null && segment.TrackId == trackId)
                    count++;
            }
            return count;
        }

        private static int CountNotifies(AnimMontageSO montage, string trackId)
        {
            int count = 0;
            foreach (AnimNotifyPlacement notify in montage.Notifies)
            {
                if (notify != null && notify.TrackId == trackId)
                    count++;
            }
            return count;
        }

        private static int CountNotifyStates(AnimMontageSO montage, string trackId)
        {
            int count = 0;
            foreach (AnimNotifyStatePlacement state in montage.NotifyStates)
            {
                if (state != null && state.TrackId == trackId)
                    count++;
            }
            return count;
        }

        private static string BuildSegmentSummary(AnimMontageSO montage, string trackId, float currentTime)
        {
            foreach (MontageSegment segment in montage.Segments)
            {
                if (segment == null || segment.TrackId != trackId || !segment.ContainsTime(currentTime))
                    continue;

                string clipName = segment.Clip != null ? segment.Clip.name : "Missing Clip";
                return $"Active: {clipName}  {segment.StartTime:0.###}-{segment.EndTime:0.###}";
            }
            return "No active segment";
        }

        private static string BuildNotifySummary(AnimMontageSO montage, string trackId, float currentTime)
        {
            AnimNotifyPlacement next = null;
            foreach (AnimNotifyPlacement notify in montage.Notifies)
            {
                if (notify == null || notify.TrackId != trackId)
                    continue;
                if (Mathf.Abs(notify.Time - currentTime) <= 0.02f)
                    return $"At Cursor: {GetNotifyName(notify)} @ {notify.Time:0.###}";
                if (notify.Time >= currentTime && (next == null || notify.Time < next.Time))
                    next = notify;
            }
            return next != null ? $"Next: {GetNotifyName(next)} @ {next.Time:0.###}" : "No upcoming notify";
        }

        private static string BuildNotifyStateSummary(AnimMontageSO montage, string trackId, float currentTime)
        {
            foreach (AnimNotifyStatePlacement state in montage.NotifyStates)
            {
                if (state == null || state.TrackId != trackId || !state.ContainsTime(currentTime))
                    continue;
                return $"Active: {GetNotifyStateName(state)}  {state.StartTime:0.###}-{state.EndTime:0.###}";
            }
            return "No active state";
        }

        private static string GetNotifyName(AnimNotifyPlacement placement) =>
            placement.Notify != null ? placement.Notify.DisplayName : "Missing Notify";

        private static string GetNotifyStateName(AnimNotifyStatePlacement placement) =>
            placement.NotifyState != null ? placement.NotifyState.DisplayName : "Missing State";

        private void SelectCurrentPlayer()
        {
            if (selectedPlayer == null)
                return;
            Selection.activeGameObject = selectedPlayer.gameObject;
            EditorGUIUtility.PingObject(selectedPlayer.gameObject);
        }

        private void PingSelectedMontage()
        {
            if (selectedPlayer == null || selectedPlayer.CurrentMontage == null)
                return;
            Selection.activeObject = selectedPlayer.CurrentMontage;
            EditorGUIUtility.PingObject(selectedPlayer.CurrentMontage);
        }

        private static string GetStateText(ObjectAnimMontagePlayer player)
        {
            if (player.IsPaused)
                return "Paused";
            if (player.IsPlaying)
                return "Playing";
            return player.CurrentMontage != null ? "Stopped" : "Idle";
        }

        private static Color GetStateColor(ObjectAnimMontagePlayer player)
        {
            if (player.IsPaused)
                return new Color(0.95f, 0.7f, 0.25f);
            if (player.IsPlaying)
                return new Color(0.35f, 0.8f, 0.45f);
            return MutedTextColor;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "Missing";

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static VisualElement CreateRowBase() => new()
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                minHeight = 30,
                paddingLeft = 7,
                paddingRight = 7
            }
        };

        private static void AddHeaderCell(VisualElement row, string text, float grow) =>
            AddCell(row, text, grow, FontStyle.Bold, MutedTextColor);

        private static void AddCell(VisualElement row, string text, float grow, FontStyle style = FontStyle.Normal,
            Color? color = null)
        {
            var label = new Label(text)
            {
                tooltip = text,
                style =
                {
                    flexGrow = grow,
                    flexBasis = 0,
                    minWidth = 48,
                    unityFontStyleAndWeight = style,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap,
                    fontSize = 11
                }
            };
            if (color.HasValue)
                label.style.color = color.Value;
            row.Add(label);
        }

        private static Button CreateButton(string text)
        {
            var button = new Button { text = text };
            button.style.height = 24;
            button.style.minWidth = 0;
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.flexShrink = 1;
            return button;
        }

        private static Color GroupBackground => EditorGUIUtility.isProSkin
            ? new Color(0.19f, 0.19f, 0.19f)
            : new Color(0.94f, 0.94f, 0.94f);

        private static Color CardBackground => EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f)
            : new Color(0.98f, 0.98f, 0.98f);

        private static Color HeaderBackground => EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.24f, 0.24f)
            : new Color(0.82f, 0.82f, 0.82f);

        private static Color ListBackground => EditorGUIUtility.isProSkin
            ? new Color(0.13f, 0.13f, 0.13f)
            : new Color(0.9f, 0.9f, 0.9f);

        private static Color RowEvenColor => EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.16f, 0.16f)
            : new Color(0.96f, 0.96f, 0.96f);

        private static Color RowOddColor => EditorGUIUtility.isProSkin
            ? new Color(0.18f, 0.18f, 0.18f)
            : new Color(0.91f, 0.91f, 0.91f);

        private static Color SelectedRowColor => EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.32f, 0.48f)
            : new Color(0.64f, 0.78f, 0.98f);

        private static Color BorderColor => EditorGUIUtility.isProSkin
            ? new Color(0.08f, 0.08f, 0.08f)
            : new Color(0.62f, 0.62f, 0.62f);

        private static Color LabelTextColor => EditorGUIUtility.isProSkin
            ? new Color(0.86f, 0.86f, 0.86f)
            : new Color(0.12f, 0.12f, 0.12f);

        private static Color MutedTextColor => EditorGUIUtility.isProSkin
            ? new Color(0.66f, 0.66f, 0.66f)
            : new Color(0.35f, 0.35f, 0.35f);
    }
}
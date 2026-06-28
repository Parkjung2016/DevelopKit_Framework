using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem.Panels
{
    internal sealed class InventoryContainersPanel : InventoryEditorPanelBase
    {
        private VisualElement listHost;
        private VisualElement detailHost;
        private ScrollView detailScroll;
        private int selectedIndex = -1;

        public InventoryContainersPanel(InventoryEditorContext context) : base(context) { }

        public override string Title => "Containers";

        public override void Refresh()
        {
            Root.Clear();

            if (Context.Setup == null)
            {
                Root.Add(CreateMissingSetupMessage("Containers 탭에서 InventoryConfigSO를 생성/편집/삭제할 수 있습니다."));
                return;
            }

            var split = InventoryEditorUIFactory.CreateSplitView(260);
            Root.Add(split);
            (listHost, detailHost) = InventoryEditorUIFactory.GetSplit(split);

            listHost.Add(BuildListToolbar());
            listHost.Add(new ScrollView { name = "container-list-scroll", style = { flexGrow = 1 } });

            RebuildList();
            RebuildDetail();
        }

        private VisualElement BuildListToolbar() =>
            InventoryCollectionToolbar.Build(new InventoryCollectionToolbar.Options
            {
                NewLabel = "+ New Config",
                AddExistingType = typeof(InventoryConfigSO),
                OnNew = CreateNewConfig,
                OnAddExisting = obj => AddConfigReference(obj as InventoryConfigSO),
                OnDuplicate = DuplicateSelectedConfig,
                OnRemoveReference = RemoveSelectedReference,
                OnDeleteAsset = DeleteSelectedAsset,
                OnMoveUp = () => MoveSelected(-1),
                OnMoveDown = () => MoveSelected(1),
                CanActOnSelection = () => IsValidSelection(),
                CanMoveUp = () => IsValidSelection() && selectedIndex > 0,
                CanMoveDown = () => IsValidSelection() && selectedIndex < GetConfigs().Length - 1
            });

        private ScrollView ListScroll => listHost?.Q<ScrollView>("container-list-scroll");

        private InventoryConfigSO[] GetConfigs() =>
            Context.ContainerConfigs ?? System.Array.Empty<InventoryConfigSO>();

        private bool IsValidSelection()
        {
            InventoryConfigSO[] configs = GetConfigs();
            return selectedIndex >= 0 && selectedIndex < configs.Length && configs[selectedIndex] != null;
        }

        private InventoryConfigSO GetSelected() => IsValidSelection() ? GetConfigs()[selectedIndex] : null;

        private void RebuildList()
        {
            ScrollView scroll = ListScroll;
            if (scroll == null)
                return;

            scroll.Clear();
            InventoryConfigSO[] configs = GetConfigs();

            for (int i = 0; i < configs.Length; i++)
            {
                InventoryConfigSO config = configs[i];
                if (config == null)
                    continue;

                int index = i;
                var row = new VisualElement();
                row.AddToClassList(InventoryEditorStyles.ListRowClass);
                if (index == selectedIndex)
                    row.AddToClassList(InventoryEditorStyles.ListRowSelectedClass);

                row.RegisterCallback<ClickEvent>(_ =>
                {
                    selectedIndex = index;
                    RebuildList();
                    RebuildDetail();
                });

                row.Add(BuildConfigListRow(config));
                scroll.Add(row);
            }
        }

        private static VisualElement BuildConfigListRow(InventoryConfigSO config)
        {
            var inner = new VisualElement();
            inner.AddToClassList(InventoryEditorStyles.ListRowInnerClass);

            var slot = InventoryEditorVisuals.CreateEmptySlot(InventoryEditorVisuals.SlotSize.Small);
            inner.Add(slot);

            var content = new VisualElement();
            content.AddToClassList(InventoryEditorStyles.ListRowContentClass);
            content.Add(InventoryEditorVisuals.CreateEllipsisLabel(config.ContainerId ?? config.name, bold: true));
            content.Add(InventoryEditorVisuals.CreateEllipsisLabel(
                $"{InventoryEnumCatalog.GetContainerKindDisplayName(config.Kind)} · slots {config.SlotCount}",
                fontSize: 11));

            var ruleLabel = InventoryEditorVisuals.CreateEllipsisLabel(
                $"슬롯: {InventoryContainerRulesUI.DescribeSlotRule(config)} · 용량: {InventoryContainerRulesUI.DescribeCapacityRule(config)}",
                fontSize: 10);
            ruleLabel.style.opacity = 0.72f;
            ruleLabel.style.marginTop = 1;
            content.Add(ruleLabel);

            inner.Add(content);
            return inner;
        }

        private void RebuildDetail()
        {
            InventoryConfigSO config = GetSelected();
            if (config == null)
            {
                detailHost.Clear();
                detailHost.Add(new HelpBox("왼쪽에서 컨테이너 Config를 선택하세요.", HelpBoxMessageType.None));
                return;
            }

            VisualElement detail = InventoryEditorUIFactory.BeginDetailPanel(detailHost);
            detailScroll = detail as ScrollView;
            detail.Add(InventoryInspectorUI.BuildHeader(config.ContainerId ?? config.name));
            detail.Add(InventoryCollectionToolbar.BuildDetailActions(config, DuplicateSelectedConfig, DeleteSelectedAsset));

            SerializedObject serializedObject = new SerializedObject(config);
            detail.Add(InventoryContainerRulesUI.Build(
                config,
                serializedObject,
                () =>
                {
                    serializedObject.ApplyModifiedProperties();
                    InventoryEditorAssetNaming.SyncContainerFileName(config);
                    InventoryEditorUIFactory.ApplyAssetChanges(config);
                    RebuildList();
                    InventoryEditorVisuals.RefreshDetailHeaderTitle(detailScroll, config.ContainerId ?? config.name);
                },
                RebuildDetail));
        }

        private void CreateNewConfig()
        {
            InventoryConfigSO config = InventoryEditorAssetActions.CreateAsset<InventoryConfigSO>(
                Context,
                asset =>
                {
                    asset.ContainerId = "new_container";
                    asset.Kind = (ContainerKind)InventoryEnumCore.MainContainerKindValue;
                    asset.SlotCount = 20;
                },
                asset => InventoryEditorAssetNaming.ForContainer(asset.ContainerId),
                Context.GetSetupAssetDirectory());

            AddConfigReference(config);
            Refresh();
            EditorGUIUtility.PingObject(config);
        }

        private void AddConfigReference(InventoryConfigSO config)
        {
            if (config == null)
                return;

            InventoryConfigSO[] configs = GetConfigs();
            for (int i = 0; i < configs.Length; i++)
            {
                if (configs[i] == config)
                    return;
            }

            Undo.RecordObject(Context.Setup, "Add Container Config");
            var list = new List<InventoryConfigSO>(configs) { config };
            Context.Setup.ContainerConfigs = list.ToArray();
            Context.MarkDirty(Context.Setup);
            selectedIndex = list.Count - 1;
            RebuildList();
            RebuildDetail();
        }

        private void DuplicateSelectedConfig()
        {
            InventoryConfigSO source = GetSelected();
            if (source == null)
                return;

            InventoryConfigSO copy = InventoryEditorAssetActions.DuplicateAsset(
                source,
                Context,
                asset => asset.ContainerId = source.ContainerId + "_copy",
                asset => InventoryEditorAssetNaming.ForContainer(asset.ContainerId),
                Context.GetSetupAssetDirectory());
            if (copy == null)
                return;

            EditorUtility.SetDirty(copy);
            AddConfigReference(copy);
            Refresh();
        }

        private void RemoveSelectedReference()
        {
            if (!IsValidSelection())
                return;

            Undo.RecordObject(Context.Setup, "Remove Container Config");
            var list = new List<InventoryConfigSO>(GetConfigs());
            list.RemoveAt(selectedIndex);
            Context.Setup.ContainerConfigs = list.ToArray();
            Context.MarkDirty(Context.Setup);
            selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, list.Count - 1);
            Refresh();
        }

        private void DeleteSelectedAsset()
        {
            InventoryConfigSO config = GetSelected();
            if (config == null || !InventoryEditorAssetActions.ConfirmDeleteAsset(config))
                return;

            InventoryEditorAssetActions.RemoveReferencesTo(config, Context);
            RemoveSelectedReference();
            InventoryEditorAssetActions.DeleteAssetFile(config);
            Refresh();
        }

        private void MoveSelected(int delta)
        {
            if (!IsValidSelection())
                return;

            Undo.RecordObject(Context.Setup, "Reorder Container Configs");
            if (!InventoryEditorAssetActions.MoveArrayElement(
                GetConfigs(),
                selectedIndex,
                delta,
                items =>
                {
                    Context.Setup.ContainerConfigs = items;
                    Context.MarkDirty(Context.Setup);
                }))
                return;

            selectedIndex += delta;
            Refresh();
        }
    }
}

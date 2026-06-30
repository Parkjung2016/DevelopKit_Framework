using PJDev.DevelopKit.Framework.Editors.InventorySystem;
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.EquipmentSystem
{
    [CustomEditor(typeof(EquipmentSetupSO))]
    public sealed class EquipmentSetupSOEditor : Editor
    {
        private VisualElement root;
        private VisualElement validationHost;
        private VisualElement containerHost;
        private VisualElement slotLayoutHost;
        private VisualElement tagGuideHost;
        private VisualElement integrationHost;
        private EquipmentContainerSectionBinding containerSection;
        private EquipmentSlotLayoutView slotLayoutView;
        private ItemDatabaseSO linkedItemDatabase;
        private bool isRefreshing;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            InventoryEditorStyleSheet.Apply(root);
            linkedItemDatabase = EquipmentEditorUI.LoadLinkedItemDatabase((EquipmentSetupSO)target);

            root.Add(InventoryInspectorUI.BuildHeader("Equipment Setup"));
            root.Add(EquipmentEditorUI.BuildIntroHelpBox());

            var presetSection = InventoryEditorUIFactory.CreateSection("Quick Start");
            presetSection.Add(EquipmentEditorUI.BuildPresetToolbar((EquipmentSetupSO)target, OnPresetApplied));
            root.Add(presetSection);

            validationHost = new VisualElement();
            root.Add(validationHost);

            containerHost = new VisualElement();
            root.Add(containerHost);
            RefreshContainerSection();

            slotLayoutHost = new VisualElement();
            root.Add(slotLayoutHost);
            slotLayoutView = new EquipmentSlotLayoutView(OnSlotCategoryChanged, SyncSlotArray);
            slotLayoutView.Mount(slotLayoutHost);

            var tagSection = InventoryEditorUIFactory.CreateSection("Tag Prefix");
            InventoryEditorUIFactory.BindPropertyFields(
                tagSection,
                serializedObject,
                OnTagPrefixChanged,
                "EquipmentTagPrefix");
            tagGuideHost = new VisualElement();
            tagSection.Add(tagGuideHost);
            root.Add(tagSection);

            root.Add(EquipmentEditorUI.BuildProfileOverridesSection(
                (EquipmentSetupSO)target,
                serializedObject,
                linkedItemDatabase,
                OnItemDatabaseChanged,
                MarkOverridesDirty));

            integrationHost = new VisualElement();
            root.Add(integrationHost);

            RegisterTracks();
            RefreshDynamicContent(rebuildContainer: false);
            return root;
        }

        private void RegisterTracks()
        {
            SerializedProperty slotCountProp = InventoryEditorUIFactory.FindSerializedProperty(serializedObject, "SlotCount");
            if (slotCountProp != null)
            {
                root.TrackPropertyValue(slotCountProp, _ =>
                {
                    if (isRefreshing)
                        return;

                    OnSlotCountChanged();
                });
            }
        }

        private void OnPresetApplied()
        {
            serializedObject.Update();
            RefreshDynamicContent(rebuildContainer: true);
        }

        private void OnSlotCountChanged()
        {
            if (isRefreshing)
                return;

            SyncNormalize();
            var setup = (EquipmentSetupSO)target;
            containerSection?.UpdateSlotCountDisplay(setup.SlotCount);
            slotLayoutView.Sync(setup);
            RefreshValidation();
            RefreshIntegration();
        }

        private void OnContainerMetaChanged()
        {
            if (isRefreshing)
                return;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            RefreshIntegration();
        }

        private void OnItemTypeChanged()
        {
            if (isRefreshing)
                return;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void OnTagPrefixChanged()
        {
            if (isRefreshing)
                return;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            RefreshTagGuide();
            slotLayoutView.UpdateTagHints((EquipmentSetupSO)target);
        }

        private void OnSlotCategoryChanged(int slotIndex, string category)
        {
            if (isRefreshing)
                return;

            var setup = (EquipmentSetupSO)target;
            serializedObject.Update();
            setup.Normalize();

            if (setup.SlotCategories == null || setup.SlotCategories.Length <= slotIndex)
                setup.Normalize();

            string existing = setup.SlotCategories[slotIndex] ?? string.Empty;
            if (existing == category)
                return;

            Undo.RecordObject(setup, "Change Equipment Slot Category");
            setup.SlotCategories[slotIndex] = category;
            setup.Normalize();
            serializedObject.Update();
            EditorUtility.SetDirty(setup);
            RefreshValidation();
        }

        private void SyncSlotArray()
        {
            if (isRefreshing)
                return;

            var setup = (EquipmentSetupSO)target;
            Undo.RecordObject(setup, "Sync Equipment Slot Categories");
            setup.Normalize();
            serializedObject.Update();
            EditorUtility.SetDirty(setup);
            containerSection?.UpdateSlotCountDisplay(setup.SlotCount);
            slotLayoutView.Sync(setup);
            RefreshValidation();
        }

        private void OnItemDatabaseChanged(ItemDatabaseSO database)
        {
            linkedItemDatabase = database;
            EquipmentEditorUI.SaveLinkedItemDatabase((EquipmentSetupSO)target, database);
        }

        private void MarkOverridesDirty()
        {
            if (isRefreshing)
                return;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void SyncNormalize()
        {
            serializedObject.Update();
            ((EquipmentSetupSO)target).Normalize();
            serializedObject.Update();
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void RefreshDynamicContent(bool rebuildContainer)
        {
            isRefreshing = true;
            try
            {
                serializedObject.Update();
                ((EquipmentSetupSO)target).Normalize();
                serializedObject.Update();

                var setup = (EquipmentSetupSO)target;

                if (rebuildContainer)
                    RefreshContainerSection();
                else
                    containerSection?.UpdateSlotCountDisplay(setup.SlotCount);

                slotLayoutView.Sync(setup);
                RefreshValidation();
                RefreshTagGuide();
                RefreshIntegration();
            }
            finally
            {
                isRefreshing = false;
            }
        }

        private void RefreshContainerSection()
        {
            containerHost.Clear();
            containerSection = EquipmentContainerSectionBinding.Build(
                (EquipmentSetupSO)target,
                serializedObject,
                OnSlotCountChanged,
                OnContainerMetaChanged,
                OnItemTypeChanged);
            containerHost.Add(containerSection.Root);
        }

        private void RefreshValidation()
        {
            validationHost.Clear();
            validationHost.Add(EquipmentEditorUI.BuildValidationSection((EquipmentSetupSO)target));
        }

        private void RefreshTagGuide()
        {
            tagGuideHost.Clear();
            tagGuideHost.Add(EquipmentEditorUI.BuildTagGuideSection((EquipmentSetupSO)target));
        }

        private void RefreshIntegration()
        {
            integrationHost.Clear();
            integrationHost.Add(EquipmentEditorUI.BuildIntegrationSection((EquipmentSetupSO)target));
        }
    }
}

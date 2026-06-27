using System;
using System.Collections.Generic;
using System.IO;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>JSON 소스 파일 선택·삭제 UI 패널입니다.</summary>
    internal sealed class GameplayTagSourceFilePanel : VisualElement
    {
        public event Action<string> SourceFilterChanged;
        public event Action SourceFilesChanged;

        private readonly PopupField<string> sourceFilterField;
        private readonly Button deleteButton;
        private readonly Label emptyHintLabel;
        private readonly List<string> filterOptions = new();

        private readonly bool showDeleteButton;

        public GameplayTagSourceFilePanel(bool showDeleteButton = true)
        {
            this.showDeleteButton = showDeleteButton;
            AddToClassList(GameplayTagEditorStyles.SourcePanelClass);
            style.flexDirection = FlexDirection.Column;

            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexWrap = Wrap.Wrap,
                    minWidth = 0
                }
            };
            Add(row);

            RebuildFilterOptions();
            sourceFilterField = new PopupField<string>(
                GameplayTagEditorLocalization.SourceFile,
                filterOptions,
                0);
            sourceFilterField.style.flexGrow = 1;
            sourceFilterField.style.flexShrink = 1;
            sourceFilterField.style.minWidth = 60;
            sourceFilterField.RegisterValueChangedCallback(_ =>
            {
                if (HasSourceFiles())
                    SourceFilterChanged?.Invoke(GetSelectedFilter());
            });
            row.Add(sourceFilterField);

            deleteButton = new(TryDeleteSelectedSourceFile)
            {
                text = GameplayTagEditorLocalization.DeleteSourceFile
            };
            deleteButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
            deleteButton.style.marginLeft = 6;
            deleteButton.style.flexShrink = 0;
            deleteButton.style.display = showDeleteButton ? DisplayStyle.Flex : DisplayStyle.None;
            row.Add(deleteButton);

            emptyHintLabel = new Label();
            emptyHintLabel.AddToClassList(GameplayTagEditorStyles.SourceEmptyHintClass);
            emptyHintLabel.style.display = DisplayStyle.None;
            Add(emptyHintLabel);

            UpdateEmptyState(notify: false);
        }

        /// <summary>디스크의 소스 파일 목록을 다시 읽어 드롭다운을 갱신합니다.</summary>
        public void Refresh()
        {
            if (sourceFilterField == null)
                return;

            string previous = HasSourceFiles() ? sourceFilterField.value : null;
            RebuildFilterOptions();

            if (filterOptions.Count == 0)
                return;

            sourceFilterField.choices = filterOptions;

            if (HasSourceFiles())
            {
                int index = filterOptions.FindIndex(o => o == previous);
                sourceFilterField.SetValueWithoutNotify(filterOptions[index >= 0 ? index : 0]);
            }
            else
            {
                sourceFilterField.SetValueWithoutNotify(filterOptions[0]);
            }

            UpdateEmptyState(notify: true);
        }

        /// <summary>현재 선택된 소스 파일 이름을 반환합니다. 파일이 없으면 null입니다.</summary>
        public string GetSelectedFilter()
        {
            if (!HasSourceFiles())
                return null;

            return sourceFilterField.value;
        }

        private bool HasSourceFiles()
        {
            return GameplayTagEditorUtility.HasSourceFiles();
        }

        private void RebuildFilterOptions()
        {
            filterOptions.Clear();

            foreach (FileGameplayTagSource source in FileGameplayTagSource.GetAllFileSources())
                filterOptions.Add(source.Name);

            if (filterOptions.Count == 0)
                filterOptions.Add(GameplayTagEditorLocalization.NoSourceFiles);
        }

        private void UpdateEmptyState(bool notify)
        {
            bool hasFiles = HasSourceFiles();
            emptyHintLabel.text = GameplayTagEditorLocalization.NoSourceFilesPrompt;
            emptyHintLabel.style.display = hasFiles ? DisplayStyle.None : DisplayStyle.Flex;
            sourceFilterField.SetEnabled(hasFiles);
            if (showDeleteButton)
                deleteButton.SetEnabled(hasFiles);

            if (notify)
                SourceFilterChanged?.Invoke(GetSelectedFilter());
        }

        private void TryDeleteSelectedSourceFile()
        {
            if (!HasSourceFiles())
            {
                EditorUtility.DisplayDialog(
                    GameplayTagEditorLocalization.DeleteSourceFileTitle,
                    GameplayTagEditorLocalization.NoSourceFilesPrompt,
                    GameplayTagEditorLocalization.Ok);
                return;
            }

            string selected = GetSelectedFilter();
            if (string.IsNullOrEmpty(selected))
            {
                EditorUtility.DisplayDialog(
                    GameplayTagEditorLocalization.DeleteSourceFileTitle,
                    GameplayTagEditorLocalization.SelectSourceFileToDelete,
                    GameplayTagEditorLocalization.Ok);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    GameplayTagEditorLocalization.DeleteSourceFileTitle,
                    string.Format(GameplayTagEditorLocalization.DeleteSourceFileMessage, selected),
                    GameplayTagEditorLocalization.Delete,
                    GameplayTagEditorLocalization.Cancel))
            {
                return;
            }

            string filePath = Path.Combine(FileGameplayTagSource.DirectoryPath, selected);
            filePath = Path.GetFullPath(filePath);

            if (!File.Exists(filePath))
            {
                EditorUtility.DisplayDialog(
                    GameplayTagEditorLocalization.DeleteSourceFileTitle,
                    GameplayTagEditorLocalization.SourceFileNotFound,
                    GameplayTagEditorLocalization.Ok);
                Refresh();
                return;
            }

            try
            {
                File.Delete(filePath);
                GameplayTagManager.ReloadTags();
                Refresh();
                SourceFilesChanged?.Invoke();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    GameplayTagEditorLocalization.DeleteSourceFileTitle,
                    ex.Message,
                    GameplayTagEditorLocalization.Ok);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>새 태그 생성 폼 UI 패널입니다.</summary>
    internal sealed class GameplayTagAddTagPanel : VisualElement
    {
        public event Action<GameplayTag> TagCreated;
        public event Action Cancelled;

        private const int NewFileOptionIndex = 0;

        private readonly PopupField<string> parentField;
        private readonly TextField segmentField;
        private readonly Label fullNamePreview;
        private readonly TextField commentField;
        private readonly PopupField<string> sourceFileField;
        private readonly TextField newSourceFileField;
        private readonly Label validationLabel;
        private readonly List<string> sourceOptions = new();
        private readonly VisualElement newSourceFileRow;

        public GameplayTagAddTagPanel(string defaultParentTagName = null, string defaultSourceFileName = null)
        {
            AddToClassList(GameplayTagEditorStyles.AddFormPanelClass);
            style.flexDirection = FlexDirection.Column;
            style.minHeight = 0;
            style.minWidth = 0;

            Label title = new(GameplayTagEditorLocalization.AddNewTag);
            title.AddToClassList(GameplayTagEditorStyles.PanelTitleClass);
            title.style.flexShrink = 0;
            Add(title);

            ScrollView formScroll = new(ScrollViewMode.Vertical);
            formScroll.style.flexGrow = 1;
            formScroll.style.flexShrink = 1;
            formScroll.style.minHeight = 0;
            formScroll.style.minWidth = 0;
            Add(formScroll);

            VisualElement formBody = new();
            formBody.style.minWidth = 0;
            formScroll.Add(formBody);

            RebuildSourceOptions();
            int sourceIndex = GetDefaultSourceIndex();
            if (!string.IsNullOrEmpty(defaultSourceFileName))
            {
                int found = sourceOptions.FindIndex(s =>
                    string.Equals(s, defaultSourceFileName, StringComparison.OrdinalIgnoreCase));
                if (found >= 0)
                    sourceIndex = found;
            }

            sourceFileField = new PopupField<string>(
                GameplayTagEditorLocalization.SourceFile,
                sourceOptions,
                sourceIndex);
            sourceFileField.AddToClassList(GameplayTagEditorStyles.FormRowClass);
            sourceFileField.RegisterValueChangedCallback(_ =>
            {
                UpdateNewSourceFileVisibility();
                RefreshParentOptions(defaultParentTagName);
            });
            formBody.Add(sourceFileField);

            newSourceFileRow = new VisualElement();
            newSourceFileField = new TextField(GameplayTagEditorLocalization.FileName) { value = "DefaultGameplayTags" };
            newSourceFileField.AddToClassList(GameplayTagEditorStyles.FormRowClass);
            newSourceFileRow.Add(newSourceFileField);
            formBody.Add(newSourceFileRow);

            string initialSource = GetSelectedSourceFileName();
            List<string> parentOptions = GameplayTagNameComposer.BuildParentOptionsForSource(initialSource);
            int parentIndex = ResolveParentIndex(parentOptions, defaultParentTagName);
            parentField = new PopupField<string>(GameplayTagEditorLocalization.Parent, parentOptions, parentIndex);
            parentField.AddToClassList(GameplayTagEditorStyles.FormRowClass);
            parentField.RegisterValueChangedCallback(_ => UpdatePreview());
            formBody.Add(parentField);

            segmentField = new TextField(GameplayTagEditorLocalization.Name);
            segmentField.tooltip = GameplayTagEditorLocalization.NameTooltip;
            segmentField.AddToClassList(GameplayTagEditorStyles.FormRowClass);
            segmentField.RegisterValueChangedCallback(_ => UpdatePreview());
            formBody.Add(segmentField);

            fullNamePreview = new Label($"{GameplayTagEditorLocalization.FullName}: —");
            fullNamePreview.AddToClassList(GameplayTagEditorStyles.DetailLabelClass);
            fullNamePreview.style.marginBottom = 6;
            fullNamePreview.style.whiteSpace = WhiteSpace.Normal;
            formBody.Add(fullNamePreview);

            commentField = new TextField(GameplayTagEditorLocalization.Comment);
            commentField.AddToClassList(GameplayTagEditorStyles.FormRowClass);
            formBody.Add(commentField);

            validationLabel = new Label();
            validationLabel.AddToClassList(GameplayTagEditorStyles.ValidationErrorClass);
            validationLabel.style.display = DisplayStyle.None;
            formBody.Add(validationLabel);

            VisualElement actions = new();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexShrink = 0;
            actions.style.marginTop = 4;
            Button createButton = new(TryCreate) { text = GameplayTagEditorLocalization.Create };
            createButton.AddToClassList(GameplayTagEditorStyles.PrimaryButtonClass);
            actions.Add(createButton);

            Button cancelButton = new(() => Cancelled?.Invoke()) { text = GameplayTagEditorLocalization.Cancel };
            cancelButton.AddToClassList(GameplayTagEditorStyles.ActionButtonClass);
            cancelButton.style.marginLeft = 6;
            actions.Add(cancelButton);
            Add(actions);

            UpdateNewSourceFileVisibility();
            UpdatePreview();
        }

        /// <summary>부모 태그 드롭다운 기본값을 갱신합니다.</summary>
        public void SetDefaultParent(string parentTagName)
        {
            RefreshParentOptions(parentTagName);
        }

        /// <summary>태그 이름 입력 필드에 포커스를 둡니다.</summary>
        public void FocusNameField()
        {
            segmentField.Focus();
        }

        private void RefreshParentOptions(string preferredParentTagName)
        {
            string sourceFileName = GetSelectedSourceFileName();
            List<string> parentOptions = GameplayTagNameComposer.BuildParentOptionsForSource(sourceFileName);
            int parentIndex = ResolveParentIndex(parentOptions, preferredParentTagName);

            string previous = parentField.value;
            parentField.choices = parentOptions;
            parentField.index = parentIndex;

            if (parentOptions.Contains(previous))
                parentField.SetValueWithoutNotify(previous);

            UpdatePreview();
        }

        private static int ResolveParentIndex(List<string> parentOptions, string preferredParentTagName)
        {
            if (!string.IsNullOrEmpty(preferredParentTagName))
            {
                int found = parentOptions.FindIndex(p =>
                    string.Equals(p, preferredParentTagName, StringComparison.OrdinalIgnoreCase));
                if (found >= 0)
                    return found;
            }

            return 0;
        }

        private void RebuildSourceOptions()
        {
            sourceOptions.Clear();
            sourceOptions.Add(GameplayTagEditorLocalization.NewFile);
            foreach (FileGameplayTagSource source in FileGameplayTagSource.GetAllFileSources())
                sourceOptions.Add(source.Name);
        }

        private int GetDefaultSourceIndex()
        {
            return sourceOptions.Count > 1 ? 1 : NewFileOptionIndex;
        }

        private string GetSelectedSourceFileName()
        {
            if (sourceFileField.index == NewFileOptionIndex)
                return null;

            return sourceFileField.value;
        }

        private void UpdateNewSourceFileVisibility()
        {
            bool isNewFile = sourceFileField.index == NewFileOptionIndex;
            newSourceFileRow.style.display = isNewFile ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdatePreview()
        {
            if (!GameplayTagNameComposer.TryComposeValidName(
                    parentField.value,
                    segmentField.value,
                    out string fullName,
                    out _))
            {
                string partial = GameplayTagNameComposer.Compose(parentField.value, segmentField.value);
                fullNamePreview.text = string.IsNullOrEmpty(partial)
                    ? $"{GameplayTagEditorLocalization.FullName}: —"
                    : $"{GameplayTagEditorLocalization.FullName}: {partial}";
                return;
            }

            if (GameplayTagNameComposer.WillCreateMissingParents(fullName))
            {
                List<string> missing = GameplayTagNameComposer.GetMissingParentsInSourceFile(
                    fullName,
                    GetSelectedSourceFileName() ?? GameplayTagNameComposer.ToSourceFileName(newSourceFileField.value));

                if (missing.Count > 0)
                {
                    fullNamePreview.text =
                        $"{GameplayTagEditorLocalization.FullName}: {fullName}\n{GameplayTagEditorLocalization.AutoCreate}: {string.Join(", ", missing)}";
                    return;
                }
            }

            fullNamePreview.text = $"{GameplayTagEditorLocalization.FullName}: {fullName}";
        }

        private void TryCreate()
        {
            validationLabel.style.display = DisplayStyle.None;

            if (!GameplayTagNameComposer.TryComposeValidName(
                    parentField.value,
                    segmentField.value,
                    out string tagName,
                    out string error))
            {
                ShowError(error);
                return;
            }

            if (!ValidateSourceFile(out error))
            {
                ShowError(error);
                return;
            }

            try
            {
                if (!GetOrCreateSource(out FileGameplayTagSource source, out string sourceError))
                {
                    ShowError(sourceError);
                    return;
                }

                string targetSourceFileName = source.Name;
                List<string> crossFileTags =
                    GameplayTagCrossFileUtility.CollectCrossFileTagNames(targetSourceFileName, tagName);

                if (!GameplayTagCrossFileUtility.TryResolveTagsInOtherFilesForMove(
                        source,
                        crossFileTags,
                        out string moveError))
                {
                    if (!string.IsNullOrEmpty(moveError))
                        ShowError(moveError);

                    return;
                }

                List<string> missingParents =
                    GameplayTagNameComposer.GetMissingParentsInSourceFile(tagName, targetSourceFileName);

                if (missingParents.Count > 0)
                {
                    string message = string.Format(
                        GameplayTagEditorLocalization.CreateMissingParentsMessage,
                        targetSourceFileName,
                        string.Join("\n", missingParents));

                    if (!EditorUtility.DisplayDialog(
                            GameplayTagEditorLocalization.CreateMissingParentsTitle,
                            message,
                            GameplayTagEditorLocalization.Ok,
                            GameplayTagEditorLocalization.Cancel))
                    {
                        return;
                    }
                }

                if (!source.ContainsTag(tagName))
                {
                    if (!source.TryAddTag(tagName, commentField.value?.Trim(), out string addError))
                    {
                        ShowError(GameplayTagEditorUtility.LocalizeRuntimeMessage(addError));
                        return;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(commentField.value))
                {
                    if (!source.TryUpdateComment(tagName, commentField.value.Trim(), out string commentError))
                    {
                        ShowError(GameplayTagEditorUtility.LocalizeRuntimeMessage(commentError));
                        return;
                    }
                }

                GameplayTagManager.ReloadTags();

                GameplayTag added = GameplayTagManager.RequestTag(tagName);
                if (!added.IsValid)
                {
                    ShowError(GameplayTagEditorLocalization.TagCreateReloadFailed);
                    return;
                }

                TagCreated?.Invoke(added);
            }
            catch (Exception ex)
            {
                ShowError(GameplayTagEditorUtility.LocalizeRuntimeMessage(ex.Message));
            }
        }

        private void ShowError(string message)
        {
            validationLabel.text = message;
            validationLabel.style.display = DisplayStyle.Flex;
        }

        private bool ValidateSourceFile(out string error)
        {
            error = null;

            if (sourceFileField.index != NewFileOptionIndex)
                return true;

            string baseName = GameplayTagNameComposer.NormalizeSourceFileBaseName(newSourceFileField.value);
            if (string.IsNullOrEmpty(baseName))
            {
                error = GameplayTagEditorLocalization.SourceFileNameRequired;
                return false;
            }

            string fileName = GameplayTagNameComposer.ToSourceFileName(baseName);
            string filePath = Path.Combine(FileGameplayTagSource.DirectoryPath, fileName);
            filePath = Path.GetFullPath(filePath);

            if (File.Exists(filePath))
            {
                error = string.Format(GameplayTagEditorLocalization.SourceFileAlreadyExists, fileName);
                return false;
            }

            Directory.CreateDirectory(FileGameplayTagSource.DirectoryPath);
            return true;
        }

        private bool GetOrCreateSource(out FileGameplayTagSource source, out string error)
        {
            source = null;
            error = null;

            if (sourceFileField.index != NewFileOptionIndex)
            {
                source = FileGameplayTagSource.GetAllFileSources()
                    .FirstOrDefault(s => s.Name == sourceFileField.value);

                if (source == null)
                {
                    error = GameplayTagEditorLocalization.SourceFileNotFoundInList;
                    return false;
                }

                return true;
            }

            string fileName = GameplayTagNameComposer.ToSourceFileName(newSourceFileField.value);
            string filePath = Path.Combine(FileGameplayTagSource.DirectoryPath, fileName);
            filePath = Path.GetFullPath(filePath);

            Directory.CreateDirectory(FileGameplayTagSource.DirectoryPath);

            source = new FileGameplayTagSource(filePath);
            if (!source.TryLoad())
            {
                error = GameplayTagEditorLocalization.SourceFileCreateFailed;
                source = null;
                return false;
            }

            return true;
        }
    }
}

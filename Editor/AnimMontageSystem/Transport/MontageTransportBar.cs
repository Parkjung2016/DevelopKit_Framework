using System;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageTransportBar : VisualElement
    {
        private static readonly GUIContent PlayContent = ResolveIcon("PlayButton", "Animation.Play", "Play");
        private static readonly GUIContent PauseContent = ResolveIcon("PauseButton", "Animation.Pause", "Pause");

        private readonly MontageEditorContext context;
        private readonly ToolbarButton playPauseButton;
        private readonly Image playPauseIcon;
        private readonly ToolbarButton stopButton;
        private readonly Toggle loopToggle;
        private readonly FloatField speedField;
        private readonly FloatField rateScaleField;
        private readonly Toggle horizontalRootMotionToggle;
        private readonly Toggle verticalRootMotionToggle;
        private readonly Toggle rotationRootMotionToggle;
        private readonly VisualElement segmentDivider;
        private readonly VisualElement segmentGroup;
        private readonly ToolbarButton splitSegmentButton;
        private readonly ToolbarButton replaceSegmentButton;
        private readonly ToolbarButton resetSegmentTrimButton;
        private readonly Label timeLabel;
        private readonly Func<bool> hasSelectedSegment;
        private readonly Func<bool> canSplitSelectedSegment;

        public MontageTransportBar(
            MontageEditorContext context,
            Action onPlayPause,
            Action onStop,
            Action onSplitSegment,
            Action onReplaceSegmentClip,
            Action onResetSegmentTrim,
            Func<bool> hasSelectedSegment,
            Func<bool> canSplitSelectedSegment)
        {
            this.context = context;
            this.hasSelectedSegment = hasSelectedSegment;
            this.canSplitSelectedSegment = canSplitSelectedSegment;
            AddToClassList(AnimMontageEditorStyles.TransportBarClass);
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.flexShrink = 0;

            var title = new Label("Timeline");
            title.AddToClassList(AnimMontageEditorStyles.PanelHeaderTitleClass);
            title.style.flexShrink = 0;
            title.style.marginRight = 8;
            Add(title);

            Add(CreateDivider());

            var playbackGroup = CreateGroup();
            playPauseButton = CreateIconButton(out playPauseIcon, onPlayPause);
            playPauseButton.tooltip = "Play / Pause (Space)";
            playbackGroup.Add(playPauseButton);

            stopButton = CreateIconButton(
                out _,
                onStop,
                ResolveIcon("Animation.PrevKey", "Animation.FirstKey", "Stop"));
            stopButton.tooltip = "Stop and reset to start";
            playbackGroup.Add(stopButton);
            Add(playbackGroup);

            Add(CreateDivider());

            var optionsGroup = CreateGroup();
            loopToggle = new Toggle("Loop") { value = context.Loop };
            loopToggle.AddToClassList(AnimMontageEditorStyles.TransportToggleClass);
            loopToggle.tooltip = "Loop playback";
            loopToggle.RegisterValueChangedCallback(evt => context.Loop = evt.newValue);
            optionsGroup.Add(loopToggle);

            speedField = new FloatField { value = context.PlaybackSpeed, label = "Preview Speed" };
            speedField.AddToClassList(AnimMontageEditorStyles.TransportSpeedFieldClass);
            speedField.tooltip = GetSpeedTooltip();
            speedField.RegisterValueChangedCallback(evt =>
            {
                if (EditorApplication.isPlaying)
                    return;

                context.PlaybackSpeed = Mathf.Max(0.01f, evt.newValue);
                RefreshPlaybackState();
            });
            optionsGroup.Add(speedField);

            rateScaleField = new FloatField { label = "Rate" };
            rateScaleField.AddToClassList(AnimMontageEditorStyles.TransportSpeedFieldClass);
            rateScaleField.tooltip = "Montage RateScale. Combined playback is Speed x Rate.";
            rateScaleField.RegisterValueChangedCallback(evt =>
            {
                if (EditorApplication.isPlaying)
                    return;

                SetMontageFloat("rateScale", Mathf.Max(0.01f, evt.newValue), "Set Montage RateScale");
            });
            optionsGroup.Add(rateScaleField);

            horizontalRootMotionToggle = CreateRootMotionToggle(
                "H",
                "Horizontal Root Motion (X/Z movement)",
                "applyHorizontalRootMotion",
                "Set Horizontal Root Motion");
            optionsGroup.Add(horizontalRootMotionToggle);

            verticalRootMotionToggle = CreateRootMotionToggle(
                "V",
                "Vertical Root Motion (Y movement)",
                "applyVerticalRootMotion",
                "Set Vertical Root Motion");
            optionsGroup.Add(verticalRootMotionToggle);

            rotationRootMotionToggle = CreateRootMotionToggle(
                "R",
                "Rotation Root Motion",
                "applyRotationRootMotion",
                "Set Rotation Root Motion");
            optionsGroup.Add(rotationRootMotionToggle);
            Add(optionsGroup);

            segmentDivider = CreateDivider();
            Add(segmentDivider);

            segmentGroup = CreateGroup();
            splitSegmentButton = CreateTextButton("Split", onSplitSegment, "Split selected Animation Segment at playhead. Disabled for loop clips.");
            segmentGroup.Add(splitSegmentButton);

            replaceSegmentButton = CreateTextButton("Clip", onReplaceSegmentClip, "Replace selected Animation Segment clip.");
            segmentGroup.Add(replaceSegmentButton);

            resetSegmentTrimButton = CreateTextButton("Reset", onResetSegmentTrim, "Reset selected Animation Segment trim.");
            segmentGroup.Add(resetSegmentTrimButton);
            Add(segmentGroup);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            timeLabel = new Label("0.00 / 0.00");
            timeLabel.AddToClassList(AnimMontageEditorStyles.TransportTimeClass);
            Add(timeLabel);

            context.PlaybackStateChanged += RefreshPlaybackState;
            context.PlayheadChanged += RefreshTime;
            context.MontageChanged += Refresh;
            context.SelectionChanged += RefreshSegmentActions;
            context.Changed += RefreshPlaybackState;
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                context.PlaybackStateChanged -= RefreshPlaybackState;
                context.PlayheadChanged -= RefreshTime;
                context.MontageChanged -= Refresh;
                context.SelectionChanged -= RefreshSegmentActions;
                context.Changed -= RefreshPlaybackState;
            });

            Refresh();
        }

        public void Refresh()
        {
            RefreshPlaybackState();
            RefreshSegmentActions();
            RefreshTime();
        }

        private void RefreshPlaybackState()
        {
            AnimMontageSO montage = context.Montage;
            ApplyIcon(playPauseButton, playPauseIcon, context.IsPlaying ? PauseContent : PlayContent);
            bool locked = EditorApplication.isPlaying;
            playPauseButton.SetEnabled(montage != null && !locked);
            stopButton.SetEnabled(montage != null && !locked && context.IsPlaying);
            loopToggle.SetValueWithoutNotify(context.Loop);
            speedField.SetValueWithoutNotify(context.PlaybackSpeed);
            speedField.tooltip = GetSpeedTooltip();
            loopToggle.SetEnabled(!locked);
            speedField.SetEnabled(!locked);
            rateScaleField.SetEnabled(montage != null && !locked);
            horizontalRootMotionToggle.SetEnabled(montage != null && !locked);
            verticalRootMotionToggle.SetEnabled(montage != null && !locked);
            rotationRootMotionToggle.SetEnabled(montage != null && !locked);
            rateScaleField.SetValueWithoutNotify(montage != null ? montage.RateScale : 1f);
            horizontalRootMotionToggle.SetValueWithoutNotify(montage != null && montage.ApplyHorizontalRootMotion);
            verticalRootMotionToggle.SetValueWithoutNotify(montage != null && montage.ApplyVerticalRootMotion);
            rotationRootMotionToggle.SetValueWithoutNotify(montage != null && montage.ApplyRotationRootMotion);
        }

        private void RefreshSegmentActions()
        {
            bool hasSegment = hasSelectedSegment?.Invoke() == true;
            DisplayStyle display = hasSegment ? DisplayStyle.Flex : DisplayStyle.None;
            segmentDivider.style.display = display;
            segmentGroup.style.display = display;
            bool locked = EditorApplication.isPlaying;
            splitSegmentButton.SetEnabled(!locked && hasSegment && canSplitSelectedSegment?.Invoke() == true);
            replaceSegmentButton.SetEnabled(!locked && hasSegment);
            resetSegmentTrimButton.SetEnabled(!locked && hasSegment);
        }

        private void RefreshTime()
        {
            float length = context.Montage != null ? context.Montage.Length : 0f;
            timeLabel.text = $"{context.PlayheadTime:0.00} / {length:0.00}";
            RefreshSegmentActions();
        }

        private string GetSpeedTooltip()
        {
            float rateScale = context.Montage != null ? context.Montage.RateScale : 1f;
            return $"Timeline Speed x Montage RateScale = {context.PlaybackSpeed:0.###} x {rateScale:0.###} = {context.EffectivePlaybackSpeed:0.###}";
        }

        private Toggle CreateRootMotionToggle(string label, string tooltip, string propertyName, string undoName)
        {
            var toggle = new Toggle(label);
            toggle.AddToClassList(AnimMontageEditorStyles.TransportToggleClass);
            toggle.tooltip = tooltip;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (EditorApplication.isPlaying)
                    return;

                SetMontageBool(propertyName, evt.newValue, undoName);
            });
            return toggle;
        }
        private void SetMontageFloat(string propertyName, float value, string undoName)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, undoName);
            SerializedObject so = new(montage);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
                return;

            if (Mathf.Approximately(property.floatValue, value))
                return;

            property.floatValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(montage);
            context.NotifyExternalChange();
            RefreshPlaybackState();
        }

        private void SetMontageBool(string propertyName, bool value, string undoName)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, undoName);
            SerializedObject so = new(montage);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
                return;

            if (property.boolValue == value)
                return;

            property.boolValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(montage);
            context.NotifyExternalChange();
            RefreshPlaybackState();
        }

        private static GUIContent ResolveIcon(string primary, string fallback, string text)
        {
            GUIContent content = EditorGUIUtility.IconContent(primary);
            if (content?.image != null)
                return content;

            content = EditorGUIUtility.IconContent(fallback);
            if (content?.image != null)
                return content;

            return new GUIContent(text);
        }

        private static VisualElement CreateGroup()
        {
            var group = new VisualElement();
            group.AddToClassList(AnimMontageEditorStyles.TransportGroupClass);
            return group;
        }

        private static VisualElement CreateDivider()
        {
            var divider = new VisualElement();
            divider.AddToClassList(AnimMontageEditorStyles.TransportDividerClass);
            return divider;
        }

        private static ToolbarButton CreateTextButton(string text, Action onClick, string tooltip)
        {
            var button = new ToolbarButton(onClick) { text = text, tooltip = tooltip };
            button.AddToClassList(AnimMontageEditorStyles.TransportButtonClass);
            float width = Mathf.Clamp(18f + text.Length * 7f, 42f, 62f);
            button.style.width = width;
            button.style.minWidth = width;
            button.style.maxWidth = width;
            button.style.height = 22;
            button.style.minHeight = 22;
            button.style.maxHeight = 22;
            button.style.paddingLeft = 4;
            button.style.paddingRight = 4;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.fontSize = 10;
            button.style.flexShrink = 0;
            return button;
        }
        private static ToolbarButton CreateIconButton(out Image icon, Action onClick, GUIContent content = null)
        {
            var button = new ToolbarButton(onClick);
            button.AddToClassList(AnimMontageEditorStyles.TransportButtonClass);

            icon = new Image
            {
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.alignSelf = Align.Center;
            button.Add(icon);

            ApplyIcon(button, icon, content ?? PlayContent);
            return button;
        }

        private static void ApplyIcon(ToolbarButton button, Image icon, GUIContent content)
        {
            icon.image = content?.image;
            icon.style.display = icon.image != null ? DisplayStyle.Flex : DisplayStyle.None;
            button.text = icon.image != null ? string.Empty : content?.text ?? "?";
        }
    }
}
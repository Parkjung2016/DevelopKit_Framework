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
        private readonly Toggle applyRootMotionToggle;
        private readonly Label timeLabel;

        public MontageTransportBar(MontageEditorContext context, Action onPlayPause, Action onStop)
        {
            this.context = context;
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

            speedField = new FloatField { value = context.PlaybackSpeed, label = "Speed" };
            speedField.AddToClassList(AnimMontageEditorStyles.TransportSpeedFieldClass);
            speedField.tooltip = GetSpeedTooltip();
            speedField.RegisterValueChangedCallback(evt =>
            {
                context.PlaybackSpeed = Mathf.Max(0.01f, evt.newValue);
                RefreshPlaybackState();
            });
            optionsGroup.Add(speedField);

            rateScaleField = new FloatField { label = "Rate" };
            rateScaleField.AddToClassList(AnimMontageEditorStyles.TransportSpeedFieldClass);
            rateScaleField.tooltip = "Montage RateScale. Combined playback is Speed x Rate.";
            rateScaleField.RegisterValueChangedCallback(evt =>
                SetMontageFloat("rateScale", Mathf.Max(0.01f, evt.newValue), "Set Montage RateScale"));
            optionsGroup.Add(rateScaleField);

            applyRootMotionToggle = new Toggle("Root");
            applyRootMotionToggle.AddToClassList(AnimMontageEditorStyles.TransportToggleClass);
            applyRootMotionToggle.tooltip = "Apply Root Motion for this montage";
            applyRootMotionToggle.RegisterValueChangedCallback(evt =>
                SetMontageBool("applyRootMotion", evt.newValue, "Set Montage Root Motion"));
            optionsGroup.Add(applyRootMotionToggle);
            Add(optionsGroup);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            timeLabel = new Label("0.00 / 0.00");
            timeLabel.AddToClassList(AnimMontageEditorStyles.TransportTimeClass);
            Add(timeLabel);

            context.PlaybackStateChanged += RefreshPlaybackState;
            context.PlayheadChanged += RefreshTime;
            context.MontageChanged += Refresh;
            context.Changed += RefreshPlaybackState;
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                context.PlaybackStateChanged -= RefreshPlaybackState;
                context.PlayheadChanged -= RefreshTime;
                context.MontageChanged -= Refresh;
                context.Changed -= RefreshPlaybackState;
            });

            Refresh();
        }

        public void Refresh()
        {
            RefreshPlaybackState();
            RefreshTime();
        }

        private void RefreshPlaybackState()
        {
            AnimMontageSO montage = context.Montage;
            ApplyIcon(playPauseButton, playPauseIcon, context.IsPlaying ? PauseContent : PlayContent);
            playPauseButton.SetEnabled(montage != null);
            stopButton.SetEnabled(montage != null);
            loopToggle.SetValueWithoutNotify(context.Loop);
            speedField.SetValueWithoutNotify(context.PlaybackSpeed);
            speedField.tooltip = GetSpeedTooltip();
            rateScaleField.SetEnabled(montage != null);
            applyRootMotionToggle.SetEnabled(montage != null);
            rateScaleField.SetValueWithoutNotify(montage != null ? montage.RateScale : 1f);
            applyRootMotionToggle.SetValueWithoutNotify(montage != null && montage.ApplyRootMotion);
        }

        private void RefreshTime()
        {
            float length = context.Montage != null ? context.Montage.Length : 0f;
            timeLabel.text = $"{context.PlayheadTime:0.00} / {length:0.00}";
        }

        private string GetSpeedTooltip()
        {
            float rateScale = context.Montage != null ? context.Montage.RateScale : 1f;
            return $"Timeline Speed x Montage RateScale = {context.PlaybackSpeed:0.###} x {rateScale:0.###} = {context.EffectivePlaybackSpeed:0.###}";
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
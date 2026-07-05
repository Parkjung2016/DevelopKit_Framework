using System;
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
            speedField.tooltip = "Playback speed multiplier";
            speedField.RegisterValueChangedCallback(evt =>
                context.PlaybackSpeed = Mathf.Max(0.01f, evt.newValue));
            optionsGroup.Add(speedField);
            Add(optionsGroup);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            timeLabel = new Label("0.00 / 0.00");
            timeLabel.AddToClassList(AnimMontageEditorStyles.TransportTimeClass);
            Add(timeLabel);

            context.PlaybackStateChanged += RefreshPlaybackState;
            context.PlayheadChanged += RefreshTime;
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                context.PlaybackStateChanged -= RefreshPlaybackState;
                context.PlayheadChanged -= RefreshTime;
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
            ApplyIcon(playPauseButton, playPauseIcon, context.IsPlaying ? PauseContent : PlayContent);
            playPauseButton.SetEnabled(context.Montage != null);
            stopButton.SetEnabled(context.Montage != null);
            loopToggle.SetValueWithoutNotify(context.Loop);
            speedField.SetValueWithoutNotify(context.PlaybackSpeed);
        }

        private void RefreshTime()
        {
            float length = context.Montage != null ? context.Montage.Length : 0f;
            timeLabel.text = $"{context.PlayheadTime:0.00} / {length:0.00}";
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

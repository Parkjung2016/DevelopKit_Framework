using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageViewportFlyNotification
    {
        private const float DisplayDuration = 0.5f;
        private const float FadeDuration = 0.15f;

        private static string message;
        private static double expireTime;
        private static GUIStyle labelStyle;

        public static bool IsVisible => !string.IsNullOrEmpty(message) && EditorApplication.timeSinceStartup < expireTime;

        public static void ShowSpeed(float speed, bool accelerationEnabled)
        {
            message = FormatSpeed(speed, accelerationEnabled);
            expireTime = EditorApplication.timeSinceStartup + DisplayDuration;
        }

        public static void Clear()
        {
            message = null;
            expireTime = 0d;
        }

        public static void Draw(Rect viewportRect)
        {
            if (!IsVisible)
                return;

            EnsureStyle();

            double remaining = expireTime - EditorApplication.timeSinceStartup;
            float alpha = remaining <= FadeDuration ? Mathf.Clamp01((float)(remaining / FadeDuration)) : 1f;

            GUIContent content = new(message);
            Vector2 size = labelStyle.CalcSize(content);
            size.x += 24f;
            size.y += 10f;

            Rect bgRect = new(
                viewportRect.x + (viewportRect.width - size.x) * 0.5f,
                viewportRect.y + 36f,
                size.x,
                size.y);

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            EditorGUI.DrawRect(bgRect, new Color(0.12f, 0.12f, 0.12f, 0.92f * alpha));
            GUI.Label(bgRect, content, labelStyle);
            GUI.color = previousColor;
        }

        private static void EnsureStyle()
        {
            if (labelStyle != null)
                return;

            GUIStyle baseStyle = GUI.skin.FindStyle("NotificationText")
                ?? GUI.skin.FindStyle("ProgressBarText")
                ?? EditorStyles.boldLabel;

            labelStyle = new GUIStyle(baseStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = false,
                padding = new RectOffset(12, 12, 4, 4)
            };

            labelStyle.normal.textColor = Color.white;
        }

        private static string FormatSpeed(float speed, bool accelerationEnabled)
        {
            string formatted = speed switch
            {
                < 0.0001f => speed.ToString("F6"),
                < 0.001f => speed.ToString("F5"),
                < 0.01f => speed.ToString("F4"),
                < 0.1f => speed.ToString("F3"),
                < 10f => speed.ToString("F2"),
                _ => speed.ToString("F0")
            };

            if (speed < 0.1f)
            {
                formatted = formatted.TrimEnd('0');
                if (formatted.EndsWith("."))
                    formatted = formatted.TrimEnd('.');
            }

            return accelerationEnabled ? $"{formatted}x" : formatted;
        }
    }
}
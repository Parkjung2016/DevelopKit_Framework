using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>UISystem 기본(Built-in) 레이어·캔버스 구성을 공개합니다.</summary>
    public static class UISystemBuiltIn
    {
        private static readonly BuiltInCanvasGroupInfo[] CanvasGroupsInternal =
        {
            new(UICanvasGroups.Main, "본체 UI (화면 전환·HUD)", 0, "Main UI Canvas"),
            new(UICanvasGroups.Floating, "떠 있는 UI (팝업·모달)", 100, "Floating UI Canvas"),
            new(UICanvasGroups.System, "시스템 UI (로딩·알림)", 200, "System UI Canvas")
        };

        private static readonly BuiltInLayerInfo[] LayersInternal =
        {
            new(UILayers.Screen, "화면 전환 스택 (로비, 인벤 등)", 0, UICanvasGroups.Main, true, "Screens"),
            new(UILayers.Overlay, "HUD 등 상시 표시 오버레이", 100, UICanvasGroups.Main, false, "Overlay"),
            new(UILayers.Popup, "일반 팝업", 200, UICanvasGroups.Floating, false, "Popups"),
            new(UILayers.Modal, "모달 팝업 (하위 입력 차단)", 300, UICanvasGroups.Floating, false, "Modals"),
            new(UILayers.System, "로딩, 연결 끊김, 시스템 알림", 400, UICanvasGroups.System, false, "System")
        };

        /// <summary>기본 레이어 목록입니다.</summary>
        public static IReadOnlyList<BuiltInLayerInfo> Layers => LayersInternal;

        /// <summary>기본 Canvas 묶음 목록입니다.</summary>
        public static IReadOnlyList<BuiltInCanvasGroupInfo> CanvasGroups => CanvasGroupsInternal;

        /// <summary>기본 레이어 ID 목록입니다. 프리팹 <c>layerId</c>에 사용합니다.</summary>
        public static IReadOnlyList<string> LayerIds { get; } = new[]
        {
            UILayers.Screen,
            UILayers.Overlay,
            UILayers.Popup,
            UILayers.Modal,
            UILayers.System
        };

        /// <summary>기본 Canvas 묶음 ID 목록입니다.</summary>
        public static IReadOnlyList<string> CanvasGroupIds { get; } = new[]
        {
            UICanvasGroups.Main,
            UICanvasGroups.Floating,
            UICanvasGroups.System
        };

        public static bool IsBuiltInLayerId(string layerId) => TryGetLayer(layerId, out _);

        public static bool IsBuiltInCanvasGroupId(string groupId) => TryGetCanvasGroup(groupId, out _);

        public static bool TryGetLayer(string layerId, out BuiltInLayerInfo info)
        {
            if (!string.IsNullOrEmpty(layerId))
            {
                for (int i = 0; i < LayersInternal.Length; i++)
                {
                    if (string.Equals(LayersInternal[i].LayerId, layerId, StringComparison.Ordinal))
                    {
                        info = LayersInternal[i];
                        return true;
                    }
                }
            }

            info = default;
            return false;
        }

        public static bool TryGetCanvasGroup(string groupId, out BuiltInCanvasGroupInfo info)
        {
            if (!string.IsNullOrEmpty(groupId))
            {
                for (int i = 0; i < CanvasGroupsInternal.Length; i++)
                {
                    if (string.Equals(CanvasGroupsInternal[i].GroupId, groupId, StringComparison.Ordinal))
                    {
                        info = CanvasGroupsInternal[i];
                        return true;
                    }
                }
            }

            info = default;
            return false;
        }

        public static IReadOnlyList<UILayerDefinition> CreateLayerDefinitions()
        {
            var list = new List<UILayerDefinition>(LayersInternal.Length);
            for (int i = 0; i < LayersInternal.Length; i++)
                list.Add(LayersInternal[i].ToLayerDefinition());

            return list;
        }

        public static IReadOnlyList<UICanvasGroupDefinition> CreateCanvasGroupDefinitions()
        {
            var list = new List<UICanvasGroupDefinition>(CanvasGroupsInternal.Length);
            for (int i = 0; i < CanvasGroupsInternal.Length; i++)
                list.Add(CanvasGroupsInternal[i].ToDefinition());

            return list;
        }
    }
}

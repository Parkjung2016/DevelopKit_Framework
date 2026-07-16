using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>프로젝트별 UI 레이어·Canvas 묶음 구성을 정의하는 ScriptableObject입니다.</summary>
    [CreateAssetMenu(fileName = "UILayerSettings", menuName = "PJDev/UISystem/Layer Settings")]
    public sealed class UILayerSettings : ScriptableObject
    {
        [SerializeField]
        private List<UICanvasGroupDefinition> canvasGroups = new();

        [SerializeField]
        private List<UILayerDefinition> layers = new();

        /// <summary>프로젝트 Canvas 묶음 정의입니다.</summary>
        public IReadOnlyList<UICanvasGroupDefinition> CanvasGroups => canvasGroups;

        /// <summary>현재 에셋에 설정된 레이어입니다.</summary>
        public IReadOnlyList<UILayerDefinition> Layers => layers;

        /// <summary>프레임워크 기본 레이어 정보입니다.</summary>
        public static IReadOnlyList<BuiltInLayerInfo> BuiltIn => UISystemBuiltIn.Layers;

        /// <summary>프레임워크 기본 Canvas 묶음 정보입니다.</summary>
        public static IReadOnlyList<BuiltInCanvasGroupInfo> BuiltInCanvasGroups => UISystemBuiltIn.CanvasGroups;

        /// <summary>프레임워크 기본 레이어 ID입니다.</summary>
        public static IReadOnlyList<string> BuiltInLayerIds => UISystemBuiltIn.LayerIds;

        private void OnEnable() => EnsureDefaults();

        private void OnValidate() => EnsureDefaults();

        /// <summary>기본 구성으로 초기화합니다.</summary>
        public void ResetToBuiltInDefaults()
        {
            canvasGroups = new List<UICanvasGroupDefinition>(UISystemBuiltIn.CreateCanvasGroupDefinitions());
            layers = new List<UILayerDefinition>(UISystemBuiltIn.CreateLayerDefinitions());
        }

        /// <summary>에셋 없이 사용할 기본 설정 인스턴스를 만듭니다.</summary>
        public static UILayerSettings CreateBuiltIn()
        {
            UILayerSettings settings = CreateInstance<UILayerSettings>();
            settings.ResetToBuiltInDefaults();
            return settings;
        }

        internal void EnsureDefaults()
        {
            if (canvasGroups == null || canvasGroups.Count == 0)
                canvasGroups = new List<UICanvasGroupDefinition>(UISystemBuiltIn.CreateCanvasGroupDefinitions());

            if (layers == null || layers.Count == 0)
                layers = new List<UILayerDefinition>(UISystemBuiltIn.CreateLayerDefinitions());
            else
                EnsureBuiltInLayersPresent();

            for (int i = 0; i < layers.Count; i++)
                layers[i]?.MigrateLegacyCanvasGroup();

            EnsureSingleScreenStack();
        }

        private void EnsureSingleScreenStack()
        {
            if (!ContainsLayerId(UILayers.Screen))
                return;

            for (int i = 0; i < layers.Count; i++)
            {
                UILayerDefinition layer = layers[i];
                if (layer == null)
                    continue;

                layer.SetUseScreenStack(string.Equals(layer.LayerId, UILayers.Screen, StringComparison.Ordinal));
            }
        }

        private void EnsureBuiltInLayersPresent()
        {
            IReadOnlyList<BuiltInLayerInfo> builtIn = UISystemBuiltIn.Layers;
            for (int i = 0; i < builtIn.Count; i++)
            {
                BuiltInLayerInfo info = builtIn[i];
                if (ContainsLayerId(info.LayerId))
                    continue;

                int insertIndex = layers.Count;
                for (int j = 0; j < layers.Count; j++)
                {
                    if (layers[j].SortOrder > info.SortOrder)
                    {
                        insertIndex = j;
                        break;
                    }
                }

                layers.Insert(insertIndex, info.ToLayerDefinition());
            }
        }

        private bool ContainsLayerId(string layerId)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                UILayerDefinition layer = layers[i];
                if (layer != null && string.Equals(layer.LayerId, layerId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}

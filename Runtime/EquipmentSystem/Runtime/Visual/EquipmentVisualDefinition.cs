using UnityEngine;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>장비 비주얼 1종에 대한 스폰 정보입니다.</summary>
    public struct EquipmentVisualDefinition
    {
        public string AssetKey;
        public Vector3 LocalPosition;
        public Vector3 LocalEulerAngles;
        public Vector3 LocalScale;

        public readonly bool IsEmpty => string.IsNullOrEmpty(AssetKey);

        public static EquipmentVisualDefinition FromAssetKey(string assetKey) =>
            new()
            {
                AssetKey = assetKey,
                LocalScale = Vector3.one
            };
    }
}

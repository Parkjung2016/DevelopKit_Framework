using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class SpawnEffectAnimNotify : AnimNotify
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private bool parentToOwner;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private float destroyAfterSeconds = 3f;

        public override string DisplayName => "Spawn Effect";
        public override Color EditorColor => new(0.35f, 0.85f, 0.95f, 1f);

        public override void OnNotify(AnimNotifyContext context)
        {
            if (prefab == null)
            {
                return;
            }

            Transform ownerTransform = AnimNotifyRuntimeUtility.GetOwnerTransform(context);
            Transform parent = parentToOwner ? ownerTransform : null;
            Vector3 worldPosition = ownerTransform != null
                ? ownerTransform.TransformPoint(localPosition)
                : localPosition;
            Quaternion worldRotation = ownerTransform != null
                ? ownerTransform.rotation * Quaternion.Euler(localEulerAngles)
                : Quaternion.Euler(localEulerAngles);

            GameObject instance = Object.Instantiate(prefab, worldPosition, worldRotation);
            AnimNotifyRuntimeUtility.MoveToOwnerScene(instance, context);
            if (parent != null)
                instance.transform.SetParent(parent, true);
            instance.transform.localScale = localScale;
            AnimNotifyRuntimeUtility.PlayEffects(instance);

            if (destroyAfterSeconds > 0f)
            {
                AnimNotifyRuntimeUtility.DestroySpawnedEffect(instance, destroyAfterSeconds);
            }
        }
    }
}

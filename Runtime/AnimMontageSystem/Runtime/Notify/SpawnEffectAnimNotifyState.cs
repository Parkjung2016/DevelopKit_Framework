using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class SpawnEffectAnimNotifyState : AnimNotifyState
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private bool parentToOwner = true;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private bool destroyOnEnd = true;

        [NonSerialized] private Dictionary<GameObject, GameObject> activeInstances;

        public override string DisplayName => "Effect State";
        public override Color EditorColor => new(0.95f, 0.7f, 0.35f, 1f);

        public override void OnBegin(AnimNotifyContext context)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject key = AnimNotifyRuntimeUtility.GetOwnerKey(context);
            if (key == null)
            {
                return;
            }

            activeInstances ??= new Dictionary<GameObject, GameObject>();
            EndInstance(key);

            Transform ownerTransform = AnimNotifyRuntimeUtility.GetOwnerTransform(context);
            Transform parent = parentToOwner ? ownerTransform : null;
            Vector3 worldPosition = ownerTransform != null
                ? ownerTransform.TransformPoint(localPosition)
                : localPosition;
            Quaternion worldRotation = ownerTransform != null
                ? ownerTransform.rotation * Quaternion.Euler(localEulerAngles)
                : Quaternion.Euler(localEulerAngles);

            GameObject instance = UnityEngine.Object.Instantiate(prefab, worldPosition, worldRotation);
            AnimNotifyRuntimeUtility.MoveToOwnerScene(instance, context);
            if (parent != null)
                instance.transform.SetParent(parent, true);
            instance.transform.localScale = localScale;
            AnimNotifyRuntimeUtility.PlayEffects(instance);
            activeInstances[key] = instance;
        }

        public override void OnEnd(AnimNotifyContext context)
        {
            if (!destroyOnEnd)
            {
                return;
            }

            GameObject key = AnimNotifyRuntimeUtility.GetOwnerKey(context);
            if (key == null)
            {
                return;
            }

            EndInstance(key);
        }

        private void EndInstance(GameObject key)
        {
            if (activeInstances == null || !activeInstances.TryGetValue(key, out GameObject instance))
            {
                return;
            }

            activeInstances.Remove(key);
            if (instance != null)
            {
                AnimNotifyRuntimeUtility.DestroyObject(instance);
            }
        }
    }
}

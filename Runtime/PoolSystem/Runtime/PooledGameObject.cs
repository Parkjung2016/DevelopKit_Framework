using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime
{
    [DisallowMultipleComponent]
    internal sealed class PooledGameObject : MonoBehaviour
    {
        private IPoolable[] callbacks = Array.Empty<IPoolable>();

        internal GameObjectPool Owner { get; private set; }
        internal bool IsRented { get; set; }
        internal Vector3 DefaultLocalScale { get; private set; }

        internal void Initialize(GameObjectPool owner, Vector3 defaultLocalScale)
        {
            Owner = owner;
            DefaultLocalScale = defaultLocalScale;
            CacheCallbacks();
        }

        internal void NotifySpawned()
        {
            for (int i = 0; i < callbacks.Length; i++)
            {
                try
                {
                    callbacks[i].OnSpawned();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        internal void NotifyDespawned()
        {
            for (int i = callbacks.Length - 1; i >= 0; i--)
            {
                try
                {
                    callbacks[i].OnDespawned();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void CacheCallbacks()
        {
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            int count = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolable)
                    count++;
            }

            callbacks = count == 0 ? Array.Empty<IPoolable>() : new IPoolable[count];
            int callbackIndex = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolable poolable)
                    callbacks[callbackIndex++] = poolable;
            }
        }

        private void OnDestroy()
        {
            GameObjectPool owner = Owner;
            Owner = null;
            owner?.NotifyInstanceDestroyed(this);
        }
    }
}
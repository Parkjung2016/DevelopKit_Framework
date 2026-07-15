using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime
{
    /// <summary>하나의 Prefab 인스턴스를 재사용하는 명시적 소유 풀입니다.</summary>
    public sealed class GameObjectPool : IDisposable
    {
        private readonly GameObject prefab;
        private readonly Transform storageRoot;
        private readonly Pool<PooledGameObject> pool;
        private readonly HashSet<PooledGameObject> activeInstances =
            new(ReferenceComparer<PooledGameObject>.Instance);

        private bool disposed;

        public GameObject Prefab => prefab;
        public int CountAll => pool.CountAll;
        public int CountActive => activeInstances.Count;
        public int CountInactive => pool.CountInactive;
        public int MaxSize => pool.MaxSize;
        public bool IsDisposed => disposed;

        public GameObjectPool(
            GameObject prefab,
            int initialCapacity = 0,
            int maxSize = 128,
            Transform storageParent = null)
        {
            this.prefab = prefab != null
                ? prefab
                : throw new ArgumentNullException(nameof(prefab));

            var root = new GameObject($"[{prefab.name} Pool]");
            storageRoot = root.transform;
            storageRoot.SetParent(storageParent, false);
            root.SetActive(false);

            pool = new Pool<PooledGameObject>(
                create: CreateInstance,
                onDestroy: DestroyInstance,
                initialCapacity: 0,
                maxSize: maxSize,
                collectionCheck: true);
            pool.Prewarm(initialCapacity);
        }

        public GameObject Spawn(
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            ThrowIfDisposed();

            PooledGameObject marker = pool.Rent();
            activeInstances.Add(marker);
            marker.IsRented = true;

            Transform instanceTransform = marker.transform;
            instanceTransform.SetParent(parent, false);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instanceTransform.localScale = marker.DefaultLocalScale;

            marker.gameObject.SetActive(true);
            marker.NotifySpawned();
            return marker.gameObject;
        }

        public GameObject Spawn(Transform parent = null) =>
            Spawn(prefab.transform.position, prefab.transform.rotation, parent);

        public bool Return(GameObject instance)
        {
            if (disposed || instance == null)
                return false;

            PooledGameObject marker = FindMarker(instance);
            if (marker == null || !ReferenceEquals(marker.Owner, this) || !marker.IsRented)
                return false;

            marker.NotifyDespawned();
            marker.IsRented = false;
            activeInstances.Remove(marker);

            marker.gameObject.SetActive(false);
            Transform instanceTransform = marker.transform;
            instanceTransform.SetParent(storageRoot, false);
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = marker.DefaultLocalScale;

            pool.Return(marker);
            return true;
        }

        public bool Owns(GameObject instance)
        {
            if (instance == null)
                return false;

            PooledGameObject marker = FindMarker(instance);
            return marker != null && ReferenceEquals(marker.Owner, this);
        }

        public void Prewarm(int count)
        {
            ThrowIfDisposed();
            pool.Prewarm(count);
        }

        public void Clear()
        {
            ThrowIfDisposed();
            pool.Clear();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            if (activeInstances.Count > 0)
            {
                var active = new PooledGameObject[activeInstances.Count];
                activeInstances.CopyTo(active);
                activeInstances.Clear();

                for (int i = 0; i < active.Length; i++)
                {
                    PooledGameObject marker = active[i];
                    if (marker != null)
                        DestroyObject(marker.gameObject);
                }
            }

            pool.Clear();

            if (storageRoot != null)
                DestroyObject(storageRoot.gameObject);
        }

        internal void NotifyInstanceDestroyed(PooledGameObject marker)
        {
            activeInstances.Remove(marker);
            pool.Forget(marker);
        }

        internal static PooledGameObject FindMarker(GameObject instance) =>
            instance != null
                ? instance.GetComponentInParent<PooledGameObject>(true)
                : null;

        private PooledGameObject CreateInstance()
        {
            GameObject instance = Object.Instantiate(prefab, storageRoot);
            instance.name = prefab.name;
            instance.SetActive(false);

            PooledGameObject marker = instance.GetComponent<PooledGameObject>();
            if (marker == null)
                marker = instance.AddComponent<PooledGameObject>();

            marker.Initialize(this, prefab.transform.localScale);
            return marker;
        }

        private static void DestroyInstance(PooledGameObject marker)
        {
            if (marker != null)
                DestroyObject(marker.gameObject);
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GameObjectPool));
        }
    }
}
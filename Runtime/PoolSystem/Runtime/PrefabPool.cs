using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime
{
    public readonly struct PrefabPoolStats
    {
        public GameObject Prefab { get; }
        public int CountAll { get; }
        public int CountActive { get; }
        public int CountInactive { get; }
        public int MaxSize { get; }

        internal PrefabPoolStats(GameObjectPool pool)
        {
            Prefab = pool.Prefab;
            CountAll = pool.CountAll;
            CountActive = pool.CountActive;
            CountInactive = pool.CountInactive;
            MaxSize = pool.MaxSize;
        }
    }

    /// <summary>Prefab별 GameObjectPool을 필요할 때 만들고 공유합니다.</summary>
    public static class PrefabPool
    {
        private const int DefaultMaxSize = 128;

        private static readonly Dictionary<GameObject, GameObjectPool> Pools =
            new(ReferenceComparer<GameObject>.Instance);

        private static Transform root;

        public static int PoolCount => Pools.Count;

        public static GameObject Spawn(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            return GetOrCreate(prefab).Spawn(position, rotation, parent);
        }

        public static GameObject Spawn(GameObject prefab, Transform parent = null)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            return Spawn(
                prefab,
                prefab.transform.position,
                prefab.transform.rotation,
                parent);
        }

        public static void Prewarm(GameObject prefab, int count, int maxSize = DefaultMaxSize)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            GetOrCreate(prefab, count, maxSize).Prewarm(count);
        }

        public static bool Release(GameObject instance)
        {
            if (instance == null)
                return false;

            PooledGameObject marker = GameObjectPool.FindMarker(instance);
            return marker != null &&
                   marker.Owner != null &&
                   marker.Owner.Return(instance);
        }

        public static bool IsPooled(GameObject instance) =>
            GameObjectPool.FindMarker(instance) != null;

        public static bool Remove(GameObject prefab)
        {
            if (prefab == null || !Pools.Remove(prefab, out GameObjectPool pool))
                return false;

            pool.Dispose();
            DestroyRootIfEmpty();
            return true;
        }

        public static void GetStats(List<PrefabPoolStats> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            if (destination.Capacity < Pools.Count)
                destination.Capacity = Pools.Count;

            foreach (GameObjectPool pool in Pools.Values)
                destination.Add(new PrefabPoolStats(pool));
        }

        public static void ClearInactive()
        {
            foreach (GameObjectPool pool in Pools.Values)
                pool.Clear();
        }
        public static void Clear()
        {
            foreach (GameObjectPool pool in Pools.Values)
                pool.Dispose();

            Pools.Clear();

            if (root != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(root.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
            }

            root = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();

        private static GameObjectPool GetOrCreate(
            GameObject prefab,
            int initialCapacity = 0,
            int maxSize = DefaultMaxSize)
        {
            if (Pools.TryGetValue(prefab, out GameObjectPool existing) && !existing.IsDisposed)
                return existing;

            var pool = new GameObjectPool(
                prefab,
                initialCapacity: initialCapacity,
                maxSize: maxSize,
                storageParent: GetRoot());
            Pools[prefab] = pool;
            return pool;
        }

        private static Transform GetRoot()
        {
            if (root != null)
                return root;

            var rootObject = new GameObject("[Prefab Pool]");
            root = rootObject.transform;
            rootObject.SetActive(false);

            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(rootObject);

            return root;
        }

        private static void DestroyRootIfEmpty()
        {
            if (Pools.Count != 0 || root == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(root.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(root.gameObject);

            root = null;
        }
    }
}
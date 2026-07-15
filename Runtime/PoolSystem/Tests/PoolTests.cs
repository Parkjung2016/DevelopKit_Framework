using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime.Tests
{
    [TestFixture]
    public sealed class PoolTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            PrefabPool.Clear();

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void RentAndReturn_ReusesSameInstance()
        {
            int created = 0;
            var pool = new Pool<TestItem>(() =>
            {
                created++;
                return new TestItem();
            });

            TestItem first = pool.Rent();
            pool.Return(first);
            TestItem second = pool.Rent();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, created);
            Assert.AreEqual(1, pool.CountActive);
        }

        [Test]
        public void Return_DuplicateOrForeignItemThrows()
        {
            var pool = new Pool<TestItem>(() => new TestItem());
            TestItem item = pool.Rent();
            pool.Return(item);

            Assert.Throws<InvalidOperationException>(() => pool.Return(item));
            Assert.Throws<InvalidOperationException>(() => pool.Return(new TestItem()));
        }

        [Test]
        public void Return_OverMaxSizeDestroysOverflow()
        {
            int destroyed = 0;
            var pool = new Pool<TestItem>(
                create: () => new TestItem(),
                onDestroy: _ => destroyed++,
                maxSize: 1);

            TestItem first = pool.Rent();
            TestItem second = pool.Rent();
            pool.Return(first);
            pool.Return(second);

            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
            Assert.AreEqual(1, destroyed);
        }

        [Test]
        public void ListLease_ClearsAndReturnsList()
        {
            List<int> first;
            using (ListPool<int>.Rent(out first))
                first.Add(10);

            List<int> second = ListPool<int>.Rent();

            Assert.AreSame(first, second);
            Assert.IsEmpty(second);
            ListPool<int>.Return(second);
        }

        [Test]
        public void GameObjectPool_ReusesInstanceAndCallsLifecycle()
        {
            GameObject prefab = CreateGameObject("EffectPrefab");
            var callback = prefab.AddComponent<PoolTestBehaviour>();
            var pool = new GameObjectPool(prefab, initialCapacity: 1, maxSize: 4);

            GameObject first = pool.Spawn(new Vector3(1f, 2f, 3f), Quaternion.identity);
            PoolTestBehaviour firstCallback = first.GetComponent<PoolTestBehaviour>();

            Assert.IsTrue(first.activeSelf);
            Assert.AreEqual(1, firstCallback.SpawnCount);
            Assert.AreEqual(0, callback.SpawnCount);

            Assert.IsTrue(pool.Return(first));
            Assert.IsFalse(first.activeSelf);
            Assert.AreEqual(1, firstCallback.DespawnCount);

            GameObject second = pool.Spawn();
            Assert.AreSame(first, second);
            Assert.AreEqual(2, firstCallback.SpawnCount);

            pool.Dispose();
        }

        [Test]
        public void PrefabPool_PrewarmSpawnReleaseAndStats()
        {
            GameObject prefab = CreateGameObject("ProjectilePrefab");

            PrefabPool.Prewarm(prefab, count: 3, maxSize: 8);
            GameObject instance = PrefabPool.Spawn(prefab);

            var stats = new List<PrefabPoolStats>();
            PrefabPool.GetStats(stats);

            Assert.AreEqual(1, stats.Count);
            Assert.AreEqual(3, stats[0].CountAll);
            Assert.AreEqual(1, stats[0].CountActive);
            Assert.AreEqual(2, stats[0].CountInactive);
            Assert.IsTrue(PrefabPool.Release(instance));

            PrefabPool.GetStats(stats);
            Assert.AreEqual(0, stats[0].CountActive);
            Assert.AreEqual(3, stats[0].CountInactive);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private sealed class TestItem
        {
        }
    }

    internal sealed class PoolTestBehaviour : MonoBehaviour, IPoolable
    {
        public int SpawnCount { get; private set; }
        public int DespawnCount { get; private set; }

        public void OnSpawned() => SpawnCount++;

        public void OnDespawned() => DespawnCount++;
    }
}
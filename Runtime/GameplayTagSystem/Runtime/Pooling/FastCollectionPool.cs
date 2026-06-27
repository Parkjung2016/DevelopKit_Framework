using System.Collections.Generic;
using UnityEngine.Pool;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    internal class FastCollectionPool<TCollection, TItem> where TCollection : class, ICollection<TItem>, new()
    {
        internal static readonly ObjectPool<TCollection> pool = new
        (
            collectionCheck: false,
            createFunc: () => new TCollection(),
            actionOnRelease: delegate (TCollection l)
            {
                l.Clear();
            }
        );

        public static TCollection Get()
        {
            return pool.Get();
        }

        public static PooledObject<TCollection> Get(out TCollection value)
        {
            return pool.Get(out value);
        }

        public static void Release(TCollection toRelease)
        {
            pool.Release(toRelease);
        }
    }
}

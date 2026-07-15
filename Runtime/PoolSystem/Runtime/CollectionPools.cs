using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime
{
    public static class CollectionPool<TCollection, TItem>
        where TCollection : class, ICollection<TItem>, new()
    {
        private static readonly Pool<TCollection> Pool = new(
            create: static () => new TCollection(),
            onReturn: static collection => collection.Clear(),
            initialCapacity: 1,
            maxSize: 64,
            collectionCheck: true);

        public static TCollection Rent() => Pool.Rent();

        public static PoolLease<TCollection> Rent(out TCollection collection) =>
            Pool.Rent(out collection);

        public static void Return(TCollection collection) => Pool.Return(collection);

        public static void Clear() => Pool.Clear();
    }

    public static class ListPool<T>
    {
        public static List<T> Rent() => CollectionPool<List<T>, T>.Rent();

        public static PoolLease<List<T>> Rent(out List<T> list) =>
            CollectionPool<List<T>, T>.Rent(out list);

        public static void Return(List<T> list) => CollectionPool<List<T>, T>.Return(list);
    }

    public static class HashSetPool<T>
    {
        public static HashSet<T> Rent() => CollectionPool<HashSet<T>, T>.Rent();

        public static PoolLease<HashSet<T>> Rent(out HashSet<T> set) =>
            CollectionPool<HashSet<T>, T>.Rent(out set);

        public static void Return(HashSet<T> set) => CollectionPool<HashSet<T>, T>.Return(set);
    }

    public static class DictionaryPool<TKey, TValue>
    {
        public static Dictionary<TKey, TValue> Rent() =>
            CollectionPool<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>.Rent();

        public static PoolLease<Dictionary<TKey, TValue>> Rent(out Dictionary<TKey, TValue> dictionary) =>
            CollectionPool<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>.Rent(out dictionary);

        public static void Return(Dictionary<TKey, TValue> dictionary) =>
            CollectionPool<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>.Return(dictionary);
    }
}
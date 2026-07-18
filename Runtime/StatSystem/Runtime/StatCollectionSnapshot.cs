using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// 저장할 수 있는 스탯 기본값 하나를 나타냅니다.
    /// </summary>
    [Serializable]
    public struct StatValueSnapshot
    {
        [SerializeField] private StatId id;
        [SerializeField] private float baseValue;

        public StatId Id => id;
        public float BaseValue => baseValue;

        public StatValueSnapshot(StatId id, float baseValue)
        {
            this.id = id;
            this.baseValue = baseValue;
        }
    }

    /// <summary>
    /// 저장할 수 있는 스탯 Modifier 하나를 나타냅니다.
    /// </summary>
    [Serializable]
    public struct StatModifierSnapshot
    {
        [SerializeField] private StatId id;
        [SerializeField] private string key;
        [SerializeField] private float flat;
        [SerializeField] private float percent;

        public StatId Id => id;
        public string Key => key;
        public StatModifier Modifier => new(flat, percent);

        public StatModifierSnapshot(StatId id, string key, in StatModifier modifier)
        {
            this.id = id;
            this.key = key;
            flat = modifier.Flat;
            percent = modifier.Percent;
        }
    }

    /// <summary>
    /// StatCollection의 저장 및 복원용 데이터입니다.
    /// 영속 키를 사용한 Modifier만 함께 저장됩니다.
    /// </summary>
    [Serializable]
    public sealed class StatCollectionSnapshot
    {
        [SerializeField] private List<StatValueSnapshot> stats = new();
        [SerializeField] private List<StatModifierSnapshot> modifiers = new();

        public IReadOnlyList<StatValueSnapshot> Stats =>
            stats ?? (IReadOnlyList<StatValueSnapshot>)Array.Empty<StatValueSnapshot>();

        public IReadOnlyList<StatModifierSnapshot> Modifiers =>
            modifiers ?? (IReadOnlyList<StatModifierSnapshot>)Array.Empty<StatModifierSnapshot>();

        internal void Capture(StatCollection source, bool includePersistentModifiers)
        {
            stats ??= new List<StatValueSnapshot>();
            modifiers ??= new List<StatModifierSnapshot>();
            stats.Clear();
            modifiers.Clear();

            if (stats.Capacity < source.Count)
                stats.Capacity = source.Count;

            foreach (Stat stat in source)
            {
                stats.Add(new StatValueSnapshot(stat.Id, stat.BaseValue));
                if (includePersistentModifiers)
                    stat.CapturePersistentModifiers(modifiers);
            }
        }
    }
}

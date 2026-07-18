using System;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// enum 값을 StatId로 변환해 기존 스탯 API를 그대로 사용할 수 있게 합니다.
    /// 변환 결과는 enum 타입마다 한 번만 생성됩니다.
    /// </summary>
    public static class StatEnumExtensions
    {
        public static Stat GetStat<TEnum>(this StatCollection stats, TEnum id)
            where TEnum : struct, Enum =>
            stats.GetStat(StatId.From(id));

        public static bool TryGetStat<TEnum>(this StatCollection stats, TEnum id, out Stat stat)
            where TEnum : struct, Enum =>
            stats.TryGetStat(StatId.From(id), out stat);

        public static bool HasStat<TEnum>(this StatCollection stats, TEnum id)
            where TEnum : struct, Enum =>
            stats.HasStat(StatId.From(id));

        public static float GetBaseValue<TEnum>(this StatCollection stats, TEnum id)
            where TEnum : struct, Enum =>
            stats.GetBaseValue(StatId.From(id));

        public static void SetBaseValue<TEnum>(this StatCollection stats, TEnum id, float value)
            where TEnum : struct, Enum =>
            stats.SetBaseValue(StatId.From(id), value);

        public static float AddBaseValue<TEnum>(this StatCollection stats, TEnum id, float amount)
            where TEnum : struct, Enum =>
            stats.AddBaseValue(StatId.From(id), amount);

        public static void SetModifier<TEnum>(
            this StatCollection stats,
            TEnum id,
            StatModifierKey key,
            in StatModifier modifier)
            where TEnum : struct, Enum =>
            stats.SetModifier(StatId.From(id), key, modifier);

        public static void SetFlatModifier<TEnum>(
            this StatCollection stats,
            TEnum id,
            StatModifierKey key,
            float amount)
            where TEnum : struct, Enum =>
            stats.SetFlatModifier(StatId.From(id), key, amount);

        public static void SetPercentModifier<TEnum>(
            this StatCollection stats,
            TEnum id,
            StatModifierKey key,
            float percent)
            where TEnum : struct, Enum =>
            stats.SetPercentModifier(StatId.From(id), key, percent);

        public static bool RemoveFlatModifier<TEnum>(
            this StatCollection stats,
            TEnum id,
            StatModifierKey key)
            where TEnum : struct, Enum =>
            stats.RemoveFlatModifier(StatId.From(id), key);

        public static bool RemovePercentModifier<TEnum>(
            this StatCollection stats,
            TEnum id,
            StatModifierKey key)
            where TEnum : struct, Enum =>
            stats.RemovePercentModifier(StatId.From(id), key);
        public static bool RemoveModifiers<TEnum>(
            this StatCollection stats,
            TEnum id,
            StatModifierKey key)
            where TEnum : struct, Enum =>
            stats.RemoveModifiers(StatId.From(id), key);

        public static Stat GetStat<TEnum>(this ObjectStatSystem system, TEnum id)
            where TEnum : struct, Enum =>
            system.GetStat(StatId.From(id));

        public static bool TryGetStat<TEnum>(
            this ObjectStatSystem system,
            TEnum id,
            out Stat stat)
            where TEnum : struct, Enum =>
            system.TryGetStat(StatId.From(id), out stat);

        public static bool HasStat<TEnum>(this ObjectStatSystem system, TEnum id)
            where TEnum : struct, Enum =>
            system.HasStat(StatId.From(id));

        public static float GetBaseValue<TEnum>(this ObjectStatSystem system, TEnum id)
            where TEnum : struct, Enum =>
            system.GetBaseValue(StatId.From(id));

        public static void SetBaseValue<TEnum>(
            this ObjectStatSystem system,
            TEnum id,
            float value)
            where TEnum : struct, Enum =>
            system.SetBaseValue(StatId.From(id), value);

        public static float AddBaseValue<TEnum>(
            this ObjectStatSystem system,
            TEnum id,
            float amount)
            where TEnum : struct, Enum =>
            system.AddBaseValue(StatId.From(id), amount);
        public static void SetModifier<TEnum>(
            this ObjectStatSystem system,
            TEnum id,
            StatModifierKey key,
            in StatModifier modifier)
            where TEnum : struct, Enum =>
            system.SetModifier(StatId.From(id), key, modifier);

        public static void SetFlatModifier<TEnum>(
            this ObjectStatSystem system,
            TEnum id,
            StatModifierKey key,
            float amount)
            where TEnum : struct, Enum =>
            system.SetFlatModifier(StatId.From(id), key, amount);

        public static void SetPercentModifier<TEnum>(
            this ObjectStatSystem system,
            TEnum id,
            StatModifierKey key,
            float percent)
            where TEnum : struct, Enum =>
            system.SetPercentModifier(StatId.From(id), key, percent);

        public static bool RemoveFlatModifier<TEnum>(
            this ObjectStatSystem system,
            TEnum id,
            StatModifierKey key)
            where TEnum : struct, Enum =>
            system.RemoveFlatModifier(StatId.From(id), key);

        public static bool RemovePercentModifier<TEnum>(
            this ObjectStatSystem system,
            TEnum id,
            StatModifierKey key)
            where TEnum : struct, Enum =>
            system.RemovePercentModifier(StatId.From(id), key);
        public static bool RemoveModifiers<TEnum>(
            this ObjectStatSystem system,
            TEnum id,
            StatModifierKey key)
            where TEnum : struct, Enum =>
            system.RemoveModifiers(StatId.From(id), key);
    }
}

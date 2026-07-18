using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class StatIdValueAttribute : Attribute
    {
        public string Value { get; }

        public StatIdValueAttribute(string value) => Value = value;
    }

    [Serializable]
    public struct StatId : IEquatable<StatId>
    {
        [SerializeField] private string value;

        public string Value => value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(value);

        public StatId(string value) => this.value = value?.Trim();

        public static StatId From<TEnum>(TEnum value) where TEnum : struct, Enum =>
            EnumStatIdCache<TEnum>.Get(value);

        public bool Equals(StatId other) =>
            StringComparer.Ordinal.Equals(value, other.value);

        public override bool Equals(object obj) => obj is StatId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;

        public static implicit operator StatId(string value) => new(value);
        public static explicit operator string(StatId id) => id.Value;
        public static bool operator ==(StatId left, StatId right) => left.Equals(right);
        public static bool operator !=(StatId left, StatId right) => !left.Equals(right);
    }

    internal static class EnumStatIdCache<TEnum> where TEnum : struct, Enum
    {
        private static readonly Dictionary<TEnum, StatId> Ids = Build();

        public static StatId Get(TEnum value) =>
            Ids.TryGetValue(value, out StatId id) ? id : new StatId(value.ToString());

        private static Dictionary<TEnum, StatId> Build()
        {
            Array values = Enum.GetValues(typeof(TEnum));
            var result = new Dictionary<TEnum, StatId>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                var value = (TEnum)values.GetValue(i);
                string name = Enum.GetName(typeof(TEnum), value) ?? value.ToString();
                FieldInfo field = typeof(TEnum).GetField(name);
                string statValue = field?.GetCustomAttribute<StatIdValueAttribute>()?.Value;
                result[value] = new StatId(
                    string.IsNullOrWhiteSpace(statValue) ? name : statValue);
            }

            return result;
        }
    }
}

using System;
using System.Threading;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// Stat Modifier의 출처를 식별합니다. 문자열 키는 저장되며, 런타임 키는 현재 실행 중에만 유효합니다.
    /// </summary>
    public readonly struct StatModifierKey : IEquatable<StatModifierKey>
    {
        private static long nextRuntimeId;
        private readonly string persistentId;
        private readonly long runtimeId;

        private StatModifierKey(string persistentId, long runtimeId)
        {
            this.persistentId = persistentId;
            this.runtimeId = runtimeId;
        }

        public bool IsValid => IsPersistent || runtimeId != 0;
        public bool IsPersistent => !string.IsNullOrEmpty(persistentId);
        public string PersistentId => IsPersistent ? persistentId : null;

        public static StatModifierKey Persistent(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            string trimmed = id.Trim();
            if (trimmed.Length == 0)
                throw new ArgumentException("Modifier key cannot be empty.", nameof(id));

            return new StatModifierKey(trimmed, 0);
        }

        public static StatModifierKey CreateRuntime()
        {
            long id;
            do
            {
                id = Interlocked.Increment(ref nextRuntimeId);
            } while (id == 0);

            return new StatModifierKey(null, id);
        }

        public bool Equals(StatModifierKey other) =>
            runtimeId == other.runtimeId &&
            string.Equals(persistentId, other.persistentId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is StatModifierKey other && Equals(other);

        public override int GetHashCode() => IsPersistent
            ? StringComparer.Ordinal.GetHashCode(persistentId)
            : runtimeId.GetHashCode();

        public override string ToString() => IsPersistent
            ? persistentId
            : runtimeId != 0 ? $"Runtime:{runtimeId}" : "Invalid";

        public static bool operator ==(StatModifierKey left, StatModifierKey right) => left.Equals(right);
        public static bool operator !=(StatModifierKey left, StatModifierKey right) => !left.Equals(right);
        public static implicit operator StatModifierKey(string id) => Persistent(id);
    }
}

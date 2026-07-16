using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_AbilitySetup", menuName = "PJDev/Ability System/Setup")]
    public sealed class AbilitySetupSO : ScriptableObject, IReadOnlyList<AbilitySO>
    {
        [SerializeField] private List<AbilitySO> abilities = new();

        public int Count => abilities.Count;
        public AbilitySO this[int index] => abilities[index];

        public List<AbilitySO>.Enumerator GetEnumerator() => abilities.GetEnumerator();

        IEnumerator<AbilitySO> IEnumerable<AbilitySO>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
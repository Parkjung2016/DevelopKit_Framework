using System.Collections;
using UnityEngine;

namespace Skddkkkk.DevelopKit.Framework.AbilitySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_AbilitySetup", menuName = "SO/GameAbility/Config/Setup")]
    public class AbilitySetupSO : ScriptableObject, IEnumerable
    {
        [SerializeField] private AbilitySO[] giveAbilities;

        public IEnumerator GetEnumerator()
        {
            return giveAbilities.GetEnumerator();
        }
    }
}
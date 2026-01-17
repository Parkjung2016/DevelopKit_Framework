using System.Collections.Generic;
using UnityEngine;

namespace Code.Runtime.Core.StatSystem
{
    [CreateAssetMenu(fileName = "SO_StatOverrideList", menuName = "SO/StatSystem/StatOverrideList")]
    public class StatOverrideListSO : ScriptableObject
    {
        public List<StatOverride> statOverrides = new();
    }
}
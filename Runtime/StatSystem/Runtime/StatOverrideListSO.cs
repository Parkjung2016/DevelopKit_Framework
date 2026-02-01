using System.Collections.Generic;
using UnityEngine;

namespace Skddkkkk.DevelopKit.Framework.StatSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_StatOverrideList", menuName = "SO/StatSystem/StatOverrideList")]
    public class StatOverrideListSO : ScriptableObject
    {
        public List<StatOverride> statOverrides = new();
    }
}
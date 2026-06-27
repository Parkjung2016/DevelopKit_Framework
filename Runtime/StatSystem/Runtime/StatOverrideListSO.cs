using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_StatOverrideList", menuName = "PJDev/SO/StatSystem/StatOverrideList")]
    public class StatOverrideListSO : ScriptableObject
    {
        public List<StatOverride> statOverrides = new();
    }
}
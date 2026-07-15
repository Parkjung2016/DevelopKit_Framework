using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_StatOverrides", menuName = "PJDev/Stat System/Stat Overrides")]
    public sealed class StatOverrideListSO : ScriptableObject
    {
        [SerializeField] private List<StatOverride> statOverrides = new();

        public IReadOnlyList<StatOverride> Entries => statOverrides;

        public void CopyEntriesTo(List<StatOverrideEntry> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            for (int i = 0; i < statOverrides.Count; i++)
            {
                StatOverride entry = statOverrides[i];
                if (entry != null && entry.IsValid)
                    destination.Add(entry.CreateEntry());
            }
        }
    }
}
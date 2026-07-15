using UnityEngine;

namespace PJDev.DevelopKit.Framework.PoolSystem.Runtime
{
    [AddComponentMenu("PJDev/Framework/Prefab Pool Preloader")]
    public sealed class PrefabPoolPreloader : MonoBehaviour
    {
        [SerializeField] private PrefabPoolSettingsSO settings = null;

        private void Awake()
        {
            if (settings != null)
                settings.Prewarm();
        }
    }
}
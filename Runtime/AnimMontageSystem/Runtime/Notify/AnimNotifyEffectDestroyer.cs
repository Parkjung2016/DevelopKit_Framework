using PJDev.DevelopKit.BasicTemplate.Runtime.PoolSystem;
using UnityEngine;
using UnityEngine.VFX;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public sealed class AnimNotifyEffectDestroyer : MonoBehaviour
    {
        private ParticleSystem[] particleSystems;
        private VisualEffect[] visualEffects;
        private float stopAtTime;
        private float minimumDestroyAtTime;
        private float forceDestroyAtTime;
        private bool stopped;
        private bool stopLoopingEffectsOnly;
        private bool shouldStopEffects;

        public void Begin(float delay, bool stopLoopingEffectsOnly)
        {
            particleSystems ??= GetComponentsInChildren<ParticleSystem>(true);
            visualEffects ??= GetComponentsInChildren<VisualEffect>(true);
            this.stopLoopingEffectsOnly = stopLoopingEffectsOnly;
            shouldStopEffects = !stopLoopingEffectsOnly
                                || AnimNotifyRuntimeUtility.HasLoopingEffects(particleSystems, visualEffects);
            stopAtTime = Time.time + Mathf.Max(0f, delay);
            minimumDestroyAtTime = shouldStopEffects
                ? stopAtTime
                : Time.time + AnimNotifyRuntimeUtility.EstimateNaturalEffectLifetime(particleSystems, visualEffects);
            forceDestroyAtTime = minimumDestroyAtTime + 10f;
            stopped = false;
            enabled = true;
        }

        private void Update()
        {
            if (shouldStopEffects && !stopped)
            {
                if (Time.time < stopAtTime)
                    return;

                AnimNotifyRuntimeUtility.StopEffects(particleSystems, visualEffects, stopLoopingEffectsOnly);
                stopped = true;
            }

            if (Time.time < minimumDestroyAtTime)
            {
                return;
            }

            if (Time.time < forceDestroyAtTime
                && AnimNotifyRuntimeUtility.AreEffectsAlive(particleSystems, visualEffects))
            {
                return;
            }

            CompleteDestroy();
        }

        private void CompleteDestroy()
        {
            enabled = false;
            if (!PrefabPool.Release(gameObject))
            {
                if (gameObject.activeSelf)
                    gameObject.SetActive(false);

                Destroy(gameObject);
            }
        }
    }
}

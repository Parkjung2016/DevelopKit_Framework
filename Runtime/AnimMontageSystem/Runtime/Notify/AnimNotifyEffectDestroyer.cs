using UnityEngine;
using UnityEngine.VFX;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public sealed class AnimNotifyEffectDestroyer : MonoBehaviour
    {
        private ParticleSystem[] particleSystems;
        private VisualEffect[] visualEffects;
        private float stopAtTime;
        private float forceDestroyAtTime;
        private bool stopped;
        private bool stopLoopingEffectsOnly;
        private bool shouldStopEffects;

        public void Begin(float delay, bool stopLoopingEffectsOnly)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            visualEffects = GetComponentsInChildren<VisualEffect>(true);
            this.stopLoopingEffectsOnly = stopLoopingEffectsOnly;
            shouldStopEffects = !stopLoopingEffectsOnly
                                || AnimNotifyRuntimeUtility.HasLoopingEffects(particleSystems, visualEffects);
            stopAtTime = Time.time + Mathf.Max(0f, delay);
            forceDestroyAtTime = (shouldStopEffects ? stopAtTime : Time.time) + 10f;
            stopped = false;
            enabled = true;

            if (!shouldStopEffects && !AnimNotifyRuntimeUtility.AreEffectsAlive(particleSystems, visualEffects))
                CompleteDestroy();
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

            if (Time.time < forceDestroyAtTime
                && AnimNotifyRuntimeUtility.AreEffectsAlive(particleSystems, visualEffects))
            {
                return;
            }

            CompleteDestroy();
        }

        private void CompleteDestroy()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);

            Destroy(gameObject);
        }
    }
}

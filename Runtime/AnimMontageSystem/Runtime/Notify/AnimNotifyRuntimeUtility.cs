using PJDev.DevelopKit.BasicTemplate.Runtime.PoolSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    internal static class AnimNotifyRuntimeUtility
    {
        public static Transform GetOwnerTransform(AnimNotifyContext context)
        {
            if (context.Animator != null)
            {
                return context.Animator.transform;
            }

            return context.Owner != null ? context.Owner.transform : null;
        }

        public static GameObject GetOwnerKey(AnimNotifyContext context)
        {
            if (context.Owner != null)
            {
                return context.Owner;
            }

            return context.Animator != null ? context.Animator.gameObject : null;
        }

        public static GameObject SpawnPrefab(
            GameObject prefab,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Transform parent,
            AnimNotifyContext context)
        {
            if (prefab == null)
                return null;

            GameObject instance = Application.isPlaying
                ? PrefabPool.Spawn(prefab, worldPosition, worldRotation, parent)
                : Object.Instantiate(prefab, worldPosition, worldRotation, parent);

            MoveToOwnerScene(instance, context);
            return instance;
        }
        public static void MoveToOwnerScene(GameObject instance, AnimNotifyContext context)
        {
            if (instance == null)
            {
                return;
            }

            GameObject owner = GetOwnerKey(context);
            if (owner == null || !owner.scene.IsValid())
            {
                return;
            }

            if (instance.scene != owner.scene)
                SceneManager.MoveGameObjectToScene(instance, owner.scene);

            AttachToEditorPreviewOwner(instance, owner);
        }

        public static void PlayEffects(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Clear(true);
                particleSystem.Play(true);
            }

            VisualEffect[] visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
            for (int i = 0; i < visualEffects.Length; i++)
            {
                VisualEffect visualEffect = visualEffects[i];
                if (visualEffect == null)
                    continue;

                visualEffect.Reinit();
                visualEffect.Play();
            }
        }

        private static void AttachToEditorPreviewOwner(GameObject instance, GameObject owner)
        {
            if (Application.isPlaying || instance == null || owner == null)
            {
                return;
            }

            Transform ownerTransform = owner.transform;
            Transform instanceTransform = instance.transform;
            if (ownerTransform == null || instanceTransform == null)
            {
                return;
            }

            if (instanceTransform == ownerTransform || instanceTransform.IsChildOf(ownerTransform))
            {
                return;
            }

            instanceTransform.SetParent(ownerTransform, true);
        }

        public static void DestroyObject(Object target, float delay = 0f)
        {
            if (target == null)
            {
                return;
            }

            if (target is GameObject gameObject)
            {
                float safeDelay = Mathf.Max(0f, delay);
                if (HasEffects(gameObject))
                {
                    StopEffectsThenDestroy(gameObject, safeDelay);
                    return;
                }

                if (Application.isPlaying && PrefabPool.IsPooled(gameObject))
                {
                    if (safeDelay <= 0f)
                        PrefabPool.Release(gameObject);
                    else
                        DestroyEffectObject(gameObject, safeDelay, stopLoopingEffectsOnly: false);

                    return;
                }
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target, Mathf.Max(0f, delay));
                return;
            }

#if UNITY_EDITOR
            if (delay <= 0f)
            {
                Object.DestroyImmediate(target);
                return;
            }

            double destroyTime = EditorApplication.timeSinceStartup + delay;
            void Tick()
            {
                if (target == null)
                {
                    EditorApplication.update -= Tick;
                    return;
                }

                if (EditorApplication.timeSinceStartup < destroyTime)
                {
                    return;
                }

                EditorApplication.update -= Tick;
                Object.DestroyImmediate(target);
            }

            EditorApplication.update += Tick;
#else
            Object.DestroyImmediate(target);
#endif
        }

        public static void DestroySpawnedEffect(GameObject target, float loopingStopDelay)
        {
            if (target == null)
                return;

            if (!HasEffects(target))
            {
                DestroyObject(target, Mathf.Max(0f, loopingStopDelay));
                return;
            }

            DestroyEffectObject(target, Mathf.Max(0f, loopingStopDelay), stopLoopingEffectsOnly: true);
        }

        private static bool HasEffects(GameObject target)
        {
            if (target == null)
                return false;

            return target.GetComponentInChildren<ParticleSystem>(true) != null
                   || target.GetComponentInChildren<VisualEffect>(true) != null;
        }

        private static void StopEffectsThenDestroy(GameObject target, float delay)
        {
            if (target == null)
                return;

            DestroyEffectObject(target, delay, stopLoopingEffectsOnly: false);
        }

        private static void DestroyEffectObject(GameObject target, float delay, bool stopLoopingEffectsOnly)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
            {
                AnimNotifyEffectDestroyer destroyer = target.GetComponent<AnimNotifyEffectDestroyer>();
                if (destroyer == null)
                    destroyer = target.AddComponent<AnimNotifyEffectDestroyer>();

                destroyer.Begin(delay, stopLoopingEffectsOnly);
                return;
            }

#if UNITY_EDITOR
            double stopTime = EditorApplication.timeSinceStartup + delay;
            bool stopped = false;
            ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
            VisualEffect[] visualEffects = target.GetComponentsInChildren<VisualEffect>(true);
            bool hasLoopingEffects = HasLoopingEffects(particleSystems, visualEffects);
            bool shouldStopEffects = !stopLoopingEffectsOnly || hasLoopingEffects;
            double minimumDestroyTime = shouldStopEffects
                ? stopTime
                : EditorApplication.timeSinceStartup + EstimateNaturalEffectLifetime(particleSystems, visualEffects);
            double forceDestroyTime = minimumDestroyTime + 10.0;

            void Tick()
            {
                if (target == null)
                {
                    EditorApplication.update -= Tick;
                    return;
                }

                double now = EditorApplication.timeSinceStartup;
                if (shouldStopEffects && !stopped)
                {
                    if (now < stopTime)
                        return;

                    StopEffects(particleSystems, visualEffects, stopLoopingEffectsOnly);
                    stopped = true;
                }

                if (now < minimumDestroyTime)
                    return;

                if (now < forceDestroyTime && AreEffectsAlive(particleSystems, visualEffects))
                    return;

                EditorApplication.update -= Tick;
                Object.DestroyImmediate(target);
            }

            EditorApplication.update += Tick;
#else
            Object.DestroyImmediate(target);
#endif
        }

        internal static bool HasLoopingEffects(ParticleSystem[] particleSystems, VisualEffect[] visualEffects)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                if (particleSystem.main.loop)
                    return true;
            }

            return visualEffects.Length > 0;
        }

        internal static float EstimateNaturalEffectLifetime(ParticleSystem[] particleSystems, VisualEffect[] visualEffects)
        {
            float lifetime = 0.25f;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule main = particleSystem.main;
                if (main.loop)
                    continue;

                lifetime = Mathf.Max(
                    lifetime,
                    GetMaxCurveValue(main.startDelay) + main.duration + GetMaxCurveValue(main.startLifetime));
            }

            if (visualEffects.Length > 0)
                lifetime = Mathf.Max(lifetime, 10f);

            return lifetime;
        }

        private static float GetMaxCurveValue(ParticleSystem.MinMaxCurve curve)
        {
            return curve.mode switch
            {
                ParticleSystemCurveMode.Constant => curve.constant,
                ParticleSystemCurveMode.TwoConstants => curve.constantMax,
                ParticleSystemCurveMode.Curve => curve.curveMax != null
                    ? GetCurveMaxValue(curve.curveMax, curve.curveMultiplier)
                    : curve.constant,
                ParticleSystemCurveMode.TwoCurves => curve.curveMax != null
                    ? GetCurveMaxValue(curve.curveMax, curve.curveMultiplier)
                    : curve.constantMax,
                _ => curve.constantMax
            };
        }

        private static float GetCurveMaxValue(AnimationCurve curve, float multiplier)
        {
            if (curve == null || curve.length == 0)
                return 0f;

            float max = 0f;
            for (int i = 0; i < curve.length; i++)
                max = Mathf.Max(max, curve.keys[i].value);

            return max * multiplier;
        }

        internal static void StopEffects(ParticleSystem[] particleSystems, VisualEffect[] visualEffects, bool loopingOnly)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                if (loopingOnly && !particleSystem.main.loop)
                    continue;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            for (int i = 0; i < visualEffects.Length; i++)
            {
                VisualEffect visualEffect = visualEffects[i];
                if (visualEffect == null)
                    continue;

                visualEffect.Stop();
            }
        }

        internal static bool AreEffectsAlive(ParticleSystem[] particleSystems, VisualEffect[] visualEffects)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem != null && particleSystem.IsAlive(true))
                    return true;
            }

            for (int i = 0; i < visualEffects.Length; i++)
            {
                VisualEffect visualEffect = visualEffects[i];
                if (visualEffect != null && visualEffect.aliveParticleCount > 0)
                    return true;
            }

            return false;
        }

        internal static void SimulateEffects(ParticleSystem[] particleSystems, VisualEffect[] visualEffects, float deltaTime)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Simulate(deltaTime, true, false, false);
            }

            for (int i = 0; i < visualEffects.Length; i++)
            {
                VisualEffect visualEffect = visualEffects[i];
                if (visualEffect == null)
                    continue;

                visualEffect.Simulate(deltaTime);
            }
        }

    }
}

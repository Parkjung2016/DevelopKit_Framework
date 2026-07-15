using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    /// <summary>
    /// 프리뷰 인스턴스의 파티클과 VFX 캐시 및 에디터 시뮬레이션 시간을 관리합니다.
    /// </summary>
    internal sealed class MontagePreviewEffectSimulator
    {
        private const double CacheRefreshInterval = 0.05d;
        private const float MaxSimulationStep = 0.05f;

        private readonly List<ParticlePreviewEffect> particleEffects = new();
        private readonly List<VisualEffectPreviewEffect> visualEffects = new();
        private readonly List<ParticleSystem> particleBuffer = new();
        private readonly List<VisualEffect> visualEffectBuffer = new();
        private readonly HashSet<ParticleSystem> activeParticles = new();
        private readonly HashSet<VisualEffect> activeVisualEffects = new();
        private readonly HashSet<ParticleSystem> trackedParticles = new();
        private readonly HashSet<VisualEffect> trackedVisualEffects = new();

        private double lastCacheRefreshTime;

        public void Bind(GameObject root)
        {
            Reset();
            Refresh(root, true);
        }

        public void Simulate(GameObject root)
        {
            if (root == null)
                return;

            Refresh(root, false);
            double now = EditorApplication.timeSinceStartup;
            SimulateParticles(now);
            SimulateVisualEffects(now);
        }

        public void Reset()
        {
            particleEffects.Clear();
            visualEffects.Clear();
            particleBuffer.Clear();
            visualEffectBuffer.Clear();
            activeParticles.Clear();
            activeVisualEffects.Clear();
            trackedParticles.Clear();
            trackedVisualEffects.Clear();
            lastCacheRefreshTime = 0d;
        }

        private void Refresh(GameObject root, bool force)
        {
            if (root == null)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (!force && now - lastCacheRefreshTime < CacheRefreshInterval)
                return;

            lastCacheRefreshTime = now;
            particleBuffer.Clear();
            visualEffectBuffer.Clear();
            root.GetComponentsInChildren(true, particleBuffer);
            root.GetComponentsInChildren(true, visualEffectBuffer);

            activeParticles.Clear();
            activeVisualEffects.Clear();
            for (int i = 0; i < particleBuffer.Count; i++)
            {
                ParticleSystem particle = particleBuffer[i];
                if (particle != null)
                    activeParticles.Add(particle);
            }

            for (int i = 0; i < visualEffectBuffer.Count; i++)
            {
                VisualEffect visualEffect = visualEffectBuffer[i];
                if (visualEffect != null)
                    activeVisualEffects.Add(visualEffect);
            }

            RemoveMissingEffects();

            for (int i = 0; i < particleBuffer.Count; i++)
            {
                ParticleSystem particle = particleBuffer[i];
                if (particle != null && trackedParticles.Add(particle))
                    particleEffects.Add(new ParticlePreviewEffect(particle, now));
            }

            for (int i = 0; i < visualEffectBuffer.Count; i++)
            {
                VisualEffect visualEffect = visualEffectBuffer[i];
                if (visualEffect != null && trackedVisualEffects.Add(visualEffect))
                    visualEffects.Add(new VisualEffectPreviewEffect(visualEffect, now));
            }
        }

        private void RemoveMissingEffects()
        {
            for (int i = particleEffects.Count - 1; i >= 0; i--)
            {
                ParticleSystem particle = particleEffects[i].ParticleSystem;
                if (particle != null && activeParticles.Contains(particle))
                    continue;

                trackedParticles.Remove(particle);
                particleEffects.RemoveAt(i);
            }

            for (int i = visualEffects.Count - 1; i >= 0; i--)
            {
                VisualEffect visualEffect = visualEffects[i].VisualEffect;
                if (visualEffect != null && activeVisualEffects.Contains(visualEffect))
                    continue;

                trackedVisualEffects.Remove(visualEffect);
                visualEffects.RemoveAt(i);
            }
        }

        private void SimulateParticles(double now)
        {
            for (int i = particleEffects.Count - 1; i >= 0; i--)
            {
                ParticlePreviewEffect effect = particleEffects[i];
                if (effect.ParticleSystem == null)
                {
                    trackedParticles.Remove(effect.ParticleSystem);
                    particleEffects.RemoveAt(i);
                    continue;
                }

                float deltaTime = Mathf.Clamp((float)(now - effect.LastUpdateTime), 0f, MaxSimulationStep);
                effect.LastUpdateTime = now;
                if (deltaTime > 0f)
                    effect.ParticleSystem.Simulate(deltaTime, true, false, false);
            }
        }

        private void SimulateVisualEffects(double now)
        {
            for (int i = visualEffects.Count - 1; i >= 0; i--)
            {
                VisualEffectPreviewEffect effect = visualEffects[i];
                if (effect.VisualEffect == null)
                {
                    trackedVisualEffects.Remove(effect.VisualEffect);
                    visualEffects.RemoveAt(i);
                    continue;
                }

                float deltaTime = Mathf.Clamp((float)(now - effect.LastUpdateTime), 0f, MaxSimulationStep);
                effect.LastUpdateTime = now;
                if (deltaTime > 0f)
                    effect.VisualEffect.Simulate(deltaTime);
            }
        }

        private sealed class ParticlePreviewEffect
        {
            public ParticlePreviewEffect(ParticleSystem particleSystem, double lastUpdateTime)
            {
                ParticleSystem = particleSystem;
                LastUpdateTime = lastUpdateTime;
            }

            public ParticleSystem ParticleSystem { get; }
            public double LastUpdateTime { get; set; }
        }

        private sealed class VisualEffectPreviewEffect
        {
            public VisualEffectPreviewEffect(VisualEffect visualEffect, double lastUpdateTime)
            {
                VisualEffect = visualEffect;
                LastUpdateTime = lastUpdateTime;
            }

            public VisualEffect VisualEffect { get; }
            public double LastUpdateTime { get; set; }
        }
    }
}

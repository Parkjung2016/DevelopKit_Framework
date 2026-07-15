using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using Unity.Cinemachine;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Cinemachine
{
    internal sealed class MontageCinemachineCameraShakeProvider : IMontageCameraShakeProvider
    {
        private sealed class Session
        {
            public MontageCameraShakeSettings Settings;
            public CinemachineBasicMultiChannelPerlin Noise;
        }

        private sealed class NoiseState
        {
            public CinemachineBasicMultiChannelPerlin Noise;
            public NoiseSettings OriginalProfile;
            public float OriginalAmplitude;
            public float OriginalFrequency;
            public bool OriginalEnabled;
            public bool CreatedByProvider;
            public readonly HashSet<long> Requests = new();
        }

        private static readonly MontageCinemachineCameraShakeProvider Instance = new();
        private readonly Dictionary<long, Session> sessions = new();
        private readonly Dictionary<CinemachineBasicMultiChannelPerlin, NoiseState> noiseStates = new();
        private readonly Dictionary<GameObject, CinemachineImpulseSource> impulseSources = new();
        private readonly HashSet<CinemachineImpulseListener> createdImpulseListeners = new();
        private NoiseSettings generatedProfile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubsystem()
        {
            Instance.Reset();
            MontageCameraShakeRuntime.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => MontageCameraShakeRuntime.Provider = Instance;

        public void Set(long requestId, MontageCameraShakeSettings settings)
        {
            if (!sessions.TryGetValue(requestId, out Session session))
            {
                session = new Session();
                sessions.Add(requestId, session);
            }

            session.Settings = settings;
            CinemachineBasicMultiChannelPerlin activeNoise = FindActiveNoise(out bool created);
            if (session.Noise != activeNoise)
            {
                Detach(requestId, session.Noise);
                session.Noise = activeNoise;
                Attach(requestId, activeNoise, created);
            }

            Apply(activeNoise);
        }

        public void Remove(long requestId)
        {
            if (!sessions.TryGetValue(requestId, out Session session))
                return;

            Detach(requestId, session.Noise);
            sessions.Remove(requestId);
        }

        public void GenerateImpulse(GameObject sourceObject, MontageCameraImpulseSettings settings)
        {
            if (settings.Strength <= 0f)
                return;

            GameObject cameraObject =
                FindOutputCameraObject() ?? FindActiveCameraObject() ?? sourceObject;
            if (cameraObject == null)
                return;

            EnsureImpulseListener();
            CinemachineImpulseSource source = GetOrCreateImpulseSource(cameraObject);
            if (source == null)
                return;

            CinemachineImpulseDefinition definition =
                source.ImpulseDefinition ??= new CinemachineImpulseDefinition();
            definition.ImpulseChannel = 1;
            definition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            definition.ImpulseShape = ToCinemachineShape(settings.Shape);
            definition.ImpulseDuration = settings.Duration;
            source.DefaultVelocity = settings.Direction;
            source.GenerateImpulseAtPositionWithVelocity(
                cameraObject.transform.position,
                settings.Direction * settings.Strength);
        }

        public void Reset()
        {
            foreach (NoiseState state in noiseStates.Values)
                Restore(state);

            noiseStates.Clear();
            sessions.Clear();

            foreach (CinemachineImpulseSource source in impulseSources.Values)
                DestroyRuntimeObject(source);
            impulseSources.Clear();

            foreach (CinemachineImpulseListener listener in createdImpulseListeners)
                DestroyRuntimeObject(listener);
            createdImpulseListeners.Clear();

            if (generatedProfile != null)
            {
                DestroyRuntimeObject(generatedProfile);
                generatedProfile = null;
            }
        }

        private void Attach(long requestId, CinemachineBasicMultiChannelPerlin noise, bool created)
        {
            if (noise == null)
                return;

            if (!noiseStates.TryGetValue(noise, out NoiseState state))
            {
                state = new NoiseState
                {
                    Noise = noise,
                    OriginalProfile = noise.NoiseProfile,
                    OriginalAmplitude = noise.AmplitudeGain,
                    OriginalFrequency = noise.FrequencyGain,
                    OriginalEnabled = noise.enabled,
                    CreatedByProvider = created
                };
                noiseStates.Add(noise, state);
                noise.ReSeed();
            }

            state.Requests.Add(requestId);
        }

        private void Detach(long requestId, CinemachineBasicMultiChannelPerlin noise)
        {
            if (noise == null || !noiseStates.TryGetValue(noise, out NoiseState state))
                return;

            state.Requests.Remove(requestId);
            if (state.Requests.Count > 0)
            {
                Apply(noise);
                return;
            }

            noiseStates.Remove(noise);
            Restore(state);
        }

        private void Apply(CinemachineBasicMultiChannelPerlin noise)
        {
            if (noise == null || !noiseStates.TryGetValue(noise, out NoiseState state))
                return;

            float amplitude = 0f;
            float frequency = 0f;
            foreach (long requestId in state.Requests)
            {
                if (!sessions.TryGetValue(requestId, out Session session))
                    continue;

                amplitude += session.Settings.Amplitude;
                frequency = Mathf.Max(frequency, session.Settings.Frequency);
            }

            noise.NoiseProfile = GetOrCreateProfile();
            noise.AmplitudeGain = amplitude;
            noise.FrequencyGain = frequency;
            noise.enabled = amplitude > 0f && frequency > 0f;
        }

        private NoiseSettings GetOrCreateProfile()
        {
            if (generatedProfile != null)
                return generatedProfile;

            generatedProfile = ScriptableObject.CreateInstance<NoiseSettings>();
            generatedProfile.name = "Montage Camera Shake";
            generatedProfile.hideFlags = HideFlags.HideAndDontSave;
            generatedProfile.PositionNoise = new[]
            {
                CreateNoiseLayer(MontageCameraShakeSampler.PositionAmplitude)
            };
            generatedProfile.OrientationNoise = new[]
            {
                CreateNoiseLayer(MontageCameraShakeSampler.RotationAmplitude)
            };
            return generatedProfile;
        }

        private static NoiseSettings.TransformNoiseParams CreateNoiseLayer(Vector3 amplitude) => new()
        {
            X = CreateNoiseChannel(amplitude.x),
            Y = CreateNoiseChannel(amplitude.y),
            Z = CreateNoiseChannel(amplitude.z)
        };

        private static NoiseSettings.NoiseParams CreateNoiseChannel(float amplitude) => new()
        {
            Amplitude = amplitude,
            Frequency = 1f,
            Constant = false
        };

        private static CinemachineBasicMultiChannelPerlin FindActiveNoise(out bool created)
        {
            created = false;
            GameObject cameraObject = FindActiveCameraObject();
            if (cameraObject == null)
                return null;

            CinemachineBasicMultiChannelPerlin noise =
                cameraObject.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise != null)
                return noise;

            noise = cameraObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
            created = true;
            return noise;
        }

        private CinemachineImpulseSource GetOrCreateImpulseSource(GameObject sourceObject)
        {
            if (impulseSources.TryGetValue(sourceObject, out CinemachineImpulseSource source)
                && source != null)
            {
                return source;
            }

            source = sourceObject.AddComponent<CinemachineImpulseSource>();
            source.hideFlags = HideFlags.HideInInspector;
            impulseSources[sourceObject] = source;
            return source;
        }

        private void EnsureImpulseListener()
        {
            GameObject cameraObject = FindActiveCameraObject();
            if (cameraObject == null)
                return;

            CinemachineImpulseListener[] listeners =
                cameraObject.GetComponents<CinemachineImpulseListener>();
            for (int i = 0; i < listeners.Length; i++)
            {
                CinemachineImpulseListener listener = listeners[i];
                if (listener != null && (listener.ChannelMask & 1) != 0)
                    return;
            }

            CinemachineImpulseListener created =
                cameraObject.AddComponent<CinemachineImpulseListener>();
            created.ChannelMask = 1;
            created.Gain = 1f;
            created.UseCameraSpace = true;
            created.ApplyAfter = CinemachineCore.Stage.Noise;
            createdImpulseListeners.Add(created);
        }

        private static GameObject FindOutputCameraObject()
        {
            CinemachineBrain brain = FindActiveBrain();
            Camera outputCamera = brain?.OutputCamera;
            if (outputCamera == null)
                outputCamera = Camera.main;

            return outputCamera != null ? outputCamera.gameObject : null;
        }

        private static GameObject FindActiveCameraObject() =>
            (FindActiveBrain()?.ActiveVirtualCamera as CinemachineVirtualCameraBase)?.gameObject;

        private static CinemachineBrain FindActiveBrain()
        {
            Camera mainCamera = Camera.main;
            CinemachineBrain brain =
                mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
            if (brain == null && CinemachineBrain.ActiveBrainCount > 0)
                brain = CinemachineBrain.GetActiveBrain(0);

            return brain;
        }

        private static CinemachineImpulseDefinition.ImpulseShapes ToCinemachineShape(
            MontageCameraImpulseShape shape) => shape switch
        {
            MontageCameraImpulseShape.Recoil =>
                CinemachineImpulseDefinition.ImpulseShapes.Recoil,
            MontageCameraImpulseShape.Explosion =>
                CinemachineImpulseDefinition.ImpulseShapes.Explosion,
            MontageCameraImpulseShape.Rumble =>
                CinemachineImpulseDefinition.ImpulseShapes.Rumble,
            _ => CinemachineImpulseDefinition.ImpulseShapes.Bump
        };

        private static void Restore(NoiseState state)
        {
            if (state?.Noise == null)
                return;

            if (state.CreatedByProvider)
            {
                DestroyRuntimeObject(state.Noise);
                return;
            }

            state.Noise.NoiseProfile = state.OriginalProfile;
            state.Noise.AmplitudeGain = state.OriginalAmplitude;
            state.Noise.FrequencyGain = state.OriginalFrequency;
            state.Noise.enabled = state.OriginalEnabled;
        }

        private static void DestroyRuntimeObject(Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }
    }
}
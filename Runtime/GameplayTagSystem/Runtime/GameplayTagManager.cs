using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>게임플레이 태그 등록과 조회를 담당합니다.</summary>
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class GameplayTagManager
    {
        internal const int RetainedRemapGenerations = 8;

        private static readonly Dictionary<string, GameplayTagDefinition> tagDefinitionsByName =
            new(StringComparer.Ordinal);
        private static readonly Dictionary<int, int[]> indexRemapsByGeneration = new();

        private static GameplayTagDefinition[] tagDefinitions;
        private static GameplayTag[] tagLookUpTable;
        private static GameplayTag[] tags;
        private static bool isInitialized;
        private static bool hasBeenReloaded;
        private static int currentGeneration;

        public static bool HasBeenReloaded => hasBeenReloaded;

        internal static int Generation
        {
            get
            {
                InitializeIfNeeded();
                return currentGeneration;
            }
        }

        public static ReadOnlySpan<GameplayTag> GetAllTags()
        {
            InitializeIfNeeded();
            return tags;
        }

        public static GameplayTag GetTagFromRuntimeIndex(int runtimeIndex)
        {
            InitializeIfNeeded();
            if ((uint)runtimeIndex >= (uint)tagLookUpTable.Length)
                return GameplayTag.None;

            return tagLookUpTable[runtimeIndex];
        }

        internal static GameplayTagDefinition GetDefinitionFromRuntimeIndex(int runtimeIndex)
        {
            InitializeIfNeeded();
            return tagDefinitions[runtimeIndex];
        }

        internal static bool TryGetCurrentDefinition(string name, out GameplayTagDefinition definition)
        {
            InitializeIfNeeded();
            if (string.IsNullOrEmpty(name))
            {
                definition = null;
                return false;
            }

            return tagDefinitionsByName.TryGetValue(name, out definition);
        }

        internal static bool HasRuntimeIndexRemap(int sourceGeneration)
        {
            InitializeIfNeeded();
            if (sourceGeneration == currentGeneration)
                return true;
            if (sourceGeneration <= 0 || sourceGeneration > currentGeneration)
                return false;

            for (int generation = sourceGeneration; generation < currentGeneration; generation++)
            {
                if (!indexRemapsByGeneration.ContainsKey(generation))
                    return false;
            }

            return true;
        }
        internal static bool TryRemapRuntimeIndex(
            int sourceGeneration,
            int sourceIndex,
            out int currentIndex)
        {
            InitializeIfNeeded();
            currentIndex = sourceIndex;

            if (sourceGeneration <= 0 || sourceGeneration > currentGeneration)
                return sourceGeneration == currentGeneration;

            for (int generation = sourceGeneration; generation < currentGeneration; generation++)
            {
                if (!indexRemapsByGeneration.TryGetValue(generation, out int[] remap) ||
                    (uint)currentIndex >= (uint)remap.Length)
                {
                    currentIndex = -1;
                    return false;
                }

                currentIndex = remap[currentIndex];
                if (currentIndex < 0)
                    return false;
            }

            return true;
        }

        public static GameplayTag RequestTag(string name, bool logWarningIfNotFound = true)
        {
            InitializeIfNeeded();

            if (string.IsNullOrEmpty(name))
                return GameplayTag.None;

            if (tagDefinitionsByName.TryGetValue(name, out GameplayTagDefinition definition))
                return definition.Tag;

            if (logWarningIfNotFound)
                CDebug.LogWarning($"등록되지 않은 게임플레이 태그입니다: \"{name}\".");

            return GameplayTag.CreateInvalid(name);
        }

        public static bool RequestTag(string name, out GameplayTag tag)
        {
            tag = RequestTag(name, logWarningIfNotFound: false);
            return tag.IsValid;
        }

        public static void ReloadTags()
        {
            isInitialized = false;
            InitializeIfNeeded();
            hasBeenReloaded = true;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        private static void InitializeIfNeeded()
        {
            if (isInitialized)
                return;

            GameplayTagRegistrationContext context = new();

#if UNITY_EDITOR
            foreach (IGameplayTagSource source in AssemblyGameplayTagSource.GetAllSources())
                source.RegisterTags(context);

            foreach (IGameplayTagSource source in FileGameplayTagSource.GetAllFileSources())
                source.RegisterTags(context);
#else
            BuildGameplayTagSource buildSource = new();
            buildSource.RegisterTags(context);
#endif

            FinishInitialization(context);
        }

        private static void FinishInitialization(GameplayTagRegistrationContext context)
        {
            foreach (GameplayTagRegistrationError error in context.GetRegistrationErrors())
            {
                CDebug.LogError(
                    $"게임플레이 태그 등록 실패 \"{error.TagName}\": " +
                    $"{error.Message} (소스: {error.Source?.Name ?? "Unknown"})");
            }

            ApplyDefinitions(context.GenerateDefinitions());
            isInitialized = true;
        }

        private static void ApplyDefinitions(GameplayTagDefinition[] definitions)
        {
            GameplayTagDefinition[] previousDefinitions = tagDefinitions;
            int previousGeneration = currentGeneration;
            int nextGeneration = previousGeneration + 1;

            tagDefinitions = definitions ?? Array.Empty<GameplayTagDefinition>();
            tagDefinitionsByName.Clear();
            for (int i = 0; i < tagDefinitions.Length; i++)
            {
                GameplayTagDefinition definition = tagDefinitions[i];
                definition.SetGeneration(nextGeneration);
                tagDefinitionsByName[definition.TagName] = definition;
            }

            if (previousDefinitions != null && previousGeneration > 0)
            {
                int[] remap = new int[previousDefinitions.Length];
                for (int i = 0; i < previousDefinitions.Length; i++)
                {
                    string name = previousDefinitions[i].TagName;
                    remap[i] = tagDefinitionsByName.TryGetValue(name, out GameplayTagDefinition current)
                        ? current.RuntimeIndex
                        : -1;
                }

                indexRemapsByGeneration[previousGeneration] = remap;

                int expiredGeneration = nextGeneration - RetainedRemapGenerations - 1;
                if (expiredGeneration > 0)
                    indexRemapsByGeneration.Remove(expiredGeneration);
            }

            currentGeneration = nextGeneration;
            tagLookUpTable = new GameplayTag[tagDefinitions.Length];
            tags = new GameplayTag[Math.Max(0, tagDefinitions.Length - 1)];
            for (int i = 0; i < tagDefinitions.Length; i++)
            {
                GameplayTag tag = tagDefinitions[i].Tag;
                tagLookUpTable[i] = tag;
                if (i > 0)
                    tags[i - 1] = tag;
            }
        }

        internal static void InitializeForTests(params IGameplayTagSource[] sources)
        {
            ResetInitializationState();
            GameplayTagRegistrationContext context = new();
            foreach (IGameplayTagSource source in sources)
                source.RegisterTags(context);

            FinishInitialization(context);
        }

        internal static void ReloadForTests(params IGameplayTagSource[] sources)
        {
            GameplayTagRegistrationContext context = new();
            foreach (IGameplayTagSource source in sources)
                source.RegisterTags(context);

            FinishInitialization(context);
            isInitialized = true;
            hasBeenReloaded = true;
        }

        internal static void RestoreDefaultInitialization()
        {
            ResetInitializationState();
            InitializeIfNeeded();
        }

        private static void ResetInitializationState()
        {
            isInitialized = false;
            hasBeenReloaded = false;
            currentGeneration = 0;
            indexRemapsByGeneration.Clear();
            tagDefinitionsByName.Clear();
            tagDefinitions = null;
            tagLookUpTable = null;
            tags = null;
        }
    }
}
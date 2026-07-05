using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>
    /// 게임플레이 태그 등록·조회·리로드를 담당하는 정적 매니저입니다.
    /// </summary>
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public static partial class GameplayTagManager
    {
        private static Dictionary<string, GameplayTagDefinition> tagDefinitionsByName = new();
        private static GameplayTagDefinition[] tagDefinitions;
        private static GameplayTag[] tagLookUpTable;
        private static GameplayTag[] tags;
        private static bool isInitialized;
        private static bool hasBeenReloaded;

        /// <summary>에디터 또는 런타임에서 <see cref="ReloadTags"/>가 호출된 적이 있는지 여부입니다.</summary>
        public static bool HasBeenReloaded => hasBeenReloaded;

        /// <summary>등록된 모든 태그를 반환합니다. None 태그는 포함하지 않습니다.</summary>
        public static ReadOnlySpan<GameplayTag> GetAllTags()
        {
            InitializeIfNeeded();
            return new ReadOnlySpan<GameplayTag>(tags);
        }

        /// <summary>런타임 인덱스로 태그를 조회합니다.</summary>
        public static GameplayTag GetTagFromRuntimeIndex(int runtimeIndex)
        {
            if (tagLookUpTable == null || runtimeIndex < 0 || runtimeIndex >= tagLookUpTable.Length)
                return GameplayTag.None;

            return tagLookUpTable[runtimeIndex];
        }

        internal static GameplayTagDefinition GetDefinitionFromRuntimeIndex(int runtimeIndex)
        {
            return tagDefinitions[runtimeIndex];
        }

        /// <summary>이름으로 태그를 요청합니다. 없으면 유효하지 않은 태그를 반환합니다.</summary>
        public static GameplayTag RequestTag(string name, bool logWarningIfNotFound = true)
        {
            InitializeIfNeeded();

            if (string.IsNullOrEmpty(name))
                return GameplayTag.None;

            if (!TryGetDefinition(name, out GameplayTagDefinition definition))
            {
                if (logWarningIfNotFound)
                    CDebug.LogWarning($"등록되지 않은 태그 이름입니다: \"{name}\".");

                return GameplayTagDefinition.CreateInvalidDefinition(name).Tag;
            }

            return definition.Tag;
        }

        /// <summary>이름으로 태그를 요청하고, 존재 여부를 반환합니다.</summary>
        public static bool RequestTag(string name, out GameplayTag tag)
        {
            GameplayTag result = RequestTag(name, logWarningIfNotFound: false);
            tag = result;
            return tag.IsValid && !tag.IsNone;
        }

        /// <summary>등록된 태그를 다시 로드합니다. 에디터에서 JSON 변경 후 호출합니다.</summary>
        public static void ReloadTags()
        {
            isInitialized = false;
            tagDefinitionsByName.Clear();

            InitializeIfNeeded();

            hasBeenReloaded = true;

            if (Application.isPlaying)
            {
                CDebug.LogWarning(
                    "플레이 모드 중 게임플레이 태그가 다시 로드되었습니다. " +
                    "기존 태그 컨테이너는 예상대로 동작하지 않을 수 있습니다. 도메인 리로드가 필요합니다.");
            }
        }

        private static bool TryGetDefinition(string name, out GameplayTagDefinition definition)
        {
            return tagDefinitionsByName.TryGetValue(name, out definition);
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
            // GameplayTagAttribute가 있는 어셈블리에서 태그를 등록합니다.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                AssemblyGameplayTagSource source = new(assembly);
                source.RegisterTags(context);
            }

            // ProjectSettings/GameplayTags 아래 JSON 파일에서 태그를 등록합니다.
            foreach (IGameplayTagSource source in FileGameplayTagSource.GetAllFileSources())
                source.RegisterTags(context);
#else
            // StreamingAssets의 빌드 태그 파일에서 등록합니다.
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
            tagDefinitions = definitions;

            tagLookUpTable = tagDefinitions
                .Select(definition => definition.Tag)
                .ToArray();

            // None 태그를 제외한 목록을 만듭니다.
            tags = tagDefinitions
                .Select(definition => definition.Tag)
                .Skip(1)
                .ToArray();

            tagDefinitionsByName.Clear();
            foreach (GameplayTagDefinition definition in tagDefinitions)
                tagDefinitionsByName[definition.TagName] = definition;
        }

        /// <summary>유닛 테스트용으로 지정한 소스만으로 태그를 등록합니다.</summary>
        internal static void InitializeForTests(params IGameplayTagSource[] sources)
        {
            ResetInitializationState();

            GameplayTagRegistrationContext context = new();
            foreach (IGameplayTagSource source in sources)
                source.RegisterTags(context);

            FinishInitialization(context);
        }

        /// <summary>유닛 테스트 후 프로젝트 기본 태그 등록 상태로 되돌립니다.</summary>
        internal static void RestoreDefaultInitialization()
        {
            ResetInitializationState();
            InitializeIfNeeded();
        }

        private static void ResetInitializationState()
        {
            isInitialized = false;
            hasBeenReloaded = false;
            tagDefinitionsByName.Clear();
            tagDefinitions = null;
            tagLookUpTable = null;
            tags = null;
        }
    }
}

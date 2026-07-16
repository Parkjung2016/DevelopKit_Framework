using System;
using System.Collections.Generic;
using System.Reflection;
using PJDev.DevelopKit.BasicTemplate.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>Assembly의 GameplayTagAttribute를 최초 한 번만 읽어 등록합니다.</summary>
    internal sealed class AssemblyGameplayTagSource : IGameplayTagSource
    {
        private readonly struct RegisteredTag
        {
            public readonly string Name;
            public readonly string Description;
            public readonly GameplayTagFlags Flags;

            public RegisteredTag(GameplayTagAttribute attribute)
            {
                Name = attribute.TagName;
                Description = attribute.Description;
                Flags = attribute.Flags;
            }
        }

        private static readonly object Sync = new();
        private static readonly Dictionary<Assembly, AssemblyGameplayTagSource> SourcesByAssembly = new();
        private static AssemblyGameplayTagSource[] sourceSnapshot = Array.Empty<AssemblyGameplayTagSource>();
        private static bool initialized;

        private readonly Assembly assembly;
        private readonly RegisteredTag[] tags;

        public string Name => assembly.GetName().Name;

        static AssemblyGameplayTagSource()
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
        }

        private AssemblyGameplayTagSource(Assembly assembly)
        {
            this.assembly = assembly;
            tags = ReadTags(assembly);
        }

        public static IReadOnlyList<AssemblyGameplayTagSource> GetAllSources()
        {
            lock (Sync)
            {
                if (!initialized)
                {
                    initialized = true;
                    Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < assemblies.Length; i++)
                        AddAssembly(assemblies[i]);

                    RebuildSnapshot();
                }

                return sourceSnapshot;
            }
        }

        public void RegisterTags(GameplayTagRegistrationContext context)
        {
            for (int i = 0; i < tags.Length; i++)
            {
                RegisteredTag tag = tags[i];
                context.RegisterTag(tag.Name, tag.Description, tag.Flags, this);
            }
        }

        private static RegisteredTag[] ReadTags(Assembly assembly)
        {
            try
            {
                object[] attributes = assembly.GetCustomAttributes(typeof(GameplayTagAttribute), inherit: false);
                RegisteredTag[] result = new RegisteredTag[attributes.Length];
                for (int i = 0; i < attributes.Length; i++)
                    result[i] = new RegisteredTag((GameplayTagAttribute)attributes[i]);

                return result;
            }
            catch (ReflectionTypeLoadException ex)
            {
                for (int i = 0; i < ex.LoaderExceptions.Length; i++)
                    CDebug.LogError($"Assembly '{assembly.FullName}' 태그 로드 실패: {ex.LoaderExceptions[i].Message}");
            }
            catch (Exception ex)
            {
                CDebug.LogError($"Assembly '{assembly.FullName}' 태그 조회 실패: {ex.Message}");
            }

            return Array.Empty<RegisteredTag>();
        }

        private static void OnAssemblyLoaded(object _, AssemblyLoadEventArgs args)
        {
            lock (Sync)
            {
                if (!initialized || SourcesByAssembly.ContainsKey(args.LoadedAssembly))
                    return;

                AddAssembly(args.LoadedAssembly);
                RebuildSnapshot();
            }
        }

        private static void AddAssembly(Assembly assembly)
        {
            if (assembly == null || SourcesByAssembly.ContainsKey(assembly))
                return;

            SourcesByAssembly.Add(assembly, new AssemblyGameplayTagSource(assembly));
        }

        private static void RebuildSnapshot()
        {
            sourceSnapshot = new AssemblyGameplayTagSource[SourcesByAssembly.Count];
            SourcesByAssembly.Values.CopyTo(sourceSnapshot, 0);
            Array.Sort(sourceSnapshot, static (a, b) =>
                string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }
    }
}
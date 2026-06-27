using System;
using System.Reflection;
using PJDev.DevelopKit.BasicTemplate.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    internal sealed class AssemblyGameplayTagSource : IGameplayTagSource
    {
        public string Name => assembly.GetName().Name;

        private Assembly assembly;

        public AssemblyGameplayTagSource(Assembly assembly)
        {
            this.assembly = assembly;
        }

        public void RegisterTags(GameplayTagRegistrationContext context)
        {
            try
            {
                foreach (GameplayTagAttribute attribute in assembly.GetCustomAttributes<GameplayTagAttribute>())
                    context.RegisterTag(attribute.TagName, attribute.Description, attribute.Flags, this);
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (Exception loaderException in ex.LoaderExceptions)
                    CDebug.LogError($"Failed to load type from assembly '{assembly.FullName}': {loaderException.Message}");
            }
            catch (Exception ex)
            {
                CDebug.LogError($"Failed to fetch tags from assembly '{assembly.FullName}': {ex.Message}");
            }
        }
    }
}

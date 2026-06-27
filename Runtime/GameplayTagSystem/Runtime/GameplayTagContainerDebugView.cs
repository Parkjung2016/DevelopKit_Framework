using System.Diagnostics;
using System.Linq;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    internal sealed class GameplayTagContainerDebugView
    {
        [DebuggerDisplay("{DebuggerDisplay,nq}")]
        public struct Tag
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private IGameplayTagContainer container { get; set; }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private GameplayTag tag;

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly string DebuggerDisplay
            {
                get
                {
                    string name = tag.Name;

                    if (container is IGameplayTagCountContainer countContainer)
                    {
                        int count = countContainer.GetTagCount(tag);
                        int explicitCount = countContainer.GetExplicitTagCount(tag);

                        return $"{name} (Explicit: {explicitCount}, Total: {count})";
                    }

                    bool isExplicit = container.HasTagExact(tag);
                    return isExplicit ? $"{name} (Explicit)" : name;
                }
            }

            public Tag(IGameplayTagContainer container, GameplayTag tag)
            {
                this.container = container;
                this.tag = tag;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IGameplayTagContainer Container { get; set; }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public Tag[] Tags
        {
            get => Container.GetTags()
                  .Select(tag => new Tag(Container, tag))
                  .ToArray();
        }

        public GameplayTagContainerDebugView(IGameplayTagContainer container)
        {
            Container = container;
        }
    }
}

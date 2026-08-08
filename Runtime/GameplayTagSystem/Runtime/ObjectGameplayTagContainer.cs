using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>게임 오브젝트에 런타임 태그 컨테이너를 제공합니다.</summary>
    [DefaultExecutionOrder(-99999)]
    public sealed class ObjectGameplayTagContainer : MonoBehaviour
    {
        [SerializeField] private GameplayTagContainer persistentTags;

        private GameplayTagCountContainer container;

        /// <summary>이 오브젝트가 보유한 런타임 태그 컨테이너입니다.</summary>
        public GameplayTagCountContainer Container => container ??= CreateContainer();

        private void Awake()
        {
            _ = Container;
        }

        public static implicit operator GameplayTagCountContainer(ObjectGameplayTagContainer component) =>
            component?.Container;

        private GameplayTagCountContainer CreateContainer()
        {
            var result = new GameplayTagCountContainer();
            if (persistentTags != null)
                result.AddTags(persistentTags);

            return result;
        }
    }
}
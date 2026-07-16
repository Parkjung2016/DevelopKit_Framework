using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>게임 오브젝트에 런타임 태그 컨테이너를 제공합니다.</summary>
    [DefaultExecutionOrder(-99999)]
    public sealed class ObjectGameplayTagContainer : MonoBehaviour
    {
        /// <summary>이 오브젝트가 보유한 런타임 태그 컨테이너입니다.</summary>
        public GameplayTagCountContainer Container => container;

        [SerializeField]
        private GameplayTagContainer persistentTags;

        private GameplayTagCountContainer container;

        private void Awake()
        {
            container = new GameplayTagCountContainer();
            if (persistentTags != null)
                container.AddTags(persistentTags);
        }

        public static implicit operator GameplayTagCountContainer(ObjectGameplayTagContainer component)
        {
            return component?.Container;
        }
    }
}
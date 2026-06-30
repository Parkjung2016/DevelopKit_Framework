using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    [DefaultExecutionOrder(-99999)]
    /// <summary>게임 오브젝트에 태그 개수 컨테이너를 부착하는 컴포넌트입니다.</summary>
    public sealed class ObjectGameplayTagContainer : MonoBehaviour
    {
        /// <summary>런타임 태그 개수 컨테이너입니다.</summary>
        public GameplayTagCountContainer GameplayTagContainer => gameplayTagContainer;

        [SerializeField]
        private GameplayTagContainer persistentTags;

        private GameplayTagCountContainer gameplayTagContainer;

        private void Awake()
        {
            gameplayTagContainer = new GameplayTagCountContainer();
            gameplayTagContainer.AddTags(persistentTags);
        }

        public static implicit operator GameplayTagCountContainer(ObjectGameplayTagContainer container)
        {
            return container.GameplayTagContainer;
        }
    }
}

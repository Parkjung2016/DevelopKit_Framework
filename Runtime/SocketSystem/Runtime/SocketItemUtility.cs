using UnityEngine;
using Object = UnityEngine.Object;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    public static class SocketItemUtility
    {
        /// <summary>
        /// GameObject에 붙은 <see cref="ISocketItem"/>을 반환하고, 없으면 <see cref="GameObjectSocketItem"/>으로 래핑합니다.
        /// </summary>
        public static ISocketItem FromGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            if (gameObject.TryGetComponent(out ISocketItem existing))
                return existing;

            return new GameObjectSocketItem(gameObject);
        }

        /// <summary>
        /// <see cref="ISocketItem"/> 구현 컴포넌트, 같은 GameObject, 루트 순으로 찾고 없으면 <see cref="FromGameObject"/>로 래핑합니다.
        /// </summary>
        public static ISocketItem FromComponent(Component component)
        {
            if (component == null)
                return null;

            if (component is ISocketItem direct)
                return direct;

            if (component.TryGetComponent(out ISocketItem onSelf))
                return onSelf;

            Transform root = component.transform.root;
            if (root != component.transform && root.TryGetComponent(out ISocketItem onRoot))
                return onRoot;

            return FromGameObject(component.gameObject);
        }

        public static void ReleaseDestroy(ISocketItem socketItem)
        {
            if (socketItem?.SocketTransform == null)
                return;

            Object.Destroy(socketItem.SocketTransform.gameObject);
        }
    }
}

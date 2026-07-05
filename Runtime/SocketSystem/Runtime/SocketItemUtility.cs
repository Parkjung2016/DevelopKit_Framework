using UnityEngine;
using Object = UnityEngine.Object;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    public static class SocketItemUtility
    {
        /// <summary>
        /// <see cref="ISocketItem"/> 구현 컴포넌트, 같은 GameObject, 루트 순으로 찾고 없으면 <see cref="GameObjectSocketItem"/>으로 래핑합니다.
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

            return new GameObjectSocketItem(component.gameObject);
        }

        public static void ReleaseDestroy(ISocketItem socketItem)
        {
            if (socketItem?.SocketTransform == null)
                return;

            Object.Destroy(socketItem.SocketTransform.gameObject);
        }
    }
}

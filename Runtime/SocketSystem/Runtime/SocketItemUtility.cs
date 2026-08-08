using UnityEngine;
using Object = UnityEngine.Object;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    public static class SocketItemUtility
    {
        /// <summary>GameObject의 ISocketItem을 찾고, 없으면 가벼운 래퍼를 만듭니다.</summary>
        public static ISocketItem FromGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            return gameObject.TryGetComponent(out ISocketItem existing)
                ? existing
                : new GameObjectSocketItem(gameObject);
        }

        /// <summary>컴포넌트 자신과 루트에서 ISocketItem을 찾습니다.</summary>
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

        public static void Destroy(ISocketItem socketItem)
        {
            if (socketItem?.SocketTransform == null)
                return;

            GameObject gameObject = socketItem.SocketTransform.gameObject;
            if (Application.isPlaying)
                Object.Destroy(gameObject);
            else
                Object.DestroyImmediate(gameObject);
        }
    }
}
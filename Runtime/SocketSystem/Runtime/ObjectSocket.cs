using UnityEngine;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    public class ObjectSocket : MonoBehaviour
    {
        private ISocketItem item;

        public bool HasItem => item != null;

        public void ChangeItem(ISocketItem item, Vector3 localPosition, Quaternion localRotation)
        {
            ChangeItem(item, localPosition, localRotation, Vector3.one);
        }

        public void ChangeItem(
            ISocketItem item,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            if (item == null || item.SocketTransform == null)
            {
                ClearItem();
                return;
            }

            item.SocketTransform.SetParent(transform);
            item.SocketTransform.SetLocalPositionAndRotation(localPosition, localRotation);
            item.SocketTransform.localScale = localScale == default ? Vector3.one : localScale;

            this.item = item;
        }

        public void ClearItem() => item = null;

        public bool TryGetItem<T>(out T result) where T : class, ISocketItem
        {
            if (item == null)
            {
                result = null;
                return false;
            }

            result = item as T;
            return result != null;
        }
    }
}

using UnityEngine;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    public class ObjectSocket : MonoBehaviour
    {
        private ISocketItem item;

        public void ChangeItem(ISocketItem item, Vector3 position, Quaternion rotation)
        {
            item.SocketTransform.SetParent(transform);
            item.SocketTransform.SetLocalPositionAndRotation(position, rotation);
            item.SocketTransform.localScale = Vector3.one;

            this.item = item;
        }

        public bool TryGetItem<T>(out T result) where T : class, ISocketItem
        {
            if (item == null)
            {
                result = null;
                return false;
            }

            result = item as T;
            return true;
        }
    }
}

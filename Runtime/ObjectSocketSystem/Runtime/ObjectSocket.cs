using UnityEngine;

namespace Skddkkkk.DevelopKit.Framework.ObjectSocketSystem.Runtime
{
    public class ObjectSocket : MonoBehaviour
    {
        private ISocketItem _item;

        public void ChangeItem(ISocketItem item, Vector3 position, Quaternion rotation)
        {
            item.SocketTransform.SetParent(transform);
            item.SocketTransform.SetLocalPositionAndRotation(position, rotation);
            item.SocketTransform.localScale = Vector3.one;

            _item = item;
        }

        public T GetItem<T>() where T : class, ISocketItem
        {
            return _item as T;
        }
    }
}
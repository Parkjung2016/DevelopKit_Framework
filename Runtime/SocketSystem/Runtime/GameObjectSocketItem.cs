using UnityEngine;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    /// <summary><see cref="GameObject"/>을 <see cref="ObjectSocket.ChangeItem"/>에 연결합니다.</summary>
    public sealed class GameObjectSocketItem : ISocketItem
    {
        public GameObjectSocketItem(GameObject gameObject)
        {
            GameObject = gameObject;
        }

        public GameObject GameObject { get; }

        public Transform SocketTransform => GameObject != null ? GameObject.transform : null;
    }
}

using UnityEngine;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    /// <summary>스폰된 <see cref="GameObject"/>를 <see cref="ObjectSocket.ChangeItem"/>에 연결합니다. 프리팹에 <see cref="ISocketItem"/> 컴포넌트가 있으면 그쪽을 우선 사용하세요.</summary>
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

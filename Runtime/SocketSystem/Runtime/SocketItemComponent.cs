using UnityEngine;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    /// <summary>프리팹에 붙여 <see cref="ObjectSocket.ChangeItem"/>에 연결하는 기본 <see cref="ISocketItem"/>입니다.</summary>
    public abstract class SocketItemComponent : MonoBehaviour, ISocketItem
    {
        public Transform SocketTransform => transform;
    }
}

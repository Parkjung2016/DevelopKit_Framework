using UnityEngine;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    /// <summary>프리팹을 소켓에 연결할 때 상속하는 기본 컴포넌트입니다.</summary>
    public abstract class SocketItemComponent : MonoBehaviour, ISocketItem
    {
        public Transform SocketTransform => transform;
    }
}
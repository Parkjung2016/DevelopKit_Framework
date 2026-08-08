using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    /// <summary>일반 GameObject를 소켓 아이템으로 연결합니다.</summary>
    public sealed class GameObjectSocketItem : ISocketItem
    {
        public GameObjectSocketItem(GameObject gameObject)
        {
            GameObject = gameObject != null
                ? gameObject
                : throw new ArgumentNullException(nameof(gameObject));
        }

        public GameObject GameObject { get; }

        public Transform SocketTransform => GameObject.transform;
    }
}
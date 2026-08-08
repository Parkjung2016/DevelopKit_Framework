using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SocketSystem.Runtime
{
    [DefaultExecutionOrder(-10000)]
    public class ObjectSocketSystem : MonoBehaviour
    {
        private readonly Dictionary<string, ObjectSocket> sockets = new(StringComparer.Ordinal);
        private readonly List<ObjectSocket> socketBuffer = new();

        public int SocketCount => sockets.Count;

        private void Awake() => RebuildSocketCache();

        public void RebuildSocketCache()
        {
            sockets.Clear();
            socketBuffer.Clear();
            GetComponentsInChildren(true, socketBuffer);

            for (int i = 0; i < socketBuffer.Count; i++)
            {
                ObjectSocket socket = socketBuffer[i];
                string socketKey = socket.name;
                if (string.IsNullOrEmpty(socketKey))
                    continue;

                if (!sockets.TryAdd(socketKey, socket))
                    Debug.LogWarning($"Duplicate socket key '{socketKey}'.", socket);
            }
        }

        public bool TryGetSocket(string socketKey, out ObjectSocket socket)
        {
            if (string.IsNullOrEmpty(socketKey))
            {
                socket = null;
                return false;
            }

            return sockets.TryGetValue(socketKey, out socket);
        }
    }
}
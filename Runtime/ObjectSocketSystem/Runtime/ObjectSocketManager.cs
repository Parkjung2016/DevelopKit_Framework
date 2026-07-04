using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.ObjectSocketSystem.Runtime
{
    [DefaultExecutionOrder(-10000)]
    public class ObjectSocketManager : MonoBehaviour
    {
        private readonly Dictionary<string, ObjectSocket> sockets = new();

        private void Awake() => RebuildSocketCache();

        public void RebuildSocketCache()
        {
            sockets.Clear();
            ObjectSocket[] characterSockets = GetComponentsInChildren<ObjectSocket>(true);

            for (int i = 0; i < characterSockets.Length; i++)
            {
                ObjectSocket socket = characterSockets[i];
                string socketKey = socket.name;

                if (string.IsNullOrEmpty(socketKey))
                    continue;

                if (!sockets.TryAdd(socketKey, socket))
                    Debug.LogWarning($"{socket.name} is already registered!");
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

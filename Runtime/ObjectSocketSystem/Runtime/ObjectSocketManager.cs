using System.Collections.Generic;
using UnityEngine;

namespace Skddkkkk.DevelopKit.Framework.ObjectSocketSystem.Runtime
{
    [DefaultExecutionOrder(-10000)]
    public class ObjectSocketManager : MonoBehaviour
    {
        private readonly Dictionary<string, ObjectSocket> _sockets = new();

        private void Awake()
        {
            InitializeSocketCache();
        }

        private void InitializeSocketCache()
        {
            _sockets.Clear();
            ObjectSocket[] characterSockets = GetComponentsInChildren<ObjectSocket>();
            for (int i = 0; i < characterSockets.Length; i++)
            {
                ObjectSocket socket = characterSockets[i];
                if (!_sockets.TryAdd(socket.name.Replace("Socket_", ""), socket))
                {
                    Debug.LogWarning($"{socket.name} is already registered!");
                }
            }
        }

        public ObjectSocket GetSocket(string socketName) => _sockets[socketName];
    }
}
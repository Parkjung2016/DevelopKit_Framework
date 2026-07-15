using System;
using System.Text;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    /// <summary>Unity JsonUtility를 사용하는 기본 세이브 직렬화 구현입니다.</summary>
    public sealed class JsonSaveSerializer : ISaveSerializer
    {
        public static readonly JsonSaveSerializer Instance = new();

        private JsonSaveSerializer()
        {
        }

        public byte[] Serialize<T>(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(value, false));
        }

        public T Deserialize<T>(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Save data is empty.", nameof(data));

            T value = JsonUtility.FromJson<T>(Encoding.UTF8.GetString(data));
            if (value == null)
                throw new InvalidOperationException($"Could not deserialize {typeof(T).Name}.");

            return value;
        }
    }
}
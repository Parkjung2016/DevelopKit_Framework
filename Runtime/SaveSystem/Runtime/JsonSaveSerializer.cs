using System;
using System.Text;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    /// <summary>Unity <see cref="JsonUtility"/> 기반 직렬화. 대상 타입에 [Serializable]이 필요합니다.</summary>
    public sealed class JsonSaveSerializer : ISaveSerializer
    {
        public static readonly JsonSaveSerializer Instance = new();

        public byte[] Serialize<T>(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            string json = JsonUtility.ToJson(value, false);
            return Encoding.UTF8.GetBytes(json);
        }

        public bool TryDeserialize<T>(byte[] data, out T value)
        {
            value = default;
            if (data == null || data.Length == 0)
                return false;

            try
            {
                string json = Encoding.UTF8.GetString(data);
                value = JsonUtility.FromJson<T>(json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

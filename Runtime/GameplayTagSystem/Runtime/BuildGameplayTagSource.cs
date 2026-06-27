using System;
using System.IO;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;
using UnityEngine.Networking;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    internal sealed class BuildGameplayTagSource : IGameplayTagSource
    {
        public string Name => "Build";

        public void RegisterTags(GameplayTagRegistrationContext context)
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "GameplayTags");
                byte[] data = LoadData(path);

                using MemoryStream memoryStream = new(data);
                using BinaryReader reader = new(memoryStream);

                while (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    string tagName = reader.ReadString();
                    context.RegisterTag(tagName, string.Empty, GameplayTagFlags.None, this);
                }
            }
            catch (Exception e)
            {
                CDebug.LogError($"Failed to load gameplay tags from StreamingAssets: {e.Message}");
            }
        }

        private byte[] LoadData(string dataPath)
        {
            return Application.platform switch
            {
                RuntimePlatform.WebGLPlayer => LoadDataFromWebRequest(dataPath),
                RuntimePlatform.Android => LoadDataFromWebRequest(dataPath),
                _ => LoadDataFromFile(dataPath),
            };
        }

        private byte[] LoadDataFromWebRequest(string dataPath)
        {
            using UnityWebRequest request = UnityWebRequest.Get(dataPath);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) { }

#if UNITY_2020_2_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                if (request.responseCode == 404)
                    CDebug.LogError($"GameplayTags file not found at path: {dataPath}");
                else
                    CDebug.LogError($"Failed to load gameplay tags from StreamingAssets: {request.error}");

                return Array.Empty<byte>();
            }

            return request.downloadHandler.data;
        }

        private byte[] LoadDataFromFile(string path)
        {
            if (!File.Exists(path))
            {
                CDebug.LogError($"GameplayTags file not found at path: {path}");
                return Array.Empty<byte>();
            }
            return File.ReadAllBytes(path);
        }
    }
}

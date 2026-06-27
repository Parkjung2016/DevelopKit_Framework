using System.IO;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using UnityEditor.Build;

namespace PJDev.DevelopKit.Framework.Editors.GameplayTagSystem
{
    /// <summary>빌드 시 리프 태그 이름을 StreamingAssets 바이너리로 보냅니다.</summary>
    public sealed class BuildTags : BuildPlayerProcessor
    {
        public override int callbackOrder => 0;

        /// <inheritdoc />
        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            string customFolder = Path.Combine(Path.GetTempPath(), "PJDev", "GameplayTags", "StreamingAssets");

            if (Directory.Exists(customFolder))
                Directory.Delete(customFolder, true);

            Directory.CreateDirectory(customFolder);

            GameplayTagManager.ReloadTags();

            string filePath = Path.Combine(customFolder, "GameplayTags");
            using FileStream file = File.Create(filePath);
            using BinaryWriter writer = new(file);

            foreach (GameplayTag tag in GameplayTagManager.GetAllTags())
            {
                if (!tag.IsLeaf)
                    continue;

                writer.Write(tag.Name);
            }

            buildPlayerContext.AddAdditionalPathToStreamingAssets(customFolder);
        }
    }
}

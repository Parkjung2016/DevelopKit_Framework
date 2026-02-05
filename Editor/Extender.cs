using Skddkkkk.DevelopKit.Editors;
using UnityEditor;

namespace Skddkkkk.DevelopKit.Framework.Editors
{
    [InitializeOnLoad]
    public class Extender : Editor
    {
        private const string GAMEPLAYTAGS_NAME = "com.bandoware.gameplaytags";

        private const string GAMEPLAYTAGS_URL =
            "https://github.com/BandoWare/GameplayTags.git";

        static Extender()
        {
            bool checkGamePlayTagsInstalled = DevelopKitEditorUtility.CheckPackageInstalled(GAMEPLAYTAGS_NAME);
            if (!checkGamePlayTagsInstalled)
            {
               DevelopKitEditorUtility.AddPackage(GAMEPLAYTAGS_NAME, GAMEPLAYTAGS_URL);
            }
        }
        
    }
}
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.InventorySystem
{
    internal static class InventoryTestAutomation
    {
        private const string BaseMenu = "Tools/InventorySystem/Tests/";

        [MenuItem(BaseMenu + "Run EditMode")]
        private static void RunEditMode() => Run(TestMode.EditMode);

        private static void Run(TestMode mode)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = mode,
                assemblyNames = new[] { "PJDev.DevelopKit.Framework.InventorySystem.Tests" }
            };

            api.Execute(new ExecutionSettings(filter)
            {
                runSynchronously = false
            });

            Debug.Log($"[InventorySystem] {mode} tests started.");
        }
    }
}

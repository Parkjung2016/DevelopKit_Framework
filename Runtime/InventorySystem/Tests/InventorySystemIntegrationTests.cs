using NUnit.Framework;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    [TestFixture]
    public sealed class InventorySystemIntegrationTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
                Object.DestroyImmediate(host);
        }

        [Test]
        public void Init_FromSetup_RegistersAllContainerConfigs()
        {
            InventorySetupSO setup = ScriptableObject.CreateInstance<InventorySetupSO>();
            InventoryConfigSO mainConfig = ScriptableObject.CreateInstance<InventoryConfigSO>();
            mainConfig.ContainerId = "main";
            mainConfig.SlotCount = 8;

            InventoryConfigSO equipmentConfig = ScriptableObject.CreateInstance<InventoryConfigSO>();
            equipmentConfig.ContainerId = "equipment";
            equipmentConfig.Kind = (ContainerKind)InventoryTestValues.EquipmentKind;
            equipmentConfig.SlotCount = 4;

            setup.ContainerConfigs = new[] { mainConfig, equipmentConfig };

            host = new GameObject("InventorySystemTest");
            var system = host.AddComponent<global::PJDev.DevelopKit.Framework.InventorySystem.Runtime.InventorySystem>();
            system.Init(null, setup);

            Assert.IsNotNull(system.Group);
            Assert.AreEqual(2, system.Group.Containers.Count);
            Assert.IsTrue(system.TryGetContainer("main", out IInventoryContainer main));
            Assert.IsTrue(system.TryGetContainer("equipment", out IInventoryContainer equipment));
            Assert.AreEqual(8, main.SlotCount);
            Assert.AreEqual(4, equipment.SlotCount);
            Assert.AreEqual("main", system.ContainerId);
        }

        [Test]
        public void Init_FromSetupWithEmptyConfigs_UsesDefaultMainContainer()
        {
            InventorySetupSO setup = ScriptableObject.CreateInstance<InventorySetupSO>();
            setup.ContainerConfigs = System.Array.Empty<InventoryConfigSO>();

            host = new GameObject("InventorySystemTest");
            var system = host.AddComponent<global::PJDev.DevelopKit.Framework.InventorySystem.Runtime.InventorySystem>();
            system.Init(null, setup);

            Assert.AreEqual(1, system.Group.Containers.Count);
            Assert.AreEqual("main", system.ContainerId);
            Assert.AreEqual(20, system.SlotCount);
        }
    }
}

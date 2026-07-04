using PJDev.DevelopKit.Framework.ObjectSocketSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>캐릭터에 붙여 장비 슬롯별 소켓 매핑과 <see cref="EquipmentVisualController"/>를 설정합니다.</summary>
    public sealed class ObjectEquipmentVisualHost : MonoBehaviour
    {
        [SerializeField] private ObjectSocketManager socketManager;
        [SerializeField] private EquipmentVisualSlotSocketBinding[] slotSocketBindings = new EquipmentVisualSlotSocketBinding[0];

        private EquipmentVisualController controller;

        public EquipmentVisualController Controller => controller;

        public void Initialize(
            EquipmentSetupSO setup,
            IEquipmentVisualResolver resolver,
            IEquipmentVisualSpawner spawner)
        {
            if (socketManager == null)
                socketManager = GetComponent<ObjectSocketManager>();

            controller?.Dispose();
            controller = new EquipmentVisualController(socketManager, slotSocketBindings);
            controller.Initialize(setup, resolver, spawner);
        }

        private void OnDestroy() => controller?.Dispose();
    }
}

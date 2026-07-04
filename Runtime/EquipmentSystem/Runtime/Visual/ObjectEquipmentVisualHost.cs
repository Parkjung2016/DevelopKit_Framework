using PJDev.DevelopKit.Framework.SocketSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>캐릭터에 붙여 장비 슬롯별 소켓 매핑과 <see cref="EquipmentVisualController"/>를 설정합니다.</summary>
    public sealed class ObjectEquipmentVisualHost : MonoBehaviour
    {
        [SerializeField] private ObjectSocketSystem socketSystem;
        [Tooltip("Inspector에서 슬롯 카테고리 표시·바인딩 개수 맞추기용. Initialize에 넘기는 setup과 동일하게 두세요.")]
        [SerializeField] private EquipmentSetupSO slotLayoutGuide;
        [SerializeField] private EquipmentVisualSlotSocketBinding[] slotSocketBindings = new EquipmentVisualSlotSocketBinding[0];

        private EquipmentVisualController controller;

        public EquipmentVisualController Controller => controller;

        public void Initialize(
            EquipmentSetupSO setup,
            IEquipmentVisualResolver resolver,
            IEquipmentVisualSpawner spawner)
        {
            controller?.Dispose();
            controller = new EquipmentVisualController(socketSystem, slotSocketBindings);
            controller.Initialize(setup, resolver, spawner);
        }

        private void OnDestroy() => controller?.Dispose();
    }
}

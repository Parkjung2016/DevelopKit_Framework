using PJDev.DevelopKit.Framework.SocketSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>캐릭터의 장비 슬롯과 소켓을 연결하고 장비 비주얼을 관리합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class ObjectEquipmentVisualHost : MonoBehaviour
    {
        [SerializeField] private ObjectSocketSystem socketSystem = null;
        [Tooltip("인스펙터에서 슬롯 카테고리와 소켓 연결 상태를 확인할 때 사용합니다.")]
        [SerializeField] private EquipmentSetupSO slotLayoutGuide = null;
        [SerializeField] private EquipmentVisualSlotSocketBinding[] slotSocketBindings =
            new EquipmentVisualSlotSocketBinding[0];

        private EquipmentVisualController controller;

        public ObjectSocketSystem SocketSystem => socketSystem;
        public EquipmentSetupSO SlotLayoutGuide => slotLayoutGuide;
        public EquipmentVisualController Controller => controller;
        public bool IsInitialized => controller != null;

        public void Initialize(
            EquipmentSetupSO setup,
            IEquipmentVisualResolver resolver,
            IEquipmentVisualSpawner spawner)
        {
            Clear();

            if (socketSystem == null)
            {
                Debug.LogWarning("장비 비주얼을 초기화하려면 ObjectSocketSystem이 필요합니다.", this);
                return;
            }

            if (setup == null)
            {
                Debug.LogWarning("장비 비주얼을 초기화하려면 EquipmentSetupSO가 필요합니다.", this);
                return;
            }

            controller = new EquipmentVisualController(socketSystem, slotSocketBindings);
            controller.Initialize(setup, resolver, spawner);
        }

        public void Clear()
        {
            controller?.Dispose();
            controller = null;
        }

        private void OnDestroy() => Clear();
    }
}
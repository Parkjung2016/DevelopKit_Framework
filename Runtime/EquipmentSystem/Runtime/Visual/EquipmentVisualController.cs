using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using PJDev.DevelopKit.Framework.SocketSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    /// <summary>슬롯별 장비 비주얼의 생성, 교체, 해제를 관리합니다.</summary>
    public sealed class EquipmentVisualController : IDisposable
    {
        private readonly ObjectSocketSystem socketSystem;
        private readonly Dictionary<int, string> socketKeyByEquipSlot = new();
        private readonly Dictionary<int, SlotVisualState> slotStates = new();

        private string[] slotCategories = Array.Empty<string>();
        private IEquipmentVisualResolver resolver = NullEquipmentVisualResolver.Instance;
        private IEquipmentVisualSpawner spawner = NullEquipmentVisualSpawner.Instance;
        private bool isInitialized;

        public EquipmentVisualController(
            ObjectSocketSystem socketSystem,
            EquipmentVisualSlotSocketBinding[] slotSocketBindings = null)
        {
            this.socketSystem = socketSystem ?? throw new ArgumentNullException(nameof(socketSystem));
            RebuildSlotSocketBindings(slotSocketBindings);
        }

        public void Initialize(
            EquipmentSetupSO setup,
            IEquipmentVisualResolver resolver,
            IEquipmentVisualSpawner spawner) =>
            Configure(setup?.SlotCategories, resolver, spawner, setup);

        public void Initialize(
            string[] slotCategories,
            IEquipmentVisualResolver resolver,
            IEquipmentVisualSpawner spawner) =>
            Configure(slotCategories, resolver, spawner, setup: null);

        public bool TryGetSocketKey(int equipSlotIndex, out string socketKey) =>
            socketKeyByEquipSlot.TryGetValue(equipSlotIndex, out socketKey)
            && !string.IsNullOrEmpty(socketKey);

        public void Equip(int equipSlotIndex, in ItemStack stack, in ItemDefinition definition)
        {
            if (!isInitialized)
                return;

            ClearSlot(equipSlotIndex);

            if (!TryGetSlotCategory(equipSlotIndex, out string slotCategory))
                return;

            if (!resolver.TryResolve(equipSlotIndex, slotCategory, stack, definition,
                    out EquipmentVisualDefinition visual)
                || visual.IsEmpty)
                return;

            if (!TryGetSocketKey(equipSlotIndex, out string socketKey))
                return;

            if (!socketSystem.TryGetSocket(socketKey, out ObjectSocket socket) || socket == null)
                return;

            SlotVisualState state = GetOrCreateSlotState(equipSlotIndex);
            int spawnGeneration = ++state.SpawnGeneration;
            state.Socket = socket;

            var request = new EquipmentVisualSpawnRequest(
                stack.ItemId,
                visual.AssetKey,
                equipSlotIndex,
                stack.InstanceId);
            IEquipmentVisualSpawner spawnOwner = spawner;
            spawnOwner.Spawn(
                request,
                socketItem => OnSlotVisualSpawnCompleted(
                    state,
                    spawnGeneration,
                    visual,
                    spawnOwner,
                    socketItem));
        }

        public void Unequip(int equipSlotIndex) => ClearSlot(equipSlotIndex);

        public void ClearAll()
        {
            foreach (SlotVisualState state in slotStates.Values)
            {
                state.SpawnGeneration++;
                ReleaseSlotVisual(state);
            }

            slotStates.Clear();
        }

        public void Dispose()
        {
            ClearAll();
            isInitialized = false;
        }

        private void Configure(
            string[] slotCategories,
            IEquipmentVisualResolver resolver,
            IEquipmentVisualSpawner spawner,
            EquipmentSetupSO setup)
        {
            ClearAll();

            this.slotCategories = setup != null
                ? setup.CreateSlotCategorySnapshot()
                : slotCategories == null
                    ? Array.Empty<string>()
                    : (string[])slotCategories.Clone();

            this.resolver = resolver ?? NullEquipmentVisualResolver.Instance;
            this.spawner = spawner ?? NullEquipmentVisualSpawner.Instance;
            isInitialized = true;
        }

        private void OnSlotVisualSpawnCompleted(
            SlotVisualState state,
            int spawnGeneration,
            in EquipmentVisualDefinition visual,
            IEquipmentVisualSpawner spawnOwner,
            ISocketItem socketItem)
        {
            if (spawnGeneration != state.SpawnGeneration)
            {
                if (socketItem != null)
                    spawnOwner.Release(socketItem);
                return;
            }

            if (socketItem == null)
                return;

            if (socketItem.SocketTransform == null || state.Socket == null)
            {
                spawnOwner.Release(socketItem);
                return;
            }

            state.Socket.ChangeItem(
                socketItem,
                visual.LocalPosition,
                Quaternion.Euler(visual.LocalEulerAngles),
                visual.LocalScale == default ? Vector3.one : visual.LocalScale);

            state.SocketItem = socketItem;
            state.Spawner = spawnOwner;
        }

        private void ClearSlot(int equipSlotIndex)
        {
            if (!slotStates.TryGetValue(equipSlotIndex, out SlotVisualState state))
                return;

            state.SpawnGeneration++;
            ReleaseSlotVisual(state);
        }

        private void ReleaseSlotVisual(SlotVisualState state)
        {
            if (state.SocketItem != null)
            {
                (state.Spawner ?? spawner).Release(state.SocketItem);
                state.SocketItem = null;
                state.Spawner = null;
            }

            state.Socket?.ClearItem();
            state.Socket = null;
        }

        private void RebuildSlotSocketBindings(EquipmentVisualSlotSocketBinding[] slotSocketBindings)
        {
            socketKeyByEquipSlot.Clear();

            if (slotSocketBindings == null)
                return;

            for (int i = 0; i < slotSocketBindings.Length; i++)
            {
                EquipmentVisualSlotSocketBinding binding = slotSocketBindings[i];
                if (binding.EquipSlotIndex < 0 || string.IsNullOrEmpty(binding.SocketKey))
                    continue;

                socketKeyByEquipSlot[binding.EquipSlotIndex] = binding.SocketKey;
            }
        }

        private bool TryGetSlotCategory(int equipSlotIndex, out string slotCategory)
        {
            if (equipSlotIndex >= 0 && equipSlotIndex < slotCategories.Length)
            {
                slotCategory = slotCategories[equipSlotIndex];
                return true;
            }

            slotCategory = EquipmentSlotCategories.Any;
            return false;
        }

        private SlotVisualState GetOrCreateSlotState(int equipSlotIndex)
        {
            if (!slotStates.TryGetValue(equipSlotIndex, out SlotVisualState state))
            {
                state = new SlotVisualState();
                slotStates.Add(equipSlotIndex, state);
            }

            return state;
        }

        private sealed class SlotVisualState
        {
            public int SpawnGeneration;
            public ObjectSocket Socket;
            public ISocketItem SocketItem;
            public IEquipmentVisualSpawner Spawner;
        }
    }
}
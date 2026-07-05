# EquipmentSystem

InventorySystem 위에 슬롯 카테고리(무기/투구/갑옷 등)와 장착 API를 제공하는 모듈입니다.

## 구성

| 타입 | 역할 |
|------|------|
| `EquipmentSetupSO` | 장비 컨테이너 설정, 슬롯 카테고리 배열, 태그/오버라이드 프로필 |
| `EquipmentSlotRule` | `ISlotRule` — 슬롯 인덱스별 카테고리 검증 |
| `EquipmentSystem` | `TryEquipFromContainer`, `TryUnequipToContainer`, `TrySwapEquippedSlots` |
| `ObjectEquipmentSystem` | `InventorySystem`과 연동하는 MonoBehaviour 진입점 |
| `IEquipmentEffectApplier` | 장착/해제 시 스탯·비주얼 등 게임 로직 훅 |
| `EquipmentVisualController` | 슬롯별 비주얼 생성·추적·해제 (순수 C#) |
| `ObjectEquipmentVisualHost` | Inspector fallback + Controller 생성 (MonoBehaviour) |
| `CompositeEquipmentEffectApplier` | 스탯 + 비주얼 등 여러 applier 조합 |

## EquipmentVisual (비주얼 분리)

테이블은 **ModelKey만**. Left/Right 등 착용 위치는 **장비 슬롯 인덱스 → ObjectSocket** 매핑으로 고정합니다.

```
캐릭터
  ObjectSocketSystem + socket_l, socket_r …
        ↑
  EquipmentVisualController
        ├─ slotSocketBindings (EquipSlotIndex → SocketKey)
        ├─ IEquipmentVisualResolver (ItemId → ModelKey)
        └─ IEquipmentVisualSpawner (ModelKey → ISocketItem)
              ↓
        ObjectSocket.ChangeItem(ISocketItem)

장착 슬롯 0 → socket_l   (왼손 무기)
장착 슬롯 1 → socket_r   (오른손 방패/무기)
```

| 타입 | 역할 |
|------|------|
| `EquipmentVisualRecord` | 테이블 1행 (`ModelKey`만) |
| `EquipmentVisualSlotSocketBinding` | `EquipSlotIndex → SocketKey` |
| `EquipmentVisualController` | Resolver → Spawner → `ObjectSocket.ChangeItem` |
| `ISocketItem` / `GameObjectSocketItem` | 소켓에 부착할 대상 (Transform 제공) |
| `SocketItemComponent` | 커스텀 비주얼용 `MonoBehaviour` + `ISocketItem` 베이스 |
| `SocketItemUtility` | Component → ISocketItem 변환, 기본 Release |
| `ObjectEquipmentVisualHost` | Inspector에서 슬롯↔소켓 매핑 |

**Left/Right 예:** 쉴드는 `Weapon`/`OffHand` 카테고리로 슬롯 0·1 모두 허용 → **어느 슬롯에 장착했는지**로 소켓이 결정됩니다. 아이템마다 SocketKey 불필요.

### DT_Equipment

```csv
ItemId,ModelKey
1001,Weapon_Sword_01
1002,Weapon_Shield_01
```

### EquipmentSetupSO + 소켓 매핑 예

`SlotCategories` (슬롯 규칙):

| Index | Category |
|---|---|
| 0 | LeftHand |
| 1 | RightHand |
| 2 | Head |

`ObjectEquipmentVisualHost` (비주얼 소켓):

| EquipSlotIndex | SocketKey |
|---|---|
| 0 | socket_l |
| 1 | socket_r |
| 2 | socket_head |

### 코드

```csharp
public sealed class EquipmentTableDataSource : IEquipmentVisualDataSource
{
    public bool TryGetByItemId(int itemId, out EquipmentVisualRecord record)
    {
        if (!equipmentTable.TryGetByItemId(itemId, out var row))
        {
            record = default;
            return false;
        }

        record = new EquipmentVisualRecord { ModelKey = row.ModelKey };
        return !record.IsEmpty;
    }
}

var host = character.GetComponent<ObjectEquipmentVisualHost>();
host.Initialize(
    equipmentSetup,
    new DataSourceEquipmentVisualResolver(new EquipmentTableDataSource()),
    new DelegateEquipmentVisualSpawner((request, onCompleted) =>
    {
        AddressableManager.Instance
            .InstantiateAsync(request.Definition.AssetKey)
            .OnCompleted(go =>
            {
                ISocketItem item = go.TryGetComponent(out ISocketItem existing)
                    ? existing
                    : new GameObjectSocketItem(go);
                onCompleted(item);
            })
            .Run();
    }));

objectEquipmentSystem.Init(owner, inventory, equipmentSetup,
    new EquipmentVisualEffectApplier(host.Controller));
```

커스텀 비주얼(애니·VFX 등)은 프리팹에 `SocketItemComponent`를 상속한 컴포넌트를 붙이고 Spawner가 그 `ISocketItem`을 반환하면 됩니다.
## 아이템 카테고리 지정

1. **태그** — `ItemDefinitionSO`에 `equip.Weapon` 형태 태그 (`CatalogTagEquipmentProfileSource`)
2. **오버라이드** — `EquipmentSetupSO.ItemProfileOverrides`에 `itemId → SlotCategory`
3. **코드** — `DictionaryEquipmentProfileSource` 또는 `CompositeEquipmentProfileSource`

## Inventory 연동

1. `InventorySetupSO.ContainerConfigs`에 장비 컨테이너를 등록하거나, `EquipmentSetupSO.CreateContainer()`로 생성한 컨테이너를 `InventoryGroup`에 등록합니다.
2. 장비 컨테이너는 `EquipmentSlotRule`을 사용해야 슬롯별 카테고리가 적용됩니다.
3. `ObjectEquipmentSystem.Init(owner, inventorySystem, setup)` 후 `TryEquipFromInventory` 등을 호출합니다.

## Inventory 구조 개선 (동일 패키지)

| 타입 | 역할 |
|------|------|
| `IItemInstanceStore` / `InMemoryItemInstanceStore` | `InstanceId`별 가변 데이터 저장 |
| `IItemActionResolver` / `ItemActionResolver` | `itemId` → `IItemUseHandler` 조회 |
| `InventoryGroup.TrySwapBetween` | 컨테이너 간 스왑 (장비 교체) |
| `InventoryGroup.TryMoveBetween` | 고유 인스턴스 `InstanceId` 보존 이동 |

## StatSystem 연동 (게임 측)

`IEquipmentEffectApplier`를 구현해 장착 시 스탯 modifier를 적용합니다. StatSystem 의존은 게임 프로젝트에서 선택적으로 추가하세요.

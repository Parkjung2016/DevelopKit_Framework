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

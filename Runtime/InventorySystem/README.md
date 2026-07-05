# InventorySystem

범용 Unity 인벤토리 프레임워크. UI 없이 **컨테이너 로직 / 세이브 / 라우팅 / 제작 / 루트**까지 제공합니다.

## 빠른 시작

### 1. ScriptableObject 생성

| 메뉴 | 용도 |
|------|------|
| `SO/InventorySystem/Item` | 아이템 정의 |
| `SO/InventorySystem/ItemDatabase` | 아이템 DB (IItemDatabase) |
| `SO/InventorySystem/Database Setup` | 전역 Item / Recipe / Loot DB 묶음 |
| `SO/InventorySystem/Config` | 컨테이너 설정 (슬롯 수, Kind, 규칙) |
| `SO/InventorySystem/Setup` | 컨테이너 config 배열 |
| `SO/InventorySystem/Recipe` | 제작 레시피 |
| `SO/InventorySystem/LootTable` | 드롭 테이블 |

### ItemType / ContainerKind 커스터마이즈

1. `PJDev → Inventory → Data Editor` → **Enums** 탭
2. 항목 추가·**Name**·표시명 편집 (Name = C# enum 이름, 자유롭게 변경)
3. **Generate Enums** → `Runtime/Generated/*.cs` 갱신

설정 파일: `ProjectSettings/InventorySystem/InventoryEnums.json`  
메뉴: `PJDev → Inventory → Generate Enums`

> 패키지 배포 시 `Runtime/InventorySystem/Runtime/Generated/` 가 포함되어야 합니다.  
> 최초 설치 후 enum이 없으면 에디터가 자동 생성을 시도합니다.

- **value=0**: ItemType None / ContainerKind Main 관례 (이름은 변경 가능)
- **itemTypeRoutes**: 아이템 타입 value → 컨테이너 kind value 라우팅
- **value 변경** 시 기존 SO·routes가 깨질 수 있음

### 2. 런타임 초기화

**MonoBehaviour (`InventorySystem`)** — 전역 DB + 컨테이너 Setup을 분리합니다.

```csharp
// 부팅 시 전역 DB 등록 (InventorySystem의 Database Setup 필드 또는 직접 호출)
databaseSetup.RegisterGlobals();  // ItemCatalog / RecipeCatalog / LootTableCatalog

// 컨테이너만 InventorySetupSO로 초기화
inventorySystem.Init(owner, containerSetupSO, instanceFactory: new WeaponInstanceFactory());
// → ItemInstanceCatalog.Configure(...) 자동 호출
```

### ItemInstance (가변 데이터)

정적 정의는 `ItemCatalog`, 인스턴스별 데이터는 `ItemInstanceCatalog`입니다.

```csharp
// 1) Factory 등록 (Init 시 또는 Configure)
public sealed class WeaponInstanceFactory : IItemInstanceFactory
{
    public bool TryCreate(int itemId, out IItemInstanceData data)
    {
        if (!ItemCatalog.TryGetDefinition(itemId, out _))
        {
            data = null;
            return false;
        }

        data = new WeaponInstanceData();
        return true;
    }
}

// 2) 어디서든 조회
if (ItemInstanceCatalog.TryGet<WeaponInstanceData>(instanceId, out var weapon))
    ApplyEnhance(weapon.EnhanceLevel);

// 3) itemId fallback (Store miss 시 Factory로 생성·캐시)
ItemInstanceQueries.TryGet(ItemInstanceCatalog.Current, instanceId, itemId, out IItemInstanceData data);
```

고유 아이템이 인벤에 **처음 들어갈 때** Factory가 자동 호출됩니다. 슬롯에서 **완전히 제거되면** Store에서도 삭제됩니다.

**ItemType → Equipment SlotCategory 2단 라우팅 (클래스 없이):**

```csharp
using PJDev.DevelopKit.Framework.EquipmentSystem.Runtime;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

IItemInstanceFactory factory = ItemInstanceFactoryBuilder.Create()
    .ConfigureEquipment(equipmentSetup.CreateProfileSource(), equip => equip
        .Set(EquipmentSlotCategories.Weapon, () => new WeaponInstanceData())
        .Set(EquipmentSlotCategories.Head, () => new HeadInstanceData())
        .SetFallback())
    .For(ItemType.Consumable, () => new ConsumableInstanceData())
    .For(ItemType.Quest, id => new QuestInstanceData { ItemId = id })
    .Fallback()
    .Build();

inventorySystem.Init(owner, setup, factory);
```

특정 ItemId만 예외 처리:

```csharp
var overrides = new ItemInstanceFactoryRegistry()
    .Register(9001, () => new LegendWeaponInstanceData())
    .SetFallback(ItemInstanceFactoryBuilder.Create()...Build());

ItemInstanceFactoryBuilder.Create()
    .For(ItemType.Equipment, overrides)
    ...
```

SlotCategory는 `equip.Weapon` 태그·`EquipmentSetupSO` 오버라이드와 **동일한 ProfileSource**를 씁니다.

**순수 C# (테스트·서버)** — `InventorySessionBuilder` + 전역 Catalog를 사용합니다.

```csharp
databaseSetup.RegisterGlobals();
// 또는
ItemCatalog.Set(itemDatabaseSO);
RecipeCatalog.Set(recipeDatabaseSO);
LootTableCatalog.Set(lootTableDatabaseSO);

var configs = containerSetupSO.CreateContainerConfigs();
var group = InventorySessionBuilder.CreateGroup(configs);
group.TryAddItem(itemId, count);
```

```csharp
// 장비 컨테이너 — ItemCatalog 등록 후 DB 인자 생략 가능
var equip = equipmentSetup.CreateContainer();
group.RegisterContainer(equip);
```

### 3. 멀티 컨테이너 + 제작/루팅

```csharp
inventorySystem.TryMoveBetween("main", 0, "equipment", 1);
inventorySystem.TryCraft(recipeSO);           // SO는 InventoryAuthoringExtensions 경유
inventorySystem.TryGrantLoot(lootTableSO);

// 핵심 API는 정의 struct만 사용 (SO 무관)
group.TryCraft(recipeDefinition);
group.TryGrantLoot(lootTableDefinition);
```

## 아키텍처

```
ItemCatalog / RecipeCatalog / LootTableCatalog (전역 — DatabaseSetup.RegisterGlobals)
    ↑ Resolve()
InventoryGroup / InventoryContainer (순수 C# — InventorySessionBuilder)
    ↑
InventorySystem (MonoBehaviour — DatabaseSetup + InventorySetupSO)
```

| 개념 | 설명 |
|------|------|
| `ItemCatalog` / `RecipeCatalog` / `LootTableCatalog` | 프로젝트 전역 DB |
| `InventoryDatabaseSetupSO` | 전역 DB SO 참조 + `RegisterGlobals()` |
| `InventorySetupSO` | 컨테이너 config 배열 (MonoBehaviour Init용) |
| `InventoryContainerConfig` | SO 없이 컨테이너 부트스트랩용 struct |
| `InventorySessionBuilder` | config 배열 → `InventoryGroup` 생성 |
| `ContainerId` | UI/세이브/크로스 이동용 문자열 ID |
| `ContainerKind` | Main, Equipment, QuickBar… — 라우터 분기 |
| `ItemDefinitionSO` | 디자이너용 아이템 에셋 |
| `ItemDefinition` | 런타임 struct |

## API 요약

| 용도 | API |
|------|-----|
| 기본 가방 (Main) | `TryAddItem`, `TryMoveSlot`, `ExportSaveData` |
| 특정 컨테이너 | `TryGetContainer(id, out container)` |
| 컨테이너 간 이동 | `TryMoveBetween`, `TrySwapBetween` |
| 제작/루팅 | `TryCraft`, `TryGrantLoot` |
| 전체 세이브 | `ExportGroupSaveData` / `ImportGroupSaveData` |

## 확장 포인트

| 인터페이스 | 용도 |
|-----------|------|
| `IItemDatabase` / `IItemCatalog` | 아이템 정의 |
| `IItemContainerRouter` | 타입별 컨테이너 라우팅 |
| `ISlotRule` | 슬롯 수용 규칙 |
| `IContainerCapacityRuleEx` | 무게/슬롯 용량 |
| `IItemUseHandler` | 아이템 사용 |
| `IItemActionResolver` | itemId → `IItemUseHandler` 조회 |
| `IItemInstanceStore` | `InstanceId`별 가변 인스턴스 데이터 |
| `ItemInstanceCatalog` | 전역 Store 접근 (`ItemCatalog`와 동일 패턴) |
| `IItemInstanceFactory` | 고유 아이템 생성 시 기본 인스턴스 데이터 |
| `ItemInstanceQueries` | `TryGet` / fallback 조회 헬퍼 |
| `IItemInstanceIdGenerator` | 고유 인스턴스 ID |

## 테스트

`Tests/` — Unity Edit Mode (NUnit), Test Runner 창에서 실행

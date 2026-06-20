# InventorySystem

범용 Unity 인벤토리 프레임워크. UI 없이 **컨테이너 로직 / 세이브 / 라우팅 / 제작 / 루트**까지 제공합니다.

## 빠른 시작

### 1. ScriptableObject 생성

| 메뉴 | 용도 |
|------|------|
| `SO/InventorySystem/Item` | 아이템 정의 |
| `SO/InventorySystem/ItemDatabase` | 아이템 DB (IItemDatabase) |
| `SO/InventorySystem/Config` | 컨테이너 설정 (슬롯 수, Kind, 규칙) |
| `SO/InventorySystem/Setup` | DB + 컨테이너 설정 묶음 |
| `SO/InventorySystem/Recipe` | 제작 레시피 |
| `SO/InventorySystem/LootTable` | 드롭 테이블 |

### 2. 단일 인벤토리 (MonoBehaviour)

```csharp
inventorySystem.Init(owner, itemDatabaseSO);
inventorySystem.TryAddItem(itemId, count);
inventorySystem.ExportSaveData();
inventorySystem.ImportSaveData(saveData);
```

### 3. 멀티 인벤토리 (Group)

```csharp
groupSystem.Init(owner, setupSO);
groupSystem.TryAddItem(itemId, count);
groupSystem.TryCraft(recipeSO);
groupSystem.TryGrantLoot(lootTableSO);
groupSystem.ExportSaveData();
```

## 아키텍처

```
InventoryGroupSystem
  └─ InventoryGroup
       ├─ InventoryContainer ("main")
       ├─ InventoryContainer ("equipment")
       └─ ...
```

| 개념 | 설명 |
|------|------|
| `ContainerId` | UI/세이브/크로스 이동용 문자열 ID |
| `ContainerKind` | Main, Equipment, QuickBar… — 라우터 분기 |
| `ItemDefinitionSO` | 디자이너용 아이템 에셋 |
| `ItemDefinition` | 런타임 struct |

## 확장 포인트

| 인터페이스 | 용도 |
|-----------|------|
| `IItemDatabase` / `IItemCatalog` | 아이템 정의 |
| `IItemContainerRouter` | 타입별 컨테이너 라우팅 |
| `ISlotRule` | 슬롯 수용 규칙 |
| `IContainerCapacityRuleEx` | 무게/슬롯 용량 |
| `IItemUseHandler` | 아이템 사용 |
| `IItemInstanceIdGenerator` | 고유 인스턴스 ID |

## 테스트

`Tests/` — Unity Edit Mode (NUnit)

Editor 메뉴 자동 실행:
- `Tools/InventorySystem/Tests/Run EditMode`

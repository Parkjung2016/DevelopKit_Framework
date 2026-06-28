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

```csharp
// 권장: InventorySetupSO (ContainerConfigs + DB)
inventorySystem.Init(owner, setupSO);

// 프로토타입: DB만 넘기면 기본 main 컨테이너(20슬롯) 생성
inventorySystem.Init(owner, itemDatabase);

inventorySystem.TryAddItem(itemId, count);
inventorySystem.ExportSaveData();           // Primary 컨테이너
inventorySystem.ExportGroupSaveData();      // 전체 그룹
```

### 3. 멀티 컨테이너 + 제작/루팅

```csharp
inventorySystem.TryMoveBetween("main", 0, "equipment", 1);
inventorySystem.TryCraft(recipeSO);
inventorySystem.TryGrantLoot(lootTableSO);
```

## 아키텍처

```
InventorySystem (MonoBehaviour — 단일 진입점)
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

## API 요약

| 용도 | API |
|------|-----|
| 기본 가방 (Main) | `TryAddItem`, `TryMoveSlot`, `ExportSaveData` |
| 특정 컨테이너 | `TryGetContainer(id, out container)` |
| 컨테이너 간 이동 | `TryMoveBetween` |
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
| `IItemInstanceIdGenerator` | 고유 인스턴스 ID |

## 테스트

`Tests/` — Unity Edit Mode (NUnit)

Editor 메뉴 자동 실행:
- `Tools/InventorySystem/Tests/Run EditMode`

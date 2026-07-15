# DevelopKit Framework Architecture

Unity SO·Prefab 기반 **레이어드 OOP**.

## 레이어 (의존 방향 ↓)

```
Shared        GlobalRegistry, FrameworkInitOptions     (의존 없음)
    ↑
Domain        InventoryGroup, EquipmentSystem, StatCollection …  (Pure C#)
    ↑
Catalog       ItemCatalog, StatCatalog, ItemInstanceCatalog …   (GlobalRegistry<T>)
    ↑
Presentation  ObjectInventorySystem, ObjectEquipmentSystem, ObjectStatSystem, ObjectAbilitySystem …
    ↑
Game          Player : MonoBehaviour, IInventoryOwner …
```

각 모듈은 **자기 Init만** 책임집니다. Host/Orchestrator 레이어는 두지 않습니다.

## 코드 따라가기

### 1. 세션 시작 (씬당 1회)

```csharp
inventoryDatabaseSetup.RegisterGlobals();
StatCatalog.Set(statDatabase);
```

각 모듈은 필요한 Catalog만 직접 등록합니다. 통합 Bootstrap 에셋은 사용하지 않습니다.

### 2. 액터 Init (GameObject당, 순서만 지키면 됨)

```csharp
inventory.Init(this, inventorySetup, router);
equipment.Init(this, inventory);
stat.Init();
ability.Init(this);
```

### 3. Domain API

| MonoBehaviour (`Object*`) | Domain |
|---------------------------|--------|
| `ObjectInventorySystem` | `.Group` |
| `ObjectEquipmentSystem` | `.Equipment` |
| `ObjectStatSystem` | `.Stats` |
| `ObjectAbilitySystem` | (AbilitySO 런타임 관리) |

## 네이밍

| 패턴 | 의미 | 예 |
|------|------|-----|
| `Object*` | MonoBehaviour 어댑터 | `ObjectInventorySystem`, `ObjectEquipmentSystem` |
| (없음) | Pure C# Domain | `InventoryGroup`, `EquipmentSystem` |
| `*Catalog` | `GlobalRegistry<T>` 전역 DB | `ItemCatalog`, `StatCatalog` |

모든 Unity 진입점 MB는 **`Object*` 접두사**를 사용합니다.

## Catalog 등록

| Catalog | 등록 |
|---------|------|
| Item / Recipe / Loot | `InventoryDatabaseSetupSO.RegisterGlobals()` |
| Stat | `StatDatabaseSO` → `StatCatalog.Set` |
| ItemInstance | `ObjectInventorySystem.Init` → `ItemInstanceCatalog.Configure` |

테스트 정리: 사용한 Catalog의 `Clear()`를 직접 호출합니다. Play Mode 종료 시에는 모듈별로 자동 정리됩니다.

## Static 정리 (Domain Reload 비활성화)

| Unity 버전 | Catalog / Registry | GameplayTag / IdGenerator |
|------------|-------------------|---------------------------|
| **6000.5+** | `[AutoStaticsCleanup]` on `GlobalRegistry<T>`, `*Catalog`, … | 각 타입의 `[AutoStaticsCleanup]` |
| **6000.5 미만 (Editor)** | 각 모듈이 `FrameworkPlayModeCleanup`에 자체 `Clear()` 등록 | 동일 |
| **6000.5 미만 (Player)** | `Application.quitting` → `FrameworkPlayModeCleanup.RunAll()` | 동일 |

Domain Reload **켜짐**: 어셈블리 리로드로 static 초기화 (별도 처리 불필요).

테스트에서는 `ItemCatalog.Clear()`, `RecipeCatalog.Clear()`, `LootTableCatalog.Clear()`, `ItemInstanceCatalog.Clear()`, `StatCatalog.Clear()` 중 사용한 항목을 명시적으로 정리합니다.

## InitOptions

Catalog를 먼저 등록한 뒤 Init 호출 시:

```csharp
inventory.Init(owner, initOptions: FrameworkInitOptions.SkipGlobalCatalogs);
```

## 모듈 의존

```
Shared (leaf)
Inventory, Stat, DeterministicSimulation → Shared
Equipment → Inventory + Socket
Ability → GameplayTag
AnimMontage → (standalone)
```

## SOLID (요약)

- Domain은 SO/Unity 모름
- 확장은 Interface (`IItemInstanceFactory`, `IEquipmentEffectApplier`)
- Catalog는 `GlobalRegistry<T>` 한 패턴
- Init 순서는 게임 코드(또는 문서)에서 관리 — 프레임워크 Host 없음

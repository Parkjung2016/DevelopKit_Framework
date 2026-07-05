# Core (Bootstrap)

세션 단위 **Catalog 등록**만 담당합니다. MonoBehaviour 오케스트레이션은 없습니다.

## 타입

| 타입 | 역할 |
|------|------|
| `FrameworkDatabaseSetupSO` | 인벤+스탯 DB SO 한 장 |
| `FrameworkGlobals` | RegisterAll / ClearCatalogs |

## 사용

```csharp
// 씬/세션 1회
frameworkDatabaseSetup.RegisterAll();

// 액터 — 각 Object* MB가 직접 Init
inventory.Init(this, inventorySetup);
equipment.Init(this, inventory);
stat.Init();
ability.Init(this);

// Domain
inventory.Group.TryAddItem(itemId, 1);
equipment.Equipment.TryEquipFromContainer("main", 0, 0);
stat.Collection.SetBaseValue("HP", 100f);
```

테스트 teardown: `FrameworkGlobals.ClearCatalogs()`

공유 primitive: [Shared](../Shared/README.md) · 전체 구조: [ARCHITECTURE.md](../../ARCHITECTURE.md)

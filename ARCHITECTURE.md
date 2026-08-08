# DevelopKit Framework Architecture

Framework는 기능별 asmdef로 나뉜 Unity 패키지이며, BasicTemplate은 여러 시스템이 함께 쓰는 작은 기반 기능을 제공합니다.

## 설계 원칙

- 런타임 도메인 코드는 가능한 한 Unity 오브젝트와 분리합니다.
- MonoBehaviour 진입점은 Object*System 이름을 사용합니다.
- 초기화 메서드는 Initialize, 정리는 Clear 또는 Shutdown으로 통일합니다.
- 조회는 Try*, 상태는 Is*, 변경 명령은 동사로 시작합니다.
- 반복 실행 경로에서는 임시 배열과 LINQ 사용을 피하고 재사용 버퍼를 사용합니다.
- 전역 데이터가 필요한 시스템만 GlobalRegistry<T> 기반 Catalog를 사용합니다.
- 확장 지점은 작은 인터페이스로 제공하고 기본 구현은 내부에 숨깁니다.

## 의존 방향

~~~text
BasicTemplate
  -> Shared
    -> Inventory / Stat / GameplayTag / Random / Save / UI
      -> Equipment / Ability / NotificationDot
        -> AnimMontage 및 게임 코드
~~~

구체적인 의존은 각 asmdef가 결정합니다. 상위 시스템이 하위 시스템을 참조하며, 반대 방향 참조는 두지 않습니다.

## 일반적인 초기화

Inspector에 설정 에셋을 연결한 컴포넌트는 실행 시 자동으로 초기화됩니다. 게임에서 구현을 주입하거나 순서를 직접 관리해야 할 때만 명시적으로 호출합니다.

~~~csharp
inventory.Initialize(inventorySetup, router, itemFactory);
equipment.Initialize(inventory, equipmentSetup, effectApplier);
stats.Initialize(statCatalog, statOverrides);
abilities.Initialize(owner);
~~~

각 컴포넌트의 IsInitialized로 준비 여부를 확인할 수 있습니다.

## Catalog

Catalog는 프로젝트 전체에서 공유해야 하는 정의 데이터만 보관합니다.

~~~csharp
inventoryDatabaseSetup.RegisterGlobals();
StatCatalog.Set(statDatabase);
~~~

테스트나 세션 종료 시에는 사용한 Catalog의 Clear()를 호출합니다. Domain Reload가 꺼진 환경은 각 모듈의 정리 코드가 static 상태를 초기화합니다.

## 런타임 데이터

| 컴포넌트 | 런타임 데이터 |
|---|---|
| ObjectInventorySystem | Group, PrimaryContainer |
| ObjectEquipmentSystem | Equipment, ReadOnlyEquipment |
| ObjectStatSystem | Stats |
| ObjectAbilitySystem | 등록된 Ability 인스턴스와 입력 연결 |
| ObjectAnimMontagePlayer | 현재 Montage, 재생 시간, 이벤트 |

ScriptableObject는 설정과 정의에 사용합니다. 플레이 중 변경되는 값은 일반 C# 객체나 컴포넌트가 소유합니다.

## 확장 기준

- 아이템 생성: IItemInstanceFactory
- 장비 효과: IEquipmentEffectApplier
- Ability 입력: AbilityInputBridgeSO
- Root Motion 적용: IMontageRootMotionController
- UI View: UIViewBase
- 알림닷 View: INotificationDotView
- 저장 직렬화/암호화/경로: SaveSystem의 각 인터페이스

새 구현은 기존 시스템을 수정하기보다 해당 인터페이스를 구현해 연결하는 방식을 우선합니다.

## 성능 기준

- 매 프레임 호출되는 코드는 재사용 컬렉션을 소유합니다.
- 에디터 검색과 리플렉션 결과는 캐시하고, 데이터 변경 시에만 무효화합니다.
- 풀링은 BasicTemplate.PoolSystem을 공통 진입점으로 사용합니다.
- 결정론 코드에서는 고정 틱, 고정 순서, 명시적 난수 소스를 사용합니다.
# Framework / BasicTemplate 리팩터링 보고서

## 목표

- 처음 사용하는 개발자도 공개 API의 이름과 흐름만 보고 바로 사용할 수 있게 한다.
- 런타임 반복 경로의 할당, LINQ, 중복 조회를 줄인다.
- 시스템마다 달랐던 초기화와 확장 방식을 통일한다.
- 프로젝트 전용 결합과 불필요한 레거시 API를 제거한다.
- 에디터와 런타임 코드를 분리하고, 각 시스템의 책임을 작게 유지한다.

## 적용 범위

Framework의 Ability, AnimMontage, DeterministicSimulation, Equipment, GameplayTag,
Inventory, NotificationDot, Random, Save, Shared, Socket, Stat, UI 시스템과
BasicTemplate의 Attribute, Extension, Timer, Manager, Pool, SerializeInterface,
Singleton, Utility 모듈을 점검하고 리팩터링했다.

## 공통 구조

- 시스템 진입점과 초기화 이름을 `Initialize` 중심으로 맞췄다.
- 런타임 객체가 특정 프로젝트의 Owner 인터페이스를 구현해야 하는 제약을 제거했다.
- 정적 정의는 Catalog, 실행 중 상태는 Runtime 객체가 담당하도록 역할을 나눴다.
- 콜백 중 컬렉션 변경은 대기 목록에 모아 현재 순회가 끝난 뒤 반영한다.
- 공개할 이유가 없는 변환 및 마이그레이션 API는 내부 구현으로 숨겼다.
- 한국어 XML 주석을 실제 사용 관점에서 짧고 자연스럽게 정리했다.

## 시스템별 변경

### Ability / Stat / GameplayTag

- Ability 소유자는 `UnityEngine.Object`를 사용하며 Stat 연결은 선택 사항이다.
- 비용 검사와 적용 흐름을 한곳으로 모아 중복 계산을 줄였다.
- Stat은 ID와 Catalog를 중심으로 접근할 수 있어 SO 직접 의존 없이 사용할 수 있다.
- GameplayTag 정의 조회와 중복 검사를 단순화하고 다중 차단 태그를 지원한다.

### Inventory / Equipment / Socket

- 초기화 API를 `Initialize`로 통일했다.
- `IInventoryOwner`, `IEquipmentOwner`, `IAbilitySystemOwner` 같은 불필요한 결합을 제거했다.
- 데이터 보관, 장착 규칙, 소켓 연결 책임을 분리해 교체 가능한 구조로 정리했다.

### Save / Random / Pool

- Save 기본 경로와 결과 상태를 명확히 하고 취소 결과가 올바르게 전달되도록 정리했다.
- Random은 Framework RandomSystem을 단일 진입점으로 사용한다.
- Pool은 반복 생성 경로의 콜백 조회를 캐시하고 Addressables 연동 경로를 유지했다.

### UI / NotificationDot

- UI Layer 공개 API는 문자열 ID를 기준으로 통일했다.
- 기존 Canvas Group 직렬화 값은 내부 호환 처리만 유지하고 외부 enum API는 숨겼다.
- Toast와 Loading의 실행 항목을 값 타입으로 바꿔 반복 표시 중 관리 객체 할당을 줄였다.
- NotificationDot enum 메타데이터 반사는 정의 생성 시 한 번만 수행한다.
- Presenter 대상 갱신에서 LINQ와 임시 컬렉션 생성을 제거했다.

### AnimMontage

- Montage 길이를 캐시하고 데이터 변경 시에만 다시 계산한다.
- Notify 완료 프레임의 중복 Dispatch를 제거했다.
- 오디오 파형 필드 반사 결과를 타입별로 캐시했다.
- 공개 Notify 이벤트는 `OnNotify`로 통일하고 `NotifyFired`를 제거했다.
- 프리뷰의 Play Mode 전환 정리 경로와 PlayableGraph 생명주기를 점검했다.

### DeterministicSimulation

- 명령 실행 및 과거 명령 제거에 재사용 버퍼를 적용했다.
- 시스템이 Tick 중 등록 또는 해제돼도 현재 순회를 깨지 않고 다음 경계에서 반영한다.
- `SystemCount`, `TickCount`와 명확한 bool 반환값을 추가했다.

### BasicTemplate

- ComponentManager 모듈을 유지하고 재수집 중복, Owner 타입 안전성, 활성화 인자 버그를 수정했다.
- ComponentOrder를 타입별로 캐시하고 동일 순서에서는 Hierarchy 탐색 순서를 유지한다.
- String, List, Renderer 확장 메서드의 LINQ와 불필요한 임시 객체를 제거했다.
- 무작위 선택과 셔플은 Framework RandomSystem을 사용하도록 중복 API를 제거했다.
- `RayCastUtil`을 명확한 `RaycastUtility`로 바꾸고 Camera를 명시적으로 받는다.
- Scene 전환은 `FadeOut -> Load -> FadeIn` 순서로 정리하고 동시 로드를 차단한다.
- ZWrite 설정은 `SetZWrite` 하나로 통합했다.

## 주요 API 변경

| 이전 | 현재 |
| --- | --- |
| `Init` | `Initialize` |
| `NotifyFired` | `OnNotify` |
| `ListExtensions.RefreshWith` | `ReplaceWith` |
| `EnableZWrite` / `DisableZWrite` | `SetZWrite` |
| `RayCastUtil` | `RaycastUtility` |
| `GetCurScene` | `CurrentScene` / `GetCurrentScene<T>` |
| `IInventoryOwner`, `IEquipmentOwner`, `IAbilitySystemOwner` | 제거, 필요한 객체나 서비스를 직접 전달 |
| UI Canvas Group enum 기반 공개 API | 문자열 Layer ID 기반 API |

`Enumerable.Random`과 `List.Shuffle`은 제거했다. 난수 정책이 필요한 코드는
Framework RandomSystem을 사용한다. `ISceneTransition.Go`도 제거하고 전환 수명 주기는
`FadeOut`, `FadeIn`으로 표현한다.

## 성능 개선 기준

- Update, Tick, Notify, UI 표시처럼 자주 호출되는 경로에서 LINQ를 사용하지 않는다.
- 반복적으로 필요한 Reflection 결과와 계산값은 캐시한다.
- 콜백 순회 중 컬렉션을 복사하지 않고 재사용 버퍼나 지연 변경 목록을 사용한다.
- 초기화 시 만들 수 있는 조회 테이블은 실행 중 매번 재구성하지 않는다.
- 에디터 전용 탐색과 표시 로직이 런타임 어셈블리에 들어가지 않게 유지한다.

## 검증 결과

- `dotnet build DevelopKit_Framework.sln --no-restore`: 성공, 컴파일 오류 0개.
- 시스템별 Runtime, Editor, Test 어셈블리 빌드: 성공.
- 제거한 구 API 이름과 `TODO`, `FIXME`, `HACK` 잔여 검색: 대상 없음.
- Unity EditMode 테스트는 로컬 Hot Reload 패키지가 배치 모드에서 창을 열려고 한 문제와
  Unity 라이선스 액세스 토큰 오류 때문에 테스트 러너가 시작되기 전에 종료됐다.

전체 빌드의 남은 경고는 Unity가 생성한 프로젝트의 `System.Numerics.Vectors` 참조 버전
충돌과 외부 Anti-Cheat 플러그인의 직렬화 분석 경고다. 이번 리팩터링 소스의 C# 컴파일
오류는 없다.

## 사용 원칙

1. 데이터 정의는 Catalog 또는 SO에 두고 실행 중 상태는 별도 Runtime 객체에 둔다.
2. 시스템은 `Initialize`한 뒤 명확한 동사형 API로 사용한다.
3. 프로젝트 고유 타입은 상속 강제보다 Context, 인터페이스, 콜백으로 연결한다.
4. 반복 호출 경로에는 LINQ, Reflection, 새 배열 생성을 추가하지 않는다.
5. 새 기능은 기존 공개 타입에 필드를 계속 늘리기보다 작은 정책 객체나 서비스로 확장한다.

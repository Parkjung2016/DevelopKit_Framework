# Random System

빠른 결정론적 난수와 게임에서 체감되는 쏠림을 제어하는 선택 정책을 제공합니다. 런타임 코어는 Unity API에 의존하지 않아 일반 C# 코드에서도 사용할 수 있습니다.

## 어떤 방식을 써야 하나요?

- 순수 난수와 재현 가능한 결과가 필요함: `RandomGenerator`
- 목록을 같은 확률로 선택함: `RandomPick.Pick`
- 서로 다른 확률로 반복 선택함: `WeightedTable<T>`
- 한 주기 안에서 결과 비율을 정확히 맞춤: `ShuffleBag<T>`
- 성공 횟수를 목표 확률에서 1회 이내로 유지함: `BalancedChance`

순수 난수에는 연속 성공과 연속 실패가 자연스럽게 발생합니다. 이런 현상을 허용할 수 없다면 `RandomGenerator.Chance` 대신 `ShuffleBag<T>` 또는 `BalancedChance`를 사용해야 합니다.

## 기본 난수

```csharp
RandomGenerator random = RandomProvider.Shared;

int index = random.NextInt(0, 10); // 0 이상, 10 미만
float value = random.NextFloat();  // 0 이상, 1 미만
bool success = random.Chance(0.25);
```

`NextInt`는 나머지 연산만 사용하는 범위 변환과 달리 rejection sampling을 사용해서 특정 값이 더 자주 선택되는 편향을 제거합니다.

동일한 seed로 만들면 항상 같은 결과가 나옵니다.

```csharp
var first = RandomProvider.Create(100);
var second = RandomProvider.Create(100);
```

`Snapshot`과 `Restore`를 사용하면 저장, 리플레이, 네트워크 동기화 시점의 난수 상태를 복원할 수 있습니다.

## 가중치 선택

같은 가중치 목록을 반복해서 선택할 때는 `WeightedTable<T>`가 합계와 누적 가중치를 한 번만 계산합니다.

```csharp
var table = new WeightedTable<Loot>(loot, item => item.Weight);
Loot selected = table.Pick(random);
```

가중치가 바뀌면 `Rebuild`를 호출합니다. 0 이하, `NaN`, 무한대 가중치는 선택 대상에서 제외됩니다.

## 셔플 백

`ShuffleBag<T>`는 등록한 항목을 모두 소진할 때까지 중복 없이 순서를 섞습니다. 주기가 바뀔 때 가능한 경우 직전 결과와 첫 결과가 같지 않도록 조정합니다.

```csharp
var bag = new ShuffleBag<Rarity>();
bag.Add(Rarity.Common, 7);
bag.Add(Rarity.Rare, 2);
bag.Add(Rarity.Legendary, 1);

Rarity rarity = bag.Next();
```

위 설정은 매 10회마다 Common 7회, Rare 2회, Legendary 1회를 정확히 보장하면서 순서만 무작위로 만듭니다.

## 누적 확률 보정

`BalancedChance`는 누적 성공 횟수가 목표값에서 1회 이상 벗어나지 않게 합니다. 전투 판정처럼 완전한 예측 불가능성이 중요한 곳보다, 제작 성공이나 보상처럼 장기간 실패 쏠림이 없어야 하는 곳에 적합합니다.

```csharp
var criticalChance = new BalancedChance();
bool critical = criticalChance.Roll(0.2);
```

## 성능 기준

- `RandomGenerator`는 클래스를 한 번 만든 뒤 호출 중 할당하지 않습니다.
- `RandomProvider.Shared`는 스레드마다 하나의 생성기를 재사용합니다.
- `ShuffleBag<T>`는 설정 변경 시에만 내부 목록을 다시 구성합니다.
- `WeightedTable<T>`는 생성 또는 `Rebuild` 시에만 배열을 만듭니다. 선택은 이진 탐색으로 처리합니다.
- 병렬 작업에서는 작업마다 seed와 stream이 다른 `RandomGenerator`를 만들어 사용하세요.

# DeterministicSimulation

플랫폼·프레임레이트와 무관하게 **동일 입력 → 동일 결과**를 보장하는 결정론 시뮬레이션 모듈입니다. 록스텝 멀티플레이, 리플레이, 서버 검증에 사용합니다.

## 구성

| 타입 | 역할 |
|------|------|
| `Fixed64` | Q32.32 고정소수점 (`float`/`double` 대체) |
| `FixedVector2` / `FixedVector3` | 고정소수점 벡터 |
| `FixedMath` | Abs, Clamp, Lerp, Sqrt, Sin/Cos(LUT) |
| `DetRandom` | PCG32 기반 결정론 RNG |
| `IRandomSource` | 프레임워크 공통 난수 API (`System.Random` / `DetRandom` 통합) |
| `RandomSources` | `System()`, `Deterministic(seed)` |
| `RandomUtility` | 가중치 추첨 (`TryPickWeightedIndex`) |
| `DeterministicSimulator` | 고정 틱 시뮬레이션 루프 |
| `ISimulationSystem` | 틱마다 실행되는 시스템 |
| `SimulationCommandQueue<T>` | 틱별 입력·명령 버퍼 |
| `DeterministicHasher` | FNV-1a 상태 해시 (디싱크 탐지) |

## 빠른 시작

```csharp
using PJDev.DevelopKit.Framework.DeterministicSimulation.Runtime;

public sealed class MovementSystem : ISimulationSystem
{
    public FixedVector2 Position;

    public void OnSimulationReset(DeterministicSimulator simulation)
        => Position = FixedVector2.Zero;

    public void BeforeTick(DeterministicSimulator simulation) { }

    public void SimulateTick(DeterministicSimulator simulation)
    {
        Fixed64 speed = Fixed64.FromInt(5);
        Fixed64 dt = Fixed64.One / Fixed64.FromInt(simulation.Config.TickRate);
        Position += new FixedVector2(speed * dt, Fixed64.Zero);
    }
}

var simulation = new DeterministicSimulator();
simulation.Register(new MovementSystem());
simulation.Reset(new SimulationConfig(tickRate: 60, seed: 2024));

for (int i = 0; i < 120; i++)
    simulation.Step();
```

## 록스텝 / 리플레이

```csharp
struct InputCommand
{
    public int MoveX;
    public int MoveY;
}

var queue = new SimulationCommandQueue<InputCommand>();
queue.Enqueue(tick: 10, new InputCommand { MoveX = 1, MoveY = 0 });

simulation.Step(queue, cmd => ApplyInput(cmd));
```

모든 클라이언트가 **같은 seed + 같은 tick별 command**를 적용하면 `ComputeStateHash()` 결과가 일치해야 합니다.

## 프레임워크 연동 (Loot 등)

`IRandomSource`는 Inventory `LootRoller` 등에서 공통으로 사용합니다.

```csharp
// 싱글플레이 — random 생략 시 호출마다 새 System.Random
group.TryGrantLoot("basic_loot");

// 같은 seed → 같은 드랍 (리플레이·PVP)
group.TryGrantLoot("basic_loot", RandomSources.Deterministic(2024));

// 시뮬 RNG와 공유
DetRandomSource rng = simulation.CreateRandomSource();
group.TryGrantLoot(table, rng);
simulation.SyncRandom(rng);
```

```csharp
// 커스텀 가중치 추첨
RandomUtility.TryPickWeightedIndex(candidates, i => weights[i], RandomSources.Deterministic(1), out int picked);
```

## 주의사항

- **결정론 경로에서 `float`/`UnityEngine.Random`/`Time.deltaTime` 사용 금지**
- `Fixed64.FromFloat()`는 에디터·디버그 전용입니다
- Sin/Cos LUT는 빌드 시 1회 생성되며 런타임 연산은 고정소수점만 사용합니다
- Unity Physics·Animator 등 엔진 시스템은 결정론적이지 않습니다. 게임플레이 로직만 이 모듈로 분리하세요

## 테스트

`DeterministicSimulation.Tests` — Fixed64 연산, RNG 시퀀스, 동일 seed 해시 일치 검증

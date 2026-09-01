# Animation System

일반 상태 애니메이션과 일회성 애니메이션을 한 편집 환경에서 관리하는 범용 애니메이션 시스템입니다. `Animation Sequence`와 `Animation Montage` 모두 같은 Notify, Notify State, 프리뷰 기능을 사용합니다.

## 에셋 구분

### Animation Sequence

State Machine에서 계속 재생할 애니메이션과 Notify 타임라인을 묶는 에셋입니다.

- Sequence 하나는 Animation Clip 하나를 참조합니다.
- `AnimStateMachineSO`의 State가 Sequence를 직접 참조합니다.
- Sequence의 Clip 구간은 이동, 분할, Trim, 삭제할 수 없습니다.
- Notify와 Notify State는 자유롭게 추가하고 편집할 수 있습니다.
- State별 Speed와 Loop, Transition Blend, Parameter Condition을 설정할 수 있습니다.
### Animation Montage

공격, 피격, 스킬처럼 코드에서 시작하고 한 번의 재생 흐름으로 끝나는 애니메이션입니다.

- 여러 Animation Segment와 Empty State를 배치할 수 있습니다.
- Segment 겹침, Blend In/Out, Rate Scale을 지원합니다.
- `ObjectAnimMontagePlayer.Play()`로 재생합니다.

## Animation Editor

메뉴 경로:

```text
PJDev/Animation/Animation Editor
```

브라우저는 `Libraries`, `Sequences`, `Montages` 탭으로 나뉩니다. Animation Library를 선택하면 연결된 Sequence와 Montage만 표시됩니다.

주요 기능:

- Animation Library, Sequence, Montage 생성
- 에셋 이름 변경과 삭제
- Preview Model 연결
- Notify와 Notify State 배치
- Sound 파형, Effect, Camera Shake 프리뷰
- 다중 선택, 복사, 붙여넣기, Undo, Redo
- Montage Segment, Empty State, Blend, Root Motion 편집
- Play Mode에서 에셋 편집과 프리뷰 재생 자동 잠금

Sequence를 선택하면 Clip 교체와 Notify 편집에 필요한 UI만 표시됩니다. Montage 전용 Rate Scale, Root Motion 설정과 Segment 편집 명령은 숨겨집니다.

## Animation State Machine

`AnimStateMachineSO`는 Unity Animator Controller를 사용하지 않는 커스텀 State Machine입니다. State는 `AnimSequenceSO`를 직접 참조하고, `AnimationStateMachinePlayer`가 Playable Graph로 재생과 전환을 처리합니다.

1. 애니메이션을 재생할 오브젝트에 `AnimationStateMachinePlayer`를 추가합니다.
2. Player Inspector에서 `Create & Open`을 눌러 State Machine을 만듭니다.
3. Sequence를 그래프로 드래그하고 State와 Transition을 연결합니다.
4. 기본으로 재생할 State를 선택하고 `D`를 누르거나 `Set Default`를 실행합니다.

Animator Controller는 필요하지 않습니다. `Animator`는 Playables가 모델의 본에 포즈를 출력하기 위해서만 사용하며, Player가 비활성 자식까지 자동으로 찾습니다. Animator가 전혀 없으면 `Create If Missing` 설정에 따라 같은 오브젝트에 자동으로 추가합니다. Humanoid 모델은 Avatar가 설정된 모델 Animator를 연결하는 것이 좋습니다.
State 오른쪽 출력 포트를 대상 노드로 드래그하면 Transition이 생깁니다. + Node에서는 Sequence State, Conduit, State Alias, 내부 State Machine을 만들 수 있고 Project 창의 Sequence를 그래프로 직접 드래그할 수도 있습니다. Entry는 이동할 수 있지만 삭제할 수 없습니다. 내부 State Machine은 자체 Entry와 기본 노드를 가지며 더블클릭으로 들어가고 툴바의 < 버튼으로 상위 그래프로 돌아갑니다. State Alias는 여러 State가 공유하는 Transition 출발점이고, Conduit는 조건을 만족하는 경로로 즉시 분기합니다. 좁은 창에서는 Parameters와 Details 패널이 선택 상태에 맞춰 자동으로 정리됩니다.

### Sequence 빠른 시작

1. Animation Sequence를 만들고 Clip을 지정합니다.
2. 오브젝트에 `AnimationStateMachinePlayer`를 추가합니다.
3. Player Inspector의 `Create & Open`에서 State Machine을 만들고 Sequence State를 연결합니다.
4. 코드에서 State Machine Parameter 값을 변경합니다.
`csharp
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEngine;

public sealed class CharacterAnimation : MonoBehaviour
{
    [SerializeField] private AnimationStateMachinePlayer animationPlayer;

    public void SetMoveSpeed(float speed) =>
        animationPlayer.SetFloat("Move Speed", speed);

    public void Jump() =>
        animationPlayer.SetTrigger("Jump");
}
```

지원 Parameter는 `Float`, `Int`, `Bool`, `Trigger`입니다. Transition은 Exit Time, Blend Duration, 여러 Condition을 함께 사용할 수 있습니다. Parameter 이름을 그래프에서 바꾸면 연결된 Condition도 같이 갱신됩니다.

## Montage 빠른 시작

```csharp
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEngine;

public sealed class AttackController : MonoBehaviour
{
    [SerializeField] private ObjectAnimMontagePlayer montagePlayer;
    [SerializeField] private AnimMontageSO attackMontage;

    public void Attack() => montagePlayer.Play(attackMontage);
    public void CancelAttack() => montagePlayer.Stop();
}
```

주요 API:

```csharp
player.Play(montage);
player.Play(montage, startTime: 0.5f);
player.Pause();
player.Pause(false);
player.SetTime(1.2f);
player.Stop();
```

자동 재생은 `ObjectAnimMontageAutoPlayer`가 담당합니다.

## Notify

Notify와 Notify State는 ScriptableObject가 아닌 직렬화 가능한 일반 클래스입니다. 같은 타입을 여러 번 배치해도 각 항목의 값은 독립적입니다.

```csharp
[System.Serializable]
public sealed class DamageNotify : AnimNotify
{
    [SerializeField] private float damage = 10f;

    public override void OnNotify(AnimNotifyContext context)
    {
        // 프로젝트의 전투 시스템을 호출합니다.
    }
}
```

```csharp
[System.Serializable]
public sealed class SuperArmorNotifyState : AnimNotifyState
{
    public override void OnBegin(AnimNotifyContext context) { }
    public override void OnTick(AnimNotifyContext context, float deltaTime) { }
    public override void OnEnd(AnimNotifyContext context) { }
}
```

`AnimNotifyContext.AnimationAsset`은 Sequence와 Montage에서 공통으로 사용할 수 있습니다. 타입이 필요하면 `context.Sequence` 또는 `context.Montage`를 확인합니다.

기본 제공 타입:

- `LogAnimNotify`
- `SpawnEffectAnimNotify`
- `PlaySoundAnimNotify`
- `TransformNotify`
- `SpawnEffectAnimNotifyState`
- `PlayLoopSoundAnimNotifyState`
- `PlaybackSpeedAnimNotifyState`
- `TimeControlAnimNotifyState`
- `CameraShakeAnimNotify`
- `CameraShakeAnimNotifyState`

`Trigger In Editor`를 켜면 타임라인을 직접 이동할 때도 에디터 프리뷰가 실행됩니다.

## Montage Blend와 Root Motion

Montage의 `Blend In`은 기본 애니메이션에서 Montage로 전환되는 시간이고, `Blend Out`은 기본 애니메이션으로 돌아가는 시간입니다. Segment끼리 겹치면 겹친 구간이 자동 Blend 구간이 됩니다.

Root Motion 적용 방식:

- `Transform`: Transform에 직접 적용
- `Rigidbody`: `MovePosition`, `MoveRotation` 사용
- `CharacterController`: `Move`와 Transform 회전 사용
- `Custom`: 프로젝트 전용 컨트롤러 사용

```csharp
public sealed class CharacterRootMotion : MontageRootMotionController
{
    [SerializeField] private CharacterController controller;

    public override void ApplyMontageRootMotion(
        ObjectAnimMontagePlayer player,
        Animator animator,
        Vector3 deltaPosition,
        Quaternion deltaRotation)
    {
        controller.Move(deltaPosition);
        transform.rotation *= deltaRotation;
    }
}
```

Sequence의 Root Motion은 State가 참조하는 원본 Clip과 Animator 설정을 따릅니다.

## 확장 원칙

- 한 시점의 동작은 `AnimNotify`를 상속합니다.
- 구간 동작은 `AnimNotifyState`를 상속합니다.
- Sequence와 Montage 공통 데이터는 `IAnimationNotifyAsset`으로 받습니다.
- Montage 이동 처리 교체는 `IMontageRootMotionController`를 구현합니다.
- 프로젝트 기능은 기본 타입을 수정하지 않고 새 Notify 또는 Controller로 추가합니다.
- 런타임 어셈블리는 Editor 어셈블리를 참조하지 않습니다.

## 성능

- State Machine은 Parameter Dictionary, Notify Cursor, 두 개의 Blend Playable을 재사용합니다.
- Notify Dispatcher와 평가 버퍼를 재사용합니다.
- Animation Clip 선택 팝업은 Unity Search 결과를 비동기로 나눠 읽습니다.
- 타임라인 Repaint에서는 트랙과 Segment 목록을 새로 할당하지 않습니다.
- Playable Graph와 Root Motion 샘플러는 재생 종료 및 Play Mode 전환 때 정리합니다.

# Anim Montage System

Unity에서 Animation Clip, Notify, Notify State를 하나의 타임라인으로 편집하고 재생하는 범용 애니메이션 시스템입니다. 런타임 코드는 에디터에 의존하지 않으며, Notify와 Root Motion 적용 방식을 프로젝트에 맞게 확장할 수 있습니다.

## 구성

```text
Data       AnimMontageSO, MontageSegment, AnimNotifyPlacement, AnimNotifyStatePlacement
Playback   ObjectAnimMontagePlayer, MontagePlaybackState, MontageBlendController
Animation  MontagePlayableGraph, MontageSegmentBlending
Notify     AnimNotify, AnimNotifyState, MontageNotifyDispatcher
Motion     MontageRootMotionDriver, MontageTransformDriver
Editor     AnimMontageEditorWindow
```

`ObjectAnimMontagePlayer`는 재생 순서만 관리합니다. Blend, Root Motion, Transform Notify 적용은 각각 별도 객체가 담당하므로 기능을 수정하거나 교체할 때 영향 범위가 작습니다.

## 빠른 시작

캐릭터 또는 오브젝트에 `ObjectAnimMontagePlayer`를 추가하고 Animator를 연결합니다.

```csharp
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEngine;

public sealed class AttackController : MonoBehaviour
{
    [SerializeField] private ObjectAnimMontagePlayer montagePlayer;
    [SerializeField] private AnimMontageSO attackMontage;

    public void Attack()
    {
        montagePlayer.Play(attackMontage);
    }

    public void CancelAttack()
    {
        montagePlayer.Stop();
    }
}
```

주요 재생 API:

```csharp
player.Play(montage);
player.Play(montage, startTime: 0.5f);
player.Pause();
player.Pause(false);
player.SetTime(1.2f);
player.Stop();

float time = player.CurrentTime;
float duration = player.Duration;
float progress = player.NormalizedTime;
```

자동 재생이 필요하면 `ObjectAnimMontageAutoPlayer`를 함께 사용합니다. 기본 Montage와 Play On Awake 설정은 Auto Player가 담당합니다.

## Animation Track

- `Animation Segment`: Animation Clip을 재생합니다.
- `Empty State`: 진입 직전 Montage 포즈를 유지합니다.
- Empty State와 다음 Animation Segment를 겹치면 겹친 길이만큼 두 포즈가 블렌드됩니다.
- Segment 사이의 일반 공백은 직전 포즈를 유지합니다.
- Loop Clip은 Segment 길이에 맞춰 반복 재생할 수 있습니다.

## Blend

Montage의 `Blend In`은 Animator Controller에서 Montage로 전환되는 시간을 정합니다. `Blend Out`은 Montage가 끝난 뒤 Animator Controller로 돌아가는 시간을 정합니다.

Animation Segment끼리 겹치면 겹친 구간이 자동 블렌드 구간이 됩니다. Empty State와 Animation Segment의 겹침도 같은 규칙을 사용합니다.

## Notify

Notify는 ScriptableObject가 아닌 직렬화 가능한 일반 클래스입니다. 같은 Notify 타입을 여러 번 배치해도 각 배치가 독립된 값을 가집니다.

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

Notify State는 구간의 시작, 갱신, 종료를 처리합니다.

```csharp
[System.Serializable]
public sealed class SuperArmorNotifyState : AnimNotifyState
{
    public override void OnBegin(AnimNotifyContext context) { }
    public override void OnTick(AnimNotifyContext context, float deltaTime) { }
    public override void OnEnd(AnimNotifyContext context) { }
}
```

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

`Trigger In Editor`를 켜면 타임라인을 직접 이동할 때도 해당 Notify의 에디터 프리뷰가 실행됩니다.

## 재생 이벤트

```csharp
player.OnPlay += OnMontagePlay;
player.OnComplete += OnMontageComplete;
player.OnStop += OnMontageStop;
player.OnInterrupted += OnMontageInterrupted;
player.OnNotify += OnNotify;
```

모든 재생 이벤트를 한 곳에서 처리하려면 `OnPlaybackEvent`를 사용합니다. 이벤트에는 원본 SO 전체가 아닌 런타임에 필요한 `MontageRuntimeInfo`만 전달됩니다.

## Root Motion

Montage에서 Horizontal, Vertical, Rotation Root Motion 중 하나 이상을 켜면 Root Motion이 활성화됩니다.

`ObjectAnimMontagePlayer`의 Root Motion Mode:

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

```csharp
player.SetCustomRootMotionController(rootMotionController);
```

Rigidbody와 CharacterController 참조가 비어 있으면 부모 방향에서 자동으로 찾습니다.

## Montage Editor

메뉴 경로:

```text
PJDev/Animation/Montage Editor
```

에디터에서 다음 작업을 할 수 있습니다.

- Library와 Montage 생성, 이름 변경, 삭제
- Preview Model 연결
- Animation Segment와 Empty State 배치
- Notify와 Notify State 배치
- 이동, 크기 조절, 다중 선택, 복사, 붙여넣기
- Root Motion, Blend In, Blend Out, Rate Scale 프리뷰
- Sound 파형, Effect, Camera Shake 프리뷰
- 런타임 플레이어 상태 모니터링

Play Mode에서는 Montage 에셋 편집과 에디터 타임라인 재생이 잠깁니다.

## 확장 원칙

- 한 시점의 동작은 `AnimNotify`를 상속합니다.
- 구간 동작은 `AnimNotifyState`를 상속합니다.
- 이동 처리 교체는 `IMontageRootMotionController`를 구현합니다.
- 런타임 기능은 Editor 어셈블리를 참조하지 않습니다.
- 프로젝트 전용 기능은 기본 타입을 수정하지 않고 새 Notify 또는 Controller로 추가합니다.

## 성능

- 재생 중 Montage 길이는 시작 시 한 번 계산해 캐시합니다.
- 재생 샘플, Notify 버퍼, 타임라인 렌더링 목록을 재사용합니다.
- Root Motion 샘플러와 Playable Graph는 재생 중 재사용하고 종료 시 정리합니다.
- 타임라인 Repaint에서는 트랙 및 Segment 목록을 새로 할당하지 않습니다.
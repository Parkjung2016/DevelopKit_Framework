# AnimMontageSystem

Unity에서 Unreal Montage와 비슷한 방식으로 애니메이션 세그먼트, Notify, Notify State를 편집하고 재생하기 위한 시스템입니다.

## 구성

```text
Data      AnimMontageSO, MontageSegment, AnimNotifyPlacement, AnimNotifyStatePlacement
Notify    AnimNotify, AnimNotifyState
Domain    MontageEvaluator, MontagePlaybackState, MontageNotifyDispatcher
Runtime   ObjectAnimMontagePlayer, ObjectAnimMontageAutoPlayer
Editor    AnimMontageEditorWindow
```

## 기본 사용

```csharp
var player = GetComponent<ObjectAnimMontagePlayer>();
player.Play(montageSO);
player.SetTime(0.5f);
player.Pause(true);
```

`ObjectAnimMontagePlayer`는 Montage의 현재 시간에 맞춰 Animation Segment를 샘플링하고, 지나간 구간의 Notify와 Notify State를 실행합니다.

시작 시 자동으로 Montage를 재생해야 하면 `ObjectAnimMontageAutoPlayer` 컴포넌트를 추가하고 `Montage`, `Start Time`, `Play On Awake`를 설정합니다. `ObjectAnimMontagePlayer` 자체에는 자동 재생 설정을 두지 않습니다.

## Root Motion

Montage의 `Apply Root Motion`을 켜면 `ObjectAnimMontagePlayer`가 `Animator.deltaPosition`, `Animator.deltaRotation`을 받아 이동에 적용합니다.

`ObjectAnimMontagePlayer` 인스펙터의 `Root Motion Mode`에서 적용 방식을 고를 수 있습니다.

- `Transform`: 기존 방식처럼 Animator Transform에 직접 적용합니다.
- `Rigidbody`: `Rigidbody.MovePosition`, `Rigidbody.MoveRotation`으로 적용합니다.
- `CharacterController`: `CharacterController.Move`로 위치를 적용하고 Transform 회전을 갱신합니다.
- `Custom`: 직접 만든 컨트롤러가 Root Motion delta를 처리합니다.

Rigidbody 또는 CharacterController 참조가 비어 있으면 Player가 부모 방향에서 자동으로 한 번 찾고, 그래도 없으면 Transform 방식으로 fallback 됩니다.

커스텀 컨트롤러는 `MontageRootMotionController`를 상속해서 만들 수 있습니다.

```csharp
public sealed class MyRootMotionController : MontageRootMotionController
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

Root Motion Mode는 `ObjectAnimMontagePlayer` 인스펙터에서 설정합니다. Custom 모드에서는 인스펙터에 `MontageRootMotionController`를 할당하거나 런타임에서 컨트롤러 인스턴스만 교체할 수 있습니다.

```csharp
player.SetCustomRootMotionController(myController);
```

## Notify / Notify State

`AnimNotify`와 `AnimNotifyState`는 ScriptableObject가 아니라 직렬화 가능한 일반 클래스입니다. Montage 타임라인에 배치된 각 Notify가 자기 인스턴스를 직접 들고 있으므로, 같은 Notify 타입을 여러 번 배치해도 각 배치마다 필드 값을 다르게 설정할 수 있습니다.

```csharp
[System.Serializable]
public sealed class MyNotify : AnimNotify
{
    [SerializeField] private string message;

    public override void OnNotify(AnimNotifyContext context)
    {
        Debug.Log(message);
    }
}
```

```csharp
[System.Serializable]
public sealed class MyNotifyState : AnimNotifyState
{
    public override void OnBegin(AnimNotifyContext context)
    {
    }

    public override void OnTick(AnimNotifyContext context, float deltaTime)
    {
    }

    public override void OnEnd(AnimNotifyContext context)
    {
    }
}
```

Montage Editor에서 Notify 트랙을 우클릭한 뒤 `Create/Notify...` 또는 `Create/Notify State...`를 선택하면 프로젝트에 존재하는 Notify 클래스 타입을 고를 수 있습니다.

기본 제공 Notify:

- `LogAnimNotify`: 로그 출력
- `SpawnEffectAnimNotify`: 프리팹 이펙트 1회 생성
- `PlaySoundAnimNotify`: 사운드 1회 재생
- `SpawnEffectAnimNotifyState`: Notify State 구간 동안 프리팹 이펙트 유지
- `PlayLoopSoundAnimNotifyState`: Notify State 구간 동안 루프 사운드 재생

## 에디터 동작 옵션

Notify와 Notify State에는 `Trigger In Editor Scrub` 옵션이 있습니다.

- 꺼짐: 타임라인 재생 중에만 실행됩니다.
- 켜짐: 타임라인 바를 직접 움직이는 스크럽 중에도 실행됩니다.

Montage Editor의 재생 중에는 런타임과 같은 Dispatcher 흐름으로 Notify가 실행됩니다.

## 기본 Notify / Notify State

Montage 타임라인의 확장 요소는 AnimNotify 또는 AnimNotifyState로 작성합니다.

- AnimNotify: 특정 시점에 한 번 실행되는 동작
- AnimNotifyState: 시작, 갱신, 종료가 필요한 구간 동작
- TransformNotify: 지정된 시점부터 위치, 회전, 크기를 채널별 Duration과 Easing Curve로 적용
- PlaybackSpeedAnimNotifyState: 지정된 구간의 Montage 재생 속도 조절
- TimeControlAnimNotifyState: 지정된 구간의 TimeScale 레이어 조절
- CameraShakeAnimNotifyState: 지정된 구간에 Cinemachine 카메라 흔들림 적용

Camera Shake는 Montage Editor 재생 중에는 항상 표시됩니다. 타임라인 바를 직접 움직일 때는 해당 State의 Trigger On Manual Preview가 켜져 있어야 프리뷰에 표시됩니다. 런타임에서는 활성 Cinemachine Camera의 Basic Multi Channel Perlin을 사용하며, State가 끝나면 기존 Noise 설정을 복원합니다.

타임라인 확장 동작은 일반 클래스인 AnimNotify 또는 AnimNotifyState를 상속해 만들고, Notify Track 또는 Notify State Track에 배치합니다.
## Montage Editor

메뉴 경로:

```text
PJDev/Animation/Montage Editor
```

주요 기능:

- Montage SO 선택 및 편집
- Preview Mesh 기반 애니메이션 미리보기
- Animation Segment, Notify, Notify State 배치

- 타임라인 재생, 정지, 루프, 속도 조절
- Inspector에서 선택한 타임라인 요소의 필드 편집
- Log Viewer에서 Notify 실행 로그 확인

## Log Viewer

Montage Editor 오른쪽 아래의 Log Viewer는 Unity 로그를 실시간으로 표시합니다.

- Info / Warn / Error 필터
- 검색 필터
- Clear 버튼
- 최대 500개 로그 유지

`Debug.Log` 또는 `CDebug.Log`로 찍은 Notify 로그도 Log Viewer에 표시됩니다.

## 참고

기존 Notify ScriptableObject asset 방식은 사용하지 않습니다. Notify는 클래스 타입을 선택해 Montage 안에 직접 저장하는 방식입니다.
# AnimMontageSystem

Unity에서 Unreal Montage와 비슷한 방식으로 애니메이션 세그먼트, Notify, Notify State, 커스텀 트랙/요소를 편집하고 재생하기 위한 시스템입니다.

## 구성

```text
Data      AnimMontageSO, MontageSegment, AnimNotifyPlacement, AnimNotifyStatePlacement
Notify    AnimNotify, AnimNotifyState
Domain    MontageEvaluator, MontagePlaybackState, MontageNotifyDispatcher
Runtime   ObjectAnimMontagePlayer
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

## 에디터 동작 옵션

Notify와 Notify State에는 `Trigger In Editor Scrub` 옵션이 있습니다.

- 꺼짐: 타임라인 재생 중에만 실행됩니다.
- 켜짐: 타임라인 바를 직접 움직이는 스크럽 중에도 실행됩니다.

Montage Editor의 재생 중에는 런타임과 같은 Dispatcher 흐름으로 Notify가 실행됩니다.

## 커스텀 트랙 / 커스텀 요소

기본 Animation Segment, Notify, Notify State 외에 별도 목적의 트랙과 요소를 만들 수 있습니다.

```csharp
[System.Serializable]
public sealed class MyTrack : MontageTimelineTrack
{
    public override string DisplayName => "My Track";
    public override Color EditorColor => Color.cyan;

    public float Strength = 1f;
}
```

```csharp
[System.Serializable]
public sealed class MyElement : MontageTimelineElement
{
    public override string DisplayName => "My Element";
    public override float DefaultDuration => 0.5f;

    public string MarkerName = "Hit";
}
```

Montage Editor에서 타임라인을 우클릭한 뒤 `Track/Add Custom Track...`으로 커스텀 트랙 타입을 고르고, 해당 트랙 위에서 `Create/Custom Element...`로 요소 타입을 골라 배치할 수 있습니다.

커스텀 트랙과 커스텀 요소는 ScriptableObject 에셋을 만들지 않습니다. Notify와 Notify State처럼 `[System.Serializable]` 클래스 타입을 선택하면 Montage 데이터 안에 `[SerializeReference]` 인스턴스로 저장되며, 배치별로 속성을 다르게 편집할 수 있습니다.

## Montage Editor

메뉴 경로:

```text
PJDev/Animation/Montage Editor
```

주요 기능:

- Montage SO 선택 및 편집
- Preview Mesh 기반 애니메이션 미리보기
- Animation Segment, Notify, Notify State 배치
- 커스텀 트랙/요소 배치
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


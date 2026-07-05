# AnimMontageSystem

언리얼 **Animation Montage + Notify** 워크플로를 Unity SO·MonoBehaviour로 제공합니다.

## 레이어

```
Data (SO)     AnimMontageSO, AnimNotifySO, MontageSegment …
Domain        MontageEvaluator, MontagePlaybackState, MontageNotifyDispatcher
Runtime       ObjectAnimMontagePlayer
Editor        AnimMontageEditorWindow (Preview + Timeline + Inspector)
```

## 런타임

```csharp
var player = GetComponent<ObjectAnimMontagePlayer>();
player.Play(montageSO);
player.SetTime(0.5f);
player.Pause(true);
```

Notify는 `AnimNotifySO` / `AnimNotifyStateSO`를 상속해 ScriptableObject로 작성합니다.

## 에디터

`PJDev/Animation/Montage Editor` — Montage SO 선택, 프리뷰, 타임라인에서 Notify 배치.

## 에셋 메뉴

| 메뉴 | 설명 |
|------|------|
| `PJDev/Animation/Montage` | Montage SO |
| `PJDev/Animation/Notify/Log` | 샘플 Notify |

# UI System 기본 UI

## 구성

- `UIProgressBar`: 로딩, 체력, 경험치 등에 재사용하는 진행률 UI
- `UIToastView`: Toast 큐, 중복 방지, 표시 정책을 관리하는 View
- `UIToastItem`: Toast 메시지 하나를 표시하는 재사용 Item
- `UILoadingView`: 진행률, 메시지, 취소, 입력 차단을 표시하는 View
- `LoadingHandle`: 로딩 작업의 진행률과 수명을 관리하는 Handle

## Toast 설정

1. `UIToastView`가 있는 프리팹을 만든다.
2. 메시지가 배치될 `Container`를 연결한다.
3. `UIToastItem` 프리팹 또는 자식 템플릿을 `Item Prefab`에 연결한다.
4. `UIViewCatalog`에 View ID `Toast`로 등록한다.
5. `Allow Multiple Instances`는 끈다.

```csharp
UIManager.Instance.ShowToast("저장했습니다.");
UIManager.Instance.ShowToast("인벤토리가 가득 찼습니다.", ToastType.Warning);
```

`Display Mode`에서 동작을 선택한다.

- `Queue`: 메시지를 하나씩 표시
- `Stack`: 여러 메시지를 동시에 표시
- `Replace`: 현재 메시지를 새 메시지로 교체

## Loading 설정

1. `UILoadingView`가 있는 프리팹을 만든다.
2. 메시지, `UIProgressBar`, 로딩 표시, 취소 버튼을 연결한다.
3. 입력을 막으려면 프리팹 루트에 화면 전체를 덮는 Raycast 대상 Graphic을 둔다.
4. `UIViewCatalog`에 View ID `Loading`으로 등록한다.
5. `Allow Multiple Instances`는 끈다.

```csharp
using LoadingHandle loading = UIManager.Instance.ShowLoading("로딩 중");

loading.SetMessage("캐릭터 생성 중");
loading.SetProgress(0.5f);
```

진행률을 모르는 작업은 기본 Indeterminate 상태를 사용한다.

```csharp
using LoadingHandle loading = UIManager.Instance.ShowLoading(
    "서버 연결 중",
    isIndeterminate: true,
    blockInput: true,
    cancelRequested: CancelConnection);
```

여러 `LoadingHandle`이 동시에 열려 있으면 마지막 Handle이 닫힐 때까지 Loading View가 유지된다.

## 커스텀

디자인은 프리팹에서 변경한다. 표시 동작을 바꿔야 할 때만 다음 클래스를 상속한다.

- `UIProgressBar`
- `UIToastItem`
- `UIToastView`
- `UILoadingView`

커스텀 View도 Catalog에서 `Toast` 또는 `Loading` View ID로 등록하면 기존 호출 코드를 바꿀 필요가 없다. 다른 종류의 Toast나 Loading 화면을 함께 사용해야 하면 호출 시 `viewId`를 지정한다.

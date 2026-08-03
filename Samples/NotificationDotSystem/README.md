# Notification Dot System Example

## 빠른 확인

1. 빈 GameObject에 `NotificationDotExampleController`를 추가합니다.
2. 다른 GameObject에 `NotificationDotPresenter`를 추가하고 `Spawn Point`를 연결합니다.
3. 감시 목록의 `+` 버튼에서 `ExampleNotification` 값들을 추가하고 Priority를 지정합니다.
4. `NotificationDotExampleViewRegistry`에 표시할 프리팹을 연결합니다.
5. Play Mode에서 Controller의 Context Menu로 알림 개수를 변경합니다.

`Inbox`는 `Mail`에, `FreeItem`과 `DailyReward`는 `Shop`에 자동 합산됩니다. 여러 알림이 활성화되더라도 Presenter는 Priority가 가장 높은 하나만 표시합니다.

## 커스텀 프리팹

프리팹에 `INotificationDotView`를 구현한 컴포넌트를 붙이면 선택된 키와 개수를 받을 수 있습니다. 숫자만 표시하면 기본 `NotificationDotCountView`를 사용할 수 있습니다.

프리팹 등록 컴포넌트는 `NotificationDotViewInstaller`를 상속합니다. 등록 결과는 컴포넌트가 비활성화될 때 자동으로 해제됩니다.

`NotificationDotPresenter`는 표시할 프리팹을 `PrefabPool`에서 자동으로 가져오고, 알림이 사라지면 바로 반환합니다. 풀을 직접 관리할 필요는 없습니다. 커스텀 뷰가 애니메이션 같은 내부 상태를 초기화해야 한다면 `IPoolable`을 함께 구현하면 됩니다.

```csharp
public sealed class MenuDotViews : NotificationDotViewInstaller
{
    [SerializeField] private GameObject rewardPrefab;

    protected override void RegisterViews()
    {
        Register(ExampleNotification.DailyReward, rewardPrefab);
    }
}
```
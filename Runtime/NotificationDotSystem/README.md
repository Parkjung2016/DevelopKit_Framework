# Notification Dot System

메뉴 배지, 읽지 않은 메시지, 받을 수 있는 보상처럼 개수 기반 알림을 관리합니다.

## Enum 정의

각 enum 타입이 자동으로 하나의 알림 그룹이 됩니다. 별도의 그룹 이름은 설정하지 않으며, 값별 옵션만 `NotificationDot`으로 작성합니다.

```csharp
using PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime;

[NotificationDot]
public enum MenuDots
{
    Mail,

    [NotificationDot(
        Parent = nameof(Mail),
        ClearOnVisit = true,
        ViewKey = "InboxBadge")]
    Inbox,

    [NotificationDot(Parent = nameof(Mail))]
    Reward
}
```

`Inbox` 또는 `Reward`가 활성화되면 부모인 `Mail`에도 개수가 자동으로 합산됩니다. 자식 목록은 따로 관리하지 않으며, 계층이 필요한 enum 값에만 `Parent`를 지정합니다.

다른 enum의 값을 부모로 지정해도 됩니다. 부모 이름이 프로젝트에서 유일하면 `Parent = nameof(OtherDots.Reward)`만으로 연결됩니다. 같은 이름이 여러 enum에 있다면 `ParentType = typeof(OtherDots)`을 함께 지정합니다.

부모와 자식 enum에 같은 값 이름이 있다면 enum 값을 직접 전달하는 타입 안전 문법을 권장합니다.

```csharp
[NotificationDot(
    MenuDots.Reward,
    Relation = NotificationDotRelation.Parent)]
FreeItem
```

```csharp
[NotificationDot]
public enum EventDots
{
    [NotificationDot(
        Parent = nameof(MenuDots.Reward),
        ParentType = typeof(MenuDots))]
    FreeItem
}
```

## 방문 처리

```csharp
NotificationDots.SetActive(MenuDots.Inbox, true);
NotificationDots.Visit(MenuDots.Inbox);
```

`ClearOnVisit = true`인 알림은 `Visit`을 호출하면 사라지고, 새 값이 들어오면 다시 표시됩니다.

## 알림 간 종속성

같은 `NotificationDot` 생성자로 다른 enum 값이나 런타임 키를 연결합니다.

```csharp
public enum QuestDots
{
    Reward
}

public enum LobbyDots
{
    [NotificationDot(QuestDots.Reward)]
    QuestMenu,

    [NotificationDot(
        typeof(QuestDots),
        nameof(QuestDots.Reward),
        NotificationDotDependencyMode.Count)]
    QuestCount,

    [NotificationDot("Event/Summer/Reward")]
    EventMenu
}
```

기본 `Active` 모드는 원본이 활성화되면 1을 더합니다. `Count` 모드는 원본 개수를 그대로 반영합니다. 여러 종속성이 필요하면 `NotificationDot`을 여러 번 붙이면 됩니다. 순환 종속성은 등록할 때 예외로 알려줍니다.

## UI 표현

Scene에는 `NotificationDotPresenter` 하나만 배치합니다. Inspector에서 `Spawn Point`와 감시할 enum 값, Priority를 설정하면 활성 항목 중 Priority가 가장 높은 하나만 표시합니다. 같은 Priority라면 목록 위쪽 항목이 우선합니다.

프리팹은 Presenter가 아니라 초기화 코드에서 알림 값과 연결합니다.

```csharp
using PJDev.DevelopKit.Framework.NotificationDotSystem.UI;

public sealed class MenuDotViews : NotificationDotViewInstaller
{
    [SerializeField] private GameObject rewardBadgePrefab;

    protected override void RegisterViews()
    {
        Register(MenuDots.Reward, rewardBadgePrefab);
    }
}
```

프리팹에 `INotificationDotView`를 구현하면 선택된 키와 개수를 받을 수 있습니다. 숫자만 표시하면 `NotificationDotCountView`를 사용할 수 있습니다. 여러 알림에서 같은 프리팹 키를 공유해야 할 때만 `ViewKey` 등록 방식을 사용합니다.
## 런타임 타입 등록

Enum에 없는 이벤트성 알림도 실행 중에 추가할 수 있습니다.

```csharp
private NotificationDotRegistration summerEventDot;

void Start()
{
    summerEventDot = NotificationDots.Register(
        new NotificationDotDefinition("Event/Summer/Reward")
            .ClearOnVisit()
            .UseView("EventRewardBadge")
            .DependsOn(MenuDots.Reward));
}

void OnDestroy()
{
    summerEventDot?.Dispose();
}
```

## 기본 API

```csharp
NotificationDots.SetCount(MenuDots.Inbox, 3);
NotificationDots.Add(MenuDots.Reward);
NotificationDots.Clear(MenuDots.Inbox);

int childCount = NotificationDots.GetCount(MenuDots.Inbox);
int parentCount = NotificationDots.GetCount(MenuDots.Mail);
int directCount = NotificationDots.GetDirectCount(MenuDots.Mail);
int allCount = NotificationDots.GetCount<MenuDots>();
```

Enum 경로와 어트리뷰트는 타입별 최초 사용 시 한 번만 분석하고 캐시합니다. 반복 호출에는 Reflection이나 경로 문자열 생성이 없습니다.

## 확인 도구

`PJDev > Notification Dots > Runtime Monitor`에서는 활성 개수, 방문 정책, `ViewKey`, 종속성을 확인할 수 있습니다.

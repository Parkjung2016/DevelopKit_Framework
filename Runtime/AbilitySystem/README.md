# Ability System

Ability 등록, 활성화 조건, Gameplay Tag, Stat 비용과 Effect 실행을 한 흐름으로 관리합니다. Ability 에셋은 객체마다 런타임 복제본을 만들어 사용하므로 원본 SO에 실행 상태가 저장되지 않습니다.

## 구성

- `ObjectAbilitySystem`: Ability 등록, 활성화, 종료와 입력 연결을 관리합니다.
- `AbilitySO`: Ability 설정과 커스텀 실행 코드를 정의합니다.
- `AbilityContext`: owner, source Stat, target Stat을 Effect와 Ability에 전달합니다.
- `AbilityStatCost`: 활성화 전에 필요한 Stat을 검사하고 기본값에서 차감합니다.
- `AbilityEffect`: Ability에 붙일 수 있는 일반 클래스 기반 확장 지점입니다.
- `StatAbilityEffect`: 기본값 변경과 활성 Modifier를 제공합니다.

## Stat 연결

Ability를 사용하는 오브젝트에 `ObjectStatSystem`을 함께 추가하면 자동으로 source Stat으로 연결됩니다. 다른 대상의 Stat을 변경할 때는 활성화하면서 대상의 `ObjectStatSystem`을 전달합니다.

```csharp
abilitySystem.TryActivateAbility(attackAbility, enemyStats);
```

`StatAbilityEffect`의 Target을 `Self`로 설정하면 사용자 Stat을, `Target`으로 설정하면 전달받은 대상 Stat을 변경합니다. 대상이 생략되면 source Stat을 사용합니다.

## Stat 변경 종류

`Mode`에서 변경 대상을 먼저 선택합니다.

- `BaseValue`: 기본값을 직접 변경합니다. Add, Set, AddPercent 중 하나를 선택합니다.
- `Modifier`: BaseValue는 유지하고 Flat Amount와 Percent를 최종값 계산에 반영합니다. Ability 종료 시 자동 제거됩니다.

Modifier 계산은 StatSystem 규칙을 그대로 사용합니다.

```text
(BaseValue + Flat) * (1 + Percent / 100)
```

## Stat 비용

Ability의 Stat Costs에 마나, 스태미나 등의 비용을 등록할 수 있습니다. 고정 Amount와 Percent를 함께 사용할 수 있으며, Percent 기준은 현재 BaseValue 또는 MaxValue 중에서 선택합니다. 같은 Stat 비용이 여러 개면 합산한 뒤 활성화 가능 여부를 먼저 검사하므로 일부 비용만 차감되는 일이 없습니다.

비용은 `BaseValue`에서 차감하며 Stat의 최소값 아래로 내려가야 한다면 Ability가 실행되지 않습니다.

## 커스텀 Effect

SO를 추가로 만들지 않고 일반 직렬화 클래스로 확장합니다.

```csharp
[Serializable]
public sealed class DamageEventEffect : AbilityEffect
{
    public override void Apply(in AbilityContext context)
    {
        // 프로젝트의 데미지 이벤트 전달
    }
}
```

컴파일되면 Ability Inspector의 Effects 추가 메뉴에 자동으로 나타납니다. 지속 효과는 `RemoveWhenAbilityEnds`를 `true`로 반환하고 `Remove`에서 적용한 상태를 정리합니다.

## 종료 규칙

Ability 구현에서 작업이 끝나면 `EndAbility()`를 호출합니다. Ability 제거, 컴포넌트 비활성화, 오브젝트 제거 시에도 활성 Ability가 종료되며 지속 Modifier가 정리됩니다.

# Pool System

반복 생성되는 객체와 컬렉션을 재사용하는 범용 풀 시스템입니다. 런타임 할당과 `Instantiate`/`Destroy` 호출을 줄이면서, 객체의 소유권과 반환 시점을 코드에서 분명하게 드러내는 것을 목표로 합니다.

## 일반 객체

`Pool<T>`는 클래스 인스턴스를 재사용합니다. `create`는 필수이며 대여, 반환, 제거 시 실행할 콜백을 선택해서 전달할 수 있습니다.

```csharp
var pool = new Pool<BulletData>(
    create: () => new BulletData(),
    onRent: item => item.Reset(),
    maxSize: 64);

BulletData item = pool.Rent();
pool.Return(item);
```

`collectionCheck`가 켜진 기본 상태에서는 다른 풀의 객체 반환과 중복 반환을 예외로 알려줍니다. 검사가 필요 없는 성능 민감 경로에서만 `false`를 사용하세요.

## 컬렉션

임시 컬렉션은 `ListPool<T>`, `HashSetPool<T>`, `DictionaryPool<TKey, TValue>`로 대여합니다. 반환할 때 내용은 자동으로 비워집니다.

```csharp
using (ListPool<Enemy>.Rent(out List<Enemy> enemies))
{
    FindEnemies(enemies);
    Process(enemies);
}
```

`using`을 쓰기 어려운 경우 `Rent()`와 `Return()`을 직접 짝지어 호출할 수 있습니다.

## Prefab

여러 시스템에서 같은 Prefab 풀을 공유하려면 `PrefabPool`을 사용합니다.

```csharp
GameObject effect = PrefabPool.Spawn(effectPrefab, position, rotation);
PrefabPool.Release(effect);
```

자식 오브젝트를 넘겨도 해당 인스턴스의 풀을 찾아 반환합니다. 풀에서 생성하지 않은 오브젝트는 `Release`가 `false`를 반환하므로 필요한 경우 기존 제거 로직을 폴백으로 사용할 수 있습니다.

한 시스템이 풀의 수명까지 직접 관리해야 한다면 `GameObjectPool`을 생성하고 `Dispose()`로 정리합니다.

```csharp
using var projectilePool = new GameObjectPool(projectilePrefab, initialCapacity: 16, maxSize: 128);
GameObject projectile = projectilePool.Spawn(position, rotation);
projectilePool.Return(projectile);
```

## 생명 주기

Prefab의 컴포넌트가 `IPoolable`을 구현하면 다음 시점에 자동 호출됩니다.

- `OnSpawned()`: 오브젝트가 활성화된 직후
- `OnDespawned()`: 오브젝트가 비활성화되기 직전

콜백 컴포넌트 목록은 인스턴스를 만들 때 한 번만 찾아서 보관합니다. 런타임에 `IPoolable` 컴포넌트를 추가하거나 제거했다면 다음 생성 인스턴스부터 반영됩니다.

## 미리 생성

`PrefabPoolSettingsSO`에 Prefab, 미리 생성할 개수, 최대 보관 개수를 설정하고 `PrefabPoolPreloader`에 연결하면 `Awake`에서 미리 생성됩니다. 같은 Prefab은 설정 목록에 한 번만 등록하세요.

Play Mode에서는 `PJDev > Pool System > Monitor`에서 각 Prefab 풀의 활성, 비활성, 전체 개수를 확인하고 비활성 인스턴스를 정리할 수 있습니다.

## 사용 규칙

- 대여한 객체는 반드시 대여한 풀에 반환합니다.
- 풀 객체를 직접 `Destroy`하지 않는 것이 기본입니다. 외부에서 제거되더라도 개수는 보정되지만 재사용 이점은 사라집니다.
- 정적 `PrefabPool`은 메인 스레드에서 사용합니다.
- UI View처럼 Addressables 해제나 별도 수명 규칙이 있는 객체는 해당 시스템의 수명 관리 계약을 유지합니다.
- 풀 크기는 실제 동시 사용량을 기준으로 잡습니다. 지나친 미리 생성은 시작 시간과 메모리 사용량을 늘립니다.

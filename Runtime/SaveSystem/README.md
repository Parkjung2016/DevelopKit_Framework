# Save System

슬롯 ID를 기준으로 데이터를 저장하고 불러오는 범용 로컬 저장 모듈입니다.

직렬화, 암호화, 저장 위치가 인터페이스로 분리되어 있어 프로젝트마다 필요한 구현만 교체할 수 있습니다.

## 주요 타입

| 타입 | 역할 |
|---|---|
| `SaveManager` | 저장, 불러오기, 삭제를 제공하는 진입점 |
| `ISaveSerializer` | 객체와 바이트 데이터 사이의 변환 |
| `ISaveEncryption` | 저장 데이터 암호화와 복호화 |
| `ISaveStorage` | 슬롯 데이터의 동기 읽기와 쓰기 |
| `IAsyncSaveStorage` | 슬롯 데이터의 비동기 읽기와 쓰기 |
| `SaveSettingsSO` | 기본 로컬 저장 구성을 만드는 설정 에셋 |
| `SaveResult` | 저장과 삭제 결과 |
| `LoadResult<T>` | 불러오기 결과와 데이터 |

## 빠른 시작

### 암호화 저장

```csharp
SaveManager save = SaveManager.CreateEncrypted("project-secret");

var data = new PlayerSaveData
{
    Level = 5,
    Gold = 1200
};

SaveResult saveResult = save.Save("profile-1", data);
if (!saveResult.IsSuccess)
    Debug.LogError(saveResult.Message);

LoadResult<PlayerSaveData> loadResult = save.Load<PlayerSaveData>("profile-1");
if (loadResult.IsSuccess)
    Apply(loadResult.Value);
```

### 암호화하지 않는 저장

```csharp
SaveManager save = SaveManager.CreateUnencrypted();
```

기본 저장 위치는 `Application.persistentDataPath/Saves`이며 확장자는 `.sav`입니다.

## 설정 에셋

1. `Create > PJDev > Save System > Settings`에서 `SaveSettingsSO`를 생성합니다.
2. `Folder Name`, 파일 확장자와 암호화 사용 여부를 설정합니다. 암호화를 켰다면 `Encryption Password`도 입력합니다.
3. `CreateManager()`로 `SaveManager`를 생성합니다.

- `Folder Name`: `Application.persistentDataPath` 아래에 만들 폴더 이름입니다. 절대 경로를 저장하지 않으므로 다른 PC와 프로젝트에서도 설정 에셋을 재사용할 수 있습니다.
- `Encryption Password`: 저장 파일 암호화에 사용하는 프로젝트 전용 비밀번호입니다. 배포 후 변경하면 이전 암호화 파일을 열 수 없습니다.
- `Resolved Directory`: 현재 플랫폼에서 계산된 실제 저장 위치이며 읽기 전용입니다.

```csharp
[SerializeField] private SaveSettingsSO saveSettings;

private SaveManager save;

private void Awake()
{
    save = saveSettings.CreateManager();
}
```

## 바이트 데이터

이미 직렬화된 데이터는 별도 변환 없이 저장할 수 있습니다.

```csharp
save.SaveBytes("inventory", bytes);

LoadResult<byte[]> result = save.LoadBytes("inventory");
if (result.IsSuccess)
    RestoreInventory(result.Value);
```

## 비동기 저장과 불러오기

로컬 파일 저장소는 실제 비동기 파일 I/O를 사용하므로 큰 슬롯을 처리할 때 메인 스레드를 막지 않습니다.

```csharp
using CancellationTokenSource cancellation = new();

SaveResult saveResult = await save.SaveAsync(
    "profile-1",
    data,
    cancellation.Token);

LoadResult<PlayerSaveData> loadResult = await save.LoadAsync<PlayerSaveData>(
    "profile-1",
    cancellation.Token);
```

`SaveBytesAsync`, `LoadBytesAsync`, `DeleteAsync`도 같은 방식으로 사용할 수 있습니다.

- 취소되면 `OperationCanceledException`을 전달합니다.
- `LocalFileSaveStorage`의 동기·비동기 쓰기와 삭제는 같은 잠금을 사용합니다.
- 커스텀 저장소가 `IAsyncSaveStorage`를 구현하지 않으면 비동기 API는 해당 저장소의 동기 메서드를 호출합니다.
- 직렬화와 역직렬화는 호출자 컨텍스트에서 실행하고 파일 I/O만 비동기로 처리합니다.
## 결과 처리

실패 시 예외를 외부로 던지거나 강제로 로그를 출력하지 않습니다. 호출한 코드가 결과를 보고 처리합니다.

```csharp
LoadResult<PlayerSaveData> result = save.Load<PlayerSaveData>("profile");

switch (result.Error)
{
    case SaveError.None:
        Apply(result.Value);
        break;
    case SaveError.SlotNotFound:
        CreateNewProfile();
        break;
    default:
        Debug.LogError(result.Message);
        break;
}
```

## 데이터 안전성

- 파일은 임시 파일에 완전히 기록한 뒤 기존 슬롯과 교체합니다.
- 파일 헤더에는 버전, 암호화 여부, 데이터 길이와 CRC32 체크섬이 들어갑니다.
- 암호화는 AES-256-CBC를 사용합니다.
- HMAC-SHA256으로 암호문을 검증하므로 잘못된 키와 변조된 데이터를 복호화 전에 거부합니다.
- 슬롯 ID에 경로 문자나 제어 문자가 포함되면 `InvalidSlotId`를 반환합니다.

현재 파일 포맷은 버전 2입니다. 이전 포맷을 읽는 레거시 코드나 자동 마이그레이션은 포함하지 않습니다.

## 커스텀 구현

```csharp
var save = new SaveManager(
    customSerializer,
    cloudStorage,
    customEncryption);
```

`SaveSettingsSO`의 로컬 저장 위치만 재사용하거나 커스텀 직렬화기를 결합할 수도 있습니다.

```csharp
LocalFileSaveStorage storage = saveSettings.CreateStorage();
SaveManager saveFromSettings = saveSettings.CreateManager(customSerializer);
```

| 인터페이스 | 교체 예 |
|---|---|
| `ISaveSerializer` | MessagePack, Newtonsoft JSON |
| `ISaveStorage` | 동기 클라우드 저장소, 데이터베이스 |
| `IAsyncSaveStorage` | 비동기 클라우드 저장소, 데이터베이스 |
| `ISaveEncryption` | 프로젝트 전용 암호화 |

기본 `JsonSaveSerializer`는 Unity `JsonUtility`를 사용하므로 저장 클래스에 `[Serializable]`이 필요합니다.

## 성능

- 저장 파일 교체 외에는 불필요한 파일 복사를 만들지 않습니다.
- CRC 테이블은 한 번만 생성해 재사용합니다.
- PBKDF2 키 생성은 `SaveManager`를 만들 때 한 번만 수행합니다.
- `SaveManager`를 저장할 때마다 새로 만들지 말고 재사용하세요.

## Save Slot Browser

`PJDev > Save System > Slot Browser`에서 로컬 슬롯을 관리할 수 있습니다.

- `SaveSettingsSO`를 선택하면 계산된 Directory와 Extension을 읽기 전용으로 표시합니다.
- 마지막으로 선택한 `SaveSettingsSO`는 에셋 GUID로 저장되어 창을 다시 열어도 복원됩니다.
- 파일 검증은 백그라운드에서 실행되며 목록은 가상화되어 슬롯이 많아도 창 조작을 막지 않습니다.
- 버전, 암호화 여부, payload 크기, CRC32와 수정 시간을 확인할 수 있습니다.
- 검색, 새로고침, 폴더 열기, 파일 위치 표시와 슬롯 삭제를 지원합니다.
- `F5`는 새로고침, `Delete`는 선택 슬롯 삭제, `Ctrl+F`는 검색 포커스입니다.
- `SaveSettingsSO`를 더블클릭하면 해당 설정으로 Slot Browser가 열립니다.
## 테스트

`Tests` 폴더의 Unity Edit Mode 테스트에서 다음 항목을 검증합니다.

- 객체와 바이트 데이터 왕복
- 잘못된 키와 암호문 변조 검출
- 파일 체크섬 손상 검출
- 저장소 실패 결과
- 메모리 저장소 데이터 격리
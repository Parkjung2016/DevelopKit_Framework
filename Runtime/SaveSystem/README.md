# SaveSystem

로컬 파일 기반 **범용 세이브** 모듈입니다. 어떤 `[Serializable]` 데이터든 슬롯 ID로 저장·로드할 수 있고, AES-256 암호화를 선택적으로 적용합니다.

## 구성

| 타입 | 역할 |
|------|------|
| `SaveSystem` | `Save<T>` / `TryLoad<T>` / `Delete` 진입점 |
| `ISaveSerializer` | 객체 ↔ `byte[]` (`JsonSaveSerializer`) |
| `ISaveEncryptor` | `byte[]` 암·복호화 (`AesSaveEncryptor`, `NullSaveEncryptor`) |
| `ISaveStorage` | 슬롯 읽기/쓰기 (`LocalFileSaveStorage`, `InMemorySaveStorage`) |
| `SaveSetupSO` | 패스프레이즈·경로·암호화 on/off 설정 |

## 빠른 시작

```csharp
// 기본: persistentDataPath/Saves + AES + JsonUtility
SaveSystem save = SaveSystem.CreateDefault(passphrase: "my-secret");

var data = new PlayerSaveData { Level = 5, Gold = 1200 };
save.Save("profile-1", data);

SaveLoadResult<PlayerSaveData> loaded = save.TryLoad<PlayerSaveData>("profile-1");
if (loaded.Success)
    Apply(loaded.Value);
```

### ScriptableObject 설정

1. `Create > PJDev/SO/SaveSystem/Setup` → `SaveSetupSO`
2. `Passphrase`, `UseEncryption`, 저장 경로 설정
3. `saveSetup.CreateSaveSystem()` 으로 인스턴스 생성

## 데이터 타입 요구사항

- `JsonSaveSerializer`는 Unity `JsonUtility`를 사용합니다.
- **클래스에 `[Serializable]`** 이 필요합니다.
- `Dictionary` 등 JsonUtility 미지원 타입은 커스텀 `ISaveSerializer`를 주입하세요.

## Raw 바이트 API

이미 직렬화된 바이트(예: Inventory `ExportSaveData` 결과를 자체 JSON으로 변환)를 그대로 저장할 때:

```csharp
save.SaveRaw("inventory", plainBytes);
save.TryLoadRaw("inventory");
```

## 파일 포맷

```
[PJDS][flags+version][payload]
```

- `payload`는 AES 사용 시 IV(16) + cipher
- 잘못된 키·손상 파일은 `SaveFailReason.DecryptionFailed` / `InvalidFormat`

## 보안 참고

- 로컬 세이브 **난독화·변조 방지 보조** 수준입니다. 서버 검증이 필요한 값은 서버에서 재검증하세요.
- 프로덕션에서는 `SaveSetupSO.Passphrase` 대신 프로젝트별 비밀값·키 유도 로직을 사용하세요.
- 개발 중 암호화 끄기: `SaveSystem.CreateUnencrypted()` 또는 `UseEncryption = false`

## 다른 시스템 연동 예

```csharp
InventoryGroupSaveData inventory = inventorySystem.ExportGroupSaveData();
save.Save("player-inventory", inventory);

SaveLoadResult<InventoryGroupSaveData> restored = save.TryLoad<InventoryGroupSaveData>("player-inventory");
if (restored.Success)
    inventorySystem.ImportGroupSaveData(restored.Value);
```

## 확장

| 인터페이스 | 용도 |
|-----------|------|
| `ISaveSerializer` | MessagePack, Newtonsoft 등 교체 |
| `ISaveEncryptor` | 커스텀 암호화 |
| `ISaveKeyProvider` | 키 유도 (패스프레이즈, 디바이스 ID + salt) |
| `ISaveStorage` | Cloud, PlayerPrefs 래퍼 |

## 테스트

`Tests/` — Unity Edit Mode (NUnit)

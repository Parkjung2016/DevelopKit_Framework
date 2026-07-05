# Shared

프레임워크 모듈 간 **공유 primitive**. 다른 Framework 모듈에 의존하지 않습니다.

- `GlobalRegistry<T>` — Catalog 공통 구현 (`[AutoStaticsCleanup]`, Unity 6000.5+)
- `FrameworkInitOptions` — Init 동작 플래그
- `FrameworkPlayModeCleanup` — Unity 6000.5 미만 Play Mode static 정리 레지스트리

Domain Reload 없이 Play Mode를 반복할 때 static 상태는 Unity lifecycle API(6000.5+) 또는 `FrameworkPlayModeCleanup`(구버전)으로 초기화됩니다.

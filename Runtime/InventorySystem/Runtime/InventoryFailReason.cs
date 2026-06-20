namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public enum InventoryFailReason
    {
        None,                     // 실패 없음
        InvalidItemId,            // 아이템 ID가 유효하지 않음
        InvalidCount,             // 수량 값이 유효하지 않음(0 이하 등)
        InvalidSlotIndex,         // 슬롯 인덱스가 범위를 벗어남
        DatabaseNotReady,         // 데이터베이스/컨테이너 상태가 준비되지 않음
        DefinitionNotFound,       // 아이템 정의를 찾을 수 없음
        SlotMismatch,             // 지정 슬롯의 아이템과 요청 아이템이 일치하지 않음
        NoSpace,                  // 아이템을 넣을 공간이 없음
        NoChange,                 // 처리 후 상태 변화가 없음
        ItemNotFound,             // 인벤토리에 해당 아이템이 없음
        InsufficientItemCount,    // 보유 수량이 부족함
        SlotEmpty,                // 대상 슬롯이 비어 있음
        CapacityRuleDenied,       // 용량 규칙에 의해 거절됨(일반)
        ItemTypeNotAllowed,       // 슬롯 규칙상 아이템 타입이 허용되지 않음
        WeightLimitExceeded,      // 무게 제한을 초과함
        OccupiedSlotLimitReached, // 점유 슬롯 제한에 도달함
        SlotRuleDenied,           // 슬롯 규칙에 의해 거절됨(일반)
        ItemActionDenied,         // 아이템 액션(사용/드롭/거래)이 허용되지 않음
        ContainerNotFound,        // 대상 컨테이너를 찾을 수 없음
        RecipeNotFound,           // 레시피를 찾을 수 없음
        LootTableNotFound         // 루트 테이블을 찾을 수 없음
    }
}

#if UNITASK_INSTALLED
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>UI 뷰의 공통 계약입니다.</summary>
    public interface IUIView
    {
        /// <summary>뷰 고유 ID입니다. 카탈로그·디버깅에 사용합니다.</summary>
        string ViewId { get; }

        /// <summary><see cref="UILayerSettings"/>에 정의된 레이어 ID입니다.</summary>
        string LayerId { get; }

        /// <summary>같은 레이어 내 정렬·Back 우선순위입니다. 값이 클수록 우선합니다.</summary>
        int Priority { get; }

        /// <summary>현재 표시 상태입니다.</summary>
        UIViewState State { get; }

        /// <summary>뷰가 화면에 보이는지 여부입니다.</summary>
        bool IsVisible { get; }

        /// <summary>Back 입력 시 이 뷰의 처리 방식입니다.</summary>
        UIViewBackBehavior BackBehavior { get; }

        /// <summary>Back 입력 시 이 뷰를 닫을지 여부입니다.</summary>
        bool CloseOnBack { get; }

        /// <summary>Back 입력이 하위 UI로 전달되지 않도록 막는지 여부입니다.</summary>
        bool BlocksBack { get; }

#if UNITASK_INSTALLED
        /// <summary>뷰를 표시합니다.</summary>
        UniTask Show(object context = null, CancellationToken cancellationToken = default);

        /// <summary>뷰를 숨깁니다.</summary>
        UniTask Hide(bool immediate = false, CancellationToken cancellationToken = default);
#else
        /// <summary>뷰를 표시합니다.</summary>
        void Show(object context = null);

        /// <summary>뷰를 숨깁니다.</summary>
        void Hide(bool immediate = false);
#endif

        /// <summary>Back 입력을 처리합니다. 처리했으면 true를 반환합니다.</summary>
        bool HandleBack();
    }
}

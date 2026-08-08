#if UNITASK_INSTALLED
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>UI View가 공통으로 제공해야 하는 기능입니다.</summary>
    public interface IUIView
    {
        /// <summary>카탈로그와 중복 인스턴스 관리에 사용하는 고유 ID입니다.</summary>
        string ViewId { get; }

        /// <summary>View가 표시될 UI 레이어 ID입니다.</summary>
        string LayerId { get; }

        /// <summary>같은 레이어에서의 표시 및 Back 처리 우선순위입니다.</summary>
        int Priority { get; }

        /// <summary>현재 View 상태입니다.</summary>
        UIViewState State { get; }

        /// <summary>View가 화면에 표시 중인지 확인합니다.</summary>
        bool IsVisible { get; }

        /// <summary>Back 입력을 받았을 때의 동작입니다.</summary>
        UIViewBackBehavior BackBehavior { get; }

        /// <summary>Back 입력으로 이 View를 닫는지 확인합니다.</summary>
        bool CloseOnBack { get; }

        /// <summary>Back 입력이 아래 View로 전달되지 않도록 막는지 확인합니다.</summary>
        bool BlocksBack { get; }

#if UNITASK_INSTALLED
        /// <summary>View를 표시합니다.</summary>
        UniTask Show(object context = null, CancellationToken cancellationToken = default);

        /// <summary>View를 숨깁니다.</summary>
        UniTask Hide(bool immediate = false, CancellationToken cancellationToken = default);
#else
        /// <summary>View를 표시합니다.</summary>
        void Show(object context = null);

        /// <summary>View를 숨깁니다.</summary>
        void Hide(bool immediate = false);
#endif

        /// <summary>Back 입력을 처리했으면 true를 반환합니다.</summary>
        bool HandleBack();
    }
}

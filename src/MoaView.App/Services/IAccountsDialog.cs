using MoaView.Core.Entities;

namespace MoaView.App.Services;

/// <summary>서비스의 계정 관리 창을 연다(06 UI/UX 3절 계정 탭·3.1 편집 대화상자). 창이 닫힐 때까지 기다린다.</summary>
public interface IAccountsDialog
{
    Task ShowAsync(StreamingService service);
}

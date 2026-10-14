namespace MoaView.App.Services;

/// <summary>설정 창(06 UI/UX 3절)을 모달로 열고, 닫힌 뒤 메인 화면이 반영해야 할 변경을 돌려준다.</summary>
public interface ISettingsDialog
{
    Task<SettingsResult> ShowAsync();
}

/// <summary>
/// 설정 창에서 바뀐 것. ChangedServiceIds에 든 서비스는 엔티티가 바뀌었으므로 열려 있던 WebView를 해제하고 다시 만든다.
/// ServicesChanged는 순서·활성화·추가·삭제까지 포함해 사이드바를 다시 읽어야 하는지 뜻한다.
/// </summary>
public sealed record SettingsResult(IReadOnlySet<int> ChangedServiceIds, bool ServicesChanged)
{
    public static SettingsResult None { get; } = new(new HashSet<int>(), false);
}

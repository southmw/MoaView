using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MoaView.App.Views;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;

namespace MoaView.App.ViewModels;

/// <summary>서비스 하나의 계정 목록(FR-ACC-01, 05, 06). 비밀번호는 저장 시 DPAPI로 암호화하고 이 클래스는 평문을 보관하지 않는다.</summary>
public sealed partial class AccountsViewModel(
    StreamingService service,
    IAccountRepository repository,
    ICredentialProtector protector,
    string primaryLoginProvider,
    ILogger<AccountsViewModel> logger) : ObservableObject
{
    public StreamingService Service { get; } = service;

    public string Title => $"{Service.Name} 계정 관리";

    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];

    [ObservableProperty]
    public partial AccountItemViewModel? Selected { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }

    public async Task LoadAsync()
    {
        var list = await repository.GetByServiceAsync(Service.Id);
        Accounts.Clear();
        foreach (var account in list)
        {
            Accounts.Add(new AccountItemViewModel(account));
        }

        Selected = Accounts.FirstOrDefault();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var draft = new Account
        {
            ServiceId = Service.Id,
            DisplayName = "내 계정",
            LoginId = string.Empty,
            LoginProvider = primaryLoginProvider,
            ProfileName = NextProfileName(),
            IsDefault = Accounts.Count == 0,
        };

        if (AccountEditDialog.Edit(draft, isNew: true) is { } result)
        {
            await SaveAsync(draft, result.Password);
        }
    }

    [RelayCommand]
    private async Task EditAsync(AccountItemViewModel? item)
    {
        item ??= Selected;
        if (item is null)
        {
            return;
        }

        var account = await repository.GetByIdAsync(item.Account.Id);
        if (account is null)
        {
            await LoadAsync();
            return;
        }

        if (AccountEditDialog.Edit(account, isNew: false) is { } result)
        {
            await SaveAsync(account, result.Password);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(AccountItemViewModel? item)
    {
        item ??= Selected;
        if (item is null)
        {
            return;
        }

        await repository.DeleteAsync(item.Account.Id);
        logger.LogInformation("[{Service}] 계정 삭제: {Id}", Service.Code, item.Account.Id);
        Status = $"'{item.DisplayName}' 계정을 삭제했습니다.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SetDefaultAsync(AccountItemViewModel? item)
    {
        item ??= Selected;
        if (item is null || item.IsDefault)
        {
            return;
        }

        var account = await repository.GetByIdAsync(item.Account.Id);
        if (account is null)
        {
            return;
        }

        account.IsDefault = true;
        await repository.SaveAsync(account);
        await LoadAsync();
    }

    private async Task SaveAsync(Account account, string? newPassword)
    {
        if (!string.IsNullOrEmpty(newPassword))
        {
            account.EncryptedPassword = protector.Protect(newPassword);
            // 비밀번호를 바꿨으면 실패 횟수를 초기화한다(04 아키텍처 5.3절 LoginGuard).
            account.LoginFailCount = 0;
        }

        await repository.SaveAsync(account);
        logger.LogInformation("[{Service}] 계정 저장: Id={Id}, 기본={Default}, 자동입력={Fill}, 자동제출={Submit}",
            Service.Code, account.Id, account.IsDefault, account.AutoFillEnabled, account.AutoSubmitEnabled);
        Status = $"'{account.DisplayName}' 계정을 저장했습니다.";
        await LoadAsync();
        Selected = Accounts.FirstOrDefault(a => a.Account.Id == account.Id);
    }

    /// <summary>첫 계정은 서비스 프로필(svc_{code})을 그대로 써서 현재 세션과 맞춘다. 추가 계정은 고유 접미사를 붙인다(D-11).</summary>
    private string NextProfileName() =>
        Accounts.Count == 0 ? Service.ProfileName : $"{Service.ProfileName}_{Guid.NewGuid():N}"[..Math.Min(40, Service.ProfileName.Length + 33)];
}

public sealed class AccountItemViewModel(Account account)
{
    public Account Account { get; } = account;

    public string DisplayName => Account.DisplayName;

    public string LoginId => Account.LoginId;

    public string LoginProviderText => Account.LoginProvider switch
    {
        LoginProviders.Native => "서비스 ID",
        LoginProviders.Naver => "네이버",
        LoginProviders.Kakao => "카카오",
        LoginProviders.CjOne => "CJ ONE",
        LoginProviders.Google => "Google",
        LoginProviders.TId => "T 아이디",
        var other => other,
    };

    public bool IsDefault => Account.IsDefault;

    public bool HasPassword => Account.EncryptedPassword is { Length: > 0 };

    public string AutoText =>
        !Account.AutoFillEnabled ? "꺼짐" : Account.AutoSubmitEnabled ? "입력 + 제출" : "입력만";

    public string LastLoginText => Account.LastLoginAt is { } t ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-";

    public string FailText => Account.LoginFailCount > 0 ? $"{Account.LoginFailCount}회" : "-";
}

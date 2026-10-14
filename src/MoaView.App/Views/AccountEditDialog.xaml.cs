using System.Windows;
using MoaView.Core.Entities;
using Wpf.Ui.Controls;
using MoaView.App.Resources;

namespace MoaView.App.Views;

/// <summary>
/// 계정 추가·수정(06 UI/UX 3.1절). 비밀번호는 PasswordBox에서만 읽어 호출자에게 한 번 넘기고 어디에도 보관하지 않는다.
/// 수정 시 비밀번호를 비워 두면 기존 값을 유지한다.
/// </summary>
public partial class AccountEditDialog : FluentWindow
{
    public sealed record Result(string? Password);

    private static readonly (string Code, string Name)[] Providers =
    [
        (LoginProviders.Native, Strings.ProviderNativeLong),
        (LoginProviders.Naver, Strings.ProviderNaverLong),
        (LoginProviders.Google, Strings.ProviderGoogleLong),
        (LoginProviders.Kakao, Strings.ProviderKakaoLong),
        (LoginProviders.CjOne, Strings.ProviderCjOneLong),
        (LoginProviders.TId, Strings.ProviderTIdLong),
    ];

    private readonly Account _account;
    private readonly bool _isNew;

    private AccountEditDialog(Account account, bool isNew)
    {
        _account = account;
        _isNew = isNew;
        InitializeComponent();

        Title = isNew ? Strings.AddAccount : Strings.EditAccount;
        TitleBar.Title = Title;

        foreach (var (code, name) in Providers)
        {
            ProviderBox.Items.Add(new ProviderItem(code, name));
        }

        DisplayNameBox.Text = account.DisplayName;
        LoginIdBox.Text = account.LoginId;
        ProviderBox.SelectedIndex = Math.Max(0, Array.FindIndex(Providers, p => p.Code == account.LoginProvider));
        IsDefaultBox.IsChecked = account.IsDefault;
        AutoFillBox.IsChecked = account.AutoFillEnabled;
        AutoSubmitBox.IsChecked = account.AutoSubmitEnabled;
        PasswordLabel.Text = isNew || account.EncryptedPassword is null ? Strings.Password : Strings.PasswordKeepHint;
    }

    /// <summary>저장을 누르면 account를 갱신하고 새 비밀번호(없으면 null)를 돌려준다. 취소면 null.</summary>
    public static Result? Edit(Account account, bool isNew)
    {
        var dialog = new AccountEditDialog(account, isNew) { Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? new Result(dialog._password) : null;
    }

    private string? _password;

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var displayName = DisplayNameBox.Text.Trim();
        var loginId = LoginIdBox.Text.Trim();
        var password = PasswordBox.Password;

        if (displayName.Length == 0 || loginId.Length == 0)
        {
            ShowError(Strings.AccountValidationRequired);
            return;
        }

        if (_isNew && password.Length == 0 && AutoFillBox.IsChecked == true)
        {
            ShowError(Strings.AccountValidationPassword);
            return;
        }

        _account.DisplayName = displayName;
        _account.LoginId = loginId;
        _account.LoginProvider = (ProviderBox.SelectedItem as ProviderItem)?.Code ?? LoginProviders.Native;
        _account.IsDefault = IsDefaultBox.IsChecked == true;
        _account.AutoFillEnabled = AutoFillBox.IsChecked == true;
        _account.AutoSubmitEnabled = AutoSubmitBox.IsChecked == true && _account.AutoFillEnabled;
        _password = password.Length > 0 ? password : null;

        PasswordBox.Clear();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        PasswordBox.Clear();
        DialogResult = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private sealed record ProviderItem(string Code, string Name)
    {
        public override string ToString() => Name;
    }
}

using MoaView.Core.Entities;

namespace MoaView.Core.Models;

/// <summary>
/// 자동 제출 반복 방지(FR-ACC-10, 04 아키텍처 5.3절). 비밀번호를 바꾼 뒤 예전 값으로 반복 제출하면 계정이 잠길 수 있으므로
/// 같은 계정은 쿨다운 안에 다시 제출하지 않고, 연속 실패가 한계에 이르면 제출을 막는다.
/// </summary>
public static class LoginGuard
{
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(10);
    public const int DefaultMaxConsecutiveFailures = 2;

    public enum Verdict
    {
        Allowed,
        AutoSubmitDisabled,
        NoPassword,
        TooManyFailures,
        InCooldown,
    }

    public static Verdict Evaluate(Account account, DateTime utcNow, TimeSpan? cooldown = null, int? maxConsecutiveFailures = null)
    {
        if (!account.AutoSubmitEnabled)
        {
            return Verdict.AutoSubmitDisabled;
        }

        if (account.EncryptedPassword is null || account.EncryptedPassword.Length == 0)
        {
            return Verdict.NoPassword;
        }

        if (account.LoginFailCount >= (maxConsecutiveFailures ?? DefaultMaxConsecutiveFailures))
        {
            return Verdict.TooManyFailures;
        }

        if (account.LastAutoSubmitAt is { } last && utcNow - last < (cooldown ?? DefaultCooldown))
        {
            return Verdict.InCooldown;
        }

        return Verdict.Allowed;
    }

    public static bool CanAutoSubmit(Account account, DateTime utcNow, TimeSpan? cooldown = null, int? maxConsecutiveFailures = null) =>
        Evaluate(account, utcNow, cooldown, maxConsecutiveFailures) == Verdict.Allowed;

    /// <summary>자동 제출 결과를 계정에 반영한다. 실패가 한계에 이르면 자동 제출을 끈다. 끈 경우 true.</summary>
    public static bool RecordResult(Account account, bool succeeded, DateTime utcNow, int? maxConsecutiveFailures = null)
    {
        if (succeeded)
        {
            account.LastLoginAt = utcNow;
            account.LoginFailCount = 0;
            return false;
        }

        account.LoginFailCount++;
        if (account.LoginFailCount >= (maxConsecutiveFailures ?? DefaultMaxConsecutiveFailures) && account.AutoSubmitEnabled)
        {
            account.AutoSubmitEnabled = false;
            return true;
        }

        return false;
    }
}

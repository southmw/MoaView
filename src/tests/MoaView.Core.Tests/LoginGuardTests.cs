using FluentAssertions;
using MoaView.Core.Entities;
using MoaView.Core.Models;
using Xunit;

namespace MoaView.Core.Tests;

public class LoginGuardTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private static Account Account(bool autoSubmit = true, int failCount = 0, DateTime? lastSubmit = null) => new()
    {
        DisplayName = "me", LoginId = "me", ProfileName = "svc_x_1",
        EncryptedPassword = [1, 2, 3], AutoSubmitEnabled = autoSubmit,
        LoginFailCount = failCount, LastAutoSubmitAt = lastSubmit,
    };

    [Fact]
    public void Allows_when_enabled_with_password_and_no_recent_submit()
    {
        LoginGuard.Evaluate(Account(), Now).Should().Be(LoginGuard.Verdict.Allowed);
    }

    [Fact]
    public void Blocks_when_auto_submit_is_off()
    {
        LoginGuard.Evaluate(Account(autoSubmit: false), Now).Should().Be(LoginGuard.Verdict.AutoSubmitDisabled);
    }

    [Fact]
    public void Blocks_without_password()
    {
        var account = Account();
        account.EncryptedPassword = null;

        LoginGuard.Evaluate(account, Now).Should().Be(LoginGuard.Verdict.NoPassword);
    }

    [Fact]
    public void Blocks_inside_cooldown_and_allows_after()
    {
        var account = Account(lastSubmit: Now.AddMinutes(-9));

        LoginGuard.Evaluate(account, Now).Should().Be(LoginGuard.Verdict.InCooldown);
        LoginGuard.Evaluate(account, Now.AddMinutes(2)).Should().Be(LoginGuard.Verdict.Allowed);
    }

    [Fact]
    public void Blocks_after_two_consecutive_failures()
    {
        LoginGuard.Evaluate(Account(failCount: 2), Now).Should().Be(LoginGuard.Verdict.TooManyFailures);
    }

    [Fact]
    public void Second_failure_disables_auto_submit_and_success_resets()
    {
        var account = Account();

        LoginGuard.RecordResult(account, succeeded: false, Now).Should().BeFalse();
        account.LoginFailCount.Should().Be(1);
        account.AutoSubmitEnabled.Should().BeTrue();

        LoginGuard.RecordResult(account, succeeded: false, Now).Should().BeTrue();
        account.LoginFailCount.Should().Be(2);
        account.AutoSubmitEnabled.Should().BeFalse();

        LoginGuard.RecordResult(account, succeeded: true, Now).Should().BeFalse();
        account.LoginFailCount.Should().Be(0);
        account.LastLoginAt.Should().Be(Now);
    }
}

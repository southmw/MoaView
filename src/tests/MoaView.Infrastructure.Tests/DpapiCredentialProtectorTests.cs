using System.Security.Cryptography;
using FluentAssertions;
using MoaView.Infrastructure.Security;
using Xunit;

namespace MoaView.Infrastructure.Tests;

public class DpapiCredentialProtectorTests
{
    private static readonly byte[] Salt = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Round_trip_restores_plain_text()
    {
        var protector = new DpapiCredentialProtector(Salt);

        var cipher = protector.Protect("p@ss워드!");

        var plainBytes = System.Text.Encoding.UTF8.GetBytes("p@ss워드!");
        cipher.Length.Should().BeGreaterThan(plainBytes.Length);
        cipher.AsSpan().IndexOf(plainBytes).Should().Be(-1, "평문이 그대로 들어 있으면 안 된다");
        protector.Unprotect(cipher).Should().Be("p@ss워드!");
    }

    [Fact]
    public void Different_install_salt_cannot_decrypt()
    {
        var cipher = new DpapiCredentialProtector(Salt).Protect("secret");
        var other = new DpapiCredentialProtector(RandomNumberGenerator.GetBytes(32));

        var act = () => other.Unprotect(cipher);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Tampered_cipher_is_rejected()
    {
        var protector = new DpapiCredentialProtector(Salt);
        var cipher = protector.Protect("secret");
        cipher[^1] ^= 0xFF;

        var act = () => protector.Unprotect(cipher);

        act.Should().Throw<CryptographicException>();
    }
}

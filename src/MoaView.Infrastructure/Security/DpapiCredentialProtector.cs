using System.IO;
using System.Security.Cryptography;
using System.Text;
using MoaView.Core.Abstractions;
using MoaView.Infrastructure.Configuration;

namespace MoaView.Infrastructure.Security;

/// <summary>
/// Windows DPAPI(CurrentUser)로 비밀번호를 암호화한다(05 데이터베이스 설계 5절, D-03).
/// 엔트로피 = 소스 상수 + 설치 시 생성한 난수(%LOCALAPPDATA%\MoaView\.entropy). 같은 Windows 계정의 다른 앱이
/// DB만으로 복호화하기 어렵게 한다. 다른 PC·계정으로 옮기면 복호화되지 않는 것이 의도된 동작이다.
/// </summary>
public sealed class DpapiCredentialProtector : ICredentialProtector
{
    private const string EntropyFileName = ".entropy";
    // 앱 이름이 MoaView로 바뀐 뒤에도 이미 저장된 비밀번호를 풀 수 있도록 엔트로피 상수는 그대로 둔다.
    private static readonly byte[] AppConstant = Encoding.UTF8.GetBytes("MyOTTPlayer.Credential.v1");

    private readonly byte[] _entropy;

    public DpapiCredentialProtector(AppPaths paths)
        : this(LoadOrCreateInstallSalt(Path.Combine(paths.Root, EntropyFileName)))
    {
    }

    /// <summary>테스트용. 설치 난수를 직접 넘긴다.</summary>
    public DpapiCredentialProtector(byte[] installSalt)
    {
        _entropy = SHA256.HashData([.. AppConstant, .. installSalt]);
    }

    public byte[] Protect(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        var data = Encoding.UTF8.GetBytes(plainText);
        try
        {
            return ProtectedData.Protect(data, _entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
        }
    }

    public string Unprotect(byte[] cipher)
    {
        ArgumentNullException.ThrowIfNull(cipher);
        var data = ProtectedData.Unprotect(cipher, _entropy, DataProtectionScope.CurrentUser);
        try
        {
            return Encoding.UTF8.GetString(data);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
        }
    }

    private static byte[] LoadOrCreateInstallSalt(string path)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (existing.Length == 32)
            {
                return existing;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var salt = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(path, salt);
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        return salt;
    }
}

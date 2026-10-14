namespace MoaView.Core.Abstractions;

/// <summary>
/// 비밀번호 암호화(04 아키텍처 4.5절). 구현은 Windows DPAPI(CurrentUser)다(D-03).
/// 복호화한 평문은 자동 입력 스크립트에 넘기기 직전에만 메모리에 두고 로그에 남기지 않는다(NFR-05).
/// </summary>
public interface ICredentialProtector
{
    byte[] Protect(string plainText);

    string Unprotect(byte[] cipher);
}

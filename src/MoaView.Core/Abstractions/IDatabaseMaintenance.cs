namespace MoaView.Core.Abstractions;

/// <summary>설정 > 데이터 탭의 DB 백업(FR-SET-05). 복원은 Could로 미구현.</summary>
public interface IDatabaseMaintenance
{
    /// <summary>
    /// 실행 중인 DB를 일관된 상태로 복사한다(SQLite VACUUM INTO). 대상 파일이 이미 있으면 먼저 지운다.
    /// 암호화된 비밀번호는 같은 Windows 계정·같은 설치 엔트로피에서만 풀리므로 다른 PC에서는 복원되지 않는다.
    /// </summary>
    Task BackupAsync(string destinationPath, CancellationToken cancellationToken = default);
}

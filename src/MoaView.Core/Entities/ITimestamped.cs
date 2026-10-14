namespace MoaView.Core.Entities;

/// <summary>CreatedAt·UpdatedAt을 SaveChanges에서 자동으로 채우는 엔티티(05 데이터베이스 설계 6절). UTC로 저장한다.</summary>
public interface ITimestamped
{
    DateTime CreatedAt { get; set; }

    DateTime UpdatedAt { get; set; }
}

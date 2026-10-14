namespace MoaView.Core.Entities;

/// <summary>앱 설정 Key-Value(05 데이터베이스 설계 3.3절). 값은 JSON 문자열이다.</summary>
public sealed class AppSetting
{
    public required string Key { get; set; }

    public string? Value { get; set; }
}

using System.IO;
using System.Reflection;
using System.Text.Json;

namespace MoaView.App.Services;

/// <summary>
/// Scripts/*.js 포함 리소스를 읽고 인자를 JSON으로 넣는다. 값은 항상 직렬화해서 넣으므로 스크립트 주입이 되지 않는다(10 보안 점검 3절).
/// </summary>
public static class ScriptLoader
{
    private const string ArgsPlaceholder = "__ARGS__";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string Load(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Scripts." + name, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException($"포함 리소스 스크립트를 찾을 수 없습니다: {name}");

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            var script = reader.ReadToEnd();
            Cache[name] = script;
            return script;
        }
    }

    public static string Load(string name, object args) =>
        Load(name).Replace(ArgsPlaceholder, JsonSerializer.Serialize(args, JsonOptions), StringComparison.Ordinal);

    /// <summary>ExecuteScriptAsync는 결과를 JSON으로 돌려준다. 문자열 결과를 풀어 준다.</summary>
    public static string? ReadString(string? executeScriptResult)
    {
        if (string.IsNullOrEmpty(executeScriptResult) || executeScriptResult == "null")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(executeScriptResult);
        }
        catch (JsonException)
        {
            return executeScriptResult;
        }
    }

    public static bool ReadBool(string? executeScriptResult) =>
        string.Equals(executeScriptResult, "true", StringComparison.OrdinalIgnoreCase);
}

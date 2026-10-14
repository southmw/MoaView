using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MoaView.Infrastructure.WebView;

/// <summary>
/// Phase 0 PoC(P5-b)에서 검증해 옮긴 코드. 아직 본 앱에서 호출하지 않으며, 치지직 그리드 정식 지원(D-14)을 넣을 때 사용한다.
/// Edge 추가 기능 스토어의 공식 CRX 패키지를 내려받아 압축을 풀고, 스토어와 같은 확장 ID가 되도록
/// manifest.json에 공개키(key)를 넣는다. WebView2는 스토어 설치를 지원하지 않고 폴더 로드만 되기 때문이다.
/// 확장 내용은 수정하지 않는다(정식 패키지 그대로, D-14).
/// </summary>
public static class ExtensionInstaller
{
    public sealed record Result(string Folder, string ExtensionId, string Name, string Version, IReadOnlyList<string> Permissions);

    public static async Task<Result> InstallFromEdgeStoreAsync(string storeId, string targetRoot, Action<string> log)
    {
        var url = $"https://edge.microsoft.com/extensionwebstorebase/v1/crx?response=redirect&x=id%3D{storeId}%26installsource%3Dondemand%26uc";
        log($"CRX 내려받기: {url}");

        // 스토어는 https → http(CDN)로 리디렉션하는데 HttpClient는 스킴이 바뀌는 리디렉션을 따라가지 않으므로 직접 따라간다.
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var bytes = await DownloadFollowingRedirectsAsync(http, url, log);
        log($"CRX 크기 {bytes.Length} bytes");

        var (publicKey, zipOffset) = ParseCrx3(bytes, log);
        var extensionId = ToExtensionId(publicKey);
        if (!string.Equals(extensionId, storeId, StringComparison.Ordinal))
        {
            // 요청한 스토어 ID와 다른 키로 서명된 패키지는 설치하지 않는다(바꿔치기 방지).
            throw new InvalidDataException($"공개키에서 계산한 ID({extensionId})가 스토어 ID({storeId})와 다릅니다. 설치를 중단합니다.");
        }

        var folder = Path.Combine(targetRoot, extensionId);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        Directory.CreateDirectory(folder);
        using (var zip = new ZipArchive(new MemoryStream(bytes, zipOffset, bytes.Length - zipOffset), ZipArchiveMode.Read))
        {
            zip.ExtractToDirectory(folder);
        }

        // 스토어 서명 메타데이터는 폴더 로드에서 금지된 이름(_metadata)이라 제거한다.
        var metadata = Path.Combine(folder, "_metadata");
        if (Directory.Exists(metadata))
        {
            Directory.Delete(metadata, recursive: true);
        }

        var manifestPath = Path.Combine(folder, "manifest.json");
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath, Encoding.UTF8))?.AsObject()
                       ?? throw new InvalidDataException("manifest.json을 읽을 수 없습니다.");
        manifest["key"] = Convert.ToBase64String(publicKey);
        await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }), Encoding.UTF8);

        var permissions = manifest["permissions"]?.AsArray().Select(p => p?.ToString() ?? string.Empty).ToList() ?? [];
        return new Result(
            folder,
            extensionId,
            manifest["name"]?.ToString() ?? "?",
            manifest["version"]?.ToString() ?? "?",
            permissions);
    }

    private static async Task<byte[]> DownloadFollowingRedirectsAsync(HttpClient http, string url, Action<string> log)
    {
        for (var hop = 0; hop < 8; hop++)
        {
            using var response = await http.GetAsync(url);
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400 && response.Headers.Location is not null)
            {
                var location = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(new Uri(url), response.Headers.Location);
                log($"리디렉션 {status} → {location.Scheme}://{location.Host}{location.AbsolutePath}");

                // 스토어 CDN은 http로 리디렉션하고 https를 지원하지 않는다. https를 먼저 시도하고, 안 되면 http로 받는다.
                // 평문 전송의 바꿔치기는 아래 CRX3 서명 검증(요청한 스토어 ID의 공개키)으로 막는다. Chrome도 같은 방식이다.
                if (location.Scheme == "http")
                {
                    var secure = new UriBuilder(location) { Scheme = "https", Port = -1 }.Uri;
                    try
                    {
                        using var secureResponse = await http.GetAsync(secure);
                        if (secureResponse.IsSuccessStatusCode)
                        {
                            log("https로 내려받음");
                            return await secureResponse.Content.ReadAsByteArrayAsync();
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        log($"https 시도 실패({ex.Message.Split('.')[0]}). http로 받고 서명으로 검증");
                    }
                }

                url = location.ToString();
                continue;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        throw new HttpRequestException("리디렉션이 너무 많습니다.");
    }

    /// <summary>
    /// CRX3 = "Cr24" + version(4) + headerLength(4) + CrxFileHeader(protobuf) + ZIP.
    /// 헤더에서 서명 crx_id와 일치하는 RSA 공개키를 찾고, 그 키로 서명(SHA-256 + RSA PKCS#1)을 검증한다.
    /// 서명 대상 = "CRX3 SignedData\0" + len(signed_header_data, 4바이트 LE) + signed_header_data + ZIP.
    /// </summary>
    private static (byte[] PublicKey, int ZipOffset) ParseCrx3(byte[] bytes, Action<string> log)
    {
        if (bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 0, 4) != "Cr24")
        {
            throw new InvalidDataException("CRX 형식이 아닙니다(매직 번호 불일치). 스토어 ID를 확인하세요.");
        }

        var version = BitConverter.ToUInt32(bytes, 4);
        if (version != 3)
        {
            throw new InvalidDataException($"지원하지 않는 CRX 버전 {version}");
        }

        var headerLength = BitConverter.ToInt32(bytes, 8);
        var header = bytes.AsSpan(12, headerLength);

        var proofs = new List<(byte[] PublicKey, byte[] Signature)>();
        byte[]? signedHeaderData = null;
        byte[]? signedCrxId = null;

        foreach (var (field, value) in ReadFields(header))
        {
            switch (field)
            {
                case 2: // sha256_with_rsa: AsymmetricKeyProof { bytes public_key = 1; bytes signature = 2; }
                    byte[]? key = null;
                    byte[]? signature = null;
                    foreach (var (inner, innerValue) in ReadFields(value))
                    {
                        if (inner == 1) key = innerValue;
                        if (inner == 2) signature = innerValue;
                    }

                    if (key is not null && signature is not null)
                    {
                        proofs.Add((key, signature));
                    }
                    break;

                case 10000: // signed_header_data: SignedData { bytes crx_id = 1; }
                    signedHeaderData = value;
                    foreach (var (inner, innerValue) in ReadFields(value))
                    {
                        if (inner == 1)
                        {
                            signedCrxId = innerValue;
                        }
                    }
                    break;
            }
        }

        if (proofs.Count == 0 || signedHeaderData is null || signedCrxId is null)
        {
            throw new InvalidDataException("CRX 헤더에 RSA 서명 또는 서명 데이터가 없습니다.");
        }

        var proof = proofs.FirstOrDefault(p => SHA256.HashData(p.PublicKey).AsSpan(0, 16).SequenceEqual(signedCrxId));
        if (proof.PublicKey is null)
        {
            throw new InvalidDataException("crx_id와 일치하는 공개키가 없습니다.");
        }

        var zipOffset = 12 + headerLength;
        var signedData = BuildSignedData(signedHeaderData, bytes.AsSpan(zipOffset));
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(proof.PublicKey, out _);
        if (!rsa.VerifyData(signedData, proof.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            throw new InvalidDataException("CRX 서명 검증 실패. 패키지가 변조되었거나 손상되었습니다.");
        }

        log("CRX 서명 검증 통과");
        return (proof.PublicKey, zipOffset);
    }

    private static byte[] BuildSignedData(byte[] signedHeaderData, ReadOnlySpan<byte> zip)
    {
        var prefix = "CRX3 SignedData\0"u8;
        var result = new byte[prefix.Length + 4 + signedHeaderData.Length + zip.Length];
        var offset = 0;
        prefix.CopyTo(result);
        offset += prefix.Length;
        BitConverter.TryWriteBytes(result.AsSpan(offset, 4), signedHeaderData.Length);
        offset += 4;
        signedHeaderData.CopyTo(result, offset);
        offset += signedHeaderData.Length;
        zip.CopyTo(result.AsSpan(offset));
        return result;
    }

    /// <summary>길이 구분(wire type 2) 필드만 필요한 최소 protobuf 리더.</summary>
    private static IEnumerable<(int Field, byte[] Value)> ReadFields(ReadOnlySpan<byte> buffer)
    {
        var result = new List<(int, byte[])>();
        var i = 0;
        while (i < buffer.Length)
        {
            var key = ReadVarint(buffer, ref i);
            var field = (int)(key >> 3);
            var wireType = (int)(key & 7);

            switch (wireType)
            {
                case 0:
                    ReadVarint(buffer, ref i);
                    break;
                case 2:
                    var length = (int)ReadVarint(buffer, ref i);
                    result.Add((field, buffer.Slice(i, length).ToArray()));
                    i += length;
                    break;
                default:
                    throw new InvalidDataException($"예상하지 못한 protobuf wire type {wireType}");
            }
        }

        return result;
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> buffer, ref int index)
    {
        ulong value = 0;
        var shift = 0;
        while (true)
        {
            var b = buffer[index++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return value;
            }

            shift += 7;
        }
    }

    /// <summary>확장 ID = SHA-256(공개키)의 앞 16바이트를 0~f → a~p로 바꾼 32글자.</summary>
    private static string ToExtensionId(byte[] publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        var sb = new StringBuilder(32);
        foreach (var b in hash.AsSpan(0, 16))
        {
            sb.Append((char)('a' + (b >> 4)));
            sb.Append((char)('a' + (b & 0x0F)));
        }

        return sb.ToString();
    }
}

# UI 문자열 리소스 도구

UI 문자열은 `src/MoaView.App/Resources/Strings.resx`(한국어, 기본 문화권)에 두고, `Strings.Designer.cs`의 강타입 접근자로 씁니다(D-08 다국어 대비). XAML은 `{x:Static res:Strings.Key}`, C#은 `Strings.Key` 또는 `string.Format(Strings.Key, ...)`입니다.

`dotnet` CLI만으로는 Visual Studio의 ResXFileCodeGenerator가 돌지 않으므로 Designer 파일은 여기 스크립트로 만듭니다.

## 평소 작업 (문자열 추가·변경)

1. `Strings.resx`에 `<data name="Key">` 항목을 추가하거나 값을 고친다.
2. `python tools/resx/gen_designer.py` 로 `Strings.Designer.cs`를 다시 만든다.
3. 코드에서 `Strings.Key`로 쓴다. 보간이 필요하면 값에 `{0}`, `{1}`을 두고 `string.Format`을 쓴다.

로그 메시지와 예외 메시지는 개발자용이라 리소스로 빼지 않습니다.

## 최초 분리 때 쓴 스크립트 (참고용)

| 스크립트 | 역할 |
|----------|------|
| `extract.py` | XAML 속성(Text·Content·Header·ToolTip·Title·PlaceholderText·AutomationProperties.Name)과 C# 리터럴에서 한국어 문자열을 뽑아 JSON으로 출력. 로그·주석·예외 메시지 줄은 제외 |
| `strings-map.json` | `[키, 원문]` 목록. 같은 키를 여러 원문(보간 변수명만 다른 경우)에 쓸 수 있음 |
| `apply.py` | 매핑대로 소스를 치환하고 `Strings.resx` 생성. `--check`는 매핑되지 않은 문자열만 출력 |
| `gen_designer.py` | resx → `Strings.Designer.cs` |

다른 언어를 추가하려면 `Strings.en.resx`처럼 문화권 접미사를 붙인 resx를 같은 폴더에 두면 .NET이 위성 어셈블리로 빌드하고 `Strings.Culture`(기본은 현재 UI 문화권)에 따라 고릅니다.

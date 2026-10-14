"""strings-map.json(키→원문)대로 XAML·C#의 한국어 리터럴을 리소스 참조로 바꾸고 Strings.resx를 만든다(2단계).

사용: python tools/resx/apply.py            # 치환 + resx 생성
      python tools/resx/apply.py --check    # 매핑되지 않은 문자열만 출력

- XAML: Attr="원문" → Attr="{x:Static res:Strings.Key}" (루트에 xmlns:res 추가)
- C#  : "원문" → Strings.Key, $"...{expr}..." → string.Format(Strings.Key, expr, ...) (resx 값은 {0}, {1} 형식)
- 로그·주석·예외 메시지 줄은 건드리지 않는다(extract.py와 같은 규칙).
매핑 파일은 [["Key", "원문"], ...] 목록이며 같은 키가 여러 원문(정규화 후 같은 값)에 쓰일 수 있다.
"""
import json
import re
import sys
from pathlib import Path
from xml.sax.saxutils import escape

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1] / "src" / "MoaView.App"
RES_DIR = ROOT / "Resources"
NAMESPACE_DECL = 'xmlns:res="clr-namespace:MoaView.App.Resources"'

XAML_ATTRS = ("Text", "Content", "Header", "ToolTip", "Title", "PlaceholderText", "AutomationProperties.Name")
XAML_RE = re.compile(r'\b(' + "|".join(re.escape(a) for a in XAML_ATTRS) + r')="([^"{]*[가-힣][^"]*)"')
CS_LITERAL_RE = re.compile(r'\$?"((?:[^"\\]|\\.)*[가-힣](?:[^"\\]|\\.)*)"')
CS_SKIP_LINE = re.compile(r'\bLog(Information|Debug|Warning|Error|Critical|Trace)\b|^\s*///|^\s*//|Exception\(|\[Description')
HOLE_RE = re.compile(r'\{([^{}:]+?)(?::([^{}]+))?\}')

CS_FILES = [
    "ViewModels/AccountsViewModel.cs", "ViewModels/MainViewModel.cs", "ViewModels/SettingsViewModel.cs",
    "Views/AccountEditDialog.xaml.cs", "Views/OAuthPopupWindow.xaml.cs", "Views/ServiceEditDialog.xaml.cs",
    "Services/LoginAssistService.cs", "Controls/ServiceHostControl.cs", "App.xaml.cs",
]


def cs_unescape(text: str) -> str:
    return text.replace("\\\\", "\x00").replace("\\n", "\n").replace("\\t", "\t").replace('\\"', '"').replace("\x00", "\\")


def normalize(raw: str, interpolated: bool) -> tuple[str, list[str]]:
    """원문을 resx 값으로. 보간식은 {0}, {1}로 바꾸고 식 목록을 돌려준다."""
    text = cs_unescape(raw)
    args: list[str] = []
    if interpolated:
        def repl(m: re.Match) -> str:
            expr = m.group(1).strip()
            if expr not in args:
                args.append(expr)
            index = args.index(expr)
            return "{" + str(index) + (":" + m.group(2) if m.group(2) else "") + "}"
        text = HOLE_RE.sub(repl, text)
    return text, args


def load_map() -> dict[str, str]:
    pairs = json.loads((HERE / "strings-map.json").read_text(encoding="utf-8"))
    return {value: key for key, value in pairs}


def write_resx(entries: dict[str, str]) -> None:
    RES_DIR.mkdir(exist_ok=True)
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<root>",
        '  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>',
        '  <resheader name="version"><value>2.0</value></resheader>',
        '  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>',
        '  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>',
    ]
    for key in sorted(entries):
        lines.append(f'  <data name="{key}" xml:space="preserve">')
        lines.append(f"    <value>{escape(entries[key])}</value>")
        lines.append("  </data>")
    lines.append("</root>")
    (RES_DIR / "Strings.resx").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    check_only = "--check" in sys.argv
    value_to_key = load_map()
    resx: dict[str, str] = {}
    missing: list[str] = []

    def register(raw: str, interpolated: bool) -> tuple[str, list[str]] | None:
        key = value_to_key.get(raw)
        if key is None:
            missing.append(raw)
            return None
        text, args = normalize(raw, interpolated)
        if key in resx and resx[key] != text:
            raise SystemExit(f"키 {key}의 값이 서로 다릅니다:\n  {resx[key]!r}\n  {text!r}")
        resx[key] = text
        return key, args

    # XAML
    for xaml in sorted((ROOT / "Views").glob("*.xaml")):
        src = xaml.read_text(encoding="utf-8")
        changed = False

        def xrepl(m: re.Match) -> str:
            nonlocal changed
            hit = register(m.group(2), False)
            if hit is None:
                return m.group(0)
            changed = True
            return f'{m.group(1)}="{{x:Static res:Strings.{hit[0]}}}"'

        out = XAML_RE.sub(xrepl, src)
        if changed and NAMESPACE_DECL not in out:
            out = out.replace('xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"',
                              'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"\n        ' + NAMESPACE_DECL, 1)
        if changed and not check_only:
            xaml.write_text(out, encoding="utf-8", newline="\n")

    # C#
    for rel in CS_FILES:
        path = ROOT / rel
        lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
        changed = False
        for i, line in enumerate(lines):
            if CS_SKIP_LINE.search(line):
                continue

            def crepl(m: re.Match) -> str:
                nonlocal changed
                interpolated = m.group(0).startswith("$")
                hit = register(m.group(1), interpolated)
                if hit is None:
                    return m.group(0)
                changed = True
                key, args = hit
                if interpolated and args:
                    return f"string.Format(Strings.{key}, {', '.join(args)})"
                return f"Strings.{key}"

            lines[i] = CS_LITERAL_RE.sub(crepl, line)
        if changed and not check_only:
            text = "".join(lines)
            if "using MoaView.App.Resources;" not in text:
                last_using = max(i for i, l in enumerate(lines) if l.startswith("using "))
                lines.insert(last_using + 1, "using MoaView.App.Resources;\n")
                text = "".join(lines)
            path.write_text(text, encoding="utf-8", newline="\n")

    sys.stdout.reconfigure(encoding="utf-8")
    if missing:
        print(f"매핑 없음 {len(set(missing))}개:")
        for m in sorted(set(missing)):
            print("  ", repr(m))
    if not check_only:
        write_resx(resx)
        print(f"resx 항목 {len(resx)}개 생성")


if __name__ == "__main__":
    main()

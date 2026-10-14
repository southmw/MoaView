"""UI 한국어 문자열을 XAML 속성과 C# 리터럴에서 추출해 JSON으로 만든다(1단계).

사용: python tools/resx/extract.py > tools/resx/strings-found.json
로그·주석·예외 메시지는 제외한다(개발자용이라 리소스로 빼지 않는다).
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "src" / "MoaView.App"
XAML_ATTRS = ("Text", "Content", "Header", "ToolTip", "Title", "PlaceholderText", "AutomationProperties.Name")
XAML_RE = re.compile(r'\b(' + "|".join(re.escape(a) for a in XAML_ATTRS) + r')="([^"{]*[가-힣][^"]*)"')
CS_LITERAL_RE = re.compile(r'\$?"((?:[^"\\]|\\.)*[가-힣](?:[^"\\]|\\.)*)"')
CS_SKIP_LINE = re.compile(r'\bLog(Information|Debug|Warning|Error|Critical|Trace)\b|^\s*///|^\s*//|Exception\(|\[Description')

CS_FILES = [
    "ViewModels/AccountsViewModel.cs", "ViewModels/MainViewModel.cs", "ViewModels/SettingsViewModel.cs",
    "Views/AccountEditDialog.xaml.cs", "Views/OAuthPopupWindow.xaml.cs", "Views/ServiceEditDialog.xaml.cs",
    "Services/LoginAssistService.cs", "Controls/ServiceHostControl.cs", "App.xaml.cs",
]


def main() -> None:
    found: list[dict] = []
    for xaml in sorted((ROOT / "Views").glob("*.xaml")):
        for n, line in enumerate(xaml.read_text(encoding="utf-8").splitlines(), 1):
            for m in XAML_RE.finditer(line):
                found.append({"file": str(xaml.relative_to(ROOT)), "line": n, "kind": "xaml", "attr": m.group(1), "value": m.group(2)})
    for rel in CS_FILES:
        path = ROOT / rel
        for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if CS_SKIP_LINE.search(line):
                continue
            for m in CS_LITERAL_RE.finditer(line):
                raw = m.group(0)
                found.append({"file": rel, "line": n, "kind": "cs-interp" if raw.startswith("$") else "cs", "value": m.group(1)})
    uniq = {}
    for f in found:
        uniq.setdefault(f["value"], []).append(f"{f['file']}:{f['line']}")
    sys.stdout.reconfigure(encoding="utf-8")
    json.dump({"count": len(uniq), "strings": [{"value": v, "at": at} for v, at in uniq.items()]}, sys.stdout, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()

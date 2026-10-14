# 앱 아이콘 생성

`src/MoaView.App/Assets/moaview.ico`와 `moaview.png`는 아래 두 스크립트로 만든다. 외부 도구 없이 Windows의 GDI+와 Python 표준 라이브러리만 쓴다.

```bash
mkdir -p out
powershell -NoProfile -ExecutionPolicy Bypass -File tools/icon/render-icon.ps1 -Out "$(cygpath -w "$PWD/out")"   # 1024px 원본을 그려 256~16px PNG/RAW로 축소
python -I tools/icon/build-ico.py out src/MoaView.App/Assets/moaview.ico                                     # 7단계 ICO 조립(128px 이상은 PNG, 이하는 BMP)
cp out/icon_256.png src/MoaView.App/Assets/moaview.png
```

디자인: 사이드바 타일 3개(모아 놓은 서비스) + 재생 삼각형, 파랑→보라 둥근 배경. 서비스 로고는 쓰지 않는다(D-08).

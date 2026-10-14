// 로그아웃 상태 확인 (FR-ACC-08). args: { marker: CSS 셀렉터 | null }
// 반환: 'logged-out' | 'logged-in' | 'unknown'
// marker가 있으면 그 요소가 보이면 logged-out, 아니면 logged-in. 없으면 휴리스틱:
//   1) 보이는 "로그아웃" 링크·버튼 → logged-in, 보이는 "로그인" → logged-out
//   2) 헤더·내비게이션 안의 숨겨진 항목(마우스를 올려야 열리는 드롭다운, Wavve 등)도 같은 규칙으로 검사
//   3) 둘 다 없으면 unknown(아직 안 그려짐)
(function (args) {
  const visible = (el) => !!(el && el.offsetParent !== null && el.getClientRects().length > 0);
  if (args.marker) {
    try { return visible(document.querySelector(args.marker)) ? 'logged-out' : 'logged-in'; } catch { return 'unknown'; }
  }
  const text = (n) => (n.innerText || n.textContent || n.getAttribute('aria-label') || n.getAttribute('title') || '').trim().toLowerCase();
  const href = (n) => (n.getAttribute('href') || '').toLowerCase();
  const loginWords = ['로그인', 'sign in', 'log in', 'login'];
  const logoutWords = ['로그아웃', 'sign out', 'log out', 'logout'];
  const isLogin = (n) => {
    const t = text(n);
    return (/(^|\/)(login|signin|servicelogin|nidlogin)/.test(href(n)) || (t.length <= 14 && loginWords.some((w) => t === w || t.startsWith(w + '/') || t.startsWith(w + ' '))))
      && !logoutWords.some((w) => t.includes(w));
  };
  const isLogout = (n) => /(logout|signout)/.test(href(n)) || logoutWords.some((w) => text(n) === w);

  const all = [...document.querySelectorAll('a,button,[role="button"]')];
  const shown = all.filter(visible);
  if (shown.some(isLogout)) return 'logged-in';
  if (shown.some(isLogin)) return 'logged-out';

  const headerScopes = [...document.querySelectorAll('header, nav, [id*="header" i], [id*="gnb" i], [class*="header" i], [class*="gnb" i], [class*="navbar" i], [class*="dropdown" i]')];
  const hidden = headerScopes.flatMap((h) => [...h.querySelectorAll('a,button,[role="button"],li')]).filter((n) => !visible(n));
  if (hidden.some(isLogout)) return 'logged-in';
  if (hidden.some(isLogin)) return 'logged-out';
  return 'unknown';
})(__ARGS__);

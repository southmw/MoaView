// 개발용: 로그인 폼 구조 분석 (서비스별 LoginFormSpec 셀렉터를 정할 때 쓴다).
// 입력란의 값은 절대 포함하지 않는다. 반환: JSON 문자열.
(function () {
  const visible = (el) => !!(el && el.offsetParent !== null && el.getClientRects().length > 0);
  const describe = (el) => ({
    tag: el.tagName.toLowerCase(),
    type: el.type || null,
    name: el.name || null,
    id: el.id || null,
    placeholder: el.placeholder || null,
    autocomplete: el.autocomplete || null,
    aria: el.getAttribute('aria-label'),
    text: (el.innerText || (el.type === 'submit' ? el.value : '') || '').trim().slice(0, 30) || null,
    classes: (el.className && typeof el.className === 'string') ? el.className.split(/\s+/).slice(0, 3).join(' ') : null,
    visible: visible(el),
    form: el.form ? (el.form.id || el.form.name || el.form.getAttribute('action') || 'form') : null,
  });
  const inputs = [...document.querySelectorAll('input,textarea,select')].map(describe);
  const buttons = [...document.querySelectorAll('button,input[type="submit"],[role="button"]')].filter(visible).map(describe).slice(0, 40);
  const loginLinks = [...document.querySelectorAll('a[href*="login"],a[href*="logout"],a[href*="signin"],a[href*="signout"]')]
    .filter(visible)
    .map((a) => ({ text: (a.innerText || '').trim().slice(0, 20), href: (a.getAttribute('href') || '').split('?')[0] }))
    .slice(0, 20);
  // 헤더 드롭다운처럼 숨겨진 로그인/로그아웃 항목(로그아웃 상태 휴리스틱이 보는 것)
  const hiddenHeaderItems = [...document.querySelectorAll('header, nav, [id*="header" i], [id*="gnb" i], [class*="header" i], [class*="gnb" i], [class*="navbar" i], [class*="dropdown" i]')]
    .flatMap((h) => [...h.querySelectorAll('a,button,[role="button"],li')])
    .filter((n) => !visible(n))
    .map((n) => ({ tag: n.tagName.toLowerCase(), text: (n.textContent || '').trim().slice(0, 20), href: (n.getAttribute('href') || '').split('?')[0] || null, classes: (typeof n.className === 'string' ? n.className.split(/\s+/).slice(0, 2).join(' ') : null) }))
    .filter((d) => /로그인|로그아웃|login|logout|sign/i.test(d.text + ' ' + (d.href || '')))
    .slice(0, 20);
  return JSON.stringify({
    url: location.origin + location.pathname,
    title: document.title,
    forms: [...document.forms].map((f) => ({ id: f.id || null, name: f.name || null, action: (f.getAttribute('action') || '').split('?')[0], method: f.method })),
    inputs: inputs,
    buttons: buttons,
    loginLinks: loginLinks,
    hiddenHeaderItems: hiddenHeaderItems,
  });
})();

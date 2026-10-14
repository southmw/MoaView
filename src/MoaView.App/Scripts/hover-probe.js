// 로그아웃 상태 판단 보조: 마우스를 올려야 메뉴가 생기는 헤더(Wavve 등)에 마우스 진입/이탈 이벤트를 보낸다.
// args: { action: 'open' | 'close' }. 반환: 이벤트를 보낸 요소 수
(function (args) {
  const visible = (el) => !!(el && el.offsetParent !== null && el.getClientRects().length > 0);
  const scopes = [...document.querySelectorAll('header, nav, [id*="header" i], [id*="gnb" i], [class*="header" i], [class*="gnb" i], [class*="navbar" i]')];
  const selector = '[class*="dropdown-trigger" i], [class*="dropdown" i] > button, [class*="profile" i], [class*="user-menu" i], [class*="usermenu" i], [class*="mypage" i], [class*="my-menu" i], [class*="account" i]';
  const triggers = scopes.flatMap((s) => [...s.querySelectorAll(selector)]).filter(visible).slice(0, 3);
  const names = args.action === 'open'
    ? ['pointerover', 'pointerenter', 'mouseover', 'mouseenter']
    : ['pointerout', 'pointerleave', 'mouseout', 'mouseleave'];
  for (const el of triggers) {
    for (const name of names) {
      const bubbles = !name.endsWith('enter') && !name.endsWith('leave');
      const init = { bubbles, cancelable: true, view: window };
      el.dispatchEvent(name.startsWith('pointer') ? new PointerEvent(name, init) : new MouseEvent(name, init));
    }
  }
  return triggers.length;
})(__ARGS__);

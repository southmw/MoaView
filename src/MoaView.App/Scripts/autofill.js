// 로그인 폼 자동 입력 / 제출 (04 아키텍처 5.3절, FR-ACC-04).
// args: { action: 'fill' | 'submit', loginId, password, idSelector?, passwordSelector?, submitSelector?, entrySelector?, allowEntryClick }
// 셀렉터가 없거나 맞지 않으면 "보이는 비밀번호 입력란 + 그 앞의 텍스트 입력란" 휴리스틱으로 찾는다.
// 폼이 없고 "로그인 방식 선택" 화면이면(TVING 등) 자체 ID 로그인 버튼을 눌러 폼으로 들어간다('entry-clicked').
// 반환: 'filled' | 'filled-password-only' | 'id-only' | 'entry-clicked' | 'submitted' | 'submitted-enter' | 'no-form'
// 값은 호스트가 JSON으로 직렬화해 넣는다. 이 스크립트는 값을 어디에도 기록하지 않는다.
(function (args) {
  const visible = (el) => !!(el && el.offsetParent !== null && !el.disabled && !el.readOnly && el.getClientRects().length > 0);
  const query = (selector) => {
    if (!selector) return null;
    try { return document.querySelector(selector); } catch { return null; }
  };
  const textInputs = (root) => [...root.querySelectorAll('input[type="text"],input[type="email"],input[type="tel"],input:not([type])')].filter(visible);
  const setValue = (el, value) => {
    const proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
    el.focus();
    setter.call(el, value);
    // React/Vue 등이 상태를 반영하도록 input/change를 발생시킨다.
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
    el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true }));
  };
  const findSubmit = (anchor, selector) => {
    let button = query(selector);
    if (visible(button)) return button;
    const form = anchor.form || anchor.closest('form');
    const scope = form || document;
    button = [...scope.querySelectorAll('button[type="submit"],input[type="submit"]')].find(visible);
    if (button) return button;
    const words = ['로그인', '계속', '다음', 'sign in', 'log in', 'login', 'continue', 'next'];
    return [...scope.querySelectorAll('button,[role="button"]')].find((b) => {
      if (!visible(b)) return false;
      const text = (b.innerText || b.value || '').trim().toLowerCase();
      return words.some((w) => text.includes(w));
    }) || null;
  };

  let password = query(args.passwordSelector);
  if (!visible(password)) {
    password = [...document.querySelectorAll('input[type="password"]')].find(visible) || null;
  }

  let id = query(args.idSelector);
  if (!visible(id)) {
    const scope = password ? (password.form || password.closest('form') || document) : document;
    const candidates = textInputs(scope);
    id = password
      ? candidates.filter((c) => c.compareDocumentPosition(password) & Node.DOCUMENT_POSITION_FOLLOWING).pop() || candidates[0] || null
      : candidates.find((c) => /id|email|user|login|account|아이디|이메일/i.test((c.name || '') + ' ' + (c.id || '') + ' ' + (c.placeholder || '') + ' ' + (c.autocomplete || ''))) || null;
  }

  if (args.action === 'submit') {
    const anchor = password || id;
    if (!anchor) return 'no-form';
    try { sessionStorage.setItem('moaview.loginSubmitted', String(Date.now())); } catch { /* 저장소 사용 불가 */ }
    const button = findSubmit(anchor, args.submitSelector);
    if (button) { button.click(); return 'submitted'; }
    const form = anchor.form || anchor.closest('form');
    if (form) { form.requestSubmit ? form.requestSubmit() : form.submit(); return 'submitted'; }
    anchor.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', keyCode: 13, bubbles: true }));
    anchor.dispatchEvent(new KeyboardEvent('keyup', { key: 'Enter', code: 'Enter', keyCode: 13, bubbles: true }));
    return 'submitted-enter';
  }

  // 제출 감지: 사용자가 로그인 버튼을 누르거나 Enter로 제출하면 같은 출처의 sessionStorage에 표시를 남긴다.
  // 호스트는 다음 일반 페이지에서 이 표시로 "실제로 로그인을 시도했는지"를 판단한다(쿠키 서명 학습 조건).
  if (!window.__moaSubmitHook) {
    window.__moaSubmitHook = true;
    const mark = () => { try { sessionStorage.setItem('moaview.loginSubmitted', String(Date.now())); } catch { /* 저장소 사용 불가 */ } };
    document.addEventListener('submit', mark, true);
    document.addEventListener('click', (e) => {
      const b = e.target && e.target.closest ? e.target.closest('button,input[type="submit"],[role="button"]') : null;
      if (b && (b.type === 'submit' || /로그인|sign in|log in|login|다음|next|continue|계속/i.test((b.innerText || b.value || '').trim()))) mark();
    }, true);
    document.addEventListener('keydown', (e) => { if (e.key === 'Enter') mark(); }, true);
  }

  // action === 'fill'
  if (!password) {
    // 이메일 먼저 받는 2단계 로그인: ID만 넣고 '계속'을 누르면 비밀번호 단계가 나온다.
    if (id && args.loginId) {
      if (id.value !== args.loginId) setValue(id, args.loginId);
      return 'id-only';
    }

    if (args.allowEntryClick) {
      // 로그인 방식 선택 화면: 셀렉터가 있으면 그것을, 없으면 "아이디/이메일(서비스명) 로그인" 버튼을 찾는다. 소셜·제휴 버튼은 제외.
      let entry = query(args.entrySelector);
      if (!visible(entry)) {
        const social = /(네이버|naver|카카오|kakao|구글|google|apple|애플|페이스북|facebook|cj\s*one|t\s*아이디|tid|회원가입|가입|join|sign\s*up)/i;
        const own = /(아이디|이메일|id|email)\s*(로|으로|로그인|login)|로그인$/i;
        entry = [...document.querySelectorAll('button,a,[role="button"]')].find((b) => {
          if (!visible(b)) return false;
          const t = (b.innerText || b.getAttribute('aria-label') || '').trim();
          return t.length <= 30 && own.test(t) && !social.test(t);
        }) || null;
      }
      if (entry) { entry.click(); return 'entry-clicked'; }
    }
    return 'no-form';
  }

  if (id && args.loginId && id.value !== args.loginId) setValue(id, args.loginId);
  if (password.value !== args.password) setValue(password, args.password);
  return id ? 'filled' : 'filled-password-only';
})(__ARGS__);

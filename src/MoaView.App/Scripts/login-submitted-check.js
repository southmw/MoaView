// 로그인 페이지에서 제출이 있었는지 확인하고 표시를 지운다(autofill.js가 남김). 반환: 제출 후 지난 밀리초, 없으면 -1
(function () {
  try {
    const v = sessionStorage.getItem('moaview.loginSubmitted');
    if (!v) return -1;
    sessionStorage.removeItem('moaview.loginSubmitted');
    const age = Date.now() - Number(v);
    return Number.isFinite(age) ? age : -1;
  } catch {
    return -1;
  }
})();

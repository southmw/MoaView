// PoC(Phase 0)에서 옮긴 영상 정보 조회. 페이지의 <video> 해상도·프레임 드롭·PiP 상태를 JSON 문자열로 반환한다.
// 설정 > 정보 또는 개발용 진단(Phase 3)에서 RunScriptForResultAsync로 사용 예정.
(() => {
  const pageUrl = location.origin + location.pathname;
  const videos = [...document.querySelectorAll('video')].map(v => {
    const q = v.getVideoPlaybackQuality ? v.getVideoPlaybackQuality() : null;
    return {
      size: v.videoWidth + 'x' + v.videoHeight,
      client: Math.round(v.clientWidth) + 'x' + Math.round(v.clientHeight),
      paused: v.paused,
      time: Math.round(v.currentTime),
      frames: q ? { total: q.totalVideoFrames, dropped: q.droppedVideoFrames } : null,
      src: (v.currentSrc || '').slice(0, 60)
    };
  });
  return JSON.stringify({
    url: pageUrl.slice(0, 80),
    screen: screen.width + 'x' + screen.height + ' @' + devicePixelRatio,
    viewport: innerWidth + 'x' + innerHeight,
    fullscreen: !!document.fullscreenElement,
    pip: !!document.pictureInPictureElement,
    pipEnabled: !!document.pictureInPictureEnabled,
    videos
  });
})();

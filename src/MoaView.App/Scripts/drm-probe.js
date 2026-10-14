// PoC(Phase 0)에서 옮긴 DRM·코덱 진단. EME(requestMediaKeySystemAccess)로 CDM 존재와 robustness, MSE 코덱, 디코딩 성능을 조사한다.
// 본 앱은 웹 메시지를 끄므로(10 보안 점검) 결과를 window.__moaDrmProbe에 JSON 문자열로 두고, 호출 측이 잠시 뒤
// `window.__moaDrmProbe` 를 다시 읽는다. 설정 > 정보의 DRM 진단(Phase 3)에서 사용 예정.
(async () => {
  const post = (result) => { window.__moaDrmProbe = JSON.stringify(result); };
  const h264 = 'video/mp4; codecs="avc1.640028"';
  const hevc = 'video/mp4; codecs="hvc1.1.6.L153.B0"';
  const aac = 'audio/mp4; codecs="mp4a.40.2"';
  const tests = [
    ['com.widevine.alpha', 'SW_SECURE_CRYPTO'],
    ['com.widevine.alpha', 'SW_SECURE_DECODE'],
    ['com.widevine.alpha', 'HW_SECURE_CRYPTO'],
    ['com.widevine.alpha', 'HW_SECURE_DECODE'],
    ['com.widevine.alpha', 'HW_SECURE_ALL'],
    ['com.microsoft.playready', ''],
    ['com.microsoft.playready.recommendation', ''],
    ['com.microsoft.playready.recommendation', '150'],
    ['com.microsoft.playready.recommendation', '2000'],
    ['com.microsoft.playready.recommendation', '3000'],
    ['com.microsoft.playready.recommendation.3000', ''],
    ['com.microsoft.playready.hardware', ''],
    ['com.apple.fps', ''],
  ];
  const eme = [];
  for (const [ks, rob] of tests) {
    for (const [label, ct] of [['h264', h264], ['hevc', hevc]]) {
      try {
        const cfg = [{
          initDataTypes: ['cenc'],
          videoCapabilities: [{ contentType: ct, robustness: rob }],
          audioCapabilities: [{ contentType: aac, robustness: '' }],
        }];
        const access = await navigator.requestMediaKeySystemAccess(ks, cfg);
        const got = access.getConfiguration();
        eme.push(`${ks} [${rob || '-'}] ${label}: OK (${(got.videoCapabilities || []).map(v => v.robustness || '-').join(',')})`);
      } catch (e) {
        eme.push(`${ks} [${rob || '-'}] ${label}: ${e.name}`);
      }
    }
  }
  const codecs = {
    h264: h264,
    hevc: hevc,
    hevc10: 'video/mp4; codecs="hvc1.2.4.L153.B0"',
    av1: 'video/mp4; codecs="av01.0.08M.08"',
    vp9: 'video/webm; codecs="vp09.00.40.08"',
    vp9_10bit: 'video/webm; codecs="vp09.02.40.10"',
  };
  const mse = Object.entries(codecs).map(([k, v]) => `${k}=${MediaSource.isTypeSupported(v)}`).join(' ');
  const probes = {};
  const dec = async (name, video) => {
    try {
      const r = await navigator.mediaCapabilities.decodingInfo({ type: 'media-source', video });
      probes[name] = `supported=${r.supported} smooth=${r.smooth} efficient=${r.powerEfficient}`;
    } catch (e) { probes[name] = e.name; }
  };
  await dec('h264_1080p', { contentType: h264, width: 1920, height: 1080, bitrate: 8000000, framerate: 30 });
  await dec('hevc10_4k_hdr', { contentType: codecs.hevc10, width: 3840, height: 2160, bitrate: 20000000, framerate: 24, hdrMetadataType: 'smpteSt2086', colorGamut: 'rec2020', transferFunction: 'pq' });
  await dec('vp9_4k', { contentType: codecs.vp9, width: 3840, height: 2160, bitrate: 20000000, framerate: 60 });
  await dec('av1_4k', { contentType: codecs.av1, width: 3840, height: 2160, bitrate: 20000000, framerate: 60 });
  post({ eme, mse, decoding: probes, hardwareConcurrency: navigator.hardwareConcurrency, screen: `${screen.width}x${screen.height}@${devicePixelRatio} depth=${screen.colorDepth}` });
})();

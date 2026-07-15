/**
 * スマホ・PC 共通の監視画面制御
 *
 * 画面初期化、プレビュー操作、録画操作、音声再生、RTSP 設定管理をまとめて扱う。
 */

const statePill = document.getElementById('statePill');
const liveBadge = document.getElementById('liveBadge');
const statusValue = document.getElementById('statusValue');
const recordingValue = document.getElementById('recordingValue');
const cameraValue = document.getElementById('cameraValue');
const pathValue = document.getElementById('pathValue');
const audioValue = document.getElementById('audioValue');
const localUrlValue = document.getElementById('localUrlValue');
const tailscaleUrlValue = document.getElementById('tailscaleUrlValue');
const recordStartBtn = document.getElementById('recordStartBtn');
const recordStopBtn = document.getElementById('recordStopBtn');
const recordTargetValue = document.getElementById('recordTargetValue');
const cameraSelect = document.getElementById('cameraSelect');
const networkCameraSelect = document.getElementById('networkCameraSelect');
const audioListenBtn = document.getElementById('audioListenBtn');
const audioVolumeRange = document.getElementById('audioVolumeRange');
const audioVolumeValue = document.getElementById('audioVolumeValue');
const audioLevelFill = document.getElementById('audioLevelFill');
const audioLevelValue = document.getElementById('audioLevelValue');
const networkConfigSelect = document.getElementById('networkConfigSelect');
const configCameraIdInput = document.getElementById('configCameraIdInput');
const configDisplayNameInput = document.getElementById('configDisplayNameInput');
const configHostAddressInput = document.getElementById('configHostAddressInput');
const configRtspUrlInput = document.getElementById('configRtspUrlInput');
const configEnabledCheck = document.getElementById('configEnabledCheck');
const configNewBtn = document.getElementById('configNewBtn');
const configSaveBtn = document.getElementById('configSaveBtn');
const configDeleteBtn = document.getElementById('configDeleteBtn');
const configCheckBtn = document.getElementById('configCheckBtn');
const configDetectBtn = document.getElementById('configDetectBtn');
const configApplyDetectedBtn = document.getElementById('configApplyDetectedBtn');
const detectedCameraSelect = document.getElementById('detectedCameraSelect');
const configPathValue = document.getElementById('configPathValue');
const configStatusValue = document.getElementById('configStatusValue');
const previewFrame = document.getElementById('previewFrame');
const previewShade = document.getElementById('previewShade');
const previewHeadline = document.getElementById('previewHeadline');
const previewDetail = document.getElementById('previewDetail');
const snapshot = document.getElementById('snapshot');
const previewZoomOutBtn = document.getElementById('previewZoomOutBtn');
const previewZoomValue = document.getElementById('previewZoomValue');
const previewZoomInBtn = document.getElementById('previewZoomInBtn');
const previewResetBtn = document.getElementById('previewResetBtn');

const audioVolumeStorageKey = 'remote-camera-audio-volume';
const previewMinScale = 1;
const previewMaxScale = 4;
const previewScaleStep = 0.25;

let actionBusy = false;
let cameraSwitchBusy = false;
let cameraReady = false;
let audioSocket = null;
let audioContext = null;
let audioGainNode = null;
let audioConnected = false;
let currentAudioLevel = 0;
let nextAudioTime = 0;
let lastDeviceSignature = '';
let lastNetworkCameraSignature = '';
let configBusy = false;
let lastConfigSignature = '';
let detectedCameraItems = [];
let previewScale = 1;
let previewOffsetX = 0;
let previewOffsetY = 0;
let previewDragActive = false;
let previewPinchActive = false;
let previewDragStartX = 0;
let previewDragStartY = 0;
let previewDragOriginX = 0;
let previewDragOriginY = 0;
let previewPinchStartDistance = 0;
let previewPinchStartScale = 1;
let previewMouseDragActive = false;

/**
 * 数値を最小値と最大値の範囲に収める。
 *
 * @param {number} value 対象の数値
 * @param {number} min 最小値
 * @param {number} max 最大値
 * @returns {number} 補正後の数値
 */
function clampValue(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

/**
 * タッチ2点間の距離を返す。
 *
 * @param {TouchList} touches タッチ一覧
 * @returns {number} 2点間距離
 */
function getTouchDistance(touches) {
  if (!touches || touches.length < 2) {
    return 0;
  }

  const diffX = touches[0].clientX - touches[1].clientX;
  const diffY = touches[0].clientY - touches[1].clientY;
  return Math.hypot(diffX, diffY);
}

/**
 * 現在の倍率に合わせて移動量を収める。
 */
function clampPreviewOffset() {
  const maxOffsetX = ((previewFrame.clientWidth || 0) * (previewScale - 1)) / 2;
  const maxOffsetY = ((previewFrame.clientHeight || 0) * (previewScale - 1)) / 2;

  // 画像が枠から完全に外れない範囲で止める。
  previewOffsetX = clampValue(previewOffsetX, -maxOffsetX, maxOffsetX);
  previewOffsetY = clampValue(previewOffsetY, -maxOffsetY, maxOffsetY);
}

/**
 * プレビュー画像の見た目を更新する。
 */
function applyPreviewTransform() {
  clampPreviewOffset();

  // 拡大率と移動量は transform に集約しておくと、差分更新が分かりやすい。
  snapshot.style.transform = 'translate(' + previewOffsetX + 'px, ' + previewOffsetY + 'px) scale(' + previewScale + ')';
  previewZoomValue.textContent = Math.round(previewScale * 100) + '%';
  previewZoomOutBtn.disabled = previewScale <= previewMinScale;
  previewZoomInBtn.disabled = previewScale >= previewMaxScale;
  previewResetBtn.disabled = previewScale <= previewMinScale;
  previewFrame.dataset.zoomed = previewScale > previewMinScale ? 'true' : 'false';
}

/**
 * プレビュー倍率と位置を初期状態へ戻す。
 */
function resetPreviewTransform() {
  previewScale = previewMinScale;
  previewOffsetX = 0;
  previewOffsetY = 0;
  previewDragActive = false;
  previewPinchActive = false;
  previewMouseDragActive = false;
  previewFrame.dataset.dragging = 'false';
  applyPreviewTransform();
}

/**
 * 拡大率を更新する。
 *
 * @param {number} nextScale 次の倍率
 */
function setPreviewScale(nextScale) {
  previewScale = clampValue(nextScale, previewMinScale, previewMaxScale);

  // 等倍に戻した時は位置も戻しておかないと、次回の操作感が悪くなる。
  if (previewScale <= previewMinScale) {
    previewOffsetX = 0;
    previewOffsetY = 0;
  }

  applyPreviewTransform();
}

/**
 * タッチドラッグ移動を開始する。
 *
 * @param {Touch} touch 開始タッチ
 */
function startPreviewDrag(touch) {
  if (!touch || previewScale <= previewMinScale) {
    previewDragActive = false;
    return;
  }

  previewDragActive = true;
  previewDragStartX = touch.clientX;
  previewDragStartY = touch.clientY;
  previewDragOriginX = previewOffsetX;
  previewDragOriginY = previewOffsetY;
}

/**
 * マウスドラッグ移動を開始する。
 *
 * @param {MouseEvent} event マウスイベント
 */
function startPreviewMouseDrag(event) {
  if (!event || previewScale <= previewMinScale) {
    previewMouseDragActive = false;
    return;
  }

  previewMouseDragActive = true;
  previewDragStartX = event.clientX;
  previewDragStartY = event.clientY;
  previewDragOriginX = previewOffsetX;
  previewDragOriginY = previewOffsetY;
  previewFrame.dataset.dragging = 'true';
}

/**
 * マウスドラッグ移動を終了する。
 */
function stopPreviewMouseDrag() {
  previewMouseDragActive = false;
  previewFrame.dataset.dragging = 'false';
}

/**
 * ツールバー操作中かどうかを返す。
 *
 * @param {EventTarget | null} target 判定対象
 * @returns {boolean} ツールバー上なら true
 */
function isPreviewToolbarActionTarget(target) {
  const targetElement = target instanceof Element ? target : null;
  return !!(targetElement && targetElement.closest('.preview-toolbar-actions'));
}

/**
 * プレビュー画像のタッチ開始を処理する。
 *
 * @param {TouchEvent} event タッチイベント
 */
function handlePreviewTouchStart(event) {
  if (!cameraReady || isPreviewToolbarActionTarget(event.target)) {
    return;
  }

  // 2本指ならピンチ操作として扱う。
  if (event.touches.length >= 2) {
    previewPinchActive = true;
    previewDragActive = false;
    previewPinchStartDistance = getTouchDistance(event.touches);
    previewPinchStartScale = previewScale;
    event.preventDefault();
    return;
  }

  // 1本指移動は、拡大中だけ有効にする。
  if (event.touches.length === 1 && previewScale > previewMinScale) {
    startPreviewDrag(event.touches[0]);
    event.preventDefault();
  }
}

/**
 * プレビュー画像のタッチ移動を処理する。
 *
 * @param {TouchEvent} event タッチイベント
 */
function handlePreviewTouchMove(event) {
  if (!cameraReady) {
    return;
  }

  if (event.touches.length >= 2 && previewPinchActive) {
    const nextDistance = getTouchDistance(event.touches);
    if (previewPinchStartDistance > 0 && nextDistance > 0) {
      // ピンチ開始時の倍率を基準にしておくと、途中でガタつきにくい。
      setPreviewScale(previewPinchStartScale * (nextDistance / previewPinchStartDistance));
    }

    event.preventDefault();
    return;
  }

  if (event.touches.length === 1 && previewDragActive) {
    previewOffsetX = previewDragOriginX + (event.touches[0].clientX - previewDragStartX);
    previewOffsetY = previewDragOriginY + (event.touches[0].clientY - previewDragStartY);
    applyPreviewTransform();
    event.preventDefault();
  }
}

/**
 * プレビュー画像のタッチ終了を処理する。
 *
 * @param {TouchEvent} event タッチイベント
 */
function handlePreviewTouchEnd(event) {
  if (event.touches.length >= 2) {
    previewPinchStartDistance = getTouchDistance(event.touches);
    previewPinchStartScale = previewScale;
    return;
  }

  if (event.touches.length === 1 && previewScale > previewMinScale) {
    previewPinchActive = false;
    startPreviewDrag(event.touches[0]);
    return;
  }

  previewDragActive = false;
  previewPinchActive = false;
}

/**
 * プレビュー画像のホイール拡大縮小を処理する。
 *
 * @param {WheelEvent} event ホイールイベント
 */
function handlePreviewWheel(event) {
  if (!cameraReady || isPreviewToolbarActionTarget(event.target)) {
    return;
  }

  const direction = event.deltaY < 0 ? 1 : -1;
  const nextScale = previewScale + (previewScaleStep * direction);
  setPreviewScale(nextScale);
  event.preventDefault();
}

/**
 * プレビュー画像のマウス押下を処理する。
 *
 * @param {MouseEvent} event マウスイベント
 */
function handlePreviewMouseDown(event) {
  if (!cameraReady || event.button !== 0 || isPreviewToolbarActionTarget(event.target)) {
    return;
  }

  startPreviewMouseDrag(event);
  if (previewMouseDragActive) {
    event.preventDefault();
  }
}

/**
 * プレビュー画像のマウス移動を処理する。
 *
 * @param {MouseEvent} event マウスイベント
 */
function handlePreviewMouseMove(event) {
  if (!previewMouseDragActive) {
    return;
  }

  previewOffsetX = previewDragOriginX + (event.clientX - previewDragStartX);
  previewOffsetY = previewDragOriginY + (event.clientY - previewDragStartY);
  applyPreviewTransform();
  event.preventDefault();
}

/**
 * プレビュー画像のマウス終了を処理する。
 */
function handlePreviewMouseUp() {
  if (!previewMouseDragActive) {
    return;
  }

  stopPreviewMouseDrag();
}

/**
 * カメラ選択 UI の活性状態を更新する。
 *
 * @param {any} data 状態 API の応答
 */
function updateCameraSelectState(data) {
  // 録画中や切替中は、途中で再選択されないように固定する。
  cameraSelect.disabled = actionBusy || cameraSwitchBusy || !!data.recording || cameraSelect.options.length === 0;
  networkCameraSelect.disabled = actionBusy || cameraSwitchBusy || !!data.recording || networkCameraSelect.options.length === 0;
}

/**
 * 音声ボタンと音声状態表示を更新する。
 *
 * @param {any} data 状態 API の応答
 */
function updateAudioButtonState(data) {
  const audioRunning = !!data.audioRunning;
  audioValue.textContent = data.audioStatus || (audioRunning ? '配信中' : '停止中');
  audioListenBtn.disabled = !audioRunning;
  audioListenBtn.textContent = audioConnected ? '音声を止める' : '音声を聴く';

  // 配信も再生も止まっていれば、メーターはゼロに戻す。
  if (!audioRunning && !audioConnected) {
    setAudioLevel(0);
  }
}

/**
 * RTSP 設定関連ボタンの活性状態を更新する。
 */
function updateConfigButtonsState() {
  const disabled = configBusy;

  networkConfigSelect.disabled = disabled || networkConfigSelect.options.length === 0;
  configCameraIdInput.disabled = disabled;
  configDisplayNameInput.disabled = disabled;
  configHostAddressInput.disabled = disabled;
  configRtspUrlInput.disabled = disabled;
  configEnabledCheck.disabled = disabled;
  configNewBtn.disabled = disabled;
  configSaveBtn.disabled = disabled;
  configDeleteBtn.disabled = disabled;
  configCheckBtn.disabled = disabled;
  configDetectBtn.disabled = disabled;
  configApplyDetectedBtn.disabled = disabled || detectedCameraSelect.options.length === 0;
  detectedCameraSelect.disabled = disabled || detectedCameraSelect.options.length === 0;
}

/**
 * 録画ボタン周りの表示を更新する。
 *
 * @param {any} data 状態 API の応答
 */
function updateRecordingControls(data) {
  const defaultTarget = (data.defaultRecordingDirectory || 'C:\\RemoteCamera') + '\\';
  const targetText = data.recordingPath || data.recordingTargetPath || defaultTarget;

  recordTargetValue.textContent = targetText;
  recordStartBtn.disabled = actionBusy || !!data.recording;
  recordStopBtn.disabled = actionBusy || !data.recording;
  recordStartBtn.textContent = data.recording ? '録画中' : '録画';
  updateCameraSelectState(data);
  updateAudioButtonState(data);
}

/**
 * ローカルカメラ選択肢の表示文言を組み立てる。
 *
 * @param {any} device カメラ情報
 * @returns {string} 表示文字列
 */
function buildCameraOptionLabel(device) {
  return device.displayName + ' (' + device.captureIndex + ')';
}

/**
 * ローカルカメラ一覧を更新する。
 *
 * @param {number | undefined} preferredCaptureIndex 優先して選びたい captureIndex
 */
async function refreshDevices(preferredCaptureIndex) {
  try {
    const response = await fetch('/devices', { cache: 'no-store' });
    if (!response.ok) {
      throw new Error('devices');
    }

    const data = await response.json();
    const signature = JSON.stringify(data.devices || []);
    const currentCaptureIndex = typeof preferredCaptureIndex === 'number'
      ? preferredCaptureIndex
      : data.currentCaptureIndex;

    if (signature !== lastDeviceSignature) {
      lastDeviceSignature = signature;
      cameraSelect.innerHTML = '';

      // 一覧が変わった時だけ option を作り直す。
      for (const device of data.devices || []) {
        const option = document.createElement('option');
        option.value = String(device.captureIndex);
        option.textContent = buildCameraOptionLabel(device);
        cameraSelect.appendChild(option);
      }
    }

    if (typeof currentCaptureIndex === 'number') {
      cameraSelect.value = String(currentCaptureIndex);
    }

    cameraSelect.disabled = cameraSwitchBusy || cameraSelect.options.length === 0;
  } catch {
    cameraSelect.disabled = true;
  }
}

/**
 * ネットワークカメラ選択肢の表示文言を組み立てる。
 *
 * @param {any} camera カメラ情報
 * @returns {string} 表示文字列
 */
function buildNetworkCameraOptionLabel(camera) {
  if (camera.hostAddress) {
    return camera.displayName + ' / ' + camera.hostAddress + ' (RTSPカメラ)';
  }

  return camera.displayName + ' (RTSPカメラ)';
}

/**
 * ネットワークカメラ一覧を更新する。
 *
 * @param {string | undefined} preferredCameraId 優先して選びたい cameraId
 */
async function refreshNetworkCameras(preferredCameraId) {
  try {
    const response = await fetch('/network-cameras', { cache: 'no-store' });
    if (!response.ok) {
      throw new Error('network-cameras');
    }

    const data = await response.json();
    const signature = JSON.stringify(data.cameras || []);
    const currentCameraId = typeof preferredCameraId === 'string' && preferredCameraId.length > 0
      ? preferredCameraId
      : data.currentCameraId;

    if (signature !== lastNetworkCameraSignature) {
      lastNetworkCameraSignature = signature;
      networkCameraSelect.innerHTML = '';

      for (const camera of data.cameras || []) {
        const option = document.createElement('option');
        option.value = camera.cameraId;
        option.textContent = buildNetworkCameraOptionLabel(camera);
        networkCameraSelect.appendChild(option);
      }
    }

    if (typeof currentCameraId === 'string' && currentCameraId.length > 0) {
      networkCameraSelect.value = currentCameraId;
    }

    networkCameraSelect.disabled = cameraSwitchBusy || networkCameraSelect.options.length === 0;
  } catch {
    networkCameraSelect.disabled = true;
  }
}

/**
 * ローカルカメラを切り替える。
 *
 * @param {number} captureIndex 切替先 captureIndex
 */
async function selectCamera(captureIndex) {
  if (cameraSwitchBusy) {
    return;
  }

  cameraSwitchBusy = true;
  cameraSelect.disabled = true;

  try {
    const response = await fetch('/camera/select', {
      method: 'POST',
      cache: 'no-store',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ captureIndex })
    });

    const data = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(data && data.message ? data.message : 'camera');
    }
  } catch {
    // 詳細表示は状態 API の取り直しに任せる。
  } finally {
    cameraSwitchBusy = false;
  }

  await refreshStatus();
  await refreshDevices(captureIndex);
  await refreshNetworkCameras();
}

/**
 * ネットワークカメラを切り替える。
 *
 * @param {string} cameraId 切替先 cameraId
 */
async function selectNetworkCamera(cameraId) {
  if (cameraSwitchBusy) {
    return;
  }

  cameraSwitchBusy = true;
  networkCameraSelect.disabled = true;

  try {
    const response = await fetch('/network-camera/select', {
      method: 'POST',
      cache: 'no-store',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ cameraId })
    });

    const data = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(data && data.message ? data.message : 'network-camera');
    }
  } catch {
    // 詳細表示は状態 API の取り直しに任せる。
  } finally {
    cameraSwitchBusy = false;
  }

  await refreshStatus();
  await refreshDevices();
  await refreshNetworkCameras(cameraId);
}

/**
 * 音声再生用の AudioContext を準備する。
 */
async function ensureAudioContext() {
  if (!audioContext) {
    const AudioContextClass = window.AudioContext || window.webkitAudioContext;
    audioContext = new AudioContextClass();
  }

  if (!audioGainNode) {
    audioGainNode = audioContext.createGain();
    audioGainNode.connect(audioContext.destination);

    // 保存済みの音量を即反映しておく。
    applyAudioVolumeValue(audioVolumeRange.value);
  }

  if (audioContext.state === 'suspended') {
    await audioContext.resume();
  }
}

/**
 * スマホ側の再生音量を反映する。
 *
 * @param {string} rawValue スライダーの値
 */
function applyAudioVolumeValue(rawValue) {
  const numericValue = Number(rawValue);
  const safeValue = Number.isFinite(numericValue)
    ? Math.max(0, Math.min(100, numericValue))
    : 100;

  audioVolumeRange.value = String(safeValue);
  audioVolumeValue.textContent = safeValue + '%';
  localStorage.setItem(audioVolumeStorageKey, String(safeValue));

  if (audioGainNode) {
    audioGainNode.gain.value = safeValue / 100;
  }
}

/**
 * 保存済みの音量を読み込む。
 */
function initializeAudioVolume() {
  const savedValue = localStorage.getItem(audioVolumeStorageKey);
  applyAudioVolumeValue(savedValue || '100');
}

/**
 * 空の RTSP 設定オブジェクトを返す。
 *
 * @returns {{enabled: boolean, cameraId: string, displayName: string, hostAddress: string, rtspUrl: string}} 初期値
 */
function buildEmptyNetworkConfig() {
  return {
    enabled: true,
    cameraId: '',
    displayName: '',
    hostAddress: '',
    rtspUrl: ''
  };
}

/**
 * RTSP 設定フォームへ値を反映する。
 *
 * @param {any} item 設定情報
 */
function applyNetworkConfigForm(item) {
  const config = item || buildEmptyNetworkConfig();

  // 未設定時も空文字を入れて、input の表示ゆれを防ぐ。
  configCameraIdInput.value = config.cameraId || '';
  configDisplayNameInput.value = config.displayName || '';
  configHostAddressInput.value = config.hostAddress || '';
  configRtspUrlInput.value = config.rtspUrl || '';
  configEnabledCheck.checked = config.enabled !== false;
}

/**
 * RTSP 設定フォームの内容を送信用データへ組み立てる。
 *
 * @returns {{enabled: boolean, cameraId: string, displayName: string, hostAddress: string, rtspUrl: string}} 送信用データ
 */
function createNetworkConfigPayload() {
  return {
    enabled: !!configEnabledCheck.checked,
    cameraId: configCameraIdInput.value.trim(),
    displayName: configDisplayNameInput.value.trim(),
    hostAddress: configHostAddressInput.value.trim(),
    rtspUrl: configRtspUrlInput.value.trim()
  };
}

/**
 * 保存済みの RTSP 設定一覧を更新する。
 *
 * @param {string | undefined} preferredCameraId 優先して選びたい cameraId
 */
async function refreshNetworkCameraConfigs(preferredCameraId) {
  try {
    const response = await fetch('/network-camera-configs', { cache: 'no-store' });
    if (!response.ok) {
      throw new Error('network-camera-configs');
    }

    const data = await response.json();
    const items = data.items || [];
    const signature = JSON.stringify(items);
    const currentCameraId = typeof preferredCameraId === 'string' && preferredCameraId.length > 0
      ? preferredCameraId
      : networkConfigSelect.value;

    configPathValue.textContent = data.configPath || '-';

    if (signature !== lastConfigSignature) {
      lastConfigSignature = signature;
      networkConfigSelect.innerHTML = '';

      for (const item of items) {
        const option = document.createElement('option');
        option.value = item.cameraId;
        option.textContent = item.displayName + (item.enabled ? ' [有効]' : ' [無効]');
        option.dataset.item = JSON.stringify(item);
        networkConfigSelect.appendChild(option);
      }
    }

    if (currentCameraId) {
      networkConfigSelect.value = currentCameraId;
    }

    if (!networkConfigSelect.value && networkConfigSelect.options.length > 0) {
      networkConfigSelect.selectedIndex = 0;
    }

    if (networkConfigSelect.value) {
      const selectedOption = networkConfigSelect.selectedOptions[0];
      if (selectedOption && selectedOption.dataset.item) {
        applyNetworkConfigForm(JSON.parse(selectedOption.dataset.item));
      }
    } else if (items.length === 0) {
      applyNetworkConfigForm(buildEmptyNetworkConfig());
    }
  } catch {
    configStatusValue.textContent = '設定一覧の取得に失敗しました。';
  } finally {
    updateConfigButtonsState();
  }
}

/**
 * 自動検出した RTSP 候補を一覧へ描画する。
 *
 * @param {any[]} items 検出結果一覧
 */
function renderDetectedCameraItems(items) {
  detectedCameraItems = Array.isArray(items) ? items : [];
  detectedCameraSelect.innerHTML = '';

  for (const item of detectedCameraItems) {
    const option = document.createElement('option');
    option.value = item.cameraId;
    option.textContent = item.displayName + ' [' + item.hostAddress + ']';
    detectedCameraSelect.appendChild(option);
  }

  updateConfigButtonsState();
}

/**
 * 音声レベル表示を更新する。
 *
 * @param {number} level 0.0 から 1.0 の音声レベル
 */
function setAudioLevel(level) {
  const safeLevel = Number.isFinite(level)
    ? Math.max(0, Math.min(1, level))
    : 0;

  currentAudioLevel = safeLevel;
  audioLevelFill.style.width = Math.round(safeLevel * 100) + '%';
  audioLevelValue.textContent = Math.round(safeLevel * 100) + '%';
}

/**
 * PCM データから音声レベルを計算する。
 *
 * @param {Int16Array} pcm PCM 音声データ
 * @returns {number} 0.0 から 1.0 の音声レベル
 */
function calculateAudioLevel(pcm) {
  if (!pcm || pcm.length === 0) {
    return 0;
  }

  let peak = 0;

  // 平均値ではなくピーク値を見ると、監視用途では反応が分かりやすい。
  for (let index = 0; index < pcm.length; index++) {
    const sample = Math.abs(pcm[index]);
    if (sample > peak) {
      peak = sample;
    }
  }

  return Math.min(1, peak / 32768);
}

/**
 * 音声ストリームを閉じる。
 */
function closeAudioStream() {
  audioConnected = false;
  nextAudioTime = 0;
  setAudioLevel(0);

  if (audioSocket) {
    try {
      audioSocket.close();
    } catch {
      // 切断失敗でも画面側の状態復旧を優先する。
    }
  }

  audioSocket = null;
  audioListenBtn.textContent = '音声を聴く';
}

/**
 * 受信した PCM データを再生キューへ積む。
 *
 * @param {ArrayBuffer} arrayBuffer 受信データ
 */
function scheduleAudioChunk(arrayBuffer) {
  if (!audioContext) {
    return;
  }

  const pcm = new Int16Array(arrayBuffer);
  if (pcm.length === 0) {
    return;
  }

  const incomingLevel = calculateAudioLevel(pcm);
  if (incomingLevel >= currentAudioLevel) {
    setAudioLevel(incomingLevel);
  }

  const buffer = audioContext.createBuffer(1, pcm.length, 16000);
  const channelData = buffer.getChannelData(0);

  // Int16 PCM を Web Audio で扱える float へ変換する。
  for (let index = 0; index < pcm.length; index++) {
    channelData[index] = pcm[index] / 32768;
  }

  const source = audioContext.createBufferSource();
  source.buffer = buffer;
  source.connect(audioGainNode);

  // 次の開始時刻を少し先に寄せて、細かい途切れを減らす。
  const startTime = Math.max(audioContext.currentTime + 0.04, nextAudioTime);
  source.start(startTime);
  nextAudioTime = startTime + buffer.duration;
}

/**
 * 音声 WebSocket を開く。
 */
async function openAudioStream() {
  await ensureAudioContext();

  const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
  audioSocket = new WebSocket(protocol + '//' + location.host + '/audio/ws');
  audioSocket.binaryType = 'arraybuffer';

  audioSocket.onopen = () => {
    audioConnected = true;
    audioListenBtn.textContent = '音声を止める';
  };

  audioSocket.onmessage = event => {
    scheduleAudioChunk(event.data);
  };

  audioSocket.onclose = () => {
    closeAudioStream();
  };

  audioSocket.onerror = () => {
    closeAudioStream();
  };
}

/**
 * RTSP 設定を保存する。
 */
async function saveNetworkConfig() {
  configBusy = true;
  updateConfigButtonsState();

  try {
    const payload = createNetworkConfigPayload();
    const response = await fetch('/network-camera-config/save', {
      method: 'POST',
      cache: 'no-store',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(payload)
    });

    const data = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(data && data.message ? data.message : 'save');
    }

    configStatusValue.textContent = data.message || '設定を保存しました。';
    await refreshNetworkCameraConfigs(payload.cameraId);
    await refreshNetworkCameras(payload.cameraId);
  } catch (error) {
    configStatusValue.textContent = error && error.message ? error.message : '設定保存に失敗しました。';
  } finally {
    configBusy = false;
    updateConfigButtonsState();
  }
}

/**
 * RTSP 設定を削除する。
 */
async function deleteNetworkConfig() {
  const cameraId = configCameraIdInput.value.trim();
  if (!cameraId) {
    configStatusValue.textContent = '削除する識別子がありません。';
    return;
  }

  configBusy = true;
  updateConfigButtonsState();

  try {
    const response = await fetch('/network-camera-config/delete', {
      method: 'POST',
      cache: 'no-store',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ cameraId })
    });

    const data = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(data && data.message ? data.message : 'delete');
    }

    configStatusValue.textContent = data.message || '設定を削除しました。';
    applyNetworkConfigForm(buildEmptyNetworkConfig());
    await refreshNetworkCameraConfigs();
    await refreshNetworkCameras();
  } catch (error) {
    configStatusValue.textContent = error && error.message ? error.message : '設定削除に失敗しました。';
  } finally {
    configBusy = false;
    updateConfigButtonsState();
  }
}

/**
 * RTSP 設定の通信確認を行う。
 */
async function checkNetworkConfig() {
  configBusy = true;
  updateConfigButtonsState();

  try {
    const response = await fetch('/network-camera-config/check', {
      method: 'POST',
      cache: 'no-store',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(createNetworkConfigPayload())
    });

    const data = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(data && data.message ? data.message : 'check');
    }

    configStatusValue.textContent = data.statusText || '通信確認が完了しました。';
  } catch (error) {
    configStatusValue.textContent = error && error.message ? error.message : '通信確認に失敗しました。';
  } finally {
    configBusy = false;
    updateConfigButtonsState();
  }
}

/**
 * 同一 LAN 上の RTSP 候補を自動検出する。
 */
async function detectNetworkConfigs() {
  configBusy = true;
  configStatusValue.textContent = '同一 LAN の RTSP 候補を自動検出しています。';
  updateConfigButtonsState();

  try {
    const response = await fetch('/network-camera-config/detect', {
      method: 'POST',
      cache: 'no-store'
    });

    const data = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(data && data.message ? data.message : 'detect');
    }

    renderDetectedCameraItems(data.items || []);
    configStatusValue.textContent = (data.items || []).length === 0
      ? '候補は見つかりませんでした。'
      : (data.items || []).length + ' 件の候補を検出しました。';
  } catch (error) {
    configStatusValue.textContent = error && error.message ? error.message : '自動検出に失敗しました。';
  } finally {
    configBusy = false;
    updateConfigButtonsState();
  }
}

/**
 * 監視状態を更新する。
 */
async function refreshStatus() {
  try {
    const response = await fetch('/status', { cache: 'no-store' });
    if (!response.ok) {
      throw new Error('status');
    }

    const data = await response.json();
    cameraReady = !!data.cameraReady;

    const recordingText = data.recording ? '録画中' : '停止中';
    const sourceText = data.cameraSourceType === 'NetworkRtsp' ? 'RTSP' : 'ローカル';
    const cameraText = data.cameraName ? data.cameraName + ' (' + sourceText + ')' : '未選択';
    const pathText = data.recordingPath ? data.recordingPath : '未選択';
    const statusText = data.cameraStatus ? data.cameraStatus : '未起動';

    statusValue.textContent = statusText;
    recordingValue.textContent = recordingText;
    cameraValue.textContent = cameraText;
    pathValue.textContent = pathText;
    localUrlValue.textContent = data.localUrl || localUrlValue.textContent;
    tailscaleUrlValue.textContent = data.tailscaleUrl ? data.tailscaleUrl : '未検出';

    const state = data.recording ? 'recording' : cameraReady ? 'ready' : 'waiting';
    statePill.dataset.state = state;
    statePill.textContent = data.recording ? '録画中' : cameraReady ? '待機中' : '起動待ち';
    liveBadge.textContent = data.recording ? 'REC' : cameraReady ? 'LIVE' : 'WAIT';

    if (cameraReady) {
      previewShade.style.display = 'none';
      snapshot.style.display = 'block';
      refreshSnapshot();
      previewHeadline.textContent = data.recording ? '録画しながら監視中です。' : 'ライブプレビューを表示しています。';
      previewDetail.textContent = data.recording ? '録画中はカメラ切替を一時停止しています。' : 'スマホでも見やすい比率で最新フレームを更新します。';
    } else {
      previewShade.style.display = 'grid';
      snapshot.style.display = 'none';
      previewHeadline.textContent = 'カメラの準備中です。';
      previewDetail.textContent = 'USBカメラが使えるようになると、ここに最新フレームが表示されます。';
    }

    updateRecordingControls(data);
    if (!cameraSwitchBusy) {
      await refreshDevices(data.cameraCaptureIndex);
      await refreshNetworkCameras(data.networkCameraId);
    }
  } catch {
    statusValue.textContent = '状態の取得に失敗しました。';
    statePill.dataset.state = 'waiting';
    statePill.textContent = '通信エラー';
    liveBadge.textContent = 'WAIT';
    previewShade.style.display = 'grid';
    snapshot.style.display = 'none';
    previewHeadline.textContent = '状態取得に失敗しました。';
    previewDetail.textContent = 'しばらく待って再読込してください。';
    recordStartBtn.disabled = true;
    recordStopBtn.disabled = true;
    cameraSelect.disabled = true;
    networkCameraSelect.disabled = true;
    audioListenBtn.disabled = true;
  }
}

/**
 * 録画開始・停止を実行する。
 *
 * @param {string} url 実行先 URL
 * @param {string} busyText 実行中のボタン表示
 */
async function executeRecordingAction(url, busyText) {
  if (actionBusy) {
    return;
  }

  actionBusy = true;
  recordStartBtn.disabled = true;
  recordStopBtn.disabled = true;
  recordStartBtn.textContent = busyText;

  try {
    const response = await fetch(url, { method: 'POST', cache: 'no-store' });
    const data = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(data && data.message ? data.message : 'record');
    }
  } catch {
    // 結果表示は次の状態取得で揃える。
  } finally {
    actionBusy = false;
  }

  await refreshStatus();
}

/**
 * 最新スナップショットを読み直す。
 */
function refreshSnapshot() {
  if (!cameraReady) {
    return;
  }

  // キャッシュ回避のためにタイムスタンプを付ける。
  snapshot.src = '/snapshot.jpg?t=' + Date.now();
}

/**
 * 画面イベントを束ねて登録する。
 */
function bindEvents() {
  recordStartBtn.addEventListener('click', () => executeRecordingAction('/record/start', '開始中'));
  recordStopBtn.addEventListener('click', () => executeRecordingAction('/record/stop', '停止中'));

  cameraSelect.addEventListener('change', () => {
    const selectedValue = Number(cameraSelect.value);
    if (!Number.isNaN(selectedValue)) {
      selectCamera(selectedValue);
    }
  });

  networkCameraSelect.addEventListener('change', () => {
    const selectedValue = networkCameraSelect.value;
    if (selectedValue) {
      selectNetworkCamera(selectedValue);
    }
  });

  networkConfigSelect.addEventListener('change', () => {
    const selectedOption = networkConfigSelect.selectedOptions[0];
    if (selectedOption && selectedOption.dataset.item) {
      applyNetworkConfigForm(JSON.parse(selectedOption.dataset.item));
      configStatusValue.textContent = '登録済み設定を読み込みました。';
    }
  });

  audioVolumeRange.addEventListener('input', () => {
    applyAudioVolumeValue(audioVolumeRange.value);
  });

  configNewBtn.addEventListener('click', () => {
    networkConfigSelect.value = '';
    applyNetworkConfigForm(buildEmptyNetworkConfig());
    configStatusValue.textContent = '新規入力へ切り替えました。';
  });

  configSaveBtn.addEventListener('click', () => {
    saveNetworkConfig();
  });

  configDeleteBtn.addEventListener('click', () => {
    deleteNetworkConfig();
  });

  configCheckBtn.addEventListener('click', () => {
    checkNetworkConfig();
  });

  configDetectBtn.addEventListener('click', () => {
    detectNetworkConfigs();
  });

  configApplyDetectedBtn.addEventListener('click', () => {
    const selectedItem = detectedCameraItems.find(item => item.cameraId === detectedCameraSelect.value);
    if (!selectedItem) {
      configStatusValue.textContent = '検出候補を選択してください。';
      return;
    }

    applyNetworkConfigForm({
      enabled: true,
      cameraId: selectedItem.cameraId,
      displayName: selectedItem.displayName,
      hostAddress: selectedItem.hostAddress,
      rtspUrl: selectedItem.rtspUrl
    });
    configStatusValue.textContent = selectedItem.statusText;
  });

  audioListenBtn.addEventListener('click', async () => {
    if (audioConnected) {
      closeAudioStream();
      return;
    }

    try {
      await openAudioStream();
    } catch {
      closeAudioStream();
    }
  });

  previewZoomOutBtn.addEventListener('click', () => {
    setPreviewScale(previewScale - previewScaleStep);
  });

  previewZoomInBtn.addEventListener('click', () => {
    setPreviewScale(previewScale + previewScaleStep);
  });

  previewResetBtn.addEventListener('click', () => {
    resetPreviewTransform();
  });

  previewFrame.addEventListener('wheel', handlePreviewWheel, { passive: false });
  previewFrame.addEventListener('mousedown', handlePreviewMouseDown);
  window.addEventListener('mousemove', handlePreviewMouseMove);
  window.addEventListener('mouseup', handlePreviewMouseUp);
  previewFrame.addEventListener('mouseleave', handlePreviewMouseUp);
  previewFrame.addEventListener('touchstart', handlePreviewTouchStart, { passive: false });
  previewFrame.addEventListener('touchmove', handlePreviewTouchMove, { passive: false });
  previewFrame.addEventListener('touchend', handlePreviewTouchEnd, { passive: false });
  previewFrame.addEventListener('touchcancel', handlePreviewTouchEnd, { passive: false });

  snapshot.onload = () => {
    applyPreviewTransform();
    previewShade.style.display = 'none';
    snapshot.style.display = 'block';
  };

  snapshot.onerror = () => {
    if (cameraReady) {
      previewHeadline.textContent = 'プレビュー画像の取得に失敗しました。';
      previewDetail.textContent = 'カメラが一時的に応答していない可能性があります。';
      previewShade.style.display = 'grid';
      snapshot.style.display = 'none';
    }
  };
}

/**
 * 定期更新を開始する。
 */
function startTimers() {
  setInterval(refreshStatus, 1000);
  setInterval(refreshDevices, 5000);
  setInterval(refreshNetworkCameras, 5000);
  setInterval(refreshSnapshot, 500);
  setInterval(() => {
    // レベルメーターは少しずつ減衰させて、見た目の追従性を作る。
    if (currentAudioLevel <= 0.01) {
      if (currentAudioLevel !== 0) {
        setAudioLevel(0);
      }

      return;
    }

    setAudioLevel(currentAudioLevel * 0.84);
  }, 120);
}

/**
 * 監視画面を初期化する。
 */
function initializeMonitorPage() {
  bindEvents();

  // 初期表示時に一覧と状態を一度そろえる。
  refreshDevices();
  refreshNetworkCameras();
  refreshNetworkCameraConfigs();
  initializeAudioVolume();
  applyPreviewTransform();
  refreshStatus();
  startTimers();
}

initializeMonitorPage();

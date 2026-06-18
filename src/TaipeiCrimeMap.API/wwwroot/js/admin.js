'use strict';

(function () {
  const PING_URL = '/api/crime/coordinate/ping';
  const PATCH_URL = '/api/crime/coordinate';
  const CACHE_CLEAR_URL = '/api/admin/cache/clear';
  const STORAGE_KEY = 'adminAuthCredentials';
  const MAX_HISTORY = 60;
  const OFFLINE_MS = 5_000;   // 5 s → 灰色離線
  const REMOVE_MS  = 15_000;  // 15 s → 從 DOM 與 Map 完全移除

  // CPU 折線圖色盤（每台 Server 一個顏色）
  const CPU_COLORS = [
    '#ef4444', '#3b82f6', '#10b981', '#f59e0b',
    '#8b5cf6', '#ec4899', '#14b8a6', '#f97316',
  ];

  // DOM refs
  const loginSection = document.getElementById('login-section');
  const formSection = document.getElementById('form-section');
  const loginForm = document.getElementById('login-form');
  const loginError = document.getElementById('login-error');
  const updateForm = document.getElementById('update-form');
  const resultEl = document.getElementById('result');
  const logoutBtn = document.getElementById('btn-logout');
  const clearCacheBtn = document.getElementById('btn-clear-cache');
  const wsStatusEl = document.getElementById('ws-status');

  // 靜態硬體資訊 DOM
  const hwInfo = document.getElementById('hw-info');
  const hwCores = document.getElementById('hw-cores');
  const hwTotalMem = document.getElementById('hw-total-mem');
  const hwOs = document.getElementById('hw-os');
  const hwDotnet = document.getElementById('hw-dotnet');

  // 多機狀態
  // Map<hostId, { lastSeen: number, colorIdx: number }>
  const serverState = new Map();
  let colorNext = 0;

  // Chart.js
  let metricsChart = null;
  const timeLabels = [];
  let lastLabelMs = 0;
  // Map<hostId, number[]>  — cpuData
  const serverCpuData = new Map();

  // WebSocket state
  let ws = null;
  let _hwInfoShown = false;
  let offlineTimer = null;

  // ── Credentials ────────────────────────────────────────────────
  function getStoredCredentials() { return sessionStorage.getItem(STORAGE_KEY); }
  function setStoredCredentials(v) { sessionStorage.setItem(STORAGE_KEY, v); }
  function clearStoredCredentials() { sessionStorage.removeItem(STORAGE_KEY); }

  async function checkAuth(credentials) {
    const r = await fetch(PING_URL, { headers: { Authorization: `Basic ${credentials}` } });
    return r.ok;
  }

  function showLogin() {
    loginSection.hidden = false;
    formSection.hidden = true;
    closeWebSocket();
  }

  function showForm() {
    loginSection.hidden = true;
    formSection.hidden = false;
    try { ensureChart(); } catch (e) { console.warn('[admin] ensureChart failed:', e); }
    openWebSocket();
    startOfflineCheck();
  }

  async function init() {
    const stored = getStoredCredentials();
    if (stored && (await checkAuth(stored))) showForm();
    else { clearStoredCredentials(); showLogin(); }
  }

  // ── Tab switching ──────────────────────────────────────────────
  document.querySelectorAll('.tab-btn').forEach((btn) => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.tab-btn').forEach((b) => b.classList.remove('active'));
      btn.classList.add('active');

      const tabName = btn.dataset.tab;
      document.querySelectorAll('.tab-panel').forEach((panel) => {
        panel.style.display = panel.id === `tab-${tabName}` ? '' : 'none';
      });

      if (tabName === 'monitor') {
        ensureChart();
        openWebSocket();
        startOfflineCheck();
      } else {
        closeWebSocket();
        stopOfflineCheck();
      }
    });
  });

  // ── WebSocket ──────────────────────────────────────────────────
  function openWebSocket() {
    if (ws && ws.readyState <= WebSocket.OPEN) return;

    const credentials = getStoredCredentials();
    if (!credentials) return;

    // 重連時清空所有舊卡片與 chart，避免多張相同 HostId 的卡片殘留
    clearAllServerBlocks();
    _hwInfoShown = false;

    const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${proto}//${location.host}/ws/metrics?token=${encodeURIComponent(credentials)}`;

    setWsStatus('connecting');
    ws = new WebSocket(url);
    ws.addEventListener('open', () => setWsStatus('connected'));
    ws.addEventListener('message', (event) => {
      try {
        const data = JSON.parse(event.data);
        if (!_hwInfoShown && data.hostId) { populateHwInfo(data); _hwInfoShown = true; }
        handleServerData(data);
      } catch (_) { /* ignore malformed */ }
    });
    ws.addEventListener('close', () => setWsStatus('disconnected'));
    ws.addEventListener('error', () => setWsStatus('error'));
  }

  function closeWebSocket() {
    if (ws) { ws.close(); ws = null; }
    _hwInfoShown = false;
    setWsStatus('disconnected');
  }

  function setWsStatus(state) {
    if (!wsStatusEl) return;
    wsStatusEl.className = 'ws-status';
    const map = {
      connected: ['⬤ 已連線', 'connected'],
      connecting: ['⬤ 連線中…', ''],
      error: ['⬤ 連線錯誤', 'error'],
    };
    const [text, cls] = map[state] || ['⬤ 未連線', ''];
    wsStatusEl.textContent = text;
    if (cls) wsStatusEl.classList.add(cls);
  }

  // ── Static hw info ─────────────────────────────────────────────
  function populateHwInfo(data) {
    hwCores.textContent = data.cpuCores ?? '—';
    hwTotalMem.textContent = data.totalMemoryMb ? `${data.totalMemoryMb} MB` : '—';
    hwOs.textContent = data.osDescription ?? '—';
    hwDotnet.textContent = data.dotNetVersion ?? '—';
    hwInfo.style.display = '';
  }

  // ── Per-server handling ────────────────────────────────────────
  function handleServerData(data) {
    const hostId = data.hostId ?? 'unknown';
    const shortId = hostId;

    // 確保 state 和 chart dataset 存在
    if (!serverState.has(hostId)) {
      serverState.set(hostId, { colorIdx: colorNext++ % CPU_COLORS.length });
      createServerBlock(hostId, shortId, data.environment ?? '');
      addChartDataset(hostId, serverState.get(hostId).colorIdx);
    }

    // 更新 lastSeen
    serverState.get(hostId).lastSeen = Date.now();

    // 更新卡片數值
    updateServerBlock(hostId, data);

    // 更新 CPU 折線圖
    pushCpuPoint(hostId, data.cpuPercent ?? 0);

    // 確保卡片在線
    const block = document.getElementById(`sb-${shortId}`);
    if (block) block.classList.remove('offline');
    const dot = block?.querySelector('.online-dot');
    if (dot) { dot.textContent = '⬤ 在線'; dot.className = 'online-dot online'; }
  }

  function envClass(env) {
    if (!env) return 'unknown';
    const l = env.toLowerCase();
    if (l === 'production') return 'production';
    if (l === 'staging' || l === 'uat') return 'uat';
    if (l === 'development') return 'development';
    return 'unknown';
  }

  function createServerBlock(hostId, shortId, env) {
    const list = document.getElementById('server-list');
    const div = document.createElement('div');
    div.className = 'server-block';
    div.id = `sb-${shortId}`;
    div.innerHTML = `
      <div class="server-head">
        <span class="server-name">${shortId}</span>
        <span class="env-badge env-${envClass(env)}">${env || 'unknown'}</span>
        <span class="online-dot online">⬤ 在線</span>
      </div>
      <div class="sm-row">
        <div class="sm-cell"><div class="sm-label">CPU</div><div class="sm-val" id="${shortId}-cpu">—</div></div>
        <div class="sm-cell"><div class="sm-label">記憶體</div><div class="sm-val" id="${shortId}-mem">—</div></div>
        <div class="sm-cell"><div class="sm-label">GC</div><div class="sm-val" id="${shortId}-gc">—</div></div>
        <div class="sm-cell"><div class="sm-label">Uptime</div><div class="sm-val" id="${shortId}-uptime">—</div></div>
        <div class="sm-cell"><div class="sm-label">執行緒</div><div class="sm-val" id="${shortId}-threads">—</div></div>
      </div>`;
    list.appendChild(div);
  }

  function updateServerBlock(hostId, data) {
    const shortId = hostId;
    const set = (suffix, val) => {
      const el = document.getElementById(`${shortId}-${suffix}`);
      if (el) el.textContent = val;
    };
    set('cpu', `${data.cpuPercent}%`);
    set('mem', `${data.memoryMb} MB`);
    set('gc', `${data.gcMemoryMb} MB`);
    set('uptime', data.uptime ?? '—');
    set('threads', data.threadCount ?? '—');
  }

  // ── Offline detection ──────────────────────────────────────────
  function removeServerBlock(hostId) {
    const block = document.getElementById(`sb-${hostId}`);
    if (block) block.remove();
    serverState.delete(hostId);
    serverCpuData.delete(hostId);
    if (metricsChart) {
      const idx = metricsChart.data.datasets.findIndex((d) => d.label === hostId);
      if (idx !== -1) metricsChart.data.datasets.splice(idx, 1);
      metricsChart.update('none');
    }
  }

  function clearAllServerBlocks() {
    [...serverState.keys()].forEach(removeServerBlock);
    timeLabels.length = 0;
    lastLabelMs = 0;
    if (metricsChart) metricsChart.update('none');
  }

  function startOfflineCheck() {
    if (offlineTimer) return;
    offlineTimer = setInterval(() => {
      const now = Date.now();
      const toRemove = [];
      serverState.forEach((state, hostId) => {
        const elapsed = !state.lastSeen ? Infinity : now - state.lastSeen;
        if (elapsed > REMOVE_MS) { toRemove.push(hostId); return; }
        const offline = elapsed > OFFLINE_MS;
        const shortId = hostId;
        const block = document.getElementById(`sb-${shortId}`);
        if (!block) return;
        block.classList.toggle('offline', offline);
        const dot = block.querySelector('.online-dot');
        if (dot) {
          dot.textContent = offline ? '⬤ 離線' : '⬤ 在線';
          dot.className = `online-dot ${offline ? 'offline' : 'online'}`;
        }
      });
      toRemove.forEach(removeServerBlock);
    }, 1000);
  }

  function stopOfflineCheck() {
    if (offlineTimer) { clearInterval(offlineTimer); offlineTimer = null; }
  }

  // ── Chart（CPU 多線趨勢） ──────────────────────────────────────
  function ensureChart() {
    if (metricsChart) return;
    const ctx = document.getElementById('metrics-chart').getContext('2d');
    metricsChart = new Chart(ctx, {
      type: 'line',
      data: { labels: timeLabels, datasets: [] },
      options: {
        animation: false,
        responsive: true,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'top' } },
        scales: {
          x: { ticks: { maxTicksLimit: 6, maxRotation: 0 } },
          y: {
            type: 'linear',
            min: 0,
            max: 100,
            title: { display: true, text: 'CPU %' },
            ticks: { stepSize: 20 },
          },
        },
      },
    });
  }

  function addChartDataset(hostId, colorIdx) {
    if (!metricsChart) return;
    const shortId = hostId;
    if (metricsChart.data.datasets.find((d) => d.label === shortId)) return;
    const color = CPU_COLORS[colorIdx % CPU_COLORS.length];
    const data = new Array(timeLabels.length).fill(null);
    serverCpuData.set(hostId, data);
    metricsChart.data.datasets.push({
      label: shortId,
      data,
      borderColor: color,
      backgroundColor: color + '18',
      tension: 0.3,
      pointRadius: 0,
      borderWidth: 2,
    });
    metricsChart.update('none');
  }

  function pushCpuPoint(hostId, cpuPercent) {
    if (!metricsChart) return;

    const now = Date.now();
    // 每 800ms 推一個時間刻度
    if (now - lastLabelMs >= 800) {
      lastLabelMs = now;
      const label = new Date().toLocaleTimeString('zh-TW', { hour12: false });
      timeLabels.push(label);
      if (timeLabels.length > MAX_HISTORY) timeLabels.shift();

      // 所有 dataset 補 null 對齊新刻度
      metricsChart.data.datasets.forEach((ds) => {
        while (ds.data.length < timeLabels.length) ds.data.push(null);
        if (ds.data.length > MAX_HISTORY) ds.data.shift();
      });
    }

    // 更新此 server 的最後一個資料點
    const ds = metricsChart.data.datasets.find((d) => d.label === hostId);
    if (ds && timeLabels.length > 0) {
      while (ds.data.length < timeLabels.length) ds.data.push(null);
      ds.data[timeLabels.length - 1] = cpuPercent;
    }

    metricsChart.update('none');
  }

  // ── Login / Logout ─────────────────────────────────────────────
  loginForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    loginError.textContent = '';
    const username = document.getElementById('login-username').value;
    const password = document.getElementById('login-password').value;
    const credentials = btoa(`${username}:${password}`);
    if (await checkAuth(credentials)) {
      setStoredCredentials(credentials);
      showForm();
    } else {
      loginError.textContent = '帳號或密碼錯誤';
    }
  });

  logoutBtn.addEventListener('click', () => {
    clearStoredCredentials();
    resultEl.textContent = '';
    updateForm.reset();
    showLogin();
  });

  // ── Clear cache ────────────────────────────────────────────────
  clearCacheBtn.addEventListener('click', async () => {
    const credentials = getStoredCredentials();
    if (!credentials) { showLogin(); return; }

    resultEl.textContent = '清除快取中...';
    try {
      const response = await fetch(CACHE_CLEAR_URL, {
        method: 'POST',
        headers: { Authorization: `Basic ${credentials}` },
      });
      if (response.status === 401) { resultEl.textContent = '❌ 認證失敗（401），請重新登入'; return; }
      if (!response.ok) { resultEl.textContent = `❌ 清除快取失敗（HTTP ${response.status}）`; return; }
      const data = await response.json();
      resultEl.textContent =
        `✅ 快取清除完成：L1（記憶體）${data.l1Cleared ? '成功' : '失敗'}，` +
        `L2（Garnet）${data.l2Cleared ? '成功' : '失敗'}`;
    } catch (error) {
      resultEl.textContent = `❌ 清除快取發生錯誤：${error.message}`;
    }
  });

  // ── Coordinate update ──────────────────────────────────────────
  function parseLines(raw) {
    return raw.split('\n').map((l) => l.trim()).filter((l) => l.length > 0);
  }

  async function submitOne(credentials, rawLocation, latitude, longitude) {
    const response = await fetch(PATCH_URL, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json', Authorization: `Basic ${credentials}` },
      body: JSON.stringify({ rawLocation, latitude, longitude }),
    });
    if (response.status === 200) { const d = await response.json(); return `✅ ${rawLocation} → 成功，受影響 ${d.affected} 筆`; }
    if (response.status === 404) return `⚠️ ${rawLocation} → 找不到符合的地點（404）`;
    if (response.status === 400) { const m = await response.text(); return `❌ ${rawLocation} → 驗證失敗（400）：${m}`; }
    if (response.status === 401) return `❌ ${rawLocation} → 認證失敗（401），請重新登入`;
    return `❌ ${rawLocation} → 發生錯誤（HTTP ${response.status}）`;
  }

  updateForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    const credentials = getStoredCredentials();
    if (!credentials) { showLogin(); return; }

    const rawLocationInput = document.getElementById('rawLocation').value;
    const lines = parseLines(rawLocationInput);
    if (lines.length === 0) { resultEl.textContent = '請輸入發生地點'; return; }

    const isBatch = lines.length > 1 || lines[0].includes(',');
    const messages = [];
    resultEl.textContent = '處理中...';

    if (isBatch) {
      for (const line of lines) {
        const parts = line.split(',').map((p) => p.trim());
        if (parts.length !== 3) { messages.push(`❌ ${line} → 格式錯誤，需為「地點,緯度,經度」`); continue; }
        const [rawLocation, latStr, lngStr] = parts;
        const latitude = Number(latStr);
        const longitude = Number(lngStr);
        if (!rawLocation || Number.isNaN(latitude) || Number.isNaN(longitude)) {
          messages.push(`❌ ${line} → 格式錯誤，需為「地點,緯度,經度」`); continue;
        }
        messages.push(await submitOne(credentials, rawLocation, latitude, longitude));
      }
    } else {
      const rawLocation = lines[0];
      const latitude = Number(document.getElementById('latitude').value);
      const longitude = Number(document.getElementById('longitude').value);
      if (Number.isNaN(latitude) || Number.isNaN(longitude)) { resultEl.textContent = '請輸入有效的緯度與經度'; return; }
      messages.push(await submitOne(credentials, rawLocation, latitude, longitude));
    }

    resultEl.textContent = messages.join('\n');
  });

  // ── Data edit (資料更新) ────────────────────────────────────────
  const DE_API = '/api/crime';
  const DE_PATCH_API = '/api/admin/cases';
  const DE_PAGE_SIZE = 50;
  const CASE_TYPE_NAMES = { 1: '住宅竊盜', 2: '汽車竊盜', 3: '機車竊盜', 4: '自行車竊盜', 5: '搶奪', 6: '強盜' };
  const CASE_TYPE_REV = Object.fromEntries(Object.entries(CASE_TYPE_NAMES).map(([k, v]) => [v, k]));
  let _dePage = 1;
  let _deTotalPages = 0;

  const deCaseType = document.getElementById('de-case-type');
  const deDistrict = document.getElementById('de-district');
  const deSortBy = document.getElementById('de-sort-by');
  const deSortOrder = document.getElementById('de-sort-order');
  const deTbody = document.getElementById('de-tbody');
  const dePageInfo = document.getElementById('de-page-info');
  const deStatus = document.getElementById('de-status');
  const deResult = document.getElementById('de-result');
  const btnDeQuery = document.getElementById('btn-de-query');
  const btnDePrev = document.getElementById('btn-de-prev');
  const btnDeNext = document.getElementById('btn-de-next');

  async function loadDataEditPage() {
    const credentials = getStoredCredentials();
    if (!credentials) { showLogin(); return; }
    deStatus.textContent = '查詢中...';
    deResult.textContent = '';
    deTbody.innerHTML = '';

    const params = new URLSearchParams({ page: _dePage, pageSize: DE_PAGE_SIZE, sortBy: deSortBy.value, sortOrder: deSortOrder.value });
    if (deCaseType.value) params.set('caseType', deCaseType.value);
    if (deDistrict.value) params.set('districtName', deDistrict.value);

    try {
      const resp = await fetch(`${DE_API}?${params}`, { headers: { Authorization: `Basic ${credentials}` } });
      if (!resp.ok) { deStatus.textContent = `查詢失敗 (HTTP ${resp.status})`; return; }
      const data = await resp.json();
      _deTotalPages = data.totalPages || 0;
      renderDeTable(data.data || []);
      dePageInfo.textContent = `第 ${_dePage} / ${_deTotalPages} 頁（共 ${data.total} 筆）`;
      btnDePrev.disabled = _dePage <= 1;
      btnDeNext.disabled = _dePage >= _deTotalPages;
      deStatus.textContent = '';
    } catch (err) {
      deStatus.textContent = `錯誤：${err.message}`;
    }
  }

  function renderDeTable(rows) {
    const s = 'padding:4px 6px;border:1px solid #e5e7eb';
    deTbody.innerHTML = rows.map((r) => {
      const coord = (r.latitude != null && r.longitude != null)
        ? `${r.latitude.toFixed(4)},${r.longitude.toFixed(4)}` : '—';
      const ctInt = CASE_TYPE_REV[r.caseType] || '';
      return `<tr data-cn="${r.caseNumber}" data-ct="${ctInt}">
        <td style="${s}">${r.caseNumber}</td>
        <td style="${s}">${r.caseType || '—'}</td>
        <td style="${s}" class="de-date">${r.occurredDate || '—'}</td>
        <td style="${s}" class="de-ts">${r.timeSlot || '—'}</td>
        <td style="${s}" class="de-loc">${r.rawLocation || '—'}</td>
        <td style="${s}">${coord}</td>
        <td style="${s}"><button class="btn-de-edit" style="padding:2px 8px;font-size:0.8rem">編輯</button></td>
      </tr>`;
    }).join('');

    deTbody.querySelectorAll('.btn-de-edit').forEach((btn) => {
      btn.addEventListener('click', () => startInlineEdit(btn.closest('tr')));
    });
  }

  function startInlineEdit(tr) {
    if (tr.querySelector('input')) return;
    const dateCell = tr.querySelector('.de-date');
    const tsCell = tr.querySelector('.de-ts');
    const locCell = tr.querySelector('.de-loc');
    const btnCell = tr.querySelector('.btn-de-edit').parentElement;
    const origDate = dateCell.textContent.trim();
    const origTs = tsCell.textContent.trim();
    const origLoc = locCell.textContent.trim();

    dateCell.innerHTML = `<input type="text" value="${origDate === '—' ? '' : origDate}" style="width:80px;font-size:0.8rem">`;
    tsCell.innerHTML = `<input type="text" value="${origTs === '—' ? '' : origTs}" style="width:50px;font-size:0.8rem">`;
    locCell.innerHTML = `<input type="text" value="${origLoc === '—' ? '' : origLoc}" style="width:140px;font-size:0.8rem">`;
    btnCell.innerHTML = '<button class="btn-de-save" style="padding:2px 6px;font-size:0.75rem;background:#16a34a;color:#fff;border:none;border-radius:3px;cursor:pointer">儲存</button> <button class="btn-de-cancel" style="padding:2px 6px;font-size:0.75rem;background:#6b7280;color:#fff;border:none;border-radius:3px;cursor:pointer">取消</button>';

    btnCell.querySelector('.btn-de-cancel').addEventListener('click', () => {
      dateCell.textContent = origDate;
      tsCell.textContent = origTs;
      locCell.textContent = origLoc;
      btnCell.innerHTML = '<button class="btn-de-edit" style="padding:2px 8px;font-size:0.8rem">編輯</button>';
      btnCell.querySelector('.btn-de-edit').addEventListener('click', () => startInlineEdit(tr));
    });

    btnCell.querySelector('.btn-de-save').addEventListener('click', async () => {
      const credentials = getStoredCredentials();
      if (!credentials) { showLogin(); return; }
      const caseNumber = tr.dataset.cn;
      const caseType = tr.dataset.ct;
      const newDate = dateCell.querySelector('input').value.trim();
      const newTs = tsCell.querySelector('input').value.trim();
      const newLoc = locCell.querySelector('input').value.trim();
      const msgs = [];

      if (newDate !== origDate.replace('—', '') || newTs !== origTs.replace('—', '')) {
        const body = {};
        if (newDate) {
          const m = newDate.match(/^(\d{4})-(\d{2})-(\d{2})$/);
          body.occurrenceDate = m ? (parseInt(m[1], 10) - 1911) * 10000 + parseInt(m[2], 10) * 100 + parseInt(m[3], 10) : parseInt(newDate, 10);
        }
        if (newTs) body.timeSlot = newTs;
        if (Object.keys(body).length > 0) {
          try {
            const r = await fetch(`${DE_PATCH_API}/${caseNumber}/${caseType}`, {
              method: 'PATCH',
              headers: { 'Content-Type': 'application/json', Authorization: `Basic ${credentials}` },
              body: JSON.stringify(body),
            });
            msgs.push(r.ok ? '日期/時段已更新' : `日期/時段更新失敗 (${r.status})`);
          } catch (e) { msgs.push(`日期/時段錯誤：${e.message}`); }
        }
      }

      if (newLoc && newLoc !== origLoc.replace('—', '')) {
        try {
          const r = await fetch(PATCH_URL, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json', Authorization: `Basic ${credentials}` },
            body: JSON.stringify({ rawLocation: newLoc, latitude: 0, longitude: 0 }),
          });
          msgs.push(r.ok ? '地點已更新' : `地點更新失敗 (${r.status})`);
        } catch (e) { msgs.push(`地點錯誤：${e.message}`); }
      }

      deResult.textContent = msgs.length > 0 ? msgs.join('; ') : '無變更';
      loadDataEditPage();
    });
  }

  btnDeQuery.addEventListener('click', () => { _dePage = 1; loadDataEditPage(); });
  document.getElementById('btn-de-clear-cache').addEventListener('click', async () => {
    const credentials = getStoredCredentials();
    if (!credentials) { showLogin(); return; }
    deResult.textContent = '清除快取中...';
    try {
      const r = await fetch(CACHE_CLEAR_URL, { method: 'POST', headers: { Authorization: `Basic ${credentials}` } });
      if (!r.ok) { deResult.textContent = `清除快取失敗 (HTTP ${r.status})`; return; }
      const d = await r.json();
      deResult.textContent = `快取清除完成：L1 ${d.l1Cleared ? '✅' : '❌'}，L2 ${d.l2Cleared ? '✅' : '❌'}`;
    } catch (e) { deResult.textContent = `錯誤：${e.message}`; }
  });
  btnDePrev.addEventListener('click', () => { if (_dePage > 1) { _dePage--; loadDataEditPage(); } });
  btnDeNext.addEventListener('click', () => { if (_dePage < _deTotalPages) { _dePage++; loadDataEditPage(); } });

  const deGotoInput = document.getElementById('de-goto-page');
  function gotoPage() {
    const p = parseInt(deGotoInput.value, 10);
    if (p >= 1 && p <= _deTotalPages) { _dePage = p; loadDataEditPage(); }
  }
  document.getElementById('btn-de-goto').addEventListener('click', gotoPage);
  deGotoInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') gotoPage(); });

  // ── Bulk add cases ─────────────────────────────────────────────
  const BULK_API_URL = '/api/admin/cases/bulk';
  const bulkInput = document.getElementById('bulk-input');
  const bulkPreviewBtn = document.getElementById('btn-bulk-preview');
  const bulkPreviewArea = document.getElementById('bulk-preview-area');
  const bulkPreviewTbody = document.querySelector('#bulk-preview-table tbody');
  const bulkSubmitBtn = document.getElementById('btn-bulk-submit');
  const bulkResultEl = document.getElementById('bulk-result');

  let _parsedBulkItems = [];

  function parseBulkInput(text) {
    return text.split('\n')
      .map(l => l.trim())
      .filter(l => l.length > 0)
      .map(line => {
        const parts = line.split('\t');
        if (parts.length < 5) return null;
        return {
          caseNumber: parseInt(parts[0], 10),
          caseType: parts[1].trim(),
          occurrenceDate: parseInt(parts[2], 10),
          timeSlot: parts[3].trim(),
          rawLocation: parts[4].trim(),
        };
      });
  }

  bulkPreviewBtn.addEventListener('click', () => {
    bulkResultEl.textContent = '';
    const parsed = parseBulkInput(bulkInput.value);
    _parsedBulkItems = parsed.filter(x => x !== null);

    bulkPreviewTbody.innerHTML = '';
    parsed.forEach((item, i) => {
      const tr = document.createElement('tr');
      if (item === null) {
        tr.innerHTML = `<td colspan="6" style="padding:4px 8px;border:1px solid #e5e7eb;color:#dc2626">第 ${i + 1} 行格式錯誤（欄位不足 5 個）</td>`;
      } else {
        const style = 'padding:4px 8px;border:1px solid #e5e7eb';
        tr.innerHTML =
          `<td style="${style}">${i + 1}</td>` +
          `<td style="${style}">${item.caseNumber}</td>` +
          `<td style="${style}">${item.caseType}</td>` +
          `<td style="${style}">${item.occurrenceDate}</td>` +
          `<td style="${style}">${item.timeSlot}</td>` +
          `<td style="${style}">${item.rawLocation}</td>`;
      }
      bulkPreviewTbody.appendChild(tr);
    });

    bulkPreviewArea.style.display = _parsedBulkItems.length > 0 ? '' : 'none';
  });

  let _batchPollTimer = null;

  function pollBatchStatus(batchId, credentials) {
    if (_batchPollTimer) clearInterval(_batchPollTimer);
    const progressEl = document.getElementById('bulk-progress');
    const barEl = document.getElementById('bulk-progress-bar');
    if (progressEl) progressEl.style.display = '';

    _batchPollTimer = setInterval(async () => {
      try {
        const resp = await fetch(`/api/admin/cases/batch/${batchId}/status`, {
          headers: { Authorization: `Basic ${credentials}` },
        });
        if (!resp.ok) return;
        const s = await resp.json();
        const total = s.pending + s.success + s.failure;
        const pct = total > 0 ? Math.round(((s.success + s.failure) / total) * 100) : 0;
        if (barEl) barEl.style.width = `${pct}%`;

        let msg = `⏳ 進度 ${pct}%：待處理 ${s.pending}　成功 ${s.success}　失敗 ${s.failure}`;
        if (s.failures && s.failures.length > 0) {
          msg += '\n\n失敗明細：';
          s.failures.forEach(f => { msg += `\n  編號 ${f.caseNumber}：${f.error}`; });
        }
        bulkResultEl.textContent = msg;

        if (s.pending === 0) {
          clearInterval(_batchPollTimer);
          _batchPollTimer = null;
          bulkSubmitBtn.disabled = false;
          if (progressEl) progressEl.style.display = 'none';
          const icon = s.failure > 0 ? '⚠️' : '✅';
          let final = `${icon} 完成！成功 ${s.success} 筆，失敗 ${s.failure} 筆`;
          if (s.failures && s.failures.length > 0) {
            final += '\n\n失敗明細：';
            s.failures.forEach(f => { final += `\n  編號 ${f.caseNumber}：${f.error}`; });
          }
          bulkResultEl.textContent = final;
        }
      } catch (err) {
        console.warn('[admin] poll batch status error:', err);
      }
    }, 2000);
  }

  bulkSubmitBtn.addEventListener('click', async () => {
    if (_parsedBulkItems.length === 0) return;
    const credentials = getStoredCredentials();
    if (!credentials) { showLogin(); return; }

    bulkResultEl.textContent = '送出中...';
    bulkSubmitBtn.disabled = true;
    try {
      const resp = await fetch(BULK_API_URL, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Basic ${credentials}`,
        },
        body: JSON.stringify(_parsedBulkItems),
      });
      if (resp.status === 401) { bulkResultEl.textContent = '❌ 認證失敗（401），請重新登入'; bulkSubmitBtn.disabled = false; return; }
      if (!resp.ok) { bulkResultEl.textContent = `❌ 送出失敗（HTTP ${resp.status}）`; bulkSubmitBtn.disabled = false; return; }

      const data = await resp.json();
      bulkResultEl.textContent = `⏳ 已排入佇列：批次 ${data.batchId}，共 ${data.totalCount} 筆，處理中...`;
      pollBatchStatus(data.batchId, credentials);
    } catch (err) {
      bulkResultEl.textContent = `❌ 發生錯誤：${err.message}`;
      bulkSubmitBtn.disabled = false;
    }
  });

  window.addEventListener('beforeunload', () => { closeWebSocket(); stopOfflineCheck(); });

  init();
})();

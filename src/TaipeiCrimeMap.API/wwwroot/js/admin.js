'use strict';

(function () {
  const PING_URL = '/api/crime/coordinate/ping';
  const PATCH_URL = '/api/crime/coordinate';
  const CACHE_CLEAR_URL = '/api/admin/cache/clear';
  const STORAGE_KEY = 'adminAuthCredentials';
  const MAX_HISTORY = 60;

  // DOM refs
  const loginSection = document.getElementById('login-section');
  const formSection = document.getElementById('form-section');
  const loginForm = document.getElementById('login-form');
  const loginError = document.getElementById('login-error');
  const updateForm = document.getElementById('update-form');
  const resultEl = document.getElementById('result');
  const logoutBtn = document.getElementById('btn-logout');
  const clearCacheBtn = document.getElementById('btn-clear-cache');

  // Metric card refs
  const mcCpu = document.getElementById('mc-cpu');
  const mcMem = document.getElementById('mc-mem');
  const mcGc = document.getElementById('mc-gc');
  const mcUptime = document.getElementById('mc-uptime');
  const mcThreads = document.getElementById('mc-threads');
  const mcConns = document.getElementById('mc-conns');
  const wsStatusEl = document.getElementById('ws-status');

  // DOM refs for static hw info
  const hwInfo = document.getElementById('hw-info');
  const hwCores = document.getElementById('hw-cores');
  const hwTotalMem = document.getElementById('hw-total-mem');
  const hwOs = document.getElementById('hw-os');
  const hwDotnet = document.getElementById('hw-dotnet');

  // WebSocket state
  let ws = null;
  let _hwInfoShown = false;

  // Chart.js instance
  let metricsChart = null;
  const cpuHistory = [];
  const memHistory = [];
  const timeLabels = [];

  function getStoredCredentials() {
    return sessionStorage.getItem(STORAGE_KEY);
  }

  function setStoredCredentials(value) {
    sessionStorage.setItem(STORAGE_KEY, value);
  }

  function clearStoredCredentials() {
    sessionStorage.removeItem(STORAGE_KEY);
  }

  async function checkAuth(credentials) {
    const response = await fetch(PING_URL, {
      headers: { Authorization: `Basic ${credentials}` },
    });
    return response.ok;
  }

  function showLogin() {
    loginSection.hidden = false;
    formSection.hidden = true;
    closeWebSocket();
  }

  function showForm() {
    loginSection.hidden = true;
    formSection.hidden = false;
  }

  async function init() {
    const stored = getStoredCredentials();
    if (stored && (await checkAuth(stored))) {
      showForm();
    } else {
      clearStoredCredentials();
      showLogin();
    }
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
      } else {
        closeWebSocket();
      }
    });
  });

  // ── WebSocket ──────────────────────────────────────────────────

  function openWebSocket() {
    if (ws && ws.readyState <= WebSocket.OPEN) return;

    const credentials = getStoredCredentials();
    if (!credentials) return;

    const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${proto}//${location.host}/ws/metrics?token=${encodeURIComponent(credentials)}`;

    setWsStatus('connecting');
    ws = new WebSocket(url);

    ws.addEventListener('open', () => setWsStatus('connected'));

    ws.addEventListener('message', (event) => {
      try {
        const data = JSON.parse(event.data);
        if (!_hwInfoShown) {
          populateHwInfo(data);
          _hwInfoShown = true;
        }
        updateCards(data);
        pushChartPoint(data);
      } catch (_) { /* ignore malformed frame */ }
    });

    ws.addEventListener('close', () => setWsStatus('disconnected'));
    ws.addEventListener('error', () => setWsStatus('error'));
  }

  function closeWebSocket() {
    if (ws) {
      ws.close();
      ws = null;
    }
    _hwInfoShown = false;
    setWsStatus('disconnected');
  }

  function setWsStatus(state) {
    if (!wsStatusEl) return;
    wsStatusEl.className = 'ws-status';
    if (state === 'connected') {
      wsStatusEl.textContent = '⬤ 已連線';
      wsStatusEl.classList.add('connected');
    } else if (state === 'connecting') {
      wsStatusEl.textContent = '⬤ 連線中…';
    } else if (state === 'error') {
      wsStatusEl.textContent = '⬤ 連線錯誤';
      wsStatusEl.classList.add('error');
    } else {
      wsStatusEl.textContent = '⬤ 未連線';
    }
  }

  // ── Static hardware info (shown once) ─────────────────────────

  function populateHwInfo(data) {
    hwCores.textContent = data.cpuCores;
    hwTotalMem.textContent = `${data.totalMemoryMb} MB`;
    hwOs.textContent = data.osDescription;
    hwDotnet.textContent = data.dotNetVersion;
    hwInfo.style.display = '';
  }

  // ── Metric cards ───────────────────────────────────────────────

  function updateCards(data) {
    mcCpu.textContent = `${data.cpuPercent}%`;
    mcMem.textContent = `${data.memoryMb} MB`;
    mcGc.textContent = `${data.gcMemoryMb} MB`;
    mcUptime.textContent = data.uptime;
    mcThreads.textContent = data.threadCount;
    mcConns.textContent = data.connectionCount;
  }

  // ── Chart ──────────────────────────────────────────────────────

  function ensureChart() {
    if (metricsChart) return;

    const ctx = document.getElementById('metrics-chart').getContext('2d');
    metricsChart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: timeLabels,
        datasets: [
          {
            label: 'CPU %',
            data: cpuHistory,
            borderColor: '#ef4444',
            backgroundColor: 'rgba(239,68,68,0.08)',
            yAxisID: 'yCpu',
            tension: 0.3,
            pointRadius: 0,
            borderWidth: 2,
          },
          {
            label: '記憶體 MB',
            data: memHistory,
            borderColor: '#3b82f6',
            backgroundColor: 'rgba(59,130,246,0.08)',
            yAxisID: 'yMem',
            tension: 0.3,
            pointRadius: 0,
            borderWidth: 2,
          },
        ],
      },
      options: {
        animation: false,
        responsive: true,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'top' } },
        scales: {
          x: {
            ticks: { maxTicksLimit: 6, maxRotation: 0 },
          },
          yCpu: {
            type: 'linear',
            position: 'left',
            min: 0,
            max: 100,
            title: { display: true, text: 'CPU %' },
            ticks: { stepSize: 20 },
          },
          yMem: {
            type: 'linear',
            position: 'right',
            min: 0,
            title: { display: true, text: 'MB' },
            grid: { drawOnChartArea: false },
          },
        },
      },
    });
  }

  function pushChartPoint(data) {
    if (!metricsChart) return;

    const now = new Date().toLocaleTimeString('zh-TW', { hour12: false });
    timeLabels.push(now);
    cpuHistory.push(data.cpuPercent);
    memHistory.push(data.memoryMb);

    if (timeLabels.length > MAX_HISTORY) {
      timeLabels.shift();
      cpuHistory.shift();
      memHistory.shift();
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
    if (!credentials) {
      showLogin();
      return;
    }

    resultEl.textContent = '清除快取中...';

    try {
      const response = await fetch(CACHE_CLEAR_URL, {
        method: 'POST',
        headers: { Authorization: `Basic ${credentials}` },
      });

      if (response.status === 401) {
        resultEl.textContent = '❌ 認證失敗（401），請重新登入';
        return;
      }

      if (!response.ok) {
        resultEl.textContent = `❌ 清除快取失敗（HTTP ${response.status}）`;
        return;
      }

      const data = await response.json();
      resultEl.textContent =
        `✅ 快取清除完成：L1（記憶體）${data.l1Cleared ? '成功' : '失敗'}，` +
        `L2（Garnet）${data.l2Cleared ? '成功' : '失敗'}`;
    } catch (error) {
      resultEl.textContent = `❌ 清除快取發生錯誤：${error.message}`;
    }
  });

  // ── Coordinate update form ─────────────────────────────────────

  function parseLines(raw) {
    return raw
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);
  }

  async function submitOne(credentials, rawLocation, latitude, longitude) {
    const response = await fetch(PATCH_URL, {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Basic ${credentials}`,
      },
      body: JSON.stringify({ rawLocation, latitude, longitude }),
    });

    if (response.status === 200) {
      const data = await response.json();
      return `✅ ${rawLocation} → 成功，受影響 ${data.affected} 筆`;
    }
    if (response.status === 404) {
      return `⚠️ ${rawLocation} → 找不到符合的地點（404）`;
    }
    if (response.status === 400) {
      const message = await response.text();
      return `❌ ${rawLocation} → 驗證失敗（400）：${message}`;
    }
    if (response.status === 401) {
      return `❌ ${rawLocation} → 認證失敗（401），請重新登入`;
    }
    return `❌ ${rawLocation} → 發生錯誤（HTTP ${response.status}）`;
  }

  updateForm.addEventListener('submit', async (event) => {
    event.preventDefault();

    const credentials = getStoredCredentials();
    if (!credentials) {
      showLogin();
      return;
    }

    const rawLocationInput = document.getElementById('rawLocation').value;
    const lines = parseLines(rawLocationInput);

    if (lines.length === 0) {
      resultEl.textContent = '請輸入發生地點';
      return;
    }

    const isBatch = lines.length > 1 || lines[0].includes(',');
    const messages = [];
    resultEl.textContent = '處理中...';

    if (isBatch) {
      for (const line of lines) {
        const parts = line.split(',').map((part) => part.trim());

        if (parts.length !== 3) {
          messages.push(`❌ ${line} → 格式錯誤，需為「地點,緯度,經度」`);
          continue;
        }

        const [rawLocation, latStr, lngStr] = parts;
        const latitude = Number(latStr);
        const longitude = Number(lngStr);

        if (!rawLocation || Number.isNaN(latitude) || Number.isNaN(longitude)) {
          messages.push(`❌ ${line} → 格式錯誤，需為「地點,緯度,經度」`);
          continue;
        }

        messages.push(await submitOne(credentials, rawLocation, latitude, longitude));
      }
    } else {
      const rawLocation = lines[0];
      const latitude = Number(document.getElementById('latitude').value);
      const longitude = Number(document.getElementById('longitude').value);

      if (Number.isNaN(latitude) || Number.isNaN(longitude)) {
        resultEl.textContent = '請輸入有效的緯度與經度';
        return;
      }

      messages.push(await submitOne(credentials, rawLocation, latitude, longitude));
    }

    resultEl.textContent = messages.join('\n');
  });

  // Close WebSocket when page unloads
  window.addEventListener('beforeunload', closeWebSocket);

  init();
})();

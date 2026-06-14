'use strict';

(function () {
  const PING_URL = '/api/crime/coordinate/ping';
  const PATCH_URL = '/api/crime/coordinate';
  const CACHE_CLEAR_URL = '/api/admin/cache/clear';
  const STORAGE_KEY = 'adminAuthCredentials';

  const loginSection = document.getElementById('login-section');
  const formSection = document.getElementById('form-section');
  const loginForm = document.getElementById('login-form');
  const loginError = document.getElementById('login-error');
  const updateForm = document.getElementById('update-form');
  const resultEl = document.getElementById('result');
  const logoutBtn = document.getElementById('btn-logout');
  const clearCacheBtn = document.getElementById('btn-clear-cache');

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

  init();
})();

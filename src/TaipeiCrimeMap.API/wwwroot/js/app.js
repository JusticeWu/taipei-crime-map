/**
 * app.js — Taipei Crime Map main application logic
 *
 * Responsibilities:
 *  1. Read filter values from the UI
 *  2. Heat mode: GET /api/crime/heatmap only (12 district points, instant)
 *  3. Point mode: parallel GET /api/crime?page=N&pageSize=200 + heatmap in background
 *  4. Show loading progress (top-left of map)
 *  5. Update stats panel and charts after load
 *  6. Smart mode toggle: switches display without re-fetching when data is cached
 */

(function () {
  'use strict';

  const API_BASE        = '/api/crime';
  const API_POINTS      = '/api/crime/points'; // slim DTO endpoint for point mode
  const PAGE_SIZE       = 500;                 // 11,514 records → 24 requests (was 58)

  /* -----------------------------------------------------------------------
     DOM references
  ----------------------------------------------------------------------- */
  let elCaseType, elDistrict, elYearFrom, elYearTo;
  let elBtnQuery, elToggleMode;
  let elStatTotal, elStatWithCoords, elStatTopDistrict;
  let elLoadingOverlay;

  /* -----------------------------------------------------------------------
     State
  ----------------------------------------------------------------------- */
  let _lastData        = [];   // paged individual records (point mode)
  let _lastHeatmapData = null; // cached /api/crime/heatmap response
  let _queryGeneration = 0;    // incremented on each new query to abort stale loads

  /* -----------------------------------------------------------------------
     Loading overlay
  ----------------------------------------------------------------------- */
  function setLoading(visible) {
    if (elLoadingOverlay) elLoadingOverlay.classList.toggle('hidden', !visible);
  }

  /* -----------------------------------------------------------------------
     Toggle mode enable / disable
  ----------------------------------------------------------------------- */
  function setToggleDisabled(disabled) {
    if (!elToggleMode) return;
    elToggleMode.querySelectorAll('input[type="radio"]').forEach(r => { r.disabled = disabled; });
    elToggleMode.style.opacity       = disabled ? '0.45' : '';
    elToggleMode.style.pointerEvents = disabled ? 'none'  : '';
  }

  /* -----------------------------------------------------------------------
     Display mode
  ----------------------------------------------------------------------- */
  function getDisplayMode() {
    if (!elToggleMode) return 'heat';
    const checked = elToggleMode.querySelector('input[type="radio"]:checked');
    return checked ? checked.value : 'heat';
  }

  /* -----------------------------------------------------------------------
     Query params
  ----------------------------------------------------------------------- */
  function buildQueryParams() {
    const params = new URLSearchParams();
    const caseType = elCaseType ? elCaseType.value.trim() : '';
    const district = elDistrict ? elDistrict.value.trim() : '';
    const yearFrom = elYearFrom ? elYearFrom.value.trim() : '';
    const yearTo   = elYearTo   ? elYearTo.value.trim()   : '';

    if (caseType) params.set('caseType',     caseType);
    if (district) params.set('districtName', district);
    if (yearFrom) params.set('yearFrom',     yearFrom);
    if (yearTo)   params.set('yearTo',       yearTo);

    return params;
  }

  /* -----------------------------------------------------------------------
     Stats
  ----------------------------------------------------------------------- */
  function computeStats(data) {
    if (!Array.isArray(data) || data.length === 0)
      return { total: 0, withCoords: 0, topDistrict: '—' };

    let withCoords = 0;
    const districtCount = {};

    for (const item of data) {
      if (item.latitude != null && item.longitude != null) withCoords++;
      const d = item.districtName || item.district || '';
      if (d) districtCount[d] = (districtCount[d] || 0) + 1;
    }

    let topDistrict = '—', maxCount = 0;
    for (const [name, count] of Object.entries(districtCount)) {
      if (count > maxCount) { maxCount = count; topDistrict = name; }
    }

    return { total: data.length, withCoords, topDistrict };
  }

  function computeStatsFromHeatmap(data) {
    if (!Array.isArray(data) || data.length === 0)
      return { total: 0, withCoords: 0, topDistrict: '—' };

    const total = data.reduce((s, p) => s + (p.weight || 0), 0);
    const top   = data.reduce((a, b) => (a.weight || 0) >= (b.weight || 0) ? a : b, data[0]);
    return { total, withCoords: 0, topDistrict: top.district || '—' };
  }

  function renderStats(stats) {
    if (elStatTotal)       elStatTotal.textContent       = stats.total.toLocaleString();
    if (elStatWithCoords)  elStatWithCoords.textContent  = stats.withCoords.toLocaleString();
    if (elStatTopDistrict) elStatTopDistrict.textContent = stats.topDistrict;
  }

  /* -----------------------------------------------------------------------
     Heat mode query — calls /api/crime/heatmap only (12 district points)
  ----------------------------------------------------------------------- */
  async function queryHeatmapOnly() {
    const generation = ++_queryGeneration;

    setLoading(true);
    if (elBtnQuery) elBtnQuery.disabled = true;
    setToggleDisabled(true);

    _lastData        = [];
    _lastHeatmapData = null;

    if (window.mapModule && typeof window.mapModule.startProgressiveLoad === 'function') {
      window.mapModule.startProgressiveLoad('heat');
    }

    try {
      const resp = await fetch(`${API_BASE}/heatmap?${buildQueryParams()}`,
        { headers: { Accept: 'application/json' } });
      if (!resp.ok) throw new Error(`API ${resp.status}`);

      const data = await resp.json();
      if (generation !== _queryGeneration) return;

      _lastHeatmapData = data;

      if (window.mapModule && typeof window.mapModule.setHeatmap === 'function') {
        window.mapModule.setHeatmap(data);
      }
      if (window.mapModule && typeof window.mapModule.clearProgress === 'function') {
        window.mapModule.clearProgress();
      }

      renderStats(computeStatsFromHeatmap(data));

    } catch (err) {
      console.error('Heatmap query failed:', err);
      renderStats({ total: 0, withCoords: 0, topDistrict: '查詢失敗' });
    } finally {
      if (generation === _queryGeneration) {
        setLoading(false);
        if (elBtnQuery) elBtnQuery.disabled = false;
        setToggleDisabled(false);
      }
    }
  }

  /* -----------------------------------------------------------------------
     sessionStorage cache for point-mode data
     key: crimes:{caseType}:{districtName}:{yearFrom}:{yearTo}
  ----------------------------------------------------------------------- */
  const CACHE_PREFIX = 'crimes:points:v2:'; // v2: PointCrimeDto 加入 district/timeSlot/rawLocation

  function buildCacheKey() {
    const p = buildQueryParams();
    return CACHE_PREFIX +
      (p.get('caseType')     || '') + ':' +
      (p.get('districtName') || '') + ':' +
      (p.get('yearFrom')     || '') + ':' +
      (p.get('yearTo')       || '');
  }

  function readFromCache(key) {
    try {
      const raw = sessionStorage.getItem(key);
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  }

  function writeToCache(key, data) {
    try {
      // Remove other crime cache entries first to stay within ~5 MB limit
      for (let i = sessionStorage.length - 1; i >= 0; i--) {
        const k = sessionStorage.key(i);
        if (k && k.startsWith(CACHE_PREFIX) && k !== key) {
          sessionStorage.removeItem(k);
        }
      }
      sessionStorage.setItem(key, JSON.stringify(data));
    } catch {
      // Quota exceeded or serialisation error — ignore, fall through to normal API
    }
  }

  /* -----------------------------------------------------------------------
     Point mode query — progressive paged load of all individual records.
     Also fetches /api/crime/heatmap in background so heat mode switch is instant.
  ----------------------------------------------------------------------- */
  async function queryProgressive() {
    const generation = ++_queryGeneration;

    setLoading(true);
    if (elBtnQuery) elBtnQuery.disabled = true;
    setToggleDisabled(true);

    _lastData        = [];
    _lastHeatmapData = null;

    if (window.mapModule && typeof window.mapModule.startProgressiveLoad === 'function') {
      window.mapModule.startProgressiveLoad('point');
    }

    // Fetch heatmap in background so heat mode switch won't require a new request
    const heatParams = buildQueryParams();
    fetch(`${API_BASE}/heatmap?${heatParams}`, { headers: { Accept: 'application/json' } })
      .then(r => r.ok ? r.json() : null)
      .then(data => { if (data && generation === _queryGeneration) _lastHeatmapData = data; })
      .catch(() => {});

    // ── sessionStorage cache check ────────────────────────────────────────
    const cacheKey = buildCacheKey();
    const cached = readFromCache(cacheKey);
    if (cached && cached.length > 0 && generation === _queryGeneration) {
      console.log(`[點位圖] sessionStorage 命中，跳過 API｜${cached.length} 筆，開始渲染`);
      _lastData = cached;
      // Queue cached data as PAGE_SIZE chunks for progressive rAF rendering
      for (let i = 0; i < cached.length; i += PAGE_SIZE) {
        appendToMap(cached.slice(i, i + PAGE_SIZE), 'point');
      }
      if (window.mapModule) {
        window.mapModule.finalizeLoad(_lastData, 'point');
        window.mapModule.clearProgress();
      }
      renderStats(computeStats(_lastData));
      if (window.chartModule && typeof window.chartModule.update === 'function') {
        window.chartModule.update(_lastData);
      }
      setLoading(false);
      if (elBtnQuery) elBtnQuery.disabled = false;
      setToggleDisabled(false);
      return;
    }

    try {
      const baseParams = buildQueryParams();
      baseParams.set('pageSize', String(PAGE_SIZE));

      // ── Page 1 ────────────────────────────────────────────────────────────
      const t0 = performance.now();
      console.log('[點位圖] 開始發請求');

      baseParams.set('page', '1');
      const resp1 = await fetch(`${API_POINTS}?${baseParams}`, { headers: { Accept: 'application/json' } });
      if (!resp1.ok) throw new Error(`API ${resp1.status}`);

      const first = await resp1.json();
      if (generation !== _queryGeneration) return;

      const { data: firstData, total, totalPages } = first;
      console.log(`[點位圖] Page 1 到達｜totalPages: ${totalPages}，total: ${total}｜T+${(performance.now()-t0).toFixed(0)}ms`);

      _lastData = _lastData.concat(firstData);
      appendToMap(firstData, 'point');
      updateProgress(_lastData.length, total);

      // ── Pages 2..totalPages — fetch all in parallel, render each on arrival
      if (totalPages > 1) {
        const tasks = [];
        for (let page = 2; page <= totalPages; page++) {
          const params = new URLSearchParams(baseParams);
          params.set('page', String(page));
          tasks.push(
            fetch(`${API_POINTS}?${params}`, { headers: { Accept: 'application/json' } })
              .then(r => r.ok ? r.json() : null)
              .then(pageResult => {
                if (!pageResult || generation !== _queryGeneration) return;
                _lastData = _lastData.concat(pageResult.data);
                appendToMap(pageResult.data, 'point');
                updateProgress(_lastData.length, total);
              })
              .catch(err => console.warn('Page fetch failed:', err))
          );
        }
        await Promise.allSettled(tasks);
      }

      if (generation !== _queryGeneration) return;

      const t1 = performance.now();
      console.log(`[點位圖] 所有請求完成｜網路耗時: ${(t1-t0).toFixed(0)} ms｜${_lastData.length} 筆`);

      // ── Save to sessionStorage ─────────────────────────────────────────
      writeToCache(cacheKey, _lastData);

      // ── Finalize ──────────────────────────────────────────────────────────
      if (window.mapModule && typeof window.mapModule.finalizeLoad === 'function') {
        window.mapModule.finalizeLoad(_lastData, 'point');
      }
      if (window.mapModule && typeof window.mapModule.clearProgress === 'function') {
        window.mapModule.clearProgress();
      }

      renderStats(computeStats(_lastData));

      if (window.chartModule && typeof window.chartModule.update === 'function') {
        window.chartModule.update(_lastData);
      }

    } catch (err) {
      console.error('Query failed:', err);
      renderStats({ total: 0, withCoords: 0, topDistrict: '查詢失敗' });
    } finally {
      if (generation === _queryGeneration) {
        setLoading(false);
        if (elBtnQuery) elBtnQuery.disabled = false;
        setToggleDisabled(false);
      }
    }
  }

  /* -----------------------------------------------------------------------
     Dispatch query based on current mode
  ----------------------------------------------------------------------- */
  function doQuery() {
    if (getDisplayMode() === 'heat') {
      queryHeatmapOnly();
    } else {
      queryProgressive();
    }
  }

  function appendToMap(data, mode) {
    if (window.mapModule && typeof window.mapModule.appendData === 'function') {
      window.mapModule.appendData(data, mode);
    }
  }

  function updateProgress(loaded, total) {
    if (window.mapModule && typeof window.mapModule.setProgress === 'function') {
      window.mapModule.setProgress(loaded, total);
    }
  }

  /* -----------------------------------------------------------------------
     Year select — set max and placeholder dynamically to current year
  ----------------------------------------------------------------------- */
  function populateYearSelects() {
    const currentYear = new Date().getFullYear();
    if (elYearFrom) {
      elYearFrom.max         = String(currentYear);
      elYearFrom.placeholder = '2018';
    }
    if (elYearTo) {
      elYearTo.max         = String(currentYear);
      elYearTo.placeholder = String(currentYear);
    }
  }

  /* -----------------------------------------------------------------------
     Mode toggle — smart switch using cached data when available
  ----------------------------------------------------------------------- */
  function onModeChange() {
    const mode = getDisplayMode();

    if (mode === 'point') {
      if (_lastData.length > 0) {
        // Already have point data — just re-render
        if (window.mapModule && typeof window.mapModule.update === 'function') {
          window.mapModule.update(_lastData, 'point');
        }
      } else {
        // No data yet — load it now
        queryProgressive();
      }
    } else { // 'heat'
      if (_lastHeatmapData) {
        // Have cached heatmap — re-render without fetching
        if (window.mapModule) {
          if (typeof window.mapModule.update === 'function') {
            window.mapModule.update(_lastData, 'heat');
          }
          if (typeof window.mapModule.setHeatmap === 'function') {
            window.mapModule.setHeatmap(_lastHeatmapData);
          }
        }
      } else {
        // No heatmap data — fetch it now
        queryHeatmapOnly();
      }
    }
  }

  /* -----------------------------------------------------------------------
     Init
  ----------------------------------------------------------------------- */
  function init() {
    elCaseType        = document.getElementById('select-case-type');
    elDistrict        = document.getElementById('select-district');
    elYearFrom        = document.getElementById('input-year-from');
    elYearTo          = document.getElementById('input-year-to');
    elBtnQuery        = document.getElementById('btn-query');
    elToggleMode      = document.getElementById('toggle-mode');
    elStatTotal       = document.getElementById('stat-total');
    elStatWithCoords  = document.getElementById('stat-with-coords');
    elStatTopDistrict = document.getElementById('stat-top-district');
    elLoadingOverlay  = document.getElementById('loading-overlay');

    if (window.mapModule && typeof window.mapModule.init === 'function') {
      window.mapModule.init('map');
    }
    if (window.chartModule && typeof window.chartModule.init === 'function') {
      window.chartModule.init();
    }

    populateYearSelects();

    if (elBtnQuery) elBtnQuery.addEventListener('click', doQuery);
    if (elToggleMode) elToggleMode.addEventListener('change', onModeChange);

    doQuery(); // defaults to heat mode → queryHeatmapOnly() (instant)
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();

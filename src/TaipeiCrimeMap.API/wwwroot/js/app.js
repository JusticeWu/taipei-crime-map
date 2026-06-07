/**
 * app.js — Taipei Crime Map main application logic
 *
 * Responsibilities:
 *  1. Read filter values from the UI
 *  2. Progressive GET /api/crime?page=N&pageSize=200
 *  3. Append each page to the map immediately
 *  4. Show loading progress (top-left of map)
 *  5. Update stats panel and charts after all pages load
 *  6. Re-render on display-mode toggle using cached data
 */

(function () {
  'use strict';

  const API_BASE  = '/api/crime';
  const PAGE_SIZE = 200;

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
  let _lastData      = [];   // all accumulated records for current query
  let _queryGeneration = 0;  // incremented on each new query to abort stale loads

  /* -----------------------------------------------------------------------
     Loading overlay
  ----------------------------------------------------------------------- */
  function setLoading(visible) {
    if (elLoadingOverlay) elLoadingOverlay.classList.toggle('hidden', !visible);
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

  function renderStats(stats) {
    if (elStatTotal)       elStatTotal.textContent       = stats.total.toLocaleString();
    if (elStatWithCoords)  elStatWithCoords.textContent  = stats.withCoords.toLocaleString();
    if (elStatTopDistrict) elStatTopDistrict.textContent = stats.topDistrict;
  }

  /* -----------------------------------------------------------------------
     Progressive query
  ----------------------------------------------------------------------- */
  async function queryProgressive() {
    const generation = ++_queryGeneration;

    setLoading(true);
    if (elBtnQuery) elBtnQuery.disabled = true;

    _lastData = [];

    const mode = getDisplayMode();

    if (window.mapModule && typeof window.mapModule.startProgressiveLoad === 'function') {
      window.mapModule.startProgressiveLoad(mode);
    }

    try {
      const baseParams = buildQueryParams();
      baseParams.set('pageSize', String(PAGE_SIZE));

      // ── Page 1 ────────────────────────────────────────────────────────────
      baseParams.set('page', '1');
      const url1 = `${API_BASE}?${baseParams}`;
      console.log('[app] fetching page 1:', url1);

      const resp1 = await fetch(url1, { headers: { Accept: 'application/json' } });
      if (!resp1.ok) {
        const body = await resp1.text().catch(() => '');
        throw new Error(`API ${resp1.status}: ${body}`);
      }

      const first = await resp1.json();
      console.log('[app] page 1 response:', JSON.stringify(first).slice(0, 200));

      if (generation !== _queryGeneration) return;

      // 防禦：確保欄位存在
      const firstData  = Array.isArray(first.data)   ? first.data   : [];
      const total      = typeof first.total      === 'number' ? first.total      : firstData.length;
      const totalPages = typeof first.totalPages === 'number' ? first.totalPages : 1;

      console.log(`[app] total=${total}, totalPages=${totalPages}, page1 records=${firstData.length}`);

      _lastData = _lastData.concat(firstData);
      appendToMap(firstData, mode);
      updateProgress(_lastData.length, total);

      // ── Pages 2..totalPages ───────────────────────────────────────────────
      for (let page = 2; page <= totalPages; page++) {
        if (generation !== _queryGeneration) return;

        baseParams.set('page', String(page));
        const resp = await fetch(`${API_BASE}?${baseParams}`, { headers: { Accept: 'application/json' } });
        if (!resp.ok) { console.warn(`[app] page ${page} failed:`, resp.status); continue; }

        const pageResult = await resp.json();
        if (generation !== _queryGeneration) return;

        const pageData = Array.isArray(pageResult.data) ? pageResult.data : [];
        _lastData = _lastData.concat(pageData);
        appendToMap(pageData, mode);
        updateProgress(_lastData.length, total);
      }

      // ── Finalize ──────────────────────────────────────────────────────────
      console.log(`[app] all ${_lastData.length} records loaded`);

      if (window.mapModule && typeof window.mapModule.finalizeLoad === 'function') {
        window.mapModule.finalizeLoad(_lastData, mode);
      }
      if (window.mapModule && typeof window.mapModule.clearProgress === 'function') {
        window.mapModule.clearProgress();
      }

      renderStats(computeStats(_lastData));

      if (window.chartModule && typeof window.chartModule.update === 'function') {
        window.chartModule.update(_lastData);
      }

    } catch (err) {
      console.error('[app] query failed:', err);
      renderStats({ total: 0, withCoords: 0, topDistrict: '查詢失敗' });
    } finally {
      if (generation === _queryGeneration) {
        setLoading(false);
        if (elBtnQuery) elBtnQuery.disabled = false;
      }
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
     Year selects — dynamic generation
  ----------------------------------------------------------------------- */
  function populateYearSelects() {
    const MIN_YEAR    = 2015;
    const currentYear = new Date().getFullYear();

    [elYearFrom, elYearTo].forEach(el => {
      if (!el) return;
      // Remove existing dynamic options (keep the first "全部" option)
      while (el.options.length > 1) el.remove(1);
      for (let y = MIN_YEAR; y <= currentYear; y++) {
        const opt = document.createElement('option');
        opt.value       = String(y);
        opt.textContent = String(y);
        el.appendChild(opt);
      }
    });
  }

  /* -----------------------------------------------------------------------
     Mode toggle — re-render without re-fetch
  ----------------------------------------------------------------------- */
  function onModeChange() {
    if (!_lastData.length) return;
    const mode = getDisplayMode();
    if (window.mapModule && typeof window.mapModule.update === 'function') {
      window.mapModule.update(_lastData, mode);
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

    populateYearSelects();

    if (window.mapModule && typeof window.mapModule.init === 'function') {
      window.mapModule.init('map');
    }
    if (window.chartModule && typeof window.chartModule.init === 'function') {
      window.chartModule.init();
    }

    if (elBtnQuery) elBtnQuery.addEventListener('click', queryProgressive);
    if (elToggleMode) elToggleMode.addEventListener('change', onModeChange);

    queryProgressive();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();

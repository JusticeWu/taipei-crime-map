/**
 * app.js — Taipei Crime Map main application logic
 *
 * Responsibilities:
 *  1. Read filter values from the UI
 *  2. Call GET /api/crime with query-string parameters
 *  3. Update stats panel
 *  4. Delegate map rendering to window.mapModule
 *  5. Delegate chart rendering to window.chartModule
 *  6. Auto-query on page load
 *  7. Re-query on button click
 */

(function () {
  'use strict';

  /* -----------------------------------------------------------------------
     Constants
  ----------------------------------------------------------------------- */
  const API_BASE = '/api/crime';

  /* -----------------------------------------------------------------------
     DOM references (resolved after DOMContentLoaded)
  ----------------------------------------------------------------------- */
  let elCaseType;
  let elDistrict;
  let elYearFrom;
  let elYearTo;
  let elBtnQuery;
  let elToggleMode;
  let elStatTotal;
  let elStatWithCoords;
  let elStatTopDistrict;
  let elLoadingOverlay;

  /* -----------------------------------------------------------------------
     Helpers
  ----------------------------------------------------------------------- */

  /**
   * Show or hide the full-screen loading overlay.
   * @param {boolean} visible
   */
  function setLoading(visible) {
    if (!elLoadingOverlay) return;
    elLoadingOverlay.classList.toggle('hidden', !visible);
  }

  /**
   * Read the currently selected display mode from the toggle radio buttons.
   * @returns {'heat'|'point'}
   */
  function getDisplayMode() {
    if (!elToggleMode) return 'heat';
    const checked = elToggleMode.querySelector('input[type="radio"]:checked');
    return checked ? checked.value : 'heat';
  }

  /**
   * Build query-string params from the current filter values.
   * Empty / blank values are omitted.
   * @returns {URLSearchParams}
   */
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

  /**
   * Compute aggregate statistics from the raw crime data array.
   *
   * Each element is expected to be an object that may contain:
   *   - latitude  / longitude  (numbers, nullable)
   *   - districtName           (string)
   *
   * @param {Array<Object>} data
   * @returns {{ total: number, withCoords: number, topDistrict: string }}
   */
  function computeStats(data) {
    if (!Array.isArray(data) || data.length === 0) {
      return { total: 0, withCoords: 0, topDistrict: '—' };
    }

    let withCoords = 0;
    const districtCount = {};

    for (const item of data) {
      // Count records that have valid lat/lng
      const hasLat = item.latitude  != null && item.latitude  !== '';
      const hasLng = item.longitude != null && item.longitude !== '';
      if (hasLat && hasLng) withCoords++;

      // Tally district occurrences
      const district = item.districtName || item.district || '';
      if (district) {
        districtCount[district] = (districtCount[district] || 0) + 1;
      }
    }

    // Find the district with the highest count
    let topDistrict = '—';
    let maxCount = 0;
    for (const [name, count] of Object.entries(districtCount)) {
      if (count > maxCount) {
        maxCount = count;
        topDistrict = name;
      }
    }

    return { total: data.length, withCoords, topDistrict };
  }

  /**
   * Write computed stats into the stats panel DOM elements.
   * @param {{ total: number, withCoords: number, topDistrict: string }} stats
   */
  function renderStats(stats) {
    if (elStatTotal)       elStatTotal.textContent       = stats.total.toLocaleString();
    if (elStatWithCoords)  elStatWithCoords.textContent  = stats.withCoords.toLocaleString();
    if (elStatTopDistrict) elStatTopDistrict.textContent = stats.topDistrict;
  }

  /* -----------------------------------------------------------------------
     Core: fetch data and propagate to modules
  ----------------------------------------------------------------------- */

  /**
   * Fetch crime data from the API with the current filter settings,
   * then update stats, map and charts.
   */
  async function query() {
    setLoading(true);
    if (elBtnQuery) elBtnQuery.disabled = true;

    try {
      const params = buildQueryParams();
      const url = params.toString() ? `${API_BASE}?${params}` : API_BASE;

      const response = await fetch(url, {
        method: 'GET',
        headers: { 'Accept': 'application/json' },
      });

      if (!response.ok) {
        throw new Error(`API responded with status ${response.status}`);
      }

      const data = await response.json();

      // 1. Update stats panel
      const stats = computeStats(data);
      renderStats(stats);

      // 2. Update map
      const mode = getDisplayMode();
      if (window.mapModule && typeof window.mapModule.update === 'function') {
        window.mapModule.update(data, mode);
      } else {
        console.warn('window.mapModule.update is not available.');
      }

      // 3. Update charts
      if (window.chartModule && typeof window.chartModule.update === 'function') {
        window.chartModule.update(data);
      } else {
        console.warn('window.chartModule.update is not available.');
      }

    } catch (err) {
      console.error('Query failed:', err);
      // Reset stats to error state without crashing
      renderStats({ total: 0, withCoords: 0, topDistrict: '查詢失敗' });
    } finally {
      setLoading(false);
      if (elBtnQuery) elBtnQuery.disabled = false;
    }
  }

  /* -----------------------------------------------------------------------
     Display-mode change handler
  ----------------------------------------------------------------------- */

  /**
   * When the user switches heat/point mode, re-render the map
   * without hitting the network again (use last known data).
   * We keep a reference to the last fetched dataset for this purpose.
   */
  let _lastData = [];

  /**
   * Augmented query that caches the result for mode-toggle re-renders.
   */
  async function queryAndCache() {
    setLoading(true);
    if (elBtnQuery) elBtnQuery.disabled = true;

    try {
      const params = buildQueryParams();
      const url = params.toString() ? `${API_BASE}?${params}` : API_BASE;

      const response = await fetch(url, {
        method: 'GET',
        headers: { 'Accept': 'application/json' },
      });

      if (!response.ok) {
        throw new Error(`API responded with status ${response.status}`);
      }

      _lastData = await response.json();

      const stats = computeStats(_lastData);
      renderStats(stats);

      const mode = getDisplayMode();
      if (window.mapModule && typeof window.mapModule.update === 'function') {
        window.mapModule.update(_lastData, mode);
      }

      if (window.chartModule && typeof window.chartModule.update === 'function') {
        window.chartModule.update(_lastData);
      }

    } catch (err) {
      console.error('Query failed:', err);
      renderStats({ total: 0, withCoords: 0, topDistrict: '查詢失敗' });
    } finally {
      setLoading(false);
      if (elBtnQuery) elBtnQuery.disabled = false;
    }
  }

  /**
   * Re-render map with cached data when display mode changes (no new fetch).
   */
  function onModeChange() {
    if (!_lastData.length) return;
    const mode = getDisplayMode();
    if (window.mapModule && typeof window.mapModule.update === 'function') {
      window.mapModule.update(_lastData, mode);
    }
  }

  /* -----------------------------------------------------------------------
     Initialisation
  ----------------------------------------------------------------------- */

  function init() {
    // Resolve DOM references
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

    // Initialise map module
    if (window.mapModule && typeof window.mapModule.init === 'function') {
      window.mapModule.init('map');
    } else {
      console.warn('window.mapModule.init is not available.');
    }

    // Initialise chart module
    if (window.chartModule && typeof window.chartModule.init === 'function') {
      window.chartModule.init();
    } else {
      console.warn('window.chartModule.init is not available.');
    }

    // Bind query button
    if (elBtnQuery) {
      elBtnQuery.addEventListener('click', queryAndCache);
    }

    // Bind display-mode toggle (re-render without re-fetch)
    if (elToggleMode) {
      elToggleMode.addEventListener('change', onModeChange);
    }

    // Auto-query on load (no filter params)
    queryAndCache();
  }

  // Bootstrap after DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();

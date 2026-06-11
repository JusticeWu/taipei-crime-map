'use strict';

/**
 * Pure-function unit tests for app.js logic.
 *
 * These functions are extracted / replicated here for isolated testing.
 * Any change to the corresponding logic in app.js must be reflected here.
 */

// ── Functions under test ────────────────────────────────────────────────────

const CACHE_PREFIX = 'crimes:points:';

function buildCacheKey(caseType, district, yearFrom, yearTo) {
  return CACHE_PREFIX +
    (caseType  || '') + ':' +
    (district  || '') + ':' +
    (yearFrom  || '') + ':' +
    (yearTo    || '');
}

function getMaxYear() {
  return new Date().getFullYear();
}

function parsePagedResult(response) {
  return {
    data:       Array.isArray(response && response.data) ? response.data : [],
    total:      (response && response.total)      || 0,
    totalPages: (response && response.totalPages) || 0,
  };
}

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
  return { total, withCoords: 0, topDistrict: (top && top.district) || '—' };
}

// ── buildCacheKey ───────────────────────────────────────────────────────────

describe('buildCacheKey', () => {
  test('no filter → all segments empty', () => {
    expect(buildCacheKey('', '', '', '')).toBe('crimes:points::::');
  });

  test('caseType only', () => {
    expect(buildCacheKey('1', '', '', '')).toBe('crimes:points:1:::');
  });

  test('all filters set', () => {
    expect(buildCacheKey('2', '大安區', '2020', '2024'))
      .toBe('crimes:points:2:大安區:2020:2024');
  });

  test('null / undefined treated as empty string', () => {
    expect(buildCacheKey(null, undefined, null, undefined))
      .toBe('crimes:points::::');
  });

  test('different filters produce different keys', () => {
    const k1 = buildCacheKey('1', '大安區', '', '');
    const k2 = buildCacheKey('2', '大安區', '', '');
    expect(k1).not.toBe(k2);
  });
});

// ── getMaxYear ──────────────────────────────────────────────────────────────

describe('getMaxYear', () => {
  test('returns a reasonable calendar year', () => {
    const year = getMaxYear();
    expect(year).toBeGreaterThanOrEqual(2024);
    expect(year).toBeLessThanOrEqual(2100);
  });

  test('matches new Date().getFullYear()', () => {
    expect(getMaxYear()).toBe(new Date().getFullYear());
  });
});

// ── parsePagedResult ────────────────────────────────────────────────────────

describe('parsePagedResult', () => {
  test('parses a complete valid response', () => {
    const result = parsePagedResult({ data: [1, 2, 3], total: 100, totalPages: 5 });
    expect(result.data).toEqual([1, 2, 3]);
    expect(result.total).toBe(100);
    expect(result.totalPages).toBe(5);
  });

  test('null response returns safe defaults', () => {
    expect(parsePagedResult(null)).toEqual({ data: [], total: 0, totalPages: 0 });
  });

  test('undefined response returns safe defaults', () => {
    expect(parsePagedResult(undefined)).toEqual({ data: [], total: 0, totalPages: 0 });
  });

  test('missing total / totalPages default to 0', () => {
    const result = parsePagedResult({ data: [{ id: 1 }] });
    expect(result.total).toBe(0);
    expect(result.totalPages).toBe(0);
    expect(result.data).toHaveLength(1);
  });

  test('non-array data field coerces to empty array', () => {
    const result = parsePagedResult({ data: 'invalid', total: 5, totalPages: 1 });
    expect(result.data).toEqual([]);
  });
});

// ── computeStats ────────────────────────────────────────────────────────────

describe('computeStats', () => {
  test('empty array → all zeros and dash', () => {
    expect(computeStats([])).toEqual({ total: 0, withCoords: 0, topDistrict: '—' });
  });

  test('null input → all zeros and dash', () => {
    expect(computeStats(null)).toEqual({ total: 0, withCoords: 0, topDistrict: '—' });
  });

  test('counts total correctly', () => {
    const data = Array.from({ length: 7 }, () => ({}));
    expect(computeStats(data).total).toBe(7);
  });

  test('withCoords: only items with non-null lat AND lng', () => {
    const data = [
      { latitude: 25.0, longitude: 121.5 },
      { latitude: null, longitude: null },
      { latitude: 25.1, longitude: 121.6 },
      { latitude: 0,    longitude: 0     },   // zero is still a valid coord
    ];
    expect(computeStats(data).withCoords).toBe(3);
  });

  test('topDistrict: most frequent district wins', () => {
    const data = [
      { district: '大安區' }, { district: '大安區' },
      { district: '中山區' },
    ];
    expect(computeStats(data).topDistrict).toBe('大安區');
  });

  test('topDistrict: dash when no district field present', () => {
    expect(computeStats([{}, {}, {}]).topDistrict).toBe('—');
  });

  test('districtName field is also accepted', () => {
    const data = [{ districtName: '信義區' }, { districtName: '信義區' }];
    expect(computeStats(data).topDistrict).toBe('信義區');
  });
});

// ── Mobile filter panel toggle ──────────────────────────────────────────────

/**
 * Replicates the mobile filter-toggle state machine from app.js
 * (openFilterPanel / closeFilterPanel / toggleFilterPanel /
 * updateFilterToggleLabel) plus the CSS height contract from style.css:
 *  - #btn-filter-toggle is fixed at height 48px (collapsed total height)
 *  - #filter-panel.open adds extra height on top of the 48px bar
 * Any change to that logic/CSS must be reflected here.
 */
const FILTER_BAR_HEIGHT     = 48;  // px — #btn-filter-toggle height
const FILTER_PANEL_OPEN_ADD = 400; // px — representative #filter-panel.open extra height

function createFilterPanelState() {
  let open = false;
  return {
    get open() { return open; },
    get label() { return open ? '篩選條件 ▲' : '篩選條件 ▼'; },
    get totalHeight() { return open ? FILTER_BAR_HEIGHT + FILTER_PANEL_OPEN_ADD : FILTER_BAR_HEIGHT; },
    openPanel()  { open = true; },
    closePanel() { open = false; },
    toggle()     { open = !open; },
  };
}

describe('mobile filter panel toggle', () => {
  test('collapsed by default with height 48px and ▼ label', () => {
    const state = createFilterPanelState();
    expect(state.open).toBe(false);
    expect(state.totalHeight).toBe(48);
    expect(state.label).toBe('篩選條件 ▼');
  });

  test('expanded height is greater than 48px with ▲ label', () => {
    const state = createFilterPanelState();
    state.openPanel();
    expect(state.open).toBe(true);
    expect(state.totalHeight).toBeGreaterThan(48);
    expect(state.label).toBe('篩選條件 ▲');
  });

  test('clicking the row toggles open → closed regardless of state', () => {
    const state = createFilterPanelState();

    state.toggle(); // closed → open
    expect(state.open).toBe(true);
    expect(state.totalHeight).toBeGreaterThan(48);

    state.toggle(); // open → closed
    expect(state.open).toBe(false);
    expect(state.totalHeight).toBe(48);
  });

  test('toggle from open state collapses back to 48px', () => {
    const state = createFilterPanelState();
    state.openPanel();
    state.toggle();
    expect(state.open).toBe(false);
    expect(state.totalHeight).toBe(48);
  });
});

// ── computeStatsFromHeatmap ─────────────────────────────────────────────────

describe('computeStatsFromHeatmap', () => {
  test('empty array → all zeros and dash', () => {
    expect(computeStatsFromHeatmap([])).toEqual({ total: 0, withCoords: 0, topDistrict: '—' });
  });

  test('sums all weights for total', () => {
    const data = [
      { weight: 100, district: '大安區' },
      { weight: 250, district: '中山區' },
      { weight:  50, district: '士林區' },
    ];
    expect(computeStatsFromHeatmap(data).total).toBe(400);
  });

  test('withCoords is always 0 (no geocoding)', () => {
    const data = [{ weight: 999, district: '大安區' }];
    expect(computeStatsFromHeatmap(data).withCoords).toBe(0);
  });

  test('topDistrict: district with highest weight', () => {
    const data = [
      { weight: 100, district: '大安區' },
      { weight: 500, district: '北投區' },
      { weight: 200, district: '信義區' },
    ];
    expect(computeStatsFromHeatmap(data).topDistrict).toBe('北投區');
  });

  test('missing weight treated as 0', () => {
    const data = [{ district: '大安區' }, { weight: 10, district: '中山區' }];
    expect(computeStatsFromHeatmap(data).topDistrict).toBe('中山區');
  });
});

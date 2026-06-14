'use strict';

/**
 * Pure-logic tests for map.js layer state management.
 *
 * Replicates the _heatLayer / _markerLayer / _fallbackLayer state machine
 * (clearLayers / update / startProgressiveLoad / setHeatmap) so mode
 * switches between heatmap and point mode never leave a stale layer
 * attached to the map (see L026/L027).
 *
 * Any change to the corresponding logic in map.js must be reflected here.
 */

// ── Replicated state machine ────────────────────────────────────────────────

function createMapState() {
  const onMap = new Set();
  let heatLayer = null;
  let markerLayer = null;
  let fallbackLayer = null;

  function clearLayers() {
    if (heatLayer)     { onMap.delete(heatLayer);     heatLayer     = null; }
    if (markerLayer)   { onMap.delete(markerLayer);   markerLayer   = null; }
    if (fallbackLayer) { onMap.delete(fallbackLayer); fallbackLayer = null; }
  }

  function buildHeatLayer() {
    heatLayer = { type: 'heat' };
    onMap.add(heatLayer);
  }

  function buildMarkerLayer() {
    markerLayer = { type: 'marker' };
    onMap.add(markerLayer);
  }

  function buildDistrictFallbackLayer(hasFallbackData) {
    if (fallbackLayer) { onMap.delete(fallbackLayer); fallbackLayer = null; }
    if (!hasFallbackData) return;
    fallbackLayer = { type: 'fallback' };
    onMap.add(fallbackLayer);
  }

  // Full re-render (mode toggle after data already loaded)
  function update(mode, hasFallbackData) {
    clearLayers();
    if (mode === 'heat') buildHeatLayer();
    else if (mode === 'point') buildMarkerLayer();
    buildDistrictFallbackLayer(hasFallbackData);
  }

  // Called before progressive loading
  function startProgressiveLoad(mode) {
    clearLayers();
    if (mode === 'point') buildMarkerLayer();
    // heat: no layer created here — setHeatmap() handles it
  }

  // Replaces heat layer + district bubbles together
  function setHeatmap() {
    if (heatLayer)     { onMap.delete(heatLayer);     heatLayer     = null; }
    heatLayer = { type: 'heat' };
    onMap.add(heatLayer);

    if (fallbackLayer) { onMap.delete(fallbackLayer); fallbackLayer = null; }
    fallbackLayer = { type: 'fallback' };
    onMap.add(fallbackLayer);
  }

  return {
    onMap,
    get heatLayer()     { return heatLayer; },
    get markerLayer()   { return markerLayer; },
    get fallbackLayer() { return fallbackLayer; },
    clearLayers,
    update,
    startProgressiveLoad,
    setHeatmap,
  };
}

// ── Tests ────────────────────────────────────────────────────────────────────

describe('map layer state — switching to point mode', () => {
  test('update("point") removes leftover heat layer and district fallback bubbles', () => {
    const state = createMapState();
    state.update('heat', true); // simulate prior heat mode render

    expect(state.heatLayer).not.toBeNull();
    expect(state.fallbackLayer).not.toBeNull();

    state.update('point', false);

    expect(state.heatLayer).toBeNull();
    expect(state.fallbackLayer).toBeNull();
    expect(state.markerLayer).not.toBeNull();
    expect(state.onMap.has(state.markerLayer)).toBe(true);
  });

  test('startProgressiveLoad("point") removes leftover heat layer and fallback layer', () => {
    const state = createMapState();
    state.setHeatmap(); // simulate prior heat mode with district bubbles

    expect(state.heatLayer).not.toBeNull();
    expect(state.fallbackLayer).not.toBeNull();

    state.startProgressiveLoad('point');

    expect(state.heatLayer).toBeNull();
    expect(state.fallbackLayer).toBeNull();
    expect(state.markerLayer).not.toBeNull();
  });
});

describe('map layer state — switching to heat mode', () => {
  test('update("heat") removes leftover marker cluster layer', () => {
    const state = createMapState();
    state.update('point', false); // simulate prior point mode render

    expect(state.markerLayer).not.toBeNull();

    state.update('heat', true);

    expect(state.markerLayer).toBeNull();
    expect(state.heatLayer).not.toBeNull();
    expect(state.onMap.has(state.markerLayer)).toBe(false);
  });

  test('startProgressiveLoad("heat") followed by setHeatmap() removes leftover marker cluster layer', () => {
    const state = createMapState();
    state.startProgressiveLoad('point');
    state.update('point', false); // marker layer present from point mode

    expect(state.markerLayer).not.toBeNull();

    state.startProgressiveLoad('heat');
    state.setHeatmap();

    expect(state.markerLayer).toBeNull();
    expect(state.heatLayer).not.toBeNull();
    expect(state.onMap.has(state.markerLayer)).toBe(false);
  });
});

describe('map layer state — onMap set never retains stale layers', () => {
  test('repeated mode switches leave only the current mode layers on the map', () => {
    const state = createMapState();

    state.update('heat', true);
    state.update('point', false);
    state.update('heat', true);
    state.update('point', false);

    expect(state.heatLayer).toBeNull();
    expect(state.fallbackLayer).toBeNull();
    expect(state.markerLayer).not.toBeNull();
    expect(state.onMap.size).toBe(1);
    expect(state.onMap.has(state.markerLayer)).toBe(true);
  });
});

// ── Layer picker (basemap icon button + flyout menu) ────────────────────────

/**
 * Replicates the layer-picker control state machine from map.js
 * (addLayerPicker / switchBaseLayer): clicking the icon button toggles
 * the flyout menu, clicking the map or selecting a layer closes it,
 * and the selected layer label is tracked.
 * Any change to that logic in map.js must be reflected here.
 */
const BASE_LAYER_LABELS = ['Voyager（預設）', 'Dark（深色）', 'Light（淡色）', '街道圖（OSM）'];

function createLayerPickerState() {
  let menuOpen = false;
  let currentLabel = BASE_LAYER_LABELS[0];

  return {
    get menuOpen() { return menuOpen; },
    get currentLabel() { return currentLabel; },
    clickButton() { menuOpen = !menuOpen; },
    clickMap()    { menuOpen = false; },
    clickOutside() { menuOpen = false; },
    selectLayer(label) {
      if (BASE_LAYER_LABELS.includes(label)) currentLabel = label;
      menuOpen = false;
    },
  };
}

describe('layer picker control', () => {
  test('menu is closed by default with Voyager selected', () => {
    const state = createLayerPickerState();
    expect(state.menuOpen).toBe(false);
    expect(state.currentLabel).toBe('Voyager（預設）');
  });

  test('clicking the icon button opens the menu', () => {
    const state = createLayerPickerState();
    state.clickButton();
    expect(state.menuOpen).toBe(true);
  });

  test('clicking the map closes the menu', () => {
    const state = createLayerPickerState();
    state.clickButton();
    expect(state.menuOpen).toBe(true);

    state.clickMap();
    expect(state.menuOpen).toBe(false);
  });

  test('clicking outside the control closes the menu', () => {
    const state = createLayerPickerState();
    state.clickButton();
    state.clickOutside();
    expect(state.menuOpen).toBe(false);
  });

  test('selecting a layer updates the current label and closes the menu', () => {
    const state = createLayerPickerState();
    state.clickButton();
    state.selectLayer('Dark（深色）');
    expect(state.currentLabel).toBe('Dark（深色）');
    expect(state.menuOpen).toBe(false);
  });
});

// ── panTo (point mode finalizeLoad / heat mode setHeatmap) ──────────────────

/**
 * Replicates the panTo-based re-centering logic in map.js:
 *   finalizeLoad(allData, mode) — computes the average lat/lng of all loaded
 *     points (filtered via hasCoords: numeric latitude/longitude, then
 *     restricted to TAIPEI_BOUNDS) and stores it in _pendingCenter (does NOT
 *     call panTo directly).
 *   drainOneFromQueue / applyPendingCenter — once the render queue drains
 *     (renderQueue.length === 0), applies _pendingCenter via
 *     _map.panTo(_pendingCenter) and clears _pendingCenter. Only the map
 *     center moves — the zoom level is left unchanged.
 *   setHeatmap(points)          — pans the map to the average lat/lng of the
 *     aggregated district points (filtered to numeric lat/lng, then
 *     restricted to TAIPEI_BOUNDS).
 * panTo is only called when there is at least one valid point within
 * TAIPEI_BOUNDS. Points outside TAIPEI_BOUNDS are excluded from the center
 * calculation but are still rendered on the map.
 * Any change to that logic in map.js must be reflected here.
 */
const TAIPEI_BOUNDS = { minLat: 24.95, maxLat: 25.25, minLng: 121.45, maxLng: 121.75 };

function hasCoords(item) {
  return typeof item.latitude === 'number' && !isNaN(item.latitude) &&
         typeof item.longitude === 'number' && !isNaN(item.longitude);
}

function isWithinTaipei(lat, lng) {
  return lat >= TAIPEI_BOUNDS.minLat && lat <= TAIPEI_BOUNDS.maxLat &&
         lng >= TAIPEI_BOUNDS.minLng && lng <= TAIPEI_BOUNDS.maxLng;
}

function computeAverageCenter(coords) {
  const sum = coords.reduce((acc, [lat, lng]) => [acc[0] + lat, acc[1] + lng], [0, 0]);
  return [sum[0] / coords.length, sum[1] / coords.length];
}

// Replicates finalizeLoad's center computation: returns the value that
// would be stored in _pendingCenter (or null if no in-bounds points).
function finalizeLoadComputeCenter(allData) {
  const coords = (Array.isArray(allData) ? allData : [])
    .filter(hasCoords)
    .filter(i => isWithinTaipei(i.latitude, i.longitude))
    .map(i => [i.latitude, i.longitude]);
  return coords.length > 0 ? computeAverageCenter(coords) : null;
}

// Replicates applyPendingCenter: called once the render queue drains.
function applyPendingCenter(map, pendingCenter) {
  if (!pendingCenter) return null;
  map.panTo(pendingCenter);
  return null;
}

function setHeatmapPanTo(map, points) {
  const coords = points
    .filter(p => typeof p.lat === 'number' && typeof p.lng === 'number')
    .filter(p => isWithinTaipei(p.lat, p.lng))
    .map(p => [p.lat, p.lng]);
  if (coords.length > 0) {
    map.panTo(computeAverageCenter(coords));
  }
}

function createMockMap() {
  return { map: { panTo: jest.fn() } };
}

describe('panTo — point mode (finalizeLoad + 延遲至 render queue 清空後套用)', () => {
  test('沒有任何點位時，_pendingCenter 為 null，render queue 清空後不呼叫 panTo', () => {
    const { map } = createMockMap();
    const pendingCenter = finalizeLoadComputeCenter([]);
    expect(pendingCenter).toBeNull();

    applyPendingCenter(map, pendingCenter);
    expect(map.panTo).not.toHaveBeenCalled();
  });

  test('所有點位都缺少座標時，_pendingCenter 為 null，render queue 清空後不呼叫 panTo', () => {
    const { map } = createMockMap();
    const pendingCenter = finalizeLoadComputeCenter([{ latitude: null, longitude: null }]);
    expect(pendingCenter).toBeNull();

    applyPendingCenter(map, pendingCenter);
    expect(map.panTo).not.toHaveBeenCalled();
  });

  test('有點位且具有座標時，render queue 清空後呼叫 panTo，並帶入平均座標', () => {
    const { map } = createMockMap();
    const pendingCenter = finalizeLoadComputeCenter([
      { latitude: 25.03, longitude: 121.5 },
      { latitude: 25.10, longitude: 121.6 },
    ]);
    expect(pendingCenter).toEqual([25.065, 121.55]);

    // panTo 尚未被呼叫，要等 render queue 清空
    expect(map.panTo).not.toHaveBeenCalled();

    const remaining = applyPendingCenter(map, pendingCenter);
    expect(map.panTo).toHaveBeenCalledWith([25.065, 121.55]);
    expect(remaining).toBeNull(); // _pendingCenter 被清空
  });

  test('台北市範圍外的點位不會影響 panTo 中心點計算', () => {
    const { map } = createMockMap();
    const pendingCenter = finalizeLoadComputeCenter([
      { latitude: 25.03, longitude: 121.5 },
      { latitude: 22.99, longitude: 120.21 }, // 高雄，超出台北市範圍
    ]);
    expect(pendingCenter).toEqual([25.03, 121.5]);

    applyPendingCenter(map, pendingCenter);
    expect(map.panTo).toHaveBeenCalledWith([25.03, 121.5]);
  });

  test('所有點位都在台北市範圍外時，_pendingCenter 為 null，render queue 清空後不呼叫 panTo', () => {
    const { map } = createMockMap();
    const pendingCenter = finalizeLoadComputeCenter([
      { latitude: 22.99, longitude: 120.21 }, // 高雄，超出台北市範圍
    ]);
    expect(pendingCenter).toBeNull();

    applyPendingCenter(map, pendingCenter);
    expect(map.panTo).not.toHaveBeenCalled();
  });
});

describe('panTo — 範圍外的點位仍會被渲染', () => {
  test('buildMarkerLayer 不過濾範圍，台北市範圍外但具座標的點位仍會加入圖層', () => {
    const data = [
      { latitude: 25.03, longitude: 121.5 },
      { latitude: 22.99, longitude: 120.21 }, // 高雄，超出台北市範圍但仍應渲染
    ];
    const rendered = data.filter(hasCoords);
    expect(rendered).toHaveLength(2);
  });
});

describe('panTo — heat mode (setHeatmap)', () => {
  test('沒有任何點位時不呼叫 panTo', () => {
    const { map } = createMockMap();
    setHeatmapPanTo(map, []);
    expect(map.panTo).not.toHaveBeenCalled();
  });

  test('點位缺少 lat/lng 時不呼叫 panTo', () => {
    const { map } = createMockMap();
    setHeatmapPanTo(map, [{ district: '中正區', weight: 10 }]);
    expect(map.panTo).not.toHaveBeenCalled();
  });

  test('有點位且具有 lat/lng 時呼叫 panTo，並帶入平均座標', () => {
    const { map } = createMockMap();
    setHeatmapPanTo(map, [
      { district: '中正區', weight: 10, lat: 25.0328, lng: 121.5199 },
      { district: '大同區', weight: 5,  lat: 25.0637, lng: 121.5131 },
    ]);
    expect(map.panTo).toHaveBeenCalledWith([(25.0328 + 25.0637) / 2, (121.5199 + 121.5131) / 2]);
  });

  test('台北市範圍外的點位不會影響 panTo 中心點計算', () => {
    const { map } = createMockMap();
    setHeatmapPanTo(map, [
      { district: '中正區', weight: 10, lat: 25.0328, lng: 121.5199 },
      { district: '高雄某區', weight: 3, lat: 22.99, lng: 120.21 }, // 超出台北市範圍
    ]);
    expect(map.panTo).toHaveBeenCalledWith([25.0328, 121.5199]);
  });

  test('所有點位都在台北市範圍外時不呼叫 panTo', () => {
    const { map } = createMockMap();
    setHeatmapPanTo(map, [
      { district: '高雄某區', weight: 3, lat: 22.99, lng: 120.21 }, // 超出台北市範圍
    ]);
    expect(map.panTo).not.toHaveBeenCalled();
  });
});

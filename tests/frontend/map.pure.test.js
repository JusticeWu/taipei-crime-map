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

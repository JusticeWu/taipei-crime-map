/**
 * map.js — Taipei Crime Map, Leaflet.js Map Module
 *
 * Exposes window.mapModule with:
 *   init(containerId)                  — initialise Leaflet map
 *   update(data, mode)                 — full re-render (used by mode toggle)
 *   startProgressiveLoad(mode)         — clear layers, init empty layer for mode
 *   appendData(data, mode)             — add a page of data incrementally
 *   finalizeLoad(allData, mode)        — add district bubble markers after all pages loaded
 *   setProgress(loaded, total)         — update top-left progress indicator
 *   clearProgress()                    — remove progress indicator
 */

(function () {
  'use strict';

  // ---------------------------------------------------------------------------
  // Tile layers
  // ---------------------------------------------------------------------------

  const TILE_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>';
  const OSM_ATTRIBUTION  = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

  const TILE_DEFS = {
    'Voyager（預設）': {
      url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png',
      attribution: TILE_ATTRIBUTION,
      subdomains: 'abcd',
    },
    'Dark（深色）': {
      url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',
      attribution: TILE_ATTRIBUTION,
      subdomains: 'abcd',
    },
    'Light（淡色）': {
      url: 'https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png',
      attribution: TILE_ATTRIBUTION,
      subdomains: 'abcd',
    },
    '街道圖（OSM）': {
      url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      attribution: OSM_ATTRIBUTION,
      subdomains: 'abc',
    },
  };

  // ---------------------------------------------------------------------------
  // Constants
  // ---------------------------------------------------------------------------

  const TAIPEI_CENTER = [25.0478, 121.5318];
  const DEFAULT_ZOOM  = 13;

  const CASE_TYPE_COLORS = {
    '住宅竊盜':   '#e74c3c',
    '汽車竊盜':   '#e67e22',
    '機車竊盜':   '#f1c40f',
    '自行車竊盜': '#2ecc71',
    '搶奪':      '#9b59b6',
    '強盜':      '#1abc9c',
  };
  const DEFAULT_COLOR = '#95a5a6';

  const HEAT_OPTIONS = { radius: 20, blur: 15, maxZoom: 17, max: 1.0 };
  const HEAT_INTENSITY = 0.5;

  // District centroids — used for fallback markers when exact coords are unavailable
  const DISTRICT_CENTROIDS = {
    '中正區': [25.0328, 121.5199],
    '大同區': [25.0637, 121.5131],
    '中山區': [25.0694, 121.5326],
    '松山區': [25.0499, 121.5776],
    '大安區': [25.0266, 121.5432],
    '萬華區': [25.0333, 121.4981],
    '信義區': [25.0330, 121.5654],
    '士林區': [25.0934, 121.5241],
    '北投區': [25.1317, 121.4988],
    '內湖區': [25.0831, 121.5874],
    '南港區': [25.0549, 121.6076],
    '文山區': [24.9989, 121.5699],
  };

  // ---------------------------------------------------------------------------
  // Internal state
  // ---------------------------------------------------------------------------

  let _map              = null;
  let _heatLayer        = null;
  let _markerLayer      = null;
  let _fallbackLayer    = null;   // district bubble markers for null-coord data
  let _legendCtrl       = null;
  let _districtLabelLayer = null;
  let _progressCtrl     = null;
  let _baseLayers       = {};

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  function hasCoords(item) {
    return typeof item.latitude === 'number' && !isNaN(item.latitude) &&
           typeof item.longitude === 'number' && !isNaN(item.longitude);
  }

  function colorForType(caseType) {
    return CASE_TYPE_COLORS[caseType] || DEFAULT_COLOR;
  }

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  function buildPopupHtml(item) {
    return [
      '<div class="crime-popup">',
      `  <strong>${escapeHtml(item.caseType || '未知')}</strong>`,
      '  <table>',
      `    <tr><th>行政區</th><td>${escapeHtml(item.district || '—')}</td></tr>`,
      `    <tr><th>日期</th><td>${escapeHtml(item.occurredDate || '—')}</td></tr>`,
      `    <tr><th>時段</th><td>${escapeHtml(item.timeSlot || '—')}</td></tr>`,
      `    <tr><th>地點</th><td>${escapeHtml(item.rawLocation || '—')}</td></tr>`,
      '  </table>',
      '</div>',
    ].join('\n');
  }

  // ---------------------------------------------------------------------------
  // Layer builders
  // ---------------------------------------------------------------------------

  function buildHeatLayer(data) {
    const points = data.filter(hasCoords).map(i => [i.latitude, i.longitude, HEAT_INTENSITY]);
    _heatLayer = L.heatLayer(points, HEAT_OPTIONS).addTo(_map);
  }

  function buildMarkerLayer(data) {
    _markerLayer = L.layerGroup();
    data.filter(hasCoords).forEach(item => {
      const color  = colorForType(item.caseType);
      const marker = L.circleMarker([item.latitude, item.longitude], {
        radius: 6, color, fillColor: color, fillOpacity: 0.7, weight: 1,
      });
      marker.bindPopup(buildPopupHtml(item), { maxWidth: 260 });
      _markerLayer.addLayer(marker);
    });
    _markerLayer.addTo(_map);
  }

  // District bubble markers — shown for items without exact coordinates.
  // Groups all data by district and places one interactive bubble per district.
  function buildDistrictFallbackLayer(data) {
    if (_fallbackLayer) { _map.removeLayer(_fallbackLayer); _fallbackLayer = null; }

    const counts = {};
    data.forEach(item => {
      if (item.district) counts[item.district] = (counts[item.district] || 0) + 1;
    });

    if (Object.keys(counts).length === 0) return;

    _fallbackLayer = L.layerGroup();
    Object.entries(DISTRICT_CENTROIDS).forEach(([district, latlng]) => {
      const count = counts[district] || 0;
      if (count === 0) return;

      const icon = L.divIcon({
        className: '',
        html: `<div class="district-bubble">` +
              `<div class="db-count">${count.toLocaleString()}</div>` +
              `<div class="db-name">${escapeHtml(district)}</div>` +
              `</div>`,
        iconAnchor: [28, 28],
        iconSize:   [56, 56],
      });

      L.marker(latlng, { icon, interactive: true })
        .bindPopup(
          `<strong>${escapeHtml(district)}</strong><br>` +
          `案件數：${count.toLocaleString()} 筆`,
          { maxWidth: 180 }
        )
        .addTo(_fallbackLayer);
    });
    _fallbackLayer.addTo(_map);
  }

  // ---------------------------------------------------------------------------
  // Legend
  // ---------------------------------------------------------------------------

  function addLegend() {
    if (_legendCtrl) return;
    const entries = [...Object.entries(CASE_TYPE_COLORS), ['其他', DEFAULT_COLOR]];
    const LegendControl = L.Control.extend({
      options: { position: 'bottomright' },
      onAdd() {
        const el = L.DomUtil.create('div', 'crime-legend');
        el.innerHTML =
          '<div class="legend-title">案件類型</div>' +
          entries.map(([label, color]) =>
            `<div class="legend-item">` +
            `<span class="legend-dot" style="background:${color};"></span>` +
            `<span class="legend-label">${escapeHtml(label)}</span>` +
            `</div>`
          ).join('');
        return el;
      },
    });
    _legendCtrl = new LegendControl();
    _legendCtrl.addTo(_map);
  }

  function removeLegend() {
    if (_legendCtrl) { _legendCtrl.remove(); _legendCtrl = null; }
  }

  // ---------------------------------------------------------------------------
  // District labels (legacy text — kept for cleanup; replaced by bubble markers)
  // ---------------------------------------------------------------------------

  function removeDistrictLabels() {
    if (_districtLabelLayer) { _map.removeLayer(_districtLabelLayer); _districtLabelLayer = null; }
  }

  // ---------------------------------------------------------------------------
  // Progress control
  // ---------------------------------------------------------------------------

  const ProgressControl = L.Control.extend({
    options: { position: 'topleft' },
    onAdd() {
      this._div = L.DomUtil.create('div', 'map-progress');
      this._div.textContent = '載入中…';
      return this._div;
    },
    update(loaded, total) {
      if (this._div)
        this._div.textContent = `載入中 ${loaded.toLocaleString()}/${total.toLocaleString()}`;
    },
  });

  // ---------------------------------------------------------------------------
  // Layer cleanup
  // ---------------------------------------------------------------------------

  function clearLayers() {
    if (_heatLayer)     { _map.removeLayer(_heatLayer);     _heatLayer     = null; }
    if (_markerLayer)   { _map.removeLayer(_markerLayer);   _markerLayer   = null; }
    if (_fallbackLayer) { _map.removeLayer(_fallbackLayer); _fallbackLayer = null; }
    removeLegend();
  }

  // ---------------------------------------------------------------------------
  // Inline styles
  // ---------------------------------------------------------------------------

  function injectStyles() {
    if (document.getElementById('map-module-styles')) return;
    const style = document.createElement('style');
    style.id = 'map-module-styles';
    style.textContent = `
      .crime-popup strong { font-size:14px; display:block; margin-bottom:6px; }
      .crime-popup table  { border-collapse:collapse; font-size:12px; width:100%; }
      .crime-popup th     { text-align:left; padding:2px 6px 2px 0; color:#888; white-space:nowrap; }
      .crime-popup td     { padding:2px 0; }

      .crime-legend { background:rgba(30,30,30,.85); color:#ddd; padding:10px 14px; border-radius:6px; font-size:12px; line-height:1.6; box-shadow:0 2px 8px rgba(0,0,0,.5); min-width:110px; }
      .legend-title { font-weight:bold; margin-bottom:6px; font-size:13px; border-bottom:1px solid #555; padding-bottom:4px; }
      .legend-item  { display:flex; align-items:center; gap:6px; margin-bottom:3px; }
      .legend-dot   { display:inline-block; width:12px; height:12px; border-radius:50%; flex-shrink:0; border:1px solid rgba(255,255,255,.25); }
      .legend-label { white-space:nowrap; }

      .district-bubble { width:56px; height:56px; border-radius:50%; background:rgba(44,62,80,.88); border:2px solid rgba(255,255,255,.75); display:flex; flex-direction:column; align-items:center; justify-content:center; box-shadow:0 2px 8px rgba(0,0,0,.5); cursor:pointer; transition:transform .15s; }
      .district-bubble:hover { transform:scale(1.12); }
      .db-count { font-size:13px; font-weight:bold; color:#f1c40f; line-height:1.25; }
      .db-name  { font-size:9px; color:rgba(255,255,255,.9); line-height:1.2; text-align:center; }

      .map-progress { background:rgba(30,30,30,.80); color:#fff; padding:6px 12px; border-radius:4px; font-size:13px; font-weight:bold; box-shadow:0 2px 6px rgba(0,0,0,.4); }
    `;
    document.head.appendChild(style);
  }

  // ---------------------------------------------------------------------------
  // Public API
  // ---------------------------------------------------------------------------

  const mapModule = {

    init(containerId) {
      if (_map) return;
      injectStyles();

      _map = L.map(containerId, { center: TAIPEI_CENTER, zoom: DEFAULT_ZOOM });

      // Build base layers and add default
      const tileLayers = {};
      let first = true;
      for (const [label, def] of Object.entries(TILE_DEFS)) {
        const layer = L.tileLayer(def.url, {
          attribution: def.attribution,
          subdomains:  def.subdomains,
          maxZoom:     19,
        });
        tileLayers[label] = layer;
        if (first) { layer.addTo(_map); first = false; }
      }

      // Basemap switcher (top-right)
      L.control.layers(tileLayers, {}, { position: 'topright', collapsed: false }).addTo(_map);
      _baseLayers = tileLayers;
    },

    // Full re-render (used by mode-toggle after all data is loaded)
    update(data, mode) {
      if (!_map) return;
      if (!Array.isArray(data)) return;

      const center = _map.getCenter();
      const zoom   = _map.getZoom();

      clearLayers();
      removeDistrictLabels();

      if (mode === 'heat') {
        buildHeatLayer(data);
      } else if (mode === 'point') {
        buildMarkerLayer(data);
        addLegend();
      }

      buildDistrictFallbackLayer(data);

      _map.setView(center, zoom, { animate: false });
    },

    // Called before progressive loading: clear layers and init empty layer.
    // Heat mode defers layer creation to finalizeLoad to avoid incremental redraws.
    startProgressiveLoad(mode) {
      if (!_map) return;
      clearLayers();
      removeDistrictLabels();

      if (mode === 'point') {
        _markerLayer = L.layerGroup().addTo(_map);
        addLegend();
      }
      // heat: no layer created here — buildHeatLayer called once in finalizeLoad
    },

    // Add one page of data without clearing existing layers.
    // Heat mode: no-op — all points are rendered at once in finalizeLoad.
    // Point mode: deferred via setTimeout(0) so the browser can repaint between pages.
    appendData(data, mode) {
      if (!_map || !Array.isArray(data)) return;
      if (mode === 'heat') return; // heat collected by app.js, rendered in finalizeLoad

      setTimeout(() => {
        const coordData = data.filter(hasCoords);
        if (mode === 'point' && _markerLayer) {
          coordData.forEach(item => {
            const color  = colorForType(item.caseType);
            const marker = L.circleMarker([item.latitude, item.longitude], {
              radius: 6, color, fillColor: color, fillOpacity: 0.7, weight: 1,
            });
            marker.bindPopup(buildPopupHtml(item), { maxWidth: 260 });
            _markerLayer.addLayer(marker);
          });
        }
      }, 0);
    },

    // Called once after all pages loaded.
    // Heat mode: show district bubbles first, then build heatmap on next tick
    // so the browser renders the bubble state before the (potentially slow) heat calc.
    finalizeLoad(allData, mode) {
      if (!_map) return;
      if (mode === 'heat') {
        buildDistrictFallbackLayer(allData);          // bubbles appear immediately
        setTimeout(() => {
          if (_fallbackLayer) { _map.removeLayer(_fallbackLayer); _fallbackLayer = null; }
          buildHeatLayer(allData);                    // full heatmap in one pass
          buildDistrictFallbackLayer(allData);        // re-add bubbles alongside heatmap
        }, 0);
      } else {
        buildDistrictFallbackLayer(allData);
      }
    },

    // Show / update progress indicator (top-left)
    setProgress(loaded, total) {
      if (!_map) return;
      if (!_progressCtrl) {
        _progressCtrl = new ProgressControl();
        _progressCtrl.addTo(_map);
      }
      _progressCtrl.update(loaded, total);
    },

    // Remove progress indicator
    clearProgress() {
      if (_progressCtrl) { _progressCtrl.remove(); _progressCtrl = null; }
    },

    get instance() { return _map; },
  };

  window.mapModule = mapModule;
})();

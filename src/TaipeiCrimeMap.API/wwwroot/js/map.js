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
    '住宅竊盜':   '#1E8449',
    '強盜':      '#E67E22',
    '搶奪':      '#D4A017',
    '汽車竊盜':   '#16A085',
    '機車竊盜':   '#1A5276',
    '自行車竊盜': '#8E44AD',
  };
  const DEFAULT_COLOR = '#95A5A6';

  // MarkerCluster 群聚內包含多種案類時使用的顏色
  const MIXED_CLUSTER_COLOR = '#C0392B';

  // 淺色背景（如搶奪的深黃 #D4A017）需要深色文字才能清楚閱讀
  const DARK_TEXT_COLOR  = '#333333';
  const LIGHT_TEXT_COLOR = '#FFFFFF';

  // 數字案類代碼（CaseType enum）→ 中文名稱，對應 CASE_TYPE_COLORS 的 key
  const CASE_TYPE_ID_TO_NAME = {
    1: '住宅竊盜',
    2: '汽車竊盜',
    3: '機車竊盜',
    4: '自行車竊盜',
    5: '搶奪',
    6: '強盜',
  };

  /**
   * 依案類（中文字串或數字代碼）取得對應顏色，找不到時回傳灰色 DEFAULT_COLOR
   * @param {string|number} caseType
   * @returns {string} 十六進位顏色碼
   */
  function getCaseTypeColor(caseType) {
    const name = CASE_TYPE_ID_TO_NAME[caseType] || caseType;
    return CASE_TYPE_COLORS[name] || DEFAULT_COLOR;
  }

  /**
   * 依群聚內所有點位的案類，決定群聚圓圈顏色：
   * 全部同一案類 → 該案類顏色；包含多種案類 → MIXED_CLUSTER_COLOR
   * @param {Array<string|number>} caseTypes
   * @returns {string} 十六進位顏色碼
   */
  function getClusterColor(caseTypes) {
    if (!Array.isArray(caseTypes) || caseTypes.length === 0) return MIXED_CLUSTER_COLOR;
    const colors = new Set(caseTypes.map(getCaseTypeColor));
    return colors.size === 1 ? [...colors][0] : MIXED_CLUSTER_COLOR;
  }

  /**
   * 依背景顏色決定文字顏色，確保可讀性（例如深黃底用深色字）
   * @param {string} backgroundColor
   * @returns {string} 文字顏色十六進位碼
   */
  function getClusterTextColor(backgroundColor) {
    return String(backgroundColor).toUpperCase() === '#D4A017' ? DARK_TEXT_COLOR : LIGHT_TEXT_COLOR;
  }

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

  // Render queue: ensures one page of markers is added per animation frame
  let _renderQueue      = [];
  let _renderRafId      = null;
  let _renderStartTime  = null; // performance.now() when first chunk starts rendering
  let _renderTotalChunks = 0;   // total chunks queued for current load

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  function hasCoords(item) {
    return typeof item.latitude === 'number' && !isNaN(item.latitude) &&
           typeof item.longitude === 'number' && !isNaN(item.longitude);
  }

  function colorForType(caseType) {
    return getCaseTypeColor(caseType);
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

  // 群聚圓圈圖示：依群聚內所有點位的案類決定顏色與文字顏色
  function clusterIconCreateFunction(cluster) {
    const caseTypes = cluster.getAllChildMarkers().map(m => m.options.caseType);
    const color     = getClusterColor(caseTypes);
    const textColor = getClusterTextColor(color);
    const count     = cluster.getChildCount();
    return L.divIcon({
      html: `<div style="background:${color};color:${textColor};` +
            `width:100%;height:100%;border-radius:50%;display:flex;` +
            `align-items:center;justify-content:center;font-weight:bold;">` +
            `${count}</div>`,
      className: 'crime-cluster-icon',
      iconSize: L.point(40, 40),
    });
  }

  function buildMarkerLayer(data) {
    _markerLayer = L.markerClusterGroup({ chunkedLoading: true, iconCreateFunction: clusterIconCreateFunction });
    data.filter(hasCoords).forEach(item => {
      const color  = colorForType(item.caseType);
      const marker = L.circleMarker([item.latitude, item.longitude], {
        radius: 6, color, fillColor: color, fillOpacity: 0.7, weight: 1, caseType: item.caseType,
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
  // Render queue — one page of markers per animation frame
  // ---------------------------------------------------------------------------

  function drainOneFromQueue() {
    if (_renderQueue.length === 0) { _renderRafId = null; return; }

    // Log render start on the very first chunk
    if (_renderStartTime === null) {
      _renderStartTime = performance.now();
      console.log(`[點位圖] 開始渲染｜render queue: ${_renderTotalChunks} chunks`);
    }

    const { data } = _renderQueue.shift();
    if (_markerLayer) {
      data.filter(hasCoords).forEach(item => {
        const color  = colorForType(item.caseType);
        const marker = L.circleMarker([item.latitude, item.longitude], {
          radius: 6, color, fillColor: color, fillOpacity: 0.7, weight: 1, caseType: item.caseType,
        });
        marker.bindPopup(buildPopupHtml(item), { maxWidth: 260 });
        _markerLayer.addLayer(marker);
      });
    }

    if (_renderQueue.length > 0) {
      _renderRafId = requestAnimationFrame(drainOneFromQueue);
    } else {
      _renderRafId = null;
      const ms = (performance.now() - _renderStartTime).toFixed(0);
      console.log(`[點位圖] 渲染完成｜渲染耗時: ${ms} ms`);
      _renderStartTime   = null;
      _renderTotalChunks = 0;
    }
  }

  function clearRenderQueue() {
    _renderQueue       = [];
    _renderStartTime   = null;
    _renderTotalChunks = 0;
    if (_renderRafId) { cancelAnimationFrame(_renderRafId); _renderRafId = null; }
  }

  // ---------------------------------------------------------------------------
  // Layer cleanup
  // ---------------------------------------------------------------------------

  function clearLayers() {
    if (_heatLayer)     { _map.removeLayer(_heatLayer);     _heatLayer     = null; }
    if (_markerLayer)   { _map.removeLayer(_markerLayer);   _markerLayer   = null; }
    if (_fallbackLayer) { _map.removeLayer(_fallbackLayer); _fallbackLayer = null; }
    clearRenderQueue();
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

      .crime-cluster-icon { width:40px; height:40px; box-shadow:0 1px 4px rgba(0,0,0,.5); }
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
        _markerLayer = L.markerClusterGroup({ chunkedLoading: true, iconCreateFunction: clusterIconCreateFunction });
        _markerLayer.addTo(_map);
        addLegend();
      }
      // heat: no layer created here — setHeatmap() handles it
    },

    // Add one page of data without clearing existing layers.
    // Heat mode: no-op — heatmap rendered via setHeatmap().
    // Point mode: enqueued and rendered one page per requestAnimationFrame so the
    // browser repaints between pages and the user sees markers appear progressively.
    appendData(data, mode) {
      if (!_map || !Array.isArray(data)) return;
      if (mode === 'heat') return;

      _renderQueue.push({ data });
      _renderTotalChunks++;
      if (!_renderRafId) {
        _renderRafId = requestAnimationFrame(drainOneFromQueue);
      }
    },

    // Apply heatmap API data: builds heat layer + district bubble markers together.
    // Weight is normalised to [0,1] so Leaflet.heat scales colours correctly.
    // Replaces any existing heat layer and fallback bubbles.
    setHeatmap(points) {
      if (!_map || !Array.isArray(points) || points.length === 0) return;

      // Heat layer
      if (_heatLayer) { _map.removeLayer(_heatLayer); _heatLayer = null; }
      const maxWeight = points.reduce((m, p) => Math.max(m, p.weight || 0), 1);
      _heatLayer = L.heatLayer(
        points.map(p => [p.lat, p.lng, p.weight / maxWeight]),
        HEAT_OPTIONS
      ).addTo(_map);

      // District bubble markers from aggregated data
      if (_fallbackLayer) { _map.removeLayer(_fallbackLayer); _fallbackLayer = null; }
      _fallbackLayer = L.layerGroup();
      points.forEach(p => {
        if (!p.district || !(p.weight > 0)) return;
        const icon = L.divIcon({
          className: '',
          html: `<div class="district-bubble">` +
                `<div class="db-count">${p.weight.toLocaleString()}</div>` +
                `<div class="db-name">${escapeHtml(p.district)}</div>` +
                `</div>`,
          iconAnchor: [28, 28],
          iconSize:   [56, 56],
        });
        L.marker([p.lat, p.lng], { icon, interactive: true })
          .bindPopup(
            `<strong>${escapeHtml(p.district)}</strong><br>案件數：${p.weight.toLocaleString()} 筆`,
            { maxWidth: 180 }
          )
          .addTo(_fallbackLayer);
      });
      _fallbackLayer.addTo(_map);
    },

    // Called once after all pages loaded: add district bubble markers.
    // Heat layer is already rendered via setHeatmap(); no need to rebuild it here.
    finalizeLoad(allData, mode) {
      if (!_map) return;
      buildDistrictFallbackLayer(allData);
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

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

  // 台北市合理範圍（稍微放寬）— 僅用於 fitBounds 計算，不影響點位渲染
  const TAIPEI_BOUNDS = { minLat: 24.95, maxLat: 25.25, minLng: 121.45, maxLng: 121.75 };

  const CASE_TYPE_EMOJIS = {
    '住宅竊盜':   '🏠',
    '汽車竊盜':   '🚗',
    '機車竊盜':   '🏍️',
    '自行車竊盜': '🚲',
    '搶奪':      '👜',
    '強盜':      '⚡',
  };
  const DEFAULT_EMOJI = '📍';
  const MARKER_BG_COLOR = '#FFFFFF';

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
  let _currentBaseLabel = null;
  let _layerPickerCtrl  = null;

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

  // 是否落在台北市合理範圍內 — 僅用於 fitBounds 計算，不影響點位渲染
  function isWithinTaipei(lat, lng) {
    return lat >= TAIPEI_BOUNDS.minLat && lat <= TAIPEI_BOUNDS.maxLat &&
           lng >= TAIPEI_BOUNDS.minLng && lng <= TAIPEI_BOUNDS.maxLng;
  }

  function emojiForType(caseType) {
    return CASE_TYPE_EMOJIS[caseType] || DEFAULT_EMOJI;
  }

  function buildEmojiIcon(caseType) {
    const emoji = emojiForType(caseType);
    return L.divIcon({
      className: '',
      html: `<div class="emoji-marker" style="background:${MARKER_BG_COLOR};">${emoji}</div>`,
      iconSize:   [32, 32],
      iconAnchor: [16, 16],
    });
  }

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  function buildPopupHtml(item, loading) {
    const placeholder = loading ? '載入中…' : '—';
    return [
      '<div class="crime-popup">',
      `  <strong>${escapeHtml(item.caseType || '未知')}</strong>`,
      '  <table>',
      `    <tr><th>行政區</th><td>${escapeHtml(item.district || placeholder)}</td></tr>`,
      `    <tr><th>日期</th><td>${escapeHtml(item.occurredDate || '—')}</td></tr>`,
      `    <tr><th>時段</th><td>${escapeHtml(item.timeSlot || placeholder)}</td></tr>`,
      `    <tr><th>地點</th><td>${escapeHtml(item.rawLocation || placeholder)}</td></tr>`,
      '  </table>',
      '</div>',
    ].join('\n');
  }

  // ---------------------------------------------------------------------------
  // Popup detail — fetched on demand when a point-mode marker popup is opened
  // ---------------------------------------------------------------------------

  const _detailCache = new Map();

  function attachDetailFetch(marker, item) {
    if (!item.id) return;
    marker.on('popupopen', async () => {
      let detail = _detailCache.get(item.id);
      if (!detail) {
        try {
          const resp = await fetch(`/api/crime/points/${item.id}`, { headers: { Accept: 'application/json' } });
          if (!resp.ok) throw new Error(`API ${resp.status}`);
          detail = await resp.json();
          _detailCache.set(item.id, detail);
        } catch (err) {
          console.error('Popup detail fetch failed:', err);
          detail = { district: '載入失敗', timeSlot: '載入失敗', rawLocation: '載入失敗' };
        }
      }
      marker.setPopupContent(buildPopupHtml(Object.assign({}, item, detail)));
    });
  }

  // ---------------------------------------------------------------------------
  // Layer builders
  // ---------------------------------------------------------------------------

  function buildHeatLayer(data) {
    const points = data.filter(hasCoords).map(i => [i.latitude, i.longitude, HEAT_INTENSITY]);
    _heatLayer = L.heatLayer(points, HEAT_OPTIONS).addTo(_map);
  }

  function buildMarkerLayer(data) {
    _markerLayer = L.markerClusterGroup({ chunkedLoading: true });
    data.filter(hasCoords).forEach(item => {
      const marker = L.marker([item.latitude, item.longitude], {
        icon: buildEmojiIcon(item.caseType),
      });
      marker.bindPopup(buildPopupHtml(item, true), { maxWidth: 260 });
      attachDetailFetch(marker, item);
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
    const entries = [...Object.entries(CASE_TYPE_EMOJIS), ['其他', DEFAULT_EMOJI]];
    const LegendControl = L.Control.extend({
      options: { position: 'bottomright' },
      onAdd() {
        const el = L.DomUtil.create('div', 'crime-legend');
        el.innerHTML =
          '<div class="legend-title">案件類型</div>' +
          entries.map(([label, emoji]) =>
            `<div class="legend-item">` +
            `<span class="legend-emoji" style="background:${MARKER_BG_COLOR};">${emoji}</span>` +
            `<span class="legend-label">${escapeHtml(label)}</span>` +
            `</div>`
          ).join('');
        return el;
      },
    });
    _legendCtrl = new LegendControl();
    _legendCtrl.addTo(_map);
    positionLayerPicker();
  }

  function removeLegend() {
    if (_legendCtrl) { _legendCtrl.remove(); _legendCtrl = null; }
    positionLayerPicker();
  }

  // 讓 .layer-picker 永遠位於 .crime-legend 正上方：
  // bottom = 圖例實際高度 + 12px（圖例不存在時視為高度 0）
  function positionLayerPicker() {
    if (!_layerPickerCtrl) return;
    const pickerEl = _layerPickerCtrl.getContainer();
    if (!pickerEl) return;
    const legendEl = _legendCtrl ? _legendCtrl.getContainer() : null;
    const legendHeight = legendEl ? legendEl.getBoundingClientRect().height : 0;
    pickerEl.style.bottom = (legendHeight + 12) + 'px';
  }

  // ---------------------------------------------------------------------------
  // Layer picker — icon button (bottom-right, above legend) with flyout basemap menu
  // ---------------------------------------------------------------------------

  function switchBaseLayer(label) {
    if (!_baseLayers[label] || label === _currentBaseLabel) return;
    if (_currentBaseLabel && _baseLayers[_currentBaseLabel]) {
      _map.removeLayer(_baseLayers[_currentBaseLabel]);
    }
    _baseLayers[label].addTo(_map);
    _currentBaseLabel = label;
  }

  function addLayerPicker() {
    if (_layerPickerCtrl) return;

    const LayerPickerControl = L.Control.extend({
      options: { position: 'bottomright' },
      onAdd() {
        const container = L.DomUtil.create('div', 'layer-picker');

        const button = L.DomUtil.create('button', 'layer-picker-btn', container);
        button.type = 'button';
        button.setAttribute('aria-label', '切換底圖');
        button.innerHTML =
          '<svg width="20" height="16" viewBox="0 0 20 16">' +
          '<rect x="0" y="0" width="20" height="3" rx="1.5" fill="white"/>' +
          '<rect x="2" y="6" width="16" height="3" rx="1.5" fill="white"/>' +
          '<rect x="4" y="12" width="12" height="3" rx="1.5" fill="white"/>' +
          '</svg>';

        const menu = L.DomUtil.create('div', 'layer-picker-menu', container);
        Object.keys(_baseLayers).forEach(label => {
          const item = L.DomUtil.create('div', 'layer-picker-item', menu);
          item.textContent = label;
          if (label === _currentBaseLabel) item.classList.add('selected');
          L.DomEvent.on(item, 'click', () => {
            switchBaseLayer(label);
            menu.querySelectorAll('.layer-picker-item').forEach(el => {
              el.classList.toggle('selected', el.textContent === label);
            });
            menu.classList.remove('open');
          });
        });

        L.DomEvent.on(button, 'click', (e) => {
          L.DomEvent.stop(e);
          menu.classList.toggle('open');
        });

        // 點擊控制項本身不應觸發地圖點擊或拖曳
        L.DomEvent.disableClickPropagation(container);
        L.DomEvent.disableScrollPropagation(container);

        // 點擊地圖或頁面其他地方時收合選單
        _map.on('click', () => menu.classList.remove('open'));
        document.addEventListener('click', () => menu.classList.remove('open'));

        return container;
      },
    });

    _layerPickerCtrl = new LayerPickerControl();
    _layerPickerCtrl.addTo(_map);
    positionLayerPicker();
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
        const marker = L.marker([item.latitude, item.longitude], {
          icon: buildEmojiIcon(item.caseType),
        });
        marker.bindPopup(buildPopupHtml(item, true), { maxWidth: 260 });
        attachDetailFetch(marker, item);
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

      .crime-legend { position:absolute; right:8px; bottom:10px; background:rgba(30,30,30,.85); color:#ddd; padding:10px 14px; border-radius:6px; font-size:12px; line-height:1.6; box-shadow:0 2px 8px rgba(0,0,0,.5); min-width:110px; }
      .legend-title { font-weight:bold; margin-bottom:6px; font-size:13px; border-bottom:1px solid #555; padding-bottom:4px; }
      .legend-item  { display:flex; align-items:center; gap:6px; margin-bottom:3px; }
      .legend-emoji { display:inline-flex; align-items:center; justify-content:center; width:24px; height:24px; border-radius:50%; flex-shrink:0; font-size:16px; line-height:1; }
      .legend-label { white-space:nowrap; }

      .district-bubble { width:56px; height:56px; border-radius:50%; background:rgba(44,62,80,.88); border:2px solid rgba(255,255,255,.75); display:flex; flex-direction:column; align-items:center; justify-content:center; box-shadow:0 2px 8px rgba(0,0,0,.5); cursor:pointer; transition:transform .15s; }
      .district-bubble:hover { transform:scale(1.12); }
      .db-count { font-size:13px; font-weight:bold; color:#f1c40f; line-height:1.25; }
      .db-name  { font-size:9px; color:rgba(255,255,255,.9); line-height:1.2; text-align:center; }

      .map-progress { background:rgba(30,30,30,.80); color:#fff; padding:6px 12px; border-radius:4px; font-size:13px; font-weight:bold; box-shadow:0 2px 6px rgba(0,0,0,.4); }

      .emoji-marker { width:32px; height:32px; border-radius:50%; display:flex; align-items:center; justify-content:center; font-size:22px; line-height:1; box-shadow:0 1px 4px rgba(0,0,0,.5); }

      /* 手機版：縮放按鈕（左下）與圖例（右下）距離底部 80px（10px 預設邊距 + 70px） */
      @media (max-width: 768px) {
        .leaflet-bottom.leaflet-left,
        .leaflet-bottom.leaflet-right {
          bottom: 70px;
        }
      }

      /* 底圖切換 — 深色圓角按鈕 + 自訂 SVG 疊層圖示，點擊後在按鈕上方浮出選單。
         固定於右下角；bottom 由 positionLayerPicker() 依 .crime-legend 的
         實際高度動態計算（圖例高度 + 12px），此處的 bottom 僅為 JS 執行前的
         初始值。 */
      .layer-picker {
        position: absolute;
        right: 8px;
        bottom: 12px;
      }
      .layer-picker-btn {
        display: flex;
        align-items: center;
        justify-content: center;
        width: 36px;
        height: 36px;
        background: rgba(30,30,30,.85);
        border: none;
        border-radius: 6px;
        box-shadow: 0 2px 8px rgba(0,0,0,.5);
        padding: 0;
        margin: 0;
        cursor: pointer;
      }
      .layer-picker-menu {
        display: none;
        position: absolute;
        bottom: 42px;
        right: 0;
        width: 140px;
        background: rgba(30,30,30,.92);
        border-radius: 6px;
        box-shadow: 0 2px 8px rgba(0,0,0,.5);
        overflow: hidden;
        z-index: 1000;
      }
      .layer-picker-menu.open {
        display: block;
      }
      .layer-picker-item {
        padding: 8px 12px;
        font-size: 13px;
        color: #ddd;
        cursor: pointer;
        white-space: nowrap;
      }
      .layer-picker-item:hover {
        background: rgba(255,255,255,.1);
      }
      .layer-picker-item.selected {
        background: var(--highlight, #e94560);
        color: #fff;
        font-weight: bold;
      }

      /* 手機版：圖例縮小字體，固定於地圖右下角 */
      @media (max-width: 768px) {
        .crime-legend { font-size:11px; padding:4px 8px; min-width:80px; line-height:1.3; }
        .legend-title { font-size:11px; margin-bottom:2px; padding-bottom:2px; }
        .legend-item  { margin-bottom:1px; gap:4px; }
        .legend-emoji { width:14px; height:14px; font-size:10px; }
      }
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

      _map = L.map(containerId, { center: TAIPEI_CENTER, zoom: DEFAULT_ZOOM, zoomControl: false });

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
        if (first) { layer.addTo(_map); _currentBaseLabel = label; first = false; }
      }
      _baseLayers = tileLayers;

      // Zoom control — 桌面版與手機版統一放在地圖左下角
      // （CSS 將桌面版位置固定為距底部 80px、距左側 10px）
      L.control.zoom({ position: 'bottomleft' }).addTo(_map);

      // Basemap switcher — icon button + flyout menu (bottom-right, above legend)
      addLayerPicker();

      // 視窗尺寸改變時重新計算圖層切換按鈕的位置
      window.addEventListener('resize', positionLayerPicker);
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
      if (_fallbackLayer) { _map.removeLayer(_fallbackLayer); _fallbackLayer = null; }
      removeDistrictLabels();

      if (mode === 'point') {
        _markerLayer = L.markerClusterGroup({ chunkedLoading: true });
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

      // Fit the map to the aggregated district points
      // （排除不在台北市合理範圍內的點位，但這些點位仍會在上方正常渲染熱力圖/泡泡）
      const heatCoords = points
        .filter(p => typeof p.lat === 'number' && typeof p.lng === 'number')
        .filter(p => isWithinTaipei(p.lat, p.lng))
        .map(p => [p.lat, p.lng]);
      if (heatCoords.length > 0) {
        _map.fitBounds(L.latLngBounds(heatCoords), { padding: [50, 50], maxZoom: 16 });
      }
    },

    // Called once after all pages loaded: add district bubble markers.
    // Heat layer is already rendered via setHeatmap(); no need to rebuild it here.
    finalizeLoad(allData, mode) {
      if (!_map) return;
      buildDistrictFallbackLayer(allData);

      // Fit the map to all loaded points
      // （排除不在台北市合理範圍內的點位，但這些點位仍會正常顯示在地圖上）
      const coords = (Array.isArray(allData) ? allData : [])
        .filter(hasCoords)
        .filter(i => isWithinTaipei(i.latitude, i.longitude))
        .map(i => [i.latitude, i.longitude]);
      if (coords.length > 0) {
        const bounds = L.latLngBounds(coords);
        console.log('[fitBounds] 即將執行，點位數量：', coords.length, '，bounds：', bounds);
        _map.fitBounds(bounds, { padding: [50, 50], maxZoom: 16 });
        console.log('[fitBounds] 執行完成，目前縮放層級：', _map.getZoom());
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

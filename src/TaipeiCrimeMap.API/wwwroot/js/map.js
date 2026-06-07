/**
 * map.js — Taipei Crime Map, Leaflet.js Map Module
 *
 * Exposes window.mapModule with:
 *   init(containerId)  — initialise Leaflet map
 *   update(data, mode) — render 'heat' (heatmap) or 'point' (circle markers)
 *
 * Dependencies (must be loaded before this file):
 *   - Leaflet       (L)
 *   - Leaflet.heat  (L.heatLayer)
 */

(function () {
  'use strict';

  // ---------------------------------------------------------------------------
  // Constants
  // ---------------------------------------------------------------------------

  const TAIPEI_CENTER = [25.0478, 121.5318];
  const DEFAULT_ZOOM  = 13;

  const TILE_URL = 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png';
  const TILE_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>';

  /** CircleMarker colour per caseType */
  const CASE_TYPE_COLORS = {
    '住宅竊盜':  '#e74c3c',
    '汽車竊盜':  '#e67e22',
    '機車竊盜':  '#f1c40f',
    '自行車竊盜': '#2ecc71',
    '搶奪':     '#9b59b6',
    '強盜':     '#1abc9c',
  };
  const DEFAULT_COLOR = '#95a5a6';

  /** Heatmap options */
  const HEAT_OPTIONS = {
    radius:  20,
    blur:    15,
    maxZoom: 17,
    max:     1.0,
  };

  /** Fixed intensity for each heat point */
  const HEAT_INTENSITY = 0.5;

  // ---------------------------------------------------------------------------
  // Internal state
  // ---------------------------------------------------------------------------

  let _map         = null;   // Leaflet map instance
  let _heatLayer   = null;   // Leaflet.heat layer
  let _markerLayer = null;   // L.layerGroup for CircleMarkers
  let _legendCtrl  = null;   // Leaflet control for legend

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  /**
   * Return true if the crime record has valid coordinates.
   * @param {Object} item - TheftCaseDto
   */
  function hasCoords(item) {
    return (
      typeof item.latitude  === 'number' && !isNaN(item.latitude)  &&
      typeof item.longitude === 'number' && !isNaN(item.longitude)
    );
  }

  /**
   * Return the marker colour for a given caseType string.
   * @param {string} caseType
   */
  function colorForType(caseType) {
    return CASE_TYPE_COLORS[caseType] || DEFAULT_COLOR;
  }

  /**
   * Format a popup HTML string for a TheftCaseDto.
   * @param {Object} item - TheftCaseDto
   */
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

  /** Minimal HTML escaping to avoid XSS in popup content. */
  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  // ---------------------------------------------------------------------------
  // Layer builders
  // ---------------------------------------------------------------------------

  /**
   * Build and add a heat layer from an array of TheftCaseDtos.
   * @param {Object[]} data
   */
  function buildHeatLayer(data) {
    const points = data
      .filter(hasCoords)
      .map(item => [item.latitude, item.longitude, HEAT_INTENSITY]);

    _heatLayer = L.heatLayer(points, HEAT_OPTIONS).addTo(_map);
  }

  /**
   * Build and add a CircleMarker layer from an array of TheftCaseDtos.
   * @param {Object[]} data
   */
  function buildMarkerLayer(data) {
    _markerLayer = L.layerGroup();

    data.filter(hasCoords).forEach(item => {
      const color  = colorForType(item.caseType);
      const marker = L.circleMarker([item.latitude, item.longitude], {
        radius:      6,
        color:       color,
        fillColor:   color,
        fillOpacity: 0.7,
        weight:      1,
      });

      marker.bindPopup(buildPopupHtml(item), {
        maxWidth: 260,
      });

      _markerLayer.addLayer(marker);
    });

    _markerLayer.addTo(_map);
  }

  // ---------------------------------------------------------------------------
  // Legend control
  // ---------------------------------------------------------------------------

  /**
   * Create and add a Leaflet legend control (bottom-right).
   */
  function addLegend() {
    const LegendControl = L.Control.extend({
      options: { position: 'bottomright' },

      onAdd: function () {
        const container = L.DomUtil.create('div', 'crime-legend');
        container.innerHTML = buildLegendHtml();
        return container;
      },
    });

    _legendCtrl = new LegendControl();
    _legendCtrl.addTo(_map);
  }

  /**
   * Remove the legend control from the map (if present).
   */
  function removeLegend() {
    if (_legendCtrl) {
      _legendCtrl.remove();
      _legendCtrl = null;
    }
  }

  /**
   * Build the legend HTML string.
   */
  function buildLegendHtml() {
    const entries = Object.entries(CASE_TYPE_COLORS);
    entries.push(['其他', DEFAULT_COLOR]);

    const items = entries.map(([label, color]) =>
      `<div class="legend-item">` +
      `<span class="legend-dot" style="background:${color};"></span>` +
      `<span class="legend-label">${escapeHtml(label)}</span>` +
      `</div>`
    ).join('');

    return (
      '<div class="legend-title">案件類型</div>' +
      items
    );
  }

  // ---------------------------------------------------------------------------
  // Layer cleanup
  // ---------------------------------------------------------------------------

  /**
   * Remove all crime layers from the map and reset references.
   */
  function clearLayers() {
    if (_heatLayer) {
      _map.removeLayer(_heatLayer);
      _heatLayer = null;
    }
    if (_markerLayer) {
      _map.removeLayer(_markerLayer);
      _markerLayer = null;
    }
    removeLegend();
  }

  // ---------------------------------------------------------------------------
  // District statistics (optional choropleth label overlay)
  // ---------------------------------------------------------------------------

  /**
   * Compute per-district case counts from data.
   * @param {Object[]} data
   * @returns {Object} e.g. { '大安區': 42, '信義區': 17, ... }
   */
  function computeDistrictCounts(data) {
    return data.reduce((acc, item) => {
      if (item.district) {
        acc[item.district] = (acc[item.district] || 0) + 1;
      }
      return acc;
    }, {});
  }

  // ---------------------------------------------------------------------------
  // District label overlay
  // ---------------------------------------------------------------------------

  /** Approximate centroids (lat, lng) for each Taipei district */
  const DISTRICT_CENTROIDS = {
    '松山區': [25.0504, 121.5778],
    '信義區': [25.0326, 121.5697],
    '大安區': [25.0267, 121.5441],
    '中山區': [25.0631, 121.5326],
    '中正區': [25.0430, 121.5197],
    '大同區': [25.0637, 121.5119],
    '萬華區': [25.0355, 121.4993],
    '文山區': [24.9964, 121.5705],
    '南港區': [25.0546, 121.6074],
    '內湖區': [25.0830, 121.5871],
    '士林區': [25.0934, 121.5193],
    '北投區': [25.1319, 121.4986],
  };

  let _districtLabelLayer = null;

  /**
   * Add DivIcon labels showing district name + case count.
   * @param {Object} counts - { districtName: count, ... }
   */
  function addDistrictLabels(counts) {
    _districtLabelLayer = L.layerGroup();

    Object.entries(DISTRICT_CENTROIDS).forEach(([district, latlng]) => {
      const count = counts[district] || 0;
      if (count === 0) return;

      const icon = L.divIcon({
        className: '',
        html:
          `<div class="district-label">` +
          `<div class="district-name">${escapeHtml(district)}</div>` +
          `<div class="district-count">${count}</div>` +
          `</div>`,
        iconAnchor: [40, 20],
        iconSize:   [80, 40],
      });

      L.marker(latlng, { icon, interactive: false }).addTo(_districtLabelLayer);
    });

    _districtLabelLayer.addTo(_map);
  }

  /**
   * Remove district label layer from map.
   */
  function removeDistrictLabels() {
    if (_districtLabelLayer) {
      _map.removeLayer(_districtLabelLayer);
      _districtLabelLayer = null;
    }
  }

  // ---------------------------------------------------------------------------
  // Inject inline CSS (so map.js is self-contained)
  // ---------------------------------------------------------------------------

  function injectStyles() {
    if (document.getElementById('map-module-styles')) return;

    const style = document.createElement('style');
    style.id = 'map-module-styles';
    style.textContent = `
      /* Popup */
      .crime-popup strong {
        font-size: 14px;
        display: block;
        margin-bottom: 6px;
      }
      .crime-popup table {
        border-collapse: collapse;
        font-size: 12px;
        width: 100%;
      }
      .crime-popup th {
        text-align: left;
        padding: 2px 6px 2px 0;
        color: #888;
        white-space: nowrap;
      }
      .crime-popup td {
        padding: 2px 0;
      }

      /* Legend */
      .crime-legend {
        background: rgba(30, 30, 30, 0.85);
        color: #ddd;
        padding: 10px 14px;
        border-radius: 6px;
        font-size: 12px;
        line-height: 1.6;
        box-shadow: 0 2px 8px rgba(0,0,0,0.5);
        min-width: 110px;
      }
      .legend-title {
        font-weight: bold;
        margin-bottom: 6px;
        font-size: 13px;
        border-bottom: 1px solid #555;
        padding-bottom: 4px;
      }
      .legend-item {
        display: flex;
        align-items: center;
        gap: 6px;
        margin-bottom: 3px;
      }
      .legend-dot {
        display: inline-block;
        width: 12px;
        height: 12px;
        border-radius: 50%;
        flex-shrink: 0;
        border: 1px solid rgba(255,255,255,0.25);
      }
      .legend-label {
        white-space: nowrap;
      }

      /* District labels */
      .district-label {
        text-align: center;
        pointer-events: none;
        user-select: none;
      }
      .district-name {
        font-size: 11px;
        font-weight: bold;
        color: #fff;
        text-shadow: 0 0 4px #000, 0 0 4px #000;
        line-height: 1.2;
      }
      .district-count {
        font-size: 13px;
        font-weight: bold;
        color: #f1c40f;
        text-shadow: 0 0 4px #000, 0 0 4px #000;
        line-height: 1.2;
      }
    `;

    document.head.appendChild(style);
  }

  // ---------------------------------------------------------------------------
  // Public API — window.mapModule
  // ---------------------------------------------------------------------------

  const mapModule = {

    /**
     * Initialise the Leaflet map inside the given container element.
     *
     * @param {string} containerId - id of the <div> that will host the map.
     */
    init(containerId) {
      if (_map) {
        // Already initialised — nothing to do.
        return;
      }

      injectStyles();

      _map = L.map(containerId, {
        center:     TAIPEI_CENTER,
        zoom:       DEFAULT_ZOOM,
        zoomControl: true,
      });

      L.tileLayer(TILE_URL, {
        attribution: TILE_ATTRIBUTION,
        subdomains:  'abcd',
        maxZoom:     19,
      }).addTo(_map);
    },

    /**
     * Update the map with new data and switch rendering mode.
     *
     * @param {Object[]} data - Array of TheftCaseDtos.
     * @param {string}   mode - 'heat' | 'point'
     */
    update(data, mode) {
      if (!_map) {
        console.warn('[mapModule] update() called before init().');
        return;
      }

      if (!Array.isArray(data)) {
        console.warn('[mapModule] update() expects an array as first argument.');
        return;
      }

      // Preserve current view
      const center = _map.getCenter();
      const zoom   = _map.getZoom();

      // Clear previous layers
      clearLayers();
      removeDistrictLabels();

      if (mode === 'heat') {
        buildHeatLayer(data);

        // Optionally show district count labels on heatmap
        const counts = computeDistrictCounts(data);
        addDistrictLabels(counts);

      } else if (mode === 'point') {
        buildMarkerLayer(data);
        addLegend();

        // Optionally show district count labels on point map too
        const counts = computeDistrictCounts(data);
        addDistrictLabels(counts);

      } else {
        console.warn(`[mapModule] Unknown mode "${mode}". Use 'heat' or 'point'.`);
      }

      // Restore view (setView with no animation avoids jarring pan/zoom)
      _map.setView(center, zoom, { animate: false });
    },

    /**
     * Expose the underlying Leaflet map instance (for advanced usage).
     */
    get instance() {
      return _map;
    },
  };

  // Attach to global scope
  window.mapModule = mapModule;

})();

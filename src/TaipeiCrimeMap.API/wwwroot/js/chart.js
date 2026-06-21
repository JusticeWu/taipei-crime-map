/**
 * chart.js - Chart.js 統計圖表模組（深色主題）
 * 暴露 window.chartModule，提供 init() 與 update(stats) 介面
 */
(function () {
  'use strict';

  // ── 顏色常數 ────────────────────────────────────────────────────────────────
  const THEME = {
    background: '#16213e',
    text:       '#e0e0e0',
    grid:       'rgba(255,255,255,0.1)',
    fontSize:   12,
  };

  const DISTRICT_BAR_COLOR = '#4fc3f7';
  const TIMESLOT_BAR_COLOR = '#f1c40f';

  const TREND_COLORS = [
    '#e74c3c', '#3498db', '#2ecc71', '#f39c12', '#9b59b6',
  ];

  // ── 圖表實例 ─────────────────────────────────────────────────────────────────
  let districtChart = null;
  let timeSlotChart = null;
  let tsCaseTypeTrendChart = null;
  let distTsTrendChart = null;
  let distCaseTypeTrendChart = null;

  // ── 共用 Chart.js 預設設定工廠 ───────────────────────────────────────────────
  function baseScales() {
    return {
      x: {
        ticks: { color: THEME.text, font: { size: THEME.fontSize } },
        grid:  { color: THEME.grid },
      },
      y: {
        ticks: { color: THEME.text, font: { size: THEME.fontSize } },
        grid:  { color: THEME.grid },
        beginAtZero: true,
      },
    };
  }

  function basePlugins(titleText) {
    return {
      legend: {
        display: false,
      },
      title: {
        display: true,
        text:    titleText,
        color:   THEME.text,
        font:    { size: THEME.fontSize + 2 },
      },
      tooltip: {
        titleColor: THEME.text,
        bodyColor:  THEME.text,
        backgroundColor: '#0f3460',
      },
    };
  }

  function trendPlugins(titleText) {
    return {
      legend: {
        display: true,
        position: 'bottom',
        labels: {
          color: THEME.text,
          font: { size: THEME.fontSize - 1 },
          boxWidth: 12,
          padding: 8,
        },
      },
      title: {
        display: true,
        text:    titleText,
        color:   THEME.text,
        font:    { size: THEME.fontSize + 2 },
      },
      tooltip: {
        titleColor: THEME.text,
        bodyColor:  THEME.text,
        backgroundColor: '#0f3460',
      },
    };
  }

  // ── 安全地銷毀既有圖表 ────────────────────────────────────────────────────────
  function destroyChart(instance) {
    if (instance) {
      instance.destroy();
    }
    return null;
  }

  // ── 圖表建立函式 ─────────────────────────────────────────────────────────────

  function buildDistrictChart(labels, counts) {
    var canvas = document.getElementById('chart-district');
    if (!canvas) return null;
    return new Chart(canvas.getContext('2d'), {
      type: 'bar',
      data: {
        labels: labels,
        datasets: [{
          label: '案件數', data: counts,
          backgroundColor: DISTRICT_BAR_COLOR, borderColor: DISTRICT_BAR_COLOR, borderWidth: 1,
        }],
      },
      options: {
        indexAxis: 'y', responsive: true, maintainAspectRatio: false,
        plugins: basePlugins('行政區分布'), scales: baseScales(),
      },
    });
  }

  function buildTimeSlotChart(labels, counts) {
    var canvas = document.getElementById('chart-timeslot');
    if (!canvas) return null;
    return new Chart(canvas.getContext('2d'), {
      type: 'bar',
      data: {
        labels: labels,
        datasets: [{
          label: '案件數', data: counts,
          backgroundColor: TIMESLOT_BAR_COLOR, borderColor: TIMESLOT_BAR_COLOR, borderWidth: 1,
        }],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: basePlugins('時段分布'), scales: baseScales(),
      },
    });
  }

  function buildTrendChart(canvasId, titleText, trendSeries) {
    var canvas = document.getElementById(canvasId);
    if (!canvas) return null;

    var allYears = new Set();
    trendSeries.forEach(function (s) {
      s.points.forEach(function (p) { allYears.add(p.year); });
    });
    var years = Array.from(allYears).sort(function (a, b) { return a - b; });

    var datasets = trendSeries.map(function (s, i) {
      var color = TREND_COLORS[i % TREND_COLORS.length];
      var yearMap = {};
      s.points.forEach(function (p) { yearMap[p.year] = p.count; });
      return {
        label: s.label,
        data: years.map(function (y) { return yearMap[y] || 0; }),
        borderColor: color,
        backgroundColor: color + '33',
        tension: 0.3,
        pointRadius: 3,
        borderWidth: 2,
        fill: false,
      };
    });

    return new Chart(canvas.getContext('2d'), {
      type: 'line',
      data: { labels: years.map(String), datasets: datasets },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: trendPlugins(titleText),
        scales: baseScales(),
      },
    });
  }

  // ── 無資料時顯示提示文字 ──────────────────────────────────────────────────────
  function showNoData(canvasId) {
    var canvas = document.getElementById(canvasId);
    if (!canvas) return;
    var ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = THEME.background;
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = THEME.text;
    ctx.font = '14px sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('無資料', canvas.width / 2, canvas.height / 2);
  }

  // ── 公開 API ─────────────────────────────────────────────────────────────────

  var chartModule = {
    init: function () {
      districtChart = destroyChart(districtChart);
      timeSlotChart = destroyChart(timeSlotChart);
      tsCaseTypeTrendChart = destroyChart(tsCaseTypeTrendChart);
      distTsTrendChart = destroyChart(distTsTrendChart);
      distCaseTypeTrendChart = destroyChart(distCaseTypeTrendChart);

      districtChart = buildDistrictChart([], []);
      timeSlotChart = buildTimeSlotChart([], []);
    },

    update: function (stats) {
      districtChart = destroyChart(districtChart);
      timeSlotChart = destroyChart(timeSlotChart);
      tsCaseTypeTrendChart = destroyChart(tsCaseTypeTrendChart);
      distTsTrendChart = destroyChart(distTsTrendChart);
      distCaseTypeTrendChart = destroyChart(distCaseTypeTrendChart);

      var dd = (stats && stats.districtDistribution) || [];
      var td = (stats && stats.timeSlotDistribution) || [];
      var t1 = (stats && stats.timeSlotCaseTypeTrend) || [];
      var t2 = (stats && stats.districtTimeSlotTrend) || [];
      var t3 = (stats && stats.districtCaseTypeTrend) || [];

      if (t1.length === 0) { showNoData('chart-timeslot-casetype-trend'); }
      else { tsCaseTypeTrendChart = buildTrendChart('chart-timeslot-casetype-trend', '時段．案類趨勢', t1); }

      if (t2.length === 0) { showNoData('chart-district-timeslot-trend'); }
      else { distTsTrendChart = buildTrendChart('chart-district-timeslot-trend', '行政區．時段趨勢', t2); }

      if (t3.length === 0) { showNoData('chart-district-casetype-trend'); }
      else { distCaseTypeTrendChart = buildTrendChart('chart-district-casetype-trend', '行政區．案類趨勢', t3); }

      if (dd.length === 0) { showNoData('chart-district'); }
      else {
        districtChart = buildDistrictChart(
          dd.map(function (e) { return e.district; }),
          dd.map(function (e) { return e.count; }));
      }

      if (td.length === 0) { showNoData('chart-timeslot'); }
      else {
        timeSlotChart = buildTimeSlotChart(
          td.map(function (e) { return e.timeSlot; }),
          td.map(function (e) { return e.count; }));
      }
    },
  };

  window.chartModule = chartModule;
}());

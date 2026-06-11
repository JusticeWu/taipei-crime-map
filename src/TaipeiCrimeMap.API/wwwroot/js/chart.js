/**
 * chart.js - Chart.js 統計圖表模組（深色主題）
 * 暴露 window.chartModule，提供 init() 與 update(stats) 介面
 * stats 為 /api/crime/stats 回傳的 CrimeStatsDto：
 *   { districtDistribution: [{ district, count }], timeSlotDistribution: [{ timeSlot, count }] }
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

  // ── 圖表實例 ─────────────────────────────────────────────────────────────────
  let districtChart = null;
  let timeSlotChart = null;

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

  // ── 安全地銷毀既有圖表 ────────────────────────────────────────────────────────
  function destroyChart(instance) {
    if (instance) {
      instance.destroy();
    }
    return null;
  }

  // ── 圖表建立函式 ─────────────────────────────────────────────────────────────

  /**
   * 建立（或重建）行政區分布橫向長條圖
   */
  function buildDistrictChart(labels, counts) {
    const canvas = document.getElementById('chart-district');
    if (!canvas) {
      console.warn('[chartModule] canvas#chart-district not found');
      return null;
    }

    const ctx = canvas.getContext('2d');

    return new Chart(ctx, {
      type: 'bar',
      data: {
        labels: labels,
        datasets: [{
          label:           '案件數',
          data:            counts,
          backgroundColor: DISTRICT_BAR_COLOR,
          borderColor:     DISTRICT_BAR_COLOR,
          borderWidth:     1,
        }],
      },
      options: {
        indexAxis:           'y',
        responsive:          true,
        maintainAspectRatio: false,
        plugins: basePlugins('行政區分布'),
        scales:  baseScales(),
      },
    });
  }

  /**
   * 建立（或重建）時段分布長條圖
   */
  function buildTimeSlotChart(labels, counts) {
    const canvas = document.getElementById('chart-timeslot');
    if (!canvas) {
      console.warn('[chartModule] canvas#chart-timeslot not found');
      return null;
    }

    const ctx = canvas.getContext('2d');

    return new Chart(ctx, {
      type: 'bar',
      data: {
        labels: labels,
        datasets: [{
          label:           '案件數',
          data:            counts,
          backgroundColor: TIMESLOT_BAR_COLOR,
          borderColor:     TIMESLOT_BAR_COLOR,
          borderWidth:     1,
        }],
      },
      options: {
        responsive:          true,
        maintainAspectRatio: false,
        plugins: basePlugins('時段分布'),
        scales:  baseScales(),
      },
    });
  }

  // ── 無資料時顯示提示文字 ──────────────────────────────────────────────────────
  function showNoData(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
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

  const chartModule = {
    /**
     * init() — 初始化，建立空白圖表（資料為空）
     */
    init: function () {
      districtChart = destroyChart(districtChart);
      timeSlotChart = destroyChart(timeSlotChart);

      districtChart = buildDistrictChart([], []);
      timeSlotChart = buildTimeSlotChart([], []);
    },

    /**
     * update(stats) — 依 /api/crime/stats 回傳的 CrimeStatsDto 重新渲染圖表
     * @param {Object} stats - { districtDistribution: [{district, count}], timeSlotDistribution: [{timeSlot, count}] }
     */
    update: function (stats) {
      // 銷毀舊圖表（避免 Canvas reuse 警告）
      districtChart = destroyChart(districtChart);
      timeSlotChart = destroyChart(timeSlotChart);

      const districtDistribution = (stats && stats.districtDistribution) || [];
      const timeSlotDistribution = (stats && stats.timeSlotDistribution) || [];

      if (districtDistribution.length === 0) {
        showNoData('chart-district');
      } else {
        districtChart = buildDistrictChart(
          districtDistribution.map(function (e) { return e.district; }),
          districtDistribution.map(function (e) { return e.count; })
        );
      }

      if (timeSlotDistribution.length === 0) {
        showNoData('chart-timeslot');
      } else {
        timeSlotChart = buildTimeSlotChart(
          timeSlotDistribution.map(function (e) { return e.timeSlot; }),
          timeSlotDistribution.map(function (e) { return e.count; })
        );
      }
    },
  };

  // 掛載到全域
  window.chartModule = chartModule;
}());

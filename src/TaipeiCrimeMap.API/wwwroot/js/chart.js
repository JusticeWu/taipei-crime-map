/**
 * chart.js - Chart.js 統計圖表模組（深色主題）
 * 暴露 window.chartModule，提供 init() 與 update(data) 介面
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

  const CASE_TYPE_COLORS = {
    '住宅竊盜':   '#1E8449',
    '強盜':      '#E67E22',
    '搶奪':      '#D4A017',
    '汽車竊盜':   '#16A085',
    '機車竊盜':   '#1A5276',
    '自行車竊盜': '#8E44AD',
  };

  const CASE_TYPE_ORDER = ['住宅竊盜', '汽車竊盜', '機車竊盜', '自行車竊盜', '搶奪', '強盜'];

  // ── 圖表實例 ─────────────────────────────────────────────────────────────────
  let trendChart   = null;
  let typeBarChart = null;

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
        labels: { color: THEME.text, font: { size: THEME.fontSize } },
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

  // ── 統計工具函式 ─────────────────────────────────────────────────────────────

  /**
   * 從 TheftCaseDto[] 統計每年案件數，回傳 { labels: string[], counts: number[] }
   */
  function aggregateByYear(data) {
    const map = {};
    data.forEach(function (item) {
      if (!item.occurredDate) return;
      const year = String(item.occurredDate).substring(0, 4);
      if (!year || year.length !== 4) return;
      map[year] = (map[year] || 0) + 1;
    });

    const labels = Object.keys(map).sort();
    const counts  = labels.map(function (y) { return map[y]; });
    return { labels, counts };
  }

  /**
   * 從 TheftCaseDto[] 統計各案類案件數，依 CASE_TYPE_ORDER 排序
   * 回傳 { labels: string[], counts: number[], colors: string[] }
   */
  function aggregateByCaseType(data) {
    const map = {};
    data.forEach(function (item) {
      if (!item.caseType) return;
      map[item.caseType] = (map[item.caseType] || 0) + 1;
    });

    const labels = [];
    const counts  = [];
    const colors  = [];

    CASE_TYPE_ORDER.forEach(function (type) {
      labels.push(type);
      counts.push(map[type] || 0);
      colors.push(CASE_TYPE_COLORS[type] || '#999999');
    });

    return { labels, counts, colors };
  }

  // ── 圖表建立函式 ─────────────────────────────────────────────────────────────

  /**
   * 建立（或重建）年度趨勢折線圖
   */
  function buildTrendChart(labels, counts) {
    const canvas = document.getElementById('chart-trend');
    if (!canvas) {
      console.warn('[chartModule] canvas#chart-trend not found');
      return null;
    }

    const ctx = canvas.getContext('2d');

    return new Chart(ctx, {
      type: 'line',
      data: {
        labels: labels,
        datasets: [{
          label:                '案件數',
          data:                 counts,
          borderColor:          '#4fc3f7',
          backgroundColor:      'rgba(79, 195, 247, 0.15)',
          fill:                 true,
          tension:              0.3,
          borderWidth:          2,
          pointRadius:          5,
          pointHoverRadius:     7,
          pointBackgroundColor: '#4fc3f7',
          pointBorderColor:     '#ffffff',
          pointBorderWidth:     1,
        }],
      },
      options: {
        responsive:          true,
        maintainAspectRatio: false,
        plugins: Object.assign(basePlugins('年度案件趨勢'), {
          tooltip: {
            titleColor:      THEME.text,
            bodyColor:       THEME.text,
            backgroundColor: '#0f3460',
            callbacks: {
              title: function (items) {
                return '年份：' + items[0].label;
              },
              label: function (item) {
                return '案件數：' + item.parsed.y;
              },
            },
          },
        }),
        scales: baseScales(),
      },
    });
  }

  /**
   * 建立（或重建）案類分佈長條圖
   */
  function buildTypeBarChart(labels, counts, colors) {
    const canvas = document.getElementById('chart-type-bar');
    if (!canvas) {
      console.warn('[chartModule] canvas#chart-type-bar not found');
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
          backgroundColor: colors,
          borderColor:     colors,
          borderWidth:     1,
        }],
      },
      options: {
        responsive:          true,
        maintainAspectRatio: false,
        plugins: basePlugins('案類分佈'),
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
      trendChart   = destroyChart(trendChart);
      typeBarChart = destroyChart(typeBarChart);

      trendChart   = buildTrendChart([], []);
      typeBarChart = buildTypeBarChart(
        CASE_TYPE_ORDER,
        CASE_TYPE_ORDER.map(function () { return 0; }),
        CASE_TYPE_ORDER.map(function (t) { return CASE_TYPE_COLORS[t]; })
      );
    },

    /**
     * update(data) — 依 TheftCaseDto[] 重新渲染所有圖表
     * @param {Array} data - TheftCaseDto 陣列
     */
    update: function (data) {
      // 銷毀舊圖表（避免 Canvas reuse 警告）
      trendChart   = destroyChart(trendChart);
      typeBarChart = destroyChart(typeBarChart);

      if (!data || data.length === 0) {
        showNoData('chart-trend');
        showNoData('chart-type-bar');
        return;
      }

      // 統計資料
      const yearStats    = aggregateByYear(data);
      const caseTypeStats = aggregateByCaseType(data);

      // 重建圖表
      trendChart   = buildTrendChart(yearStats.labels, yearStats.counts);
      typeBarChart = buildTypeBarChart(
        caseTypeStats.labels,
        caseTypeStats.counts,
        caseTypeStats.colors
      );
    },
  };

  // 掛載到全域
  window.chartModule = chartModule;
}());

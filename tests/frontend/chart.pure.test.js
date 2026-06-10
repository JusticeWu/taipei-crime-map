'use strict';

/**
 * Pure-function unit tests for chart.js aggregation logic.
 *
 * These functions are extracted / replicated here for isolated testing.
 * Any change to the corresponding logic in chart.js must be reflected here.
 */

// ── Functions under test ────────────────────────────────────────────────────

function aggregateByDistrict(data) {
  const map = {};
  data.forEach(function (item) {
    const district = item.district || item.districtName;
    if (!district) return;
    map[district] = (map[district] || 0) + 1;
  });

  const entries = Object.entries(map).sort(function (a, b) { return b[1] - a[1]; });
  return {
    labels: entries.map(function (e) { return e[0]; }),
    counts: entries.map(function (e) { return e[1]; }),
  };
}

function aggregateByTimeSlot(data) {
  const map = {};
  data.forEach(function (item) {
    if (!item.timeSlot) return;
    map[item.timeSlot] = (map[item.timeSlot] || 0) + 1;
  });

  const labels = Object.keys(map).sort();
  const counts = labels.map(function (t) { return map[t]; });
  return { labels, counts };
}

// ── aggregateByDistrict ─────────────────────────────────────────────────────

describe('aggregateByDistrict', () => {
  test('依案件數由多到少排序', () => {
    const data = [
      { district: '大安區' },
      { district: '信義區' },
      { district: '大安區' },
      { district: '信義區' },
      { district: '大安區' },
    ];
    expect(aggregateByDistrict(data)).toEqual({
      labels: ['大安區', '信義區'],
      counts: [3, 2],
    });
  });

  test('支援 districtName 欄位', () => {
    const data = [{ districtName: '中正區' }, { districtName: '中正區' }];
    expect(aggregateByDistrict(data)).toEqual({
      labels: ['中正區'],
      counts: [2],
    });
  });

  test('忽略缺少行政區的資料', () => {
    const data = [{ district: '大安區' }, {}, { district: null }];
    expect(aggregateByDistrict(data)).toEqual({
      labels: ['大安區'],
      counts: [1],
    });
  });

  test('空陣列 → 空結果', () => {
    expect(aggregateByDistrict([])).toEqual({ labels: [], counts: [] });
  });
});

// ── aggregateByTimeSlot ─────────────────────────────────────────────────────

describe('aggregateByTimeSlot', () => {
  test('依時段（HH~HH）由小到大排序', () => {
    const data = [
      { timeSlot: '14~16' },
      { timeSlot: '00~02' },
      { timeSlot: '00~02' },
      { timeSlot: '08~10' },
    ];
    expect(aggregateByTimeSlot(data)).toEqual({
      labels: ['00~02', '08~10', '14~16'],
      counts: [2, 1, 1],
    });
  });

  test('忽略缺少時段的資料', () => {
    const data = [{ timeSlot: '00~02' }, {}, { timeSlot: null }];
    expect(aggregateByTimeSlot(data)).toEqual({
      labels: ['00~02'],
      counts: [1],
    });
  });

  test('空陣列 → 空結果', () => {
    expect(aggregateByTimeSlot([])).toEqual({ labels: [], counts: [] });
  });
});

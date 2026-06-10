'use strict';

/**
 * Pure-function unit tests for map.js logic.
 *
 * These functions are extracted / replicated here for isolated testing.
 * Any change to the corresponding logic in map.js must be reflected here.
 */

// ── Functions under test ────────────────────────────────────────────────────

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

function getCaseTypeColor(caseType) {
  const name = CASE_TYPE_ID_TO_NAME[caseType] || caseType;
  return CASE_TYPE_COLORS[name] || DEFAULT_COLOR;
}

function getClusterColor(caseTypes) {
  if (!Array.isArray(caseTypes) || caseTypes.length === 0) return MIXED_CLUSTER_COLOR;
  const colors = new Set(caseTypes.map(getCaseTypeColor));
  return colors.size === 1 ? [...colors][0] : MIXED_CLUSTER_COLOR;
}

function getClusterTextColor(backgroundColor) {
  return String(backgroundColor).toUpperCase() === '#D4A017' ? DARK_TEXT_COLOR : LIGHT_TEXT_COLOR;
}

// ── getCaseTypeColor ────────────────────────────────────────────────────────

describe('getCaseTypeColor', () => {
  test('numeric 1 (住宅竊盜) → 綠色', () => {
    expect(getCaseTypeColor(1)).toBe('#1E8449');
  });

  test('numeric 2 (汽車竊盜) → 藍綠色', () => {
    expect(getCaseTypeColor(2)).toBe('#16A085');
  });

  test('numeric 3 (機車竊盜) → 藍色', () => {
    expect(getCaseTypeColor(3)).toBe('#1A5276');
  });

  test('numeric 4 (自行車竊盜) → 紫色', () => {
    expect(getCaseTypeColor(4)).toBe('#8E44AD');
  });

  test('numeric 5 (搶奪) → 深黃色', () => {
    expect(getCaseTypeColor(5)).toBe('#D4A017');
  });

  test('numeric 6 (強盜) → 橙色', () => {
    expect(getCaseTypeColor(6)).toBe('#E67E22');
  });

  test('中文字串 "住宅竊盜" → 綠色', () => {
    expect(getCaseTypeColor('住宅竊盜')).toBe('#1E8449');
  });

  test('中文字串 "機車竊盜" → 藍色', () => {
    expect(getCaseTypeColor('機車竊盜')).toBe('#1A5276');
  });

  test('未知數字代碼 999 → 灰色 fallback', () => {
    expect(getCaseTypeColor(999)).toBe('#95A5A6');
  });

  test('未知字串 → 灰色 fallback', () => {
    expect(getCaseTypeColor('未知案類')).toBe('#95A5A6');
  });

  test('null / undefined → 灰色 fallback', () => {
    expect(getCaseTypeColor(null)).toBe('#95A5A6');
    expect(getCaseTypeColor(undefined)).toBe('#95A5A6');
  });
});

// ── getClusterColor ─────────────────────────────────────────────────────────

describe('getClusterColor', () => {
  test('全部同一案類（數字）→ 該案類顏色', () => {
    expect(getClusterColor([3, 3, 3])).toBe('#1A5276');
  });

  test('全部同一案類（中文字串）→ 該案類顏色', () => {
    expect(getClusterColor(['住宅竊盜', '住宅竊盜'])).toBe('#1E8449');
  });

  test('包含多種案類 → 紅色 MIXED_CLUSTER_COLOR', () => {
    expect(getClusterColor([1, 2, 3])).toBe('#C0392B');
  });

  test('混合中文與數字但代表不同案類 → 紅色', () => {
    expect(getClusterColor(['住宅竊盜', 3])).toBe('#C0392B');
  });

  test('空陣列 → 紅色 fallback', () => {
    expect(getClusterColor([])).toBe('#C0392B');
  });

  test('單一元素 → 該案類顏色', () => {
    expect(getClusterColor([5])).toBe('#D4A017');
  });
});

// ── getClusterTextColor ─────────────────────────────────────────────────────

describe('getClusterTextColor', () => {
  test('搶奪深黃色背景 → 深色文字', () => {
    expect(getClusterTextColor('#D4A017')).toBe('#333333');
  });

  test('搶奪深黃色背景（小寫）→ 深色文字', () => {
    expect(getClusterTextColor('#d4a017')).toBe('#333333');
  });

  test('其他背景顏色 → 白色文字', () => {
    expect(getClusterTextColor('#1E8449')).toBe('#FFFFFF');
    expect(getClusterTextColor('#C0392B')).toBe('#FFFFFF');
    expect(getClusterTextColor('#95A5A6')).toBe('#FFFFFF');
  });
});

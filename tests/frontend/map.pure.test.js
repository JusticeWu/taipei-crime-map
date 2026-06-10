'use strict';

/**
 * Pure-function unit tests for map.js logic.
 *
 * These functions are extracted / replicated here for isolated testing.
 * Any change to the corresponding logic in map.js must be reflected here.
 */

// ── Functions under test ────────────────────────────────────────────────────

// 依嚴重性排序：住宅竊盜（紅）> 強盜（橙）> 搶奪（黃）> 汽車竊盜（綠）> 機車竊盜（青）> 自行車竊盜（紫）
const CASE_TYPE_COLORS = {
  '住宅竊盜':   '#E74C3C',
  '強盜':      '#E67E22',
  '搶奪':      '#F1C40F',
  '汽車竊盜':   '#27AE60',
  '機車竊盜':   '#1ABC9C',
  '自行車竊盜': '#9B59B6',
};
const DEFAULT_COLOR = '#95A5A6';

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

// ── getCaseTypeColor ────────────────────────────────────────────────────────

describe('getCaseTypeColor', () => {
  test('numeric 1 (住宅竊盜) → 紅色', () => {
    expect(getCaseTypeColor(1)).toBe('#E74C3C');
  });

  test('numeric 2 (汽車竊盜) → 綠色', () => {
    expect(getCaseTypeColor(2)).toBe('#27AE60');
  });

  test('numeric 3 (機車竊盜) → 青色', () => {
    expect(getCaseTypeColor(3)).toBe('#1ABC9C');
  });

  test('numeric 4 (自行車竊盜) → 紫色', () => {
    expect(getCaseTypeColor(4)).toBe('#9B59B6');
  });

  test('numeric 5 (搶奪) → 黃色', () => {
    expect(getCaseTypeColor(5)).toBe('#F1C40F');
  });

  test('numeric 6 (強盜) → 橙色', () => {
    expect(getCaseTypeColor(6)).toBe('#E67E22');
  });

  test('中文字串 "住宅竊盜" → 紅色', () => {
    expect(getCaseTypeColor('住宅竊盜')).toBe('#E74C3C');
  });

  test('中文字串 "機車竊盜" → 青色', () => {
    expect(getCaseTypeColor('機車竊盜')).toBe('#1ABC9C');
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

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
  '汽車竊盜':   '#0E6655',
  '機車竊盜':   '#2471A3',
  '自行車竊盜': '#A569BD',
  '搶奪':      '#B7950B',
  '強盜':      '#CA6F1E',
};
const DEFAULT_COLOR = '#95A5A6';

// MarkerCluster 群聚內包含多種案類時使用的顏色
const MIXED_CLUSTER_COLOR = '#C0392B';

// 淺色背景（如搶奪的深黃 #B7950B）需要深色文字才能清楚閱讀
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
  return String(backgroundColor).toUpperCase() === '#B7950B' ? DARK_TEXT_COLOR : LIGHT_TEXT_COLOR;
}

// 點位圓點半徑與半透明光暈設定
const MARKER_RADIUS = 6;
const MARKER_HALO_BLUR = 5;
const MARKER_HALO_OPACITY = 0.4;

function hexToRgba(hex, alpha) {
  const m = String(hex).replace('#', '');
  const r = parseInt(m.substring(0, 2), 16);
  const g = parseInt(m.substring(2, 4), 16);
  const b = parseInt(m.substring(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function haloClassName(color) {
  return 'point-halo-' + String(color).replace('#', '').toLowerCase();
}

function buildPointMarkerOptions(item) {
  const color = getCaseTypeColor(item.caseType);
  return {
    radius: MARKER_RADIUS,
    fillColor: color,
    fillOpacity: 0.85,
    color,
    weight: 1,
    opacity: 1,
    className: haloClassName(color),
    caseType: item.caseType,
  };
}

function buildPointHaloCss() {
  const colors = [...new Set([...Object.values(CASE_TYPE_COLORS), DEFAULT_COLOR])];
  return colors.map(color => {
    const shadow = hexToRgba(color, MARKER_HALO_OPACITY);
    return `.${haloClassName(color)} { filter: drop-shadow(0 0 ${MARKER_HALO_BLUR}px ${shadow}); }`;
  }).join('\n');
}

// ── getCaseTypeColor ────────────────────────────────────────────────────────

describe('getCaseTypeColor', () => {
  test('numeric 1 (住宅竊盜) → 綠色', () => {
    expect(getCaseTypeColor(1)).toBe('#1E8449');
  });

  test('numeric 2 (汽車竊盜) → 藍綠色', () => {
    expect(getCaseTypeColor(2)).toBe('#0E6655');
  });

  test('numeric 3 (機車竊盜) → 藍色', () => {
    expect(getCaseTypeColor(3)).toBe('#2471A3');
  });

  test('numeric 4 (自行車竊盜) → 紫色', () => {
    expect(getCaseTypeColor(4)).toBe('#A569BD');
  });

  test('numeric 5 (搶奪) → 深黃色', () => {
    expect(getCaseTypeColor(5)).toBe('#B7950B');
  });

  test('numeric 6 (強盜) → 橙色', () => {
    expect(getCaseTypeColor(6)).toBe('#CA6F1E');
  });

  test('中文字串 "住宅竊盜" → 綠色', () => {
    expect(getCaseTypeColor('住宅竊盜')).toBe('#1E8449');
  });

  test('中文字串 "機車竊盜" → 藍色', () => {
    expect(getCaseTypeColor('機車竊盜')).toBe('#2471A3');
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
    expect(getClusterColor([3, 3, 3])).toBe('#2471A3');
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
    expect(getClusterColor([5])).toBe('#B7950B');
  });
});

// ── getClusterTextColor ─────────────────────────────────────────────────────

describe('getClusterTextColor', () => {
  test('搶奪深黃色背景 → 深色文字', () => {
    expect(getClusterTextColor('#B7950B')).toBe('#333333');
  });

  test('搶奪深黃色背景（小寫）→ 深色文字', () => {
    expect(getClusterTextColor('#b7950b')).toBe('#333333');
  });

  test('其他背景顏色 → 白色文字', () => {
    expect(getClusterTextColor('#1E8449')).toBe('#FFFFFF');
    expect(getClusterTextColor('#C0392B')).toBe('#FFFFFF');
    expect(getClusterTextColor('#95A5A6')).toBe('#FFFFFF');
  });
});

// ── buildPointMarkerOptions（光暈效果）────────────────────────────────────────

describe('buildPointMarkerOptions', () => {
  test('circleMarker 本體維持正常樣式，光暈交由 CSS class 處理', () => {
    const options = buildPointMarkerOptions({ caseType: '住宅竊盜' });
    expect(options.color).toBe('#1E8449');
    expect(options.fillColor).toBe('#1E8449');
    expect(options.radius).toBe(6);
    expect(options.weight).toBe(1);
    expect(options.opacity).toBe(1);
    expect(options.fillOpacity).toBeCloseTo(0.85);
    expect(options.className).toBe('point-halo-1e8449');
  });

  test('光暈 class 名稱依案類顏色而異', () => {
    const a = buildPointMarkerOptions({ caseType: '搶奪' });
    const b = buildPointMarkerOptions({ caseType: '機車竊盜' });
    expect(a.className).toBe('point-halo-b7950b');
    expect(b.className).toBe('point-halo-2471a3');
    expect(a.className).not.toBe(b.className);
  });
});

// ── buildPointHaloCss（光暈 CSS，filter: drop-shadow）─────────────────────────

describe('buildPointHaloCss', () => {
  const css = buildPointHaloCss();

  test('每個案類顏色都產生對應的 drop-shadow class', () => {
    Object.values(CASE_TYPE_COLORS).forEach(color => {
      expect(css).toContain(`.${haloClassName(color)}`);
    });
    expect(css).toContain(`.${haloClassName(DEFAULT_COLOR)}`);
  });

  test('使用 filter: drop-shadow 而非 box-shadow（避免 SVG path 方形問題）', () => {
    expect(css).toContain('filter: drop-shadow(');
    expect(css).not.toContain('box-shadow');
  });

  test('光暈模糊半徑為 5px（落在 4~6px 範圍）且透明度為同色 0.4', () => {
    const expectedShadow = hexToRgba('#1E8449', MARKER_HALO_OPACITY);
    expect(css).toContain(`drop-shadow(0 0 ${MARKER_HALO_BLUR}px ${expectedShadow})`);
    expect(MARKER_HALO_BLUR).toBeGreaterThanOrEqual(4);
    expect(MARKER_HALO_BLUR).toBeLessThanOrEqual(6);
  });
});

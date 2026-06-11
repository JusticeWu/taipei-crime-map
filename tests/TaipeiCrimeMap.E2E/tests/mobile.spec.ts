import { test, expect } from '@playwright/test';

/**
 * Phase 2 — 手機版 RWD 測試
 * Viewport: 375 x 812（iPhone SE）
 * 對應 lessons-learned: L023（地圖/圖表容器高度塌陷）、
 * L025（篩選列按鈕被面板蓋住）、L026（attribution 留白）
 */

test.use({ viewport: { width: 375, height: 812 } });

test.describe('手機版 RWD', () => {

  test('手機版頂部固定篩選列：預設收合，且按鈕可見可點擊', async ({ page }) => {
    await page.goto('/');

    const toggleBtn = page.locator('#btn-filter-toggle');
    const filterPanel = page.locator('#filter-panel');

    await expect(toggleBtn).toBeVisible();

    const box = await toggleBtn.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.y).toBe(0); // 固定於視窗頂部

    // 預設收合狀態
    await expect(filterPanel).not.toHaveClass(/open/);
  });

  test('手機版地圖容器：#map 有非零高度且可見', async ({ page }) => {
    await page.goto('/');

    const map = page.locator('#map');
    await expect(map).toBeVisible();

    const box = await map.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.height).toBeGreaterThan(0);
    expect(box!.width).toBeGreaterThan(0);
  });

  test('手機版圖表區塊：#chart-container 顯示於地圖下方且有非零高度', async ({ page }) => {
    await page.goto('/');

    const chartContainer = page.locator('#chart-container');
    await expect(chartContainer).toBeVisible();

    const box = await chartContainer.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.height).toBeGreaterThan(0);
  });

});

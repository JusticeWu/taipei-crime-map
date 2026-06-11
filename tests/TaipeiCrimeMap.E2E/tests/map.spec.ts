import { test, expect } from '@playwright/test';

/**
 * Phase 1 — 桌面版地圖核心功能 E2E 測試
 * 對象：UAT 環境（即時資料，案件數量會隨資料更新而變動）
 */

test.describe('地圖核心功能', () => {

  test('地圖正常載入：#map 容器存在且無 console error', async ({ page }) => {
    const consoleErrors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });
    page.on('pageerror', (err) => consoleErrors.push(err.message));

    await page.goto('/');

    const map = page.locator('#map');
    await expect(map).toBeVisible();

    // Leaflet 容器初始化後會帶有 leaflet-container class
    await expect(map).toHaveClass(/leaflet-container/);

    expect(consoleErrors, `console errors:\n${consoleErrors.join('\n')}`).toEqual([]);
  });

  test('查詢按鈕正常運作：點擊查詢後等待 API 回應，地圖出現 marker', async ({ page }) => {
    await page.goto('/');

    const responsePromise = page.waitForResponse(
      (resp) => /\/api\/crime\/(points|heatmap)/.test(resp.url()) && resp.status() === 200
    );

    await page.getByRole('button', { name: '查詢' }).click();
    await responsePromise;

    // 點位（個別 marker 或群集 marker）至少要有一個出現在地圖上
    await expect(page.locator('.leaflet-marker-icon').first()).toBeVisible({ timeout: 15000 });
  });

  test('熱力圖／點位圖切換：切換後對應圖層正確出現與消失', async ({ page }) => {
    test.setTimeout(120000);
    await page.goto('/');

    const heatRadio = page.locator('#toggle-mode input[value="heat"]');

    // 初次載入會自動以點位圖模式查詢全部資料，期間切換按鈕會被停用，
    // 需等待初次查詢完成（按鈕恢復可用）才能切換顯示模式
    await expect(heatRadio).toBeEnabled({ timeout: 90000 });

    // 切到熱力圖
    // 注意：點位圖模式載入時已在背景預先抓取 /api/crime/heatmap 並快取，
    // 切換模式時若快取已存在則直接重繪，不會再發送新請求，
    // 因此這裡改為直接等待熱力圖圖層出現，而非等待 API 回應
    // radio 本身為 display:none（自訂樣式套用在 label 內的 span 上），改點擊可見的文字
    await page.locator('#toggle-mode').getByText('熱力圖').click();

    await expect(page.locator('.leaflet-heatmap-layer')).toBeVisible({ timeout: 30000 });

    // 切回點位圖
    await page.locator('#toggle-mode').getByText('點位圖').click();

    await expect(page.locator('.leaflet-heatmap-layer')).toHaveCount(0);
  });

  test('popup 顯示正確：點擊 marker 後 popup 顯示行政區欄位', async ({ page }) => {
    await page.goto('/');

    await page.waitForResponse(
      (resp) => /\/api\/crime\/(points|heatmap)/.test(resp.url()) && resp.status() === 200
    );
    await expect(page.locator('.leaflet-marker-icon').first()).toBeVisible({ timeout: 15000 });

    // 在低縮放層級下，點位多以群集（cluster）呈現；
    // 持續點擊群集讓地圖放大，直到出現可點擊的個別 marker
    let individualMarker = page.locator('.leaflet-marker-icon:not(.marker-cluster)');
    for (let attempt = 0; attempt < 6; attempt++) {
      if (await individualMarker.count() > 0) break;

      const cluster = page.locator('.marker-cluster').first();
      if (await cluster.count() === 0) break;

      await cluster.click();
      await page.waitForTimeout(800); // 等待群集放大動畫與重新渲染
      individualMarker = page.locator('.leaflet-marker-icon:not(.marker-cluster)');
    }

    await expect(individualMarker.first()).toBeVisible({ timeout: 10000 });
    await individualMarker.first().click();

    const popup = page.locator('.leaflet-popup .crime-popup');
    await expect(popup).toBeVisible({ timeout: 10000 });
    await expect(popup.locator('th', { hasText: '行政區' })).toBeVisible();
  });

  test('篩選條件收合：點擊篩選列後面板展開，再次點擊後收合', async ({ page }) => {
    // 篩選條件收合行為僅存在於手機版版面（≤768px）
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/');

    const toggleBtn = page.locator('#btn-filter-toggle');
    const filterPanel = page.locator('#filter-panel');

    await expect(toggleBtn).toBeVisible();
    await expect(filterPanel).not.toHaveClass(/open/);

    await toggleBtn.click();
    await expect(filterPanel).toHaveClass(/open/);

    await toggleBtn.click();
    await expect(filterPanel).not.toHaveClass(/open/);
  });

});

# taipei-crime-map
台北市治安地圖 - .NET 9 DDD 專案

## E2E 測試（Playwright）

E2E 測試專案位於 `tests/TaipeiCrimeMap.E2E/`，針對 UAT 環境
（`https://taipei-crime-map-uat.ambitioussand-7326440b.japaneast.azurecontainerapps.io`）
執行瀏覽器自動化測試。

### 第一次設定

```bash
cd tests/TaipeiCrimeMap.E2E
npm install
npx playwright install chromium
```

### 執行測試

於專案根目錄執行：

```bash
npm run test:e2e
```

或直接於 `tests/TaipeiCrimeMap.E2E/` 目錄下執行：

```bash
npm run test:e2e        # 無頭模式執行所有測試
npm run test:e2e:headed # 開啟瀏覽器視窗執行
npm run test:e2e:ui     # 開啟 Playwright UI 模式
```

### 測試內容

- `tests/map.spec.ts` — 桌面版地圖核心功能（地圖載入、查詢、熱力圖／點位圖切換、popup、篩選面板收合）
- `tests/mobile.spec.ts` — 手機版（375x812）RWD 版面測試

測試對象為 UAT 即時環境，測試結果會隨資料庫內容變動。
若 UAT 容器處於冷啟動狀態，初次查詢可能需要較長時間，部分測試已設定較長的逾時時間因應。

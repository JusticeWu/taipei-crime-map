# 任務報告：前端地圖開發（Agent Teams） — 2026-06-04

1. **主要解決什麼問題？**
   後端 API 資料無法直接被使用者看到；用三個並行 Agent 同時開發 UI 版面、地圖模組、圖表模組，完成可互動的台北市治安地圖前端。

2. **如何證明是否執行正確？**
   - `window.mapModule.init/update` 與 `window.chartModule.init/update` 介面對接驗證無誤（grep 確認）
   - CI PR #17 pipeline 通過後，開啟 `https://<uat-url>/` 能看到地圖頁面、點擊查詢顯示 11,514 筆資料

3. **怎樣才是好的作法？**
   事先定義共享合約（DOM ID、全域介面名稱、資料格式），讓各 Agent 能獨立開發而不互相依賴；`UseDefaultFiles + UseStaticFiles` 讓 .NET API 同時服務 REST 和靜態前端，不需另外部署 Web Server。

4. **最重要的知識或概念（最多三個）**
   - **Agent Teams 並行開發**：就像三個工人同時蓋房子的不同部分（外牆、水電、裝潢），事先說好介面（門開在哪、電線穿哪），最後組合起來就能用。
   - **`window.mapModule` / `window.chartModule` 全域介面**：各模組把自己的功能掛在 `window` 上，就像店家把招牌掛在門口，其他人不需要知道店內裝潢也能呼叫服務。
   - **Leaflet Heatmap vs CircleMarker**：熱力圖看整體密度分佈，點位圖看個別案件位置，兩者用同一份資料，切換模式只需清除圖層再重繪。

5. **核心的變數是什麼？**
   - `_lastData`：app.js 快取最後一次 API 回應，切換地圖模式時不重新 fetch
   - `mode`（`'heat'`｜`'point'`）：地圖顯示模式，由 radio toggle 控制
   - `window.mapModule` / `window.chartModule`：三個模組的唯一整合點

6. **新手可能常犯的誤區？**
   - `UseStaticFiles` 要在 `MapControllers` 之前加，否則靜態檔案請求會被 API routing 攔截。
   - Chart.js 的 canvas reuse：若不先 `destroy()` 再重建，第二次 `update` 會報錯或圖表疊加。
   - Leaflet 地圖容器高度若沒有 CSS 明確設定，地圖會顯示高度為 0 的空白區塊。

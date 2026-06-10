### 任務報告：群聚圖示白色外框 — 修正真正根因 .district-bubble border — 2026-06-11

1. 主要解決什麼問題？
   - 使用者用 Chrome DevTools 直接 Inspect 找到白色外框的真正根因：
     `.district-bubble` 規則裡的 `border: 2px solid rgba(255,255,255,.75)`
     （PR #36 實作光暈效果時誤加，與 MarkerCluster 群聚圖示無關）
   - 已從 map.js 的 `.district-bubble` CSS 規則中移除該行

2. 如何證明是否執行正確？
   - `npx jest tests/frontend`：56/56 全數通過
   - `node --check map.js`：語法正確
   - push 到 uat 後，CI（build-and-test、push-to-acr、deploy-to-uat）皆 success
     （build-and-test 第一次因 mcr.microsoft.com 暫時性 403 失敗，
     依 L018 重新執行後成功）

3. 怎樣才是好的作法？
   - 外觀相似的不同元件（MarkerCluster 群聚圓圈 vs 行政區聚合圓圈
     `.district-bubble`）容易被誤認為同一個，回報問題時應先確認
     使用者點到的是哪一個元件
   - 使用者用 DevTools Inspect 找到的 Computed/Styles 面板結果是最終依據，
     程式碼比對只能作為輔助假設

4. 最重要的知識或概念（最多三個）：
   - 畫面上看起來很像的兩個圓形圖示，可能是完全不同的程式碼在畫的
   - 真正除錯時，「打開瀏覽器親自看」永遠比「用猜的」準
   - 修 CSS 時，先找到「是哪一行造成的」，再刪掉那一行就好，不用改一大片

5. 核心的變因是什麼？
   - `.district-bubble` 的 `border` 屬性是否存在，決定行政區聚合圓圈
     外圍是否有白色外框

6. 新手可能常犯的誤區？
   - 看到「圓形圖示有白框」就直接認定是 MarkerCluster 的問題，
     沒有先確認是哪一種圓形圖示
   - 沒有實機/瀏覽器驗證，只憑程式碼推論就動手改一大片 CSS

7. 流程圖與結構圖

```mermaid
flowchart TD
    A[使用者：白色外框仍存在] --> B[使用者用 DevTools<br/>直接 Inspect 元素]
    B --> C[找到 .district-bubble<br/>border: 2px solid rgba 255,255,255,.75]
    C --> D[map.js 移除該行 border]
    D --> E[行政區聚合圓圈<br/>不再顯示白色外框]
```

8. 分支與部署記錄
   - 開發分支：uat（直接提交）
   - PR 編號：無（直接 push 到 uat，承前兩次任務的延伸修正）
   - Merge 到：uat
   - Merge 時間：2026-06-10 21:49
   - CI 結果：✅ 成功（build-and-test 第一次因 L018 暫時性失敗，重跑後成功）
   - UAT 部署：✅ 成功

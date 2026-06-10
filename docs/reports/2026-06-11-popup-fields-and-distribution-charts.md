### 任務報告：Popup 加入行政區/時段/地點、圖表改為行政區/時段分布 — 2026-06-11

1. 主要解決什麼問題？
   - 退版（PR #29 之前）後重做範圍中的第 3、4 項：
     - 第 3 項：點位 popup 補回行政區、時段、地點、發生日期
     - 第 4 項：左下角圖表由「案類分佈」「年度趨勢」改為「行政區分布」「時段分布」

2. 如何證明是否執行正確？
   - `PointCrimeDto` 擴充為 `(Latitude, Longitude, CaseType, District, OccurredDate, TimeSlot, RawLocation)`，
     `dotnet build TaipeiCrimeMap.slnx --configuration Release`：建置成功，0 警告 0 錯誤
   - SP `sp_get_theft_cases_by_filter` 原本就是 `SELECT *`，已包含 district/time_slot/raw_location，
     確認不需修改 SQL
   - `npx jest tests/frontend`：2 個測試檔、31/31 通過（含新增的 `chart.pure.test.js`）
   - push 後 CI（`build-and-test` / `push-to-acr` / `deploy-to-uat`）全部 success

3. 怎樣才是好的作法？
   - 退版前已用 `git tag pre-revert-pr29-backup` 保留舊實作，這次第 3、4 項直接從該 tag
     取出已驗證可用的 `chart.js`、`PointCrimeDto`、`buildPopupHtml`、`chart.pure.test.js`，
     避免重新設計、降低出錯風險
   - DTO 欄位變動會改變前端快取資料結構，因此將 `CACHE_PREFIX` 由 `crimes:points:`
     升級為 `crimes:points:v2:`，避免讀到缺欄位的舊快取

4. 最重要的知識或概念（最多三個）：
   - 退版前留一份備份（標籤），之後要重做的功能可以直接「複製」回來，不用重寫
   - 資料格式（DTO）變了，瀏覽器裡存的舊資料也要跟著「換新」，不然欄位會是空的
   - `SELECT *` 的預存程序不用每次加欄位都改 SQL，後端 DTO 對應到位即可

5. 核心的變因是什麼？
   - `PointCrimeDto` 的欄位是否與前端 `item.district` / `item.timeSlot` / `item.rawLocation` /
     `item.occurredDate` 的命名一致（System.Text.Json 預設轉為 camelCase）
   - `CACHE_PREFIX` 版本號是否更新，決定使用者瀏覽器是否會用到缺少新欄位的舊快取

6. 新手可能常犯的誤區？
   - 改了後端 DTO 欄位，卻忘記前端 sessionStorage 快取版本號要一起升級，
     導致使用者看到的還是舊資料
   - 以為加欄位一定要改 SQL，卻沒注意到 SP 已經是 `SELECT *`

7. 流程圖與結構圖

```mermaid
flowchart TD
    A[退版後重做第3/4項] --> B[第3項：擴充 PointCrimeDto<br/>+District/TimeSlot/RawLocation]
    B --> C[CrimeController.GetCrimePoints<br/>Select 加入新欄位]
    C --> D[map.js buildPopupHtml<br/>欄位順序：案類/行政區/時段/地點/發生日期]
    D --> E[app.js CACHE_PREFIX<br/>v1 → v2]
    E --> F[commit: feat popup fields]
    F --> G[第4項：chart.js 改為<br/>行政區分布(橫條)+時段分布(長條)]
    G --> H[index.html canvas id 改名<br/>chart-district / chart-timeslot]
    H --> I[還原 chart.pure.test.js]
    I --> J[commit: feat replace charts]
    J --> K[dotnet build + npx jest 全過]
    K --> L[push uat → CI 全綠]
```

8. 分支與部署記錄
   - 開發分支：uat（直接提交，依使用者指示）
   - PR 編號：無（直接 push 到 uat）
   - Merge 到：uat
   - Merge 時間：2026-06-11 07:16
   - CI 結果：✅ 成功（build-and-test / push-to-acr / deploy-to-uat 全綠）
   - UAT 部署：✅ 成功
   - Commits：
     - `e48a45d` feat: add district/timeSlot/rawLocation to popup
     - `7e24148` feat: replace charts with district and timeslot distribution
     - `00918b0` chore: add grep/sed/find Bash permission rules

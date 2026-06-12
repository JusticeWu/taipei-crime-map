### 任務報告：OpenTelemetry + Application Insights 觀測中介層 — 2026-06-12

1. 主要解決什麼問題？
   - 系統目前缺乏集中式的請求觀測（IP、方法、路徑、狀態碼、耗時、TraceId），
     也沒有接上 Azure Application Insights，難以在 UAT/Prod 追蹤異常請求。

2. 如何證明是否執行正確？
   - `dotnet build TaipeiCrimeMap.slnx -c Debug` 成功，0 警告 0 錯誤
   - `dotnet test`：Domain 54、Application 34、Infrastructure 20 全數通過；
     Integration.Tests 失敗為本機無 SQL Server 的既有環境問題（已用 `git stash`
     驗證在變更前也是同樣失敗，與本次修改無關）
   - PR #40 squash merge 到 `uat`，CI run 27375775695
     （build-and-test、push-to-acr、deploy-to-uat）全部 ✅

3. 怎樣才是好的作法？
   - 新功能用獨立 middleware 實作，不動既有 `TimingMiddleware`，職責分離
   - 第三方整合（Application Insights）採 graceful degradation：
     未設定連線字串時完全不註冊，本機/測試環境零影響

4. 最重要的知識或概念（最多三個）：
   - **Middleware** 就像一道道檢查哨，每個請求都會經過，可以在這裡記錄資訊
   - **Graceful degradation**：沒有某個設定時，程式不報錯，只是少了那個功能
   - **LogLevel 分級**：依照狀態碼把 log 分成「正常／警告／錯誤」，方便篩選

5. 核心的變因是什麼？
   - 環境變數 `APPLICATIONINSIGHTS_CONNECTION_STRING` 是否存在，決定是否啟用
     OpenTelemetry/Azure Monitor；本機與測試環境通常未設定，故不啟用

6. 新手可能常犯的誤區？
   - 安裝 NuGet 套件後忘記加對應的 `using`，導致擴充方法（如 `UseAzureMonitor`）找不到
   - 假設方案檔一定是 `.sln`，此專案實際是 `.slnx`

7. 流程圖與結構圖
   ```mermaid
   flowchart TD
       A[HTTP 請求進入] --> B[ObservabilityMiddleware]
       B --> C[TimingMiddleware]
       C --> D[後續 Pipeline / Controller]
       D --> E[回應]
       E --> B
       B --> F{狀態碼}
       F -->|5xx| G[LogLevel.Error]
       F -->|4xx| H[LogLevel.Warning]
       F -->|2xx/3xx| I[LogLevel.Information]
       G & H & I --> J[結構化 Log: IP/方法/路徑/狀態碼/耗時/TraceId]
       J --> K{APPLICATIONINSIGHTS_CONNECTION_STRING 存在?}
       K -->|是| L[匯出至 Application Insights]
       K -->|否| M[僅本機 Log，不啟用 OpenTelemetry]
   ```

8. 分支與部署記錄
   - 開發分支：feature/observability
   - PR 編號：#40
   - Merge 到：uat
   - Merge 時間：2026-06-12 04:36 (UTC+8)
   - CI 結果：✅ 成功
   - UAT 部署：✅ 成功

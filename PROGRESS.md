### 進度記錄：2026-06-09 01:00

**任務**：① 收尾（L013-L015 + 報告 + commit/push uat + CI 綠燈）— 已完成；
② 調查 Garnet 連線持續失敗的根本原因 — **已找到根因並驗證修復成功**

- ① 收尾：全部完成（見上一筆記錄，commit `8f1bf8e`，CI run 27146953036 全綠）

- ② Garnet 根因調查 — **結論：根因是「Container App 內部 FQDN DNS 解析異常」，
  改用短名稱 `taipei-crime-map-garnet:6379` 後問題消失**：

  已排除的假說（詳見上一筆記錄）：映像路徑重複、不同 Container Apps Environment、
  `exposedPort`/`targetPort` 不一致、Garnet 健康狀態、mTLS/IP 限制

  **方向 1（短名稱）測試結果 — 成功**：
  - 使用者於 `2026-06-08T16:38:26Z` 將 UAT Container App 環境變數
    `ConnectionStrings__Redis` 從完整 FQDN
    `taipei-crime-map-garnet.internal.ambitioussand-7326440b.japaneast.azurecontainerapps.io:6379`
    改為短名稱 `taipei-crime-map-garnet:6379`
  - 改完後觸發兩次新查詢測試（不同篩選條件，避開 L1 快取命中）：

    | 測試 | L2-Cache | DB-Query | L2-Write | 總計（容器內） | RedisConnectionException |
    |---|---|---|---|---|---|
    | #1（2022年資料）16:46:17Z | 578ms | 81ms | 121ms | 781ms | ❌ 無 |
    | #2（2023年資料）16:55:09Z | 123ms | 53ms | 34ms | 211ms | ❌ 無 |

  - 對照修正前：L2-Cache 16,071~21,120ms、L2-Write 5,789~5,850ms、
    且必定伴隨 `RedisConnectionException ... ConnectTimeout`
  - **兩次測試 L2-Cache/L2-Write 皆降到三位數毫秒，且完全沒有出現連線例外** → 短名稱解決了問題
  - 附註：測試 #2 的 `Invoke-WebRequest` 量到 22 秒，但這是「容器冷啟動」
    （log 顯示 16:55:06Z 才 `Application started`，3 秒後處理請求），
    與 Garnet 連線無關；容器內實際處理只花 271ms（`[Timing] GET /api/crime ⇒ 200 | 總耗時=271ms`）

  **根因推論**：完整 FQDN（`<app>.internal.<env-domain>.azurecontainerapps.io`）
  在此環境內的 DNS 解析或路由層出現異常（連線從未進入交握階段，`rs: NotStarted, ws: Initializing`），
  改用短名稱（同環境內 Envoy proxy 直接以 app name 路由）後恢復正常。
  這與 Microsoft 文件中「同環境內部呼叫建議優先使用短名稱」的建議相符——
  短名稱路徑更簡單，不依賴額外的 FQDN DNS 解析步驟。

- 後續建議動作：
  1. 把這次發現記錄為 lessons-learned（L016：FQDN vs 短名稱）
  2. 在 docs/decisions.md 的 Garnet 章節補充「UAT 連線字串使用短名稱」這項設定細節，
     避免未來重建 Container App 時又設成 FQDN 重蹈覆轍
  3. 確認 Prod 環境（若已部署 Garnet）是否也使用短名稱；若仍是 FQDN，應一併修正
  4. 此設定目前是手動透過 Azure Portal 修改（不在 IaC/CI 內），
     需要在文件中註明「重建 Container App 時記得用短名稱」

- 卡住的問題：無（本輪調查已有明確結論，無待解決的卡點）

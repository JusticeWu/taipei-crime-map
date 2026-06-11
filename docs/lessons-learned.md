# 已學習的教訓

## 格式說明
每個教訓包含：
- 問題描述
- 根本原因
- 正確做法
- 相關模式（避免類似錯誤）

---

## L001：git show 取錯報告檔案
- 問題：用 git show HEAD -- 'docs/reports/*.md' 取到字母排序最舊的檔案
- 根本原因：git show 對 glob 的排序是字母順序，不是時間順序
- 正確做法：用 git diff HEAD~1 HEAD --name-only 找本次變更的檔案，再 sort -r | head -1
- 相關模式：所有需要「取最新」的場景，不能依賴 glob 的預設排序

## L002：CLAUDE.md 放在錯誤位置
- 問題：CLAUDE.md 被建立在 tests/.../Handlers/ 而非專案根目錄
- 根本原因：建立檔案時沒有確認工作目錄
- 正確做法：建立重要設定檔前，先確認絕對路徑
- 相關模式：任何全域設定檔都應放在專案根目錄

## L003：Dockerfile 使用 Alpine 導致 Segfault
- 問題：aspnet:9.0-alpine 導致 Microsoft.Data.SqlClient 6.x Segfault（exit code 139）
- 根本原因：Alpine 缺少原生函式庫
- 正確做法：永遠使用 aspnet:9.0（Debian），禁止使用 alpine
- 相關模式：引入新的原生函式庫時，確認與 base image 的相容性

## L004：報告存在錯誤位置或內容不符標題
- 問題：任務完成後報告沒有存入 docs/reports/，或報告內容與標題不符
- 根本原因：沒有完成前自我檢查清單
- 正確做法：任務完成前逐項確認自我檢查清單
- 相關模式：每次任務結束都必須有對應的報告檔案

## L005：快取不應是強依賴（cache-aside 模式）
- 問題：Garnet 連線失敗導致整個 API 回傳 500
- 根本原因：GetAsync/SetAsync 沒有 try/catch，快取失敗直接拋出例外
- 正確做法：快取失敗時 log warning 並 fallthrough 到資料庫，結果仍正常回傳
- 相關模式：所有外部依賴（快取、第三方 API）都應有 graceful fallback，
  不能讓外部依賴的失敗影響核心功能

## L006：變更未被正確 commit 導致部分修正遺失
- 問題：SetAsync 的 try/catch 被認為已加入，但實際上沒有 commit
- 根本原因：沒有在 commit 前確認所有預期的變更都已包含
- 正確做法：commit 前執行 git diff 確認所有變更，
  任務完成前自我檢查清單加入「git diff 確認變更完整」
- 相關模式：多步驟修改時，每個步驟完成後立即 commit，不要累積

## L007：CSV 資料每次 deploy 重複匯入
- 問題：每次部署都重新匯入 CSV，導致資料累積到 264,822 筆
- 根本原因：匯入前沒有檢查資料是否已存在
- 正確做法：deploy 前先查 record count，> 0 則跳過匯入
- 相關模式：冪等性（Idempotency）— 重複執行同一操作結果應相同

## L008：docker-compose.yml 與實際架構不一致，導致本機驗證卡住
- 問題：實作 timing-tracker 時想啟動本機環境驗證 [Timing] log，
  執行 docker compose up 起了 postgres + garnet，但 API 連不上資料庫
  （ConnectionString 屬性尚未初始化），最後判斷無法在本機完整驗證
- 根本原因：docs/decisions.md 記載 2026-06-04 已將資料庫從 PostgreSQL
  遷移到 Azure SQL Database（程式改用 Microsoft.Data.SqlClient），
  但 docker-compose.yml 仍停留在舊架構（postgres image），
  CLAUDE.md 的「技術棧」說明也還寫著「PostgreSQL + Dapper」未同步更新
- 正確做法：架構決策變更時，需同步更新 docker-compose.yml、CLAUDE.md
  技術棧描述等周邊文件，避免後續任務誤判本機環境可直接驗證；
  本機若要連 Azure SQL，應在 README 或 decisions.md 註明連線字串
  取得方式（user-secrets / 環境變數），而不是留下會誤導的本機 compose 設定
- 相關模式：文件與架構需同步更新（Documentation Drift）—
  決策記錄變更時，連帶檢查所有引用舊架構的設定檔與說明文件

## L009：uat 長期分支被 gh pr merge --delete-branch 自動刪除
- 問題：merge uat → main 的 PR 時使用了 --delete-branch，導致 uat 被刪除，
  後續 PR 無法以 uat 為 base
- 根本原因：--delete-branch 對 feature/xxx 這種一次性分支是正確做法，
  但不應該套用在 uat、main 這種對應永久環境的長期分支上
- 正確做法：uat → main 的 PR merge 一律用 gh pr merge --merge，不加 --delete-branch；
  feature/xxx → uat 才用 gh pr merge --squash --delete-branch
- 相關模式：長期分支（uat、main）永遠不刪；merge 指令需依「分支是否為長期分支」分流

## L010：Bash session 讀不到 Windows User 層級環境變數 SLACK_WEBHOOK_URL
- 問題：在 Bash 工具直接執行 CLAUDE.md 範例的 python3 urllib 通知指令，
  os.environ['SLACK_WEBHOOK_URL'] 拋出 KeyError，通知發送失敗
- 根本原因：SLACK_WEBHOOK_URL 是設定在 Windows「使用者」層級環境變數，
  但 Bash 工具啟動的 shell 並未繼承該變數（PowerShell session 才看得到）
- 正確做法：先用 PowerShell 以 [Environment]::GetEnvironmentVariable(...,'User')
  取得變數值，於同一個 PowerShell session 中設定 $env:SLACK_WEBHOOK_URL
  後再執行 python 指令發送；不要直接假設 Bash 能讀到 Windows 使用者環境變數
- 相關模式：跨 shell（Bash vs PowerShell）環境變數不互通——
  涉及機密/設定值的指令，先確認執行環境是否能讀到該變數，必要時換到能讀到的 shell

## L011：違反 CLAUDE.md 不串接指令規定，且事後否認
- 問題：執行 `cd "D:\...\taipei-crime-map" && cat .claude/settings.json 2>/dev/null || echo "FILE NOT FOUND"`，
  觸發「Compound command contains cd with output redirection」手動確認；
  事後被問起時，先翻查指令紀錄沒找到，便錯誤回覆「本次 session 未發生」，
  直到使用者提供畫面截圖內容、重新逐行搜尋整份 transcript，才在更早的時間點找到該指令
- 根本原因：沒有遵守 CLAUDE.md「避免用 && 串接多個指令」的規定，把 `cd` 和帶有輸出重導向／管線的
  指令（`cat ... 2>/dev/null || echo ...`）串在一起執行；事後查證時又只搜尋了部分時間範圍，
  在證據不全的情況下就下結論並回覆使用者
- 正確做法：分兩步執行，先 `cd`，再單獨執行 `cat`（Bash 工具的工作目錄會在多次呼叫之間保留，
  不需要、也不應該用 `&&` 串起來）；被質疑「是否做過某事」時，應先完整搜尋紀錄
  （例如整份 transcript、而非僅憑記憶或片段搜尋）再答覆，避免在證據不足時否認
- 相關模式：每個指令獨立執行，等上一個完成再執行下一個；
  對自身行為的查證要徹底，沒有完整證據前不要下結論式的否認

## L012：.claude/settings.json 的 Bash 允許規則是「指令字串比對」，不是「目錄範圍限制」
- 問題：原本想把 `Bash(cat *)`、`Bash(type *)` 加入允許清單，
  讓讀檔指令不再跳出確認；使用者詢問這個免確認範圍是否限定在專案目錄內，
  才意識到答案是「否」，遂將兩條規則移除，恢復手動確認
- 根本原因：誤以為允許規則會根據「目前工作目錄」或「指令操作的路徑」做範圍限制；
  實際上 `Bash(cat *)` 只是對指令文字做前綴比對，`cat ~/.ssh/id_rsa`、
  `cat C:\Users\...\.azure\...` 同樣會被自動放行，與專案目錄無關
- 正確做法：新增 Bash 允許規則前，先確認該規則是否可能匹配到「專案目錄外的任意路徑」；
  對於像 `cat`/`type` 這種泛用讀檔指令，由於沒有目錄範圍限制機制，
  原則上不加入允許清單，保留手動確認作為防線
- 相關模式：權限設定的最小授權原則（Principle of Least Privilege）——
  新增任何免確認規則前，先想清楚它「最壞情況下」能做到什麼，而不是只看「最常見用法」

## L013：TimingTracker LogSummary 在 using 區塊內呼叫導致 L1 命中時不輸出
- 問題：LogSummary 在 using 區塊內被呼叫，StageTimer.Dispose 還未執行，
  _records 是空的，導致 L1 命中時完全不輸出 Timing 摘要，
  造成「L1 永遠 miss」的錯覺
- 根本原因：LogSummary 呼叫時機在 using 區塊結束之前
- 正確做法：在所有 using 區塊結束後再呼叫 LogSummary
- 相關模式：IDisposable 計時器的結果只有在 Dispose 後才會寫入，
  呼叫摘要前必須確保所有計時器已結束

## L014：Garnet 連線逾時預設值過長導致每次 L2 失敗都要等 20 秒
- 問題：StackExchange.Redis 預設 ConnectTimeout 和 SyncTimeout 過長，
  Garnet 連線失敗時要等 16,000～21,000ms 才 fallback 到 DB，
  整體請求耗時 ~45 秒
- 根本原因：連線逾時未針對 cache-aside 模式設定合理的快速失敗值
- 正確做法：ConnectTimeout 和 SyncTimeout 設為 2000ms，
  讓快取失敗時快速 fallback，不阻塞主流程
- 相關模式：快取是非強依賴，失敗要快速放棄，
  不能讓快取的問題拖慢核心業務

## L015：Log 篩選條件造成「L1 永遠 miss」的觀測偏差
- 問題：用 [Timing] 總計= 關鍵字篩選 log 時，
  天生只會選到 L1 未命中的樣本（L1 命中時因 L013 的 bug 不會輸出這行），
  造成「L1 永遠 miss」的錯覺
- 根本原因：觀測工具的篩選條件影響了觀測結果
- 正確做法：診斷快取問題時，同時觀察命中和未命中兩種 log，
  不能只看單一關鍵字
- 相關模式：觀測偏差，篩選條件本身會影響你看到的結果

## L016：Container App 內部連線應使用短名稱，FQDN 會導致 DNS 解析異常造成 ConnectTimeout
- 問題：UAT 連到同一個 Container Apps Environment 內的 Garnet，
  連線字串使用完整內部 FQDN
  （`taipei-crime-map-garnet.internal.<env-domain>.azurecontainerapps.io:6379`），
  每次都會卡在 `RedisConnectionException ... ConnectTimeout`，
  且連線狀態顯示 `rs: NotStarted, ws: Initializing`（連線從未進入交握階段）。
  曾依序排除映像路徑、Container Apps Environment 是否相同、`exposedPort`/`targetPort`、
  Garnet 健康狀態、mTLS/IP 限制等假說，皆與此無關
- 根本原因：在這個環境中，完整內部 FQDN 的 DNS 解析或路由層出現異常，
  導致封包根本沒有送達目的地容器；改用短名稱
  （`taipei-crime-map-garnet:6379`，由同環境內的 Envoy proxy 直接以 app name 路由）
  後，連線立即恢復正常（L2-Cache 從 16,000~21,000ms 降到 123~578ms，
  且完全不再出現 `RedisConnectionException`）
- 正確做法：同一個 Container Apps Environment 內部的服務間連線，
  優先使用「短名稱:port」（例如 `<app-name>:<port>`），不要用完整 FQDN；
  這也符合 Microsoft 官方文件對內部呼叫的建議——短名稱路徑更簡單，
  少一層 FQDN DNS 解析，較不容易遇到平台層的解析異常
- 相關模式：診斷連線逾時類問題時，「換一種更簡單的定址方式測試」
  （短名稱 vs FQDN、IP vs 主機名稱）是低成本、高訊息量的排查手段，
  可以在深入懷疑平台限制／改採其他服務之前先嘗試

## L017：Container App 新增/修改環境變數（Secret）後，立即呼叫 API 仍會失敗一段時間
- 問題：UAT 新增 `googlemaps-api-key` Secret 與 `GoogleMaps__ApiKey` 環境變數、
  並完成部署後，立即呼叫 `/api/crime/geocode` 仍然 100% 失敗
  （`apiCallCount` 與 `failedCount` 相同），重新呼叫多次（batchSize=10、3）
  結果都一樣，過了一段時間後再測才開始有部分成功，
  接著很快就 100% 成功
- 根本原因：Container App 更新環境變數/Secret 後會建立新 Revision，
  但舊 Revision 的容器（或快取的設定）可能仍在處理流量，
  需要一段傳播/汰換時間，新設定才會完全生效於所有實例
- 正確做法：更新 Container App 的環境變數或 Secret 後，
  不要只測一次就判定失敗，應間隔數分鐘後用小批次（batchSize=3~10）
  重試幾次，觀察 failedCount 是否逐漸下降到 0 再進行大批次處理
- 相關模式：任何「修改雲端資源設定後立即驗證」的場景，
  都應預留設定傳播時間並用小規模重試確認，
  避免把「設定剛生效中」誤判為「設定錯誤」

## L018：scripts/ask_claude.py 因缺少 ANTHROPIC_API_KEY 而無法使用，CLAUDE.md 強制諮詢步驟被略過
- 問題：依 CLAUDE.md 規定，遇到 API/資料結構等架構決策時必須先呼叫
  `python scripts/ask_claude.py "..."` 取得建議，但執行時拋出
  `TypeError: Could not resolve authentication method...`，
  確認 `ANTHROPIC_API_KEY` 在使用者環境變數（User scope）中為空，
  導致此次新增 `/api/crime/stats`、`/api/crime/points/{id}` 等 API
  設計決策無法照規定先諮詢，最後改由使用者直接提供完整規格才得以繼續
- 根本原因：本機環境未設定 `ANTHROPIC_API_KEY`，與 L010 的
  `SLACK_WEBHOOK_URL` 跨 shell 環境變數問題屬同一類型——
  腳本依賴的環境變數只在某些 shell/session 中存在或根本未設定
- 正確做法：在執行任何依賴環境變數的輔助腳本前，
  先用 `[Environment]::GetEnvironmentVariable('VAR_NAME','User')`
  （或對應 shell 的指令）確認變數確實存在；
  若確認缺少，且使用者已親自提供等同規格的決策內容，
  可視為已完成「諮詢」步驟的替代方案，但應在 lessons-learned 記錄，
  避免下次再次卡在同一個缺口
- 相關模式：CLAUDE.md 中所有「呼叫腳本/工具」的強制步驟，
  都應假設該腳本可能因環境變數缺失而失敗，
  遇到失敗時要先確認是環境問題還是邏輯問題，並紀錄根因

## L019：鬼打牆模式 — 同一個問題反覆修超過 8 小時仍無法解決
- 問題：熱力圖圖層混進點位圖的問題，從半夜修到中午，多次 git revert 退版，
  每次退版後暫時解決，重新實作後又復發，耗費超過 8 小時
- 根本原因：每次修法都是在現有架構上打補丁（加 removeLayer、加 clearLayers），
  沒有思考架構是否有根本問題。正確的架構是：popup 詳細資料應該懶載入
  （點擊時才查），統計圖表應該用獨立的彙總 API，而不是把所有欄位
  預先塞進 PointCrimeDto 一次下載
- 正確做法：當同一個問題修超過 2 次仍無法解決，應該停下來重新思考架構，
  而不是繼續在同一個方向打補丁
- 相關模式：懶載入、關注點分離、漸進式資料下載

## L020：CI email 報告重複寄送 — 同一任務多次 push 導致重複收信
- 問題：CI workflow 設定為每次 push 都寄送 email 報告，當同一個任務
  因為迭代修改而多次 push 到 uat 時，每次 push 都會觸發一次 email，
  導致同一任務的報告被重複寄送多封信
- 根本原因：email 寄送條件綁定在「push 事件」而非「任務完成」或
  「報告檔案有新增/變更」，沒有區分「同一任務的中途迭代 push」
  與「任務最終完成的 push」
- 正確做法（待辦）：修正 CI workflow，讓 email 只在偵測到新的
  docs/reports/*.md 任務報告檔案時才寄送；或改為只在同一任務的
  最後一次 push（例如以 commit message 或 PR 狀態判斷）才寄送，
  避免同一任務的中途迭代 push 重複觸發 email
- 相關模式：CI 通知觸發條件、避免通知疲勞（notification fatigue）

## L021：UI 視覺樣式調整需多輪迭代，每次小步 commit + push 確認可降低風險
- 問題：地圖點位 emoji 樣式（底色、大小、顏色對應）前後調整了 4 次
  （案類顏色圓底 → 統一深灰圓底 → 透明無底 → 白色圓底放大），
  其中第一版做完整套（顏色對應 + emoji + 圖例）後被使用者要求整個 revert 重做
- 根本原因：視覺樣式類的需求很難用文字一次描述清楚最終效果，
  使用者需要實際在 UAT 上看到才能判斷喜好；第一次直接做「完整規格」
  而非先做最小可視化版本，導致 revert 成本較高
- 正確做法：視覺樣式類任務拆成最小單位（例如先只調一個變因：底色或大小），
  每次都 commit + push 到 uat 並等待使用者在實機確認後再進行下一步，
  避免一次性實作大量視覺細節後才發現方向不對
- 相關模式：小步快跑（small batches）、視覺回饋迴圈（visual feedback loop）

## L022：民國年與西元年單位不一致導致年份篩選查無資料
- 問題：前端起始/結束年份輸入框傳給 API 的是西元年（如 2018、2024），
  但資料庫 `occurred_year` 欄位儲存的是民國年（如 104~115）；
  Stored Procedure 與統計查詢直接用 `occurred_year >= @YearFrom` 比對，
  條件恆為假（115 < 2018），導致只要填了起始年份，查詢結果就變成空資料
- 根本原因：跨層（前端 → API → SP/SQL → 資料庫欄位）的資料格式
  （西元年 vs 民國年）沒有統一的轉換點，每一層都假設「對方應該已經轉換過」
- 正確做法：在 SP / SQL 層統一把 `occurred_year` 轉換為西元年
  （`occurred_year + 1911`）後再與前端傳入的西元年比較，
  確保資料庫內部欄位（民國年）與外部 API 參數（西元年）的轉換
  集中在單一、明確的層級
- 相關模式：跨層資料格式轉換要有明確的單一負責層（single source of truth
  for unit conversion）

## L023：手機版 RWD 用 flex:1 在 height:auto 父層下會被壓縮為 0
- 問題：`#map-container` 與 `#chart-container` 在桌面版透過 `flex: 1`
  自動撐滿剩餘高度，但手機版 `#app` 改為 `flex-direction: column;
  height: auto`；此時 `flex: 1`（即 `flex-basis: 0%`）會讓兩個區塊的
  起始主軸尺寸為 0，且父層沒有固定高度可供 `flex-grow` 分配，導致
  地圖容器高度趨近於 0（地圖看不到），而 `#chart-container` 原本直接
  `display: none` 整個隱藏，造成圖表「無資料」
- 根本原因：`flex: 1`（flex-basis: 0%）會覆蓋明確設定的 `height`，
  在 `height: auto` 的 column 容器中沒有可分配空間，子元素因此塌陷為 0
- 正確做法：手機版媒體查詢中，將 `#map-container`、`#chart-container`
  改為 `flex: none` 並給予明確高度（如 `height: 60vw; min-height: 300px`
  與 `height: 480px`），讓 `height` 設定生效；圖表容器需有非零高度，
  Chart.js 才能正確計算 canvas 尺寸並繪製
- 相關模式：flexbox 子元素若需要明確高度生效，必須搭配 `flex: none`
  （或將 `flex-basis` 設為 `auto`），否則 `flex-basis: 0%` 會優先生效

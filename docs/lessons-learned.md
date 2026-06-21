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

## L024：點位圖模式精簡 DTO 缺少欄位導致統計值恆為預設值
- 問題：「最多案件行政區」欄位在點位圖模式（預設模式）下永遠顯示「—」。
  `computeStats()` 從 `_lastData`（來自 `/api/crime/points`）取
  `item.districtName || item.district` 計算最大值，但 `PointCrimeDto`
  為了縮小回應體積，刻意省略了 `district` 欄位，導致該值永遠是
  `undefined`，分組永遠為空
- 根本原因：前端統計邏輯假設來源資料包含完整欄位，但實際串接的是
  「精簡 DTO」，欄位裁減發生在後端、卻未同步檢查前端所有消費端
- 正確做法：「最多案件行政區」改用 `/api/crime/stats` 回傳的
  `districtDistribution`（涵蓋完整篩選結果且包含 district 欄位）取最大值，
  與圖表共用同一份資料來源，不再依賴點位圖精簡 DTO
- 相關模式：當後端為效能而提供「精簡 DTO」時，需盤點前端所有讀取該
  資料的欄位，避免裁減掉仍被使用的欄位（或像本次一樣改用其他既有的
  完整資料來源）

## L025：position:fixed 面板的 z-index 蓋住觸發按鈕，導致開合邏輯看似相反
- 問題：手機版「篩選條件 ▼」按鈕點擊後面板會展開，但展開後再次點擊同一
  位置卻無法關閉，使用者覺得「開合邏輯相反」，只能用面板內的 ✕ 關閉
- 根本原因：`#filter-panel` 為 `position: fixed; top: 0; z-index: 1000`，
  展開時 `max-height` 變大會整個蓋在 `#btn-filter-toggle`（一般文件流、
  無 z-index）上方，導致該按鈕的點擊區域被面板擋住，點擊事件落在面板而
  非按鈕上
- 正確做法：將觸發按鈕設為 `position: relative; z-index` 高於面板
  （例如 1001 > 1000），並讓面板 `top` 從按鈕高度（48px）開始往下滑出，
  使按鈕永遠在最上層、隨時可點擊切換；同時依 `.open` class 動態切換箭頭
  文字（▼ / ▲）讓使用者看得出目前狀態
- 相關模式：position:fixed 的覆蓋層若與觸發它的按鈕同處頁面頂端，務必
  檢查兩者的 z-index / 版面位置關係，避免覆蓋層蓋住觸發點造成「按鈕失靈」

## L026：手機版用同一條 .leaflet-bottom 規則同時偏移 attribution 與自訂控制項，導致 attribution 留白
- 問題：手機版地圖右下角的 OpenStreetMap attribution 列與地圖底部之間
  多了約 70px 的空白，即使在 `index.html` 加入 `!important` 的
  `.leaflet-control-attribution` 樣式也無法消除
- 根本原因：`map.js` 的手機版規則
  `.leaflet-bottom.leaflet-left, .leaflet-bottom.leaflet-right { bottom: 70px; }`
  原意是讓左下角縮放按鈕與右下角圖例離地圖底部 80px，但 Leaflet 的
  attribution 控制項預設也放在 `.leaflet-bottom.leaflet-right` 這個
  共用容器內，因此被同一條規則一起往上推了 70px，造成空白
- 正確做法：在 `style.css` 的手機版媒體查詢中，額外加入
  `.leaflet-bottom.leaflet-right { bottom: 0 !important; }`，用
  `!important` 蓋過共用的 70px 偏移，讓該容器（含 attribution）
  緊貼地圖底部；`.leaflet-bottom.leaflet-left`（縮放按鈕側）維持
  `bottom: 70px` 不變。圖例與圖層切換按鈕因為改用
  `positionLayerPicker()`／自身的 `position:absolute; bottom:Xpx`
  動態定位，整體位置會隨容器下移但相對關係不受影響
- 相關模式：[[L025]]；Leaflet 的四個角落容器（`.leaflet-top/.leaflet-bottom`
  × `.leaflet-left/.leaflet-right`）可能同時承載「框架內建控制項
  （如 attribution、zoom）」與「自訂控制項（如圖例、圖層按鈕）」，
  對整個容器套用版面偏移前，需先確認容器內還有哪些控制項會被一併影響

## L027：dotnet build 找錯 solution 檔名稱、UseAzureMonitor 擴充方法找不到
- 問題：(1) `dotnet build TaipeiCrimeMap.sln` 報錯「專案檔不存在」；
  (2) 加入 `Azure.Monitor.OpenTelemetry.AspNetCore` 套件後，
  `builder.Services.AddOpenTelemetry().UseAzureMonitor(...)` 編譯錯誤
  CS1061，找不到 `UseAzureMonitor` 擴充方法
- 根本原因：(1) 此專案的方案檔是 `TaipeiCrimeMap.slnx`（新版 .slnx 格式），
  不是傳統的 `.sln`；(2) `UseAzureMonitor` 是
  `Azure.Monitor.OpenTelemetry.AspNetCore` 命名空間底下的擴充方法，
  僅安裝套件不會自動加入對應的 `using`，編譯器找不到擴充方法所在的命名空間
- 正確做法：(1) 用 `Glob`／`ls *.sln*` 確認方案檔實際副檔名與名稱，
  不要假設一定是 `.sln`；(2) 加入 NuGet 套件後，若呼叫的是該套件提供的
  擴充方法，需同時在使用檔案加入對應的 `using <PackageNamespace>;`，
  並先 `dotnet build` 確認可編譯，再執行 `dotnet test`
- 相關模式：新增第三方套件整合時，「安裝套件」與「加入對應 using」是
  兩個獨立步驟，缺一不可；專案結構（方案檔格式、命名）不可憑經驗假設，
  動手前先用搜尋工具確認

## L028：.gitignore 的 *.e2e（VS Trace Files）在 Windows 上不分大小寫，誤擋新建立的 E2E 測試專案目錄
- 問題：依需求在 `tests/TaipeiCrimeMap.E2E/` 建立 Playwright 專案後，
  `git status` 完全看不到該目錄（連 untracked 都不顯示），
  `git add` 也無效
- 根本原因：`.gitignore` 中既有一條 Visual Studio 產生的規則 `*.e2e`
  （Visual Studio Trace Files 副檔名），git 在 Windows 上對 `.gitignore`
  pattern 的比對不分大小寫，導致 `TaipeiCrimeMap.E2E`（以 `.E2E` 結尾的
  目錄名）被這條規則整個目錄忽略
- 正確做法：新增檔案/目錄後若 `git status` 完全沒有顯示，先用
  `git status --ignored=matching -- <path>` 確認是否被忽略、
  再用 `grep` 找出對應的 `.gitignore` 規則；不要直接刪除既有的通用規則，
  改為新增明確的反向規則（`!tests/TaipeiCrimeMap.E2E/` +
  `!tests/TaipeiCrimeMap.E2E/**`）解除特定目錄的忽略
- 相關模式：建立新檔案/目錄時，命名若恰好符合 `.gitignore` 中既有的
  通用副檔名規則（尤其是大小寫不敏感的 Windows 環境），會被靜默忽略；
  `git add` 後務必用 `git status --porcelain` 確認檔案確實被加入

## L029：自訂樣式的 radio/checkbox（display:none）用 Playwright locator.check() 會卡到逾時
- 問題：`#toggle-mode` 的顯示模式切換用 `<input type="radio">` +
  `<span>` 自訂樣式，CSS 將 `input[type="radio"]` 設為 `display:none`；
  Playwright 對該 input 呼叫 `.check()`（含 `force: true`）或用
  `locator('label', { has: input })` 尋找父層 label 都會卡住直到
  60~120 秒逾時，而非立即報錯
- 根本原因：`display:none` 的元素沒有 bounding box，Playwright 的
  actionability 檢查（即使加 `force: true`）仍可能持續等待元素變為可互動；
  `{ has: ... }` 的 locator 過濾在某些情況下也未如預期立即解析
- 正確做法：自訂樣式的表單控制項，改用使用者實際看得到、點得到的元素
  （例如 `page.locator('#toggle-mode').getByText('熱力圖')`）觸發互動，
  而不是直接操作被隱藏的原生 input
- 相關模式：E2E 測試應模擬「使用者實際可互動的元素」，
  CSS 隱藏的原生表單元素即使技術上仍存在於 DOM，也不應作為互動目標

## L030：前端「智慧模式切換」會略過重複 API 請求，E2E 等待 API 回應的斷言永遠不會觸發
- 問題：撰寫「熱力圖／點位圖切換」測試時，切換到熱力圖模式後用
  `page.waitForResponse(/\/api\/crime\/heatmap/)` 等待 API 回應，
  結果卡到測試逾時（120 秒）
- 根本原因：點位圖模式載入時，`app.js` 的 `queryProgressive()` 已在
  背景預先呼叫 `/api/crime/heatmap` 並快取於 `_lastHeatmapData`；
  切換到熱力圖模式時 `onModeChange()` 發現快取已存在，直接重繪
  （`setHeatmap(_lastHeatmapData)`），不會再發送新的 API 請求，
  因此等待新請求的 Promise 永遠不會 resolve
- 正確做法：E2E 測試應斷言「使用者可觀察到的最終結果」（例如
  `.leaflet-heatmap-layer` 是否出現/消失），而非假設「使用者操作
  必定觸發特定的網路請求」；點位圖初次載入（11,514 筆，progressive
  paging）在 UAT 冷啟動時可能耗時數十秒，相關測試需個別用
  `test.setTimeout()` 放寬逾時，並先等待操作按鈕恢復可用
  （非 disabled）再進行互動
- 相關模式：[[L019]] 懶載入／快取會改變預期的網路行為；
  E2E 測試應驗證「行為結果」而非「實作細節（特定請求是否發出）」

## L031：Task.WhenAll(taskA, taskB) 在兩個 Task 回傳型別不同時，無法用陣列索引取值
- 問題：將 GetStatsByFilterAsync 改為並行查詢時，寫成
  `var results = await Task.WhenAll(districtTask, timeSlotTask); var districtRows = results[0];`
  編譯失敗
- 根本原因：districtTask 回傳 IEnumerable<StatsDistrictRow>，
  timeSlotTask 回傳 IEnumerable<StatsTimeSlotRow>，兩者型別不同，
  Task.WhenAll<TResult> 無法推論出共同的 TResult，因此無法回傳陣列
- 正確做法：先 `await Task.WhenAll(taskA, taskB)` 讓兩者並行執行完成，
  再分別 `await taskA` 與 `await taskB` 取得各自結果（此時已完成，
  不會再等待）
- 相關模式：兩個獨立、型別不同的非同步查詢要並行執行時，
  使用「await Task.WhenAll 等待 + 個別 await 取值」而非陣列索引

## L032：WHERE 條件對索引欄位做運算（occurred_year + 1911）造成 non-sargable，索引失效
- 問題：[[L022]] 為了把民國年轉西元年，在 WHERE 條件寫成
  `occurred_year + 1911 >= @YearFrom`，雖然邏輯正確，但 `occurred_year`
  欄位被算式包住後，SQL Server 無法用 `ix_theft_cases_occurred_year`
  做 Index Seek，只能 Index/Table Scan
- 根本原因：non-sargable 的寫法——對欄位本身做運算的 WHERE 條件，
  Optimizer 無法利用該欄位上的索引；同樣的轉換邏輯放在欄位側還是
  參數側，邏輯等價但效能差異很大
- 正確做法：把運算移到參數（常數）側，欄位保持原樣：
  `occurred_year >= @YearFrom - 1911`、`occurred_year <= @YearTo - 1911`，
  讓 Optimizer 可以走 Index Seek；SP 內以字串組裝 SQL 的版本也要同步修改
  （新增 migration script，不修改已部署的舊版本）
- 相關模式：sargable predicate — WHERE/JOIN/ORDER BY 條件中，
  索引欄位不應被函式或運算式包住（CAST、+ - * /、YEAR()、SUBSTRING 等），
  運算應移到常數/參數側；SELECT 投影欄位上的函式則不影響索引，無需修改

## L033：本機 AVG 防毒軟體的 SSL 攔截，導致 az CLI 與 curl 對外 HTTPS 請求失敗
- 問題：執行 `az containerapp logs show` / `az containerapp show` 一律
  失敗於 `SSLCertVerificationError: unable to get local issuer certificate`
  （連到 login.microsoftonline.com）；改用 `curl` 直接打 Container App
  的 FQDN 時，又失敗於 schannel 錯誤
  `CRYPT_E_NO_REVOCATION_CHECK`（撤銷清單檢查失敗）
- 根本原因：本機已安裝 AVG 防毒軟體並啟用「Web/Mail Shield」SSL/TLS
  掃描，會在本機產生中間人憑證（`CN=AVG Web/Mail Shield Root`）。
  (1) az CLI（Python/requests）使用自帶的 certifi CA bundle，
  不認得這張本機自簽憑證，驗證失敗；
  (2) curl 在 Windows 走 schannel，AVG 攔截後的憑證鏈撤銷資訊
  （CRL/OCSP）查詢失敗，導致 `CRYPT_E_NO_REVOCATION_CHECK`
- 正確做法：
  - az CLI 的問題：需修補 az CLI 的 CA bundle 信任 AVG 憑證
    （本次因屬本機環境設定變更，使用者選擇改走 Azure Portal 操作，未修補）
  - curl 的問題：加上 `--ssl-no-revoke` 參數跳過撤銷檢查即可正常連線
    （`curl -s --ssl-no-revoke "https://<app>.azurecontainerapps.io/..."`）
- 相關模式：本機防毒/防火牆做 SSL 攔截是一類常見、會同時影響多種 CLI
  工具（az、curl、python requests）的環境問題；遇到
  `CERTIFICATE_VERIFY_FAILED` 或 `CRYPT_E_NO_REVOCATION_CHECK` 時，
  先檢查 `Cert:\LocalMachine\Root` 是否有防毒軟體產生的中間人憑證，
  再決定要修補 CA bundle 還是用工具本身的「跳過驗證」參數繞過

## L034：CI workflow 改用新的 GitHub Secret 後，先 push 的那次執行會因 Secret 尚未建立而失敗
- 問題：將 `.github/workflows/ci.yml` 的 `to: chengyi.ks@gmail.com`
  改為 `to: ${{ secrets.REPORT_EMAIL_TO }}` 並 push 到 uat 後，
  `deploy-to-uat` 的 `Send email report` 步驟失敗：
  `##[error]At least one of 'to', 'cc' or 'bcc' must be specified`
- 根本原因：push 發生在 16:52，但 `REPORT_EMAIL_TO` 這個 GitHub Secret
  是在 push 之後才建立的（00:37 隔日 UTC，即更晚）。該次 CI run 執行時
  Secret 還不存在，`${{ secrets.REPORT_EMAIL_TO }}` 解析為空字串，
  導致 action-send-mail 的 `to` 欄位是空的而報錯
- 正確做法：
  - 變更 CI workflow 改參照新的 `${{ secrets.XXX }}` 之前，必須先用
    `gh secret set XXX` 建立好該 Secret，再 push 參照它的 commit
  - 若順序顛倒導致該次 run 失敗，Secret 建立後用
    `gh run rerun <run-id> --failed` 重跑失敗的 job 即可，
    不需要重新 push commit
- 相關模式：CI 設定變更與其所依賴的外部資源（Secret、環境變數、
  雲端資源）之間有相依順序時，「依賴方」必須在「被依賴方」就位後
  才能被觸發；若已觸發且失敗，補上依賴後重跑該次 run 即可，
  不必製造新的 commit

## L035：將 docs/reports/ 加入 .gitignore 前，要先檢查 CI 是否依賴該目錄內容
- 問題：為了讓 repo 公開化，將 `CLAUDE.md`、`scripts/ask_claude.py`、
  `docs/lessons-learned.md`、`docs/reports/`、`.claude/settings.json`、
  `.vscode/settings.json` 從 git 追蹤移除並加入 `.gitignore`。
  但 `.github/workflows/ci.yml` 的 `deploy-to-uat`／`deploy-to-prod`
  兩個 job 都有「Find latest report」步驟，會讀取 checkout 出來的
  `docs/reports/*.md` 並透過「Send email report」寄信；
  一旦 `docs/reports/` 不再被追蹤，這兩個步驟會永遠找不到報告
  （顯示「無報告」），email 寄送功能會悄悄失效
- 根本原因：「移除內部檔案的追蹤」這個決策只從「要不要公開」的角度
  評估，沒有檢查 CI/CD pipeline 是否反向依賴這些檔案的存在
- 正確做法：
  - 執行 `git rm --cached` 前，先用 `grep` 搜尋 `.github/workflows/`
    是否引用了即將 gitignore 的路徑（例如 `docs/reports`、
    `docs/lessons-learned.md`）
  - 確認有依賴後，與使用者討論處理方式（停用該步驟／改用其他資料源／
    暫不 gitignore），而不是直接執行後讓 CI 悄悄壞掉
  - 本次選擇：gitignore `docs/reports/`，同時移除 CI 中的
    「Find latest report」與「Send email report」步驟
    （uat、prod 兩個 job 都要處理）
- 相關模式：調整 `.gitignore` 或移除檔案追蹤前，先確認 CI/CD
  workflow、build script 等自動化流程是否讀取該路徑，
  避免「檔案還在本機、但 CI 看不到」造成的隱性功能失效

## L036：整合測試中如何提供 Options 綁定的測試用密碼
- 問題：新增 `AdminAuthOptions`（`AdminAuth:Username`/`AdminAuth:Password`）後，
  `appsettings.json` 依慣例只放空字串（真實值在 Azure Secret），
  但 `CustomWebApplicationFactory` 沒有對應的測試用設定來源，
  導致 401/200/404 整合測試無法組出有效的 Basic Auth 帳密
- 根本原因：測試環境缺少一個「已知且穩定」的設定值來源，
  可同時滿足「不把真實密碼寫進 repo」與「測試需要可預期的帳密」兩個需求
- 正確做法：在 `CustomWebApplicationFactory.ConfigureWebHost` 用
  `builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(...))`
  注入測試專用的固定帳密常數（例如 `AdminUsername`/`AdminPassword`），
  此設定來源會在預設設定（appsettings.json、環境變數）之後加入，
  優先生效，且不需修改 CI workflow 或新增 appsettings.Testing.json
- 相關模式：任何新增的 `IOptions<T>` 設定，若 `appsettings.json`
  為空字串佔位（如 `GoogleMaps.ApiKey`），整合測試需要該值時，
  優先考慮在 `CustomWebApplicationFactory` 用記憶體設定覆寫，

## L037：點位圖 fitBounds 應在 render queue 清空後才呼叫
- 問題：`finalizeLoad` 在所有 marker 尚未加入地圖前就呼叫
  `_map.fitBounds(...)`，調整 `maxZoom` 從 13 一路改到 16 都沒有效果，
  地圖視野仍涵蓋新北市、基隆、桃園等遠超台北市的範圍
- 根本原因：`maxZoom` 只會限制「最多放大到多少」，不會強制套用該縮放層級；
  若 `fitBounds` 計算出的自然縮放層級本身就比 `maxZoom` 小（例如算出 13，
  設定 `maxZoom: 16`），`maxZoom` 完全不會生效。而 `fitBounds` 的計算
  是在 marker 尚未透過 `requestAnimationFrame` 分批渲染完成前執行的，
  此時地圖容器/圖層狀態還不完整
- 正確做法：將 `fitBounds` 的呼叫從 `finalizeLoad` 移到
  `drainOneFromQueue`（render queue 清空、所有 chunks 渲染完成）之後：
  - `finalizeLoad` 只負責計算 bounds 並存入模組層級變數 `_pendingBounds`
  - 新增 `applyPendingBounds()`：當 `_renderQueue.length === 0` 時
    （包含一開始就空、以及最後一個 chunk 渲染完畢後兩種情況），
    若 `_pendingBounds` 有值就呼叫 `_map.fitBounds(_pendingBounds, {...})`
    並清空 `_pendingBounds`
- 相關模式：`fitBounds`／`getZoom()` 等與地圖視野相關的計算，
  應安排在所有圖層內容渲染完成之後執行，避免在地圖容器狀態尚未
  穩定時計算出不符預期的縮放層級；`maxZoom` 只是上限，不是目標值，
  不能用來「強制放大」

## L038：禁止用分號串接多個指令
- 問題：Claude Code 持續用分號串接多個指令（例如 `cd path; command`），在 PowerShell 環境下會觸發安全警告，需要手動確認。
- 根本原因：沒有遵守 CLAUDE.md 的指令執行規範。
- 正確做法：每個指令獨立執行，等上一個完成再執行下一個。`cd` 和後續指令必須分開呼叫。
- 相關模式：所有需要切換目錄再執行的場景，都要拆成兩個獨立指令。`&&` 同樣禁止，唯一例外是 `git add && git commit` 這類原子操作。

## L039：IHostedService.StopAsync 必須冪等（idempotent）
- 問題：`ServerMetricsService.StopAsync` 呼叫 `_cts.CancelAsync()` 後沒有 null out `_cts`。整合測試 `WebApplicationFactory.Dispose()` 在某些情況下會呼叫 `StopAsync` 兩次，第二次因 `_cts` 已 dispose 未 null 而拋出 `ObjectDisposedException`，導致 CI 失敗。
- 根本原因：`CancellationTokenSource.Dispose()` 後沒有將欄位設為 null，使第二次呼叫仍能通過 `if (_cts is not null)` 判斷。
- 正確做法：先將 `_cts` swap 到 local variable 並 null out 欄位，再 cancel/dispose local copy：
  ```csharp
  var cts = _cts;
  _cts = null;
  if (cts is not null) { await cts.CancelAsync(); cts.Dispose(); }
  ```
- 相關模式：任何實作 `IHostedService`、`IDisposable` 或 `IAsyncDisposable` 的類別，其 Stop/Dispose 方法都應設計為可安全呼叫多次（冪等），包含 null out 已 dispose 的資源欄位。

## L040：測試全綠不代表需求完整 — CaseImportWorker 遺漏即時 Geocoding
- 問題：資料新增功能（CaseImportWorker）寫入 DB 後，只會嘗試複用既有地址的舊座標，沒有主動呼叫 Geocoding API。新增 72 筆全新地址的真實資料後，大多數案件顯示「成功」但實際沒有座標，因為「成功」的定義只涵蓋「DB 寫入成功」，沒有涵蓋「座標已取得」。所有單元測試、整合測試、E2E 測試都通過，因為當初沒有人寫測試去驗證「座標是否真的被取得」這個案例。
- 根本原因：這不是程式碼邏輯錯誤，是需求範圍遺漏。寫測試的人（或 AI）只會驗證自己想到的場景，沒想到要驗證的場景，測試自然也不會涵蓋，因此「所有測試通過」無法保證「功能範圍完整」。
- 正確做法：用使用者視角重新檢視功能完整性，而不只依賴測試結果。發現問題後，把「成功」拆分成更精確的狀態（Status=Success 但額外標記 HasCoordinate），讓系統的回報更貼近真實情況，而不是用單一的「成功/失敗」掩蓋資料品質的細節。同時補上業務規則測試（驗證 Geocoding 成功與失敗兩種情況下的欄位狀態），但更重要的長期防護是建立監控機制（例如缺座標筆數的告警），不要只依賴測試。
- 相關模式：任何「寫入成功」與「資料完整」是兩件獨立事情的場景（例如：訂單建立成功但庫存扣減失敗、使用者註冊成功但驗證信寄送失敗），都要清楚拆分「核心操作的成功」與「附帶處理的成功」，避免用單一布林值的成功/失敗掩蓋更細緻的狀態。

## L041：CROSS APPLY + GROUP BY 衝突、Dapper dynamic 污染 tuple 名稱、Task.WhenAll 缺乏錯誤隔離
- 問題：新增 3 個組合維度趨勢圖表後，/api/crime/stats 回傳 500，所有 5 個圖表（含原本穩定的時段分布、行政區分布）全部顯示無資料。根因有三個疊加的問題：(1) SQL 用 CROSS APPLY 產生 `ct.chinese_name` 欄位但 GROUP BY 只列了 `case_type`，SQL Server 拒絕執行；(2) 修正 SQL 後，Dapper `conn.QueryAsync(sql)` 回傳 `IEnumerable<dynamic>`，後續 `.Select(r => MapTrendRow(r))` 的回傳型別被 dynamic 污染，tuple 元素名稱（`.Label`）在 runtime 不存在（只有 `Item1`），拋出 `does not contain a definition for 'Label'`；(3) `Task.WhenAll` 並行 4 個查詢，任一失敗就拋 `AggregateException`，讓原本成功的時段/行政區查詢也被一起丟棄。
- 根本原因：(1) SQL Server 不會自動推斷 CROSS APPLY 欄位與 GROUP BY 欄位的函數依賴關係，即使 `ct.chinese_name` 在邏輯上是 `case_type` 的一對一映射，仍需明確列入 GROUP BY 或改在 C# 端組裝 label。(2) C# tuple 元素名稱是純粹的編譯期語法糖，`dynamic` 會跳過編譯期型別檢查，導致 LINQ chain 中途失去 tuple 名稱。(3) `Task.WhenAll` 的設計是「全成功或全失敗」，不提供部分成功的降級機制。
- 正確做法：(1) SQL 端只做純欄位 GROUP BY，label 組裝移到 C# 端的 `MapTrendRow`。(2) 使用 `foreach` 迴圈將 dynamic 結果逐筆轉換成強型別 `List<(string, int, int)>`，切斷 dynamic 污染鏈。(3) 多個獨立查詢並行執行時，若部分查詢屬於新增、風險較高的功能，應考慮個別 try-catch 包裹，讓單一查詢失敗時降級回傳空陣列，而不是讓整個 API 因為一個新功能的 bug 而完全打不開。
- 相關模式：[[L031]] Task.WhenAll 的型別限制；任何在 SQL 中做字串組裝/計算欄位的場景，都要確認 GROUP BY 是否需要包含該欄位；Dapper 的 `QueryAsync` 無型別參數版本回傳 dynamic，後續的 LINQ chain 型別推斷會被感染，應優先使用 `QueryAsync<T>` 或在 chain 起點就轉成強型別。

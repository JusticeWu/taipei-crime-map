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

<!-- 此檔案必須放在專案根目錄 D:\SourceCode\SiteProjects\taipei-crime-map\ -->

# 台北市治安地圖 — Agent 工作說明

## 開始任務前必須讀取
- CLAUDE.md（本檔案）
- docs/decisions.md（架構決策記錄）
- docs/lessons-learned.md（已學習的教訓，避免重複犯錯）

每次開始新任務前，請先讀取這三個檔案，確保決策一致。

## 遇到架構決策時
遇到以下情況，請呼叫 scripts/ask_claude.py 詢問建議後再繼續：
- 選擇技術方案（資料庫、快取、框架）
- 設計 API 或資料結構
- 效能優化策略
- 安全性相關決策

呼叫方式：
```
python scripts/ask_claude.py "你的問題"
```

決策依據將自動帶入 CLAUDE.md 和 docs/decisions.md 的內容。

## 專案背景
- .NET 9 / ASP.NET Core，DDD 架構
- PostgreSQL + Dapper + Stored Procedure
- MemoryCache 已實作在 GetCrimesByFilterQueryHandler
- 已部署到 Azure Container Apps（UAT）
- GitHub repo：JusticeWu/taipei-crime-map（Private）

## 技術棧
- 後端：.NET 9、C#、DDD、CQRS
- 資料庫：PostgreSQL、Dapper、DbUp
- 測試：xUnit、FluentAssertions、Moq
- CI/CD：GitHub Actions
- 雲端：Azure Container Apps、Azure Container Registry（Japan East）
- 前端：Leaflet.js + Chart.js（待實作）
- 快取：MemoryCache（已實作）、Redis（待實作）

## 專案結構
src/
├── TaipeiCrimeMap.Domain/
├── TaipeiCrimeMap.Application/
├── TaipeiCrimeMap.Infrastructure/
└── TaipeiCrimeMap.API/
tests/
├── TaipeiCrimeMap.Domain.Tests/
├── TaipeiCrimeMap.Application.Tests/
├── TaipeiCrimeMap.Infrastructure.Tests/
└── TaipeiCrimeMap.Integration.Tests/
scripts/          ← DbUp SQL scripts

## 分支策略（GitLab Flow）
feature/xxx → PR → uat → 自動部署 UAT → PR → main → 自動部署 Prod

每個功能建立獨立分支，完成後開 PR merge 進 uat；
UAT 驗證通過後，從 uat 開 PR merge 進 main 觸發 Prod 部署。

| 分支 | 環境 | Container App         | 觸發條件       |
|------|------|-----------------------|----------------|
| uat  | UAT  | taipei-crime-map-uat  | push to uat    |
| main | Prod | taipei-crime-map-prod | push to main   |

## Commit 規範（Conventional Commits）
- feat：新功能
- fix：修 bug
- test：測試
- refactor：重構
- chore：設定雜務
- ci：CI/CD 設定

## Dockerfile 規範
- 永遠使用 debian-based image（`aspnet:9.0`），不使用 alpine
- 原因：Microsoft.Data.SqlClient 等原生函式庫在 Alpine 上有相容性問題
- 禁止使用：`aspnet:9.0-alpine`、`runtime:9.0-alpine`

## API 端點
- POST /api/crime/import — 匯入 CSV
- GET /api/crime — 依條件查詢案件

## 資料說明
- 六類竊盜資料，共 11,514 筆
- 案類：住宅竊盜、汽車竊盜、機車竊盜、自行車竊盜、搶奪、強盜

## 任務完成前必須自我檢查
在回報任務完成之前，請逐項確認：
- [ ] 任務報告已存入 docs/reports/YYYY-MM-DD-[任務名稱].md
- [ ] 報告包含第7項 Mermaid 流程圖或結構圖
- [ ] 報告包含第8項分支與部署記錄
- [ ] 所有測試通過（dotnet test）
- [ ] 變更已 commit 並 push
- [ ] PR 已建立並 merge 到 uat
- [ ] CI pipeline 全部綠燈
- [ ] 發送任務完成 Slack 通知（規則二）
- [ ] 此次任務是否遇到錯誤、意外行為、設計缺陷或需要修正的問題？
      如果是，必須寫入 docs/lessons-learned.md，格式：
      ## L[下一個編號]：[問題標題]
      - 問題：
      - 根本原因：
      - 正確做法：
      - 相關模式：
      注意：功能修正和架構改善同樣需要記錄，不只是操作錯誤。
以上任一項未完成，不得回報任務完成。

## 任務完成後，必須產出任務報告
格式如下，回答簡潔，其他內容盡量越少越好：

### 任務報告：[任務名稱] — [日期]

1. 主要解決什麼問題？
2. 如何證明是否執行正確？
3. 怎樣才是好的作法？
4. 最重要的知識或概念（以小學生能聽得懂的方式說明，最多三個）
5. 核心的變因是什麼？（影響結果的關鍵因素）
6. 新手可能常犯的誤區？
7. 請為此次任務畫簡潔、清晰的流程圖與結構圖（用 Mermaid 語法）
8. 分支與部署記錄
   - 開發分支：feature/xxx
   - PR 編號：#N
   - Merge 到：uat
   - Merge 時間：YYYY-MM-DD HH:MM
   - CI 結果：✅ 成功 / ❌ 失敗
   - UAT 部署：✅ 成功 / ❌ 失敗

## Slack 通知規則

所有通知一律使用 Python 發送（curl 在 Windows 會中文亂碼）。

### 通知用函式（每次直接複製執行）

```bash
python3 -c "
import urllib.request, json, os
msg = '【在此替換訊息內容】'
data = json.dumps({'text': msg}).encode('utf-8')
req = urllib.request.Request(os.environ['SLACK_WEBHOOK_URL'], data=data, headers={'Content-Type': 'application/json'})
urllib.request.urlopen(req)
"
```

### 規則一：提問之前必須先發通知（強制，不可跳過）

在終端機顯示任何問題或選項給用戶之前，**必須先執行以下 Python 指令**發送 Slack 通知，
這是強制步驟，不可跳過。

**執行順序：1. 發送 Slack 通知 → 2. 顯示問題給用戶**

```bash
python3 -c "
import urllib.request, json, os
msg = '🤖 Claude Code 需要你的確認：【問題摘要，例如：是否要刪除 feature/xxx 分支？】'
data = json.dumps({'text': msg}).encode('utf-8')
req = urllib.request.Request(os.environ.get('SLACK_WEBHOOK_URL',''), data=data, headers={'Content-Type': 'application/json'})
try: urllib.request.urlopen(req)
except: pass
"
```

### 規則二：任務完成時必須發通知

在完成任務自我檢查清單、所有項目確認後，執行以下指令：

```bash
python3 -c "
import urllib.request, json, os
msg = '✅ 任務完成：【任務名稱，例如：Prod 環境部署 CI/CD】'
data = json.dumps({'text': msg}).encode('utf-8')
req = urllib.request.Request(os.environ['SLACK_WEBHOOK_URL'], data=data, headers={'Content-Type': 'application/json'})
urllib.request.urlopen(req)
"
```

### 規則三：遇到錯誤無法繼續時必須發通知

在停止作業、等待協助**之前**，執行以下指令：

```bash
python3 -c "
import urllib.request, json, os
msg = '❌ 遇到錯誤需要協助：【錯誤摘要，例如：docker build 失敗，缺少 AZURE_CREDENTIALS secret】'
data = json.dumps({'text': msg}).encode('utf-8')
req = urllib.request.Request(os.environ['SLACK_WEBHOOK_URL'], data=data, headers={'Content-Type': 'application/json'})
urllib.request.urlopen(req)
"
```

### 本機測試通知是否正常

```bash
python3 -c "
import urllib.request, json, os
data = json.dumps({'text': '🤖 Claude Code 通知測試'}).encode('utf-8')
req = urllib.request.Request(os.environ['SLACK_WEBHOOK_URL'], data=data, headers={'Content-Type': 'application/json'})
urllib.request.urlopen(req)
"
```

CI 內的 `curl` 指令在 GitHub Actions（Linux）上中文正常，不需修改。

## 配額耗盡時的處理
在停止前必須把進度寫入 PROGRESS.md，格式：

### 進度記錄：[日期時間]
- 已完成：...
- 下一步：...
- 卡住的問題：...

下次啟動時先讀 PROGRESS.md 繼續。

## 成本控制原則
- 優先使用 Sonnet，複雜推理才用 Opus
- 每個任務完成後關閉 session
- Google Maps API 限制每日配額
- Azure 資源不用時停止（PostgreSQL、Container Apps）
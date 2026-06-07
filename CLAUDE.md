<!-- 此檔案必須放在專案根目錄 D:\SourceCode\SiteProjects\taipei-crime-map\ -->

# 台北市治安地圖 — Agent 工作說明

## 開始任務前必須讀取
- CLAUDE.md（本檔案）
- docs/decisions.md（架構決策記錄）

每次開始新任務前，請先讀取這兩個檔案，確保決策一致。

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
feature/xxx → PR → uat → 自動部署 UAT → prod

每個功能建立獨立分支，完成後開 PR merge 進 uat。

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
以上任一項未完成，不得回報任務完成。

## 任務完成後，必須產出任務報告
格式如下，回答簡潔，其他內容盡量越少越好：

### 任務報告：[任務名稱] — [日期]

1. 主要解決什麼問題？
2. 如何證明是否執行正確？
3. 怎樣才是好的作法？
4. 最重要的知識或概念（以小學生能聽得懂的方式說明，最多三個）
5. 核心的變數是什麼？
6. 新手可能常犯的誤區？
7. 請為此次任務畫簡潔、清晰的流程圖與結構圖（用 Mermaid 語法）
8. 分支與部署記錄
   - 開發分支：feature/xxx
   - PR 編號：#N
   - Merge 到：uat
   - Merge 時間：YYYY-MM-DD HH:MM
   - CI 結果：✅ 成功 / ❌ 失敗
   - UAT 部署：✅ 成功 / ❌ 失敗

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
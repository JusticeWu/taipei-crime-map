# 台北市治安地圖 — Agent 工作說明

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

## API 端點
- POST /api/crime/import — 匯入 CSV
- GET /api/crime — 依條件查詢案件

## 資料說明
- 六類竊盜資料，共 11,514 筆
- 案類：住宅竊盜、汽車竊盜、機車竊盜、自行車竊盜、搶奪、強盜

## 任務完成後，必須產出任務報告
格式如下：

### 任務報告：[任務名稱] — [日期]

#### 遇到的主要問題（最多 3 個）
1. 問題：...
   解決方式：...

#### 最重要的關鍵概念（2～3 個）
1. ...
2. ...

#### 新手常犯的誤區
- ...

#### 下一步
- ...

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
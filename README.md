# 台北市治安地圖 Taipei Crime Map

利用台北市政府開放資料，將六類竊盜案件（住宅竊盜、汽車竊盜、機車竊盜、
自行車竊盜、搶奪、強盜，共 11,514 筆）視覺化呈現在互動地圖上，
讓使用者依行政區、案類、年份、時段篩選案件分布，並透過圖表了解統計趨勢。

## 線上 Demo

https://taipei-crime-map-prod.ambitioussand-7326440b.japaneast.azurecontainerapps.io/

## 技術棧

- **後端**：.NET 9 / ASP.NET Core、C#、DDD（領域驅動設計）、CQRS
- **資料庫**：Azure SQL Database、Dapper、DbUp（SQL migration）
- **快取**：Garnet（Redis 相容的分散式快取，L2）+ MemoryCache（L1）
- **前端**：Leaflet.js（地圖）、Chart.js（統計圖表）
- **測試**：xUnit、FluentAssertions、Moq（TDD）、Jest（前端單元測試）、
  Playwright（E2E 測試）
- **CI/CD**：GitHub Actions（自動建置、測試、部署）
- **雲端**：Azure Container Apps、Azure Container Registry（Japan East）
- **可觀測性**：OpenTelemetry

## 架構特色

- **DDD 領域模型**：依 Domain / Application / Infrastructure / API 分層，
  以 CQRS 區分查詢與命令，業務邏輯集中於 Domain 層，方便測試與維護。
- **L1 / L2 雙層快取**：查詢結果先查 MemoryCache（L1，程序內），
  未命中再查 Garnet（L2，跨執行個體共用），降低資料庫負載並縮短回應時間。
- **漸進式地圖載入（Progressive Loading）**：11,514 筆案件資料分批載入，
  避免使用者等待全部資料下載完成才看到地圖內容。
- **CI/CD 自動化**：採 GitLab Flow 分支策略
  （`feature/* → uat → main`），push 到 `uat` / `main` 會自動建置、
  測試、推送映像至 ACR，並部署到對應的 Azure Container Apps
  （UAT / Prod）。
- **E2E 測試**：以 Playwright 對部署後的 UAT 環境執行瀏覽器自動化測試，
  涵蓋桌面版地圖核心功能與手機版 RWD 版面。

## 本機開發

需求：.NET 9 SDK、Node.js、可連線的 SQL Server（含 Azure SQL）、
Garnet 或 Redis（快取用，可選）。

```bash
# 還原並建置後端
dotnet restore TaipeiCrimeMap.slnx
dotnet build TaipeiCrimeMap.slnx

# 設定連線字串（appsettings.Development.json 或環境變數）
#   ConnectionStrings__DefaultConnection
#   ConnectionStrings__Redis

# 啟動 API（含前端靜態頁面 wwwroot）
dotnet run --project src/TaipeiCrimeMap.API
```

啟動後開啟瀏覽器即可看到地圖頁面與 API（`/api/crime`、`/api/crime/stats` 等）。

## 測試

```bash
# 後端測試（xUnit）
dotnet test TaipeiCrimeMap.slnx

# 前端單元測試（Jest）
npm install
npm test

# E2E 測試（Playwright，測試對象為部署後的 UAT 環境）
cd tests/TaipeiCrimeMap.E2E
npm install
npx playwright install chromium
npm run test:e2e
```

## 專案結構

```
src/
├── TaipeiCrimeMap.Domain/         # 領域模型、實體、值物件
├── TaipeiCrimeMap.Application/    # CQRS 指令/查詢、應用服務
├── TaipeiCrimeMap.Infrastructure/ # Dapper Repository、快取、SQL migration scripts
└── TaipeiCrimeMap.API/            # ASP.NET Core API + 前端靜態頁面（wwwroot）

tests/
├── TaipeiCrimeMap.Domain.Tests/
├── TaipeiCrimeMap.Application.Tests/
├── TaipeiCrimeMap.Infrastructure.Tests/
├── TaipeiCrimeMap.Integration.Tests/
├── frontend/                      # Jest 前端單元測試
└── TaipeiCrimeMap.E2E/             # Playwright E2E 測試

docs/                # 架構決策（decisions.md）、任務報告、經驗教訓（lessons-learned.md）
scripts/             # DbUp SQL migration scripts
```

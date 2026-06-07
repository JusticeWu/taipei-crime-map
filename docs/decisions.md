# 架構決策記錄（ADR）

---

### 2026-06-04：Dockerfile base image

**決策**：使用 `aspnet:9.0`（Debian），不使用 alpine

**原因**：Microsoft.Data.SqlClient 6.x 在 Alpine 有 Segfault（exit code 139），原因是 Alpine 使用 musl libc，而 SqlClient 原生函式庫依賴 glibc。Debian-based image 使用 glibc，相容性正常。

**禁止**：`aspnet:9.0-alpine`、`runtime:9.0-alpine`

---

### 2026-06-04：資料庫從 PostgreSQL 遷移到 Azure SQL Database

**決策**：改用 Azure SQL Database 免費層

**原因**：
- Azure Database for PostgreSQL Flexible Server 無法永久停止（最多 7 天後自動重啟並開始計費）
- Azure SQL Database 免費層（32 GB，100,000 vCore 秒/月）永久免費，超額自動暫停，符合低頻率使用的 UAT 環境需求

**影響**：
- ORM 從 Npgsql/EF Core 改為 Dapper + Microsoft.Data.SqlClient
- Migration runner 從 DbUp（PostgreSQL）改為自製 SqlConnection 執行器

---

### 2026-06-07：快取從 MemoryCache 改為 Garnet 分散式快取

**決策**：使用 Garnet 分散式快取（Redis 相容）取代 IMemoryCache

**原因**：
- MemoryCache 為單機記憶體快取，多實例（replica）時各自獨立，無法共享
- Garnet 是 Microsoft 開源的 Redis 相容快取伺服器，效能優於 Redis，MIT 授權免費
- 透過 IDistributedCache 介面隔離，Application 層不直接依賴 Redis

**連線字串**：`ConnectionStrings__Redis`，預設 `localhost:6379`

**Azure 手動設定**：Container App Secret + Env Var 參照

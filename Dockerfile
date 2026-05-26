# =============================================
# Stage 1: Build & Test
# =============================================

# FROM：指定基於哪個 base image 
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /repo

# 複製 solution 與 project 檔（利用 Docker layer cache）
COPY *.slnx ./
COPY global.json ./
COPY NuGet.config ./

COPY src/TaipeiCrimeMap.Domain/*.csproj             src/TaipeiCrimeMap.Domain/
COPY src/TaipeiCrimeMap.Application/*.csproj         src/TaipeiCrimeMap.Application/
COPY src/TaipeiCrimeMap.Infrastructure/*.csproj      src/TaipeiCrimeMap.Infrastructure/
COPY src/TaipeiCrimeMap.API/*.csproj                 src/TaipeiCrimeMap.API/

COPY tests/TaipeiCrimeMap.Domain.Tests/*.csproj      tests/TaipeiCrimeMap.Domain.Tests/
COPY tests/TaipeiCrimeMap.Application.Tests/*.csproj tests/TaipeiCrimeMap.Application.Tests/
COPY tests/TaipeiCrimeMap.Integration.Tests/*.csproj tests/TaipeiCrimeMap.Integration.Tests/
COPY tests/TaipeiCrimeMap.Infrastructure.Tests/*.csproj tests/TaipeiCrimeMap.Infrastructure.Tests/

# dotnet restore 根據 .csproj 下載所有 NuGet 相依套件。
RUN dotnet restore TaipeiCrimeMap.slnx

# 程式碼改動頻繁，放在 restore 之後，不影響套件還原的快取。
COPY src/ src/
COPY tests/ tests/

# dotnet test：執行所有測試專案。
# --no-restore：不重新下載套件（前面已做過）。
# --configuration Release：用 Release 模式跑測試，和最終部署環境一致。
RUN dotnet test --no-restore --configuration Release \
    --logger "console;verbosity=minimal"

# dotnet publish：編譯並打包應用程式，產生可以部署的檔案。
# --output /app/publish：所有輸出（.dll、靜態資源等）放到 /app/publish，Stage 2 來拿。
RUN dotnet publish src/TaipeiCrimeMap.API/TaipeiCrimeMap.API.csproj \
    --configuration Release \
    --output /app/publish

# =============================================
# Stage 2: Runtime
# =============================================

# aspnet:9.0 比 sdk:9.0 小得多（無編譯器），只含執行 .NET 程式所需最小環境。
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
# 設定工作目錄為 /app，一般 .NET 應用程式慣例。
WORKDIR /app

# 建立一個非 root，沒有 login shell 的系統使用者，並加入群組。
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

# ./：複製到目前的 WORKDIR，即 /app。
# 每寫一個 FROM，就開啟一個全新的、獨立的建置環境。
# 使執行環境無 SDK 和原始碼。
COPY --from=build /app/publish ./

# 1. 讓 Dockerfile 自我說明。
# 2. 給自動化工具看的 metadata。
EXPOSE 8080
# ENV 設定環境變數，在容器執行時永久生效。
# 切換到 Production 模式，關掉詳細錯誤頁面、啟用效能優化等。
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# ENTRYPOINT：不可被 docker run 的參數覆蓋（不同於 CMD），更安全。
# 用 JSON 陣列格式，是 exec form，使 dotnet 為 PID 1，使 SIGTERM 直達它，
# 使用 ASP.NET Core 內建的 Graceful Shutdown 機制：
# 1. 停止接受新的 HTTP 請求。
# 2. 等待進行中的請求完成。
# 3. 正常關閉 DB / MQ 連線。
ENTRYPOINT ["dotnet", "TaipeiCrimeMap.API.dll"]
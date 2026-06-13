#!/bin/bash
# 量測 GET /api/crime/stats 回應時間
# 用法：./benchmark_stats.sh [BASE_URL]
#   BASE_URL 預設為 UAT 環境

BASE_URL="${1:-https://taipei-crime-map-uat.ambitioussand-7326440b.japaneast.azurecontainerapps.io}"

echo "BASE_URL: $BASE_URL"
echo "依序呼叫 /api/crime/stats，yearFrom 從 2015 到 2024（避免命中快取）"
echo ""

total=0
count=0

for year in $(seq 2015 2024); do
  url="${BASE_URL}/api/crime/stats?yearFrom=${year}"
  time_sec=$(curl -s -o /dev/null -w '%{time_total}' "$url")
  printf 'yearFrom=%d -> %s 秒\n' "$year" "$time_sec"
  total=$(awk -v t="$total" -v x="$time_sec" 'BEGIN { printf "%.6f", t + x }')
  count=$((count + 1))
done

avg=$(awk -v t="$total" -v c="$count" 'BEGIN { printf "%.6f", t / c }')

echo ""
echo "平均回應時間：${avg} 秒"

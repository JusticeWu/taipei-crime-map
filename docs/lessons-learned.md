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

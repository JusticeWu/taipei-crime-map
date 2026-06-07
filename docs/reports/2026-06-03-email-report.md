# 任務報告：自動寄信功能 — 2026-06-03

1. **主要解決什麼問題？**
   每次部署 UAT 後，需要人工確認結果；改成 pipeline 自動寄信，讓關係人即時收到最新報告。

2. **如何證明是否執行正確？**
   CI 三個 job（`build-and-test`、`push-to-acr`、`deploy-to-uat`）全數通過，`Send email report` 步驟顯示成功，信箱收到標題含 commit message 的郵件。

3. **怎樣才是好的作法？**
   用 `ls -t` 取最新檔案而非寫死檔名，讓每次新增報告時不需改 CI；密碼存 GitHub Secrets 而非明文寫在 YAML。

4. **最重要的知識或概念（最多三個）**
   - **GitHub Secrets**：就像把鑰匙放在保險箱，CI 執行時才取出來用，不會出現在程式碼裡。
   - **Gmail 應用程式密碼**：不是登入密碼，是 Google 另外發的「專用通行證」，只讓特定程式寄信。
   - **`$GITHUB_OUTPUT`**：步驟之間傳資料的便條紙，一個步驟寫進去，下一個步驟可以讀出來。

5. **核心的變數是什麼？**
   - `GMAIL_USERNAME` / `GMAIL_APP_PASSWORD`：寄信憑證
   - `github.event.head_commit.message`：信件主旨來源
   - `steps.find_report.outputs.content`：信件內文（最新報告的完整內容）

6. **新手可能常犯的誤區？**
   - 用 Gmail 登入密碼而非應用程式密碼，導致 SMTP 認證失敗。
   - `$GITHUB_OUTPUT` 多行內容沒用 `<<EOF` heredoc 語法，只會拿到第一行。
   - `deploy-to-uat` job 沒加 `Checkout` 步驟，讀不到 repo 裡的報告檔案。

7. **流程圖與結構圖**

```mermaid
flowchart TD
    Push[git push to uat] --> CI[GitHub Actions CI]
    CI --> Build[build-and-test]
    Build --> ACR[push-to-acr]
    ACR --> Deploy[deploy-to-uat]
    Deploy --> FindReport["Find latest report\nls -t docs/reports/*.md"]
    FindReport --> Output["$GITHUB_OUTPUT\ncontent<<EOF...EOF"]
    Output --> Email["Send email report\ndawidd6/action-send-mail"]
    Email --> Inbox[chengyi.ks@gmail.com]
```

8. **分支與部署記錄**
   - 開發分支：feature/email-report
   - PR 編號：#13（寄信功能）、#14（報告格式更新）
   - Merge 到：uat
   - Merge 時間：2026-06-03 15:39（#13）、2026-06-03 16:08（#14）
   - CI 結果：✅ 成功
   - UAT 部署：✅ 成功

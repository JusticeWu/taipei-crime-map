# 任務報告：自動寄信功能

**日期：** 2026-06-03
**分支：** feature/email-report
**作者：** Justice Wu

---

## 任務摘要

在 GitHub Actions CI pipeline 的 `deploy-to-uat` job 中，新增自動寄送任務報告的步驟。每當有 push 觸發部署到 UAT 環境時，pipeline 會自動讀取 `docs/reports/` 目錄下最新的報告檔案，並透過 Gmail SMTP 寄送至指定信箱。

---

## 變更內容

### `.github/workflows/ci.yml`

在 `deploy-to-uat` job 新增以下步驟：

1. **Checkout** — 新增 `actions/checkout@v4.2.2`，讓 job 可以存取 repository 中的報告檔案。
2. **Find latest report** — 使用 `ls -t docs/reports/*.md` 找出最新的報告檔案，並將內容寫入 `$GITHUB_OUTPUT`。
3. **Send email report** — 使用 `dawidd6/action-send-mail@v3` 透過 Gmail SMTP 寄送報告。

### 寄信設定

| 項目 | 值 |
|------|----|
| SMTP Server | smtp.gmail.com:587 |
| 寄件人 | `${{ secrets.GMAIL_USERNAME }}` |
| 收件人 | chengyi.ks@gmail.com |
| Subject | 任務報告：[commit message] |
| Body | `docs/reports/` 最新檔案的完整內容 |

### GitHub Secrets 需求

- `GMAIL_USERNAME` — Gmail 帳號（例：chengyi.ks@gmail.com）
- `GMAIL_APP_PASSWORD` — Gmail 應用程式密碼（非登入密碼）

---

## 測試結果

- CI pipeline 於 UAT 分支執行成功
- `build-and-test`、`push-to-acr`、`deploy-to-uat` 三個 job 全部通過
- Email 寄送步驟依賴 GitHub Secrets 設定，需在 repo Settings → Secrets 中配置

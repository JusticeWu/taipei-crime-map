"""
ask_claude.py — 快速詢問 Claude 的命令列工具

用法：
    python scripts/ask_claude.py "你的問題"
    python scripts/ask_claude.py "請幫我 review 這段程式" < myfile.cs

環境變數：
    ANTHROPIC_API_KEY — Anthropic API 金鑰（必填）
"""

import sys
import os
import anthropic


def main():
    api_key = os.environ.get("ANTHROPIC_API_KEY")
    if not api_key:
        print("ERROR: ANTHROPIC_API_KEY 環境變數未設定", file=sys.stderr)
        print("請執行：set ANTHROPIC_API_KEY=sk-ant-...", file=sys.stderr)
        sys.exit(1)

    # 從命令列引數取得問題
    if len(sys.argv) < 2:
        print("用法：python scripts/ask_claude.py \"你的問題\"", file=sys.stderr)
        sys.exit(1)

    user_message = " ".join(sys.argv[1:])

    # 若有 stdin 輸入（pipe），附加到訊息後面
    if not sys.stdin.isatty():
        stdin_content = sys.stdin.read().strip()
        if stdin_content:
            user_message = f"{user_message}\n\n```\n{stdin_content}\n```"

    client = anthropic.Anthropic(api_key=api_key)

    print(f"[Claude claude-sonnet-4-6] 思考中...\n", file=sys.stderr)

    message = client.messages.create(
        model="claude-sonnet-4-6",
        max_tokens=1024,
        messages=[{"role": "user", "content": user_message}],
    )

    print(message.content[0].text)


if __name__ == "__main__":
    main()

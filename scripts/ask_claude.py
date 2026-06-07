import anthropic
import sys
import os


def ask_claude(question: str) -> str:
    claude_md = ""
    decisions_md = ""

    # CLAUDE.md 已移至專案根目錄
    claude_md_path = os.path.join(os.path.dirname(__file__), "..", "CLAUDE.md")
    decisions_path = os.path.join(os.path.dirname(__file__), "..", "docs", "decisions.md")

    if os.path.exists(claude_md_path):
        with open(claude_md_path, "r", encoding="utf-8") as f:
            claude_md = f.read()

    if os.path.exists(decisions_path):
        with open(decisions_path, "r", encoding="utf-8") as f:
            decisions_md = f.read()

    system_prompt = f"""你是台北市治安地圖專案的架構顧問，請用繁體中文回答。

以下是專案說明：
{claude_md}

以下是架構決策記錄：
{decisions_md}

請根據專案背景給出具體建議，回答要簡潔，最多200字。"""

    client = anthropic.Anthropic()
    message = client.messages.create(
        model="claude-sonnet-4-6",
        max_tokens=1024,
        system=system_prompt,
        messages=[{"role": "user", "content": question}],
    )
    return message.content[0].text


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("用法：python scripts/ask_claude.py '你的問題'")
        sys.exit(1)

    question = " ".join(sys.argv[1:])
    answer = ask_claude(question)
    print(answer)

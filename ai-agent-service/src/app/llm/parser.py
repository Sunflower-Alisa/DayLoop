import json
import re


def parse_json(text: str) -> dict:
    match = re.search(r"\{.*\}", text, re.DOTALL)
    if not match:
        raise ValueError("无法从输出中解析 JSON")
    return json.loads(match.group(0))

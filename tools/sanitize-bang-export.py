from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


NEUTRAL_EKA_URL = "https://www.kleinanzeigen.de/s-%s/k0"
SHOP_GROUP_MEMBERS = ["asd", "ede", "ali"]
SENSITIVE_PATTERNS = (
    re.compile(r"localhost", re.I),
    re.compile(r"127\.0\.0\.1"),
    re.compile(r"192\.168\."),
    re.compile(r"10\.\d+\.\d+\.\d+"),
    re.compile(r"/s-\d{5}/"),
    re.compile(r"l\d+r\d+"),
    re.compile(r"[?&](?:lat|lon|lng|location|postcode|zip)=", re.I),
)


def normalize_bang(raw: dict) -> dict:
    keyword = str(raw.get("keyword", raw.get("Keyword", ""))).strip()
    urls = list(raw.get("urls", raw.get("Urls", [])) or [])
    if keyword.casefold() == "eka":
        urls = [NEUTRAL_EKA_URL]
    return {
        "keyword": keyword,
        "alias": raw.get("alias", raw.get("Alias")),
        "defaultUrl": str(raw.get("defaultUrl", raw.get("DefaultUrl", "")) or ""),
        "urls": urls,
        "groupMembers": list(raw.get("groupMembers", raw.get("GroupMembers", [])) or []),
        "dontEncodeQuery": bool(raw.get("dontEncodeQuery", raw.get("DontEncodeQuery", False))),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Create a privacy-safe public Custom Bangs export.")
    parser.add_argument("input", type=Path, help="PluginSettings.json or a version 6 export")
    parser.add_argument("flow_output", type=Path, help="Output including Flow search groups")
    parser.add_argument(
        "chrome_output",
        type=Path,
        nargs="?",
        default=None,
        help="Optional: also write a groupMembers-stripped copy for personal re-import into Custom Bang Search",
    )
    args = parser.parse_args()

    source = json.loads(args.input.read_text(encoding="utf-8-sig"))
    raw_bangs = source.get("bangs", source.get("Bangs"))
    if not isinstance(raw_bangs, list):
        raise ValueError("Input has no bangs/Bangs array")

    normalized = [normalize_bang(item) for item in raw_bangs]
    bangs = [
        bang
        for bang in normalized
        if len(bang["keyword"]) > 1 and not bang["groupMembers"]
    ]
    keywords = {bang["keyword"].casefold() for bang in bangs}
    missing_shop_members = [member for member in SHOP_GROUP_MEMBERS if member not in keywords]
    if missing_shop_members:
        raise ValueError(f"Cannot create shop1 group; missing shortcuts: {missing_shop_members}")

    findings = []
    for bang in bangs:
        for url in [bang["defaultUrl"], *bang["urls"]]:
            for pattern in SENSITIVE_PATTERNS:
                if pattern.search(url):
                    findings.append((bang["keyword"], pattern.pattern))
    if findings:
        raise ValueError(f"Potentially private URLs remain after sanitizing: {findings}")

    shop_group = {
        "keyword": "shop1",
        "alias": None,
        "defaultUrl": "",
        "urls": [],
        "groupMembers": SHOP_GROUP_MEMBERS,
        "dontEncodeQuery": False,
    }
    flow = {"version": 6, "bangs": [shop_group, *bangs]}

    args.flow_output.parent.mkdir(parents=True, exist_ok=True)
    args.flow_output.write_text(json.dumps(flow, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    summary = f"Flow list: {len(flow['bangs'])} entries"

    if args.chrome_output is not None:
        chrome_bangs = [
            {key: value for key, value in bang.items() if key != "groupMembers"}
            for bang in bangs
        ]
        chrome = {"version": 6, "bangs": chrome_bangs}
        args.chrome_output.parent.mkdir(parents=True, exist_ok=True)
        args.chrome_output.write_text(json.dumps(chrome, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        summary += f"; Chrome-format copy: {len(chrome_bangs)} entries"

    print(summary)


if __name__ == "__main__":
    main()

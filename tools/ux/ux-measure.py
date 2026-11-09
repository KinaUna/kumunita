#!/usr/bin/env python3
"""Report and ratchet the UX counts tracked by the IMPROVE-UX lane."""

from __future__ import annotations

import argparse
import html.parser
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / "tools/ux/ux-baseline.json"
TRACKED = (
    "bare_status",
    "a11y",
    "empty_plain",
    "empty_none",
    "jargon",
    "notify_without_link",
)


def files_under(path: Path, pattern: str) -> list[Path]:
    return sorted(path.rglob(pattern)) if path.exists() else []


def strip_comments(text: str) -> str:
    text = re.sub(r"@\*.*?\*@", "", text, flags=re.DOTALL)
    text = re.sub(r"<!--.*?-->", "", text, flags=re.DOTALL)
    return re.sub(r"(?m)^\s*//.*$", "", text)


def count_bare_status() -> int:
    pattern = re.compile(
        r"\breturn\s+(?:(?:NotFound|Forbid|BadRequest|Unauthorized|StatusCode)\s*\(|"
        r"(?:NotFound|Forbid)\s*;)"
    )
    return sum(
        len(pattern.findall(file.read_text(encoding="utf-8")))
        for file in files_under(ROOT / "src/Kumunita.Web/Controllers", "*.cs")
    )


class TagCollector(html.parser.HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=False)
        self.controls: list[dict[str, str | None]] = []
        self.images: list[dict[str, str | None]] = []
        self.labels_for: set[str] = set()

    def handle_starttag(
        self, tag: str, attrs: list[tuple[str, str | None]]
    ) -> None:
        attributes = dict(attrs)
        if tag in {"input", "select", "textarea"}:
            self.controls.append(attributes)
        elif tag == "img":
            self.images.append(attributes)
        elif tag == "label" and attributes.get("for"):
            self.labels_for.add(attributes["for"] or "")


def count_a11y() -> int:
    leads = 0
    for file in files_under(ROOT / "src/Kumunita.Web/Views", "*.cshtml"):
        collector = TagCollector()
        collector.feed(strip_comments(file.read_text(encoding="utf-8")))
        for control in collector.controls:
            if control.get("type", "").lower() == "hidden":
                continue
            if (
                control.get("aria-label")
                or control.get("aria-labelledby")
                or (control.get("id") and control["id"] in collector.labels_for)
            ):
                continue
            leads += 1
        leads += sum(1 for image in collector.images if "alt" not in image)
    return leads


EMPTY_CHECK = re.compile(
    r"(?:\.Count\s*(?:==\s*0|<=\s*0|<\s*1)|\.Any\s*\(\s*\)\s*==\s*false|"
    r"!\s*\w[\w.]*\.Any\s*\(\s*\)|IsNullOrEmpty\s*\()",
    re.IGNORECASE,
)
LIST_MARKUP = re.compile(r"@(?:foreach|for)\s*\(")


def count_empty_states() -> tuple[int, int]:
    plain = 0
    none = 0
    views = ROOT / "src/Kumunita.Web/Views"
    non_list_views = {"Calendar", "Create", "Edit", "New", "Resolve", "ResolveReview"}
    for file in files_under(ROOT / "src/Kumunita.Web/Views", "*.cshtml"):
        relative = file.relative_to(views)
        if (
            file.name.startswith("_")
            or relative.parts[0].startswith("Admin")
            or file.stem in non_list_views
        ):
            continue
        text = strip_comments(file.read_text(encoding="utf-8"))
        if not LIST_MARKUP.search(text) or "kmb-empty" in text:
            continue
        if EMPTY_CHECK.search(text):
            plain += 1
        else:
            none += 1
    return plain, none


JARGON = re.compile(
    r"\b(?:GlobalAdmin|slug|intake action|standing moderator|standing over|"
    r"account lane|deletion lane)\b",
    re.IGNORECASE,
)


def count_jargon() -> int:
    count = 0
    for file in files_under(ROOT / "src/Kumunita.Core/Localization/Keys/en", "*.json"):
        if file.stem == "admin":
            continue
        strings = json.loads(file.read_text(encoding="utf-8"))
        count += sum(
            1
            for key, value in strings.items()
            if isinstance(value, str) and JARGON.search(value)
        )
    return count


def emit_call_arguments(text: str, start: int) -> str | None:
    depth = 1
    quote: str | None = None
    escaped = False
    for index in range(start, len(text)):
        char = text[index]
        if quote:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == quote:
                quote = None
            continue
        if char in {"'", '"'}:
            quote = char
        elif char == "(":
            depth += 1
        elif char == ")":
            depth -= 1
            if depth == 0:
                return text[start:index]
    return None


def split_arguments(arguments: str) -> list[str]:
    parts: list[str] = []
    start = 0
    depth = 0
    quote: str | None = None
    escaped = False
    for index, char in enumerate(arguments):
        if quote:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == quote:
                quote = None
            continue
        if char in {"'", '"'}:
            quote = char
        elif char in "([{":
            depth += 1
        elif char in ")]}" :
            depth -= 1
        elif char == "," and depth == 0:
            parts.append(arguments[start:index].strip())
            start = index + 1
    if arguments[start:].strip():
        parts.append(arguments[start:].strip())
    return parts


def count_notifications_without_link() -> int:
    emit = re.compile(r"\.EmitAsync\s*\(")
    unlinked_kinds: set[str] = set()
    for file in files_under(ROOT / "src/Kumunita.Core", "*.cs"):
        if file.name == "NotificationService.cs":
            continue
        text = strip_comments(file.read_text(encoding="utf-8"))
        for match in emit.finditer(text):
            arguments = emit_call_arguments(text, match.end())
            if arguments is None:
                continue
            named_paths = re.findall(
                r"\b(?:linkPath|acceptPath|declinePath)\s*:\s*([^,]+)", arguments
            )
            if named_paths:
                has_link = any(
                    path.strip() not in {"null", '""', "string.Empty"}
                    for path in named_paths
                )
            else:
                positional = split_arguments(arguments)
                has_link = any(
                    path not in {"null", '""', "string.Empty"}
                    for path in positional[6:9]
                )
            if not has_link:
                kind = re.search(r"\bkind\s*:\s*([^,]+)", arguments)
                if kind:
                    unlinked_kinds.add(kind.group(1).strip())
                elif len(positional) > 2:
                    unlinked_kinds.add(positional[2])
    return len(unlinked_kinds)


def measure() -> dict[str, int]:
    empty_plain, empty_none = count_empty_states()
    return {
        "bare_status": count_bare_status(),
        "a11y": count_a11y(),
        "empty_plain": empty_plain,
        "empty_none": empty_none,
        "jargon": count_jargon(),
        "notify_without_link": count_notifications_without_link(),
    }


def report(counts: dict[str, int]) -> None:
    print("IMPROVE-UX measurements (report-only)")
    print(f"{'Category':<24} Count")
    print("-" * 32)
    for category in TRACKED:
        print(f"{category:<24} {counts[category]}")


def check(counts: dict[str, int], baseline_path: Path) -> bool:
    try:
        baseline = json.loads(baseline_path.read_text(encoding="utf-8"))["counts"]
    except (OSError, json.JSONDecodeError, KeyError, TypeError) as error:
        print(f"UX baseline could not be read: {error}", file=sys.stderr)
        return False

    failures = []
    for category in TRACKED:
        limit = baseline.get(category)
        if not isinstance(limit, int) or limit < 0:
            failures.append(f"{category}: invalid or missing baseline ({limit!r})")
        elif counts[category] > limit:
            failures.append(f"{category}: {counts[category]} exceeds baseline {limit}")
    if failures:
        print("UX baseline check failed:", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        return False
    print("UX baseline check passed (no tracked count increased).")
    return True


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="fail if a tracked count exceeds baseline")
    parser.add_argument("--baseline", type=Path, default=BASELINE, help=argparse.SUPPRESS)
    args = parser.parse_args()

    counts = measure()
    report(counts)
    if args.check:
        return 0 if check(counts, args.baseline) else 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
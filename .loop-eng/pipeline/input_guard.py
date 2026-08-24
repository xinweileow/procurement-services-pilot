"""Deterministic input guardrail: flags/strips likely prompt-injection lines
from external documents (uploaded BRDs/FSDs, etc.) before they enter the
KB-build pipeline. Cheap regex tripwire, not a classifier — false negatives
are expected, but it costs zero Copilot invocations and runs at the one
place external content enters this restructure: copilot.md's Kickoff Flow
step 1, before kb-writer.md ever sees the raw upload.

Ported near-verbatim from loopengineering/src/pipeline/input_guard.py (plan
Reference implementations, pre-flight item 14 — carried forward per an
explicit human decision, not silently dropped). Only the entry point
changed: the predecessor called this inline from Python
(doc_upload.save_business_doc); this restructure has no Python-mediated
upload step at all (the human attaches documents directly in Copilot Chat),
so a CLI wrapper is new here — Copilot Chat runs it via its shell tool on
whatever file the upload gets saved to, and hands kb-writer.md the
sanitized output, never the raw file.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

_INJECTION_PATTERNS = [
    re.compile(r"ignore (all |the )?(previous|prior|above) instructions", re.IGNORECASE),
    re.compile(r"disregard (all |the )?(previous|prior|above)", re.IGNORECASE),
    re.compile(r"you are now\b", re.IGNORECASE),
    re.compile(r"new system prompt", re.IGNORECASE),
    re.compile(r"^\s*(system|assistant)\s*:", re.IGNORECASE),
]

REDACTED_MARKER = "[redacted: possible embedded instruction]"


def scan_for_injection(text: str) -> list[str]:
    """Return the lines in `text` that match a known prompt-injection pattern."""
    return [
        line for line in text.splitlines()
        if any(p.search(line) for p in _INJECTION_PATTERNS)
    ]


def sanitize_source_text(text: str) -> tuple[str, list[str]]:
    """Strip flagged lines from `text`, replacing each with an inert marker.
    Returns (sanitized_text, flagged_lines) — flagged_lines is for logging,
    not blocking: this is a BRD/FSD the human is trying to get indexed, so a
    suspicious line is neutralized rather than rejecting the whole upload.
    """
    flagged: list[str] = []
    out_lines: list[str] = []
    for line in text.splitlines():
        if any(p.search(line) for p in _INJECTION_PATTERNS):
            flagged.append(line)
            out_lines.append(REDACTED_MARKER)
        else:
            out_lines.append(line)
    return "\n".join(out_lines), flagged


def main(argv: list[str] | None = None) -> int:
    import argparse

    if sys.platform == "win32":
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(
        description="Sanitize an uploaded document for likely prompt-injection lines "
                    "before it enters KB-build (copilot.md Kickoff Flow step 1).",
    )
    parser.add_argument("path", type=Path, help="Path to the raw uploaded document (plain text/markdown).")
    parser.add_argument("--in-place", action="store_true", help="Overwrite the file with the sanitized version instead of printing it to stdout.")
    args = parser.parse_args(argv)

    text = args.path.read_text(encoding="utf-8", errors="replace")
    sanitized, flagged = sanitize_source_text(text)

    if flagged:
        print(f"input_guard: {len(flagged)} line(s) flagged and redacted in {args.path} "
              f"(sanitized, NOT blocked — relay this to the human, don't silently drop it):", file=sys.stderr)
        for line in flagged:
            print(f"  - {line}", file=sys.stderr)

    if args.in_place:
        args.path.write_text(sanitized, encoding="utf-8")
    else:
        print(sanitized)

    return 1 if flagged else 0


if __name__ == "__main__":
    raise SystemExit(main())

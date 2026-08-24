"""Copilot Chat's entry point for loop-eng's chat-triggered actions (see
.loop-eng/copilot.md). Run from `.loop-eng/` as the working directory, same
convention as task_loop.py's own CLI:

  Kickoff Flow step 1b — sanitize an uploaded document for likely
  prompt-injection lines before it enters KB-build:

      python -m pipeline.main sanitize-doc <path> --in-place

  Kickoff Flow, after the KBs are built — detect which external
  integrations the project needs an MCP server for:

      python -m pipeline.main detect-mcp-requirements <target_dir>

  Kickoff Flow, once the human has filled in API keys in that report —
  scaffold the MCP servers:

      python -m pipeline.main setup-mcp <target_dir> --credentials <report_path>

  Kickoff Flow step 4 — once the human approves spec-writer's drafted
  backlog, push it to Jira:

      python -m pipeline.main push-backlog data/backlog/<project_key>-backlog.json

  Escalation Flow option B — retry one escalated ticket from its
  checkpoint's diagnosis instead of a human taking over:

      python -m pipeline.main retry-escalated <target_dir> <ticket-id>

kb-writer/spec-writer (Kickoff steps 1-3) and the decision-brief itself
(Escalation steps 1-3) run as personas directly inside Copilot Chat --
nothing to launch there. These subcommands are the concrete pieces of code
the flows need Copilot Chat to run, via its shell tool, rather than asking
the human to know a pipeline-internal module path or handle credentials
themselves.

Everything else (task_loop.py's run_backlog, polling Jira and driving
tickets through the developer/gate/PR loop unattended) is background
infrastructure, not something a human or Copilot Chat drives directly from
this chat — see copilot.md's framing note at the top of the file. It keeps
its own separate CLI (`python -m pipeline.task_loop <target_dir>`) rather
than living here.
"""
from __future__ import annotations

import sys
from pathlib import Path

from . import input_guard, jira_sync, mcp_requirement, mcp_setup, task_loop


def main(argv: list[str] | None = None) -> int:
    import argparse

    if sys.platform == "win32":
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(
        prog="python -m pipeline.main",
        description="loop-eng's chat-triggered entry point (see .loop-eng/copilot.md). Run with .loop-eng/ as cwd.",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    sanitize = sub.add_parser("sanitize-doc", help="Kickoff Flow step 1b: strip likely prompt-injection lines from an uploaded document before KB-build.")
    sanitize.add_argument("path", type=Path, help="Path to the raw uploaded document.")
    sanitize.add_argument("--in-place", action="store_true", help="Overwrite the file with the sanitized version instead of printing it to stdout.")

    detect = sub.add_parser("detect-mcp-requirements", help="Kickoff Flow, after KBs are built: detect external integrations the project needs.")
    detect.add_argument("target_dir", type=Path, help="Path to the target repo (docs/kb/business_kb.md must already exist).")

    setup = sub.add_parser("setup-mcp", help="Kickoff Flow, once credentials are filled in: scaffold the detected MCP servers.")
    setup.add_argument("target_dir", type=Path, help="Path to the target repo.")
    setup.add_argument("--credentials", type=Path, required=True, help="Path to the mcp_requirements report YAML with api_key fields filled in.")

    push = sub.add_parser("push-backlog", help="Kickoff Flow step 4: push an approved local backlog JSON to Jira.")
    push.add_argument("backlog_path", type=Path, help="e.g. data/backlog/<project_key>-backlog.json")

    retry = sub.add_parser("retry-escalated", help="Escalation Flow option B: retry one escalated ticket from its checkpoint's diagnosis.")
    retry.add_argument("target_dir", type=Path, help="Path to the target repo (already cloned, .loop-eng vendored in).")
    retry.add_argument("ticket_id", type=str, help="The escalated ticket's Jira key, e.g. SCRUM-42.")

    args = parser.parse_args(argv)

    if args.command == "sanitize-doc":
        text = args.path.read_text(encoding="utf-8", errors="replace")
        sanitized, flagged = input_guard.sanitize_source_text(text)
        if flagged:
            print(f"input_guard: {len(flagged)} line(s) flagged and redacted (sanitized, NOT blocked -- relay this to the human):", file=sys.stderr)
            for line in flagged:
                print(f"  - {line}", file=sys.stderr)
        if args.in_place:
            args.path.write_text(sanitized, encoding="utf-8")
        else:
            print(sanitized)
        return 0

    if args.command == "detect-mcp-requirements":
        report, report_path = mcp_requirement.run_mcp_detection(args.target_dir)
        print(f"detected {len(report.required_integrations)} integration(s): "
              f"{[i.name for i in report.required_integrations]}")
        print(f"report written to {report_path} -- relay this path to the human to fill in API keys.")
        return 0

    if args.command == "setup-mcp":
        if not args.credentials.exists():
            print(f"Credentials file not found: {args.credentials}", file=sys.stderr)
            return 1
        entries = mcp_requirement.load_credentials(args.credentials)
        if not entries:
            print("No credentials with api_key filled in -- nothing to set up.")
            return 0
        results = mcp_setup.setup_from_credentials(entries, args.target_dir)
        print(f"=== {len(results)} MCP server(s) ready. ===")
        return 0

    if args.command == "push-backlog":
        try:
            key_map = jira_sync.push_backlog_to_jira(args.backlog_path)
        except jira_sync.JiraAPIError as e:
            print(e.detail)
            return 1
        print(f"\n=== pushed {len(key_map)} issue(s) to Jira. ===")
        return 0

    if args.command == "retry-escalated":
        outcome = task_loop.retry_escalated_ticket(args.ticket_id, args.target_dir)
        print(f"\n=== {args.ticket_id} retry outcome: {outcome} ===")
        return 0 if outcome in ("done", "in_progress") else 1

    return 1


if __name__ == "__main__":
    raise SystemExit(main())

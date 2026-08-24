"""MCP requirement detection — runs once during Kickoff, right after the KBs
are built from the human's BRD/FSD. Reads the Business + Technical KB and
identifies every external integration the project will need an MCP server
for. Writes a single YAML report the human fills in with API keys, which
`mcp_setup.py` then reads to actually scaffold the servers.

Ported from loopengineering/src/pipeline/mcp_requirement_agent.py (pre-
flight item 14 — carried forward per an explicit human decision, not
silently dropped). The deterministic parts (known-services catalog,
report/credentials YAML shape, catalog matching) port near-verbatim. The
one piece that can't port as-is is `_single_detection`/
`detect_mcp_requirements`: the predecessor calls `anthropic.Anthropic()`
with a forced tool_choice, and this org has no Anthropic API access under
any circumstance (pre-flight item 15). Replaced with a Copilot verdict-
skill invocation (`mcp-requirement-detector.md`,
`copilot_cli.run_verdict_skill`) — same zero-tool-access, fenced-JSON-
verdict pattern as `requirement-reviewer.md`/`structural-reviewer.md`, not
a new mechanism.

Also simplified relative to the predecessor: `run_mcp_detection` here always
reads this restructure's fixed KB locations (`docs/kb/business_kb.md` +
`docs/kb/technical_kb.md` in the target repo — the folder structure every
other skill already assumes) rather than the predecessor's flexible
business_kb_path/technical_kb_path/source_docs signature, which existed to
support KBs that hadn't been standardized to one location yet.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

import yaml

from . import copilot_cli
from .config import LOOP_ENG_ROOT

DEFAULT_AGENT = "mcp-requirement-detector"
REQUIRED_SCHEMA_KEYS = ["required_integrations"]

KNOWN_SERVICES: dict[str, dict[str, str]] = {
    "stripe": {
        "what": "Payment processing API",
        "where": "https://dashboard.stripe.com/apikeys",
        "env_var": "STRIPE_API_KEY",
    },
    "sendgrid": {
        "what": "Email delivery API",
        "where": "https://app.sendgrid.com/settings/api_keys",
        "env_var": "SENDGRID_API_KEY",
    },
    "slack": {
        "what": "Messaging / webhook integration",
        "where": "https://api.slack.com/apps -> OAuth & Permissions -> Bot Token",
        "env_var": "SLACK_BOT_TOKEN",
    },
    "twilio": {
        "what": "SMS / voice API",
        "where": "https://console.twilio.com -> Account SID & Auth Token",
        "env_var": "TWILIO_AUTH_TOKEN",
    },
    "aws s3": {
        "what": "Cloud file/object storage",
        "where": "AWS Console -> IAM -> Security Credentials -> Access Keys",
        "env_var": "AWS_SECRET_ACCESS_KEY",
    },
    "google drive": {
        "what": "Cloud file storage",
        "where": "Google Cloud Console -> APIs & Services -> Credentials",
        "env_var": "GOOGLE_DRIVE_API_KEY",
    },
    "jira": {
        "what": "Issue tracking API",
        "where": "https://id.atlassian.com/manage-profile/security/api-tokens",
        "env_var": "JIRA_API_TOKEN",
    },
    "github": {
        "what": "Code repository / issue tracking API",
        "where": "https://github.com/settings/tokens",
        "env_var": "GITHUB_TOKEN",
    },
    "supabase": {
        "what": "Database / auth / storage backend",
        "where": "Supabase Dashboard -> Settings -> API -> anon/service_role key",
        "env_var": "SUPABASE_SERVICE_ROLE_KEY",
    },
}


@dataclass
class RequiredIntegration:
    name: str
    reason: str = ""


@dataclass
class McpRequirementReport:
    required_integrations: list[RequiredIntegration] = field(default_factory=list)


def load_catalog(catalog_path: Path) -> dict[str, str]:
    if not catalog_path.exists():
        return {}
    data = yaml.safe_load(catalog_path.read_text(encoding="utf-8")) or {}
    return {name: (entry or {}).get("description", "") for name, entry in data.items()}


def _parse_integrations(data: dict) -> list[RequiredIntegration]:
    integrations = []
    for i in data.get("required_integrations", []):
        if isinstance(i, str):
            integrations.append(RequiredIntegration(name=i))
        elif isinstance(i, dict):
            integrations.append(RequiredIntegration(name=i.get("name", ""), reason=i.get("reason", "")))
    return integrations


def detect_mcp_requirements(
    business_kb: str,
    technical_kb: str,
    catalog: dict[str, str],
    cwd: Path,
    agent: str = DEFAULT_AGENT,
) -> McpRequirementReport:
    """One Copilot verdict-skill invocation — no retry-and-merge across
    multiple passes the way the predecessor did to compensate for a
    non-deterministic *local* model; `copilot_cli.run_verdict_skill` already
    has its own retry-until-valid-json fallback for format failures, and
    Phase 3's human-advisory review is this project's answer to
    semantically-wrong-but-valid output in general, not something this
    skill needs to work around itself."""
    catalog_text = (
        "\n".join(f"- {name}: {desc}" for name, desc in catalog.items())
        or "(none connected yet)"
    )
    message = (
        f"MCP servers already connected in this workspace:\n{catalog_text}\n\n"
        f"--- BUSINESS KB ---\n{business_kb}\n\n"
        f"--- TECHNICAL KB ---\n{technical_kb}"
    )
    data = copilot_cli.run_verdict_skill(agent, message, REQUIRED_SCHEMA_KEYS, cwd=cwd)
    return McpRequirementReport(required_integrations=_parse_integrations(data))


def _match_catalog(name: str, catalog: dict[str, str]) -> str | None:
    needle = name.strip().lower()
    for catalog_name in catalog:
        haystack = catalog_name.lower()
        if needle == haystack or needle in haystack or haystack in needle:
            return catalog_name
    return None


def _lookup_service(name: str) -> dict[str, str] | None:
    needle = name.strip().lower()
    for service_name, info in KNOWN_SERVICES.items():
        if needle == service_name or needle in service_name or service_name in needle:
            return info
    return None


def write_report(report: McpRequirementReport, path: Path, catalog: dict[str, str] | None = None) -> Path:
    """Write a single YAML file that serves as both the human-readable
    report and the credentials template. The human reads it, pastes API
    keys into the `api_key` fields, then feeds this same file to
    mcp_setup.py."""
    catalog = catalog or {}
    connected: list[dict] = []
    needs_setup: list[dict] = []

    for integration in report.required_integrations:
        match = _match_catalog(integration.name, catalog)
        if match:
            connected.append({"name": match, "reason": integration.reason or ""})
            continue
        service = _lookup_service(integration.name)
        if service:
            needs_setup.append({
                "name": integration.name, "what": service["what"],
                "reason": integration.reason or "(detected from project KBs)",
                "where_to_get_key": service["where"], "env_var": service["env_var"], "api_key": "",
            })
        else:
            env_key = f"{integration.name.upper().replace('-', '_').replace(' ', '_')}_API_KEY"
            needs_setup.append({
                "name": integration.name, "what": integration.name,
                "reason": integration.reason or "(detected from project KBs)",
                "where_to_get_key": "Check the service's developer portal / API settings",
                "env_var": env_key, "api_key": "",
            })

    header_lines = [
        "# ============================================================",
        "#  MCP Requirement Report",
        f"#  Generated: {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}",
        "# ============================================================",
        "#",
    ]
    if not needs_setup and not connected:
        header_lines.append("#  No external integrations detected in the project KBs.")
    else:
        if needs_setup:
            header_lines += [
                "#  ACTION REQUIRED:",
                "#    1. For each integration below, go to 'where_to_get_key'",
                "#    2. Copy your API key",
                "#    3. Paste it into the 'api_key' field (inside quotes)",
                "#    4. Leave api_key empty to skip that integration",
                "#",
                "#  When done, from .loop-eng/ run:",
                f"#    python -m pipeline.main setup-mcp <target_dir> {path}",
            ]
        if connected:
            header_lines.append("#")
            header_lines.append(f"#  Already connected: {', '.join(c['name'] for c in connected)}")
    header_lines += ["#", "# ============================================================", ""]

    path.parent.mkdir(parents=True, exist_ok=True)
    body: dict = {}
    if needs_setup:
        body["needs_setup"] = needs_setup
    if connected:
        body["already_connected"] = connected

    content = "\n".join(header_lines) + "\n"
    content += yaml.dump(body, sort_keys=False, default_flow_style=False) if body else "# (nothing to configure)\n"
    path.write_text(content, encoding="utf-8")
    return path


def load_credentials(path: Path) -> list[dict]:
    """Load a filled-in report YAML. Returns only entries from needs_setup
    that have a non-empty api_key."""
    data = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    entries = data.get("needs_setup", [])
    return [e for e in entries if e.get("api_key")]


def run_mcp_detection(
    target_dir: Path,
    catalog_path: Path | None = None,
    report_dir: Path | None = None,
    agent: str = DEFAULT_AGENT,
) -> tuple[McpRequirementReport, Path]:
    """Top-level entry point (copilot.md Kickoff Flow, new step between
    kb-writer and spec-writer): reads the just-built KBs from their fixed
    location in the target repo, detects integrations via one Copilot
    verdict-skill call, and writes the report+credentials-template YAML.

    `catalog_path` defaults to `<target_dir>/docs/mcp_catalog.yaml` (project
    metadata, committed alongside docs/kb/ — not pipeline run-state, so not
    under the gitignored `.loop-eng/data/`/`.loop-eng/.loop/`). `report_dir`
    defaults to `.loop-eng/.loop/` (this pipeline's own live-workflow-state
    directory, gitignored, same convention as escalation checkpoints) — the
    filled-in report is a live human-in-the-loop artifact, not something to
    commit.
    """
    catalog_path = catalog_path or (target_dir / "docs" / "mcp_catalog.yaml")
    report_dir = report_dir or (LOOP_ENG_ROOT / ".loop")

    business_kb = (target_dir / "docs" / "kb" / "business_kb.md").read_text(encoding="utf-8")
    technical_kb_path = target_dir / "docs" / "kb" / "technical_kb.md"
    technical_kb = technical_kb_path.read_text(encoding="utf-8") if technical_kb_path.exists() else ""

    catalog = load_catalog(catalog_path)
    report = detect_mcp_requirements(business_kb, technical_kb, catalog, cwd=target_dir, agent=agent)

    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    report_path = write_report(report, report_dir / f"mcp_requirements-{timestamp}.yaml", catalog=catalog)
    return report, report_path

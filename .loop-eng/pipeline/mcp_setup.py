"""MCP setup — reads the filled-in credentials YAML `mcp_requirement.py`
wrote, creates MCP server directories (index.js + package.json) following
the MCP protocol, saves env vars, and updates the target repo's MCP
catalog. Entirely deterministic — no Copilot/LLM call anywhere in this
module.

Ported from loopengineering/src/pipeline/mcp_setup_agent.py (pre-flight
item 14 — carried forward per an explicit human decision). Adjustments from
the predecessor:
- Every path is `target_dir`-relative rather than a single global
  `REPO_ROOT`, matching this whole restructure's convention (target_repo.py,
  worktree.py, test_agent.py) — `.loop-eng` gets vendored into a different
  target repo each time, so there is no one fixed repo root.
- `mcp_servers/` and `.env.local` live at the TARGET repo's own root, not
  under `.loop-eng` — these are the actual project's dev-time tooling and
  runtime credentials, not this pipeline's own Jira/GitHub credentials
  (which `.loop-eng/.env.local` is reserved for — see config.py).
- Registration: the predecessor shells out to `claude mcp add` unconditionally
  (Claude Code's own CLI). This restructure runs on Copilot CLI, and no
  Phase 0 spike has confirmed whether/how Copilot CLI registers local MCP
  servers — inventing a `copilot mcp add` syntax here would risk shipping a
  plausible-but-wrong command. `_register_mcp_server` now tries `claude`
  (kept, since a dev machine may still have it installed as a coding tool
  even though this pipeline itself runs on Copilot) and otherwise prints
  the manual registration path instead of guessing at Copilot's own syntax.
  Flagged as a real gap, not silently resolved.
"""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path
from textwrap import dedent

import yaml

from .mcp_requirement import load_credentials

INDEX_JS_TEMPLATE = dedent("""\
    import 'dotenv/config';
    import {{ z }} from 'zod';
    import {{ McpServer }} from '@modelcontextprotocol/sdk/server/mcp.js';
    import {{ StdioServerTransport }} from '@modelcontextprotocol/sdk/server/stdio.js';

    for (const key of {env_vars_js}) {{
      if (!process.env[key]) {{
        console.error(`Missing required environment variable: ${{key}}`);
        process.exit(1);
      }}
    }}

    const server = new McpServer({{
      name: '{server_name}',
      version: '1.0.0',
    }});

    // TODO: Register tools that wrap the {service_name} API.
    // Example:
    //
    //   server.tool(
    //     'exampleTool',
    //     'Description of what this tool does',
    //     {{
    //       param: z.string().describe('Parameter description'),
    //     }},
    //     async ({{ param }}) => {{
    //       // Call the {service_name} API using process.env.{primary_env_var}
    //       return {{
    //         content: [{{ type: 'text', text: JSON.stringify(result, null, 2) }}],
    //       }};
    //     }},
    //   );

    async function main() {{
      const transport = new StdioServerTransport();
      await server.connect(transport);
      console.error('{server_name} MCP Server is running');
    }}

    main().catch((err) => {{
      console.error('Fatal error:', err);
      process.exit(1);
    }});
""")

PACKAGE_JSON_TEMPLATE = dedent("""\
    {{
      "name": "{server_name}",
      "version": "1.0.0",
      "description": "MCP server for {service_name}",
      "main": "index.js",
      "scripts": {{
        "start": "node index.js"
      }},
      "type": "module",
      "dependencies": {{
        "@modelcontextprotocol/sdk": "^1.30.0",
        "dotenv": "^17.4.2",
        "zod": "^4.4.3"
      }}
    }}
""")


def _to_server_name(name: str) -> str:
    return name.strip().lower().replace(" ", "-").replace("_", "-") + "-mcp"


def create_mcp_server(name: str, env_var: str, servers_dir: Path) -> Path:
    """Create an MCP server directory with index.js and package.json
    following the MCP protocol."""
    server_name = _to_server_name(name)
    server_dir = servers_dir / server_name
    server_dir.mkdir(parents=True, exist_ok=True)

    env_vars_js = f"['{env_var}']"
    (server_dir / "index.js").write_text(
        INDEX_JS_TEMPLATE.format(env_vars_js=env_vars_js, server_name=server_name, service_name=name, primary_env_var=env_var),
        encoding="utf-8",
    )
    (server_dir / "package.json").write_text(
        PACKAGE_JSON_TEMPLATE.format(server_name=server_name, service_name=name), encoding="utf-8",
    )
    return server_dir


def update_catalog(server_name: str, description: str, catalog_path: Path) -> None:
    catalog = yaml.safe_load(catalog_path.read_text(encoding="utf-8")) if catalog_path.exists() else {}
    catalog = catalog or {}
    catalog[server_name] = {"description": description}
    catalog_path.parent.mkdir(parents=True, exist_ok=True)
    catalog_path.write_text(yaml.dump(catalog, sort_keys=False, default_flow_style=False), encoding="utf-8")


def save_env_vars(env_vars: dict[str, str], env_path: Path) -> None:
    """Append credentials to the target repo's own .env.local (gitignored —
    see SETUP.md step 2)."""
    existing = env_path.read_text(encoding="utf-8") if env_path.exists() else ""
    new_lines = [f"{key}={value}" for key, value in env_vars.items() if f"{key}=" not in existing]
    if new_lines:
        with env_path.open("a", encoding="utf-8") as f:
            f.write("\n".join([""] + new_lines + [""]))


def _run_npm_install(server_dir: Path) -> bool:
    npm = shutil.which("npm")
    if not npm:
        print(f"    npm not found -- run 'npm install' in {server_dir} manually")
        return False
    result = subprocess.run([npm, "install"], cwd=str(server_dir), capture_output=True, text=True, timeout=120)
    if result.returncode == 0:
        print("    npm install done")
        return True
    print(f"    npm install failed: {result.stderr[:200]}")
    return False


def _register_mcp_server(server_name: str, server_dir: Path) -> bool:
    """Best-effort only -- see module docstring. Tries `claude mcp add`
    (Claude Code, if present on this dev machine); Copilot CLI's own
    registration mechanism, if any, is unconfirmed and not guessed at here."""
    claude = shutil.which("claude")
    if not claude:
        print("    no known MCP-registration CLI found on PATH -- register manually with whatever "
              f"tool reads this workspace's MCP config, pointing it at: node {server_dir / 'index.js'}")
        return False
    result = subprocess.run(
        [claude, "mcp", "add", server_name, "--", "node", str(server_dir / "index.js")],
        capture_output=True, text=True, timeout=30,
    )
    if result.returncode == 0:
        print("    registered with claude mcp")
        return True
    print(f"    claude mcp add failed: {result.stderr[:200]}")
    return False


def setup_from_credentials(credentials: list[dict], target_dir: Path) -> list[Path]:
    """Full automated setup: create files, npm install, save env vars,
    update the catalog, best-effort register."""
    servers_dir = target_dir / "mcp_servers"
    catalog_path = target_dir / "docs" / "mcp_catalog.yaml"
    env_path = target_dir / ".env.local"

    results = []
    for entry in credentials:
        name, env_var, api_key = entry["name"], entry["env_var"], entry["api_key"]
        server_dir = create_mcp_server(name, env_var, servers_dir=servers_dir)
        server_name = _to_server_name(name)
        description = entry.get("what", name)

        print(f"  [{server_name}]")
        print(f"    created {server_dir}/index.js")
        _run_npm_install(server_dir)
        save_env_vars({env_var: api_key}, env_path)
        print(f"    {env_var} saved to {env_path}")
        update_catalog(server_name, description, catalog_path)
        print("    catalog updated")
        _register_mcp_server(server_name, server_dir)
        print()
        results.append(server_dir)

    return results


def main(argv: list[str] | None = None) -> int:
    if sys.platform == "win32":
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(description="Set up MCP servers from a filled-in credentials YAML.")
    parser.add_argument("target_dir", type=Path, help="Path to the target repo.")
    parser.add_argument("--credentials", type=Path, required=True, help="Path to the mcp_requirements YAML with api_key fields filled in.")
    args = parser.parse_args(argv)

    if not args.credentials.exists():
        print(f"Credentials file not found: {args.credentials}", file=sys.stderr)
        return 1

    entries = load_credentials(args.credentials)
    if not entries:
        print("No credentials with api_key filled in -- nothing to set up.")
        return 0

    print(f"=== Setting up {len(entries)} MCP server(s) ===\n")
    results = setup_from_credentials(entries, args.target_dir)
    print(f"=== Done. {len(results)} MCP server(s) ready. ===")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

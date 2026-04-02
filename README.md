# MergeButler

Automated PR approval CLI tool. MergeButler reads a YAML configuration file, evaluates pull requests against defined rules, and auto-approves qualifying PRs on GitHub or Azure DevOps.

## Installation

```bash
dotnet build
```

## Usage

### `evaluate`

Evaluate a pull request against configured rules and optionally approve it.

```bash
MergeButler evaluate --config .mergebutler.yml --pr <PR_URL> --platform <GitHub|AzureDevOps> [--token <TOKEN>] [--dry-run]
```

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--config` | `-c` | Yes | Path to the YAML configuration file |
| `--pr` | | Yes | URL of the pull request to evaluate |
| `--platform` | `-p` | Yes | `GitHub` or `AzureDevOps` |
| `--token` | `-t` | No | Auth token. Defaults to `GITHUB_TOKEN` or `AZURE_DEVOPS_TOKEN` env var |
| `--dry-run` | `-n` | No | Evaluate without submitting an approval |

#### Examples

```bash
# GitHub PR
MergeButler evaluate -c .mergebutler.yml --pr https://github.com/owner/repo/pull/42 -p GitHub

# Azure DevOps PR
MergeButler evaluate -c .mergebutler.yml --pr https://dev.azure.com/org/project/_git/repo/pullrequest/1 -p AzureDevOps -t $AZURE_DEVOPS_TOKEN

# Dry run — see if a PR would be approved without actually approving it
MergeButler evaluate -c .mergebutler.yml --pr https://github.com/owner/repo/pull/42 -p GitHub --dry-run
```

### `config`

View or modify MergeButler configuration (exclusions and rules).

#### `config show`

Display the effective merged configuration from user and repo levels, showing where each exclusion and rule comes from.

```bash
MergeButler config show
```

Example output:

```
Exclusions:
  [user] "DO NOT AUTO-APPROVE" (target: title)
  [repo] "\[manual review\]" (target: both)

Rules:
  [user] "Documentation only" (fileGlob: **/*.md, docs/**, LICENSE*)
  [repo] "CI/CD configuration" (fileGlob: .github/**, **/*.yml, **/*.yaml)
  [user] "Safe dependency updates" (agentic: Approve this PR if it only updates...)

User config:  C:\Users\you\.mergebutler\config.yaml
Repo config:  D:\Repos\my-project\.mergebutler\config.yaml
```

#### `config set-exclusion`

Add or update an exclusion pattern.

```bash
MergeButler config set-exclusion <pattern> [--target <Title|Description|Both>] [--scope <User|Repo>]
```

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `<pattern>` | | Yes | The exclusion pattern (positional argument) |
| `--target` | | No | What to match: `Title`, `Description`, or `Both` (default: `Both`) |
| `--scope` | `-s` | No | Where to save: `User` or `Repo` (default: `Repo`) |

```bash
# Add an exclusion to the repo config
MergeButler config set-exclusion "DO NOT MERGE" --target title

# Add an exclusion to the user config
MergeButler config set-exclusion "\\[wip\\]" --target both --scope User
```

#### `config set-rule file`

Add or update a file glob rule.

```bash
MergeButler config set-rule file <name> --patterns <glob>... [--scope <User|Repo>]
```

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `<name>` | | Yes | Rule name / unique identifier (positional argument) |
| `--patterns` | | Yes | Glob patterns to match files |
| `--scope` | `-s` | No | Where to save: `User` or `Repo` (default: `Repo`) |

```bash
MergeButler config set-rule file "Docs only" --patterns "**/*.md" --patterns "docs/**"
```

#### `config set-rule agent`

Add or update an agentic rule.

```bash
MergeButler config set-rule agent <name> --prompt <text> [--scope <User|Repo>]
```

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `<name>` | | Yes | Rule name / unique identifier (positional argument) |
| `--prompt` | | Yes | Evaluation prompt for the agentic rule |
| `--scope` | `-s` | No | Where to save: `User` or `Repo` (default: `Repo`) |

```bash
MergeButler config set-rule agent "Safe deps" --prompt "Approve if only dependency versions changed" --scope User
```

## Configuration

### Tiered Configuration

MergeButler supports tiered configuration that merges settings from two levels:

1. **User level**: `~/.mergebutler/config.yaml` — personal defaults applied to all repos.
2. **Repo level**: `{repoRoot}/.mergebutler/config.yaml` — per-repo overrides.

When both exist, repo-level items take precedence:
- **Exclusions** are merged by pattern — a repo exclusion with the same pattern replaces the user one.
- **Rules** are merged by name — a repo rule with the same name replaces the user one.

### Configuration File Format

Create a `.mergebutler.yml` file at the root of your repository. See [.mergebutler.yml](.mergebutler.yml) for a complete example.

### Exclusions

Exclusions are checked **first**. If any pattern matches the PR title or description, the PR is skipped entirely.

```yaml
exclusions:
  - pattern: "DO NOT AUTO-APPROVE"
    target: title           # title | description | both
  - pattern: "\\[manual review\\]"
    target: both
```

### Rules

Rules use **OR logic** — if any rule matches, the PR is approved.

#### File Glob Rules

Approve when **all** changed files match the configured glob patterns (strict mode).

```yaml
rules:
  - name: "Documentation only"
    type: fileGlob
    patterns:
      - "**/*.md"
      - "docs/**"
```

#### Agentic Rules

Use [GitHub Copilot](https://github.com/features/copilot) via the [Copilot SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK) to evaluate the PR diff against a prompt.

```yaml
rules:
  - name: "Safe dependency updates"
    type: agentic
    prompt: >
      Approve if this PR only updates package versions
      and does not change application logic.
```

## Evaluation Flow

1. Load and validate the YAML configuration
2. Fetch PR info (title, description, changed files, diff) from the platform
3. Check exclusions — if any match, exit without approving
4. Evaluate rules (OR logic) — first matching rule triggers approval
5. Submit approval via the platform API (skipped in `--dry-run` mode)

## Supported Platforms

| Platform | Library | Token env var |
|----------|---------|---------------|
| GitHub | [Octokit](https://github.com/octokit/octokit.net) | `GITHUB_TOKEN` |
| Azure DevOps | REST API | `AZURE_DEVOPS_TOKEN` |

## MCP Server (Local Development)

MergeButler includes an MCP (Model Context Protocol) server for interactive use with AI assistants like GitHub Copilot.

```bash
MergeButler mcp
```

This starts a stdio-based MCP server exposing the following tools:

| Tool | Description |
|------|-------------|
| `grade_pull_request` | Evaluates a PR against your rules and returns a detailed report |
| `approve_pull_request` | Submits an approval on a PR via the platform API |
| `get_config` | Returns the effective merged configuration with source annotations |
| `set_exclusion` | Adds or updates an exclusion at user or repo level |
| `set_rule` | Adds or updates a rule at user or repo level |

### VS Code / Copilot Chat Configuration

Add to your `.vscode/mcp.json`:

```json
{
  "servers": {
    "mergebutler": {
      "command": "dotnet",
      "args": ["run", "--project", "MergeButler", "--", "mcp"]
    }
  }
}
```

PR tools accept `prUrl`, `platform`, and an optional `token` parameter. If no token is provided, the tools check environment variables (`GITHUB_TOKEN` / `AZURE_DEVOPS_TOKEN`) and return a descriptive error if none is found.

### `setup`

Set up mergiraf, configure Git for structural merging, and install the resolve-conflicts Copilot skill into the current repository.

```bash
MergeButler setup [--yes]
```

| Option | Alias | Description |
|--------|-------|-------------|
| `--yes` | `-y` | Skip all prompts and perform every step automatically |

The setup command walks you through these steps interactively (or runs them all with `--yes`):

1. **Install mergiraf** — Detects your OS and available package manager (Homebrew on macOS, Scoop on Windows, Cargo as fallback) and installs [mergiraf](https://mergiraf.org), a structural merge tool.
2. **Configure diff3** — Sets `merge.conflictStyle = diff3` globally so conflict markers include the common ancestor.
3. **Enable rerere** — Enables `rerere.enabled = true` globally so Git remembers conflict resolutions.
4. **Register merge driver** — Adds the mergiraf merge driver to your global Git config.
5. **Global git attributes** — Adds `* merge=mergiraf` to `~/.config/git/attributes` so all files are routed through mergiraf.
6. **Install skill** — Copies the resolve-conflicts Copilot skill to `.github/skills/resolve-conflicts/` in the current repo.

#### Examples

```bash
# Interactive setup — prompts for each step
MergeButler setup

# Non-interactive — perform all steps automatically
MergeButler setup --yes
```
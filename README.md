# MergeButler

Automated PR approval CLI tool. MergeButler reads a YAML configuration file, evaluates pull requests against defined rules, and auto-approves qualifying PRs on GitHub or Azure DevOps.

## Installation

```bash
dotnet build
```

## Usage

```bash
MergeButler evaluate --config .mergebutler.yml --pr <PR_URL> --platform <GitHub|AzureDevOps> [--token <TOKEN>]
```

### Options

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--config` | `-c` | Yes | Path to the YAML configuration file |
| `--pr` | | Yes | URL of the pull request to evaluate |
| `--platform` | `-p` | Yes | `GitHub` or `AzureDevOps` |
| `--token` | `-t` | No | Auth token. Defaults to `GITHUB_TOKEN` or `AZURE_DEVOPS_TOKEN` env var |

### Examples

```bash
# GitHub PR
MergeButler evaluate -c .mergebutler.yml --pr https://github.com/owner/repo/pull/42 -p GitHub

# Azure DevOps PR
MergeButler evaluate -c .mergebutler.yml --pr https://dev.azure.com/org/project/_git/repo/pullrequest/1 -p AzureDevOps -t $AZURE_DEVOPS_TOKEN
```

## Configuration

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
5. Submit approval via the platform API

## Supported Platforms

- **GitHub** — uses [Octokit](https://github.com/octokit/octokit.net)
- **Azure DevOps** — uses the REST API directly

## MCP Server (Local Development)

MergeButler includes an MCP (Model Context Protocol) server for interactive use with AI assistants like GitHub Copilot.

```bash
MergeButler mcp
```

This starts a stdio-based MCP server exposing two tools:

| Tool | Description |
|------|-------------|
| `grade_pull_request` | Evaluates a PR against your rules and returns a detailed report |
| `approve_pull_request` | Submits an approval on a PR via the platform API |

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

Both tools accept `prUrl`, `platform`, and an optional `token` parameter. If no token is provided, the tools check environment variables (`GITHUB_TOKEN` / `AZURE_DEVOPS_TOKEN`) and return a descriptive error if none is found.
---
name: doc-writer
description: Guidelines for producing accurate and maintainable documentation for the MergeButler CLI tool. Use when writing or updating the README, command help text, or any user-facing documentation.
---

# Documentation Writer Skill

This skill provides guidelines for AI coding agents to produce accurate, consistent documentation for MergeButler — an automated PR approval CLI tool that evaluates pull requests against configurable rules on GitHub and Azure DevOps.

## Project Overview

MergeButler is a .NET CLI tool with four top-level commands:

- **`evaluate`** — Evaluate a pull request against configured rules and optionally approve it.
- **`config`** — View or modify MergeButler configuration (exclusions and rules).
- **`mcp`** — Start an MCP (Model Context Protocol) server for interactive use with AI assistants.
- **`setup`** — Set up mergiraf, configure Git for structural merging, and install the resolve-conflicts Copilot skill.

### Architecture

```
MergeButler/
├── Commands/           # CLI command definitions (System.CommandLine)
│   ├── EvaluateCommand.cs
│   ├── ConfigCommand.cs
│   ├── McpCommand.cs
│   ├── SetupCommand.cs
│   ├── SkillInstaller.cs
│   ├── PlatformServiceFactory.cs
│   └── Platform.cs
├── Config/             # Configuration models and tiered loading
│   ├── MergeButlerConfig.cs
│   ├── ConfigLoader.cs
│   ├── TieredConfigManager.cs
│   ├── RuleConfig.cs
│   └── ExclusionConfig.cs
├── Rules/              # Rule evaluation engine
│   ├── RuleEngine.cs
│   ├── IRule.cs
│   ├── FileGlobRule.cs
│   ├── AgenticRule.cs
│   └── ExclusionEvaluator.cs
├── PullRequests/       # Platform-specific PR services
│   ├── IPullRequestService.cs
│   ├── GitHubPullRequestService.cs
│   └── AzureDevOpsPullRequestService.cs
├── Mcp/                # MCP server tools
│   ├── PullRequestTools.cs
│   └── ConfigTools.cs
└── Program.cs          # Entry point and command registration
```

## Documentation Conventions

### Voice and Tone

- Use **second person** ("you") when addressing the user.
- Be direct and concise. MergeButler is a CLI tool — users expect terse, actionable docs.
- Prefer imperative mood for instructions: "Run the command" not "You should run the command".
- Use present tense: "MergeButler evaluates the PR" not "MergeButler will evaluate the PR".

### Formatting

- Use fenced code blocks with language tags (`bash`, `yaml`, `json`, `csharp`).
- Use tables for option/parameter reference — follow the existing `| Option | Alias | Required | Description |` pattern.
- Use `##` for major sections, `###` for subsections. Don't skip heading levels.
- Wrap CLI command names and options in backticks: `--dry-run`, `config show`.

### Code Examples

- Always show realistic, runnable examples.
- For CLI examples, use `bash` code blocks.
- Include both GitHub and Azure DevOps examples when documenting platform-specific behavior.
- Omit optional parameters in the simplest example, then show them in follow-up examples.

## CLI Reference

This section is the authoritative reference for all commands. Keep this in sync when commands change.

### `evaluate`

Evaluate a pull request against configured rules and optionally approve it.

```bash
MergeButler evaluate --config <path> --pr <url> --platform <GitHub|AzureDevOps> [--token <token>] [--dry-run]
```

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--config` | `-c` | Yes | Path to the YAML configuration file |
| `--pr` | | Yes | URL of the pull request to evaluate |
| `--platform` | `-p` | Yes | `GitHub` or `AzureDevOps` |
| `--token` | `-t` | No | Auth token. Defaults to `MERGEBUTLER__GITHUB_TOKEN` or `MERGEBUTLER__AZURE_DEVOPS_TOKEN` env var |
| `--dry-run` | `-n` | No | Evaluate without submitting an approval |

### `config show`

Display the effective merged configuration from user and repo levels, showing where each exclusion and rule comes from.

```bash
MergeButler config show
```

No options. Outputs all exclusions and rules with their source (`user` or `repo`).

### `config set-exclusion`

Add or update an exclusion pattern.

```bash
MergeButler config set-exclusion <pattern> [--target <Title|Description|Both>] [--scope <User|Repo>]
```

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `<pattern>` | | Yes | The exclusion pattern (positional argument) |
| `--target` | | No | What to match: `Title`, `Description`, or `Both` (default: `Both`) |
| `--scope` | `-s` | No | Where to save: `User` or `Repo` (default: `Repo`) |

### `config set-rule`

Add or update a rule.

```bash
MergeButler config set-rule <name> --type <FileGlob|Agentic> [--patterns <glob>...] [--prompt <text>] [--scope <User|Repo>]
```

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `<name>` | | Yes | Rule name / unique identifier (positional argument) |
| `--type` | | Yes | `FileGlob` or `Agentic` |
| `--patterns` | | Conditional | Glob patterns (required for `FileGlob` rules) |
| `--prompt` | | Conditional | Evaluation prompt (required for `Agentic` rules) |
| `--scope` | `-s` | No | Where to save: `User` or `Repo` (default: `Repo`) |

### `mcp`

Start the MergeButler MCP server (stdio transport).

```bash
MergeButler mcp
```

Exposes these tools:

| Tool | Description |
|------|-------------|
| `grade_pull_request` | Evaluate a PR against rules and return a detailed report |
| `approve_pull_request` | Submit an approval on a PR |
| `get_config` | Get the effective merged configuration with sources |
| `set_exclusion` | Add or update an exclusion at user or repo level |
| `set_rule` | Add or update a rule at user or repo level |

### `setup`

Set up mergiraf, configure Git for structural merging, and install the resolve-conflicts Copilot skill.

```bash
MergeButler setup [--yes]
```

| Option | Alias | Description |
|--------|-------|-------------|
| `--yes` | `-y` | Skip all prompts and perform every step automatically |

Steps performed:

1. Install mergiraf via detected package manager (brew / scoop / cargo)
2. Configure `merge.conflictStyle = diff3`
3. Enable `rerere.enabled = true`
4. Register the mergiraf merge driver in global git config
5. Add `* merge=mergiraf` to global git attributes
6. Install the resolve-conflicts Copilot skill to `.github/skills/resolve-conflicts/`

## Configuration

### Tiered Configuration

MergeButler supports tiered configuration that merges settings from two levels:

1. **User level**: `~/.mergebutler/config.yaml` — personal defaults applied to all repos.
2. **Repo level**: `{repoRoot}/.mergebutler/config.yaml` — per-repo overrides.

When both exist, repo-level items take precedence:
- **Exclusions** are merged by pattern — a repo exclusion with the same pattern replaces the user one.
- **Rules** are merged by name — a repo rule with the same name replaces the user one.

### Configuration File Format

```yaml
exclusions:
  - pattern: "DO NOT AUTO-APPROVE"
    target: title           # title | description | both
  - pattern: "\\[manual review\\]"
    target: both

rules:
  - name: "Documentation only"
    type: fileGlob
    patterns:
      - "**/*.md"
      - "docs/**"
      - "LICENSE*"

  - name: "Safe dependency updates"
    type: agentic
    prompt: >
      Approve this PR if it only updates package/dependency versions
      and does not change any application logic or behavior.
```

### Exclusions

Exclusions are checked **first**. If any pattern matches, the PR is skipped entirely.

- `pattern` — Regex pattern to match.
- `target` — What to match against: `title`, `description`, or `both` (default).

### Rules

Rules use **OR logic** — the first matching rule triggers approval.

#### FileGlob Rules

Approve when **all** changed files match at least one of the configured glob patterns.

- `name` — Unique identifier for the rule.
- `type` — Must be `fileGlob`.
- `patterns` — List of glob patterns.

#### Agentic Rules

Use GitHub Copilot to evaluate the PR diff against a natural-language prompt.

- `name` — Unique identifier for the rule.
- `type` — Must be `agentic`.
- `prompt` — The evaluation prompt sent to the LLM.

## Supported Platforms

| Platform | Library | Token env var |
|----------|---------|---------------|
| GitHub | [Octokit](https://github.com/octokit/octokit.net) | `MERGEBUTLER__GITHUB_TOKEN` |
| Azure DevOps | REST API | `MERGEBUTLER__AZURE_DEVOPS_TOKEN` |

## Documentation Maintenance

When updating documentation:

1. **Keep the README in sync** — The README is the primary user-facing doc. Update it whenever CLI commands, options, or behavior change.
2. **Update this skill** — When adding commands or features, update the CLI Reference section above so the skill stays accurate.
3. **Show before and after** — When documenting behavioral changes, show what the old behavior was and what it is now.
4. **Test examples** — Verify that CLI examples actually work before committing them.

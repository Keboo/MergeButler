#!/bin/bash
# Categorize conflicted files by resolution strategy.
#
# Usage: categorize-conflicts.sh
#
# Output format (tab-separated, one per line):
#   <category>\t<file_path>
#
# Categories:
#   lockfile   - Package lock files (regenerated after resolution)
#   migration  - Database migration files (flag for user)
#   mergiraf   - Files in languages mergiraf can structurally merge
#   other      - Everything else (AI-assisted resolution)

is_mergiraf_supported() {
    local file="$1"
    local ext="${file##*.}"
    local name="${file##*/}"

    case "$name" in
        go.mod|go.sum|go.work.sum|pyproject.toml) return 0 ;;
        Makefile|GNUmakefile|BUILD|WORKSPACE|CMakeLists.txt) return 0 ;;
    esac

    case "$ext" in
        java|properties|kt|rs|go) return 0 ;;
        ini) return 0 ;;
        js|jsx|mjs|json|yml|yaml|toml) return 0 ;;
        html|htm|xhtml|xml) return 0 ;;
        c|h|cc|hh|cpp|hpp|cxx|hxx|mpp|cppm|ixx|tcc) return 0 ;;
        cs|dart|dts|scala|sbt|ts|tsx) return 0 ;;
        py|php|phtml|php3|php4|php5|phps|phpt) return 0 ;;
        sol|lua|rb|ex|exs|nix) return 0 ;;
        sv|svh|md|hcl|tf|tfvars) return 0 ;;
        ml|mli|hs) return 0 ;;
        mk|bzl|bazel|cmake) return 0 ;;
    esac
    return 1
}

is_lockfile() {
    local name="${1##*/}"
    case "$name" in
        package-lock.json|yarn.lock|pnpm-lock.yaml|Cargo.lock|poetry.lock|Gemfile.lock|composer.lock|bun.lockb|bun.lock|packages.lock.json)
            return 0
            ;;
    esac
    return 1
}

is_migration() {
    local file="$1"
    case "$file" in
        migrations/*|*/migrations/*) return 0 ;;
        alembic/versions/*|*/alembic/versions/*) return 0 ;;
        db/migrate/*|*/db/migrate/*) return 0 ;;
    esac
    return 1
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    set -euo pipefail
    git rev-parse --git-dir >/dev/null 2>&1 || {
        echo "Error: not in a git repository" >&2
        exit 1
    }

    while IFS= read -r -d '' file; do
        if is_lockfile "$file"; then
            printf "lockfile\t%s\n" "$file"
        elif is_migration "$file"; then
            printf "migration\t%s\n" "$file"
        elif is_mergiraf_supported "$file"; then
            printf "mergiraf\t%s\n" "$file"
        else
            printf "other\t%s\n" "$file"
        fi
    done < <(git diff --name-only --diff-filter=U -z 2>/dev/null)
fi

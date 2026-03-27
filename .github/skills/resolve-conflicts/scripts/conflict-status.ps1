# Detect the current git conflict context by reading git internals.
#
# Usage: conflict-status.ps1
#
# Output format (tab-separated):
#   <context>\t<progress>\t<branch>

$ErrorActionPreference = 'Stop'

$gitDir = git rev-parse --git-dir 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Not in a git repository"
    exit 1
}

$branch = git branch --show-current 2>$null
if (-not $branch) { $branch = "" }

if ((Test-Path "$gitDir/rebase-merge") -or (Test-Path "$gitDir/rebase-apply")) {
    $context = "rebase"

    if (Test-Path "$gitDir/rebase-merge") {
        $rebaseDir = "$gitDir/rebase-merge"
    } else {
        $rebaseDir = "$gitDir/rebase-apply"
    }

    $current = ""
    $total = ""
    if (Test-Path "$rebaseDir/msgnum") { $current = (Get-Content "$rebaseDir/msgnum" -Raw).Trim() }
    elseif (Test-Path "$rebaseDir/next") { $current = (Get-Content "$rebaseDir/next" -Raw).Trim() }
    if (Test-Path "$rebaseDir/end") { $total = (Get-Content "$rebaseDir/end" -Raw).Trim() }
    elseif (Test-Path "$rebaseDir/last") { $total = (Get-Content "$rebaseDir/last" -Raw).Trim() }

    if (Test-Path "$rebaseDir/head-name") {
        $branch = (Get-Content "$rebaseDir/head-name" -Raw).Trim() -replace '^refs/heads/', ''
    }

    if ($current -and $total) {
        Write-Output "$context`t$current/$total`t$branch"
    } else {
        Write-Output "$context`t`t$branch"
    }
} elseif (Test-Path "$gitDir/MERGE_HEAD") {
    Write-Output "merge`t`t$branch"
} elseif (Test-Path "$gitDir/CHERRY_PICK_HEAD") {
    Write-Output "cherry-pick`t`t$branch"
} elseif (Test-Path "$gitDir/REVERT_HEAD") {
    Write-Output "revert`t`t$branch"
} else {
    Write-Output "none`t`t$branch"
}

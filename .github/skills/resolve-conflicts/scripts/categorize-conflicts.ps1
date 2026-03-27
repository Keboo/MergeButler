# Categorize conflicted files by resolution strategy.
#
# Usage: categorize-conflicts.ps1
#
# Output format (tab-separated, one per line):
#   <category>\t<file_path>

$ErrorActionPreference = 'Stop'

$null = git rev-parse --git-dir 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Not in a git repository"
    exit 1
}

$mergirafExtensions = @(
    'java','properties','kt','rs','go','ini',
    'js','jsx','mjs','json','yml','yaml','toml',
    'html','htm','xhtml','xml',
    'c','h','cc','hh','cpp','hpp','cxx','hxx','mpp','cppm','ixx','tcc',
    'cs','dart','dts','scala','sbt','ts','tsx',
    'py','php','phtml','php3','php4','php5','phps','phpt',
    'sol','lua','rb','ex','exs','nix',
    'sv','svh','md','hcl','tf','tfvars',
    'ml','mli','hs',
    'mk','bzl','bazel','cmake'
)

$mergirafNames = @(
    'go.mod','go.sum','go.work.sum','pyproject.toml',
    'Makefile','GNUmakefile','BUILD','WORKSPACE','CMakeLists.txt'
)

$lockfileNames = @(
    'package-lock.json','yarn.lock','pnpm-lock.yaml','Cargo.lock',
    'poetry.lock','Gemfile.lock','composer.lock','bun.lockb','bun.lock','packages.lock.json'
)

$migrationPatterns = @('migrations/', 'alembic/versions/', 'db/migrate/')

function Get-FileCategory($filePath) {
    $name = Split-Path $filePath -Leaf
    $ext = if ($name.Contains('.')) { $name.Split('.')[-1] } else { '' }

    if ($lockfileNames -contains $name) { return 'lockfile' }

    foreach ($pattern in $migrationPatterns) {
        if ($filePath -match [regex]::Escape($pattern)) { return 'migration' }
    }

    if ($mergirafNames -contains $name) { return 'mergiraf' }
    if ($mergirafExtensions -contains $ext) { return 'mergiraf' }

    return 'other'
}

$conflictedFiles = git diff --name-only --diff-filter=U 2>$null
if ($conflictedFiles) {
    foreach ($file in $conflictedFiles) {
        if ([string]::IsNullOrWhiteSpace($file)) { continue }
        $category = Get-FileCategory $file
        Write-Output "$category`t$file"
    }
}

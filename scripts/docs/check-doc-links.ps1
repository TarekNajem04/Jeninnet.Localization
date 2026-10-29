#requires -Version 7
<#
.SYNOPSIS
  Verifies that every relative Markdown link and image in the repository's project
  documentation (root *.md, docs/**, src/**, integrations/**, samples/**) resolves to an
  existing file or folder, and that no project document links to a git-ignored path.

.DESCRIPTION
  Absolute URLs (http/https/mailto), pure anchors (#...) and code-fenced content are
  ignored. Link targets are URL-decoded before resolution (e.g. "%20" -> space).

.PARAMETER RepositoryRoot
  Repository root. Defaults to two levels above this script.

.OUTPUTS
  Prints one line per scanned file with its link count, then a summary.
  Exits 1 when any link is broken or targets a git-ignored path.

.EXAMPLE
  pwsh scripts/docs/check-doc-links.ps1
#>
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'

function Get-MarkdownLinks([string]$Path) {
    $links = [System.Collections.Generic.List[string]]::new()
    $inFence = $false
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        $trimmed = $line.Trim()
        if ($trimmed -match '^```') { $inFence = -not $inFence; continue }
        if ($inFence) { continue }

        # Links inside inline code spans are literal examples, not links.
        $scannable = [regex]::Replace($line, '`[^`]*`', '')

        foreach ($match in [regex]::Matches($scannable, '!?\[[^\]]*\]\((?<target>[^)\s]+)(?:\s+"[^"]*")?\)')) {
            [void]$links.Add($match.Groups['target'].Value)
        }
    }
    return $links
}

$files = [System.Collections.Generic.List[string]]::new()
foreach ($rootDocument in Get-ChildItem $RepositoryRoot -Filter '*.md' -File) {
    [void]$files.Add($rootDocument.FullName)
}

foreach ($area in @('docs', 'src', 'integrations', 'samples')) {
    $path = Join-Path $RepositoryRoot $area
    if (-not (Test-Path -LiteralPath $path)) { continue }
    foreach ($file in Get-ChildItem $path -Recurse -Filter '*.md' -File) {
        if ($file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
        [void]$files.Add($file.FullName)
    }
}

$failures = [System.Collections.Generic.List[string]]::new()
$gitCommand = Get-Command git -ErrorAction SilentlyContinue
$checked = 0

foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file)) {
        [void]$failures.Add("missing document: $file")
        continue
    }

    $directory = Split-Path -Parent $file
    $relative = $file.Substring($RepositoryRoot.Length).TrimStart('\', '/')
    $links = Get-MarkdownLinks -Path $file
    $broken = 0

    foreach ($link in $links) {
        if ($link -match '^(https?|mailto):' -or $link.StartsWith('#')) { continue }

        $target = $link.Split('#')[0]
        if ([string]::IsNullOrWhiteSpace($target)) { continue }

        $decoded = [System.Uri]::UnescapeDataString($target)
        $resolved = Join-Path $directory ($decoded -replace '/', '\')
        $checked++

        if (-not (Test-Path -LiteralPath $resolved)) {
            [void]$failures.Add("${relative}: broken link -> $link")
            $broken++
            continue
        }

        if ($gitCommand -and $resolved.StartsWith($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            & $gitCommand.Source -C $RepositoryRoot check-ignore --quiet -- $resolved 2>$null
            if ($LASTEXITCODE -eq 0) {
                [void]$failures.Add("${relative}: link to a git-ignored path -> $link")
                $broken++
            }
        }
    }

    Write-Host ('{0,-60} {1,3} link(s){2}' -f $relative, $links.Count, $(if ($broken -gt 0) { "  ($broken broken)" } else { '' }))
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "DOC LINK CHECK FAILED ($($failures.Count) problem(s)):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Doc links OK: $checked relative link(s) across $($files.Count) document(s) resolve." -ForegroundColor Green

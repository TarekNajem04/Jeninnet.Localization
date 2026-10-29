#requires -Version 7
<#
.SYNOPSIS
  Verifies that the generated DocFX API metadata covers every packable assembly.

.DESCRIPTION
  Reads the committed public-API baselines (one per packable assembly) and checks the DocFX
  metadata manifest under docs/api. The manifest is produced by:
      dotnet docfx metadata docs/docfx.json
  It is git-ignored, so this check runs after metadata generation in CI.

.PARAMETER ApiDirectory
  The generated DocFX API directory. Defaults to <repo>/docs/api.

.PARAMETER ApiBaselinesDirectory
  The committed API baselines. Defaults to <repo>/tests/Jeninnet.Localization.Tests/ApiBaselines.

.OUTPUTS
  Prints the covered assembly count and exits 1 when an assembly is missing or the metadata is absent.

.EXAMPLE
  pwsh scripts/docs/check-api-reference.ps1
#>
param(
    [string]$ApiDirectory = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'docs\api'),
    [string]$ApiBaselinesDirectory = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'tests\Jeninnet.Localization.Tests\ApiBaselines'),
    [string[]]$WindowsOnlyAssemblies = @(
        'Jeninnet.Localization.Integrations.Maui',
        'Jeninnet.Localization.Integrations.WinUI'
    )
)

$ErrorActionPreference = 'Stop'

$manifestPath = Join-Path $ApiDirectory '.manifest'
$tocPath = Join-Path $ApiDirectory 'toc.yml'

if (-not (Test-Path -LiteralPath $manifestPath)) {
    Write-Error "DocFX API metadata not found at '$ApiDirectory'. Run 'dotnet docfx metadata docs/docfx.json' first."
    exit 2
}

if (-not (Test-Path -LiteralPath $tocPath)) {
    Write-Error "DocFX API table of contents not found at '$tocPath'."
    exit 2
}

if (-not (Test-Path -LiteralPath $ApiBaselinesDirectory)) {
    Write-Error "API baselines not found at '$ApiBaselinesDirectory'."
    exit 2
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$uids = @($manifest.PSObject.Properties.Name)
$baselines = @(Get-ChildItem -LiteralPath $ApiBaselinesDirectory -Filter '*.Api.txt' -File |
    ForEach-Object { $_.BaseName -replace '\.Api$', '' } |
    Where-Object { $WindowsOnlyAssemblies -notcontains $_ } |
    Sort-Object)

$missing = [System.Collections.Generic.List[string]]::new()
foreach ($assembly in $baselines) {
    $prefix = $assembly + '.'
    $found = $false
    foreach ($uid in $uids) {
        if ($uid -eq $assembly -or $uid.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            $found = $true
            break
        }
    }

    if (-not $found) {
        $missing.Add($assembly)
    }
}

if ($missing.Count -gt 0) {
    Write-Host "API REFERENCE CHECK FAILED ($($missing.Count) missing assembly/assemblies):" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "API reference OK: $($baselines.Count) cross-platform packable assemblies are represented in the generated metadata; $($WindowsOnlyAssemblies.Count) Windows-only integration package(s) are documented by their package READMEs." -ForegroundColor Green

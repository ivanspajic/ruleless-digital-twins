# scripts/smoke-test-broken-bindings.ps1
#
# Smoke test for the bindings validator (WP2 V1).
# Asserts that `--validate-model` rejects an intentionally broken
# bindings file with exit code 1 and Result: FAIL. Used to keep the
# negative path of the validator covered when the showcase / testlab
# happy paths are also green.
#
# Usage (from repo root):
#   .\scripts\smoke-test-broken-bindings.ps1
#
# Exit codes:
#   0 — smoke test passed (validator correctly returned exit 1 + FAIL)
#   1 — smoke test failed (validator did NOT reject the broken sample)
#   2 — smoke test could not run (missing fixture or build error)

$ErrorActionPreference = 'Stop'

$repoRoot   = (Resolve-Path "$PSScriptRoot\..").Path
$brokenJson = Join-Path $repoRoot 'config\ha-bindings.broken-sample.json'
$smartNode  = Join-Path $repoRoot 'SmartNode\SmartNode\SmartNode.csproj'

if (-not (Test-Path $brokenJson)) {
    Write-Error "Smoke fixture not found: $brokenJson"
    exit 2
}
if (-not (Test-Path $smartNode)) {
    Write-Error "SmartNode project not found: $smartNode"
    exit 2
}

Write-Host "Running --validate-model against the broken sample..."
$out = & dotnet run --project $smartNode -- --validate-model $brokenJson 2>&1
$rc  = $LASTEXITCODE

# Echo validator output so a CI run keeps the diagnostic trail visible.
$out | ForEach-Object { Write-Host $_ }

if ($rc -ne 1) {
    Write-Error "Smoke test FAILED: expected exit code 1, got $rc."
    exit 1
}
if (-not ($out -match 'Result: FAIL')) {
    Write-Error "Smoke test FAILED: validator did not print 'Result: FAIL'."
    exit 1
}

Write-Host ""
Write-Host "Smoke test PASSED: validator correctly rejected the broken sample (exit 1, Result: FAIL)."
exit 0

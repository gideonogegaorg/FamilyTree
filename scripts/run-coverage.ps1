# Run tests with OpenCover coverage and optionally merge into coverage/combined for local HTML.
# SonarCloud ingests **/coverage.opencover.xml directly in CI (no merge required).
# Usage: from repo root, .\scripts\run-coverage.ps1

$ErrorActionPreference = "Stop"
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root "GMO.FamilyTree.sln"))) {
    Write-Error "Run this script from the repository root (where GMO.FamilyTree.sln exists)."
    exit 1
}

Write-Host "Building and running tests with OpenCover coverage..."
dotnet test GMO.FamilyTree.sln `
  --settings coverlet.runsettings `
  --collect:"XPlat Code Coverage;Format=opencover" `
  --results-directory ./coverage `
  --verbosity minimal

$openCoverFiles = Get-ChildItem -Path ./coverage, ./tst -Recurse -Filter "coverage.opencover.xml" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
if ($openCoverFiles.Count -eq 0) {
    Write-Error "No coverage.opencover.xml files found under ./coverage"
    exit 1
}

Write-Host "Found $($openCoverFiles.Count) OpenCover report(s)."

$reports = ($openCoverFiles | ForEach-Object { $_.Replace("\", "/") }) -join ";"
$targetDir = (Join-Path $root "coverage\combined").Replace("\", "/")

Write-Host "Generating HTML report in coverage/combined (web assembly only)..."
Set-Location (Join-Path $root "src")
dotnet tool restore
dotnet tool run reportgenerator -- "-reports:$reports" "-targetdir:$targetDir" "-reporttypes:Html" "-assemblyfilters:+GMO.FamilyTree.Web"
Set-Location $root

Write-Host "Done. Open coverage/combined/index.html for the local report."

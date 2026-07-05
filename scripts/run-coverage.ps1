# Run unit + integration tests with coverage, then merge results into coverage/combined.
# Requires: dotnet (sln build), reportgenerator (dotnet tool in src).
# Usage: from repo root, .\scripts\run-coverage.ps1

$ErrorActionPreference = "Stop"
# Run from repo root so coverage and src paths are correct
$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root "GMO.FamilyTree.sln"))) {
    Write-Error "Run this script from the repository root (where GMO.FamilyTree.sln exists)."
    exit 1
}

Write-Host "Building and running tests with coverage..."
dotnet test GMO.FamilyTree.sln --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./coverage --verbosity minimal

$coberturaFiles = Get-ChildItem -Path ./coverage -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
if ($coberturaFiles.Count -eq 0) {
    Write-Error "No coverage.cobertura.xml files found under ./coverage"
    exit 1
}

$reports = ($coberturaFiles | ForEach-Object { $_.Replace("\", "/") }) -join ";"
$targetDir = (Join-Path $root "coverage\combined").Replace("\", "/")

Write-Host "Merging $($coberturaFiles.Count) coverage report(s) into coverage/combined..."
Set-Location (Join-Path $root "src")
dotnet tool restore
dotnet tool run reportgenerator -- "-reports:$reports" "-targetdir:$targetDir" "-reporttypes:Html;Cobertura"
Set-Location $root

Write-Host "Done. Open coverage/combined/index.html for the report. Merged Cobertura: coverage/combined/Cobertura.xml"

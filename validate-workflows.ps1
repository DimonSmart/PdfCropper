# Validate GitHub Actions Workflows

# This script helps validate the GitHub Actions workflow files locally
# Requires: GitHub CLI (gh) or yamllint

Write-Host "Validating GitHub Actions workflow files..." -ForegroundColor Green

$workflowFiles = Get-ChildItem -Path ".github/workflows" -Filter "*.yml"

foreach ($file in $workflowFiles) {
    Write-Host "Checking $($file.Name)..." -ForegroundColor Yellow
    
    # Basic YAML syntax check using PowerShell
    try {
        $content = Get-Content $file.FullName -Raw
        # Simple validation - check for basic YAML structure
        if ($content -match "^name:" -and $content -match "on:" -and $content -match "jobs:") {
            Write-Host "✅ $($file.Name) - Basic structure OK" -ForegroundColor Green
        } else {
            Write-Host "❌ $($file.Name) - Missing required sections" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ $($file.Name) - Error reading file" -ForegroundColor Red
    }
}

Write-Host "`nTo validate with GitHub CLI (if installed):" -ForegroundColor Cyan
Write-Host "gh workflow validate .github/workflows/build-and-test.yml" -ForegroundColor Gray
Write-Host "gh workflow validate .github/workflows/release.yml" -ForegroundColor Gray
Write-Host "gh workflow validate .github/workflows/code-quality.yml" -ForegroundColor Gray

Write-Host "`nTo test workflows locally, consider using 'act':" -ForegroundColor Cyan
Write-Host "# Install: choco install act-cli" -ForegroundColor Gray
Write-Host "# Run: act push" -ForegroundColor Gray
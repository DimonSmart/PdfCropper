# GitHub Actions CI/CD Setup

## Overview

This repository uses GitHub Actions for continuous integration and deployment. The workflows automatically build, test, and package the DimonSmart.PdfCropper library across multiple platforms and .NET versions.

## Workflows

### 1. Build and Test (`build-and-test.yml`)

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` branch

**Matrix Strategy:**
- **Operating Systems**: Ubuntu, Windows, macOS
- **.NET Versions**: 6.0.x, 8.0.x, 9.0.x

**Steps:**
1. Checkout code
2. Setup .NET SDK
3. Install platform-specific dependencies (Linux only)
4. Restore NuGet packages
5. Build solution in Release configuration
6. Run all tests with code coverage
7. Upload coverage to Codecov (Ubuntu + .NET 8.0 only)
8. Create NuGet package (main branch only)
9. Upload package as artifact

### 2. Release (`release.yml`)

**Triggers:**
- Push tags matching `v*` pattern (e.g., `v1.0.1`, `v2.0.0`)

**Steps:**
1. Extract version from git tag
2. Update project version in .csproj file
3. Build and create NuGet package
4. Create GitHub Release with package attachment
5. Publish package to NuGet.org

### 3. Code Quality (`code-quality.yml`)

**Triggers:**
- Same as build-and-test workflow

**Steps:**
1. Run tests with detailed code coverage
2. Generate HTML coverage reports
3. Upload coverage to Codecov
4. Provide coverage artifacts

## Required Secrets

To use these workflows, configure the following secrets in your GitHub repository:

### Repository Secrets (Settings → Secrets and variables → Actions)

| Secret Name | Description | Required For |
|------------|-------------|--------------|
| `NUGET_API_KEY` | NuGet.org API key for publishing packages | Release workflow |
| `CODECOV_TOKEN` | Codecov.io token for coverage reporting | Code quality workflow |

### Setting up NuGet API Key

1. Go to [NuGet.org](https://www.nuget.org/account/apikeys)
2. Create a new API key with "Push new packages and package versions" scope
3. Add the key as `NUGET_API_KEY` secret in GitHub repository settings

### Setting up Codecov Token

1. Go to [Codecov.io](https://codecov.io)
2. Add your GitHub repository
3. Copy the repository upload token
4. Add the token as `CODECOV_TOKEN` secret in GitHub repository settings

## Status Badges

The following badges are available in the README:

```markdown
[![Build and Test](https://github.com/DimonSmart/PdfCropper/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/DimonSmart/PdfCropper/actions/workflows/build-and-test.yml)
[![NuGet Version](https://img.shields.io/nuget/v/DimonSmart.PdfCropper)](https://www.nuget.org/packages/DimonSmart.PdfCropper)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DimonSmart.PdfCropper)](https://www.nuget.org/packages/DimonSmart.PdfCropper)
[![License](https://img.shields.io/badge/license-0BSD-blue.svg)](LICENSE)
[![codecov](https://codecov.io/gh/DimonSmart/PdfCropper/branch/main/graph/badge.svg)](https://codecov.io/gh/DimonSmart/PdfCropper)
```

## Creating a Release

To create a new release:

1. Update version in `src/PdfCropper/PdfCropper.csproj` if needed
2. Commit and push changes
3. Create and push a version tag:
   ```bash
   git tag v1.0.2
   git push origin v1.0.2
   ```
4. The release workflow will automatically:
   - Create a GitHub release
   - Build and publish the NuGet package
   - Attach the package to the release

## Local Development

The workflows are designed to match local development environment:

```bash
# Restore dependencies
dotnet restore PdfCropper.sln

# Build solution
dotnet build PdfCropper.sln --configuration Release

# Run tests
dotnet test PdfCropper.sln --configuration Release

# Create package
dotnet pack src/PdfCropper/PdfCropper.csproj --configuration Release
```

## Platform-Specific Notes

### Linux (Ubuntu)
The workflow automatically installs required system dependencies:
```bash
sudo apt-get install -y libfontconfig1 libgdiplus libc6-dev
```

### Windows and macOS
No additional dependencies required - native libraries are included via NuGet packages.

## Troubleshooting

### Common Issues

**Build fails on Linux:**
- Ensure system dependencies are installed
- Check if PDFium native libraries are accessible

**Tests fail on specific platform:**
- Review test logs for platform-specific errors
- Verify file paths use cross-platform conventions

**NuGet publish fails:**
- Verify `NUGET_API_KEY` secret is correctly set
- Check if package version already exists
- Ensure API key has proper permissions

**Coverage upload fails:**
- Verify `CODECOV_TOKEN` secret is set
- Check if coverage files are generated correctly
- Review Codecov repository configuration
# NuGet Package Publishing Instructions

## Automated Publishing (Recommended)

The repository includes GitHub Actions workflows for automated building, testing, and publishing:

### Creating a Release via GitHub Actions

1. **Update version** in `src/PdfCropper/PdfCropper.csproj` if needed
2. **Commit and push** changes to main branch
3. **Create and push a version tag**:
   ```bash
   git tag v1.0.2
   git push origin v1.0.2
   ```
4. **GitHub Actions will automatically**:
   - Build the project for all target frameworks
   - Run tests on Windows, Linux, and macOS
   - Create NuGet package
   - Create GitHub Release
   - Publish to NuGet.org

### Required Secrets

Configure these secrets in your GitHub repository (Settings → Secrets and variables → Actions):

- `NUGET_API_KEY`: Your NuGet.org API key for publishing packages

See [GitHub Actions Setup](GITHUB_ACTIONS.md) for detailed instructions.

## Manual Publishing

### Prerequisites

1. **API Key**: Get your NuGet API key from https://www.nuget.org/account/apikeys
2. **Account**: Make sure you have a NuGet.org account

## Publishing Steps

### 1. Build and Pack
```bash
# Build the solution in Release mode
dotnet build PdfCropper.sln --configuration Release

# Create the NuGet package
dotnet pack src/PdfCropper/PdfCropper.csproj --configuration Release
```

### 2. Test the Package Locally (Optional)
```bash
# Install from local package for testing
dotnet add package DimonSmart.PdfCropper --source "src/PdfCropper/bin/Release"
```

### 3. Publish to NuGet.org
```bash
# Set your API key (one time setup)
dotnet nuget push src/PdfCropper/bin/Release/DimonSmart.PdfCropper.1.0.1.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Package Information

- **Package ID**: DimonSmart.PdfCropper
- **Title**: DimonSmart.PdfCropper
- **Version**: 1.0.1 (cross-platform support added)
- **Authors**: DimonSmart
- **Description**: A cross-platform .NET library that intelligently trims PDF pages to actual content using content-based or bitmap-based analysis
- **Target Frameworks**: .NET 6.0, .NET 8.0, .NET 9.0
- **Platform Support**: Windows, Linux, macOS, and other Unix systems
- **Tags**: pdf, pdf cropper, pdf trimmer, pdf margin removal, ebook, ebook preparation, document processing, cross-platform, windows, linux, macos, unix
- **License**: 0BSD
- **Repository**: https://github.com/DimonSmart/PdfCropper

## Files Included in Package

- Main library DLL
- README.md
- PdfCropperIcon.png (package icon)
- XML documentation file

## Version Update Process

To release a new version:

1. Update `<Version>` and `<PackageVersion>` in `src/PdfCropper/PdfCropper.csproj`
2. Update release notes if needed
3. Build and pack again
4. Push the new package

## Dependencies

The package automatically includes these dependencies:
- iText (9.3.0)
- itext.bouncy-castle-adapter (9.3.0)
- PDFtoImage (5.1.1)

## Target Framework

- .NET 9.0

## Namespace

All classes are in the `DimonSmart.PdfCropper` namespace.
# DimonSmart.PdfCropper

[![Build and Test](https://github.com/DimonSmart/PdfCropper/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/DimonSmart/PdfCropper/actions/workflows/build-and-test.yml)
[![NuGet Version](https://img.shields.io/nuget/v/DimonSmart.PdfCropper)](https://www.nuget.org/packages/DimonSmart.PdfCropper)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DimonSmart.PdfCropper)](https://www.nuget.org/packages/DimonSmart.PdfCropper)
[![License](https://img.shields.io/badge/license-0BSD-blue.svg)](LICENSE)

DimonSmart.PdfCropper is a cross-platform .NET library that intelligently trims PDF pages to actual content using two different methods.
The library exposes a simple API through `PdfSmartCropper.CropAsync`, which accepts a PDF document in memory and returns a new PDF with recalculated `CropBox` and `TrimBox` on every non-empty page.

## Example

Here's a visual example of what PdfCropper does - it removes unnecessary margins and whitespace:

| Before Cropping | After Cropping |
|----------------|----------------|
| ![Before Crop](https://raw.githubusercontent.com/DimonSmart/PdfCropper/main/docs/images/before_crop.png) | ![After Crop](https://raw.githubusercontent.com/DimonSmart/PdfCropper/main/docs/images/after_crop.png) |

*Example pages from "[Pragmatic Type-Level Design](https://graninas.com/pragmatic-type-level-design-book/)" by Alexander Granin*

The CLI utility is particularly useful for reading e-books with minimal margins, making them more comfortable to read on tablets and e-readers by removing excessive whitespace around the content.


## Download the CLI

Need the tool without compiling it yourself? Grab the latest self-contained Windows build here:

* [PdfCropper.Cli-win-x64.exe](https://github.com/DimonSmart/PdfCropper/releases/latest/download/PdfCropper.Cli-win-x64.exe) – portable single-file executable that works on any 64-bit Windows machine.

Each tagged release also contains the NuGet package and the CLI executable as downloadable assets.


## Platform Support

✅ **Windows** - Full support  
✅ **Linux** - Full support  
✅ **macOS** - Full support  
✅ **Other Unix systems** - Compatible with .NET runtime  

**Target Frameworks**: .NET 8.0, .NET 9.0

## Installation

Install the package via NuGet:

```bash
dotnet add package DimonSmart.PdfCropper
```

Or via Package Manager Console:

```
Install-Package DimonSmart.PdfCropper
```


## Features

* **Two cropping methods**:
  - **ContentBased** (default): Analyzes PDF content (text, vectors, images) directly - fast and preserves quality
  - **BitmapBased**: Renders pages to images and analyzes pixels - more accurate for complex layouts
* Preserves the existing content streams, metadata, fonts and resources
* Leaves empty pages untouched and keeps `MediaBox`, `BleedBox` and `ArtBox` intact
* Handles rotated pages and maintains deterministic output
* Extensible logging through `IPdfCropLogger` interface
* Built on top of iText 9.3 and PDFium (for bitmap rendering)

## Usage

### Minimal crop (three lines)

```csharp
using DimonSmart.PdfCropper;

byte[] cropped = await PdfSmartCropper.CropAsync(inputBytes);
```

The default call uses the `ContentBased` method, keeps a 0.5pt safety margin, applies a 1.0pt tolerance when classifying edge-touching content, and does not touch document metadata.

### Aggressive crop with all clean-up switches

```csharp
using DimonSmart.PdfCropper;

var profile = PdfCropProfiles.Aggressive;
byte[] cropped = await PdfSmartCropper.CropAsync(
    inputBytes,
    profile.CropSettings,
    optimizationSettings: profile.OptimizationSettings,
    logger: null);
```

The aggressive preset removes edge-touching artefacts, applies maximum Deflate compression, enables smart mode, removes unused objects and metadata, clears document info, and strips embedded standard fonts.

### Built-in presets

The library ships with ready-to-use profiles for common scenarios:

| Key | Description | Crop settings | Optimization settings |
|-----|-------------|---------------|-----------------------|
| `simple` | Default behaviour for quick cropping. | Content-based, keeps edge content, 0.5pt margin, 1pt edge tolerance. | No extra optimisation (same as `PdfOptimizationSettings.Default`). |
| `ebook` | Recommended for reading PDFs on e-readers. | Content-based, ignores artefacts that touch the page edge, 1pt margin, 1pt edge tolerance. | Default optimisation. |
| `aggressive` | Tight crop plus the strongest clean-up and compression. | Content-based, ignores edge artefacts, 0.25pt margin, 1pt edge tolerance. | Full compression, smart mode, unused-object removal, metadata cleanup, PDF 1.7 target. |

Retrieve a profile via `PdfCropProfiles.Simple`, `PdfCropProfiles.Ebook`, or `PdfCropProfiles.Aggressive`. You can also resolve a profile dynamically by key: `PdfCropProfiles.TryGet("ebook", out var profile)`.

### Custom logger

```csharp
using DimonSmart.PdfCropper;

public sealed class MyLogger : IPdfCropLogger
{
    public void LogInfo(string message) => Console.WriteLine($"[INFO] {message}");
    public void LogWarning(string message) => Console.WriteLine($"[WARN] {message}");
    public void LogError(string message) => Console.Error.WriteLine($"[ERROR] {message}");
}

var logger = new MyLogger();
byte[] cropped = await PdfSmartCropper.CropAsync(inputBytes, CropMethod.ContentBased, logger);
```

Every overload throws `PdfCropException` with a `PdfCropErrorCode` when the input PDF is invalid, encrypted, or fails to process.

### Command Line Utility

The repository includes a console application that wraps the library. This CLI tool is especially useful for preparing e-books and documents for comfortable reading on tablets and e-readers by removing excessive margins and whitespace.

**Perfect for e-book readers**: Transform PDF books with large margins into reader-friendly versions that utilize screen space more efficiently.

```bash
# Run the ready-made Windows build (download link above)
PdfCropper.Cli-win-x64.exe input.pdf output.pdf

# Use the e-book preset (ignores edge artefacts, keeps 1pt margin)
PdfCropper.Cli-win-x64.exe input.pdf output.pdf --preset ebook

# Apply the aggressive preset with verbose logging
PdfCropper.Cli-win-x64.exe input.pdf output.pdf --preset aggressive -v

# From source (cross-platform)
dotnet run --project src/DimonSmart.PdfCropper.Cli/DimonSmart.PdfCropper.Cli.csproj -- input.pdf output.pdf --preset simple
```

#### CLI Options

- `--preset <simple|ebook|aggressive>` - Apply a predefined set of crop/optimisation options
- `-m, --method <0|1>` - Cropping method:
  - `0` = ContentBased (default, analyzes PDF content)
  - `1` = BitmapBased (renders to image, slower but more accurate)
- `-v, --verbose` - Enable verbose logging
- `--edge-tolerance <points>` - Distance from the page boundary (in points) that still counts as “touching the edge” when exclusion is enabled (default: 1.0)
- All low-level switches (`--margin`, `--compression-level`, `--smart`, etc.) remain available to fine-tune or override a preset

## Cropping Methods Comparison

| Feature | ContentBased | BitmapBased |
|---------|-------------|-------------|
| Speed | ⚡ Fast | 🐌 Slower |
| Quality | ✅ Preserves vector quality | ✅ Preserves vector quality |
| Accuracy | Good for standard documents | Better for complex layouts |
| Use case | Most PDFs | PDFs with complex graphics |

## Development

* Library target frameworks: `.NET 8.0`, `.NET 9.0`
* Cross-platform support: Windows, Linux, macOS, and other Unix systems
* Dependencies:
  * iText 9.3.0 (PDF manipulation)
  * PDFtoImage 5.1.1 (PDF to bitmap rendering) - includes native libraries for all platforms
  * SkiaSharp (image processing) - cross-platform 2D graphics
* Tests are located in `tests/PdfCropper.Tests` and use xUnit
* Build with `dotnet build PdfCropper.sln`
* Run tests with `dotnet test PdfCropper.sln`
* CI/CD: Automated builds and tests via GitHub Actions - see [GitHub Actions Setup](GITHUB_ACTIONS.md)

### Platform-Specific Notes

* **Linux**: Requires `libfontconfig1` and `libgdiplus` for optimal PDF rendering
* **macOS**: No additional dependencies required
* **Windows**: No additional dependencies required

## API Reference

### IPdfCropLogger Interface

```csharp
public interface IPdfCropLogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}
```

### CropMethod Enum

```csharp
public enum CropMethod
{
    ContentBased = 0,  // Analyzes PDF content
    BitmapBased = 1    // Renders to bitmap
}
```


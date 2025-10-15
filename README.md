# PdfCropper

PdfCropper is a .NET library that trims PDF pages to the actual content without rasterisation. The library exposes a single entry point, `PdfSmartCropper.CropAsync`, which accepts a PDF document in memory and returns a new PDF with recalculated `CropBox` and `TrimBox` on every non-empty page.

## Features

* Detects visible text, vector graphics and images to determine the bounding box.
* Preserves the existing content streams, metadata, fonts and resources.
* Leaves empty pages untouched and keeps `MediaBox`, `BleedBox` and `ArtBox` intact.
* Handles rotated pages and maintains deterministic output.

## Usage

```csharp
using PdfCropper;

byte[] cropped = await PdfSmartCropper.CropAsync(inputBytes, cancellationToken);
```

The method throws `PdfCropException` with a specific `PdfCropErrorCode` when the input is invalid, encrypted or cannot be processed.

## Development

* Library target framework: `.NET 8.0`.
* Dependencies are managed via the solution file `PdfCropper.sln`.
* Tests are located in `tests/PdfCropper.Tests` and rely on xUnit.

# PdfCropper - Usage Examples

## Quick Start

### 1. Basic ContentBased Cropping (Default)

```csharp
using PdfCropper;

// Simplest way - uses ContentBased method (default)
byte[] inputPdf = await File.ReadAllBytesAsync("input.pdf");
byte[] croppedPdf = await PdfSmartCropper.CropAsync(inputPdf, CropSettings.Default);
await File.WriteAllBytesAsync("output.pdf", croppedPdf);
```

### 2. BitmapBased Cropping

```csharp
using PdfCropper;

byte[] inputPdf = await File.ReadAllBytesAsync("input.pdf");
byte[] croppedPdf = await PdfSmartCropper.CropAsync(
    inputPdf,
    new CropSettings(CropMethod.BitmapBased)
);
await File.WriteAllBytesAsync("output.pdf", croppedPdf);
```

### 3. With Custom Logger

```csharp
using PdfCropper;

// Custom logger implementation
public class FileLogger : IPdfCropLogger
{
    private readonly StreamWriter _writer;

    public FileLogger(string logPath)
    {
        _writer = new StreamWriter(logPath, append: true);
    }

    public void LogInfo(string message)
    {
        _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] {message}");
        _writer.Flush();
    }

    public void LogWarning(string message)
    {
        _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [WARN] {message}");
        _writer.Flush();
    }

    public void LogError(string message)
    {
        _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] {message}");
        _writer.Flush();
    }

    public void Dispose() => _writer.Dispose();
}

// Usage
byte[] inputPdf = await File.ReadAllBytesAsync("input.pdf");
using var logger = new FileLogger("crop_log.txt");

byte[] croppedPdf = await PdfSmartCropper.CropAsync(
    inputPdf, 
    CropSettings.Default,
    logger
);

await File.WriteAllBytesAsync("output.pdf", croppedPdf);
```

### 4. With Progress Reporting (for WebAssembly/UI)

```csharp
using PdfCropper;

// Progress reporter for real-time updates
var progress = new Progress<string>(message => 
{
    Console.WriteLine(message);
    // In WebAssembly: StateHasChanged() or UpdateUI()
});

byte[] inputPdf = await File.ReadAllBytesAsync("input.pdf");
byte[] croppedPdf = await PdfSmartCropper.CropAsync(
    inputPdf, 
    CropSettings.Default,
    PdfOptimizationSettings.Default,
    logger: null,
    progress,
    CancellationToken.None
);
await File.WriteAllBytesAsync("output.pdf", croppedPdf);
```

### 5. With Cancellation Token

```csharp
using PdfCropper;

var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    byte[] inputPdf = await File.ReadAllBytesAsync("input.pdf");
    byte[] croppedPdf = await PdfSmartCropper.CropAsync(
        inputPdf, 
        CropSettings.Default,
        PdfOptimizationSettings.Default,
        logger: null,
        progress: null,
        cts.Token
    );
    await File.WriteAllBytesAsync("output.pdf", croppedPdf);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Cropping operation was cancelled");
}
```

### 5. Repeated Content Detection

```csharp
using PdfCropper;

// Exclude objects that appear on most pages (e.g., headers, footers, watermarks)
var settingsWithRepeatedDetection = new CropSettings(
    CropMethod.ContentBased,
    excludeEdgeTouchingObjects: true,
    margin: 1.0f,
    detectRepeatedObjects: true,
    repeatedObjectOccurrenceThreshold: 90.0, // Objects present on 90%+ of pages
    repeatedObjectMinimumPageCount: 3 // Only apply to docs with 3+ pages
);

byte[] inputPdf = await File.ReadAllBytesAsync("input.pdf");
byte[] croppedPdf = await PdfSmartCropper.CropAsync(inputPdf, settingsWithRepeatedDetection);
await File.WriteAllBytesAsync("output.pdf", croppedPdf);
```

### 6. Error Handling

```csharp
using PdfCropper;

try
{
    byte[] inputPdf = await File.ReadAllBytesAsync("input.pdf");
    byte[] croppedPdf = await PdfSmartCropper.CropAsync(inputPdf, CropSettings.Default);
    await File.WriteAllBytesAsync("output.pdf", croppedPdf);
}
catch (PdfCropException ex) when (ex.Code == PdfCropErrorCode.EncryptedPdf)
{
    Console.Error.WriteLine("PDF is encrypted and cannot be processed");
}
catch (PdfCropException ex) when (ex.Code == PdfCropErrorCode.InvalidPdf)
{
    Console.Error.WriteLine("PDF file is invalid or corrupted");
}
catch (PdfCropException ex)
{
    Console.Error.WriteLine($"PDF processing error: {ex.Message}");
}
```

## Command Line Examples

### Basic Usage

```bash
# ContentBased method (default)
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf
```

### BitmapBased Method

```bash
# Use bitmap-based cropping
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf -m 1
```

### Custom Safety Margin

```bash
# Default margin (0.5 points)
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf

# No margin (crop tight to content)
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --margin 0

# Small margin (1 point)
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --margin 1.0

# Large margin (5 points)
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --margin 5.0
```

**Note:** Margin is specified in points (1 point = 1/72 inch ≈ 0.35 mm)

### Verbose Logging

```bash
# Enable detailed logging
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf -v

# Example output:
# [INFO] Reading input file: input.pdf
# [INFO] Cropping PDF using ContentBased method...
# [INFO] Processing PDF with 5 page(s) using ContentBased method
# [INFO] Page 1/5: Size = 612.00 x 792.00 pts
# [INFO] Page 1/5: Content bounds = (72.00, 100.00) to (540.00, 700.00)
# [INFO] Page 1/5: Crop box = (71.50, 99.50, 469.00, 601.00)
# [INFO] Page 1/5: New size = 469.00 x 601.00 pts
# ...
```

### Debug Page Diagnostics

```bash
# Emit extended diagnostics for a single page (1-based index)
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --debug-page 2
```

### Repeated Content Detection

```bash
# Enable detection of repeated content (headers, footers, watermarks)
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --detect-repeated-objects on -v

# Disable detection of repeated content
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --detect-repeated-objects off -v

# Detect objects that appear on 85% or more pages
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --detect-repeated-objects on --repeated-threshold 85

# Only apply repeated detection to documents with 5 or more pages
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --detect-repeated-objects on --repeated-min-pages 5

# Use presets that include repeated detection (default threshold: 40%)
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --preset ebook -v  # Detects repeated content
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --preset aggressive -v  # Detects repeated content
```

### All Options Combined

```bash
# BitmapBased method with verbose logging
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf -m 1 -v

# ContentBased with custom margin and verbose logging
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf --margin 2.5 -v

# BitmapBased with custom margin, excluding edge content
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf -m 1 --margin 3.0

# ContentBased excluding edge content with large margin
DimonSmart.PdfCropper.Cli.exe input.pdf output.pdf -m 01 --margin 5.0 -v
```

### Merge Multiple PDFs

```bash
# Merge every PDF from the scans folder into a single cropped document
DimonSmart.PdfCropper.Cli.exe "scans/*.pdf" merged/scans.pdf --merge

# Output must be a specific file path (wildcards and directories are not allowed)
DimonSmart.PdfCropper.Cli.exe "~/Documents/**/*.pdf" merged/collection.pdf --merge
```

## Logger Output Examples

### ContentBased Method Logging

```
[INFO] Processing PDF with 3 page(s) using ContentBased method
[INFO] Page 1/3: Size = 595.28 x 841.89 pts
[INFO] Page 1/3: Content bounds = (56.69, 70.87) to (538.58, 770.89)
[INFO] Page 1/3: Crop box = (56.19, 70.37, 482.89, 701.02)
[INFO] Page 1/3: New size = 482.89 x 701.02 pts
[WARN] Page 2/3: Skipped (empty page)
[INFO] Page 3/3: Size = 595.28 x 841.89 pts
[INFO] Page 3/3: Content bounds = (72.00, 100.00) to (523.28, 741.89)
[INFO] Page 3/3: Crop box = (71.50, 99.50, 452.28, 642.89)
[INFO] Page 3/3: New size = 452.28 x 642.89 pts
[INFO] PDF cropping completed successfully
```

### BitmapBased Method Logging

```
[INFO] Processing PDF with 1 page(s) using BitmapBased method
[INFO] Page 1/1: Size = 612.00 x 792.00 pts
[INFO] Page 1/1: Rendering to bitmap
[INFO] Page 1/1: Bitmap size = 2550 x 3300 pixels
[INFO] Page 1/1: Content pixels = (250, 350) to (2300, 3000)
[INFO] Page 1/1: PDF coordinates = (59.50, 71.50, 492.00, 635.27)
[INFO] Page 1/1: Crop box = (59.50, 71.50, 492.00, 635.27)
[INFO] Page 1/1: New size = 492.00 x 635.27 pts
[INFO] PDF cropping completed successfully
```

## When to Use Which Method

### ContentBased (Default) - Use When:
- ✅ Speed is important
- ✅ Working with standard text documents
- ✅ Vector quality must be preserved
- ✅ PDFs have standard text/image content

### BitmapBased - Use When:
- ✅ Maximum accuracy is needed
- ✅ Complex graphical layouts
- ✅ ContentBased method produces incorrect results
- ✅ PDFs have unusual rendering quirks
- ⚠️ Be aware: Slower and requires more memory

## Performance Tips

1. **Use ContentBased by default** - It's faster and works for most cases
2. **Only use BitmapBased when needed** - It's significantly slower
3. **Process large batches in parallel** - Both methods support concurrent processing
4. **Implement proper logging** - Helps debug cropping issues
5. **Use cancellation tokens** - For better control over long-running operations

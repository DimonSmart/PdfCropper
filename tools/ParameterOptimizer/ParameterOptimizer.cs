using DimonSmart.PdfCropper;
using System.Diagnostics;

namespace ParameterOptimizer;

public class OptimizationResult
{
    public string FileName { get; set; } = string.Empty;
    public long OriginalSize { get; set; }
    public long OptimizedSize { get; set; }
    public double CompressionRatio => OriginalSize > 0 ? (double)OptimizedSize / OriginalSize : 1.0;
    public double SavingsPercent => (1.0 - CompressionRatio) * 100;
    public CropSettings BestCropSettings { get; set; } = CropSettings.Default;
    public PdfOptimizationSettings BestOptimizationSettings { get; set; } = PdfOptimizationSettings.Default;
    public string ProcessingTime { get; set; } = string.Empty;
}

public class ParameterOptimizer
{
    private readonly string _inputDirectory;
    private readonly string _outputDirectory;

    public ParameterOptimizer(string inputDirectory)
    {
        _inputDirectory = inputDirectory;
        _outputDirectory = Path.Combine(Path.GetTempPath(), "PdfOptimizerTest", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(_outputDirectory);
    }

    public async Task<List<OptimizationResult>> OptimizeAllFilesAsync()
    {
        var pdfFiles = Directory.GetFiles(_inputDirectory, "*.pdf", SearchOption.TopDirectoryOnly);
        
        Console.WriteLine($"Found {pdfFiles.Length} PDF files in folder {_inputDirectory}");
        Console.WriteLine($"Temporary folder for results: {_outputDirectory}");
        Console.WriteLine();

        var parameterCombinations = GenerateOptimizedParameterCombinations();
        Console.WriteLine($"Will test {parameterCombinations.Count} compression combinations for each file (fixed crop settings)");
        Console.WriteLine($"Using parallel processing on {Environment.ProcessorCount} cores");
        Console.WriteLine();

        // Use parallel processing with degree of parallelism limit
        var semaphore = new SemaphoreSlim(Math.Min(Environment.ProcessorCount, pdfFiles.Length));
        var completedFiles = 0;
        var totalFiles = pdfFiles.Length;
        var overallStartTime = DateTime.Now;
        
        var tasks = pdfFiles.Select(async (pdfFile, index) =>
        {
            await semaphore.WaitAsync();
            try
            {
                var fileStartTime = DateTime.Now;
                Console.WriteLine($"[{index + 1}/{totalFiles}] Starting processing: {Path.GetFileName(pdfFile)} at {fileStartTime:HH:mm:ss}");
                var result = await OptimizeSingleFileAsync(pdfFile, parameterCombinations);
                var fileEndTime = DateTime.Now;
                var fileProcessingTime = fileEndTime - fileStartTime;
                
                Interlocked.Increment(ref completedFiles);
                var overallElapsed = DateTime.Now - overallStartTime;
                var avgTimePerFile = overallElapsed.TotalMinutes / completedFiles;
                var estimatedTotalTime = TimeSpan.FromMinutes(avgTimePerFile * totalFiles);
                var remainingTime = estimatedTotalTime - overallElapsed;
                
                Console.WriteLine($"[{index + 1}/{totalFiles}] Completed processing: {Path.GetFileName(pdfFile)} in {fileProcessingTime:mm\\:ss} - savings {result.SavingsPercent:F1}%");
                Console.WriteLine($"    Overall progress: {completedFiles}/{totalFiles} (remaining ~{(remainingTime.TotalMinutes > 0 ? remainingTime.ToString(@"hh\:mm") : "00:00")})");
                Console.WriteLine();
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<OptimizationResult> OptimizeSingleFileAsync(string inputFile, List<(CropSettings, PdfOptimizationSettings)> parameterCombinations)
    {
        var fileName = Path.GetFileName(inputFile);
        var originalSize = new FileInfo(inputFile).Length;
        var stopwatch = Stopwatch.StartNew();

        var bestResult = new OptimizationResult
        {
            FileName = fileName,
            OriginalSize = originalSize,
            OptimizedSize = originalSize,
            BestCropSettings = CropSettings.Default,
            BestOptimizationSettings = PdfOptimizationSettings.Default
        };

        Console.WriteLine($"  Original size: {FormatFileSize(originalSize)}");
        Console.WriteLine($"  Starting analysis of {parameterCombinations.Count} parameter combinations...");

        var inputBytes = await File.ReadAllBytesAsync(inputFile);

        for (int i = 0; i < parameterCombinations.Count; i++)
        {
            var (cropSettings, optimizationSettings) = parameterCombinations[i];
            var combinationStartTime = DateTime.Now;

            try
            {
                // Add timeout for each operation
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 seconds per combination
                
                var pdfVersionStr = optimizationSettings.TargetPdfVersion?.ToVersionString() ?? "Original";
                Console.WriteLine($"    [{fileName}] Testing combination {i + 1}: Method={cropSettings.Method}, Margin={cropSettings.Margin}, Compression={optimizationSettings.CompressionLevel?.ToString() ?? "Default"}, PDF={pdfVersionStr}");
                
                var croppedBytes = await PdfSmartCropper.CropAsync(inputBytes, cropSettings, optimizationSettings, null, cts.Token);
                
                var combinationTime = DateTime.Now - combinationStartTime;
                Console.WriteLine($"    [{fileName}] Combination {i + 1} completed in {combinationTime.TotalSeconds:F1}s, size: {FormatFileSize(croppedBytes.Length)}");
                
                if (croppedBytes.Length < bestResult.OptimizedSize)
                {
                    bestResult.OptimizedSize = croppedBytes.Length;
                    bestResult.BestCropSettings = cropSettings;
                    bestResult.BestOptimizationSettings = optimizationSettings;
                    Console.WriteLine($"    [{fileName}] ★ NEW BEST RESULT! Size: {FormatFileSize(croppedBytes.Length)}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"    [{fileName}] ⚠ TIMEOUT combination {i + 1} (more than 30 seconds) - skipping");
            }
            catch (Exception ex)
            {
                var combinationTime = DateTime.Now - combinationStartTime;
                Console.WriteLine($"    [{fileName}] ❌ Error with combination {i + 1} in {combinationTime.TotalSeconds:F1}s: {ex.Message}");
            }

            if ((i + 1) % 5 == 0 || i == parameterCombinations.Count - 1)
            {
                var elapsed = stopwatch.Elapsed;
                var progress = (double)(i + 1) / parameterCombinations.Count * 100;
                var avgTimePerCombination = elapsed.TotalSeconds / (i + 1);
                var estimatedTotal = TimeSpan.FromSeconds(avgTimePerCombination * parameterCombinations.Count);
                var remaining = estimatedTotal - elapsed;
                Console.WriteLine($"    [{fileName}] === PROGRESS: {progress:F0}% ({i + 1}/{parameterCombinations.Count}) - remaining ~{remaining:mm\\:ss} ===");
            }
        }

        stopwatch.Stop();
        bestResult.ProcessingTime = stopwatch.Elapsed.ToString(@"mm\:ss");

        Console.WriteLine($"  Best size: {FormatFileSize(bestResult.OptimizedSize)}");
        Console.WriteLine($"  Savings: {bestResult.SavingsPercent:F1}%");
        Console.WriteLine($"  Processing time: {bestResult.ProcessingTime}");

        return bestResult;
    }

    private List<(CropSettings, PdfOptimizationSettings)> GenerateOptimizedParameterCombinations()
    {
        var combinations = new List<(CropSettings, PdfOptimizationSettings)>();

        Console.WriteLine("  Generating combinations with fixed crop settings...");

        // Fixed optimal crop settings
        var fixedCropSettings = new CropSettings(CropMethod.ContentBased, excludeEdgeTouchingObjects: true, margin: 0.5f);
        Console.WriteLine("  Fixed crop settings: ContentBased, exclude edges: true, margin: 0.5 points");

        // Vary compression parameters including all PDF versions
        var compressionCombinations = new List<PdfOptimizationSettings>
        {
            // 1. Basic settings (no optimization)
            PdfOptimizationSettings.Default,
            
            // 2. Compression level 1 only (fast)
            new PdfOptimizationSettings(compressionLevel: 1),
            
            // 3. Compression level 5 only (medium)
            new PdfOptimizationSettings(compressionLevel: 5),
            
            // 4. Compression level 9 only (maximum)
            new PdfOptimizationSettings(compressionLevel: 9),
            
            // 5. Full compression
            new PdfOptimizationSettings(enableFullCompression: true),
            
            // 6. Smart mode
            new PdfOptimizationSettings(enableSmartMode: true),
            
            // 7. Remove unused objects
            new PdfOptimizationSettings(removeUnusedObjects: true),
            
            // 8. Remove XMP metadata
            new PdfOptimizationSettings(removeXmpMetadata: true),
            
            // 9. Clear document info
            new PdfOptimizationSettings(clearDocumentInfo: true),
            
            // 10. Remove embedded standard fonts
            new PdfOptimizationSettings(removeEmbeddedStandardFonts: true),
            
            // 11-19. Tests of different PDF versions with basic compression
            new PdfOptimizationSettings(compressionLevel: 9, targetPdfVersion: PdfCompatibilityLevel.Pdf10),
            new PdfOptimizationSettings(compressionLevel: 9, targetPdfVersion: PdfCompatibilityLevel.Pdf11),
            new PdfOptimizationSettings(compressionLevel: 9, targetPdfVersion: PdfCompatibilityLevel.Pdf12),
            new PdfOptimizationSettings(compressionLevel: 9, targetPdfVersion: PdfCompatibilityLevel.Pdf13),
            new PdfOptimizationSettings(compressionLevel: 9, targetPdfVersion: PdfCompatibilityLevel.Pdf14),
            new PdfOptimizationSettings(compressionLevel: 9, targetPdfVersion: PdfCompatibilityLevel.Pdf15),
            new PdfOptimizationSettings(compressionLevel: 9, targetPdfVersion: PdfCompatibilityLevel.Pdf16),
            new PdfOptimizationSettings(compressionLevel: 9, targetPdfVersion: PdfCompatibilityLevel.Pdf17),
            new PdfOptimizationSettings(compressionLevel: 9, targetPdfVersion: PdfCompatibilityLevel.Pdf20),
            
            // 20. Compression 9 + full compression
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true),
            
            // 21. Compression 9 + smart mode
            new PdfOptimizationSettings(compressionLevel: 9, enableSmartMode: true),
            
            // 22. Full compression + smart mode
            new PdfOptimizationSettings(enableFullCompression: true, enableSmartMode: true),
            
            // 23. Compression 9 + full compression + smart mode
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, enableSmartMode: true),
            
            // 24. Compression 9 + full compression + remove objects
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, removeUnusedObjects: true),
            
            // 25. Compression 9 + full compression + smart mode + remove objects
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, enableSmartMode: true, removeUnusedObjects: true),
            
            // 26. Compression 9 + full compression + smart mode + remove metadata
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, enableSmartMode: true, removeXmpMetadata: true),
            
            // 27. Compression 9 + full compression + smart mode + clear document
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, enableSmartMode: true, clearDocumentInfo: true),
            
            // 28. Maximum settings with PDF 1.4 (most compatible new version)
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, enableSmartMode: true,
                                       removeUnusedObjects: true, removeXmpMetadata: true, clearDocumentInfo: true,
                                       targetPdfVersion: PdfCompatibilityLevel.Pdf14),
            
            // 29. Maximum settings with PDF 1.7 (modern version)
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, enableSmartMode: true,
                                       removeUnusedObjects: true, removeXmpMetadata: true, clearDocumentInfo: true,
                                       targetPdfVersion: PdfCompatibilityLevel.Pdf17),
            
            // 30. Maximum settings with PDF 2.0 (newest version)
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, enableSmartMode: true,
                                       removeUnusedObjects: true, removeXmpMetadata: true, clearDocumentInfo: true,
                                       targetPdfVersion: PdfCompatibilityLevel.Pdf20),
            
            // 31. Absolute maximum of all settings (no version)
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, enableSmartMode: true,
                                       removeUnusedObjects: true, removeXmpMetadata: true, clearDocumentInfo: true,
                                       removeEmbeddedStandardFonts: true),
            
            // 32. Absolute maximum of all settings + PDF 2.0
            new PdfOptimizationSettings(compressionLevel: 9, enableFullCompression: true, enableSmartMode: true,
                                       removeUnusedObjects: true, removeXmpMetadata: true, clearDocumentInfo: true,
                                       removeEmbeddedStandardFonts: true, targetPdfVersion: PdfCompatibilityLevel.Pdf20)
        };

        // Create combinations with fixed crop settings
        foreach (var optimizationSettings in compressionCombinations)
        {
            combinations.Add((fixedCropSettings, optimizationSettings));
        }

        Console.WriteLine($"  Created {combinations.Count} compression combinations with fixed crop settings");
        return combinations;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        const int scale = 1024;
        string[] orders = { "B", "KB", "MB", "GB", "TB" };
        
        int orderIndex = 0;
        double size = bytes;
        
        while (size >= scale && orderIndex < orders.Length - 1)
        {
            size /= scale;
            orderIndex++;
        }
        
        return $"{size:F2} {orders[orderIndex]}";
    }

    public void PrintResults(List<OptimizationResult> results)
    {
        Console.WriteLine("=== OPTIMIZATION RESULTS ===");
        Console.WriteLine();

        // Sort by savings percentage
        var sortedResults = results.OrderByDescending(r => r.SavingsPercent).ToList();

        Console.WriteLine("Files sorted by compression efficiency:");
        Console.WriteLine();

        for (int i = 0; i < sortedResults.Count; i++)
        {
            var result = sortedResults[i];
            Console.WriteLine($"{i + 1}. {result.FileName}");
            Console.WriteLine($"   Original size: {FormatFileSize(result.OriginalSize)}");
            Console.WriteLine($"   Optimized: {FormatFileSize(result.OptimizedSize)}");
            Console.WriteLine($"   Savings: {result.SavingsPercent:F1}% ({FormatFileSize(result.OriginalSize - result.OptimizedSize)})");
            Console.WriteLine($"   Processing time: {result.ProcessingTime}");
            Console.WriteLine($"   Best settings:");
            Console.WriteLine($"     - Method: {result.BestCropSettings.Method}");
            Console.WriteLine($"     - Margin: {result.BestCropSettings.Margin} points");
            Console.WriteLine($"     - Exclude edges: {result.BestCropSettings.ExcludeEdgeTouchingObjects}");
            Console.WriteLine($"     - Compression: {result.BestOptimizationSettings.CompressionLevel?.ToString() ?? "Default"}");
            Console.WriteLine($"     - PDF version: {result.BestOptimizationSettings.TargetPdfVersion?.ToVersionString() ?? "Original"}");
            Console.WriteLine($"     - Full compression: {result.BestOptimizationSettings.EnableFullCompression}");
            Console.WriteLine($"     - Smart mode: {result.BestOptimizationSettings.EnableSmartMode}");
            Console.WriteLine($"     - Remove unused objects: {result.BestOptimizationSettings.RemoveUnusedObjects}");
            Console.WriteLine($"     - Remove XMP metadata: {result.BestOptimizationSettings.RemoveXmpMetadata}");
            Console.WriteLine($"     - Clear document info: {result.BestOptimizationSettings.ClearDocumentInfo}");
            Console.WriteLine($"     - Remove standard fonts: {result.BestOptimizationSettings.RemoveEmbeddedStandardFonts}");
            Console.WriteLine();
        }

        var bestFile = sortedResults.First();
        var totalOriginalSize = results.Sum(r => r.OriginalSize);
        var totalOptimizedSize = results.Sum(r => r.OptimizedSize);
        var totalSavings = totalOriginalSize - totalOptimizedSize;
        var averageSavingsPercent = (double)totalSavings / totalOriginalSize * 100;

        Console.WriteLine("=== OVERALL STATISTICS ===");
        Console.WriteLine($"Files processed: {results.Count}");
        Console.WriteLine($"Total original size: {FormatFileSize(totalOriginalSize)}");
        Console.WriteLine($"Total optimized size: {FormatFileSize(totalOptimizedSize)}");
        Console.WriteLine($"Total savings: {FormatFileSize(totalSavings)} ({averageSavingsPercent:F1}%)");
        Console.WriteLine();
        Console.WriteLine($"BEST RESULT: {bestFile.FileName} with {bestFile.SavingsPercent:F1}% savings");
    }

    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_outputDirectory))
            {
                Directory.Delete(_outputDirectory, true);
                Console.WriteLine($"Temporary folder deleted: {_outputDirectory}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete temporary folder: {ex.Message}");
        }
    }
}
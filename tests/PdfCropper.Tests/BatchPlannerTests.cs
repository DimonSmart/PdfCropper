using System.IO;
using System.Linq;
using DimonSmart.PdfCropper.Cli;
using Xunit;

namespace PdfCropper.Tests;

public sealed class BatchPlannerTests : IDisposable
{
    private readonly string tempDirectory;

    public BatchPlannerTests()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void Plan_with_masks_maps_each_input_to_expected_output()
    {
        var inputDirectory = Path.Combine(tempDirectory, "input");
        Directory.CreateDirectory(inputDirectory);

        File.WriteAllBytes(Path.Combine(inputDirectory, "doc1.pdf"), []);
        File.WriteAllBytes(Path.Combine(inputDirectory, "doc2.pdf"), []);
        File.WriteAllBytes(Path.Combine(inputDirectory, "skip.txt"), []);

        var inputMask = Path.Combine(inputDirectory, "*.pdf");
        var outputMask = Path.Combine(tempDirectory, "out", "*_CROP.pdf");

        var plan = BatchPlanner.CreatePlan(inputMask, outputMask);

        Assert.True(plan.Success);
        Assert.Equal(2, plan.Files.Count);

        var first = plan.Files.Single(f => f.InputPath.EndsWith("doc1.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(Path.Combine("out", "doc1_CROP.pdf"), first.OutputPath, StringComparison.OrdinalIgnoreCase);

        var second = plan.Files.Single(f => f.InputPath.EndsWith("doc2.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(Path.Combine("out", "doc2_CROP.pdf"), second.OutputPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_with_directory_output_preserves_relative_structure()
    {
        var inputDirectory = Path.Combine(tempDirectory, "src");
        var nested = Path.Combine(inputDirectory, "nested");
        Directory.CreateDirectory(nested);

        File.WriteAllBytes(Path.Combine(nested, "report.pdf"), []);

        var inputMask = Path.Combine(inputDirectory, "**", "*.pdf");
        var outputDirectory = Path.Combine(tempDirectory, "cropped") + Path.DirectorySeparatorChar;

        var plan = BatchPlanner.CreatePlan(inputMask, outputDirectory);

        Assert.True(plan.Success);
        var entry = Assert.Single(plan.Files);
        Assert.EndsWith(Path.Combine("nested", "report.pdf"), entry.OutputPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_without_output_mask_requires_single_match()
    {
        var inputDirectory = Path.Combine(tempDirectory, "data");
        Directory.CreateDirectory(inputDirectory);

        File.WriteAllBytes(Path.Combine(inputDirectory, "a.pdf"), []);
        File.WriteAllBytes(Path.Combine(inputDirectory, "b.pdf"), []);

        var inputMask = Path.Combine(inputDirectory, "*.pdf");
        var outputPath = Path.Combine(tempDirectory, "result.pdf");

        var plan = BatchPlanner.CreatePlan(inputMask, outputPath);

        Assert.False(plan.Success);
        Assert.Equal(1, plan.ExitCode);
        Assert.NotNull(plan.ErrorMessage);
    }

    [Fact]
    public void Plan_returns_error_when_no_files_match()
    {
        var inputDirectory = Path.Combine(tempDirectory, "empty");
        Directory.CreateDirectory(inputDirectory);

        var inputMask = Path.Combine(inputDirectory, "*.pdf");
        var outputMask = Path.Combine(tempDirectory, "out", "*.pdf");

        var plan = BatchPlanner.CreatePlan(inputMask, outputMask);

        Assert.False(plan.Success);
        Assert.Equal(2, plan.ExitCode);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
        catch
        {
        }
    }
}

using ParameterOptimizer;

Console.WriteLine("=== PDF PARAMETER OPTIMIZER ===");
Console.WriteLine("Program for finding optimal PDF compression parameters");
Console.WriteLine();

const string booksDirectory = @"C:\Books\";

if (!Directory.Exists(booksDirectory))
{
    Console.WriteLine($"Error: Folder {booksDirectory} not found!");
    Console.WriteLine("Make sure the folder exists and contains PDF files.");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

var pdfFiles = Directory.GetFiles(booksDirectory, "*.pdf", SearchOption.TopDirectoryOnly);
if (pdfFiles.Length == 0)
{
    Console.WriteLine($"No PDF files found in folder {booksDirectory}!");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

Console.WriteLine($"Found {pdfFiles.Length} PDF files for analysis:");
foreach (var file in pdfFiles.Take(10)) // Show first 10
{
    var fileInfo = new FileInfo(file);
    Console.WriteLine($"  - {Path.GetFileName(file)} ({FormatFileSize(fileInfo.Length)})");
}

if (pdfFiles.Length > 10)
{
    Console.WriteLine($"  ... and {pdfFiles.Length - 10} more files");
}

Console.WriteLine();
Console.WriteLine("WARNING: This process may take a considerable amount of time!");
Console.WriteLine("Multiple parameter combinations will be tested for each file.");
Console.WriteLine();
Console.Write("Continue? (y/N): ");

var response = Console.ReadLine();
if (string.IsNullOrEmpty(response) || !response.Trim().ToLower().StartsWith("y"))
{
    Console.WriteLine("Operation cancelled.");
    return;
}

Console.WriteLine();
Console.WriteLine("Starting optimization...");
Console.WriteLine();

var optimizer = new ParameterOptimizer.ParameterOptimizer(booksDirectory);
try
{
    var startTime = DateTime.Now;
    var results = await optimizer.OptimizeAllFilesAsync();
    var endTime = DateTime.Now;

    Console.WriteLine();
    Console.WriteLine($"Analysis completed in {(endTime - startTime):hh\\:mm\\:ss}");
    Console.WriteLine();

    optimizer.PrintResults(results);
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");
    Console.WriteLine($"Details: {ex}");
}
finally
{
    optimizer.Cleanup();
}

Console.WriteLine();
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

static string FormatFileSize(long bytes)
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
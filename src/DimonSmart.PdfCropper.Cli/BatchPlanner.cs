using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace DimonSmart.PdfCropper.Cli;

public static class BatchPlanner
{
    private static readonly char[] GlobSpecialChars = ['*', '?'];

    internal static bool ContainsGlobPattern(string path) => !string.IsNullOrEmpty(path) && path.IndexOfAny(GlobSpecialChars) >= 0;

    public static BatchPlanningResult CreatePlan(string inputPattern, string outputPattern)
    {
        if (string.IsNullOrWhiteSpace(inputPattern))
        {
            return BatchPlanningResult.Failure("Input mask cannot be empty.", 1);
        }

        var (baseDirectory, relativePattern) = ExtractBaseDirectory(inputPattern);
        if (!Directory.Exists(baseDirectory))
        {
            return BatchPlanningResult.Failure($"Input directory '{baseDirectory}' was not found.", 2);
        }

        var normalizedPattern = NormalizeForMatcher(relativePattern);
        var matcher = OperatingSystem.IsWindows()
            ? new Matcher(StringComparison.OrdinalIgnoreCase)
            : new Matcher(StringComparison.Ordinal);
        matcher.AddInclude(normalizedPattern);

        var directoryInfo = new DirectoryInfo(baseDirectory);
        var result = matcher.Execute(new DirectoryInfoWrapper(directoryInfo));
        var matches = result.Files.ToList();
        if (matches.Count == 0)
        {
            return BatchPlanningResult.Failure($"No files matched the mask '{inputPattern}'.", 2);
        }

        var orderedRelativePaths = SortMatches(matches);

        var outputHasMask = ContainsGlobPattern(outputPattern);
        var outputLooksLikeDirectory = !outputHasMask && LooksLikeDirectory(outputPattern);

        if (outputHasMask)
        {
            var regex = GlobToRegex(normalizedPattern, out var wildcardCount);
            if (wildcardCount == 0)
            {
                return BatchPlanningResult.Failure("Output mask contains wildcards, but the input mask does not have any capturable segments.", 1);
            }

            var operations = new List<PlannedFile>(matches.Count);
            var comparer = CreatePathComparer();
            var producedOutputs = new HashSet<string>(comparer);

            foreach (var relativePath in orderedRelativePaths)
            {
                var matchResult = regex.Match(relativePath);
                if (!matchResult.Success)
                {
                    continue;
                }

                string resolvedOutput;
                try
                {
                    resolvedOutput = ApplyOutputPattern(outputPattern, matchResult.Groups);
                }
                catch (InvalidOperationException ex)
                {
                    return BatchPlanningResult.Failure(ex.Message, 1);
                }

                var fullOutputPath = Path.GetFullPath(resolvedOutput);
                if (!producedOutputs.Add(fullOutputPath))
                {
                    return BatchPlanningResult.Failure($"Output path '{fullOutputPath}' would be produced multiple times. Adjust the output mask to avoid collisions.", 1);
                }

                var fullInputPath = Path.GetFullPath(Path.Combine(baseDirectory, ToSystemPath(relativePath)));
                operations.Add(new PlannedFile(fullInputPath, fullOutputPath));
            }

            return BatchPlanningResult.CreateSuccess(operations);
        }

        if (outputLooksLikeDirectory)
        {
            var targetDirectory = Path.GetFullPath(outputPattern);
            var operations = new List<PlannedFile>(matches.Count);

            foreach (var relativePath in orderedRelativePaths)
            {
                var fullInputPath = Path.GetFullPath(Path.Combine(baseDirectory, ToSystemPath(relativePath)));
                var fullOutputPath = Path.GetFullPath(Path.Combine(targetDirectory, ToSystemPath(relativePath)));
                operations.Add(new PlannedFile(fullInputPath, fullOutputPath));
            }

            return BatchPlanningResult.CreateSuccess(operations);
        }

        if (orderedRelativePaths.Count > 1)
        {
            return BatchPlanningResult.Failure("Multiple files match the input mask. Provide an output mask or a directory path to avoid overwriting.", 1);
        }

        var singleRelative = orderedRelativePaths[0];
        var singleInput = Path.GetFullPath(Path.Combine(baseDirectory, ToSystemPath(singleRelative)));
        var singleOutput = Path.GetFullPath(outputPattern);

        return BatchPlanningResult.CreateSuccess(new[] { new PlannedFile(singleInput, singleOutput) });
    }

    public static InputDiscoveryResult DiscoverInputs(string inputPattern)
    {
        if (string.IsNullOrWhiteSpace(inputPattern))
        {
            return InputDiscoveryResult.Failure("Input mask cannot be empty.", 1);
        }

        var (baseDirectory, relativePattern) = ExtractBaseDirectory(inputPattern);
        if (!Directory.Exists(baseDirectory))
        {
            return InputDiscoveryResult.Failure($"Input directory '{baseDirectory}' was not found.", 2);
        }

        var normalizedPattern = NormalizeForMatcher(relativePattern);
        var matcher = OperatingSystem.IsWindows()
            ? new Matcher(StringComparison.OrdinalIgnoreCase)
            : new Matcher(StringComparison.Ordinal);
        matcher.AddInclude(normalizedPattern);

        var directoryInfo = new DirectoryInfo(baseDirectory);
        var result = matcher.Execute(new DirectoryInfoWrapper(directoryInfo));
        var matches = result.Files.ToList();
        if (matches.Count == 0)
        {
            return InputDiscoveryResult.Failure($"No files matched the mask '{inputPattern}'.", 2);
        }

        var orderedRelativePaths = SortMatches(matches);

        var inputs = new List<string>(orderedRelativePaths.Count);
        foreach (var relativePath in orderedRelativePaths)
        {
            var fullInputPath = Path.GetFullPath(Path.Combine(baseDirectory, ToSystemPath(relativePath)));
            inputs.Add(fullInputPath);
        }

        return InputDiscoveryResult.CreateSuccess(inputs);
    }

    internal static Regex GlobToRegex(string pattern, out int wildcardCount)
    {
        wildcardCount = 0;
        var builder = new StringBuilder("^");

        for (var i = 0; i < pattern.Length; i++)
        {
            var character = pattern[i];
            if (character == '*')
            {
                var isDouble = i + 1 < pattern.Length && pattern[i + 1] == '*';
                wildcardCount++;
                if (isDouble)
                {
                    builder.Append("(.*)");
                    i++;
                }
                else
                {
                    builder.Append("([^/]*)");
                }

                continue;
            }

            if (character == '?')
            {
                wildcardCount++;
                builder.Append("([^/])");
                continue;
            }

            if (character == '/')
            {
                builder.Append('/');
                continue;
            }

            builder.Append(Regex.Escape(character.ToString()));
        }

        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    internal static string ApplyOutputPattern(string outputPattern, GroupCollection groups)
    {
        var builder = new StringBuilder(outputPattern.Length + 16);
        var groupIndex = 1;

        for (var i = 0; i < outputPattern.Length; i++)
        {
            var character = outputPattern[i];
            if (character == '*')
            {
                if (groupIndex >= groups.Count)
                {
                    throw new InvalidOperationException("Output mask contains more wildcards than the input mask captures.");
                }

                var isDouble = i + 1 < outputPattern.Length && outputPattern[i + 1] == '*';
                builder.Append(groups[groupIndex].Value);
                groupIndex++;
                if (isDouble)
                {
                    i++;
                }

                continue;
            }

            if (character == '?')
            {
                if (groupIndex >= groups.Count)
                {
                    throw new InvalidOperationException("Output mask contains more wildcards than the input mask captures.");
                }

                builder.Append(groups[groupIndex].Value);
                groupIndex++;
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static (string BaseDirectory, string RelativePattern) ExtractBaseDirectory(string inputPattern)
    {
        var normalized = inputPattern.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var wildcardIndex = normalized.IndexOfAny(GlobSpecialChars);

        if (wildcardIndex == -1)
        {
            var fullPath = Path.GetFullPath(normalized);
            var directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
            var fileName = Path.GetFileName(fullPath);
            return (directory, string.IsNullOrEmpty(fileName) ? "*" : fileName);
        }

        var prefix = normalized[..wildcardIndex];
        var separatorIndex = prefix.LastIndexOf(Path.DirectorySeparatorChar);

        if (separatorIndex == -1)
        {
            return (Directory.GetCurrentDirectory(), normalized[0..]);
        }

        var directoryPart = normalized[..(separatorIndex + 1)];
        var patternPart = normalized[(separatorIndex + 1)..];
        if (string.IsNullOrEmpty(patternPart))
        {
            patternPart = "*";
        }

        var fullDirectory = Path.GetFullPath(directoryPart);
        return (fullDirectory, patternPart);
    }

    private static string NormalizeForMatcher(string value) => value.Replace('\\', '/');

    private static List<string> SortMatches(List<FilePatternMatch> matches)
    {
        var comparer = CreatePathComparer();
        return matches
            .Select(match => NormalizeForMatcher(match.Path))
            .OrderBy(path => path, comparer)
            .ToList();
    }

    private static string ToSystemPath(string value) => value.Replace('/', Path.DirectorySeparatorChar);

    private static bool LooksLikeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return true;
        }

        return Directory.Exists(path);
    }

    private static StringComparer CreatePathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}

public sealed record PlannedFile(string InputPath, string OutputPath);

public sealed record BatchPlanningResult(IReadOnlyList<PlannedFile> Files, string? ErrorMessage, int ExitCode)
{
    public bool Success => ErrorMessage is null;

    public static BatchPlanningResult CreateSuccess(IEnumerable<PlannedFile> files)
    {
        var list = files as IReadOnlyList<PlannedFile> ?? files.ToList();
        return new BatchPlanningResult(list, null, 0);
    }

    public static BatchPlanningResult Failure(string message, int exitCode) => new(Array.Empty<PlannedFile>(), message, exitCode);
}

public sealed record InputDiscoveryResult(IReadOnlyList<string> Files, string? ErrorMessage, int ExitCode)
{
    public bool Success => ErrorMessage is null;

    public static InputDiscoveryResult CreateSuccess(IEnumerable<string> files)
    {
        var list = files as IReadOnlyList<string> ?? files.ToList();
        return new InputDiscoveryResult(list, null, 0);
    }

    public static InputDiscoveryResult Failure(string message, int exitCode) => new(Array.Empty<string>(), message, exitCode);
}

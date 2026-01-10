using System;
using System.Collections.Generic;
using System.Globalization;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Parses and evaluates page range expressions such as "1,4-6,8-".
/// </summary>
public sealed class PageRange
{
    private readonly List<PageRangeSegment> segments = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PageRange"/> class.
    /// </summary>
    /// <param name="rangeExpression">Page range expression (e.g. "1,4-6,8-").</param>
    public PageRange(string rangeExpression)
    {
        Expression = rangeExpression ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rangeExpression))
        {
            return;
        }

        var tokens = rangeExpression.Split(',');
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index].Trim();
            if (token.Length == 0)
            {
                SetError($"Page range segment {index + 1} is empty.");
                return;
            }

            if (!TryParseSegment(token, out var segment, out var error))
            {
                SetError(error ?? $"Page range segment '{token}' is invalid.");
                return;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            SetError("Page range expression does not contain any ranges.");
        }
    }

    /// <summary>
    /// Gets the original page range expression.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Gets a value indicating whether the expression contains parsing errors.
    /// </summary>
    public bool HasError { get; private set; }

    /// <summary>
    /// Gets an explanation when <see cref="HasError"/> is true.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Returns true when the specified 1-based page number is in the configured ranges.
    /// </summary>
    public bool Contains(int pageNumber)
    {
        if (pageNumber < 1 || HasError)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Contains(pageNumber))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Expression;

    private void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        segments.Clear();
    }

    private static bool TryParseSegment(string token, out PageRangeSegment segment, out string? error)
    {
        segment = default;
        error = null;

        var firstDash = token.IndexOf('-');
        if (firstDash < 0)
        {
            if (!TryParsePositiveInt(token, out var page))
            {
                error = $"Page number '{token}' is not a positive integer.";
                return false;
            }

            segment = new PageRangeSegment(page, page);
            return true;
        }

        if (firstDash != token.LastIndexOf('-'))
        {
            error = $"Page range segment '{token}' contains more than one dash.";
            return false;
        }

        var left = token[..firstDash].Trim();
        var right = token[(firstDash + 1)..].Trim();

        if (left.Length == 0 && right.Length == 0)
        {
            error = "Page range segment '-' is not valid.";
            return false;
        }

        if (left.Length == 0)
        {
            if (!TryParsePositiveInt(right, out var end))
            {
                error = $"Page range end '{right}' is not a positive integer.";
                return false;
            }

            segment = new PageRangeSegment(null, end);
            return true;
        }

        if (right.Length == 0)
        {
            if (!TryParsePositiveInt(left, out var start))
            {
                error = $"Page range start '{left}' is not a positive integer.";
                return false;
            }

            segment = new PageRangeSegment(start, null);
            return true;
        }

        if (!TryParsePositiveInt(left, out var startValue))
        {
            error = $"Page range start '{left}' is not a positive integer.";
            return false;
        }

        if (!TryParsePositiveInt(right, out var endValue))
        {
            error = $"Page range end '{right}' is not a positive integer.";
            return false;
        }

        if (startValue > endValue)
        {
            error = $"Page range start '{startValue}' cannot be greater than end '{endValue}'.";
            return false;
        }

        segment = new PageRangeSegment(startValue, endValue);
        return true;
    }

    private static bool TryParsePositiveInt(string token, out int value)
    {
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            value = default;
            return false;
        }

        return value > 0;
    }

    private readonly struct PageRangeSegment
    {
        public PageRangeSegment(int? start, int? end)
        {
            Start = start;
            End = end;
        }

        public int? Start { get; }

        public int? End { get; }

        public bool Contains(int pageNumber)
        {
            if (Start.HasValue && pageNumber < Start.Value)
            {
                return false;
            }

            if (End.HasValue && pageNumber > End.Value)
            {
                return false;
            }

            return true;
        }
    }
}

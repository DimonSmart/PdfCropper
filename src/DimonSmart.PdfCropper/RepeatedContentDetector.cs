using System;
using System.Collections.Generic;

namespace DimonSmart.PdfCropper;

using ContentObjectKey = ContentBasedCroppingStrategy.ContentObjectKey;
using PageContentAnalysis = ContentBasedCroppingStrategy.PageContentAnalysis;

internal static class RepeatedContentDetector
{
    public static HashSet<ContentObjectKey> Detect(
        IEnumerable<PageContentAnalysis?> analyses,
        double thresholdPercent,
        CancellationToken ct)
    {
        var occurrences = new Dictionary<ContentObjectKey, int>();
        var analyzedPageCount = 0;

        foreach (var analysis in analyses)
        {
            ct.ThrowIfCancellationRequested();

            if (analysis == null)
            {
                continue;
            }

            analyzedPageCount++;
            var pageObjects = new HashSet<ContentObjectKey>();
            foreach (var detectedObject in analysis.Objects)
            {
                pageObjects.Add(detectedObject.Key);
            }

            foreach (var key in pageObjects)
            {
                occurrences.TryGetValue(key, out var count);
                occurrences[key] = count + 1;
            }
        }

        if (analyzedPageCount == 0)
        {
            return new HashSet<ContentObjectKey>();
        }

        var requiredCount = (int)Math.Ceiling(analyzedPageCount * thresholdPercent / 100d);
        if (requiredCount <= 0)
        {
            requiredCount = 1;
        }

        var repeatedObjects = new HashSet<ContentObjectKey>();
        foreach (var (key, count) in occurrences)
        {
            if (count >= requiredCount)
            {
                repeatedObjects.Add(key);
            }
        }

        return repeatedObjects;
    }
}

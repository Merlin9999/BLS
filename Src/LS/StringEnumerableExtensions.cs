using System.Collections.Immutable;

namespace CLConsole;

public static class StringEnumerableExtensions
{
    public static bool StartsWith(this IEnumerable<string> sourcePathSegments, IEnumerable<string> matchPathSegments, int matchPathSegmentCount, StringComparer? comparer = null)
    {
        return sourcePathSegments.StartsWith(matchPathSegments.Take(matchPathSegmentCount), comparer);
    }

    public static bool StartsWith(this IEnumerable<string> sourcePathSegments, IEnumerable<string> matchPathSegments, StringComparer? comparer = null)
    {
        comparer ??= StringComparer.InvariantCulture;

        foreach (string matchSegment in matchPathSegments)
        {
            string? sourceSegment = sourcePathSegments.FirstOrDefault();
            if (sourceSegment == null)
                return false;

            if (comparer.Compare(sourceSegment, matchSegment) != 0)
                return false;

            sourcePathSegments = sourcePathSegments.Skip(1);
        }

        return true;
    }
}
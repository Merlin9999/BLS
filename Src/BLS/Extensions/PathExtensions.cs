using System.Globalization;
using BLS.Globbers;

namespace BLS.Extensions;

public static class PathExtensions
{
    public static bool IsParentPathContainingNestedPathOf(this string basePath, string nestedPath, bool assumeFileSystemIsCaseSensitive)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(nestedPath))
            return false;

        var baseDir = new DirectoryInfo(AbstractGlobber.NormalizePathSegmentSeparators(AbstractGlobber.TrimTrailingPathSegmentSeparators(basePath)));
        var targetDir = new DirectoryInfo(AbstractGlobber.NormalizePathSegmentSeparators(AbstractGlobber.TrimTrailingPathSegmentSeparators(nestedPath)));

        CompareOptions compareOptions = assumeFileSystemIsCaseSensitive
            ? CompareOptions.Ordinal
            : CompareOptions.OrdinalIgnoreCase;

        while (targetDir != null)
        {
            if (string.Compare(targetDir.FullName, baseDir.FullName, CultureInfo.InvariantCulture, compareOptions) == 0)
                return true;

            targetDir = targetDir.Parent;
        }

        return false;
    }
}
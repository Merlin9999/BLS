using System.Collections.Immutable;
using System.Security;
using System.Text.RegularExpressions;
using DotNet.Globbing;

namespace BLS;

public class ImprovedGlobber : AbstractGlobber
{
    // Derived from: https://stackoverflow.com/a/54300816/677612
    // Check out: https://stackoverflow.com/a/34580159/677612

    private static readonly Regex DriveOnlyRegex = new Regex("^[a-zA-Z][:]$");

    public ImprovedGlobber(IGlobberArgs args) 
        : base(args)
    {
        this.Args.IncludeGlobPaths = this.Args.IncludeGlobPaths
            .Select(path => string.Join("/", SplitPathIntoSegments(path).Where(s => s != ".")))
            .ToImmutableList();
        this.Args.ExcludeGlobPaths = this.Args.ExcludeGlobPaths
            .Select(path => string.Join("/", SplitPathIntoSegments(path).Where(s => s != ".")))
            .ToImmutableList();
    }

    protected override IEnumerable<string> FindMatches(string basePath, IgnoredExceptionSet ignoredExceptions)
    {
        StringComparer stringComparer = this.Args.CaseSensitive 
            ? StringComparer.InvariantCulture 
            : StringComparer.InvariantCultureIgnoreCase;

        foreach (string globPath in this.Args.IncludeGlobPaths) 
            this.ValidateGlobPath(globPath, true);

        foreach (string globPath in this.Args.ExcludeGlobPaths)
            this.ValidateGlobPath(globPath, false);

        string normalizedBasePath = ToBackSlashPathSeparators(basePath);

        DirectoryInfo baseDir = new DirectoryInfo(normalizedBasePath);
        DirectoryInfo commonRootDir = this.DetermineCommonRootPathFromBasePathAndIncludes(baseDir, stringComparer);

        var includeGlobInfos = this.Args.IncludeGlobPaths
            .Select(g => BuildRelativeFileName(Path.Combine(baseDir.FullName, g), commonRootDir, baseDir))
            .Select(g => new {Path = g, Glob = Glob.Parse(g) })
            .ToImmutableList();

        ImmutableList<Glob> includeGlobs = includeGlobInfos.Select(x => x.Glob).ToImmutableList();
        ImmutableList<string> includePaths = includeGlobInfos.Select(x => x.Path).ToImmutableList();
        int minLevel = includePaths.Min(path => CalculateFolderSegmentCount(path));
        int maxLevel = minLevel < 0 ? -1 : includePaths.Max(path => CalculateFolderSegmentCount(path));
        
        ImmutableList<Glob> excludeGlobs = this.Args.ExcludeGlobPaths
            .Select(g => BuildRelativeFileName(Path.Combine(baseDir.FullName, g), commonRootDir, baseDir))
            .Select(g => Glob.Parse(g))
            .ToImmutableList();

        foreach (FileInfo fileInfo in this.EnumerateAllFiles(1, maxLevel,commonRootDir, excludeGlobs, commonRootDir, baseDir, ignoredExceptions))
        {
            string fileName = BuildRelativeFileName(fileInfo.FullName, commonRootDir, baseDir);

            if (includeGlobs.Any(glob => glob.IsMatch(fileName)) && !excludeGlobs.Any(glob => glob.IsMatch(fileName)))
                yield return fileName;
        }
    }

    private IEnumerable<FileInfo> EnumerateAllFiles(int level, int maxLevel, DirectoryInfo dirInfo, ImmutableList<Glob> excludeGlobs, 
        DirectoryInfo commonRootDir, DirectoryInfo baseDir, IgnoredExceptionSet ignoredExceptions)
    {
        if (maxLevel > 0 && level > maxLevel)
            yield break;

        ImmutableList<FileInfo> files = this.DoFileSysOp(
            () => dirInfo.EnumerateFiles().ToImmutableList(),
            () => ImmutableList<FileInfo>.Empty,
            ignoredExceptions);

        foreach (FileInfo fileInfo in files)
            yield return fileInfo;

        ImmutableList<DirectoryInfo> subDirInfos = this.DoFileSysOp(
            () => dirInfo.EnumerateDirectories().ToImmutableList(),
            () => ImmutableList<DirectoryInfo>.Empty,
            ignoredExceptions);

        foreach (DirectoryInfo subDirInfo in subDirInfos)
        {
            string fullFolderNamePath = BuildRelativeFileName(subDirInfo.FullName, commonRootDir, baseDir);

            if (excludeGlobs.Any(glob => glob.IsMatch(fullFolderNamePath)))
                continue;

            foreach (FileInfo fileInfo in this.EnumerateAllFiles(level + 1, maxLevel, subDirInfo, excludeGlobs, commonRootDir, baseDir, ignoredExceptions))
                yield return fileInfo;
        }
    }

    private T DoFileSysOp<T>(Func<T> operation, Func<T> getDefault, IgnoredExceptionSet ignoredExceptions)
    {
        try
        {
            return operation();
        }
        catch (SecurityException se) when (!this.Args.AbortOnFileSystemAccessExceptions)
        {
            ignoredExceptions.Add(se);
            return getDefault();
        }
        catch (UnauthorizedAccessException uae) when (!this.Args.AbortOnFileSystemAccessExceptions)
        {
            ignoredExceptions.Add(uae);
            return getDefault();
        }
        catch (DirectoryNotFoundException dnfe) when (!this.Args.AbortOnFileSystemAccessExceptions)
        {
            ignoredExceptions.Add(dnfe);
            return getDefault();
        }
        catch (IOException ioe) when (!this.Args.AbortOnFileSystemAccessExceptions)
        {
            ignoredExceptions.Add(ioe);
            return getDefault();
        }
    }

    private void ValidateGlobPath(string path, bool isIncludePath)
    {
        if (Path.IsPathRooted(path))
            throw new ArgumentException($"Glob path cannot be rooted! Path: \"{path}\"");

        string[] segments = SplitPathIntoSegments(path).Where(s => s != ".").ToArray();

        if (isIncludePath)
        {
            bool inParentSegmentPrefix = false;
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                string segment = segments[i];
                if (inParentSegmentPrefix)
                {
                    if (segment != "..")
                        throw new ArgumentException(
                            $"Include Glob path cannot have segment of \"..\" after first named directory! Path: \"{path}\"");
                }
                else
                {
                    if (segment == "..")
                        inParentSegmentPrefix = true;
                }
            }
        }
    }

    private (string RelativePrefix, string RelativePath) SplitPathByParentPrefix(string path)
    {
        var segments = SplitPathIntoSegments(path);
        return SplitPathByParentPrefix(path, segments);
    }

    private static (string RelativePrefix, string RelativePath) SplitPathByParentPrefix(string[] segments)
    {
        string path = Path.Combine(segments);
        return SplitPathByParentPrefix(path, segments);
    }

    private static (string RelativePrefix, string RelativePath) SplitPathByParentPrefix(string path, string[] segments)
    {
        int index = GetIndexOfLastSpecialFolder();
        if (index < 0)
            return (string.Empty, path);

        return (Path.Combine(segments.Take(index + 1).ToArray()),
            Path.Combine(segments.Skip(index + 1).ToArray()));

        int GetIndexOfLastSpecialFolder()
        {
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                string segment = segments[i];
                if (segment == "..")
                    return i;
            }

            return -1;
        }
    }

    private static string[] SplitPathIntoSegments(string path)
    {
        string normalizedPath = ToBackSlashPathSeparators(path);
        string[] segments = normalizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return segments;
    }

    private DirectoryInfo DetermineCommonRootPathFromBasePathAndIncludes(DirectoryInfo basePath, StringComparer comparer)
    {
        var rootDirectoryList = this.Args.IncludeGlobPaths
            .Select(p => new { Path = p, RelativePathPrefix = Path.Combine(this.SplitPathByParentPrefix(p).RelativePrefix) })
            .Where(x => x.RelativePathPrefix.Length > 0)
            .Select(x => new DirectoryInfo(Path.GetFullPath(Path.Combine(basePath.FullName, x.RelativePathPrefix))))
            .Prepend(basePath)
            .ToImmutableList();

        var rootPathSegmentsList = rootDirectoryList
            .Select(di => SplitPathIntoSegments(di.FullName))
            .ToImmutableList();

        int minTotalSegments = rootPathSegmentsList
            .Select(x => x.Length)
            .Min();

        var matchingPathSegments = Enumerable.Range(1, minTotalSegments)
            .Reverse()
            .Select(x => rootPathSegmentsList.First().Take(x).ToImmutableList())
            .Where(pathSegsToMatch => rootPathSegmentsList.All(pathSegs => pathSegs.StartsWith(pathSegsToMatch, comparer)));

        string commonRootPath = Path.Combine(matchingPathSegments.First().ToArray());
        if (DriveOnlyRegex.IsMatch(commonRootPath))
            commonRootPath = commonRootPath + '\\';
        else if (Path.IsPathRooted(commonRootPath) && !(commonRootPath.EndsWith('\\') || commonRootPath.EndsWith('/')))
            commonRootPath = commonRootPath + '\\';

        return new DirectoryInfo(commonRootPath);
    }
}
using System.Collections.Immutable;
using System.IO;
using System.Security;
using DotNet.Globbing;
using static System.Net.WebRequestMethods;

namespace CLConsole;

public class ImprovedGlobber : AbstractGlobber
{
    // Derived from: https://stackoverflow.com/a/54300816/677612
    // Check out: https://stackoverflow.com/a/34580159/677612

    private bool? _doPathSeparatorReplace;
    private bool DoPathSeparatorReplace => this._doPathSeparatorReplace ??= Path.DirectorySeparatorChar == '\\' && Path.AltDirectorySeparatorChar == '/';
    
    public ImprovedGlobber(IGlobberArgs args) 
        : base(args)
    {
    }

    protected override IEnumerable<string> FindMatches(string basePath, List<Exception> ignoredFileAccessExceptions)
    {
        StringComparer stringComparer = this._args.CaseSensitive 
            ? StringComparer.InvariantCulture 
            : StringComparer.InvariantCultureIgnoreCase;

        foreach (string globPath in this._args.IncludeGlobPaths) 
            this.VerifyPathIsNotRooted(globPath);

        foreach (string globPath in this._args.ExcludeGlobPaths)
            this.VerifyPathIsNotRooted(globPath);

        string normalizedBasePath = this.NormalizePathSeparators(basePath);

        DirectoryInfo baseDir = new DirectoryInfo(normalizedBasePath);
        DirectoryInfo rootDir = this.DetermineRootPathFromBasePathAndIncludes(baseDir, stringComparer);

        ImmutableList<Glob> includeGlobs = this._args.IncludeGlobPaths
            .Select(g => BuildRelativeFileName(Path.Combine(baseDir.FullName, g), rootDir, baseDir))
            .Select(g => Glob.Parse(g))
            .ToImmutableList();

        ImmutableList<Glob> excludeGlobs = this._args.ExcludeGlobPaths
            .Select(g => BuildRelativeFileName(Path.Combine(baseDir.FullName, g), rootDir, baseDir))
            .Select(g => Glob.Parse(g))
            .ToImmutableList();

        foreach (FileInfo fileInfo in this.EnumerateAllFiles(rootDir, excludeGlobs, rootDir, baseDir, ignoredFileAccessExceptions))
        {
            string fileName = BuildRelativeFileName(fileInfo.FullName, rootDir, baseDir);

            if (includeGlobs.Any(glob => glob.IsMatch(fileName)) && !excludeGlobs.Any(glob => glob.IsMatch(fileName)))
                yield return this.ToForwardSlashPathSeparators(fileName);
        }
    }

    private IEnumerable<FileInfo> EnumerateAllFiles(DirectoryInfo dirInfo, ImmutableList<Glob> excludeGlobs, 
        DirectoryInfo rootDir, DirectoryInfo baseDir, List<Exception> ignoredFileAccessExceptions)
    {
        ImmutableList<FileInfo> files = this.DoFileSysOp(
            () => dirInfo.EnumerateFiles().ToImmutableList(),
            () => ImmutableList<FileInfo>.Empty,
            ignoredFileAccessExceptions);

        foreach (FileInfo fileInfo in files)
            yield return fileInfo;

        ImmutableList<DirectoryInfo> subDirInfos = this.DoFileSysOp(
            () => dirInfo.EnumerateDirectories().ToImmutableList(),
            () => ImmutableList<DirectoryInfo>.Empty,
            ignoredFileAccessExceptions);

        foreach (DirectoryInfo subDirInfo in subDirInfos)
        {
            string fullFolderNamePath = BuildRelativeFileName(subDirInfo.FullName, rootDir, baseDir);

            if (excludeGlobs.Any(glob => glob.IsMatch(fullFolderNamePath)))
                continue;

            foreach (FileInfo fileInfo in this.EnumerateAllFiles(subDirInfo, excludeGlobs, rootDir, baseDir, ignoredFileAccessExceptions))
                yield return fileInfo;
        }
    }

    private static string BuildRelativeFileName(string fullFileNamePath, DirectoryInfo rootDir, DirectoryInfo baseDir)
    {
        string relativeFileName = Path.GetRelativePath(rootDir.FullName, fullFileNamePath);
        string prefix = Path.GetRelativePath(baseDir.FullName, rootDir.FullName);
        if (prefix != ".")
            relativeFileName = Path.Combine(prefix, relativeFileName);
        return relativeFileName;
    }

    private T DoFileSysOp<T>(Func<T> operation, Func<T> getDefault, List<Exception> ignoredFileAccessExceptions)
    {
        try
        {
            return operation();
        }
        catch (SecurityException se) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(se);
            return getDefault();
        }
        catch (UnauthorizedAccessException uae) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(uae);
            return getDefault();
        }
        catch (DirectoryNotFoundException dnfe) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(dnfe);
            return getDefault();
        }
        catch (IOException ioe) when (!this._args.AbortOnFileSystemAccessExceptions)
        {
            ignoredFileAccessExceptions.Add(ioe);
            return getDefault();
        }
    }

    private void VerifyPathIsNotRooted(string path)
    {
        if (Path.IsPathRooted(path))
            throw new ArgumentException($"Glob path cannot be rooted! Path: \"{path}\"");
    }

    private (string RelativePrefix, string RelativePath) SplitPathByParentPrefix(string path)
    {
        var segments = this.SplitPathIntoSegments(path);
        return SplitPathByParentPrefix(path, segments);
    }

    private static (string RelativePrefix, string RelativePath) SplitPathByParentPrefix(string[] segments)
    {
        string path = Path.Combine(segments);
        return SplitPathByParentPrefix(path, segments);
    }

    private static (string RelativePrefix, string RelativePath) SplitPathByParentPrefix(string path, string[] segments)
    {
        int index = GetIndexOfLastSpecialFolder(segments);
        if (index < 0)
            return (string.Empty, path);

        return (Path.Combine(segments.Take(index + 1).ToArray()),
            Path.Combine(segments.Skip(index + 1).ToArray()));

        int GetIndexOfLastSpecialFolder(string[] strings)
        {
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                string segment = segments[i];
                if (segment == ".." || segment == ".")
                    return i;
            }

            return -1;
        }
    }

    private string[] SplitPathIntoSegments(string path)
    {
        string normalizedPath = this.NormalizePathSeparators(path);
        string[] segments = normalizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return segments;
    }

    private DirectoryInfo DetermineRootPathFromBasePathAndIncludes(DirectoryInfo basePath, StringComparer comparer)
    {
        var rootDirectoryList = this._args.IncludeGlobPaths
            .Select(p => new { Path = p, RelativePathPrefix = Path.Combine(this.SplitPathByParentPrefix(p).RelativePrefix) })
            .Where(x => x.RelativePathPrefix.Length > 0)
            .Select(x => new DirectoryInfo(Path.GetFullPath(Path.Combine(basePath.FullName, x.RelativePathPrefix))))
            .Prepend(basePath)
            .ToImmutableList();

        var rootPathSegmentsList = rootDirectoryList
            .Select(di => this.SplitPathIntoSegments(di.FullName))
            .ToImmutableList();

        int minTotalSegments = rootPathSegmentsList
            .Select(x => x.Length)
            .Min();

        var matchingPathSegments = Enumerable.Range(1, minTotalSegments)
            .Reverse()
            .Select(x => rootPathSegmentsList.First().Take(x).ToImmutableList())
            .Where(pathSegsToMatch => rootPathSegmentsList.All(pathSegs => pathSegs.StartsWith(pathSegsToMatch, comparer)));

        return new DirectoryInfo(Path.Combine(matchingPathSegments.First().ToArray()));
    }

    private string NormalizePathSeparators(string path)
    {
        return this.DoPathSeparatorReplace
            ? path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            : path;
    }

    private string ToForwardSlashPathSeparators(string path)
    {
        return this.DoPathSeparatorReplace
            ? path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : path;
    }
}
using System.Collections.Immutable;
using System.IO;
using System.Security;
using DotNet.Globbing;

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
        foreach (string globPath in this._args.IncludeGlobPaths) 
            this.VerifyPathIsNotRooted(globPath);

        foreach (string globPath in this._args.ExcludeGlobPaths)
            this.VerifyPathIsNotRooted(globPath);

        ImmutableList<Glob> includeGlobs = this._args.IncludeGlobPaths.Select(g => Glob.Parse(g)).ToImmutableList();
        ImmutableList<Glob> excludeGlobs = this._args.ExcludeGlobPaths.Select(g => Glob.Parse(g)).ToImmutableList();

        string normalizedBasePath = this.NormalizePathSeparators(basePath);

        var baseDir = new DirectoryInfo(normalizedBasePath);
        var rootDir = baseDir;

        string? leadingDotPathFromIncludes = includeGlobs
            .Select(glob => this.GetLeadingDotDotPaths(glob.ToString()))
            .Distinct()
            .SingleOrDefault();

        StringComparer stringComparer = this._args.CaseSensitive ? StringComparer.InvariantCulture : StringComparer.InvariantCultureIgnoreCase;
        string? adjustedRootDir = leadingDotPathFromIncludes == null
            ? null
            : Path.GetFullPath(Path.Combine(baseDir.FullName, leadingDotPathFromIncludes));

        if (adjustedRootDir != null)
            rootDir = new DirectoryInfo(adjustedRootDir);

        ImmutableList<Glob> adjustedExcludePaths = leadingDotPathFromIncludes == null
            ? excludeGlobs
            : this.LeftTrimPathsIfAllMatchPathValueToLTrim(leadingDotPathFromIncludes, this._args.ExcludeGlobPaths.ToImmutableList(), stringComparer)
                .Select(g => Glob.Parse(g))
                .ToImmutableList();
        
        foreach (FileInfo fileInfo in this.EnumerateAllFiles(rootDir, adjustedExcludePaths, ignoredFileAccessExceptions))
        {
            string fileName = Path.GetRelativePath(rootDir.FullName, fileInfo.FullName);
            if (leadingDotPathFromIncludes != null)
                fileName = Path.Combine(leadingDotPathFromIncludes, fileName);

            if (includeGlobs.Any(glob => glob.IsMatch(fileName)) && !excludeGlobs.Any(glob => glob.IsMatch(fileName)))
                yield return this.ToForwardSlashPathSeparators(fileName);
        }
    }
    
    private IEnumerable<FileInfo> EnumerateAllFiles(DirectoryInfo dirInfo, ImmutableList<Glob> excludeGlobs, List<Exception> ignoredFileAccessExceptions)
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
            if (excludeGlobs.Any(glob => glob.IsMatch(subDirInfo.FullName)))
                continue;

            foreach (FileInfo fileInfo in this.EnumerateAllFiles(subDirInfo, excludeGlobs, ignoredFileAccessExceptions))
                yield return fileInfo;
        }
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

    private string GetPathTrailingDotDotPaths(string path)
    {
        string normalizedPath = this.NormalizePathSeparators(path);
        string[] segments = normalizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine(segments.SkipWhile(s => s == ".." || s == ".").ToArray());
    }

    private ImmutableList<string> LeftTrimPathsIfAllMatchPathValueToLTrim(string pathValueToLTrim, ImmutableList<string> pathsToRemoveFrom, StringComparer stringComparer)
    {
        pathValueToLTrim = this.NormalizePathSeparators(pathValueToLTrim);
        string[] segmentsToRemove = pathValueToLTrim.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        if (!segmentsToRemove.Any())
            return pathsToRemoveFrom;

        ImmutableList<string[]> pathsToRemoveInSegments = pathsToRemoveFrom
            .Select(p =>
            {
                string normalizedPath = this.NormalizePathSeparators(p);
                return normalizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            }).ToImmutableList();

        var adjustedPaths = new List<string>();

        foreach (string[] pathSegmentsToLTrimFrom in pathsToRemoveInSegments)
        {
            if (pathSegmentsToLTrimFrom.Length <= segmentsToRemove.Length)
                return pathsToRemoveFrom;

            for (int i = 0; i < segmentsToRemove.Length; i++)
                if (pathSegmentsToLTrimFrom[i] != segmentsToRemove[i])
                    return pathsToRemoveFrom;

            adjustedPaths.Add(Path.Combine(pathSegmentsToLTrimFrom.Skip(segmentsToRemove.Length).ToArray()));
        }

        return adjustedPaths.ToImmutableList();
    }

    private string GetLeadingDotDotPaths(string path)
    {
        string normalizedPath = this.NormalizePathSeparators(path);
        string[] segments = normalizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine(segments.TakeWhile(s => s == ".." || s == ".").ToArray());
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

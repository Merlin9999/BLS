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
            .Select(path => string.Join("/", SplitPathAndNormalizeRelativeSegments(path).Where(s => s != ".")))
            .ToImmutableList();
        this.Args.ExcludeGlobPaths = this.Args.ExcludeGlobPaths
            .Select(path => string.Join("/", SplitPathAndNormalizeRelativeSegments(path).Where(s => s != ".")))
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
        
        ImmutableList<IncludeGlobber> includeInfos = this.Args.IncludeGlobPaths
            .Select(gp => BuildRelativeFileName(Path.Combine(baseDir.FullName, gp), commonRootDir, baseDir))
            .Select(gp => new IncludeGlobber(gp))
            .ToImmutableList();
        
        ImmutableList<Glob> excludeGlobs = this.Args.ExcludeGlobPaths
            .Select(g => BuildRelativeFileName(Path.Combine(baseDir.FullName, g), commonRootDir, baseDir))
            .Select(g => Glob.Parse(g))
            .ToImmutableList();

        foreach (FileInfo fileInfo in this.EnumerateAllFiles(1, commonRootDir, includeInfos, excludeGlobs, commonRootDir, baseDir, ignoredExceptions))
        {
            string fileName = BuildRelativeFileName(fileInfo.FullName, commonRootDir, baseDir);

            if (includeInfos.Any(glob => glob.IsMatch(fileName)) && !excludeGlobs.Any(glob => glob.IsMatch(fileName)))
                yield return fileName;
        }
    }

    private IEnumerable<FileInfo> EnumerateAllFiles(int level, DirectoryInfo dirInfo, ImmutableList<IncludeGlobber> includeInfos, 
        ImmutableList<Glob> excludeGlobs, DirectoryInfo commonRootDir, DirectoryInfo baseDir, IgnoredExceptionSet ignoredExceptions)
    {
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

            if (includeInfos.Any(include => include.IsFolderMatch(fullFolderNamePath) || include.IsRecursFolder(fullFolderNamePath)))
            {
                // Not sure if it is faster to potentially try to match against extra Globs, or build a filtered Glob list for each subdirectory.
                ImmutableList<IncludeGlobber> subDirIncludeInfos = includeInfos;
                    //.Where(include => include.IsFolderMatch(fullFolderNamePath))
                    //.ToImmutableList();

                foreach (FileInfo fileInfo in this.EnumerateAllFiles(level + 1, subDirInfo, subDirIncludeInfos,
                             excludeGlobs, commonRootDir, baseDir, ignoredExceptions))
                    yield return fileInfo;
            }
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

    private DirectoryInfo DetermineCommonRootPathFromBasePathAndIncludes(DirectoryInfo basePath, StringComparer comparer)
    {
        var rootDirectoryList = this.Args.IncludeGlobPaths
            .Select(p => new { Path = p, RelativePathPrefix = GetParentRelativePrefix(p) })
            .Where(x => x.RelativePathPrefix.Length > 0)
            .Select(x => new DirectoryInfo(Path.GetFullPath(Path.Combine(basePath.FullName, x.RelativePathPrefix))))
            .Prepend(basePath)
            .ToImmutableList();

        var rootPathSegmentsList = rootDirectoryList
            .Select(di => SplitPathIntoSegments(di.FullName))
            .ToImmutableList();

        int minTotalSegments = rootPathSegmentsList
            .Select(x => x.Count)
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

    protected static string GetParentRelativePrefix(string path)
    {
        var segments = SplitPathIntoSegments(path);
        int index = GetIndexOfLastSpecialFolder();
        if (index < 0)
            return string.Empty;

        return Path.Combine(segments.Take(index + 1).ToArray());

        int GetIndexOfLastSpecialFolder()
        {
            for (int i = segments.Count - 1; i >= 0; i--)
            {
                string segment = segments[i];
                if (segment == "..")
                    return i;
            }

            return -1;
        }
    }

    public class IncludeGlobber
    {
        private readonly ImprovedGlobberFolderInfo _folderInfo;
        private readonly Glob _glob;
        private ImmutableDictionary<int, Glob> _interimGlobCache = ImmutableDictionary<int, Glob>.Empty;

        public IncludeGlobber(string globPath)
        {
            this._folderInfo = PreprocessGlobPathAndCalculateLevels(globPath);
            this._glob = Glob.Parse(this._folderInfo.GlobPath);
        }

        public bool IsFolderMatch(string path)
        {
            return this.IsInterimMatch(path);
        }

        public bool IsRecursFolder(string path)
        {
            if (this._folderInfo.WildcardRecursFolderIdx == null)
                return false;

            ImmutableList<string> pathSegments = SplitPathIntoSegments(path);
            return this._folderInfo.WildcardRecursFolderIdx + 1 >= pathSegments.Count
                && this.IsInterimMatch(path, pathSegments);
        }

        public bool IsMatch(string path)
        {
            return this._glob.IsMatch(path);
        }

        private bool IsInterimMatch(string path)
        {
            ImmutableList<string> pathSegments = SplitPathIntoSegments(path);
            return this.IsInterimMatch(path, pathSegments);
        }

        private bool IsInterimMatch(string path, ImmutableList<string> pathSegments)
        {
            int maxGlobPathSegmentCountToMatch = this._folderInfo.WildcardRecursFolderIdx == null
                ? this._folderInfo.PathSegments.Count
                : this._folderInfo.WildcardRecursFolderIdx.Value + 1;

            int inPathSegmentCountToMatch = int.Min(maxGlobPathSegmentCountToMatch, pathSegments.Count);

            if (inPathSegmentCountToMatch >= this._folderInfo.PathSegments.Count)
                return this._glob.IsMatch(path);

            if (!this._interimGlobCache.TryGetValue(inPathSegmentCountToMatch, out Glob? interimGlob))
            {
                interimGlob = Glob.Parse(string.Join('/', this._folderInfo.PathSegments.Take(inPathSegmentCountToMatch)));
                this._interimGlobCache = this._interimGlobCache.Add(inPathSegmentCountToMatch, interimGlob);
            }

            return interimGlob.IsMatch(path);
        }

        private static ImprovedGlobberFolderInfo PreprocessGlobPathAndCalculateLevels(string globPath)
        {
            ImmutableList<string> pathSegments = SplitPathIntoSegments(globPath);
            //int dblPeriodCount = pathSegments.Count(s => s == "..");
            int indexOfWildcardRecurs = pathSegments.IndexOf("**");
            int wildcardRecursFolderIdx = indexOfWildcardRecurs < 0 ? -1 : indexOfWildcardRecurs;
            int maxFolderIdx = indexOfWildcardRecurs < 0 ? pathSegments.Count - 1 : int.MaxValue;
            int preRecursMaxFolderIdx = indexOfWildcardRecurs < 0 ? maxFolderIdx : wildcardRecursFolderIdx - 1;

            return new ImprovedGlobberFolderInfo()
            {
                PreRecursMaxFolderIdx = preRecursMaxFolderIdx,
                WildcardRecursFolderIdx = wildcardRecursFolderIdx < 0 ? null : wildcardRecursFolderIdx,
                MaxFolderIdx = maxFolderIdx,
                PathSegments = pathSegments,
                GlobPath = string.Join('/', pathSegments),
            };
        }
    }

    public record ImprovedGlobberFolderInfo
    {
        public required int PreRecursMaxFolderIdx { get; init; }
        public required int? WildcardRecursFolderIdx { get; init; }
        public required int MaxFolderIdx { get; init; }
        public required ImmutableList<string> PathSegments { get; init; }
        public required string GlobPath { get; init; }
    }
}
using System.Collections.Immutable;
using System.Security;
using System.Text.RegularExpressions;
using DotNet.Globbing;

namespace BLS.Globbers;

public abstract partial class AbstractImprovedGlobber<TFileSysInfo> : AbstractGlobber
    where TFileSysInfo : FileSystemInfo
{
    // Derived from: https://stackoverflow.com/a/54300816/677612
    // Check out: https://stackoverflow.com/a/34580159/677612

    [GeneratedRegex("^[a-zA-Z][:]$")]
    private static partial Regex GetDriveOnlyRegex();

    protected AbstractImprovedGlobber(IGlobberArgs args) 
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

        DirectoryInfo baseDir = new(normalizedBasePath);
        DirectoryInfo commonRootDir = this.DetermineCommonRootPathFromBasePathAndIncludes(baseDir, stringComparer);

        ImmutableList<IncludeGlobber> includeInfos = this.Args.IncludeGlobPaths
            .Select(gp => BuildRelativeName(Path.Combine(baseDir.FullName, gp), commonRootDir, baseDir))
            .Select(gp => new IncludeGlobber(gp, this.Args.CaseSensitive))
            .ToImmutableList();

        ImmutableList<Glob> excludeGlobs = this.Args.ExcludeGlobPaths
            .Select(g => BuildRelativeName(Path.Combine(baseDir.FullName, g), commonRootDir, baseDir))
            .Select(g => Glob.Parse(g, new GlobOptions() { Evaluation = new EvaluationOptions() { CaseInsensitive = !this.Args.CaseSensitive } }))
            .ToImmutableList();

        foreach (TFileSysInfo fileSysInfo in this.EnumerateAllEntries(commonRootDir, includeInfos, excludeGlobs, commonRootDir, baseDir, ignoredExceptions))
        {
            string fileName = BuildRelativeName(fileSysInfo.FullName, commonRootDir, baseDir);

            if (includeInfos.Any(glob => glob.IsMatch(fileName)) && !excludeGlobs.Any(glob => glob.IsMatch(fileName)))
                yield return fileName;
        }
    }

    protected abstract IEnumerable<TFileSysInfo> EnumerateAllEntries(DirectoryInfo dirInfo, ImmutableList<IncludeGlobber> includeInfos, 
        ImmutableList<Glob> excludeGlobs, DirectoryInfo commonRootDir, DirectoryInfo baseDir, IgnoredExceptionSet ignoredExceptions);

    protected T DoFileSysOp<T>(Func<T> operation, Func<T> getDefault, IgnoredExceptionSet ignoredExceptions)
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

    public void ValidateGlobPath(string path, bool isIncludePath)
    {
        if (Path.IsPathRooted(path))
            throw new ArgumentException("A single rooted glob path is supported if no base paths are supplied!");

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
        if (GetDriveOnlyRegex().IsMatch(commonRootPath))
            commonRootPath += Path.DirectorySeparatorChar;
        else if (Path.IsPathRooted(commonRootPath) && 
                 !(commonRootPath.EndsWith(Path.DirectorySeparatorChar) || commonRootPath.EndsWith(Path.AltDirectorySeparatorChar)))
            commonRootPath += Path.DirectorySeparatorChar;

        return new DirectoryInfo(commonRootPath);
    }

    private static string GetParentRelativePrefix(string path)
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

    protected class IncludeGlobber
    {
        private readonly GlobberParentFolderInfo _ancestorFolderInfo;
        private readonly Glob _glob;
        private ImmutableDictionary<int, Glob> _ancestorGlobCache = ImmutableDictionary<int, Glob>.Empty;
        private readonly bool _caseSensitive;

        public IncludeGlobber(string globPath, bool caseSensitive)
        {
            this._caseSensitive = caseSensitive;
            this._ancestorFolderInfo = PreprocessGlobPathAndCalculateLevels(globPath);
            this._glob = Glob.Parse(this._ancestorFolderInfo.GlobPath, 
                new GlobOptions() { Evaluation = new EvaluationOptions() { CaseInsensitive = !this._caseSensitive } });
        }

        public bool IsFolderMatch(string path)
        {
            return this.IsAncestorMatch(path);
        }

        public bool IsRecursFolder(string path)
        {
            if (this._ancestorFolderInfo.WildcardRecursFolderIdx == null)
                return false;

            ImmutableList<string> pathSegments = SplitPathIntoSegments(path);
            return this._ancestorFolderInfo.WildcardRecursFolderIdx + 1 >= pathSegments.Count
                && this.IsAncestorMatch(path, pathSegments);
        }

        public bool IsMatch(string path)
        {
            return this._glob.IsMatch(path);
        }

        private bool IsAncestorMatch(string path)
        {
            ImmutableList<string> pathSegments = SplitPathIntoSegments(path);
            return this.IsAncestorMatch(path, pathSegments);
        }

        private bool IsAncestorMatch(string path, ImmutableList<string> pathSegments)
        {
            int maxGlobPathSegmentCountToMatch = this._ancestorFolderInfo.WildcardRecursFolderIdx == null
                ? this._ancestorFolderInfo.PathSegments.Count
                : this._ancestorFolderInfo.WildcardRecursFolderIdx.Value + 1;

            int inPathSegmentCountToMatch = int.Min(maxGlobPathSegmentCountToMatch, pathSegments.Count);

            if (inPathSegmentCountToMatch >= this._ancestorFolderInfo.PathSegments.Count)
                return this._glob.IsMatch(path);

            if (!this._ancestorGlobCache.TryGetValue(inPathSegmentCountToMatch, out Glob? ancestorGlob))
            {
                ancestorGlob = Glob.Parse(string.Join('/', this._ancestorFolderInfo.PathSegments.Take(inPathSegmentCountToMatch)),
                    new GlobOptions() { Evaluation = new EvaluationOptions() { CaseInsensitive = !this._caseSensitive } });
                this._ancestorGlobCache = this._ancestorGlobCache.Add(inPathSegmentCountToMatch, ancestorGlob);
            }

            return ancestorGlob.IsMatch(path);
        }

        private static GlobberParentFolderInfo PreprocessGlobPathAndCalculateLevels(string globPath)
        {
            ImmutableList<string> pathSegments = SplitPathIntoSegments(globPath);
            //int dblPeriodCount = pathSegments.Count(s => s == "..");
            int indexOfWildcardRecurs = pathSegments.IndexOf("**");
            int wildcardRecursFolderIdx = indexOfWildcardRecurs < 0 ? -1 : indexOfWildcardRecurs;
            int maxFolderIdx = indexOfWildcardRecurs < 0 ? pathSegments.Count - 1 : int.MaxValue;
            int preRecursMaxFolderIdx = indexOfWildcardRecurs < 0 ? maxFolderIdx : wildcardRecursFolderIdx - 1;

            return new GlobberParentFolderInfo()
            {
                PreRecursMaxFolderIdx = preRecursMaxFolderIdx,
                WildcardRecursFolderIdx = wildcardRecursFolderIdx < 0 ? null : wildcardRecursFolderIdx,
                MaxFolderIdx = maxFolderIdx,
                PathSegments = pathSegments,
                GlobPath = string.Join('/', pathSegments),
            };
        }
    }

    public record GlobberParentFolderInfo
    {
        public required int PreRecursMaxFolderIdx { get; init; }
        public required int? WildcardRecursFolderIdx { get; init; }
        public required int MaxFolderIdx { get; init; }
        public required ImmutableList<string> PathSegments { get; init; }
        public required string GlobPath { get; init; }
    }
}
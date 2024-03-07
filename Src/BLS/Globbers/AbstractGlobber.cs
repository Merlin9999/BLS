using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using BLS.Extensions;

namespace BLS.Globbers;

public abstract partial class AbstractGlobber
{
    [GeneratedRegex(@"^[\\/]{2}[^\\/]+")]
    private static partial Regex GetNetShareRegex();

    private static bool? _areBackSlashPathSegmentSeparatorsStandard;
    private static bool? _areForwardSlashPathSegmentSeparatorsStandard;

    protected static bool AreBackSlashPathSegmentSeparatorsStandard => _areBackSlashPathSegmentSeparatorsStandard ??= Path.DirectorySeparatorChar == '\\' && Path.AltDirectorySeparatorChar == '/';
    protected static bool AreForwardSlashPathSegmentSeparatorsStandard => _areForwardSlashPathSegmentSeparatorsStandard ??= Path.DirectorySeparatorChar == '/' && Path.AltDirectorySeparatorChar == '\\';
    
    public static string ToStandardPathSegmentSeparators(string path) => path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    public static string ToAlternatePathSegmentSeparators(string path) => path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    public static string NormalizePathSegmentSeparators(string path) => ToStandardPathSegmentSeparators(path);
    public static string ToForwardSlashPathSegmentSeparators(string path) => AreForwardSlashPathSegmentSeparatorsStandard
        ? ToStandardPathSegmentSeparators(path)
        : AreBackSlashPathSegmentSeparatorsStandard
            ? ToAlternatePathSegmentSeparators(path)
            : path;
    public static string TrimTrailingPathSegmentSeparators(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static string BuildRelativeName(string fullEntryNamePath, DirectoryInfo commonRootDir, DirectoryInfo baseDir)
    {
        string relativeEntryName = Path.GetRelativePath(commonRootDir.FullName, fullEntryNamePath);
        string prefix = Path.GetRelativePath(baseDir.FullName, commonRootDir.FullName);
        if (prefix != ".")
            relativeEntryName = Path.Combine(prefix, relativeEntryName);
        return relativeEntryName;
    }

    public static ImmutableList<string> SplitPathIntoSegments(string path)
    {
        Regex netShareRegex = GetNetShareRegex();
        bool isNetShare = netShareRegex.IsMatch(path);
        string netSharePrefix = isNetShare ? path.Substring(0, 2) : string.Empty;
        path = isNetShare ? path.Substring(2) : path;

        ImmutableList<string> segments = path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToImmutableList();

        if (isNetShare)
            segments = segments.SetItem(0, netSharePrefix + segments[0]);

        return segments;
    }

    public static ImmutableList<string> SplitPathAndNormalizeRelativeSegments(string path)
    {
        ImmutableList<string> pathSegments = SplitPathIntoSegments(path)
            .Where(s => s != ".")
            .ToImmutableList();
        for (var index = pathSegments.Count - 1; index >= 1; index--)
        {
            string segment = pathSegments[index];
            if (segment != "..")
                continue;

            string priorSegment = pathSegments[index - 1];
            if (priorSegment != "..")
                pathSegments = pathSegments.RemoveAt(--index).RemoveAt(index);
        }

        return pathSegments;
    }
}

public abstract class AbstractGlobber<TFolderEntryPathInfo, TFileSysInfo>(IGlobberArgs args) : AbstractGlobber, IGlobber<TFolderEntryPathInfo, TFileSysInfo>
    where TFileSysInfo : FileSystemInfo
    where TFolderEntryPathInfo : IFolderEntryPathInfo<TFileSysInfo>
{
    protected readonly IGlobberArgs Args = args;
    private ImmutableList<TFolderEntryPathInfo> _entryCache = [];

    private bool? _canOutputImmediately;
    private bool? _useFullyQualifiedOutputPaths;
    private string? _currentDirectory;

    protected string CurrentWorkingDirectory => this._currentDirectory ??= Directory.GetCurrentDirectory();
    protected bool UseFullyQualifiedOutputPaths => this._useFullyQualifiedOutputPaths ??= this.Args.UseFullyQualifiedPaths || this.Args.BasePaths.Count() > 1;
    private bool CanOutputImmediately => this._canOutputImmediately ??= this.Args.Sort == null && (this.Args.AllowDuplicatesWhenMultipleBasePaths || this.Args.BasePaths.Count() <= 1);

    public IEnumerable<Exception> IgnoredAccessExceptions => this.IgnoredExceptions.Exceptions;
    protected IgnoredExceptionSet IgnoredExceptions { get; private set; } = new IgnoredExceptionSet();

    public IEnumerable<TFolderEntryPathInfo> Execute()
    {
        List<string> basePaths = this.Args.BasePaths.Select(NormalizePathSegmentSeparators).ToList();

        this.IgnoredExceptions = new IgnoredExceptionSet();

        if (basePaths.Count < 2)
        {
            this.FixUpIncludesAndBasePathForSingleRootedIncludeGlob(ref basePaths);

            string basePath = basePaths.Count < 1 ? "." + Path.DirectorySeparatorChar : basePaths[0];
            IEnumerable<TFolderEntryPathInfo> entries = this.FindMatches(basePath, this.IgnoredExceptions);
            entries = this.OutputOrCacheEntries(basePath, entries);
            foreach (TFolderEntryPathInfo entry in entries)
                yield return entry;
        }
        else
        {
            foreach (string basePath in basePaths)
            {
                IEnumerable<TFolderEntryPathInfo> entries = this.FindMatches(basePath, this.IgnoredExceptions);
                entries =  this.OutputOrCacheEntries(basePath, entries);
                foreach (TFolderEntryPathInfo entry in entries)
                    yield return entry;
            }
        }

        // If multiple base paths or sort was explicitly selected, entry names were cached 
        // and need to be output now.
        if (!this.CanOutputImmediately)
        {
            List<TFolderEntryPathInfo> entries = this.GetCachedEntries(basePaths);
            foreach (TFolderEntryPathInfo entry in entries)
                yield return entry;
        }
    }


    private void FixUpIncludesAndBasePathForSingleRootedIncludeGlob(ref List<string> basePaths)
    {
        if (basePaths.Count > 0)
            return;

        if (this.Args.IncludeGlobPaths.Count() != 1)
            return;

        string singleIncludeGlob = this.Args.IncludeGlobPaths.First();

        if (!Path.IsPathRooted(singleIncludeGlob))
            return;
        
        string rootedBasePath = Path.GetPathRoot(singleIncludeGlob) 
            ?? throw new NotSupportedException($"The path \"{singleIncludeGlob}\" was expected to be rooted!");
        string relativeGlobPath = Path.GetRelativePath(rootedBasePath, singleIncludeGlob);

        basePaths.Add(rootedBasePath);
        this.Args.IncludeGlobPaths = ImmutableList<string>.Empty.Add(relativeGlobPath);
        this.Args.UseFullyQualifiedPaths = true;
    }

    protected abstract IEnumerable<TFolderEntryPathInfo> FindMatches(string basePath, IgnoredExceptionSet ignoredExceptions);

    protected abstract TFolderEntryPathInfo CreateOutputEntry(string basePath, string entryPath, TFileSysInfo? fileSysInfo);

    private IEnumerable<TFolderEntryPathInfo> OutputOrCacheEntries(string basePath, IEnumerable<TFolderEntryPathInfo> entries)
    {
        if (this.CanOutputImmediately)
        {
            foreach (TFolderEntryPathInfo entry in entries)
                yield return entry;
        }
        else
        {
            this._entryCache = this._entryCache.AddRange(entries);
        }
    }

    private List<TFolderEntryPathInfo> GetCachedEntries(List<string> basePaths)
    {
        var folderEntryCompareFunc = this.BuildFolderEntryCompareFunc();
        if (this.Args.SortDescending)
            folderEntryCompareFunc = DescendingCompareFunc(folderEntryCompareFunc);

        var entryComparer = folderEntryCompareFunc.AsComparer();
        var entryEqualityComparer = folderEntryCompareFunc.AsEqualityComparer();

        // If multiple base paths, we need to remove duplicates.
        List<TFolderEntryPathInfo> entryPaths = basePaths.Count > 1
            ? this._entryCache
                .Distinct(entryEqualityComparer)
                .ToList()
            : this._entryCache.ToList();

        entryPaths.Sort(entryComparer);

        return entryPaths;

        Func<TFolderEntryPathInfo, TFolderEntryPathInfo, int> DescendingCompareFunc(Func<TFolderEntryPathInfo, TFolderEntryPathInfo, int> ascendingFunc)
        {
            return (x, y) => -ascendingFunc(x, y);
        }
    }

    private Func<TFolderEntryPathInfo, TFolderEntryPathInfo, int> BuildFolderEntryCompareFunc()
    {
        ESortType sortType = this.Args.Sort ?? ESortType.Name;

        switch (sortType)
        {
            case ESortType.Name:
                return EntryNameCompareFunc();

            case ESortType.Extension:
                return EntryExtCompareFunc();

            case ESortType.Date:
                return EntryDateCompareFunc();

            case ESortType.Size:
                return FileSizeCompareFunc();
        }

        throw new NotSupportedException($"Sort type of {nameof(ESortType)}.{sortType} is not supported!");

        Func<TFolderEntryPathInfo, TFolderEntryPathInfo, int> EntryNameCompareFunc()
        {
            StringComparer stringComparer = this.Args.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            Func<TFolderEntryPathInfo, TFolderEntryPathInfo, int> folderEntryCompareFunc = (x, y) => stringComparer.Compare(x.EntryPath, y.EntryPath);
            return folderEntryCompareFunc;
        }

        Func<TFolderEntryPathInfo, TFolderEntryPathInfo, int> EntryExtCompareFunc()
        {
            StringComparer stringComparer = this.Args.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            return CompareFunc;

            int CompareFunc(TFolderEntryPathInfo x, TFolderEntryPathInfo y)
            {
                int extCompare = stringComparer.Compare(Path.GetExtension(x.EntryPath), Path.GetExtension(y.EntryPath));
                return extCompare != 0 ? extCompare : stringComparer.Compare(x.EntryPath, y.EntryPath);
            }
        }

        Func<TFolderEntryPathInfo, TFolderEntryPathInfo, int> EntryDateCompareFunc()
        {
            return CompareFunc;

            int CompareFunc(TFolderEntryPathInfo x, TFolderEntryPathInfo y) => x.EntryInfo.LastWriteTime.CompareTo(y.EntryInfo.LastWriteTime);
        }

        Func<TFolderEntryPathInfo, TFolderEntryPathInfo, int> FileSizeCompareFunc()
        {
            return CompareFunc;

            int CompareFunc(TFolderEntryPathInfo x, TFolderEntryPathInfo y)
            {
                if (x is FilePathInfo xAsFile && y is FilePathInfo yAsFile)
                {
                    int cmp = xAsFile.EntryInfo.Length.CompareTo(yAsFile.EntryInfo.Length);
                    if (cmp != 0)
                        return cmp;
                    return EntryNameCompareFunc()(x, y);
                }

                throw new NotSupportedException($"Sort type of {nameof(ESortType)}.{sortType} is not supported for {typeof(TFolderEntryPathInfo).Name} entries!");
            }
        }
    }

    protected class IgnoredExceptionSet
    {
        private ImmutableHashSet<string> MessageSet { get; set; } = [];
        public ImmutableList<Exception> Exceptions { get; private set; } = [];

        public void Add(Exception ignoredException)
        {
            if (!this.MessageSet.Contains(ignoredException.Message))
            {
                this.Exceptions = this.Exceptions.Add(ignoredException);
                this.MessageSet = this.MessageSet.Add(ignoredException.Message);
            }
        }
    }
}

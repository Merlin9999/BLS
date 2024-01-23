using System.Collections.Immutable;

namespace BLS.Globbers;

public abstract class AbstractGlobber(IGlobberArgs args) : IGlobber
{
    protected readonly IGlobberArgs Args = args;
    private ImmutableList<string> _entryCache = [];

    private bool? _canOutputImmediately;
    private bool? _useFullyQualifiedOutputPaths;
    private string? _currentDirectory;
    private static bool? _normalizeToBackSlashPathSeparators;
    private static bool? _normalizeToForwardSlashPathSeparators;

    private string CurrentWorkingDirectory => this._currentDirectory ??= Directory.GetCurrentDirectory();
    private bool UseFullyQualifiedOutputPaths => this._useFullyQualifiedOutputPaths ??= this.Args.UseFullyQualifiedPaths || this.Args.BasePaths.Count() > 1;
    private bool CanOutputImmediately => this._canOutputImmediately ??= !this.Args.Sort && (this.Args.AllowDuplicatesWhenMultipleBasePaths || this.Args.BasePaths.Count() <= 1);
    private static bool NormalizeToBackSlashPathSeparators => _normalizeToBackSlashPathSeparators ??= Path.DirectorySeparatorChar == '\\' && Path.AltDirectorySeparatorChar == '/';
    private static bool NormalizeToForwardSlashPathSeparators => _normalizeToForwardSlashPathSeparators ??= Path.DirectorySeparatorChar == '/' && Path.AltDirectorySeparatorChar == '\\';

    public IEnumerable<Exception> IgnoredAccessExceptions => this.IgnoredExceptions.Exceptions;
    protected IgnoredExceptionSet IgnoredExceptions { get; private set; } = new IgnoredExceptionSet();

    public IEnumerable<string> Execute()
    {
        List<string> basePaths = this.Args.BasePaths.ToList();

        this.IgnoredExceptions = new IgnoredExceptionSet();

        if (basePaths.Count < 2)
        {
            this.FixUpIncludesAndBasePathForSingleRootedIncludeGlob(ref basePaths);

            string basePath = basePaths.Count < 1 ? "./" : basePaths[0];
            IEnumerable<string> entries = this.FindMatches(basePath, this.IgnoredExceptions);
            entries = this.OutputOrCacheEntries(basePath, entries);
            foreach (string entry in entries)
                yield return NormalizePathSeparators(entry);
        }
        else
        {
            foreach (string basePath in basePaths)
            {
                IEnumerable<string> entries = this.FindMatches(basePath, this.IgnoredExceptions);
                entries =  this.OutputOrCacheEntries(basePath, entries);
                foreach (string entry in entries)
                    yield return NormalizePathSeparators(entry);
            }
        }

        // If multiple base paths or sort was explicitly selected, entry names were cached 
        // and need to be output now.
        if (!this.CanOutputImmediately)
        {
            List<string> entries = this.GetCachedEntries(basePaths);
            foreach (string entry in entries)
                yield return NormalizePathSeparators(entry);
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

    protected abstract IEnumerable<string> FindMatches(string basePath, IgnoredExceptionSet ignoredExceptions);

    private IEnumerable<string> OutputOrCacheEntries(string basePath, IEnumerable<string> entryPaths)
    {
        if (this.CanOutputImmediately)
        {
            foreach (string entryPath in entryPaths)
                yield return this.GetOutputPath(basePath, entryPath);
        }
        else
        {
            IEnumerable<string> allEntries = entryPaths
                .Select(entryPath => this.GetOutputPath(basePath, entryPath));
            this._entryCache = this._entryCache.AddRange(allEntries);
        }
    }

    private List<string> GetCachedEntries(List<string> basePaths)
    {
        StringComparer stringComparer = this.Args.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        // If multiple base paths, we need to remove duplicates.
        List<string> entryPaths = basePaths.Count > 1
            ? this._entryCache
                .Distinct(stringComparer)
                .ToList()
            : this._entryCache.ToList();

        entryPaths.Sort(stringComparer);

        return entryPaths;
    }

    private string GetOutputPath(string basePath, string path)
    {
        if (!this.UseFullyQualifiedOutputPaths)
            return path;

        return Path.GetFullPath(Path.Combine(this.CurrentWorkingDirectory, basePath, path));
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

    public static string ToBackSlashPathSeparators(string path) => path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    public static string ToForwardSlashPathSeparators(string path) => path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    public static string NormalizePathSeparators(string path) => NormalizeToBackSlashPathSeparators
        ? ToBackSlashPathSeparators(path)
        : NormalizeToForwardSlashPathSeparators
            ? ToForwardSlashPathSeparators(path)
            : path;

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
        return path.Split('/', '\\')
            .ToImmutableList();
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

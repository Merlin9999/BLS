using System.Collections.Immutable;

namespace BLS;

public abstract class AbstractGlobber : IGlobber
{
    protected readonly IGlobberArgs Args;
    private ImmutableList<string> _fileCache = ImmutableList<string>.Empty;

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
    
    protected AbstractGlobber(IGlobberArgs args)
    {
        this.Args = args;
    }

    public IEnumerable<Exception> IgnoredFileAccessExceptions => this.IgnoredExceptions.Exceptions;
    protected IgnoredExceptionSet IgnoredExceptions { get; private set; } = new IgnoredExceptionSet();

    public IEnumerable<string> Execute()
    {
        List<string> basePaths = this.Args.BasePaths.ToList();

        this.IgnoredExceptions = new IgnoredExceptionSet();

        if (basePaths.Count < 2)
        {
            string basePath = basePaths.Count < 1 ? "./" : basePaths[0];
            IEnumerable<string> files = this.FindMatches(basePath, this.IgnoredExceptions);
            files = this.OutputOrCacheFiles(basePath, files);
            foreach (string file in files)
                yield return NormalizePathSeparators(file);
        }
        else
        {
            foreach (string basePath in basePaths)
            {
                var ignoredFileAccessExceptions = new List<Exception>();
                IEnumerable<string> files = this.FindMatches(basePath, this.IgnoredExceptions);
                files =  this.OutputOrCacheFiles(basePath, files);
                foreach (string file in files)
                    yield return NormalizePathSeparators(file);
            }
        }

        // If multiple base paths or sort was explicitly selected, file names were cached 
        // and need to be output now.
        if (!this.CanOutputImmediately)
        {
            List<string> files = this.GetCachedFiles(basePaths);
            foreach (string file in files)
                yield return NormalizePathSeparators(file);
        }
    }

    protected abstract IEnumerable<string> FindMatches(string basePath, IgnoredExceptionSet ignoredExceptions);

    private IEnumerable<string> OutputOrCacheFiles(string basePath, IEnumerable<string> filePaths)
    {
        if (this.CanOutputImmediately)
        {
            foreach (string filePath in filePaths)
                yield return this.GetOutputPath(basePath, filePath);
        }
        else
        {
            IEnumerable<string> allFiles = filePaths
                .Select(filePath => this.GetOutputPath(basePath, filePath));
            this._fileCache = this._fileCache.AddRange(allFiles);
        }
    }

    private List<string> GetCachedFiles(List<string> basePaths)
    {
        StringComparer stringComparer = this.Args.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        // If multiple base paths, we need to remove duplicates.
        List<string> filePaths = basePaths.Count > 1
            ? this._fileCache
                .Distinct(stringComparer)
                .ToList()
            : this._fileCache.ToList();

        filePaths.Sort(stringComparer);

        return filePaths;
    }

    private string GetOutputPath(string basePath, string path)
    {
        if (!this.UseFullyQualifiedOutputPaths)
            return path;

        return Path.GetFullPath(Path.Combine(this.CurrentWorkingDirectory, basePath, path));
    }

    protected class IgnoredExceptionSet
    {
        private ImmutableHashSet<string> MessageSet { get; set; } = ImmutableHashSet<string>.Empty;
        public ImmutableList<Exception> Exceptions { get; private set; } = ImmutableList<Exception>.Empty;

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

    public static string BuildRelativeFileName(string fullFileNamePath, DirectoryInfo commonRootDir, DirectoryInfo baseDir)
    {
        string relativeFileName = Path.GetRelativePath(commonRootDir.FullName, fullFileNamePath);
        string prefix = Path.GetRelativePath(baseDir.FullName, commonRootDir.FullName);
        if (prefix != ".")
            relativeFileName = Path.Combine(prefix, relativeFileName);
        return relativeFileName;
    }

    public static ImmutableList<string> SplitPathIntoSegments(string path)
    {
        return path.Split('/', '\\')
            .Where(s => s.Trim() != string.Empty)
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

using System.Collections.Immutable;

namespace BLS;

public abstract class AbstractGlobber : IGlobber
{
    protected readonly IGlobberArgs Args;
    private ImmutableList<string> _fileCache = ImmutableList<string>.Empty;

    private bool? _canOutputImmediately;
    private bool? _useFullyQualifiedOutputPaths;
    private string? _currentDirectory;

    private string CurrentWorkingDirectory => this._currentDirectory ??= Directory.GetCurrentDirectory();
    private bool UseFullyQualifiedOutputPaths => this._useFullyQualifiedOutputPaths ??= this.Args.BasePaths.Count() > 1;
    private bool CanOutputImmediately => this._canOutputImmediately ??= !this.Args.Sort && (this.Args.AllowDuplicatesWhenMultipleBasePaths || this.Args.BasePaths.Count() <= 1);

    protected AbstractGlobber(IGlobberArgs args)
    {
        this.Args = args;
    }

    public IEnumerable<Exception> IgnoredFileAccessExceptions => this.IgnoredExceptions;
    protected ImmutableList<Exception> IgnoredExceptions { get; set; } = ImmutableList<Exception>.Empty;

    public IEnumerable<string> Execute()
    {
        List<string> basePaths = this.Args.BasePaths.ToList();
            
        this.IgnoredExceptions = ImmutableList<Exception>.Empty;

        if (basePaths.Count < 2)
        {
            string basePath = basePaths.Count < 1 ? "./" : basePaths[0];
            var ignoredFileAccessExceptions = new List<Exception>();
            IEnumerable<string> files = this.FindMatches(basePath, ignoredFileAccessExceptions);
            files = this.OutputOrCacheFiles(basePath, files);
            foreach (string file in files)
                yield return file;
            this.IgnoredExceptions = this.IgnoredExceptions.AddRange(ignoredFileAccessExceptions);
        }
        else
        {
            foreach (string basePath in basePaths)
            {
                var ignoredFileAccessExceptions = new List<Exception>();
                IEnumerable<string> files = this.FindMatches(basePath, ignoredFileAccessExceptions);
                files =  this.OutputOrCacheFiles(basePath, files);
                foreach (string file in files)
                    yield return file;
                this.IgnoredExceptions = this.IgnoredExceptions.AddRange(ignoredFileAccessExceptions);
            }
        }

        // If multiple base paths or sort was explicitly selected, file names were cached 
        // and need to be output now.
        if (!this.CanOutputImmediately)
        {
            List<string> files = this.GetCachedFiles(basePaths);
            foreach (string file in files) 
                yield return file;
        }
    }

    protected abstract IEnumerable<string> FindMatches(string basePath, List<Exception> ignoredFileAccessExceptions);

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
}
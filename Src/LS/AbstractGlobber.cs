using System.Collections.Immutable;
using System.Security;

namespace CLConsole;

public abstract class AbstractGlobber : IGlobber
{
    protected readonly IGlobberArgs _args;
    private ImmutableList<string> _fileCache = ImmutableList<string>.Empty;

    private bool? _canOutputImmediately;
    private bool? _useFullyQualifiedOutputPaths;
    private string? _currentDirectory;

    private string CurrentWorkingDirectory => this._currentDirectory ??= Directory.GetCurrentDirectory();
    private bool UseFullyQualifiedOutputPaths => this._useFullyQualifiedOutputPaths ??= this._args.BasePaths.Count() > 1;
    private bool CanOutputImmediately => this._canOutputImmediately ??= !this._args.Sort && (this._args.AllowDuplicatesWhenMultipleBasePaths || this._args.BasePaths.Count() <= 1);

    public AbstractGlobber(IGlobberArgs args)
    {
        this._args = args;
    }

    public IEnumerable<Exception> IgnoredFileAccessExceptions => this.IgnoredExceptions;
    protected ImmutableList<Exception> IgnoredExceptions { get; set; } = ImmutableList<Exception>.Empty;

    public async IAsyncEnumerable<string> ExecuteAsync()
    {
        List<string> basePaths = this._args.BasePaths.ToList();
            
        this.IgnoredExceptions = ImmutableList<Exception>.Empty;

        if (basePaths.Count < 2)
        {
            string basePath = basePaths.Count < 1 ? "./" : basePaths[0];
            var ignoredFileAccessExceptions = new List<Exception>();
            IAsyncEnumerable<string> files = this.FindMatchesAsync(basePath, ignoredFileAccessExceptions);
            files = this.OutputOrCacheFilesAsync(basePath, files);
            await foreach (string file in files)
                yield return file;
            this.IgnoredExceptions = this.IgnoredExceptions.AddRange(ignoredFileAccessExceptions);
        }
        else
        {
            foreach (string basePath in basePaths)
            {
                var ignoredFileAccessExceptions = new List<Exception>();
                IAsyncEnumerable<string> files = this.FindMatchesAsync(basePath, ignoredFileAccessExceptions);
                files =  this.OutputOrCacheFilesAsync(basePath, files);
                await foreach (string file in files)
                    yield return file;
                this.IgnoredExceptions = this.IgnoredExceptions.AddRange(ignoredFileAccessExceptions);
            }
        }

        // If multiple base paths or sort was explicitly selected, file names were cached 
        // and need to be output now.
        if (!this.CanOutputImmediately)
        {
            List<string> files = this.GetCachedFilesAsync(basePaths);
            foreach (string file in files) 
                yield return file;
        }
    }

    protected abstract IAsyncEnumerable<string> FindMatchesImplAsync(string basePath, List<Exception> ignoredFileAccessExceptions);

    private async IAsyncEnumerable<string> FindMatchesAsync(string basePath, List<Exception> ignoredFileAccessExceptions)
    {
        IAsyncEnumerable<string> files = this.FindMatchesImplAsync(basePath, ignoredFileAccessExceptions);
        await foreach (string file in files)
            yield return file;
    }

    private async IAsyncEnumerable<string> OutputOrCacheFilesAsync(string basePath, IAsyncEnumerable<string> filePaths)
    {
        if (this.CanOutputImmediately)
        {
            await foreach (string filePath in filePaths)
                yield return this.GetOutputPath(basePath, filePath);
        }
        else
        {
            IEnumerable<string> allFiles = filePaths.ToBlockingEnumerable()
                .Select(filePath => this.GetOutputPath(basePath, filePath));
            this._fileCache = this._fileCache.AddRange(allFiles);
        }
    }

    private List<string> GetCachedFilesAsync(List<string> basePaths)
    {
        StringComparer stringComparer = this._args.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

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
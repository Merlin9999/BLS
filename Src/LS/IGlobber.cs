using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace CLConsole
{
    public interface IGlobber
    {
        IEnumerable<Exception> IgnoredFileAccessExceptions { get; }
        IAsyncEnumerable<string> ExecuteAsync();
    }

    public class SystemGlobber : IGlobber
    {
        private readonly IGlobberArgs _args;
        private ImmutableList<string> _fileCache = ImmutableList<string>.Empty;

        private bool? _canOutputImmediately;
        private bool? _useFullyQualifiedOutputPaths;
        private string? _currentDirectory;

        private string CurrentWorkingDirectory => this._currentDirectory ??= Directory.GetCurrentDirectory();
        private bool UseFullyQualifiedOutputPaths => this._useFullyQualifiedOutputPaths ??= this._args.BasePaths.Count() > 1;
        private bool CanOutputImmediately => this._canOutputImmediately ??= !this._args.Sort && (this._args.AllowDuplicatesWhenMultipleBasePaths || this._args.BasePaths.Count() <= 1);

        public SystemGlobber(IGlobberArgs args)
        {
            this._args = args;
        }

        public IEnumerable<Exception> IgnoredFileAccessExceptions => this.IgnoredExceptions;
        private ImmutableList<Exception> IgnoredExceptions { get; set; } = ImmutableList<Exception>.Empty;

        public async IAsyncEnumerable<string> ExecuteAsync()
        {
            Matcher matcher = CreateMatcher(this._args);
            List<string> basePaths = this._args.BasePaths.ToList();
            
            this.IgnoredExceptions = ImmutableList<Exception>.Empty;

            if (basePaths.Count < 2)
            {
                string basePath = basePaths.Count < 1 ? "./" : basePaths[0];
                var ignoredFileAccessExceptions = new List<Exception>();
                IAsyncEnumerable<string> files = this.FindMatchesAsync(basePath, matcher, ignoredFileAccessExceptions);
                this.IgnoredExceptions = this.IgnoredExceptions.AddRange(ignoredFileAccessExceptions);
                files = this.OutputOrCacheFilesAsync(basePath, files);
                await foreach (string file in files)
                    yield return file;
            }
            else
            {
                foreach (string basePath in basePaths)
                {
                    var ignoredFileAccessExceptions = new List<Exception>();
                    IAsyncEnumerable<string> files = this.FindMatchesAsync(basePath, matcher, ignoredFileAccessExceptions);
                    this.IgnoredExceptions = this.IgnoredExceptions.AddRange(ignoredFileAccessExceptions);
                    files =  this.OutputOrCacheFilesAsync(basePath, files);
                    await foreach (string file in files)
                        yield return file;
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async IAsyncEnumerable<string> FindMatchesAsync(string basePath, Matcher matcher, List<Exception> ignoredFileAccessExceptions)
        {
            PatternMatchingResult result;
            try
            {
                result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(basePath)));
            }
            catch (SecurityException se) when (!this._args.AbortOnFileSystemAccessExceptions)
            {
                ignoredFileAccessExceptions.Add(se);
                yield break;
            }
            catch (UnauthorizedAccessException uae) when (!this._args.AbortOnFileSystemAccessExceptions)
            {
                ignoredFileAccessExceptions.Add(uae);
                yield break;
            }
            catch (DirectoryNotFoundException dnfe) when (!this._args.AbortOnFileSystemAccessExceptions)
            {
                ignoredFileAccessExceptions.Add(dnfe);
                yield break;
            }
            catch (IOException ioe) when (!this._args.AbortOnFileSystemAccessExceptions)
            {
                ignoredFileAccessExceptions.Add(ioe);
                yield break;
            }

            foreach (FilePatternMatch file in result.Files)
                yield return file.Path;
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

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

        private static Matcher CreateMatcher(IGlobberArgs args)
        {
            StringComparison stringComparison = args.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            var matcher = new Matcher(stringComparison);
            matcher.AddIncludePatterns(args.IncludeGlobPaths);
            matcher.AddExcludePatterns(args.ExcludeGlobPaths);
            return matcher;
        }
    }
}

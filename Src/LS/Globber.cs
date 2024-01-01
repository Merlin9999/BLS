using MediatR;
using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace CLConsole;

public class Globber
{
    private readonly ListFilesArgs _args;
    private readonly TextWriter _outputWriter;
    private ImmutableList<string> _fileCache = ImmutableList<string>.Empty;

    private bool? _canOutputImmediately;
    private bool? _useFullyQualifiedOutputPaths;
    private string? _currentDirectory;

    private string CurrentDirectory => this._currentDirectory ??= Directory.GetCurrentDirectory();
    private bool UseFullyQualifiedOutputPaths => this._useFullyQualifiedOutputPaths ??= this._args.BasePaths.Count() > 1;
    private bool CanOutputImmediately => this._canOutputImmediately ??= !this._args.Sort && (this._args.AllowDuplicates || this._args.BasePaths.Count() <= 1);

    public Globber(ListFilesArgs args, TextWriter outputWriter)
    {
        this._args = args;
        this._outputWriter = outputWriter;
    }

    public async Task ExecuteAsync()
    {
        Matcher matcher = CreateMatcher(this._args);
        List<string> basePaths = this._args.BasePaths.ToList();
        
        if (basePaths.Count < 2)
        {
            string basePath = basePaths.Count < 1 ? "./" : basePaths[0];
            List<string> files = await this.FindMatchesAsync(basePath, matcher);
            await this.AddFilesAsync(basePath, files);
        }
        else
        {
            foreach (string basePath in basePaths)
            {
                List<string> files = await this.FindMatchesAsync(basePath, matcher);
                await this.AddFilesAsync(basePath, files);
            }
        }
        
        // If multiple base paths or sort was explicitly selected, file names were cached 
        // and need to be output now.
        if (!this.CanOutputImmediately)
            await this.OutputCachedFilesAsync(basePaths);
    }

    private static Matcher CreateMatcher(ListFilesArgs args)
    {
        StringComparison stringComparison = args.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var matcher = new Matcher(stringComparison);
        matcher.AddIncludePatterns(args.IncludeGlobPaths);
        matcher.AddExcludePatterns(args.ExcludeGlobPaths);
        return matcher;
    }

    private Task<List<string>> FindMatchesAsync(string basePath, Matcher matcher)
    {
        PatternMatchingResult result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(basePath)));
        return Task.FromResult(result.Files.Select(x => this.GetOutputPath(basePath, x.Path)).ToList());
    }

    private async Task AddFilesAsync(string basePath, IEnumerable<string> filePaths)
    {
        if (this.CanOutputImmediately)
        {
            foreach (string filePath in filePaths)
                await this._outputWriter.WriteLineAsync(this.GetOutputPath(basePath, filePath));
        }
        else
        {
            this._fileCache = this._fileCache.AddRange(filePaths.Select(filePath => this.GetOutputPath(basePath, filePath)));
        }
    }

    private string GetOutputPath(string basePath, string path)
    {
        if (!this.UseFullyQualifiedOutputPaths)
            return path;

        return Path.GetFullPath(Path.Combine(this.CurrentDirectory, basePath, path));
    }

    private async Task OutputCachedFilesAsync(List<string> basePaths)
    {
        StringComparer stringComparer = this._args.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        // If multiple base paths, we need to remove duplicates.
        List<string> filePaths = basePaths.Count > 1
            ? this._fileCache
                .Distinct(stringComparer)
                .ToList()
            : this._fileCache.ToList();

        filePaths.Sort(stringComparer);

        foreach (string filePath in filePaths)
            await this._outputWriter.WriteLineAsync(filePath);
    }
}

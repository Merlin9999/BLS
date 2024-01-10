using MediatR;
using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.Security;

namespace CLConsole;

public class GlobToConsole
{
    private readonly IGlobberAndFactoryArgs _args;
    private readonly TextWriter _outputWriter;

    public GlobToConsole(IGlobberAndFactoryArgs args, TextWriter outputWriter)
    {
        this._args = args;
        this._outputWriter = outputWriter;
    }

    public async Task ExecuteAsync()
    {
        IGlobber globber = GlobberFactory.Create(this._args);

        IEnumerable<string> files = globber.Execute();
        foreach (string file in files)
            await this._outputWriter.WriteLineAsync(file);

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredFileAccessExceptions.ToList());
    }

    private async Task OutputIgnoredExceptionsAsync(List<Exception> ignoredFileAccessExceptions)
    {
        if (ignoredFileAccessExceptions.Count > 0)
            await this._outputWriter.WriteLineAsync("\nExceptions ignored:");

        foreach (Exception exception in ignoredFileAccessExceptions)
            await this._outputWriter.WriteLineAsync($"   {TranslateAggregateException(exception).Message}");

        Exception TranslateAggregateException(Exception exc)
        {
            if (exc is AggregateException agg && agg.InnerExceptions.Count == 1)
                return agg.InnerExceptions[0];

            return exc;
        }
    }
}
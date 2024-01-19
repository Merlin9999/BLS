namespace BLS.GlobWriters;

public abstract class AbstractGlobWriter
{
    protected static readonly string[] ParentFolderAsArray = [".."];
    protected TextWriter OutputWriter { get; }

    protected AbstractGlobWriter(TextWriter outputWriter)
    {
        this.OutputWriter = outputWriter;
    }

    public abstract Task ExecuteAsync();

    protected async Task OutputIgnoredExceptionsAsync(List<Exception> ignoredFileAccessExceptions)
    {
        if (ignoredFileAccessExceptions.Count > 0)
            await this.OutputWriter.WriteLineAsync("\nExceptions ignored:");

        foreach (Exception exception in ignoredFileAccessExceptions)
            await this.OutputWriter.WriteLineAsync($"   {TranslateAggregateException(exception).Message}");

        static Exception TranslateAggregateException(Exception exc)
        {
            if (exc is AggregateException agg && agg.InnerExceptions.Count == 1)
                return agg.InnerExceptions[0];

            return exc;
        }
    }
}
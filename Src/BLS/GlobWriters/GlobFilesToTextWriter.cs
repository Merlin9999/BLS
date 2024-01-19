using BLS.Globbers;

namespace BLS.GlobWriters;

public class GlobFilesToTextWriter(IGlobberAndFactoryArgs args, TextWriter outputWriter)
    : AbstractGlobWriter(outputWriter)
{
    public override async Task ExecuteAsync()
    {
        IGlobber globber = FileGlobberFactory.Create(args);

        IEnumerable<string> files = globber.Execute();
        foreach (string file in files)
            await this.OutputWriter.WriteLineAsync(file);

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredAccessExceptions.ToList());
    }
}
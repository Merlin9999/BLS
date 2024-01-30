using BLS.Globbers;

namespace BLS.GlobWriters;

public class GlobFolderListToTextWriter(IGlobberArgs args, TextWriter outputWriter) : AbstractGlobWriter(outputWriter)
{
    public override async Task ExecuteAsync()
    {
        IGlobber globber = new FolderGlobber(args);

        IEnumerable<string> folders = globber.Execute();
        foreach (string folder in folders)
            await this.OutputWriter.WriteLineAsync(folder);

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredAccessExceptions.ToList());
    }
}
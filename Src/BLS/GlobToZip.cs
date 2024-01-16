using System.IO.Compression;

namespace BLS;

public class GlobToZip : AbstractGlobWriter
{
    private readonly ZipArgs _args;

    public GlobToZip(ZipArgs args, TextWriter outputWriter)
        : base(outputWriter)
    {
        this._args = args;
    }

    public async Task ExecuteAsync()
    {
        bool foundParentFolderInIncludes = this._args.IncludeGlobPaths
            .Any(p => AbstractGlobber.SplitPathAndNormalizeRelativeSegments(p).StartsWith(new[]{".."}));
        if (foundParentFolderInIncludes)
            throw new ArgumentException("Avoid using \"..\" folders in glob expressions when creating a zip file!");

        IGlobber globber = GlobberFactory.Create(this._args);

        IEnumerable<string> files = globber.Execute();

        await using var zipFile = new FileStream(this._args.ZipFileName, FileMode.OpenOrCreate);
        using var zipArchive = new ZipArchive(zipFile, ZipArchiveMode.Update);

        foreach (string file in files)
        {
            await using var fileInStream = File.Open(Path.Combine(this._args.BasePath, file), FileMode.Open);

            ZipArchiveEntry fileEntry = zipArchive.CreateEntry(file);
            await using var archiveOutStream = fileEntry.Open();
            
            await fileInStream.CopyToAsync(archiveOutStream);
        }

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredFileAccessExceptions.ToList());
    }
}
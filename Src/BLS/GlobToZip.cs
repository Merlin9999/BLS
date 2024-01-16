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

        bool duplicateFileInZipHandling = this._args.ReplaceOnDuplicate || this._args.ErrorOnDuplicate;
        IGlobber globber = GlobberFactory.Create(this._args);

        IEnumerable<string> files = globber.Execute();

        await using var zipFile = new FileStream(this._args.ZipFileName, FileMode.OpenOrCreate);
        using var zipArchive = new ZipArchive(zipFile, ZipArchiveMode.Update);

        var comparer = this._args.CaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        HashSet<string> filesInZipWithDups;
        HashSet<string> filesInZip;
        if (duplicateFileInZipHandling)
        {
            ILookup<string, ZipArchiveEntry> zipEntryLookup = zipArchive.Entries
                .ToLookup(ze => AbstractGlobber.NormalizePathSeparators(ze.FullName), comparer);

            filesInZipWithDups = zipEntryLookup
                .Where(x => x.Skip(1).Any()) // More than one matching Entry.
                .Select(x => x.Key)
                .ToHashSet(comparer);

            filesInZip = zipEntryLookup.Select(x => x.Key).ToHashSet(comparer);
        }
        else
        {
            filesInZipWithDups = new HashSet<string>(comparer);
            filesInZip = new HashSet<string>(comparer);
        }

        foreach (string file in files)
        {
            if (duplicateFileInZipHandling)
            {
                if (!filesInZip.Add(file))
                {
                    if (this._args.ReplaceOnDuplicate)
                    {
                        if (filesInZipWithDups.Contains(file))
                        {
                            var entriesToDelete = new List<ZipArchiveEntry>();
                            foreach (ZipArchiveEntry archiveEntry in zipArchive.Entries)
                                if (comparer.Compare(archiveEntry.FullName, file) == 0)
                                    entriesToDelete.Add(archiveEntry);
                            foreach (ZipArchiveEntry archiveEntry in entriesToDelete)
                                archiveEntry.Delete();
                            filesInZipWithDups.Remove(file);
                        }
                        else
                        {
                            zipArchive.GetEntry(file)?.Delete();
                        }
                    }
                    else if (this._args.ErrorOnDuplicate)
                        throw new InvalidOperationException($"Zip already contains the file \"{file}\"!");
                    else
                        throw new NotImplementedException();
                }
            }

            await using var fileInStream = File.Open(Path.Combine(this._args.BasePath, file), FileMode.Open);

            ZipArchiveEntry fileEntry = zipArchive.CreateEntry(file);
            await using var archiveOutStream = fileEntry.Open();
            
            await fileInStream.CopyToAsync(archiveOutStream);
        }

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredFileAccessExceptions.ToList());
    }
}
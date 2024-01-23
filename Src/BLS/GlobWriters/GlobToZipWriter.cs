using System.IO.Compression;
using BLS.Globbers;

namespace BLS.GlobWriters;

public class GlobToZipWriter(ZipArgs args, TextWriter outputWriter)
    : AbstractGlobToFileWriter<ZipArgs>(args, outputWriter)
{
    protected override async Task WriteFilesAsync(IEnumerable<string> files, StringComparer comparer)
    {
        bool duplicateFileInZipHandling = this.Args.ReplaceOnDuplicate || this.Args.ErrorOnDuplicate;

        await using var zipFile = new FileStream(this.Args.ZipFileName, FileMode.OpenOrCreate);
        using var zipArchive = new ZipArchive(zipFile, ZipArchiveMode.Update);

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
                    if (this.Args.ReplaceOnDuplicate)
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
                    else if (this.Args.ErrorOnDuplicate)
                        throw new InvalidOperationException($"Zip already contains the file \"{file}\"!");
                    else
                        throw new NotImplementedException();
                }
            }

            string sourceFileName = Path.Combine(this.Args.BasePath, file);
            ZipArchiveEntry targetEntry = zipArchive.CreateEntry(file);
            
            await using var sourceFileStream = File.Open(sourceFileName, FileMode.Open);
            await using var archiveTargetStream = targetEntry.Open();
            
            await sourceFileStream.CopyToAsync(archiveTargetStream);
            targetEntry.LastWriteTime = File.GetLastWriteTime(sourceFileName);
        }
    }
}
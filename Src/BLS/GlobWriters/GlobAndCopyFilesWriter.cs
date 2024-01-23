namespace BLS.GlobWriters;

public class GlobAndCopyFilesWriter(CopyFilesArgs args, TextWriter outputWriter)
    : AbstractGlobToFileWriter<CopyFilesArgs>(args, outputWriter)
{
    protected override async Task WriteFilesAsync(IEnumerable<string> files, StringComparer comparer)
    {
        foreach (string file in files)
        {
            string sourceFileName = Path.Combine(this.Args.BasePath, file);
            string targetFileName = Path.Combine(this.Args.TargetFolder, file);
            
            {
                if (this.Args.ErrorOnDuplicate && File.Exists(targetFileName))
                    throw new InvalidOperationException($"The file \"{file}\" already exists!");

                string? targetDirectory = Path.GetDirectoryName(targetFileName);
                if (!string.IsNullOrEmpty(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                await using var sourceFileStream = File.Open(sourceFileName, FileMode.Open);
                await using var targetFileStream = File.Open(targetFileName, FileMode.Create);

                await sourceFileStream.CopyToAsync(targetFileStream);
            }

            // Block above ensures that the files above were flushed and closed before assigning the time.
            File.SetLastWriteTime(targetFileName, File.GetLastWriteTime(sourceFileName));
        }
    }
}
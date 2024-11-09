using System.Buffers.Text;
using System.Security.Cryptography;
using BLS.Globbers;

namespace BLS.GlobWriters;

public class GlobAndEncodeFilesWriter(EncodeFilesArgs args, TextWriter outputWriter)
    : AbstractGlobToFileWriter<EncodeFilesArgs>(args, outputWriter)
{
    protected override async Task WriteFilesAsync(IEnumerable<FilePathInfo> files, StringComparer comparer)
    {
        foreach (FilePathInfo fileInfo in files)
        {
            string sourceFileName = Path.Combine(this.Args.BasePath, fileInfo.EntryPath);
            string targetFileNameWithoutBasePath = fileInfo.EntryPath + args.EncodedExtension;
            string targetFileName = Path.Combine(this.Args.TargetFolder, targetFileNameWithoutBasePath);

            {
                if (this.Args.ErrorOnDuplicate && File.Exists(targetFileName))
                    throw new InvalidOperationException($"The file \"{targetFileNameWithoutBasePath}\" already exists!");

                string? targetDirectory = Path.GetDirectoryName(targetFileName);
                if (!string.IsNullOrEmpty(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);
                
                await using var sourceFileStream = File.Open(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.None);
                await using var base64Stream = new CryptoStream(sourceFileStream, new ToBase64Transform(), CryptoStreamMode.Read);
                await using var targetFileStream = File.Open(targetFileName, FileMode.Create, FileAccess.Write, FileShare.None);

                await base64Stream.CopyToAsync(targetFileStream);
            }

            // Block above ensures that the files above were flushed and closed before assigning the time.
            File.SetLastWriteTime(targetFileName, fileInfo.EntryInfo.LastWriteTime);
        }
    }
}
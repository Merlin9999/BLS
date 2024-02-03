using BLS.Globbers;
using System.Reflection.Emit;
using System.Security.AccessControl;
using static System.Net.Mime.MediaTypeNames;

namespace BLS.GlobWriters;

public class GlobFileListToTextWriter(IGlobberDisplayFileArgs args, TextWriter outputWriter)
    : AbstractGlobToTextWriter(outputWriter)
{
    public override async Task ExecuteAsync()
    {
        IGlobber<FilePathInfo, FileInfo> globber = FileGlobberFactory.Create(args);

        IEnumerable<FilePathInfo> files = globber.Execute();
        foreach (FilePathInfo file in files)
            await this.OutputWriter.WriteLineAsync(this.FormatFileLine(file));

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredAccessExceptions.ToList());
    }

    private string FormatFileLine(FilePathInfo fileInfo)
    {
        if (!args.DisplayDetails && !args.DisplayOwner)
            return fileInfo.EntryPath;

        if (args.DisplayDetails && args.DisplayOwner)
            return $"{GetAttribs(fileInfo.EntryInfo.Attributes)}  {FormatDateTime(fileInfo.EntryInfo.LastWriteTime)}  {GetFileSize(fileInfo.EntryInfo)}  {GetOwner(fileInfo.EntryInfo)}  {fileInfo.EntryPath}";

        if (args.DisplayDetails)
            return $"{GetAttribs(fileInfo.EntryInfo.Attributes)}  {FormatDateTime(fileInfo.EntryInfo.LastWriteTime)}  {GetFileSize(fileInfo.EntryInfo)}  {fileInfo.EntryPath}";

        return $"{GetOwner(fileInfo.EntryInfo)}  {fileInfo.EntryPath}";
    }

    private static string GetOwner(FileInfo fileInfo, int maxLength = 20)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            FileSecurity fileSecurity = fileInfo.GetAccessControl();
            string owner = fileSecurity.GetOwner(typeof(System.Security.Principal.NTAccount))?.ToString() ?? "<Unknown>";
            return PadRightAndTruncate(owner, maxLength);
        }

        return PadRightAndTruncate("<Windows Only>", maxLength);
    }

    private static string GetFileSize(FileInfo fileInfo, int maxLength = 15)
    {
        return FormatNumber(fileInfo.Length);

        string FormatNumber(decimal number)
        {
            string value = $"{number:N0}";
            if (value.Length <= maxLength)
                return PadLeft(value, maxLength);

            number /= 1024;
            if (number < 1000m)
                return FormatToLength(number, "KB");

            number /= 1024;
            if (number < 1000m)
                return FormatToLength(number, "MB");

            number /= 1024;
            if (number < 1000m)
                return FormatToLength(number, "GB");

            number /= 1024;
            return FormatToLength(number, "TB");
        }

        string FormatToLength(decimal number, string suffix)
        {
            string value = $"{number:N2} {suffix}";
            if (value.Length <= maxLength)
                return $"{PadLeft(value, maxLength)}";

            value = $"{number:N1} {suffix}";
            if (value.Length <= maxLength)
                return $"{PadLeft(value, maxLength)}";

            value = $"{number:N0} {suffix}";
            return $"{PadLeft(value, maxLength)}";
        }
    }
}
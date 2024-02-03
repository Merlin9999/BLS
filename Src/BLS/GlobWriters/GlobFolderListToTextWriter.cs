using BLS.Globbers;
using System.Security.AccessControl;

namespace BLS.GlobWriters;

public class GlobFolderListToTextWriter(IGlobberDisplayFolderArgs args, TextWriter outputWriter) 
    : AbstractGlobToTextWriter(outputWriter)
{
    public override async Task ExecuteAsync()
    {
        var globber = new FolderGlobber(args);

        IEnumerable<FolderPathInfo> folders = globber.Execute();
        foreach (FolderPathInfo folder in folders)
            await this.OutputWriter.WriteLineAsync(this.FormatFileLine(folder));

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredAccessExceptions.ToList());
    }

    private string? FormatFileLine(FolderPathInfo folderInfo)
    {
        if (!args.DisplayDetails && !args.DisplayOwner)
            return folderInfo.EntryPath;

        if (args.DisplayDetails && args.DisplayOwner)
            return $"{GetAttribs(folderInfo.EntryInfo.Attributes)}  {FormatDateTime(folderInfo.EntryInfo.LastWriteTime)}  {GetOwner(folderInfo.EntryInfo)}  {folderInfo.EntryPath}";

        if (args.DisplayDetails)
            return $"{GetAttribs(folderInfo.EntryInfo.Attributes)}  {FormatDateTime(folderInfo.EntryInfo.LastWriteTime)}  {folderInfo.EntryPath}";

        return $"{GetOwner(folderInfo.EntryInfo)}  {folderInfo.EntryPath}";
    }

    private static string GetOwner(DirectoryInfo dirInfo, int maxLength = 20)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            DirectorySecurity dirSecurity = dirInfo.GetAccessControl();
            string owner = dirSecurity.GetOwner(typeof(System.Security.Principal.NTAccount))?.ToString() ?? "<Unknown>";
            return PadRightAndTruncate(owner, maxLength);
        }

        return PadRightAndTruncate("<Windows Only>", maxLength);
    }
}
using BLS.Globbers;
using System.Security.AccessControl;

namespace BLS.GlobWriters;

public class GlobFolderListToTextWriter(IGlobberDisplayFolderArgs args, TextWriter outputWriter) 
    : AbstractGlobToTextWriter(outputWriter)
{
    public override async Task ExecuteAsync()
    {
        IGlobber globber = new FolderGlobber(args);

        IEnumerable<string> folders = globber.Execute();
        foreach (string folder in folders)
            await this.OutputWriter.WriteLineAsync(this.FormatFileLine(folder));

        await this.OutputIgnoredExceptionsAsync(globber.IgnoredAccessExceptions.ToList());
    }

    private string? FormatFileLine(string folder)
    {
        if (!args.DisplayDetails && !args.DisplayOwner)
            return folder;

        DirectoryInfo folderInfo = args.BasePaths.Count() == 1
            ? new DirectoryInfo(Path.Combine(args.BasePaths.First(), folder))
            : new DirectoryInfo(folder);
        if (args.DisplayDetails && args.DisplayOwner)
            return $"{GetAttribs(folderInfo.Attributes)}  {FormatDateTime(folderInfo.LastWriteTime)}  {GetOwner(folderInfo)}  {folder}";

        if (args.DisplayDetails)
            return $"{GetAttribs(folderInfo.Attributes)}  {FormatDateTime(folderInfo.LastWriteTime)}  {folder}";

        return $"{GetOwner(folderInfo)}  {folder}";
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
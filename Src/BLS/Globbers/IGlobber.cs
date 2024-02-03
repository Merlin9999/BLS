namespace BLS.Globbers;

public interface IGlobber<out TFolderEntryPathInfo, TFileSysInfo>
    where TFileSysInfo : FileSystemInfo
    where TFolderEntryPathInfo : IFolderEntryPathInfo<TFileSysInfo>
{
    IEnumerable<Exception> IgnoredAccessExceptions { get; }
    IEnumerable<TFolderEntryPathInfo> Execute();
}
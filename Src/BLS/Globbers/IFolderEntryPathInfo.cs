namespace BLS.Globbers;

public interface IFolderEntryPathInfo<out TFileSysInfo> where TFileSysInfo : FileSystemInfo
{
    string CurrentWorkingDirectory { get; init; }
    string BasePath { get; init; }
    string EntryPath { get; init; }
    TFileSysInfo EntryInfo { get; }
}

public abstract record AbstractFolderEntryPathInfo<TFileSysInfo> : IFolderEntryPathInfo<TFileSysInfo> where TFileSysInfo : FileSystemInfo
{
    private TFileSysInfo? _entryInfo;
    public string CurrentWorkingDirectory { get; init; } = Environment.CurrentDirectory;
    public string BasePath { get; init; } = string.Empty;
    public required string EntryPath { get; init; }

    public TFileSysInfo EntryInfo
    {
        get => this._entryInfo ??= this.CreateEntryInfo(Path.IsPathRooted(this.EntryPath)
            ? this.EntryPath
            : Path.Combine(this.CurrentWorkingDirectory, this.BasePath, this.EntryPath));
        init => this._entryInfo = value;
    }

    protected abstract TFileSysInfo CreateEntryInfo(string fullPath);
}

public record FilePathInfo : AbstractFolderEntryPathInfo<FileInfo>
{
    protected override FileInfo CreateEntryInfo(string fullPath)
    {
        return new FileInfo(fullPath);
    }
}

public record FolderPathInfo : AbstractFolderEntryPathInfo<DirectoryInfo>
{
    protected override DirectoryInfo CreateEntryInfo(string fullPath)
    {
        return new DirectoryInfo(fullPath);
    }
}

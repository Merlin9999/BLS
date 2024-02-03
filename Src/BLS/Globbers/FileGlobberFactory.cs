namespace BLS.Globbers;

public static class FileGlobberFactory
{
    public static IGlobber<FilePathInfo, FileInfo> Create(IGlobberAndFactoryArgs args) => Create(args, args);

    public static IGlobber<FilePathInfo, FileInfo> Create(IGlobberFactoryArgs factoryArgs, IGlobberArgs args)
    {
        return factoryArgs.UseFrameworkGlobber
            ? (IGlobber<FilePathInfo, FileInfo>)new SystemFileGlobber(args)
            : (IGlobber<FilePathInfo, FileInfo>)new FileGlobber(args);
    }
}
namespace BLS.Globbers;

public static class FileGlobberFactory
{
    public static IGlobber Create(IGlobberAndFactoryArgs args) => Create(args, args);

    public static IGlobber Create(IGlobberFactoryArgs factoryArgs, IGlobberArgs args)
    {
        return factoryArgs.UseFrameworkGlobber
            ? (IGlobber)new SystemFileGlobber(args)
            : (IGlobber)new FileGlobber(args);
    }
}
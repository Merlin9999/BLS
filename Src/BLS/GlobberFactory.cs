namespace BLS;

public class GlobberFactory
{
    public static IGlobber Create(IGlobberAndFactoryArgs args) => Create(args, args);

    public static IGlobber Create(IGlobberFactoryArgs factoryArgs, IGlobberArgs args)
    {
        return factoryArgs.UseFrameworkGlobber
            ? (IGlobber)new SystemGlobber(args)
            : (IGlobber)new ImprovedGlobber(args);
    }
}
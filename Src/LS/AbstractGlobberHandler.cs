using Serilog;

namespace CLConsole;

public abstract class AbstractGlobberHandler
{
    protected static void LogPaths(IGlobberArgs args, ILogger logger)
    {
        List<string> includeGlobPaths = args.IncludeGlobPaths.ToList();
        List<string> excludeGlobPaths = args.ExcludeGlobPaths.ToList();

        logger.Debug(includeGlobPaths.Count > 0 ? "Included Glob Paths:" : "No Include Glob Paths!");
        for (var index = 0; index < includeGlobPaths.Count; index++)
        {
            string globPath = includeGlobPaths[index];
            logger.Debug($"   {index:D2} - \"{globPath}\"");
        }

        logger.Debug(excludeGlobPaths.Count > 0 ? "Excluded Glob Paths:" : "No Exclude Glob Paths.");
        for (var index = 0; index < excludeGlobPaths.Count; index++)
        {
            string globPath = excludeGlobPaths[index];
            logger.Debug($"   {index:D2} - \"{globPath}\"");
        }
    }
}
using System.ComponentModel;
using System.Text.RegularExpressions;
using CommandLine;
using CommandLine.Text;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CLConsole;

public enum EExitCode
{
    Success = 0,
    Unknown = 1,
    UnhandledException = 2,
    InvalidApplicationArguments = 3,
}

internal abstract class Program
{
    private static readonly ServiceCollection Services = new();

    static int Main(string[] args)
    {
        try
        {
            EExitCode retValue = EExitCode.Unknown;

            if (args.Length == 0)
                args = ["--help"];


            IMediator mediator = InitializeDI();

            ParserResult<object> parserResult = Parser.Default.ParseArguments<ListFilesArgs, SearchPathArgs>(args);

            //if (args.Length == 0)
            //{
            //    HelpText helpText = HelpText.AutoBuild(parserResult, Console.WindowWidth);
            //    Console.WriteLine(helpText);
            //    return (int)EExitCode.InvalidApplicationArguments;
            //}

            retValue = parserResult.MapResult(
                (ListFilesArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (SearchPathArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (errors) => EExitCode.InvalidApplicationArguments);

            return (int)retValue;
        }
        catch (Exception exc)
        {
            Log.Error(exc, "Fatal error!");
            return (int)EExitCode.UnhandledException;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static async Task<EExitCode> AsyncMain<TOptions>(TOptions options)
        where TOptions : IRequest<EExitCode>
    {
        try
        {
            IMediator mediator = InitializeDI();
            return await mediator.Send(options);
        }
        catch (Exception exc)
        {
            Log.Error(exc, "Fatal error!");
            return EExitCode.UnhandledException;
        }
    }

    private static IMediator InitializeDI()
    {
        Services.AddSingleton<IServiceCollection>(Services);
        Services.AddSingleton<ILogger>(CreateLogger());
        Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        //Log.Information("Logger initialized.");

        ServiceProvider sp =Services.BuildServiceProvider(true);

        return sp.GetService<IMediator>() 
            ?? throw new InvalidOperationException($"Dependency Injection for {nameof(IMediator)} has not been properly setup!");
    }

    private static Logger CreateLogger()
    {
        Logger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Async(x => x.Console(restrictedToMinimumLevel: LogEventLevel.Warning))
            .WriteTo.Async(x => x.Debug(restrictedToMinimumLevel: LogEventLevel.Verbose))
            .CreateLogger();
        Log.Logger = logger;

        return logger;
    }
}

[Verb("list-files", isDefault: true, ["files"], HelpText = "List Files")]
public class ListFilesArgs : BaseArgs, IRequest<EExitCode>, IGlobberArgs
{
    [Option('b', "base-paths", HelpText = "One or more base paths for globbing. Default is the working directory.")]
    public IEnumerable<string> BasePaths { get; set; } = new List<string>();

    [Option('d', "allow-duplicates", Default = false, HelpText = "Toggle allowing duplicates if multiple base paths for faster output.")]
    public bool AllowDuplicatesWhenMultipleBasePaths { get; set; }
}

[Verb("search-path", isDefault: false, ["path"], HelpText = "Search Path")]
public class SearchPathArgs : BaseArgs, IRequest<EExitCode>, IGlobberArgs
{
    public IEnumerable<string> BasePaths { get; set; } = new List<string>();

    [Option('d', "allow-duplicates", Default = true, HelpText = "Toggle allowing duplicates if multiple base paths for faster output.")]
    public bool AllowDuplicatesWhenMultipleBasePaths { get; set; } = true;
}

public abstract class BaseArgs
{
    [Value(0, HelpText = "Included Paths. At least 1 is required.")]
    public IEnumerable<string> IncludeGlobPaths { get; set; } = new List<string>();

    [Option('x', "exclude", HelpText = "Excluded Paths (optional)")]
    public IEnumerable<string> ExcludeGlobPaths { get; set; } = new List<string>();

    [Option('c', "case-sensitive", Default = false, HelpText = "Toggle to add case sensitive path matching.")]
    public bool CaseSensitive { get; set; }

    [Option('s', "sort", Default = false, HelpText = "Toggle to sort.")]
    public bool Sort { get; set; }

    [Option('a', "abort-on-access-errors", Default = false, HelpText = "Toggle abort on file system access errors.")]
    public bool AbortOnFileSystemAccessExceptions { get; set; }
}
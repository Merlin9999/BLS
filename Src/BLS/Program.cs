using System.Collections.Immutable;
using BLS.Globbers;
using CommandLine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;
using Nito.Disposables;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace BLS;

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

            var argsParser = new Parser(cfg =>
            {
                cfg.HelpWriter = Console.Error;
                cfg.CaseInsensitiveEnumValues = true;
            });

            ParserResult<object> parserResult = argsParser.ParseArguments<ListFilesArgs, ListFoldersArgs, SearchPathArgs, ZipArgs, CopyFilesArgs, GlobHelpArgs>(args);

            retValue = parserResult.MapResult(
                (ListFilesArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (ListFoldersArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (SearchPathArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (ZipArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (CopyFilesArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (GlobHelpArgs options) => AsyncContext.Run(() => AsyncMain(options)),
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

[Verb("glob-help", isDefault: false, ["glob"], HelpText = "Open a browser to a web page on related glob formatting.")]
public class GlobHelpArgs : IRequest<EExitCode>
{
}

[Verb("list-files", isDefault: true, ["files"], HelpText = "List Files")]
public class ListFilesArgs : BaseArgs, IRequest<EExitCode>, IGlobberDisplayFileArgs
{
    [Option('b', "base-paths", HelpText = "One or more base paths for globbing. Default is the working directory.")]
    public IEnumerable<string> BasePaths { get; set; } = new List<string>();

    [Option('f', "use-framework-globber", Default = false, HelpText = "Revert to DotNet Framework Globber")]
    public bool UseFrameworkGlobber { get; set; }

    [Option('t', "details", Default = false, HelpText = "Display Attributes, Last Write Time, and File Size")]
    public bool DisplayDetails { get; set; }

    [Option('w', "owner", Default = false, HelpText = "Display Owner (Windows Only)")]
    public bool DisplayOwner { get; set; }
}

[Verb("list-folders", isDefault: false, ["folders"], HelpText = "List Folders")]
public class ListFoldersArgs : BaseArgs, IRequest<EExitCode>, IGlobberDisplayFolderArgs
{
    [Option('b', "base-paths", HelpText = "One or more base paths for globbing. Default is the working directory.")]
    public IEnumerable<string> BasePaths { get; set; } = new List<string>();

    [Option('t', "details", Default = false, HelpText = "Display Attributes and Last Write Time")]
    public bool DisplayDetails { get; set; }

    [Option('w', "owner", Default = false, HelpText = "Display Owner (Windows Only)")]
    public bool DisplayOwner { get; set; }
}

[Verb("search-path", isDefault: false, ["path"], HelpText = "Search Path")]
public class SearchPathArgs : BaseArgs, IRequest<EExitCode>, IGlobberDisplayFileArgs
{
    public IEnumerable<string> BasePaths { get; set; } = new List<string>();

    [Option('f', "use-framework-globber", Default = false, HelpText = "Revert to DotNet Framework Globber")]
    public bool UseFrameworkGlobber { get; set; }

    [Option('t', "details", Default = false, HelpText = "Display Attributes, Last Write Time, and File Size")]
    public bool DisplayDetails { get; set; }

    [Option('w', "owner", Default = false, HelpText = "Display Owner (Windows Only)")]
    public bool DisplayOwner { get; set; }
}

[Verb("zip-files", isDefault: false, ["zip"], HelpText = "Copy Files to a Zip Archive")]
public class ZipArgs : GlobToWriteFileBaseArgs, IGlobToWriteFileAndFactoryArgs, IRequest<EExitCode>
{
    [Option('z', "zip-file", Required = true, HelpText = "Zip file to create or update")]
    public required string ZipFileName { get; set; }

    [Option('r', "replace-files", SetName = "file-exists-replace", Default = false, HelpText = "Toggle replacing files that already exist in the zip file")]
    public bool ReplaceOnDuplicate { get; set; }

    [Option('e', "error-on-file-exist", SetName = "file-exists-error", Default = false, HelpText = "Toggle erroring if a file already exists in the zip file")]
    public bool ErrorOnDuplicate { get; set; }

    [Option('f', "use-framework-globber", Default = false, HelpText = "Revert to DotNet Framework Globber")]
    public bool UseFrameworkGlobber { get; set; }
}

[Verb("copy-files", isDefault: false, ["copy"], HelpText = "Copy Files to Folder")]
public class CopyFilesArgs : GlobToWriteFileBaseArgs, IGlobToWriteFileAndFactoryArgs, IRequest<EExitCode>
{
    [Option('t', "target-path", Required = true, HelpText = "Target folder to copy files to")]
    public required string TargetFolder { get; set; }

    [Option('r', "replace-files", Default = false, HelpText = "Toggle replacing files that already exist")]
    public bool ReplaceOnDuplicate { get; set; }

    public bool ErrorOnDuplicate
    {
        get => !this.ReplaceOnDuplicate;
        set => this.ReplaceOnDuplicate = !value;
    }

    [Option('f', "use-framework-globber", Default = false, HelpText = "Revert to DotNet Framework Globber")]
    public bool UseFrameworkGlobber { get; set; }
}

public class GlobToWriteFileBaseArgs : BaseArgs
{
    [Option('b', "base-path", HelpText = "One or more base paths for globbing. Default is the working directory.")]
    public required string BasePath
    {
        get => this.BasePaths.FirstOrDefault() ?? string.Empty;
        set => this.BasePaths = ImmutableList<string>.Empty.Add(value);
    }
    public IEnumerable<string> BasePaths { get; set; } = ImmutableList<string>.Empty.Add(".");
}

public abstract class BaseArgs
{
    [Value(0, Required = true, MetaName = "Include Glob List", HelpText = "First args are unnamed glob paths. At least 1 is required.")]
    public IEnumerable<string> IncludeGlobPaths { get; set; } = new List<string>();

    [Option('x', "exclude", HelpText = "Exclude Glob List (optional)")]
    public IEnumerable<string> ExcludeGlobPaths { get; set; } = new List<string>();

    [Option('q', "fully-qualified-paths", HelpText = "Output fully qualified paths")]
    public bool UseFullyQualifiedPaths { get; set; }

    [Option('c', "case-sensitive", Default = false, HelpText = "Toggle to add case sensitive path matching")]
    public bool CaseSensitive { get; set; }

    [Option('s', "sort", Default = false, HelpText = "Toggle to sort by name")]
    public bool Sort { get; set; }

    [Option('d', "allow-duplicates", Default = false, HelpText = "Toggle allowing duplicates if multiple base paths for faster output")]
    public bool AllowDuplicatesWhenMultipleBasePaths { get; set; }

    [Option('a', "abort-on-access-errors", Default = false, HelpText = "Toggle abort on file system access errors")]
    public bool AbortOnFileSystemAccessExceptions { get; set; }
}

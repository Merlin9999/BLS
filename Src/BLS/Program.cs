﻿using System.Collections.Immutable;
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
    public static readonly string Base64Extension = ".base64";

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

            ParserResult<object> parserResult = argsParser.ParseArguments<ListFilesArgs, ListFoldersArgs, SearchPathArgs, ZipArgs, CopyFilesArgs, EncodeFilesArgs, DecodeFilesArgs, GlobHelpArgs>(args);

            retValue = parserResult.MapResult(
                (ListFilesArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (ListFoldersArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (SearchPathArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (ZipArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (CopyFilesArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (EncodeFilesArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                (DecodeFilesArgs options) => AsyncContext.Run(() => AsyncMain(options)),
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

public enum ESortType
{
    Name = 0x1,
    N = Name,
    Extension = 0x2,
    E = Extension,
    Date = 0x4,
    D = Date,
    Size = 0x8,
    S = Size,
}

[Verb("glob-help", isDefault: false, ["glob"], HelpText = "Open a browser to a web page on related glob formatting.")]
public class GlobHelpArgs : IRequest<EExitCode>
{
}

[Verb("list-files", isDefault: true, ["files"], HelpText = "List Files")]
public class ListFilesArgs : BaseArgs, IRequest<EExitCode>, IGlobberDisplayFileArgs
{
    [Option('b', "base-paths", HelpText = "One or more base paths for globbing. Default is the current working directory.")]
    public IEnumerable<string> BasePaths { get; set; } = new List<string>();

    [Option('f', "use-framework-globber", Default = false, HelpText = "Revert to DotNet Framework Globber")]
    public bool UseFrameworkGlobber { get; set; }

    [Option('s', "sort", Default = null, HelpText = "Sort by (N)ame, (E)xtension, (D)ate, (S)ize. ex: -sd  ex: -s date")]
    public ESortType? Sort { get; set; }

    [Option('d', "descending", Default = null, HelpText = "Sort in descending order. ex: -dsd  ex: -s date -d")]
    public bool SortDescending { get; set; }
    [Option('t', "details", Default = false, HelpText = "Display Attributes, Last Write Time, and File Size")]
    public bool DisplayDetails { get; set; }

    [Option('w', "owner", Default = false, HelpText = "Display Owner (Windows Only)")]
    public bool DisplayOwner { get; set; }
}

[Verb("list-folders", isDefault: false, ["folders"], HelpText = "List Folders")]
public class ListFoldersArgs : BaseArgs, IRequest<EExitCode>, IGlobberDisplayFolderArgs
{
    [Option('b', "base-paths", HelpText = "One or more base paths for globbing. Default is the current working directory.")]
    public IEnumerable<string> BasePaths { get; set; } = new List<string>();

    [Option('s', "sort", Default = null, HelpText = "Sort by (N)ame, (E)xtension, (D)ate. ex: -sn  ex: -s name")]
    public ESortType? Sort { get; set; }

    [Option('d', "descending", Default = null, HelpText = "Sort in descending order. ex: -dsn  ex: -s name -d")]
    public bool SortDescending { get; set; }
    
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

    public ESortType? Sort { get; set; } // Defaults to null => Name Sort
    public bool SortDescending { get; set; } // Defaults to false => Ascending Sort
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

    public ESortType? Sort { get; set; } // Defaults to null => Name Sort
    public bool SortDescending { get; set; } // Defaults to false => Ascending Sort
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

    public ESortType? Sort { get; set; } // Defaults to null => Name Sort
    public bool SortDescending { get; set; } // Defaults to false => Ascending Sort
}

[Verb("encode-files", isDefault: false, ["encode"], HelpText = "Encode Files to Base64")]
public class EncodeFilesArgs : GlobToWriteFileBaseArgs, IGlobToWriteFileAndFactoryArgs, IRequest<EExitCode>
{
    [Option('t', "target-path", Required = true, HelpText = "Target folder to write encoded files. Default is the current working directory.")]
    public required string TargetFolder { get; set; }

    [Option('e', "encoded-extension", HelpText = "Extension added when encoding. Default is .base64")]
    public string EncodedExtensionDoNotAccessDirectly { get; set; } = Program.Base64Extension;
    public string EncodedExtension => this.EncodedExtensionDoNotAccessDirectly.StartsWith('.') ? this.EncodedExtensionDoNotAccessDirectly : $".{this.EncodedExtensionDoNotAccessDirectly}";

    [Option('r', "replace-files", Default = false, HelpText = "Toggle replacing files that already exist")]
    public bool ReplaceOnDuplicate { get; set; }

    public bool ErrorOnDuplicate
    {
        get => !this.ReplaceOnDuplicate;
        set => this.ReplaceOnDuplicate = !value;
    }

    [Option('f', "use-framework-globber", Default = false, HelpText = "Revert to DotNet Framework Globber")]
    public bool UseFrameworkGlobber { get; set; }

    public ESortType? Sort { get; set; } // Defaults to null => Name Sort
    public bool SortDescending { get; set; } // Defaults to false => Ascending Sort
}

[Verb("decode-files", isDefault: false, ["decode"], HelpText = "Decode Base64 Files")]
public class DecodeFilesArgs : GlobToWriteFileBaseArgs, IGlobToWriteFileAndFactoryArgs, IRequest<EExitCode>
{
    [Option('t', "target-path", Required = true, HelpText = "Target folder to write encoded files. Default is the current working directory.")]
    public required string TargetFolder { get; set; }

    [Option('e', "encoded-extension", HelpText = "Extension removed when decoding. Default is .base64")]
    public string EncodedExtensionDoNotAccessDirectly { get; set; } = Program.Base64Extension;
    public string EncodedExtension => this.EncodedExtensionDoNotAccessDirectly.StartsWith('.') ? this.EncodedExtensionDoNotAccessDirectly : $".{this.EncodedExtensionDoNotAccessDirectly}";

    [Option('d', "decoded-extension", HelpText = "Extension added when decoding. By default, no extension is added.")]
    public string? DecodedExtensionDoNotAccessDirectly { get; set; }
    public string? DecodedExtension => this.DecodedExtensionDoNotAccessDirectly == null || this.DecodedExtensionDoNotAccessDirectly.StartsWith('.') ? this.DecodedExtensionDoNotAccessDirectly : $".{this.DecodedExtensionDoNotAccessDirectly}";

    [Option('r', "replace-files", Default = false, HelpText = "Toggle replacing files that already exist")]
    public bool ReplaceOnDuplicate { get; set; }

    public bool ErrorOnDuplicate
    {
        get => !this.ReplaceOnDuplicate;
        set => this.ReplaceOnDuplicate = !value;
    }

    [Option('f', "use-framework-globber", Default = false, HelpText = "Revert to DotNet Framework Globber")]
    public bool UseFrameworkGlobber { get; set; }

    public ESortType? Sort { get; set; } // Defaults to null => Name Sort
    public bool SortDescending { get; set; } // Defaults to false => Ascending Sort
}

public class GlobToWriteFileBaseArgs : BaseArgs
{
    [Option('b', "base-path", HelpText = "Base path for globbing. Default is the current working directory.")]
    public required string BasePath
    {
        get => this.BasePaths.FirstOrDefault() ?? string.Empty;
        set => this.BasePaths = ImmutableList<string>.Empty.Add(value);
    }

    public IEnumerable<string> BasePaths { get; set; } = ImmutableList<string>.Empty.Add(".");
}

public abstract class BaseArgs
{
    [Value(0, Required = true, MetaName = "Include File Specs.", HelpText = "Initial positional args (1 or more) - Glob Formatting (e.g. **, *, ?)")]
    public IEnumerable<string> IncludeGlobPaths { get; set; } = new List<string>();

    [Option('x', "exclude", HelpText = "Exclude File Specs. (1 or more) - Glob Formatting (e.g. **, *, ?)")]
    public IEnumerable<string> ExcludeGlobPaths { get; set; } = new List<string>();

    [Option('q', "fully-qualified-paths", HelpText = "Output fully qualified paths")]
    public bool UseFullyQualifiedPaths { get; set; }

    [Option('c', "case-sensitive", Default = false, HelpText = "Toggle to add case sensitive path matching")]
    public bool CaseSensitive { get; set; }

    [Option('p', "allow-duplicates", Default = false, HelpText = "Toggle allowing duplicates if multiple base paths for faster output")]
    public bool AllowDuplicatesWhenMultipleBasePaths { get; set; }

    [Option('a', "abort-on-access-errors", Default = false, HelpText = "Toggle abort on file system access errors")]
    public bool AbortOnFileSystemAccessExceptions { get; set; }
}

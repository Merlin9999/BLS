using System.Text.RegularExpressions;
using CommandLine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CLConsole
{
    internal abstract class Program
    {
        private static readonly ServiceCollection Services = new();

        static int Main(string[] args)
        {
            int retValue = -1;
            IMediator mediator = InitializeDI();

            retValue = Parser.Default.ParseArguments<ListFilesArgs>(args)
            .MapResult(
                (ListFilesArgs options) => AsyncContext.Run(() => AsyncMain(options)),
                errors => -2);

            return retValue;
        }

        static async Task<int> AsyncMain<TOptions>(TOptions options)
            where TOptions : IRequest<int>
        {
            try
            {
                IMediator mediator = InitializeDI();
                return await mediator.Send(options);
            }
            catch (Exception exc)
            {
                Log.Error(exc, "Fatal error!");
                return -3;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
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

    [Verb("listfiles", isDefault: true, new[]{"files"}, HelpText = "List Files")]
    public class ListFilesArgs : IRequest<int>
    {

        [Value(0, HelpText = "Included Paths. At least 1 is required.")] 
        public IEnumerable<string> IncludeGlobPaths { get; set; } = new List<string>();

        [Option('x', "exclude", HelpText = "Excluded Paths (optional)")]
        public IEnumerable<string> ExcludeGlobPaths { get; set; } = new List<string>();

        [Option('b', "base-paths", HelpText = "One or more base paths for globbing. Default is the working directory.")]
        public IEnumerable<string> BasePaths { get; set; } = new List<string>();

        [Option('c', "case-sensitive", Default = false, HelpText = "Toggle to add case sensitive path matching.")]
        public bool CaseSensitive { get; set; }

        [Option('s', "sort", Default = false, HelpText = "Toggle to sort.")]
        public bool Sort { get; set; }

        [Option('d', "allow-duplicates", Default = false, HelpText = "Toggle allowing duplicates if multiple base paths for faster output.")]
        public bool AllowDuplicates { get; set; }
    }
}

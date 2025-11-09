using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Serilog.Events;

namespace SharpFocus.LanguageServer;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Verbose,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                standardErrorFromLevel: LogEventLevel.Verbose)
            .MinimumLevel.Verbose()
            .CreateLogger();

        Log.Information("SharpFocus Language Server starting...");

        try
        {
            var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
                options
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
                    .ConfigureLogging(x => x
                        .AddSerilog(Log.Logger)
                        .AddLanguageProtocolLogging()
                        .SetMinimumLevel(LogLevel.Debug))
                    .WithServices(ConfigureServices)
                    .WithHandler<Handlers.TextDocumentSyncHandler>()
                    .WithHandler<Handlers.FocusHandler>()
                    .WithHandler<Handlers.FocusModeHandler>()
                    .WithHandler<Handlers.FlowAnalysisHandler>()
                    .WithHandler<Handlers.BackwardSliceHandler>()
                    .WithHandler<Handlers.ForwardSliceHandler>()
                    .OnInitialize(async (server, request, token) =>
                    {
                        Log.Information("Language server initialized for client: {ClientName}",
                            request.ClientInfo?.Name ?? "Unknown");
                        await Task.CompletedTask;
                    })
            );

            Log.Information("SharpFocus Language Server started successfully");
            await server.WaitForExit;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Language server failed to start");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Services.IWorkspaceManager, Services.InMemoryWorkspaceManager>();
        services.AddSingleton<Services.IFlowAnalysisCache, Services.InMemoryFlowAnalysisCache>();
        services.AddSingleton<Services.DocumentContextLoader>();
        services.AddSingleton<Services.ControlFlowGraphFactory>();
        services.AddSingleton<Services.AnalysisCacheCoordinator>();
        services.AddSingleton<Core.Abstractions.IClassSummaryBuilder, Analysis.Builders.ClassSummaryBuilder>();
        services.AddSingleton<Services.IClassSummaryCache, Services.ClassSummaryCache>();
        services.AddSingleton<Services.ICrossMethodSliceComposer, Services.CrossMethodSliceComposer>();
        services.AddSingleton<Core.Abstractions.IPlaceExtractor, Core.Utilities.RoslynPlaceExtractor>();
        services.AddSingleton<Services.IPlaceResolver, Services.RoslynPlaceResolver>();
        services.AddSingleton<Services.IDataflowAnalysisRunner, Services.DataflowAnalysisRunner>();
        services.AddSingleton<Services.IAnalysisContextBuilder, Services.AnalysisContextBuilder>();
        services.AddSingleton<Services.Slicing.ISliceAliasResolver, Services.Slicing.SliceAliasResolver>();
        services.AddSingleton<Services.Slicing.ISliceComputationStrategy, Services.Slicing.BackwardSliceStrategy>();
        services.AddSingleton<Services.Slicing.ISliceComputationStrategy, Services.Slicing.ForwardSliceStrategy>();
        services.AddSingleton<Services.IDataflowSliceService, Services.DataflowSliceService>();
        services.AddSingleton<Services.FocusModeAnalysisService>();
        services.AddSingleton<Services.AggregatedFlowAnalysisService>();
        services.AddSingleton<Services.IAnalysisOrchestrator, Services.AnalysisOrchestrator>();

        services.AddSingleton<Handlers.FocusHandler>();
        services.AddSingleton<Handlers.FocusModeHandler>();
        services.AddSingleton<Handlers.FlowAnalysisHandler>();
        services.AddSingleton<Handlers.BackwardSliceHandler>();
        services.AddSingleton<Handlers.ForwardSliceHandler>();
        services.AddSingleton<Handlers.TextDocumentSyncHandler>();

        Log.Information("Configuring services");
    }
}

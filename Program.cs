using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crosswind;

using CommandLine;

/// This is the main part of Crosswind. All it really does:
///     - Load up the telemetry service selected by the user. Such as FSUIPC.
///     - Load up the EFB receiver service selected by the user. Such as Simlink
///     - Every second (or other interval selected by user)
///         - Read the telemetry from the telemetry interface.
///         - Send the telemetry to the EFB
///
/// The design is a bit over-engineering. It was just an exercise to learn some
/// of the paradigms that are common with C#.
///
/// TODO: Requires cleanup and documentation.
sealed class Options
{
    public enum ServiceType { FSUIPC }
    public enum ReceiverType { Simlink }

    [Option('v', "versose",
        HelpText = "Make output more verbose")]
    public bool Verbose { get; set; }
    
    [Option('s', "interval", 
        Required = true, 
        HelpText = "Refresh interval in seconds.")]
    public int interval { get; set; }

    [Option('i', "interface",
        Required = true,
        MetaValue = "<SERVICE>",
        HelpText = "Flight Simulator Interface. Supported: FSUIPC."
        )]
    public ServiceType Service { get; set; }
    
    [Option('t', "target",
        Required = true,
        MetaValue = "<RECEIVER>",
        HelpText = "Electronic Flight Bag Target. Supported: Simlink."
        )]
    public ReceiverType Receiver { get; set; }}

class Program
{
    private static ILogger<Program> logger;
    
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Welcome To Crosswind!");
        Console.WriteLine("Licensed under the MIT License. See LICENSE file for details.");
        
        var result = ParseArgs(args);
        if (result.Errors.Any())
        {
            return 1;
        }

        var services = PrepareServices(result);
        var provider = services.BuildServiceProvider();
        
        ITelemetryService<FSUIPCTelemetry> telemetryService = GetTelemetryService(provider, result);
        var telemetryReceiver = GetTelemetryReceiver(provider, result);

        logger.LogInformation($"Connecting to telemetry service {telemetryService.GetType()}...");
        if (telemetryService.Connect() == false)
        {
            logger.LogError($"Failed to connect to telemetry service {telemetryService.GetType()}!");
            return 1;
        }
        logger.LogInformation($"Starting to receiver service {telemetryReceiver.GetType()}...");
        if (telemetryReceiver.Start() == false)
        {
            logger.LogError($"Failed to connect to receiver service {telemetryReceiver.GetType()}!");
            return 1;
        }
       
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Cancellation Requested.");
            cts.Cancel();
        };
        
        while (cts.IsCancellationRequested == false)
        {
            logger.LogTrace($"Querying telemetry service...");
            if (telemetryService.Refresh() == false)
            {
                logger.LogError($"Failed to refresh telemetry {telemetryService.GetType()}!");
                return 1;
            }
            
            if (telemetryReceiver is IFSUIPCReceiver fsuipcReceiver)
            {
                logger.LogTrace($"Getting telemetry from FSUIPC...");
                FSUIPCTelemetry telemetry = telemetryService.GetTelemetry();
                
                logger.LogTrace($"Sending telemetry to {telemetryReceiver.GetType()}...");
                if (fsuipcReceiver.Send(telemetry) == false)
                {
                    logger.LogError($"Failed to send to receiver service {telemetryService.GetType()}!");
                    return 1;
                }                
            }
            
            await Task.Delay(result.Value.interval * 1000);
        }

        logger.LogInformation($"Disconnecting from telemetry service {telemetryService.GetType()}!");
        if (telemetryService.Disconnect() == false)
        {
            logger.LogError($"Failed to disconnect from telemetry service {telemetryService.GetType()}!");
            return 1;
        }
        logger.LogInformation($"Disconnecting from receiver service {telemetryReceiver.GetType()}...");
        if (telemetryReceiver.Stop() == false)
        {
            logger.LogError($"Failed to stop receiver service {telemetryReceiver.GetType()}!");
            return 1;
        };

        return 0;
    }

    private static ITelemetryReceiver GetTelemetryReceiver(ServiceProvider provider, ParserResult<Options> result)
    {
        logger.LogInformation("Retrieving receiver services...");
        ITelemetryReceiver telemetryReceiver = 
            provider.GetRequiredKeyedService<ITelemetryReceiver>(result.Value.Receiver);
        return telemetryReceiver;
    }

    private static ITelemetryService<FSUIPCTelemetry> GetTelemetryService(ServiceProvider provider, ParserResult<Options> result)
    {
        logger.LogInformation("Retrieving FSUIPC telemetry services...");
        return provider.GetRequiredKeyedService<ITelemetryService<FSUIPCTelemetry>>(result.Value.Service);
    }

    private static ServiceCollection PrepareServices(ParserResult<Options> result)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(result.Value.Verbose ? LogLevel.Trace : LogLevel.Information));
        logger = services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Logger prepared...");

        logger.LogInformation("Adding services...");
        services.AddKeyedTransient<ITelemetryService<FSUIPCTelemetry>, FSUIPCService>(Options.ServiceType.FSUIPC);
        services.AddKeyedTransient<ITelemetryReceiver, SimlinkReceiver>(Options.ReceiverType.Simlink);
        logger.LogInformation("Services added successfully...");
        
        return services;
    }

    private static ParserResult<Options> ParseArgs(string[] args)
    {
        var parser = new Parser(cfg =>
        {
            cfg.HelpWriter = Console.Out;
            cfg.AutoHelp = true;
            cfg.AutoVersion = true;
            cfg.CaseInsensitiveEnumValues = true;
        });

        var result = parser.ParseArguments<Options>(args);
        return result;
    }
}


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crosswind;

using CommandLine;

sealed class Options
{
    public enum ServiceType { FSUIPC }
    public enum ReceiverType { Simlink }

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
    static async Task Main(string[] args)
    {
        Console.WriteLine("Welcome To Crosswind!");
        
        var result = ParseArgs(args);

        var services = PrepareServices();
        var provider = services.BuildServiceProvider();
        
        ITelemetryService<FSUIPCTelemetry> telemetryService = 
            provider.GetRequiredKeyedService<ITelemetryService<FSUIPCTelemetry>>(result.Value.Service);
        ITelemetryReceiver telemetryReceiver = 
            provider.GetRequiredKeyedService<ITelemetryReceiver>(result.Value.Receiver);

        telemetryService.Connect();
        telemetryReceiver.Start();
        
        while (true)
        {
            telemetryService.Refresh();
            
            FSUIPCTelemetry telemetry = telemetryService.GetTelemetry();
            Console.WriteLine(telemetry);

            if (telemetryReceiver is IFSUIPCReceiver fsuipcReceiver)
            {
                fsuipcReceiver.Send(telemetry);                
            }
            
            await Task.Delay(result.Value.interval * 1000);
        }
    }

    private static ServiceCollection PrepareServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Information));

        services.AddKeyedTransient<ITelemetryService<FSUIPCTelemetry>, FSUIPCService>(Options.ServiceType.FSUIPC);
        services.AddKeyedTransient<ITelemetryReceiver, SimlinkReceiver>(Options.ReceiverType.Simlink);
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


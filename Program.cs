namespace Crosswind;

using CommandLine;

sealed class Options
{
    public enum ServiceType { FSUIPC }
    public enum ReceiverType { Simlink }
    
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
        
        var parser = new Parser(cfg =>
        {
            cfg.HelpWriter = Console.Out;
            cfg.AutoHelp = true;
            cfg.AutoVersion = true;
            cfg.CaseInsensitiveEnumValues = true;
        });
        
        var result = parser.ParseArguments<Options>(args);
        
        // var services = new ServiceCollection();
        // services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Information));
        // services.AddTransient<FSUIPCService>();
        // services.AddTransient<SimlinkReceiver>();
        //
        // var provider = services.BuildServiceProvider();
        //
        // var fsService = provider.GetRequiredService<FSUIPCService>();
        // fsService.Connect();
        //
        // var slService = provider.GetRequiredService<SimlinkReceiver>();
        // slService.Start();
        //
        // while (true)
        // {
        //     fsService.Refresh();
        //     
        //     var telemetry = fsService.GetTelemetry();
        //     Console.WriteLine(telemetry);
        //     
        //     // slService.Send(telemetry);
        //     await Task.Delay(1000);
        // }
    }
}


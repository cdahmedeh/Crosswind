// See https://aka.ms/new-console-template for more information

using LegacySimBridge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Welcome To LegacySimBridge!");

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Information));
        services.AddTransient<FSUIPCService>();
        services.AddTransient<SimlinkService>();

        var provider = services.BuildServiceProvider();
        
        var fsService = provider.GetRequiredService<FSUIPCService>();
        fsService.Connect();

        var slService = provider.GetRequiredService<SimlinkService>();
        slService.Connect();
        
        while (true)
        {
            fsService.Refresh();
            
            var coordinates = fsService.GetCoordinates();
            var squawkCode = fsService.GetSquawkCode();
            var speeds = fsService.GetSpeeds();
            var altitudes = fsService.GetAltitudes();
            var heading = fsService.GetHeading();
            var verticalSpeeds = fsService.GetVerticalSpeeds();
            var rates = fsService.GetRates();
            var simulatorStatus = fsService.GetSimulatorStatus();

            SimlinkTelemetry telemetry = new SimlinkTelemetry(
                squawkCode,
                coordinates.Latitude,
                coordinates.Longitude,
                (int) speeds.IndicatedAirSpeed,
                (int) speeds.TrueAirSpeed,
                (int) speeds.GroundSpeed,
                (int) altitudes.IndicatedAltitude,
                (int) altitudes.AboveGroundAltitude,
                (int) (100 * altitudes.AltimeterPressure),
                (int) heading.TrueHeading,
                (int) heading.IndicatedHeading,
                (int) verticalSpeeds.VerticalSpeed,
                (int) (rates.Pitch * -1),
                (int) (rates.Bank * -1),
                (int) (rates.TurnRate * -1),
                rates.GForce,
                simulatorStatus.Paused,
                simulatorStatus.SlewMode,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );
            
            Console.WriteLine(simulatorStatus);

            Console.WriteLine(telemetry);
            
            slService.Send(telemetry);
            
            await Task.Delay(1000);
        }
    }
}


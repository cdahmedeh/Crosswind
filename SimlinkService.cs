using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace LegacySimBridge;

public class SimlinkService
{
    private const string MappingName = "NGSIMCONNECT";
    private const int MappingSizeBytes = 4096;           // matches what you saw; keep it.
    private const string MutexName = "SIMLINK_PLUGIN";
    private static readonly Encoding Utf16 = Encoding.Unicode; // UTF-16LE on Windows
    
    private readonly ILogger<SimlinkService> _logger;
    
    private MemoryMappedViewAccessor _accessor;

    public SimlinkService(ILogger<SimlinkService> logger)
    {
        _logger = logger;
    }

    public bool Connect()
    {
        CreateSimlinkMutex();
        OpenSimlinkMemoryMap();
        return true;
    }
    
    public void Send(SimlinkTelemetry telemetry)
    {
        string json = EncodeSimlinkTelemetry(telemetry);
        WriteToSimlinkMemoryMap(json);
    }

    private void CreateSimlinkMutex()
    {
        using var pluginMutex = new Mutex(false, MutexName, out var createdNew);

        if (createdNew)
        {
            _logger.LogInformation($"Mutex {MutexName} didn't exist so I has has been created.");
        }
        else
        {
            _logger.LogInformation($"Mutx {MutexName} already exists so it will just be accessed.");
        }
        
        _logger.LogInformation($"Mutex {MutexName} has been accessed.");
    }

    private void OpenSimlinkMemoryMap()
    {
        try
        {
            using var mmf =
                MemoryMappedFile.CreateOrOpen(MappingName, MappingSizeBytes, MemoryMappedFileAccess.ReadWrite);

            _accessor = mmf.CreateViewAccessor(0, MappingSizeBytes, MemoryMappedFileAccess.Write);
            
            _logger.LogInformation($"Memory Map {MappingName} has been successfully opened.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex,
                $"Access to Memory Map {MappingName} has been denied. Check if you have sufficient privileges.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex,
                $"Unable to write to to Memory Map {MappingName}.");
        }
    }
    
    private void WriteToSimlinkMemoryMap(string text)
    {
        var bytes = Utf16.GetBytes(text);
        int totalNeeded = bytes.Length + 2; // +2 for trailing 0x00 0x00

        if (totalNeeded > MappingSizeBytes)
            throw new InvalidOperationException($"Banner too large ({totalNeeded} bytes). Reduce fields or increase mapping.");

        // Clear previous content (avoid leftover bytes if new text is shorter)
        _accessor.WriteArray(0, new byte[MappingSizeBytes], 0, MappingSizeBytes);

        // Write content then terminating wide NUL
        _accessor.WriteArray(0, bytes, 0, bytes.Length);
        _accessor.Write(bytes.Length, (ushort)0);

        // Optional: a tiny memory fence—ensures Simlink never sees a half-write window
        _accessor.Flush();
    }

    private string EncodeSimlinkTelemetry(SimlinkTelemetry telemetry)
    {
        var root = new Root(
            Platform: new Platform(
                Simlink: new Simlink("1.1.39.1902"),
                Os: new Os("windows"),
                Simulator: new Simulator("FSUIPC Compatible Simulator")
            ),
            Aircraft: new Aircraft(
                Squawk: telemetry.SquawkCode,
                VerticalSpeedFpm: telemetry.VerticalSpeed,
                GForce: telemetry.GForce,
                Altitude: new Altitude(
                    True: telemetry.IndicatedAltitude,
                    Indicated: telemetry.IndicatedAltitude,
                    Pressure: telemetry.AltimeterPressure,
                    Agl: telemetry.AboveGroundAltitude,
                    PressureXp12: 0.0,
                    SeaLevelPressureInHg: 29.92
                ),
                Position: new Position(
                    Latitude: telemetry.Latitude,
                    Longitude: telemetry.Longitude
                ),
                Speed: new Speed(
                    GroundSpeedKts: telemetry.GroundSpeed,
                    IndicatedKts: telemetry.IndicatedAirSpeed,
                    TrueKts: telemetry.TrueAirSpeed
                ),
                Status: new Status(
                    BankDeg: telemetry.Bank,
                    TurnRateDegPerSec: telemetry.TurnRate,
                    MagneticHeadingDeg: telemetry.IndicatedHeading,
                    PitchDeg: telemetry.Pitch,
                    TrueHeadingDeg: telemetry.TrueHeading,
                    TrueTrackDeg: telemetry.TrueHeading
                )
            ),
            System: new SystemState(
                Paused: telemetry.Paused ? 1 : 0,
                Slew: telemetry.Slew ? 1 : 0,
                TimeSeconds: telemetry.CurrentTime,
                TimeZoneOffsetSeconds: 0
            )
        );

        var options = new JsonSerializerOptions { };
        var json = JsonSerializer.Serialize(root, options);
        
        return json;
    }
}

public record SimlinkTelemetry (
    string SquawkCode,
    double Latitude,
    double Longitude,
    int IndicatedAirSpeed,
    int TrueAirSpeed,
    int GroundSpeed,
    int IndicatedAltitude,
    int AboveGroundAltitude,
    int AltimeterPressure,
    int TrueHeading,
    int IndicatedHeading,
    int VerticalSpeed,
    int Pitch,
    int Bank,
    int TurnRate,
    double GForce,
    bool Paused,
    bool Slew,
    long CurrentTime
    );
    
public sealed record Root(
    [property: JsonPropertyName("platform")] Platform Platform,
    [property: JsonPropertyName("aircraft")] Aircraft Aircraft,
    [property: JsonPropertyName("system")]   SystemState System
);

public sealed record Platform(
    [property: JsonPropertyName("simlink")]   Simlink Simlink,
    [property: JsonPropertyName("os")]        Os Os,
    [property: JsonPropertyName("simulator")] Simulator Simulator
);

public sealed record Simlink(
    [property: JsonPropertyName("version")] string Version
);

public sealed record Os(
    [property: JsonPropertyName("family")] string Family
);

public sealed record Simulator(
    [property: JsonPropertyName("family")] string Family
);

public sealed record Aircraft(
    [property: JsonPropertyName("squawk")]         string Squawk,
    [property: JsonPropertyName("vertical_speed")] int VerticalSpeedFpm,
    [property: JsonPropertyName("gforce")]         double GForce,
    [property: JsonPropertyName("altitude")]       Altitude Altitude,
    [property: JsonPropertyName("position")]       Position Position,
    [property: JsonPropertyName("speed")]          Speed Speed,
    [property: JsonPropertyName("status")]         Status Status
);

public sealed record Altitude(
    // "true" is a JSON key; the C# property can be named "True" and still map via the attribute.
    [property: JsonPropertyName("true")]      int True,
    [property: JsonPropertyName("indicated")] int Indicated,
    [property: JsonPropertyName("pressure")]  int Pressure,
    // Some fields carry sentinel -1 for “not available”; make them nullable if you prefer:
    [property: JsonPropertyName("agl")]       int Agl,                 // or int?
    [property: JsonPropertyName("pressureXP12")] double PressureXp12,
    [property: JsonPropertyName("slp")]       double SeaLevelPressureInHg
);

public sealed record Position(
    [property: JsonPropertyName("latitude")]  double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude
);

public sealed record Speed(
    [property: JsonPropertyName("gs")]  int GroundSpeedKts,
    [property: JsonPropertyName("ias")] int IndicatedKts,   // consider int? if -1 means N/A
    [property: JsonPropertyName("tas")] int TrueKts
);

public sealed record Status(
    [property: JsonPropertyName("bank")]              double BankDeg,
    [property: JsonPropertyName("turnrate")]          double TurnRateDegPerSec,
    [property: JsonPropertyName("magnetic_heading")]  double MagneticHeadingDeg,
    [property: JsonPropertyName("pitch")]             double PitchDeg,
    [property: JsonPropertyName("true_heading")]      int TrueHeadingDeg,
    [property: JsonPropertyName("true_track")]        int TrueTrackDeg // consider double?
);

public sealed record SystemState(
    [property: JsonPropertyName("paused")]    int Paused,      // 0/1; could be bool with custom converter
    [property: JsonPropertyName("slew")]      int Slew,        // 0/1; same note
    [property: JsonPropertyName("time")]      long TimeSeconds,
    [property: JsonPropertyName("tz_offset")] int TimeZoneOffsetSeconds
);
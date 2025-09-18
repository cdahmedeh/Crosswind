using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace LegacySimBridge;

/// This is for sending telemetry to Navigraph Simlink used by Navigraph Charts
/// and a few of their other tools. Simlink is the bridge between the flight
/// simulator and Charts.
///
/// Keep in mind that all of this research was done through clean-room
/// reverse-engineering. Navigraph doesn't provide any documentation about this.
/// Much of this is a bit of hack, and I'm taking only basic precautious.
///
/// Essentially, the simulator will send position data to Simlink, and then will
/// publish it to Navigraph's cloud. Navgiraph Charts then picks that up and
/// display the aircraft on the moving map. It seems a bit excessive, but I
/// think Navigraph does this so that you can use Charts on a different machine
/// or in the web browser.
///
/// In MSFS2020 and MSFS2024, Simlink connects directly via the SimConnect API.
/// However, for FSX and Prepar3D, a module needs to be installed in the sim
/// to work with Simlink. For X-Plane 10 and above, it also requires a plugin.
///
/// Essentially, what these plugins do, is that they write to a shared memory
/// file that Simlink reads. In the shared memory, is simply a JSON string of
/// various telemetry parameters like position, speed and other statuses. I'm a
/// bit uneasy that the map is system-wide. The mapping is under the name
/// NGSIMCONNECT. 
///
/// It also creates a mutex SIMLINK_PLUGIN which I'm guessing it uses it to
/// determine if a simulator is running. However, sometimes a simulator will
/// crash or simulator plugins will fail to remove it. It doesn't seem to do
/// anything. In fact, Simlink determines if a simulator is running only by
/// checking the last time data was sent. Though, the mutex needs to exist for
/// Simlink to start reading.
///
/// Starting from the record SimlinkJsonRoot, you'll get the structure that
/// corresponds to that JSON mapping. I left some notes down there in the record
/// definition where I annotated what the values means based on some best
/// guesses.

public class SimlinkReceiver(ILogger<SimlinkReceiver> logger) : ITelemetryReceiver, IFSUIPCReceiver
{
    private static readonly string MutexName = "SIMLINK_PLUGIN";
    private static readonly string MappingName = "NGSIMCONNECT";
    private static readonly int MappingSizeBytes = 4096;
    
    private const string DefaultSimlinkVersion = "1.1.39.1902";
    private const string DefaultOperatingSystem = "windows";
    private const string DefaultSimulatorPlatform = "FSUIPC Compatible Simulator";
    private const int DefaultTimeZoneOffset = 0;
    private const double DefaultSeaLevelPressure = 29.92;
       
    private MemoryMappedViewAccessor _accessor;
     
    private Mutex _pluginMutex;
    
    public bool Start()
    {
        try
        {
            // Basically what we're doing here is creating or opening the mutex that Simlink expects to start reading
            // from the memory mapped file. Normally, you'd check if the mutex already exists, and fail the connection
            // because in theory, the existence of the mutex another sim is running. However, under many circumstances,
            // it's not cleared, even by Navigraph's own plugins. So I'm just going to reuse if it exists and leave it
            // to the user to make sure no other sim is running. If Navigraph can't bother do this properly, then
            // neither am I.
            
            // One thing to keep in mind is that mutexes belong to specific threads. So there might be issues when
            // trying to access. In the future, I'll take that into consideration if problems keep popping up.

            _pluginMutex = new Mutex(false, MutexName, out var createdNew);

            if (createdNew)
                logger.LogInformation($"Mutex {MutexName} didn't exist so it has has been created.");
            else
                logger.LogInformation($"Mutex {MutexName} already exists so it will just be re-used.");

            logger.LogInformation($"Mutex {MutexName} has been accessed.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, $"Cannot create or access {MutexName} due to permissions issues.");
            return false;
        }
        catch (IOException ex)
        {
            logger.LogError(ex, $"Cannot write to {MutexName}. It is possible that another simulator is running.");
            return false;
        }

        try
        {
            // Then, we just need to open the memory file so that we can write to it later when the time comes to sent
            // telemetry data. Honestly, the mapping size is a complete guess, it's just based on how large the JSON
            // payload typically is.
            
            // Unlike the mutex, any thread should be able to access the memory-mapped file.
            
            using var mmf =
                MemoryMappedFile.CreateOrOpen(MappingName, MappingSizeBytes, MemoryMappedFileAccess.ReadWrite);
            _accessor = mmf.CreateViewAccessor(0, MappingSizeBytes, MemoryMappedFileAccess.Write);

            logger.LogInformation($"Memory-mapped file {MappingName} has been accessed successfully.");

        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, $"Cannot create or open {MappingName} due to permissions issues.");
            return false;
        }
        catch (IOException ex)
        {
            logger.LogError(ex, $"Cannot write to {MappingName}. Try running as administrator.");
            return false;
        }
            
        return true;
    }

    public bool Stop()
    {
        try
        {
            // Just being responsible and doing some cleanup. In my testing, this wasn't really necessary. I'll consider
            // making the failures more graceful.
            
            _pluginMutex.ReleaseMutex();
            _accessor.Dispose();
        }
        catch (ApplicationException ex)
        {
            logger.LogError(ex, $"Cannot release mutex {MutexName} because it is owned by another process.");
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            logger.LogError(ex, $"Mutex {MutexName} is already disposed.");
            return false;
        }
        
        return true;
    }
    
    public bool Send(FSUIPCTelemetry telemetry)
    {
        string json = EncodeFSUIPCTelemetry(telemetry);
        WriteToSimlinkMemoryMappedFile(json);
        return true;
    }
    
    private void WriteToSimlinkMemoryMappedFile(string text)
    {
        // This is where the magic happens, writing into the memory-mapped file that Simlink is expecting.
        
        // I tried to make it sane so that Simlink doesn't misbehave. I make sure that the payload isn't too big for the
        // size and make it NUL terminated. And I clear out the file with zeroes in case there's some partial remnants
        // that Simlink could read. And finally, flushing with a memory fence in case Simlink tries to read something
        // only partially written.
       
        // Encode the payload. It was clearly unicode.
        var bytes = Encoding.Unicode.GetBytes(text);

        // Clear the memory-mapped file with a bunch of zeroes.
        _accessor.WriteArray(0, new byte[MappingSizeBytes], 0, MappingSizeBytes);

        // Write into the memory-mapped file.
        _accessor.WriteArray(0, bytes, 0, bytes.Length);
        _accessor.Write(bytes.Length, (ushort)0);

        // Flushing in case Simlink tries to do partial reads.
        _accessor.Flush();
    }


    private string EncodeFSUIPCTelemetry(FSUIPCTelemetry telemetry)
    {
        var root = new SimlinkJsonRoot(
            Platform: new Platform(
                Simlink: new Simlink(DefaultSimlinkVersion),                    // Simlink version probably to cross-check protocol compatbility.
                Os: new Os(DefaultOperatingSystem),                       // Just the operating system that the simulator is running on. I can only think of X-Plane that can run outside of Windows
                Simulator: new Simulator(DefaultSimulatorPlatform)        // Reports the Flight Simulator that is providing the telemetry. Seems to have no effect on Charts, but probably used internally for data collection.
            ),
            Aircraft: new Aircraft(
                Squawk: telemetry.SquawkCode,                                   // Squawk code numbers as a string.
                VerticalSpeedFpm: telemetry.VerticalSpeed,                      // Vertical Speed in feet/minute
                GForce: telemetry.GForce,                                       // GForce where 1.0 is level flight.
                Altitude: new Altitude(
                    True: telemetry.IndicatedAltitude,                          // Probably used to tell altitude according to actual pressure. I don't know what they mean by 'true'. You'd think it's radio altitude. But in practice reusing indicated altitude works fine.
                    Indicated: telemetry.IndicatedAltitude,                     // Altitude according to the altimeter. In feet.
                    Pressure: (int)(telemetry.AltimeterPressure * 100),         // Current pressure set on altimeter in inHg. Decimal such as 29.92
                    Agl: telemetry.RadioAltitude,                               // Altitude above the ground. In feet.
                    PressureXp12: 0.0,                                          // XP12 probably has more precise pressure reading. Just 0.0 because it's unlikely to matter.
                    SeaLevelPressureInHg: DefaultSeaLevelPressure               // It's hard to get this value from telemetry sources. So just QNH.
                ),
                Position: new Position(
                    Latitude: telemetry.Latitude,                               // Latitude position in decimals.
                    Longitude: telemetry.Longitude                              // Longitude position in decimals.
                ),
                Speed: new Speed(
                    GroundSpeedKts: telemetry.GroundSpeed,                      // Speed in relation to the ground. In knots.
                    IndicatedKts: telemetry.IndicatedAirSpeed,                  // Speed seen in the cockpit. In knots.
                    TrueKts: telemetry.TrueAirSpeed                             // Speed ignoring the wind factor. In knots.
                ),
                Status: new Status(
                    BankDeg: telemetry.Bank,                                    // Bank angle in degrees. Positive value is banking to the right. Zero is level.
                    TurnRateDegPerSec: telemetry.TurnRate,                      // Turn rate in degrees per second. Positive value is banking to the right. Zero is level.
                    MagneticHeadingDeg: telemetry.IndicatedHeading,             // Angle in degrees as indicated in the cockpit.
                    PitchDeg: telemetry.Pitch,                                  // Pitch in degrees. Positive value is climbing. Zero is level.
                    TrueHeadingDeg: telemetry.TrueHeading,                      // True heading as it would be read by GPS.
                    TrueTrackDeg: telemetry.TrueHeading                         // In practice, seems to be the same as above.
                )
            ),
            System: new SystemState(
                Paused: telemetry.Paused ? 1 : 0,                               // Simulator is paused.
                Slew: telemetry.SlewMode ? 1 : 0,                               // Simulator is in slew mode.
                TimeSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),         // Current time in Unix format, Seconds precisions. Simlink uses this to know if the data is current or stale.
                TimeZoneOffsetSeconds: DefaultTimeZoneOffset                    // This is supposed to be the number of seconds offset from UTC. But I can't get it reliably from the sim. Having 0 with Unix time seems to do the trick though.
            )
        );

        var options = new JsonSerializerOptions { };
        var json = JsonSerializer.Serialize(root, options);
    
        return json;
    }

}

/// Simlinks expected JSON looks like this.
///
///{
///  "platform": {
///    "simlink": {
///      "version": "1.1.39.1902"
///    },
///    "os": {
///      "family": "osx"
///    },
///    "simulator": {
///      "family": "X-Plane"
///    }
///  },
///  "aircraft": {
///    "squawk": 2000,
///    "vertical_speed": -0,
///    "gforce": 1.0,
///    "altitude": {
///      "true": 1924,
///      "indicated": 1924,
///      "pressure": 1924,
///      "agl": -1,
///      "pressureXP12": 0.0,
///      "slp": 29.92
///    },
///    "position": {
///      "latitude": 47.25885,
///      "longitude": 11.33112
///    },
///    "speed": {
///      "gs": 0,
///      "ias": -1,
///      "tas": 1
///    },
///    "status": {
///      "bank": 0,
///      "turnrate": 0,
///      "magnetic_heading": 0,
///      "pitch": 0,
///      "true_heading": 0,
///      "true_track": -1
///    }
///  },
///  "system": {
///    "paused": 0,
///    "slew": 0,
///    "time": 55304808,
///    "tz_offset": -3600
///  }
///}
///
 
public sealed record SimlinkJsonRoot(
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
    [property: JsonPropertyName("true")]      int True,
    [property: JsonPropertyName("indicated")] int Indicated,
    [property: JsonPropertyName("pressure")]  int Pressure,
    [property: JsonPropertyName("agl")]       int Agl,
    [property: JsonPropertyName("pressureXP12")] double PressureXp12,
    [property: JsonPropertyName("slp")]       double SeaLevelPressureInHg
);

public sealed record Position(
    [property: JsonPropertyName("latitude")]  double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude
);

public sealed record Speed(
    [property: JsonPropertyName("gs")]  int GroundSpeedKts,
    [property: JsonPropertyName("ias")] int IndicatedKts,
    [property: JsonPropertyName("tas")] int TrueKts
);

public sealed record Status(
    [property: JsonPropertyName("bank")]              double BankDeg,
    [property: JsonPropertyName("turnrate")]          double TurnRateDegPerSec,
    [property: JsonPropertyName("magnetic_heading")]  double MagneticHeadingDeg,
    [property: JsonPropertyName("pitch")]             double PitchDeg,
    [property: JsonPropertyName("true_heading")]      int TrueHeadingDeg,
    [property: JsonPropertyName("true_track")]        int TrueTrackDeg
);

public sealed record SystemState(
    [property: JsonPropertyName("paused")]    int Paused,
    [property: JsonPropertyName("slew")]      int Slew,
    [property: JsonPropertyName("time")]      long TimeSeconds,
    [property: JsonPropertyName("tz_offset")] int TimeZoneOffsetSeconds
);
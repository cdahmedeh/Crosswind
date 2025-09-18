namespace Crosswind;

/// For retrieving data from a flight simulator through an API. 
public interface ITelemetryService<out T> where T : Telemetry
{
    bool Connect();
    bool Disconnect();
    bool Refresh();
    T GetTelemetry();
}

/// Something accepts telemetry data like an EFB
public interface ITelemetryReceiver
{
    bool Start();
    bool Stop();
}

public interface IFSUIPCReceiver
{
    bool Send(FSUIPCTelemetry telemetry);
}

public record Telemetry
{
    // Information
    public string SquawkCode { get; set; }             // Squawk code numbers as a string.

    // Position
    public double Longitude { get; set; }               // Longitude position in decimals.
    public double Latitude { get; set; }                // Latitude position in decimals.
    
    // Speeds
    public int IndicatedAirSpeed { get; set; }
    public int TrueAirSpeed { get; set; }               // Speed seen in the cockpit. In knots.
    public int GroundSpeed { get; set; }                // Speed in relation to the ground. In knots.
    public int VerticalSpeed { get; set; }              // Speed ignoring the wind factor. In knots.
    
    // Altitudes
    public int IndicatedAltitude { get; set; }          // Altitude according to the altimeter. In feet.
    public int RadioAltitude { get; set; }              // Altitude above the ground. In feet.
    public double AltimeterPressure { get; set; }       // Current pressure set on altimeter in inHg. Decimal such as 29.92
    
    // Headings
    public int IndicatedHeading { get; set; }           // Altitude according to the altimeter. In feet.
    public int TrueHeading { get; set; }                // True heading as it would be read by GPS.
    public int TurnRate { get; set; }                    // Turn rate in degrees per second. Positive value is banking to the right. Zero is level.
    
    // Attitudes
    public double Pitch { get; set; }                    // Pitch in degrees. Positive value is climbing. Zero is level.
    public double Bank { get; set; }                     // Bank angle in degrees. Positive value is banking to the right. Zero is level.
    public double GForce { get; set; }                   // GForce where 1.0 is level flight.
    
    // Statuses
    public bool Paused { get; set; }                     // Simulator is paused.
    public bool SlewMode { get; set; }                   // Simulator is in slew mode.
}